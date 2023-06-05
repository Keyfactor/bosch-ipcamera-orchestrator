using Keyfactor.Extensions.Orchestrator.BoschIPCamera.Client;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.BoschIPCamera.Jobs
{
    //todo better error handling and job failure recording (sometimes job fails but says success)
    public class Management : IManagementJobExtension
    {  
        private readonly ILogger _logger;
        private readonly IPAMSecretResolver _pam;
        public string ExtensionName => "BoschIPCamera";

        public Management(IPAMSecretResolver pam)
        {
            _logger = LogHandler.GetClassLogger<Management>();
            _pam = pam;
        }

        public JobResult ProcessJob(ManagementJobConfiguration jobConfiguration)
        {
            _logger.MethodEntry(LogLevel.Debug);
            if (jobConfiguration.OperationType == Orchestrators.Common.Enums.CertStoreOperationType.Remove)
            {
                return removeCert(jobConfiguration);
            }

            return new JobResult()
            {
                Result = Orchestrators.Common.Enums.OrchestratorJobStatusJobResult.Failure,
                JobHistoryId = jobConfiguration.JobHistoryId,
                FailureMessage = $"Unsupported operation type {jobConfiguration.OperationType}"
            };
        }

        public JobResult removeCert(ManagementJobConfiguration jobConfiguration)
        {
            _logger.LogTrace($"Management Config {JsonConvert.SerializeObject(jobConfiguration)}");
            BoschIpCameraClient client = new BoschIpCameraClient(jobConfiguration, jobConfiguration.CertificateStoreDetails, _pam, _logger);

            // TODO: safe check required field is present
            var certName = jobConfiguration.JobProperties["Name"].ToString();

            //delete existing certificate
            _logger.LogDebug("Delete existing cert " + certName);
            string returnCode = client.DeleteCertByName(certName);

            if (returnCode == "fail")
            {
                _logger.LogError("Error deleting existing certificate " + certName + " on camera " +
                    jobConfiguration.CertificateStoreDetails.ClientMachine + " with error code " + returnCode);
                return new JobResult()
                {
                    Result = Orchestrators.Common.Enums.OrchestratorJobStatusJobResult.Failure,
                    JobHistoryId = jobConfiguration.JobHistoryId,
                    FailureMessage = "Error deleting existing certificate " + certName + " on camera " +
                    jobConfiguration.CertificateStoreDetails.ClientMachine + " with error code " + returnCode
                };
            }
            return new JobResult()
            {
                Result = Orchestrators.Common.Enums.OrchestratorJobStatusJobResult.Success,
                JobHistoryId = jobConfiguration.JobHistoryId,
                FailureMessage = ""
            };
        }
    }
}