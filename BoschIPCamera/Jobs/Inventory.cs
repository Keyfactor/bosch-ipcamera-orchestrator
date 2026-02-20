// Copyright 2026 Keyfactor
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

using System;
using System.Collections.Generic;
using System.Linq;
using Keyfactor.Extensions.Orchestrator.BoschIPCamera.Client;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.Orchestrator.BoschIPCamera.Jobs
{
    public class Inventory : IInventoryJobExtension
    {
        private readonly ILogger _logger;
        private readonly IPAMSecretResolver _pam;

        public Inventory(IPAMSecretResolver pam)
        {
            _logger = LogHandler.GetClassLogger<Inventory>();
            _pam = pam;
        }

        public string ExtensionName => "BoschIPCamera";

        public JobResult ProcessJob(InventoryJobConfiguration jobConfiguration, SubmitInventoryUpdate submitInventoryUpdate)
        {
            List<CurrentInventoryItem> inventoryItems;
            
            try
            {
                _logger.MethodEntry(LogLevel.Debug);
                _logger.LogTrace(
                    $"Begin Inventory for Client Machine {jobConfiguration.CertificateStoreDetails.ClientMachine}...");

                var client = new BoschIpCameraClient(jobConfiguration, jobConfiguration.CertificateStoreDetails, _pam,
                    _logger);

                var files = client.ListCerts();
                _logger.LogDebug($"Found {files.Count} certificates");

                // get cert usage
                // need request cert usage lists for each cert usage type, and parse names from response to match types
                // key = cert name, value = cert usage enum
                var certUsages = client.GetCertUsageList();
                _logger.LogDebug($"Found {certUsages.Count} certificates with a matching usage");

                inventoryItems = files.Select(f => new CurrentInventoryItem()
                {
                    Alias = f.Key,
                    Certificates = new List<string>() { f.Value },
                    PrivateKeyEntry = false,
                    UseChainLevel = false,
                    Parameters = new Dictionary<string, object>
                    {
                        { "Name", f.Key },
                        { "CertificateUsage", certUsages.ContainsKey(f.Key) ? certUsages[f.Key].ToReadableText() : "" }
                    }
                }).ToList();
            }
            catch (Exception e1)
            {
                // Status: 2=Success, 3=Warning, 4=Error
                return new JobResult() 
                { 
                    Result = OrchestratorJobStatusJobResult.Failure, 
                    JobHistoryId = jobConfiguration.JobHistoryId, 
                    FailureMessage = $"Inventory Job Failed During Inventory Item Creation: {e1.Message} - Refer to the Orchestrator logs and Command API logs for more detailed information." 
                };
            }
            
            bool success = true;
            Exception? error = null;
            
            try
            {
                // Sends inventoried certificates back to KF Command
                _logger.LogTrace("Submitting Inventory to Keyfactor via submitInventory.Invoke");
                success = submitInventoryUpdate.Invoke(inventoryItems);
            }
            catch (Exception e1)
            {
                success = false;
                error = e1;
            }
                
            if (!success)
            {
                // ** NOTE: If the cause of the submitInventory.Invoke exception is a communication issue between the Orchestrator server and the Command server, the job status returned here
                //  may not be reflected in Keyfactor Command.
                return new JobResult() 
                { 
                    Result = OrchestratorJobStatusJobResult.Failure, 
                    JobHistoryId = jobConfiguration.JobHistoryId, 
                    FailureMessage = $"Inventory Job Failed During Inventory Item Submission: {(error is not null ? error.ToString() : "Unknown error occurred.")} - " +
                                     $"Refer to the Orchestrator logs and Command API logs for more detailed information." };
            }

            _logger.LogTrace("Successfully submitted Inventory To Keyfactor via submitInventory.Invoke");
            return new JobResult()
            {
                Result = OrchestratorJobStatusJobResult.Success,
                JobHistoryId = jobConfiguration.JobHistoryId
            };
        }
    }
}