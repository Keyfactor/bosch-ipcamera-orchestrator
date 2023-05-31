using Keyfactor.Extensions.Orchestrator.BoschIPCamera.Client;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Text;

namespace Keyfactor.Extensions.Orchestrator.BoschIPCamera.Jobs
{
    //todo better error handling and job failure recording (sometimes job fails but says success)

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    struct boschIPCameraDetails
    {
        public string certUsage;
    }

    public class Reenrollment : IReenrollmentJobExtension
    {
        public string ExtensionName => "BoschIPCamera";
        private readonly ILogger _logger;
        private readonly IPAMSecretResolver _pam;

        private void UploadSync(string host, string username, string password, string fileName, string fileData)
        {
            _logger.LogTrace("Starting Cert upload to camera " + host);
            
            var boundary = "----------" + DateTime.Now.Ticks.ToString("x");
            var fileHeader =
                $"Content-Disposition: form-data; name=\"certUsageUnspecified\"; filename=\"{fileName}\";\r\nContent-Type: application/x-x509-ca-cert\r\n\r\n";
            var credCache = new CredentialCache
            {
                {new Uri("https://" + host), "Digest", new NetworkCredential(username, password)}
            };

            var authRequest = (HttpWebRequest)WebRequest.Create("https://" + host + "/upload.htm");
            authRequest.Method = "GET";
            authRequest.Credentials = credCache;
            authRequest.PreAuthenticate = true;

            try
            {
                _logger.LogTrace("Get Auth call to camera on " + host);
                authRequest.GetResponse();
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
            }

            var count = 0;
            //keep trying until we get the cert on camera or try 5 times
            while (count <= 5)
            {
                try
                {
                    count++;
                    _logger.LogTrace("Post call to camera on " + host);
                    var httpWebRequest = (HttpWebRequest)WebRequest.Create("https://" + host + "/upload.htm");
                    httpWebRequest.Credentials = credCache;
                    httpWebRequest.ContentType = "multipart/form-data; boundary=" + boundary;
                    httpWebRequest.Method = "POST";
                    //httpWebRequest.PreAuthenticate = true;

                    var requestStream = httpWebRequest.GetRequestStream();
                    WriteToStream(requestStream, "--" + boundary + "\r\n");
                    WriteToStream(requestStream, fileHeader);
                    WriteToStream(requestStream, fileData);
                    WriteToStream(requestStream, "\r\n--" + boundary + "--\r\n");
                    //requestStream.Close();

                    var myHttpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();


                    var responseStream = myHttpWebResponse.GetResponseStream();

                    var myStreamReader = new StreamReader(responseStream ?? throw new InvalidOperationException(), Encoding.Default);

                    myStreamReader.ReadToEnd();

                    myStreamReader.Close();
                    responseStream.Close();

                    myHttpWebResponse.Close();
                    return;
                }
                catch (Exception e)
                {
                    _logger.LogError(e.Message);
                    _logger.LogTrace("Failed to push cert on attempt " + count + " trying again if less than or equal to 5");
                }
            }
        }

        private static void WriteToStream(Stream s, string txt)
        {
            var bytes = Encoding.UTF8.GetBytes(txt);
            s.Write(bytes, 0, bytes.Length);
        }

        public Reenrollment(IPAMSecretResolver pam)
        {
            _logger = LogHandler.GetClassLogger<Reenrollment>();
            _pam = pam;
        }

        public JobResult ProcessJob(ReenrollmentJobConfiguration jobConfiguration, SubmitReenrollmentCSR submitReenrollmentUpdate)
        {
            _logger.MethodEntry(LogLevel.Debug);
            return PerformReenrollment(jobConfiguration, submitReenrollmentUpdate);
        }

        private JobResult PerformReenrollment(ReenrollmentJobConfiguration jobConfiguration, SubmitReenrollmentCSR submitReenrollment)
        {

            try
            {
                var sb = new StringBuilder();
                sb.Append("");

                _logger.MethodEntry(LogLevel.Debug);
                _logger.LogTrace($"Reenrollment Config {JsonConvert.SerializeObject(jobConfiguration)}");

                var client = new BoschIpCameraClient(jobConfiguration, jobConfiguration.CertificateStoreDetails, _pam, _logger);

                //delete existing certificate
                // TODO: make checkbox to confirm overwrite?
                var returnCode = client.DeleteCertByName(jobConfiguration.CertificateStoreDetails.StorePath);

                if (returnCode != "pass")
                {
                     sb.Append("Error deleting existing certificate " + jobConfiguration.CertificateStoreDetails.StorePath + " on camera " +
                        jobConfiguration.CertificateStoreDetails.ClientMachine + " with error code " + returnCode);
                }

                //setup the CSR details
                var csrSubject = SetupCsrSubject(jobConfiguration.JobProperties["subjectText"].ToString());

                //generate the CSR on the camera
                returnCode = client.CertCreate(csrSubject, jobConfiguration.CertificateStoreDetails.StorePath);

                if (returnCode != "pass")
                {
                    sb.Append("Error generating CSR for " + jobConfiguration.CertificateStoreDetails.StorePath + " on camera " +
                        jobConfiguration.CertificateStoreDetails.ClientMachine + " with error code " + returnCode);
                }

                //get the CSR from the camera
                var csr = client.DownloadCsrFromCamera(jobConfiguration.CertificateStoreDetails.StorePath);
                _logger.LogDebug("Downloaded CSR: " + csr);
              
                //sign CSR in Keyfactor
                // TODO: use Reenrollment arg to submit CSR instead of custom API call
                // TODO: error handle when not receiving Cert from Keyfactor
                var x509Cert = submitReenrollment.Invoke(csr);

                // build PEM content
                StringBuilder pemBuilder = new StringBuilder();
                pemBuilder.AppendLine("-----BEGIN CERTIFICATE-----");
                pemBuilder.AppendLine(Convert.ToBase64String(x509Cert.RawData, Base64FormattingOptions.InsertLineBreaks));
                pemBuilder.AppendLine("-----END CERTIFICATE-----");
                var pemCert = pemBuilder.ToString();

                pemCert = pemCert.Replace("\r", "");
                _logger.LogDebug(pemCert);

                //upload the signed cert to the camera
                UploadSync(jobConfiguration.CertificateStoreDetails.ClientMachine, jobConfiguration.ServerUsername, jobConfiguration.ServerPassword, jobConfiguration.CertificateStoreDetails.StorePath+".cer", pemCert);

                //turn on 802.1x - "1" is on
                // TODO: make 802.1X a setting in store / entry parameters ?
                returnCode = client.Change8021XSettings("1");
                if (returnCode != "pass")
                {
                     sb.Append("Error setting 802.1x to on for " + jobConfiguration.CertificateStoreDetails.StorePath + " on camera " +
                        jobConfiguration.CertificateStoreDetails.ClientMachine + " with error code " + returnCode);
                }

                //set cert usage
                // TODO: use readable names, multiple choice for Cert Usage, decode to correct HEX values based on constants
                // TODO: change cert usage to entry parameter
                var storeProperties = JsonConvert.DeserializeObject<boschIPCameraDetails>(jobConfiguration.CertificateStoreDetails.Properties);
                returnCode = client.SetCertUsage(jobConfiguration.CertificateStoreDetails.StorePath, storeProperties.certUsage);
                if (returnCode != "pass")
                {
                    sb.Append("Error setting certUsage of " + storeProperties.certUsage + "for store path " + jobConfiguration.CertificateStoreDetails.StorePath + " on camera " +
                        jobConfiguration.CertificateStoreDetails.ClientMachine + " with error code " + returnCode);
                }

                //reboot the camera
                client.RebootCamera();
                if (returnCode != "pass")
                {
                    sb.Append("Error rebooting camera " + jobConfiguration.CertificateStoreDetails.ClientMachine + " with error code " + returnCode);
                }

                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Success,
                    JobHistoryId = jobConfiguration.JobHistoryId,
                    FailureMessage = sb.ToString()
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"PerformReenrollment Error: {e.Message}");
                throw;
            }
            
        }
        private Dictionary<string, string> SetupCsrSubject(string subjectText)
        {
            var csrSubject = new Dictionary<string, string>();
            _logger.LogTrace($"Parsing subject text: {subjectText}");
            var splitSubject = subjectText.Split(',');
            foreach (string subjectElement in splitSubject)
            {
                _logger.LogTrace($"Splitting subject element: {subjectElement}");
                var splitSubjectElement = subjectElement.Split('=');
                var name = splitSubjectElement[0].Trim();
                var value = splitSubjectElement[1].Trim();
                _logger.LogTrace($"Adding subject element: '{name}' with value '{value}'");
                csrSubject.Add(name, value);
            }

            return csrSubject;
        }
    }
}
