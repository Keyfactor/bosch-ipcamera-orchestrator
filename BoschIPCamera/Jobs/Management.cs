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
        private readonly ILogger<Management> _logger;
        private readonly IPAMSecretResolver _pam;
        public string ExtensionName => "BoschIPCamera";

        public Management(ILogger<Management> logger, IPAMSecretResolver pam)
        {
            _logger = logger;
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
                FailureMessage = $"Unsupported operation type {jobConfiguration.OperationType}"
            };
        }

        public JobResult removeCert(ManagementJobConfiguration jobConfiguration)
        {
            _logger.LogTrace($"Management Config {JsonConvert.SerializeObject(jobConfiguration)}");
            JsonConvert.DeserializeObject<boschIPCameraDetails>(jobConfiguration.CertificateStoreDetails.Properties);
            BoschIpCameraClient client = new BoschIpCameraClient(jobConfiguration, jobConfiguration.CertificateStoreDetails, _pam, null, _logger);

            //setup the Camera Details
            _logger.LogDebug("Build default RestSharp client");

            //delete existing certificate
            _logger.LogDebug("Delete existing cert " + jobConfiguration.CertificateStoreDetails.StorePath);
            string returnCode = client.DeleteCertByName(jobConfiguration.CertificateStoreDetails.StorePath);

            if (returnCode == "fail")
            {
                _logger.LogError("Error deleting existing certificate " + jobConfiguration.CertificateStoreDetails.StorePath + " on camera " +
                    jobConfiguration.CertificateStoreDetails.ClientMachine + " with error code " + returnCode);
                return new JobResult()
                {
                    Result = Orchestrators.Common.Enums.OrchestratorJobStatusJobResult.Failure,
                    FailureMessage = "Error deleting existing certificate " + jobConfiguration.CertificateStoreDetails.StorePath + " on camera " +
                    jobConfiguration.CertificateStoreDetails.ClientMachine + " with error code " + returnCode
                };
            }
            return new JobResult()
            {
            Result = Orchestrators.Common.Enums.OrchestratorJobStatusJobResult.Success,
                FailureMessage = ""
            };
        }
    }
}