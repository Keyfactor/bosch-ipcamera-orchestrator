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
                _logger.MethodEntry(LogLevel.Debug);
                // TODO: CANNOT log entire config
                _logger.LogTrace($"Reenrollment Config {JsonConvert.SerializeObject(jobConfiguration)}");

                var client = new BoschIpCameraClient(jobConfiguration, jobConfiguration.CertificateStoreDetails, _pam, _logger);

                // TODO: safe check required field is present
                var certName = jobConfiguration.JobProperties["Name"].ToString();

                string returnCode;
                string errorMessage;
                string cameraUrl = jobConfiguration.CertificateStoreDetails.ClientMachine;

                // delete existing certificate if overwriting
                var overwrite = (bool) jobConfiguration.JobProperties["Overwrite"];
                if (overwrite)
                {
                    returnCode = client.DeleteCertByName(certName);

                    if (returnCode != "pass")
                    {
                        errorMessage = $"Error deleting existing certificate {certName} on camera {cameraUrl} with error code {returnCode}";
                        _logger.LogError(errorMessage);
                        return new JobResult
                        {
                            Result = OrchestratorJobStatusJobResult.Failure,
                            JobHistoryId = jobConfiguration.JobHistoryId,
                            FailureMessage = errorMessage
                        };
                    }
                }

                // setup the CSR details
                var csrSubject = SetupCsrSubject(jobConfiguration.JobProperties["subjectText"].ToString());

                //generate the CSR on the camera
                returnCode = client.CertCreate(csrSubject, certName);

                if (returnCode != "pass")
                {
                    errorMessage = $"Error generating CSR for {certName} on camera {cameraUrl} with error code {returnCode}";
                    _logger.LogError(errorMessage);
                    return new JobResult
                    {
                        Result = OrchestratorJobStatusJobResult.Failure,
                        JobHistoryId = jobConfiguration.JobHistoryId,
                        FailureMessage = errorMessage
                    };
                }

                //get the CSR from the camera
                var csr = client.DownloadCsrFromCamera(certName);
                _logger.LogTrace("Downloaded CSR: " + csr);

                // check that csr meets csr format
                // 404 message response can be returned instead
                if (!csr.StartsWith("-----BEGIN"))
                {
                    // error downloaded, no CSR present
                    // likely due to existing cert that was not marked to ovewrite (delete)
                    errorMessage = $"Error retrieving CSR from camera {cameraUrl} - got response: {csr}";
                    _logger.LogError(errorMessage);
                    return new JobResult
                    {
                        Result = OrchestratorJobStatusJobResult.Failure,
                        JobHistoryId = jobConfiguration.JobHistoryId,
                        FailureMessage = errorMessage
                    };
                }

                // sign CSR in Keyfactor
                var x509Cert = submitReenrollment.Invoke(csr);

                if (x509Cert == null)
                {
                    errorMessage = $"Error submitting CSR to Keyfactor. Certificate not received. CSR submitted: {csr}";
                    _logger.LogError(errorMessage);
                    return new JobResult
                    {
                        Result = OrchestratorJobStatusJobResult.Failure,
                        JobHistoryId = jobConfiguration.JobHistoryId,
                        FailureMessage = errorMessage
                    };
                }

                // build PEM content
                StringBuilder pemBuilder = new StringBuilder();
                pemBuilder.AppendLine("-----BEGIN CERTIFICATE-----");
                pemBuilder.AppendLine(Convert.ToBase64String(x509Cert.RawData, Base64FormattingOptions.InsertLineBreaks));
                pemBuilder.AppendLine("-----END CERTIFICATE-----");
                var pemCert = pemBuilder.ToString();

                pemCert = pemCert.Replace("\r", "");
                _logger.LogTrace("Uploading cert: " + pemCert);

                // upload the signed cert to the camera
                client.UploadCert(certName +".cer", pemCert);

                // turn on 802.1x
                returnCode = client.Change8021XSettings(true);
                if (returnCode != "pass")
                {
                     errorMessage = $"Error setting 802.1x to on for {certName} on camera {cameraUrl} with error code {returnCode}";
                    _logger.LogError(errorMessage);
                    return new JobResult
                    {
                        Result = OrchestratorJobStatusJobResult.Failure,
                        JobHistoryId = jobConfiguration.JobHistoryId,
                        FailureMessage = errorMessage
                    };
                }

                //set cert usage
                // TODO: safe check required field is present (before doing all reenrollment work above)
                var certUsage = jobConfiguration.JobProperties["CertificateUsage"].ToString();

                Constants.CertificateUsage usageEnum = Constants.ParseCertificateUsage(certUsage);

                returnCode = client.SetCertUsage(certName, usageEnum);
                if (returnCode != "pass")
                {
                    errorMessage = $"Error setting certUsage of {certUsage} for certificate {certName} on camera {cameraUrl} with error code {returnCode}";
                    _logger.LogError(errorMessage);
                    return new JobResult
                    {
                        Result = OrchestratorJobStatusJobResult.Failure,
                        JobHistoryId = jobConfiguration.JobHistoryId,
                        FailureMessage = errorMessage
                    };
                }

                //reboot the camera
                client.RebootCamera();
                if (returnCode != "pass")
                {
                    errorMessage = $"Error rebooting camera {cameraUrl} with error code {returnCode}";
                    _logger.LogError(errorMessage);
                    return new JobResult
                    {
                        Result = OrchestratorJobStatusJobResult.Failure,
                        JobHistoryId = jobConfiguration.JobHistoryId,
                        FailureMessage = errorMessage
                    };
                }

                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Success,
                    JobHistoryId = jobConfiguration.JobHistoryId,
                    FailureMessage = ""
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"PerformReenrollment Error: {e.Message}");
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Failure,
                    JobHistoryId = jobConfiguration.JobHistoryId,
                    FailureMessage = e.Message
                };
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
