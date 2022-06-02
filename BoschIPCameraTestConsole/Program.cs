﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Keyfactor.Extensions.Orchestrator.BoschIPCamera.Client;
using Keyfactor.Orchestrators.Extensions;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace GcpCertManagerTestConsole
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            ILoggerFactory invLoggerFactory = new LoggerFactory();
            ILogger<BoschIPcameraClient> invLogger = invLoggerFactory.CreateLogger<BoschIPcameraClient>();

            BoschIPcameraClient client = new BoschIPcameraClient();

            //generate the CSR on the camera
           client.setupStandardBoschIPcameraClient("172.78.231.174:44130", "mizell", "Keyfactor1!");
           client.certCreate("keyfactor");

            //get the CSR from the camera
           string responseContent = client.downloadCSRFromCamera("172.78.231.174:44130", "mizell", "Keyfactor1!", "keyfactor");
            
        }

        public static bool GetItems(IEnumerable<CurrentInventoryItem> items)
        {
            return true;
        }

        public static ManagementJobConfiguration GetJobConfiguration(string privateKeyPwd, string overWrite,string alias,string mapEntry,string mapEntryName,string scope,string location)
        {
            var privateKeyConfig = $"{{\"LastInventory\":[],\"CertificateStoreDetails\":{{\"ClientMachine\":\"favorable-tree-346417-feb22d67de35.json\",\"StorePath\":\"favorable-tree-346417\",\"StorePassword\":null,\"Properties\":\"{{}}\",\"Type\":5109}},\"OperationType\":2,\"Overwrite\":{overWrite},\"JobCertificate\":{{\"Thumbprint\":null,\"Contents\":\"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\",\"Alias\":\"{alias}\",\"PrivateKeyPassword\":\"{privateKeyPwd}\"}},\"JobCancelled\":false,\"ServerError\":null,\"JobHistoryId\":298380,\"RequestStatus\":1,\"ServerUsername\":null,\"ServerPassword\":null,\"UseSSL\":true,\"JobProperties\":{{\"Scope\":\"{scope}\",\"Location\":\"{location}\",\"Certificate Map Name\":\"{mapEntry}\",\"Certificate Map Entry Name\":\"{mapEntryName}\"}},\"JobTypeId\":\"00000000-0000-0000-0000-000000000000\",\"JobId\":\"d9e6e40b-f9cf-4974-a8c3-822d2c4f394f\",\"Capability\":\"CertStores.GcpCertificateManager.Management\"}}";
            
            var jobConfigString = privateKeyConfig;

            var result = JsonConvert.DeserializeObject<ManagementJobConfiguration>(jobConfigString);
            return result;
        }

        public static InventoryJobConfiguration GetInventoryJobConfiguration()
        {

        var jobConfigString = "{\"LastInventory\":[],\"CertificateStoreDetails\":{\"ClientMachine\":\"favorable-tree-346417-feb22d67de35.json\",\"StorePath\":\"favorable-tree-346417\",\"StorePassword\":null,\"Properties\":\"{\\\"Locations\\\":\\\"global\\\"}\",\"Type\":4108},\"JobCancelled\":false,\"ServerError\":null,\"JobHistoryId\":275794,\"RequestStatus\":1,\"ServerUsername\":null,\"ServerPassword\":null,\"UseSSL\":true,\"JobProperties\":null,\"JobTypeId\":\"00000000-0000-0000-0000-000000000000\",\"JobId\":\"1899814e-e955-4fc5-8c0b-5157d097aeba\",\"Capability\":\"CertStores.IISBindings.Inventory\"}";
        var result = JsonConvert.DeserializeObject<InventoryJobConfiguration>(jobConfigString);
            return result;
    }

        public static ManagementJobConfiguration GetRemoveJobConfiguration(string privateKeyPwd, string overWrite, string alias, string mapEntry, string mapEntryName, string scope, string location)
        {
            var jobConfigString = $"{{\"LastInventory\":[],\"CertificateStoreDetails\":{{\"ClientMachine\":\"favorable-tree-346417-feb22d67de35.json\",\"StorePath\":\"favorable-tree-346417\",\"StorePassword\":null,\"Properties\":\"{{}}\",\"Type\":5109}},\"OperationType\":3,\"Overwrite\":{overWrite},\"JobCertificate\":{{\"Thumbprint\":null,\"Contents\":\"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\",\"Alias\":\"{alias}\",\"PrivateKeyPassword\":\"{privateKeyPwd}\"}},\"JobCancelled\":false,\"ServerError\":null,\"JobHistoryId\":298380,\"RequestStatus\":1,\"ServerUsername\":null,\"ServerPassword\":null,\"UseSSL\":true,\"JobProperties\":{{\"Scope\":\"{scope}\",\"Location\":\"{location}\",\"Certificate Map Name\":\"{mapEntry}\",\"Certificate Map Entry Name\":\"{mapEntryName}\"}},\"JobTypeId\":\"00000000-0000-0000-0000-000000000000\",\"JobId\":\"d9e6e40b-f9cf-4974-a8c3-822d2c4f394f\",\"Capability\":\"CertStores.GcpCertificateManager.Management\"}}";
            var result = JsonConvert.DeserializeObject<ManagementJobConfiguration>(jobConfigString);
            return result;
        }
    }
}