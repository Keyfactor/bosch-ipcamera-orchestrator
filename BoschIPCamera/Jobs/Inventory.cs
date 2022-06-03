using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using Keyfactor.Extensions.Orchestrator.BoschIPCamera.Client;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.BoschIPCamera.Jobs
{
    public class Inventory : IInventoryJobExtension
    {
        private readonly ILogger<Inventory> _logger;

        public Inventory(ILogger<Inventory> logger)
        {
            _logger = logger;
        }

        public string ExtensionName => "BoschIPCamera";

        public JobResult ProcessJob(InventoryJobConfiguration jobConfiguration,
            SubmitInventoryUpdate submitInventoryUpdate)
        {
            _logger.MethodEntry(LogLevel.Debug);
            return new JobResult();
           // return PerformInventory(jobConfiguration, submitInventoryUpdate);
        }

        //private JobResult PerformInventory(InventoryJobConfiguration config, SubmitInventoryUpdate submitInventory)
        //{
        //    try
        //    {
        //        _logger.MethodEntry(LogLevel.Debug);
        //        _logger.LogTrace($"Inventory Config {JsonConvert.SerializeObject(config)}");
        //        _logger.LogTrace($"Client Machine: {config.CertificateStoreDetails.ClientMachine} ApiKey: {config.ServerPassword}");

        //        StorePath storeProps = JsonConvert.DeserializeObject<StorePath>(config.CertificateStoreDetails.Properties, new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });

        //       // var client = new GcpCertificateManagerClient();
        //       // var svc = client.GetGoogleCredentials(config.CertificateStoreDetails.ClientMachine);
        //        _logger.LogTrace("Google Cert Manager Client Created");

        //        var warningFlag = false;
        //        var sb = new StringBuilder();
        //        sb.Append("");
        //        var inventoryItems = new List<CurrentInventoryItem>();
        //        var nextPageToken = string.Empty;

        //        //todo support labels and map entries by making api calls to search maps and map entries

        //        if (storeProps != null)
        //            foreach (var location in storeProps.Location.Split(','))
        //            {
        //                var storePath = $"projects/{config.CertificateStoreDetails.StorePath}/locations/{location}";
        //                do
        //                {
        //                    //var certificatesRequest =
        //                    //    svc.Projects.Locations.Certificates.List(storePath);
        //                   // certificatesRequest.Filter = "pemCertificate!=\"\"";
        //                    //certificatesRequest.PageSize = 100;
        //                   // if (nextPageToken?.Length > 0) certificatesRequest.PageToken = nextPageToken;
        //                   // var certificatesResponse = certificatesRequest.Execute();
        //                    nextPageToken = null;
        //                    //Debug Write Certificate List Response from Google Cert Manager
        //                    _logger.LogTrace(
        //                        $"Certificate List Result {JsonConvert.SerializeObject(certificatesResponse)}");

        //                    inventoryItems.AddRange(certificatesResponse.Certificates.Select(
        //                        c =>
        //                        {
        //                            try
        //                            {
        //                                _logger.LogTrace(
        //                                    $"Building Cert List Inventory Item Alias: {c.Name} Pem: {c.PemCertificate} Private Key: dummy (from PA API)");
        //                                return BuildInventoryItem(c.Name, c.PemCertificate,
        //                                    true, storePath, svc, storeProps.ProjectNumber); //todo figure out how to see if private key exists not in Google Api return
        //                            }
        //                            catch
        //                            {
        //                                _logger.LogWarning(
        //                                    $"Could not fetch the certificate: {c?.Name} associated with description {c?.Description}.");
        //                                sb.Append(
        //                                    $"Could not fetch the certificate: {c?.Name} associated with issuer {c?.Description}.{Environment.NewLine}");
        //                                warningFlag = true;
        //                                return new CurrentInventoryItem();
        //                            }
        //                        }).Where(acsii => acsii?.Certificates != null).ToList());

        //                    if (certificatesResponse.NextPageToken?.Length > 0)
        //                    {
        //                        nextPageToken = certificatesResponse.NextPageToken;
        //                    }
        //                } while (nextPageToken?.Length > 0);
        //            }

        //        _logger.LogTrace("Submitting Inventory To Keyfactor via submitInventory.Invoke");
        //        submitInventory.Invoke(inventoryItems);
        //        _logger.LogTrace("Submitted Inventory To Keyfactor via submitInventory.Invoke");

        //        _logger.MethodExit(LogLevel.Debug);
        //        if (warningFlag)
        //        {
        //            _logger.LogTrace("Found Warning");
        //            return new JobResult
        //            {
        //                Result = OrchestratorJobStatusJobResult.Warning,
        //                JobHistoryId = config.JobHistoryId,
        //                FailureMessage = sb.ToString()
        //            };
        //        }

        //        _logger.LogTrace("Return Success");
        //        return new JobResult
        //        {
        //            Result = OrchestratorJobStatusJobResult.Success,
        //            JobHistoryId = config.JobHistoryId,
        //            FailureMessage = sb.ToString()
        //        };
        //    }
        //    catch (Exception e)
        //    {
        //        _logger.LogError($"PerformInventory Error: {e.Message}");
        //        throw;
        //    }

        //}

        //protected virtual CurrentInventoryItem BuildInventoryItem(string alias, string certPem, bool privateKey, string storePath, CertificateManagerService svc, string projectNumber)
        //{
        //    try
        //    {
        //        _logger.MethodEntry();
        //        _logger.LogTrace($"Alias: {alias} Pem: {certPem} PrivateKey: {privateKey}");

        //        1.Look up certificate map entries based on certificate name
        //        var certAttributes = GetCertificateAttributes(storePath);
        //        string modAlias = modAlias = alias.Split('/')[5];
        //        var mapSettings = GetMapSettings(storePath, modAlias, svc, projectNumber);

        //        if (mapSettings != null && mapSettings.ContainsKey("Certificate Map Name") && mapSettings["Certificate Map Name"]?.ToString().Length > 0)
        //        {
        //            modAlias = mapSettings["Certificate Map Name"] + "/" + mapSettings["Certificate Map Entry Name"] + "/" + modAlias;
        //        }

        //        var acsi = new CurrentInventoryItem
        //        {
        //            Alias = modAlias,
        //            Certificates = new[] { certPem },
        //            ItemStatus = OrchestratorInventoryItemStatus.Unknown,
        //            PrivateKeyEntry = privateKey,
        //            UseChainLevel = false,
        //            Parameters = certAttributes
        //        };

        //        return acsi;
        //    }
        //    catch (Exception e)
        //    {
        //        _logger.LogError($"Error Occurred in Inventory.BuildInventoryItem: {e.Message}");
        //        throw;
        //    }
        //}

        protected Dictionary<string, object> GetCertificateAttributes(string storePath)
        {
            var locationName = storePath.Split('/')[3];

            var siteSettingsDict = new Dictionary<string, object>
                             {
                                 { "Location", locationName}
                             };
            return siteSettingsDict;
        }


        //protected Dictionary<string, string> GetMapSettings(string storePath, string certificateName, CertificateManagerService svc, string projectNumber)
        //{
        //    var locationName = storePath.Split('/')[3];

        //    var siteSettingsDict = new Dictionary<string, string>();

        //    var certName = $"projects/{projectNumber}/locations/{locationName}/certificates/{certificateName}";

        //    //Loop through list of maps and map entries until you find the certificate
        //    var mapListRequest =
        //        svc.Projects.Locations.CertificateMaps.List(storePath);
        //    var mapListResponse = mapListRequest.Execute();

        //    foreach (CertificateMap map in mapListResponse.CertificateMaps)
        //    {
        //        var mapEntryListRequest = svc.Projects.Locations.CertificateMaps.CertificateMapEntries.List(map.Name);
        //        mapEntryListRequest.Filter = $"certificates:\"{certName}\"";
        //        var mapEntryListResponse = mapEntryListRequest.Execute();

        //        if (mapEntryListResponse?.CertificateMapEntries?.Count > 0)
        //        {
        //            var mapEntry = mapEntryListResponse.CertificateMapEntries[0];
        //            siteSettingsDict.Add("Certificate Map Name", map.Name.Split('/')[5]);
        //            siteSettingsDict.Add("Certificate Map Entry Name", mapEntry.Name.Split('/')[7]);
        //            return siteSettingsDict;
        //        }
        //    }

        //    return siteSettingsDict;
        //}
    }
}