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
            _logger.LogTrace($"Inventory Config {JsonConvert.SerializeObject(jobConfiguration)}");
            _logger.LogTrace("Parsed Properties");
            var client = new BoschIpCameraClient(jobConfiguration, jobConfiguration.CertificateStoreDetails, _pam, _logger);

            //setup the Camera Details
            _logger.LogDebug("Build default RestSharp client");

            var files = client.ListCerts();

            // get cert usage
            // need request cert usage lists for each cert usage type, and parse names from response to match types
            // key = cert name, value = cert usage
            // TODO: returned cert usage should be Enum
            var certUsages = client.GetCertUsageList();

            var inventory = files.Select(f => new CurrentInventoryItem()
            {
                Alias = f.Key,
                Certificates = new List<string>() { f.Value },
                PrivateKeyEntry = false,
                UseChainLevel = false,
                Parameters = new Dictionary<string, object>
                {
                    { "Name", f.Key },
                    { "CertificateUsage", certUsages.ContainsKey(f.Key) ? ReadCertificateUsage(certUsages[f.Key]) : "" }
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

        private string ReadCertificateUsage(string usageCode)
        {
            Constants.CertificateUsage usageEnum = Constants.ParseCertificateUsage(usageCode);
            return usageEnum.ToReadableText();
        }
    }
}