using System;

using Keyfactor.Extensions.Orchestrator.BoschIPCamera.Client;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.BoschIPCamera.Jobs
{
    public class Management : IManagementJobExtension
    {  
        private readonly ILogger<Management> _logger;
        public string ExtensionName => "BoschIPCamera";

        public Management(ILogger<Management> logger)
        {
            _logger = logger;
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
            boschIPCameraDetails storeProperties = JsonConvert.DeserializeObject<boschIPCameraDetails>(jobConfiguration.CertificateStoreDetails.Properties);
            BoschIPcameraClient client = new BoschIPcameraClient();

            //setup the Camera Details
            _logger.LogDebug("Build default RestSharp client");
            client.setupStandardBoschIPcameraClient(jobConfiguration.CertificateStoreDetails.ClientMachine, jobConfiguration.ServerUsername,
                jobConfiguration.ServerPassword, null, _logger);

            //delete existing certificate
            _logger.LogDebug("Delete existing cert " + jobConfiguration.CertificateStoreDetails.StorePath);
            string returnCode = client.deleteCertByName(jobConfiguration.CertificateStoreDetails.StorePath);

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