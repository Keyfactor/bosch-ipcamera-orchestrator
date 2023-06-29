// Copyright 2023 Keyfactor
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Keyfactor.Extensions.Orchestrator.BoschIPCamera.Client;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.BoschIPCamera.Jobs
{
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