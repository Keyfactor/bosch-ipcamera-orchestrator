using System.Collections.Generic;
using System.Linq;
using Keyfactor.Extensions.Orchestrator.BoschIPCamera.Client;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.BoschIPCamera.Jobs
{
    //todo better error handling and job failure recording (sometimes job fails but says success)
    public class Inventory : IInventoryJobExtension
    {
        private readonly ILogger<Inventory> _logger;
        private readonly IPAMSecretResolver _pam;

        public Inventory(ILogger<Inventory> logger, IPAMSecretResolver pam)
        {
            _logger = logger;
            _pam = pam;
        }

        public string ExtensionName => "BoschIPCamera";

        public JobResult ProcessJob(InventoryJobConfiguration jobConfiguration,
            SubmitInventoryUpdate submitInventoryUpdate)
        {
            _logger.MethodEntry(LogLevel.Debug);
            _logger.LogTrace($"Inventory Config {JsonConvert.SerializeObject(jobConfiguration)}");
            JsonConvert.DeserializeObject<boschIPCameraDetails>(jobConfiguration.CertificateStoreDetails.Properties);
            var client = new BoschIpCameraClient(jobConfiguration, jobConfiguration.CertificateStoreDetails, _pam, null, _logger);

            //setup the Camera Details
            _logger.LogDebug("Build default RestSharp client");

            var files = client.ListCerts();
            var inventory = files.Select(f => new CurrentInventoryItem()
            {
                Alias = f.Key,
                Certificates = new List<string>() { f.Value },
                PrivateKeyEntry = false,
                UseChainLevel = false
            }).ToList();
            
            // In the model where a bosch certstore represents just a single cert, this list needs to be trimmed.
            // inventory = inventory.Where(i => i.Alias == jobConfiguration.CertificateStoreDetails.StorePath).ToList();
            submitInventoryUpdate(inventory);
            return new JobResult()
            {
                Result = OrchestratorJobStatusJobResult.Success,
                FailureMessage = ""
            };
        }
    }
}