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

        private void UploadSync(string host, string username, string password, string fileName, string fileData)
        {
            _logger.LogTrace("Starting Cert upload to camera " + host);
            
            string boundary = "----------" + DateTime.Now.Ticks.ToString("x");
            string fileHeader = string.Format("Content-Disposition: form-data; name=\"certUsageUnspecified\"; filename=\"{0}\";\r\nContent-Type: application/x-x509-ca-cert\r\n\r\n", fileName);
            CredentialCache credCache = new CredentialCache();
            credCache.Add(new Uri("http://" + host), "Digest", new NetworkCredential(username, password));

            HttpWebRequest authRequest = (HttpWebRequest)WebRequest.Create("http://" + host + "/upload.htm");
            authRequest.Method = "GET";
            authRequest.Credentials = credCache;
            authRequest.PreAuthenticate = true;

            try
            {
                _logger.LogTrace("Get Auth call to camera on " + host);
                WebResponse response = authRequest.GetResponse();
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
            }

            bool certOnCamera = false;
            int count = 0;
            //keep trying until we get the cert on camera or try 5 times
            while (!certOnCamera && count <= 5)
            {
                try
                {
                    count++;
                    _logger.LogTrace("Post call to camera on " + host);
                    HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create("http://" + host + "/upload.htm");
                    httpWebRequest.Credentials = credCache;
                    httpWebRequest.ContentType = "multipart/form-data; boundary=" + boundary;
                    httpWebRequest.Method = "POST";
                    //httpWebRequest.PreAuthenticate = true;

                    Stream requestStream = httpWebRequest.GetRequestStream();
                    WriteToStream(requestStream, "--" + boundary + "\r\n");
                    WriteToStream(requestStream, fileHeader);
                    WriteToStream(requestStream, fileData);
                    WriteToStream(requestStream, "\r\n--" + boundary + "--\r\n");
                    //requestStream.Close();

                    HttpWebResponse myHttpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();


                    Stream responseStream = myHttpWebResponse.GetResponseStream();

                    StreamReader myStreamReader = new StreamReader(responseStream, Encoding.Default);

                    string pageContent = myStreamReader.ReadToEnd();

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
                Dictionary<string, string> csrSubject = setupCSRSubject(storeProperties);

                //setup the Camera Details
                client.setupStandardBoschIPcameraClient(jobConfiguration.CertificateStoreDetails.ClientMachine, jobConfiguration.ServerUsername,
                    jobConfiguration.ServerPassword, csrSubject, _logger);

                //delete existing certificate
                string returnCode = client.deleteCertByName(jobConfiguration.CertificateStoreDetails.StorePath);

                if (returnCode != "pass")
                {
                     sb.Append("Error deleting existing certificate " + jobConfiguration.CertificateStoreDetails.StorePath + " on camera " +
                        jobConfiguration.CertificateStoreDetails.ClientMachine + " with error code " + returnCode);
                }

                //generate the CSR on the camera
                returnCode = client.certCreate(jobConfiguration.CertificateStoreDetails.StorePath);

                if (returnCode != "pass")
                {
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
                UploadSync(jobConfiguration.CertificateStoreDetails.ClientMachine, jobConfiguration.ServerUsername, jobConfiguration.ServerPassword, jobConfiguration.CertificateStoreDetails.StorePath+".cer", cert);

                //turn on 802.1x - "1" is on
                returnCode = client.change8021xSettings("1");
                if (returnCode != "pass")
                {
                     sb.Append("Error setting 802.1x to on for " + jobConfiguration.CertificateStoreDetails.StorePath + " on camera " +
                        jobConfiguration.CertificateStoreDetails.ClientMachine + " with error code " + returnCode);
                }

                //set cert usage
                returnCode = client.setCertUsage(jobConfiguration.CertificateStoreDetails.StorePath, storeProperties.certUsage);
                if (returnCode != "pass")
                {
                    sb.Append("Error setting certUsage of " + storeProperties.certUsage + "for store path " + jobConfiguration.CertificateStoreDetails.StorePath + " on camera " +
                        jobConfiguration.CertificateStoreDetails.ClientMachine + " with error code " + returnCode);
                }

                //reboot the camera
                client.rebootCamera();
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
