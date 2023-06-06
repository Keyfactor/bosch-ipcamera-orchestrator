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

    public class Reenrollment : IReenrollmentJobExtension
    {
        public string ExtensionName => "BoschIPCamera";
        private readonly ILogger _logger;
        private readonly IPAMSecretResolver _pam;

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

                // TODO: safe check required field is present
                var certName = jobConfiguration.JobProperties["Name"].ToString();

                //delete existing certificate
                // TODO: make checkbox to confirm overwrite?
                var returnCode = client.DeleteCertByName(certName);

                if (returnCode != "pass")
                {
                     sb.Append("Error deleting existing certificate " + certName + " on camera " +
                        jobConfiguration.CertificateStoreDetails.ClientMachine + " with error code " + returnCode);
                }

                // setup the CSR details
                var csrSubject = SetupCsrSubject(jobConfiguration.JobProperties["subjectText"].ToString());

                //generate the CSR on the camera
                returnCode = client.CertCreate(csrSubject, certName);

                if (returnCode != "pass")
                {
                    sb.Append("Error generating CSR for " + certName + " on camera " +
                        jobConfiguration.CertificateStoreDetails.ClientMachine + " with error code " + returnCode);
                }

                //get the CSR from the camera
                var csr = client.DownloadCsrFromCamera(certName);
                _logger.LogDebug("Downloaded CSR: " + csr);
              
                // sign CSR in Keyfactor
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
                client.UploadCert(certName +".cer", pemCert);

                //turn on 802.1x - "1" is on
                // TODO: make 802.1X a setting in store / entry parameters ?
                returnCode = client.Change8021XSettings("1");
                if (returnCode != "pass")
                {
                     sb.Append("Error setting 802.1x to on for " + certName + " on camera " +
                        jobConfiguration.CertificateStoreDetails.ClientMachine + " with error code " + returnCode);
                }

                //set cert usage
                // TODO: safe check required field is present (before doing all reenrollment work above)
                var certUsage = jobConfiguration.JobProperties["CertificateUsage"].ToString();

                Constants.CertificateUsage usageEnum = Constants.ParseCertificateUsage(certUsage);

                returnCode = client.SetCertUsage(certName, usageEnum);
                if (returnCode != "pass")
                {
                    sb.Append("Error setting certUsage of " + certUsage + "for store path " + certName + " on camera " +
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
