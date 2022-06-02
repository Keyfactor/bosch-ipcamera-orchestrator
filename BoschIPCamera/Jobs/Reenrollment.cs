using Keyfactor.Extensions.Orchestrator.BoschIPCamera.Client;
using Keyfactor.Extensions.Orchestrator.GcpCertManager;
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

    public class Reenrollment : IReenrollmentJobExtension
    {
        public string ExtensionName => "BoschIPCamera";
        private readonly ILogger<Reenrollment> _logger;

        // Perform web request with templated structure of returned data. Optionally performs HTTPS request without verifying server certificate.
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
                _logger.LogTrace($"Reenrollment CSR Config {JsonConvert.SerializeObject(submitReenrollmentUpdate)}");

                BoschIPcameraClient client = new BoschIPcameraClient();

                //need to parse the jobConfiguration for the cert details - create a map like in the BoschIPCamera class and pass it in

                //generate the CSR on the camera
                client.setupStandardBoschIPcameraClient(jobConfiguration.CertificateStoreDetails.ClientMachine, jobConfiguration.ServerUsername,
                    jobConfiguration.ServerPassword);
                client.certCreate("keyfactor");

                //get the CSR from the camera
                string responseContent = client.downloadCSRFromCamera(jobConfiguration.CertificateStoreDetails.ClientMachine, jobConfiguration.ServerUsername,
                    jobConfiguration.ServerPassword, "keyfactor");


                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Success,
                    JobHistoryId = jobConfiguration.JobHistoryId,
                    FailureMessage = sb.ToString()
                };


            }
            catch (Exception e)
            {
                _logger.LogError($"PerformInventory Error: {e.Message}");
                throw;
            }
            
        }

    }
}
