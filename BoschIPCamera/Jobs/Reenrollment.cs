using Keyfactor.Extensions.Orchestrator.BoschIPCamera.Client;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace Keyfactor.Extensions.Orchestrator.BoschIPCamera.Jobs
{
    // Structure of KeyfactorAPI/Enrollment/CSR response
    struct enrollResponse
    {
        public certificateInformation CertificateInformation;
        public Dictionary<String, Object> Metadata;
    }

    // Nested structure within KeyfactorAPI/Enrollment/CSR response
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

        // Perform web request with templated structure of returned data. Optionally performs HTTPS request without verifying server certificate.
        private void Upload(string host, string username, string password, string fileName, string fileData)
        {
            
            string boundary = "----------" + DateTime.Now.Ticks.ToString("x");
            string fileHeader = string.Format("Content-Disposition: form-data; name=\"certUsageUnspecified\"; filename=\"{0}\";\r\nContent-Type: application/x-x509-ca-cert\r\n\r\n", fileName);
            CredentialCache credCache = new CredentialCache();
            credCache.Add(new Uri("http://" + host), "Digest", new NetworkCredential(username, password));

            HttpWebRequest authRequest = (HttpWebRequest)WebRequest.Create("http://" + host + "/upload.htm");
            authRequest.Method = "GET";
            authRequest.Credentials = credCache;
            authRequest.PreAuthenticate = true;
            authRequest.GetResponse();

            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create("http://" + host + "/upload.htm");
            httpWebRequest.Credentials = credCache;
            httpWebRequest.ContentType = "multipart/form-data; boundary=" + boundary;
            httpWebRequest.Method = "POST";
            httpWebRequest.PreAuthenticate = true;

            IAsyncResult y = null;
            var x = httpWebRequest.BeginGetRequestStream((result) =>
            {
                try
                {
                    HttpWebRequest request = (HttpWebRequest)result.AsyncState;
                    using (Stream requestStream = request.EndGetRequestStream(result))
                    {
                        WriteToStream(requestStream, "--" + boundary + "\r\n");
                        _logger.LogDebug(fileHeader);
                        WriteToStream(requestStream, fileHeader);
                        _logger.LogDebug(fileData);
                        WriteToStream(requestStream, fileData);
                        WriteToStream(requestStream, "\r\n--" + boundary + "--\r\n");
                    }
                    y = request.BeginGetResponse(a =>
                    {
                        try
                        {
                            var response = request.EndGetResponse(a);
                            var responseStream = response.GetResponseStream();
                            using (var sr = new StreamReader(responseStream))
                            {
                                using (StreamReader streamReader = new StreamReader(response.GetResponseStream()))
                                {
                                    string responseString = streamReader.ReadToEnd();
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            _logger.LogError(e.Message);
                            throw;
                        }
                    }, null);
                    while (!y.IsCompleted)
                    {
                        Thread.Sleep(1000);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e.Message);
                    throw;
                }
            }, httpWebRequest);
            while (y == null || !y.IsCompleted)
            {
                Thread.Sleep(100);
            }
            while (!x.IsCompleted)
            {
                Thread.Sleep(100);
            }
        }

        private static void WriteToStream(Stream s, string txt)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(txt);
            s.Write(bytes, 0, bytes.Length);
        }

        private static T MakeWebRequest<T>(string url, string user, string pass, string bodyStr, string method = "POST", bool skipCertCheck=false)
        {
            if (skipCertCheck)
            {
                ServicePointManager.ServerCertificateValidationCallback = delegate (
                Object obj, X509Certificate certificate, X509Chain chain,
                SslPolicyErrors errors)
                {
                    return ( true );
                };
            }
            string target;
            if (url.StartsWith("http"))
            {
                target = url;
            }
            else
            {
                target = $"https://{url}";
            }
            WebRequest req = WebRequest.Create(target);
            req.Method = method;
            string auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}"));
            req.Headers.Add("Authorization", $"Basic {auth}");
            req.Headers.Add("X-Keyfactor-Requested-With", "APIClient");
            if (method == "POST")
            {
                byte[] body = Encoding.ASCII.GetBytes(bodyStr);
                req.ContentLength = body.Length;
                req.ContentType = "application/json";
                using (Stream s = req.GetRequestStream())
                {
                    s.Write(body, 0, body.Length);
                    s.Close();
                }
            }
            Stream responseStream = req.GetResponse().GetResponseStream();
            string resp = new StreamReader(responseStream).ReadToEnd();
            T respObj = JsonConvert.DeserializeObject<T>(resp);
            return respObj;
        }

        // Format parameters and make web request to Keyfactor Enrollment API, returning the resulting certificate
        private string enrollCertificate(string CSR, string keyfactorHost, string keyfactorUser, string keyfactorPassword, string CAFullname, string templateShortname)
        {
            // CA fullname must have four backslashes (e.g. "keyfactor.local\\\\MY-CA-LOGICAL-NAME") to accommodate double-escaped JSON. Adjust if needed.
            if (!CAFullname.Contains("\\\\")) {
                CAFullname = CAFullname.Replace("\\", "\\\\");
            }

            // Format API endpoint
            string keyfactorAPIEndpoint = keyfactorHost + "/KeyfactorAPI/Enrollment/CSR";

            // Form CSR enrollment body with given CSR, CA, template, and keyfactor access info, with no SANs or metadata, and using current timestamp
            string body = $"{{\"CSR\": \"{CSR}\",\"CertificateAuthority\": \"{CAFullname}\",  \"IncludeChain\": false,  \"Metadata\": {{}},  \"Timestamp\": \"{DateTime.UtcNow.ToString("s")}\",  \"Template\": \"{templateShortname}\"}}";
            enrollResponse resp = MakeWebRequest<enrollResponse>(keyfactorAPIEndpoint, keyfactorUser, keyfactorPassword, body);
            return resp.CertificateInformation.Certificates[0];
        }

        public Reenrollment(ILogger<Reenrollment> logger)
        {
            _logger = logger;
        }

        public JobResult ProcessJob(ReenrollmentJobConfiguration jobConfiguration, SubmitReenrollmentCSR submitReenrollmentUpdate)
        {
            _logger.MethodEntry(LogLevel.Debug);
            return PerformReenrollment(jobConfiguration, submitReenrollmentUpdate);
        }

        private JobResult PerformReenrollment(ReenrollmentJobConfiguration jobConfiguration, SubmitReenrollmentCSR submitReenrollmentUpdate)
        {

            try
            {
                var sb = new StringBuilder();
                sb.Append("");

                _logger.MethodEntry(LogLevel.Debug);
                _logger.LogTrace($"Reenrollment Config {JsonConvert.SerializeObject(jobConfiguration)}");
                boschIPCameraDetails storeProperties = JsonConvert.DeserializeObject<boschIPCameraDetails>(jobConfiguration.CertificateStoreDetails.Properties);
                BoschIPcameraClient client = new BoschIPcameraClient();

                //setup the CSR details
                _logger.LogDebug("Setup camera CSR Dictionary");
                Dictionary<string, string> csrSubject = setupCSRSubject(storeProperties);

                //setup the Camera Details
                _logger.LogDebug("Build default RestSharp client");
                client.setupStandardBoschIPcameraClient(jobConfiguration.CertificateStoreDetails.ClientMachine, jobConfiguration.ServerUsername,
                    jobConfiguration.ServerPassword, csrSubject, _logger);

                //delete existing certificate
                _logger.LogDebug("Delete existing cert " + jobConfiguration.CertificateStoreDetails.StorePath);
                string returnCode = client.deleteCertByName(jobConfiguration.CertificateStoreDetails.StorePath);

                if (returnCode == "fail")
                {
                    _logger.LogError("Error deleting existing certificate " + jobConfiguration.CertificateStoreDetails.StorePath + " on camera " +
                        jobConfiguration.CertificateStoreDetails.ClientMachine + " with error code " + returnCode);
                    sb.Append("Error deleting existing certificate " + jobConfiguration.CertificateStoreDetails.StorePath + " on camera " +
                        jobConfiguration.CertificateStoreDetails.ClientMachine + " with error code " + returnCode);
                }

                //generate the CSR on the camera
                _logger.LogDebug("Generate CSR on camera");
                returnCode = client.certCreate(jobConfiguration.CertificateStoreDetails.StorePath);

                if (returnCode == "fail")
                {
                    _logger.LogError("Error generating CSR for " + jobConfiguration.CertificateStoreDetails.StorePath + " on camera " +
                        jobConfiguration.CertificateStoreDetails.ClientMachine + " with error code " + returnCode);
                    sb.Append("Error generating CSR for " + jobConfiguration.CertificateStoreDetails.StorePath + " on camera " +
                        jobConfiguration.CertificateStoreDetails.ClientMachine + " with error code " + returnCode);
                }

                //get the CSR from the camera
                string responseContent = client.downloadCSRFromCamera(jobConfiguration.CertificateStoreDetails.ClientMachine, jobConfiguration.ServerUsername,
                    jobConfiguration.ServerPassword, jobConfiguration.CertificateStoreDetails.StorePath);
                _logger.LogDebug("Downloaded CSR: " + responseContent);
              
                //sign CSR in Keyfactor
                string body = $"{{\"CSR\": \"{responseContent}\",\"CertificateAuthority\": \"{storeProperties.CA}\",  \"IncludeChain\": false,  \"Metadata\": {{}},  \"Timestamp\": \"{DateTime.UtcNow.ToString("s")}\",  \"Template\": \"{storeProperties.template}\"}}";
                enrollResponse resp = MakeWebRequest<enrollResponse>(storeProperties.keyfactorHost+"/KeyfactorAPI/Enrollment/CSR", storeProperties.keyfactorUser, jobConfiguration.CertificateStoreDetails.StorePassword, body, skipCertCheck: true);
                string cert = resp.CertificateInformation.Certificates[0];
                cert = cert.Substring(cert.IndexOf("-----"));
                _logger.LogDebug(cert);

                //upload the signed cert to the camera
                _logger.LogDebug("Uploading cert");
                Upload(jobConfiguration.CertificateStoreDetails.ClientMachine, jobConfiguration.ServerUsername, jobConfiguration.ServerPassword, jobConfiguration.CertificateStoreDetails.StorePath+".cer", cert);

                //turn on 802.1x - "1" is on
                _logger.LogDebug("Turn on 802.1x");
                returnCode = client.change8021xSettings("1");
                if (returnCode == "fail")
                {
                    _logger.LogError("Error setting 802.1x to on for " + jobConfiguration.CertificateStoreDetails.StorePath + " on camera " +
                        jobConfiguration.CertificateStoreDetails.ClientMachine + " with error code " + returnCode);
                    sb.Append("Error setting 802.1x to on for " + jobConfiguration.CertificateStoreDetails.StorePath + " on camera " +
                        jobConfiguration.CertificateStoreDetails.ClientMachine + " with error code " + returnCode);
                }

                //set cert usage
                _logger.LogDebug("Set cert usage to " + storeProperties.certUsage);
                returnCode = client.setCertUsage(jobConfiguration.CertificateStoreDetails.StorePath, storeProperties.certUsage);
                if (returnCode == "fail")
                {
                    _logger.LogError("Error setting certUsage of " + storeProperties.certUsage + "for store path " + jobConfiguration.CertificateStoreDetails.StorePath + " on camera " +
                        jobConfiguration.CertificateStoreDetails.ClientMachine + " with error code " + returnCode);
                    sb.Append("Error setting certUsage of " + storeProperties.certUsage + "for store path " + jobConfiguration.CertificateStoreDetails.StorePath + " on camera " +
                        jobConfiguration.CertificateStoreDetails.ClientMachine + " with error code " + returnCode);
                }

                //reboot the camera
                _logger.LogDebug("Reboot camera");
                client.rebootCamera();

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
        private Dictionary<string, string> setupCSRSubject(boschIPCameraDetails storeProperties)
        {
            Dictionary<string, string> csrSubject = new Dictionary<string, string>();

            csrSubject.Add("C", storeProperties.country);
            csrSubject.Add("ST", storeProperties.state);
            csrSubject.Add("L", storeProperties.city);
            csrSubject.Add("O", storeProperties.org);
            csrSubject.Add("OU", storeProperties.OU);
            csrSubject.Add("CN", storeProperties.CN);

            return csrSubject;
        }
    }
}
