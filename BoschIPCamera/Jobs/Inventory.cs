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

        public JobResult ProcessJob(InventoryJobConfiguration jobConfiguration,
            SubmitInventoryUpdate submitInventoryUpdate)
        {
            _logger.MethodEntry(LogLevel.Debug);
            var client = new BoschIpCameraClient(jobConfiguration, jobConfiguration.CertificateStoreDetails, _pam, _logger);


            var files = client.ListCerts();
            _logger.LogDebug($"Found {files.Count} certificates");

            // get cert usage
            // need request cert usage lists for each cert usage type, and parse names from response to match types
            // key = cert name, value = cert usage enum
            var certUsages = client.GetCertUsageList();
            _logger.LogDebug($"Found {certUsages.Count} certificates with a matching usage");

            var inventory = files.Select(f => new CurrentInventoryItem()
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
            
            submitInventoryUpdate(inventory);
            return new JobResult()
            {
                Result = OrchestratorJobStatusJobResult.Success,
                JobHistoryId = jobConfiguration.JobHistoryId,
                FailureMessage = ""
            };
        }
    }
}