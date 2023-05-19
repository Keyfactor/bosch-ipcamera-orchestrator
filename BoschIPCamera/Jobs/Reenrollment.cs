using Keyfactor.Extensions.Orchestrator.BoschIPCamera.Client;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
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
    // Structure of KeyfactorAPI/Enrollment/CSR response
    struct EnrollResponse
    {
        public certificateInformation CertificateInformation;
        public Dictionary<string, object> Metadata;
    }

    // Nested structure within KeyfactorAPI/Enrollment/CSR response
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    struct certificateInformation
    {
        public string SerialNumber;
        public string IssuerDN;
        public string Thumbprint;
        public int KeyfactorID;
        public int KeyfactorRequestId;
        public string[] Certificates;
        public string RequestDisposition;
        public string DispositionMessage;
        public string EnrollmentContext;
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    struct boschIPCameraDetails
    {
        public string CN;
        public string SN;
        public string country;
        public string state;
        public string city;
        public string org;
        public string OU;
        public string CA;
        public string template;
        public string keyfactorHost;
        public string keyfactorUser;
        public string keyfactorPass;
        public string certUsage;
    }

    public class Reenrollment : IReenrollmentJobExtension
    {
        public string ExtensionName => "BoschIPCamera";
        private readonly ILogger<Reenrollment> _logger;

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

        private static T MakeWebRequest<T>(string url, string user, string pass, string bodyStr, string method = "POST", bool skipCertCheck=false)
        {
            if (skipCertCheck)
            {
                ServicePointManager.ServerCertificateValidationCallback = (obj, certificate, chain, errors) => (true);
            }

            var target = url.StartsWith("http") ? url : $"https://{url}";
            var req = WebRequest.Create(target);
            req.Method = method;
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}"));
            req.Headers.Add("Authorization", $"Basic {auth}");
            req.Headers.Add("X-Keyfactor-Requested-With", "APIClient");
            if (method == "POST")
            {
                var body = Encoding.ASCII.GetBytes(bodyStr);
                req.ContentLength = body.Length;
                req.ContentType = "application/json";
                using var s = req.GetRequestStream();
                s.Write(body, 0, body.Length);
                s.Close();
            }
            var responseStream = req.GetResponse().GetResponseStream();
            var resp = new StreamReader(responseStream ?? throw new InvalidOperationException()).ReadToEnd();
            var respObj = JsonConvert.DeserializeObject<T>(resp);
            return respObj;
        }

        // Format parameters and make web request to Keyfactor Enrollment API, returning the resulting certificate
/*
        private string EnrollCertificate(string csr, string keyfactorHost, string keyfactorUser, string keyfactorPassword, string caFullname, string templateShortname)
        {
            // CA fullname must have four backslashes (e.g. "keyfactor.local\\\\MY-CA-LOGICAL-NAME") to accommodate double-escaped JSON. Adjust if needed.
            if (!caFullname.Contains("\\\\")) {
                caFullname = caFullname.Replace("\\", "\\\\");
            }

            // Format API endpoint
            var keyfactorApiEndpoint = keyfactorHost + "/KeyfactorAPI/Enrollment/CSR";

            // Form CSR enrollment body with given CSR, CA, template, and keyfactor access info, with no SANs or metadata, and using current timestamp
            var body = $"{{\"CSR\": \"{csr}\",\"CertificateAuthority\": \"{caFullname}\",  \"IncludeChain\": false,  \"Metadata\": {{}},  \"Timestamp\": \"{DateTime.UtcNow.ToString("s")}\",  \"Template\": \"{templateShortname}\"}}";
            var resp = MakeWebRequest<EnrollResponse>(keyfactorApiEndpoint, keyfactorUser, keyfactorPassword, body);
            return resp.CertificateInformation.Certificates[0];
        }
*/

        public Reenrollment(ILogger<Reenrollment> logger)
        {
            _logger = logger;
        }

        public JobResult ProcessJob(ReenrollmentJobConfiguration jobConfiguration, SubmitReenrollmentCSR submitReenrollmentUpdate)
        {
            _logger.MethodEntry(LogLevel.Debug);
            return PerformReenrollment(jobConfiguration);
        }

        private JobResult PerformReenrollment(ReenrollmentJobConfiguration jobConfiguration)
        {

            try
            {
                var sb = new StringBuilder();
                sb.Append("");

                _logger.MethodEntry(LogLevel.Debug);
                _logger.LogTrace($"Reenrollment Config {JsonConvert.SerializeObject(jobConfiguration)}");
                var storeProperties = JsonConvert.DeserializeObject<boschIPCameraDetails>(jobConfiguration.CertificateStoreDetails.Properties);
                //setup the CSR details
                var csrSubject = SetupCsrSubject(storeProperties);

                var client = new BoschIpCameraClient(jobConfiguration.CertificateStoreDetails.ClientMachine, jobConfiguration.ServerUsername,
                    jobConfiguration.ServerPassword, csrSubject, _logger);

                //delete existing certificate
                var returnCode = client.DeleteCertByName(jobConfiguration.CertificateStoreDetails.StorePath);

                if (returnCode != "pass")
                {
                     sb.Append("Error deleting existing certificate " + jobConfiguration.CertificateStoreDetails.StorePath + " on camera " +
                        jobConfiguration.CertificateStoreDetails.ClientMachine + " with error code " + returnCode);
                }

                //generate the CSR on the camera
                returnCode = client.CertCreate(jobConfiguration.CertificateStoreDetails.StorePath);

                if (returnCode != "pass")
                {
                    sb.Append("Error generating CSR for " + jobConfiguration.CertificateStoreDetails.StorePath + " on camera " +
                        jobConfiguration.CertificateStoreDetails.ClientMachine + " with error code " + returnCode);
                }

                //get the CSR from the camera
                var responseContent = client.DownloadCsrFromCamera(jobConfiguration.CertificateStoreDetails.StorePath);
                _logger.LogDebug("Downloaded CSR: " + responseContent);
              
                //sign CSR in Keyfactor
                var body = $"{{\"CSR\": \"{responseContent}\",\"CertificateAuthority\": \"{storeProperties.CA}\",  \"IncludeChain\": false,  \"Metadata\": {{}},  \"Timestamp\": \"{DateTime.UtcNow:s}\",  \"Template\": \"{storeProperties.template}\"}}";
                var resp = MakeWebRequest<EnrollResponse>(storeProperties.keyfactorHost+"/KeyfactorAPI/Enrollment/CSR", storeProperties.keyfactorUser, jobConfiguration.CertificateStoreDetails.StorePassword, body, skipCertCheck: true);
                var cert = resp.CertificateInformation.Certificates[0];
                cert = cert.Substring(cert.IndexOf("-----", StringComparison.Ordinal));
                _logger.LogDebug(cert);

                //upload the signed cert to the camera
                UploadSync(jobConfiguration.CertificateStoreDetails.ClientMachine, jobConfiguration.ServerUsername, jobConfiguration.ServerPassword, jobConfiguration.CertificateStoreDetails.StorePath+".cer", cert);

                //turn on 802.1x - "1" is on
                returnCode = client.Change8021XSettings("1");
                if (returnCode != "pass")
                {
                     sb.Append("Error setting 802.1x to on for " + jobConfiguration.CertificateStoreDetails.StorePath + " on camera " +
                        jobConfiguration.CertificateStoreDetails.ClientMachine + " with error code " + returnCode);
                }

                //set cert usage
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
        private Dictionary<string, string> SetupCsrSubject(boschIPCameraDetails storeProperties)
        {
            var csrSubject = new Dictionary<string, string>
            {
                {"C", storeProperties.country},
                {"ST", storeProperties.state},
                {"L", storeProperties.city},
                {"O", storeProperties.org},
                {"OU", storeProperties.OU},
                {"CN", storeProperties.CN}
            };


            return csrSubject;
        }
    }
}
