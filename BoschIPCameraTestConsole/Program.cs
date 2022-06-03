using System.Collections.Generic;
using System.Threading.Tasks;
using Keyfactor.Extensions.Orchestrator.BoschIPCamera.Client;
using Keyfactor.Orchestrators.Extensions;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Net;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Text;
using System.IO;
using System.Net.Http;

namespace BoschIPCameraTestConsole
{
    struct enrollResponse
    {
        public certificateInformation CertificateInformation;
        public Dictionary<String, Object> Metadata;
    }

    struct certificateInformation
    {
        public string SerialNumber;
        public string IssuerDN;
        public string Thumbprint;
        public int KeyfactorID;
        public int KeyfactorRequestId;
        public string[] Certificates;
        public string RequestDisposition;
        public string DispositionMessage;
        public string EnrollmentContext;
    }

    internal class Program
    {
        private static void Upload(string host, string fileName, string fileData)
        {
            string boundary = "----------" + DateTime.Now.Ticks.ToString("x");
            string fileHeader = string.Format("Content-Disposition: form-data; name=\"certUsageUnspecified\"; filename=\"{0}\";\r\nContent-Type: application/x-x509-ca-cert\r\n\r\n", fileName);
            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create("http://"+host+"/upload.htm");
            CredentialCache credCache = new CredentialCache();
            credCache.Add(new Uri("http://"+host), "Digest", new NetworkCredential("mizell", "Keyfactor1!"));
            httpWebRequest.Credentials = credCache;
            httpWebRequest.ContentType = "multipart/form-data; boundary=" + boundary;
            httpWebRequest.Method = "POST";
            httpWebRequest.BeginGetRequestStream((result) =>
            {
                try
                {
                    HttpWebRequest request = (HttpWebRequest)result.AsyncState;
                    using (Stream requestStream = request.EndGetRequestStream(result))
                    {
                        WriteToStream(requestStream, "--" + boundary + "\r\n");
                        WriteToStream(requestStream, fileHeader);
                        WriteToStream(requestStream, fileData);
                        WriteToStream(requestStream, "\r\n--" + boundary + "--\r\n");
                    }
                    request.BeginGetResponse(a =>
                    {
                        try
                        {
                            var response = request.EndGetResponse(a);
                            var responseStream = response.GetResponseStream();
                            using (var sr = new StreamReader(responseStream))
                            {
                                using (StreamReader streamReader = new StreamReader(response.GetResponseStream()))
                                {
                                    string responseString = streamReader.ReadToEnd();
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                            throw;
                        }
                    }, null);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    throw;
                }
            }, httpWebRequest);
        }

        private static void WriteToStream(Stream s, string txt)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(txt);
            s.Write(bytes, 0, bytes.Length);
        }

        private static async Task Main(string[] args)
        {
            //upload a certificate
            //string cert = "-----BEGIN CERTIFICATE-----MIIDeTCCAmGgAwIBAgIQSVQNM9+tTo9Dd52qg4MI1DANBgkqhkiG9w0BAQsFADBPMRMwEQYKCZImiZPyLGQBGRYDbGFiMRkwFwYKCZImiZPyLGQBGRYJa2V5ZmFjdG9yMR0wGwYDVQQDExRrZXlmYWN0b3ItS0ZUUkFJTi1DQTAeFw0xOTA1MTAwMzMyMzJaFw0yNDA1MTAwMzQyMzFaME8xEzARBgoJkiaJk/IsZAEZFgNsYWIxGTAXBgoJkiaJk/IsZAEZFglrZXlmYWN0b3IxHTAbBgNVBAMTFGtleWZhY3Rvci1LRlRSQUlOLUNBMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAmqN1+RED9SuRsLnIF4AB7uFkaismnxhGXc9LWAVBPc8bt8McchMlHmJqVN1DPR0ZT8tVT8jqIODBULrcWZVo6ox15BTrFqzrFUiIuuq16NDW+WYu2rljoMBaOTegkmWs7ZoME+w/MHqFFqPBBvg7uDSZW/w+1VKyn7aRA2Bywy6o5UHpladsokVKwNhyMQvfJnJQ2xJio8mhXV1AM15FCp8hQZ8dXj/cAPKQxk31M1thIP7M8yx779QbxIs6PKLNxarmY+D73r8Q3t8scO+GVQUwSvbDZiF+kzpl/5YTkeD6gLqfQsQr86YiK5nV5xCb2PL8KwnmMCocVImX2fm3vQIDAQABo1EwTzALBgNVHQ8EBAMCAYYwDwYDVR0TAQH/BAUwAwEB/zAdBgNVHQ4EFgQUcBUzPW7ZQuqUMP3RFTCbDU1hTGUwEAYJKwYBBAGCNxUBBAMCAQAwDQYJKoZIhvcNAQELBQADggEBAIYye4+Gd8piML1BXzkMNgt6aNOu7hS4h3sYfojtpV40OdJ64/Pt9pC5NecMt8B0ikiZvfu9c+xO20VB3uFDGNWVLqfoaZi+cvMAYH9gMrK8KiNe21jekbG1uTuIPZ0oJtEDnn7aJ+rXzVTEe6QHZ/gjVcZoPy1/rdCnzMRdH0NS6xpn0HqWpy/IxjnJP0Ux6ZPNzrEmhsUGruVJwF8u5+FTlD9pF55eHqI4COtEqJ8YEMb25s8xCCJVL0al+LbydR0neG4Ic/zA0QEwB7ixFsuytaBUOXv4QVpsu7R4mtWQHdSoJz3I+g117tHDlJfGEoQpsc/gHBwMptPQCobpI30=-----END CERTIFICATE-----";
           // string cert = "-----BEGIN CERTIFICATE-----\nMIIDeTCCAmGgAwIBAgIQSVQNM9+tTo9Dd52qg4MI1DANBgkqhkiG9w0BAQsFADBP\nMRMwEQYKCZImiZPyLGQBGRYDbGFiMRkwFwYKCZImiZPyLGQBGRYJa2V5ZmFjdG9y\nMR0wGwYDVQQDExRrZXlmYWN0b3ItS0ZUUkFJTi1DQTAeFw0xOTA1MTAwMzMyMzJa\nFw0yNDA1MTAwMzQyMzFaME8xEzARBgoJkiaJk/IsZAEZFgNsYWIxGTAXBgoJkiaJ\nk/IsZAEZFglrZXlmYWN0b3IxHTAbBgNVBAMTFGtleWZhY3Rvci1LRlRSQUlOLUNB\nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAmqN1+RED9SuRsLnIF4AB\n7uFkaismnxhGXc9LWAVBPc8bt8McchMlHmJqVN1DPR0ZT8tVT8jqIODBULrcWZVo\n6ox15BTrFqzrFUiIuuq16NDW+WYu2rljoMBaOTegkmWs7ZoME+w/MHqFFqPBBvg7\nuDSZW/w+1VKyn7aRA2Bywy6o5UHpladsokVKwNhyMQvfJnJQ2xJio8mhXV1AM15F\nCp8hQZ8dXj/cAPKQxk31M1thIP7M8yx779QbxIs6PKLNxarmY+D73r8Q3t8scO+G\nVQUwSvbDZiF+kzpl/5YTkeD6gLqfQsQr86YiK5nV5xCb2PL8KwnmMCocVImX2fm3\nvQIDAQABo1EwTzALBgNVHQ8EBAMCAYYwDwYDVR0TAQH/BAUwAwEB/zAdBgNVHQ4E\nFgQUcBUzPW7ZQuqUMP3RFTCbDU1hTGUwEAYJKwYBBAGCNxUBBAMCAQAwDQYJKoZI\nhvcNAQELBQADggEBAIYye4+Gd8piML1BXzkMNgt6aNOu7hS4h3sYfojtpV40OdJ6\n4/Pt9pC5NecMt8B0ikiZvfu9c+xO20VB3uFDGNWVLqfoaZi+cvMAYH9gMrK8KiNe\n21jekbG1uTuIPZ0oJtEDnn7aJ+rXzVTEe6QHZ/gjVcZoPy1/rdCnzMRdH0NS6xpn\n0HqWpy/IxjnJP0Ux6ZPNzrEmhsUGruVJwF8u5+FTlD9pF55eHqI4COtEqJ8YEMb2\n5s8xCCJVL0al+LbydR0neG4Ic/zA0QEwB7ixFsuytaBUOXv4QVpsu7R4mtWQHdSo\nJz3I+g117tHDlJfGEoQpsc/gHBwMptPQCobpI30=\n-----END CERTIFICATE-----";
            //Upload("172.78.231.174:44130", "KFTrainRoot.cer", cert);
            //Console.ReadLine();
            //Upload("http://172.78.231.174:44130/upload.htm", cert);

            /*
            string templateName = "WebServer";
            string CSR = "-----BEGIN CERTIFICATE REQUEST-----MIICbTCCAVUCAQAwKjELMAkGA1UEAxMCdDIxGzAZBgNVBAUTEjA5NDY4ODQzMTA2NTE2MDAyMDCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBAN+dDhcIjZktRRw3Oz0ztLjv4USn1aBgu4T/RjUPIHpPO1mm0W075xfECISr95bn5QLSITrrHvu3iqa/t1qDVcDzbfQc2GhWZrP1yRR7n5C8yh2VpAk7GR5WkzOwakeSOuqlWnLIMIjKWRXi0Yd6gKlHbx2F57TfwIVrrVaW048BzwfGWcpsHK5weqJVi6Oq8aDIwELCnVb72vQTJOpiVsKXi4acOqU2P/0c5+Ke+jLnJPfoVQ6T9TO2HOwBBJQQj287BniJ+/wS3NigGHe8IzLGhTlIOxW+lnIDr/L1IRrqg0TLHmiOeXDrZ1u3NayOQY6IxbEYeNzYAZpL9u6TMgkCAwEAATANBgkqhkiG9w0BAQsFAAOCAQEAWynB+eRuit1RrDrImKLFLOklfHGk4vvRE/s7gklrkx4aaZ/FzP1sJM4AbuMynNd0VMGmtDQAR+HARkEWkkOp79JXwBPbDs3TwMTqrguK03pHvZ5AYbYaifWS541M3qacDu4BcMoHEjTszwCtZku5667XNkLq0ltOhHPNBOGhI0G4BusaZcvs9m81nV3DVdJXkRezL28Fd7MtEtbyhAZG0oHmU7d6wvfs11UmgGd2mfiogHC2/2Wg8kKipjFN7E6r5npAMD/IK0RQ9AZzCyq9TMqxfnvtpEDc+f1FOcxpPwZxRVQrY5AQfyadR0ehpCLVxYvdWL184xo46rPrYXTgWg==-----END CERTIFICATE REQUEST-----";
            string body = $"{{\"CSR\": \"{CSR}\",\"CertificateAuthority\": \"KFTrain.keyfactor.lab\\\\keyfactor-KFTRAIN-CA\",  \"IncludeChain\": false,  \"Metadata\": {{}},  \"Timestamp\": \"{DateTime.UtcNow.ToString("s")}\",  \"Template\": \"{templateName}\"}}";
            enrollResponse resp = MakeWebRequest<enrollResponse>("192.168.78.138/KeyfactorAPI/Enrollment/CSR", "KEYFACTOR\\Administrator", "Password1", body);
            Console.WriteLine(resp);*/
            //Console.ReadLine();
           
            ILoggerFactory invLoggerFactory = new LoggerFactory();
            ILogger<BoschIPcameraClient> invLogger = invLoggerFactory.CreateLogger<BoschIPcameraClient>();

            BoschIPcameraClient client = new BoschIPcameraClient();
            String returnCode = null;

            //generate the CSR on the camera
            client.setupStandardBoschIPcameraClient("172.78.231.174:44130", "mizell", "Keyfactor1!");
         //   returnCode = client.certCreate("keyfactor");

            if (returnCode != "fail")
            {
                //get the CSR from the camera
           //     string responseContent = client.downloadCSRFromCamera("172.78.231.174:44130", "mizell", "Keyfactor1!", "keyfactor");
            }

            //upload the cert

            //turn on 802.1x
           // returnCode = client.change8021xSettings("1");

            if (returnCode != "fail")
            {
                //set cert usage
                returnCode = client.setCertUsage("keyfactor", "00000001");

                if (returnCode != "fail")
                {
                    //reboot camera
                }
            }

            
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
            var jobConfigString = $"{{\"LastInventory\":[],\"CertificateStoreDetails\":{{\"ClientMachine\":\"favorable-tree-346417-feb22d67de35.json\",\"StorePath\":\"favorable-tree-346417\",\"StorePassword\":null,\"Properties\":\"{{}}\",\"Type\":5109}},\"OperationType\":3,\"Overwrite\":{overWrite},\"JobCertificate\":{{\"Thumbprint\":null,\"Contents\":\"MIIQNAIBAzCCD+4GCSqGSIb3DQEHAaCCD98Egg/bMIIP1zCCBYwGCSqGSIb3DQEHAaCCBX0EggV5MIIFdTCCBXEGCyqGSIb3DQEMCgECoIIE+jCCBPYwKAYKKoZIhvcNAQwBAzAaBBToZowff/9eRcA1B3EQRlhwDpkYIgICBAAEggTIyocmman/TgAtU7/Ne9P+f/YfWx5/A03JnrYIJ5M7l1kUkOTXa/r+zgR2UY+LjwcmHQnkK3AA/s9oWL/DjVjXSImILMzg9Izjun2xnmaQJAXQ9qRdLvNYxBWpOVw+4HlYTlp5he9w9qyUGVQ2HiniD/rFpcg0ybA/NiUcDKHh8gWEhFjhR41knYQXJ+efu20QGKSSCTiuF0DBpBCChu5tgnK2sdFE7VPlyQBNXLRsUtaMFEF7qnyvVWCe+Cgh1NY6yhpBfNtlZoJQ6cknRsuSHYWbcvY/O3DOUjI1gCBzMJnAxd4IRAfzKcUSbvwaRrOJIhhyA1ahGq6xhD3lHfB3x+EBx7xtKk1b5FLn6X4OcVfCBIrVFgmDc/Gd7Bs/extROk7OTjg4BejH7MDSBQQznz9vPBWO2BGmMiZeVahMR2n0qOTjvihFGGvrtIK9+3/ETB7qybF4kIi/lHovqt9JA4/VZSSlFND7n4++X2wFmWl7xTj7aO3Zsy3FaoskeEUrhWqpIpwvf7nUjS0XVDQa4kAI087foOI8Sx9E6DTrU7TDdRErDPO2avutvTrnZXhmdkt0m/DqpMYoDTSmZG/8IrImKu0C8zo81f90yUIPeE+rVe8bHbYEb1lHB+yV5pzR+TuRZkIhD+jqUZHYST4CS/gxhUL981RY0Ruly3OyXdVb4O6/tvfaYI3QavV5Sw2FNhs4i5QkLFqbcP1K9ZX1F4yBVrepzhGzWF161jMBg8UeN8YW/56MIIphRmUXVtre7WDDe/6BxdCSmHXd5CGRbLrD1Gi8Ii+fpJEeV9DWJIIc2kqEZUX3kkqTicmz8BHH0S7ipgp4tzPEls+9zsE9NiZTBCuXPMInZR9Ji/uZbt/EevYJ8gNq8CG9OPL0dIkciLTqsPyBtWlrrlltqQRXilfSuvtHPa2BRzRDqdmfK4TlED7C0kcpPSpVvndH+nI4NHXX/BDoQdfs2flwyeNhVqqL5hGQkgbJwp6OTF8mpmZa9t1e+DeAXr4I7IZrdrvKvKEyErb/virGOCyEd5ediEYaL3tmfUZbaIKdIfluB13OXmBUvzE3fWPGq3re15FXbUVa9nw6cWyoYHzkDS92narUHX/zo0ticGC6210RvPMNQ/LUypthNtuq8gGxSGvzrtV/zPosSOOMaTjlGZE2nTryyEzVJDNn14OuLZ/EjDiaRfbjsIv0Lha1WugqrV8OevtawHSJE5gWWFYqruDoDkbQJ+tcm1Qg8NuPhIP3SFwOYVctHKAVxypf19p5OkB314EwlJsuCMp9n7UtMG2WWmlrCaruOVMjQzAJblJuip419clrBJfVzw/6p18+mhOwsm6Tn0rWQzTPonIOza+Zcy2MOTZtPMNv2WEB23jXHMJmn2UCGRT8+mceLSCKNoedEbS4OJdLKCB3OYFFyqmmXtzcOv6K4ZYVxZ24qLXc2l/aKZPCsE4lOCH3WY3Cszs+AprjhbMJKvMVNdxsIfVJ1wcsLrDKdS4KocSYH2Ww9AN5T+llFjC57QTdZCoZQakW+dyzfXpOrwXUraxFHeavTiQVX057BnzXaSmbO+TGts6JNebkYDqdd2aC/j2aoaCLcMHW/E2QiQt58MvcgvtbBsF/8ULpmoOlMWQwIwYJKoZIhvcNAQkVMRYEFEaNcugeJbpKVvjf9gGwRorKgogGMD0GCSqGSIb3DQEJFDEwHi4AdwB3AHcALgB0AGUAcwB0AGUAYQBkAGQAZABsAGEAawBzAGQAZgAuAGMAbwBtMIIKQwYJKoZIhvcNAQcGoIIKNDCCCjACAQAwggopBgkqhkiG9w0BBwEwKAYKKoZIhvcNAQwBBjAaBBT4ls2Db2OhuT5Qh1IF99PwahathQICBACAggnwtRro9j+o2h8p8Li76S6Wc+/3/7et1crIMP1GQsVpI1y5CPfSRNfIacNr17i46kHxj4VTjhaO9tfooH6zYMUTJsV59uczjj464DXh/QxjOumsxuTUL0EHSvhYoka4/tfr1H8uEVEtO6aeOOm5FtvA+ixtdCIZOH9NCDeKRHBnjzUxYRORVLl94NEscg1y++wNmx3HiiJDdG9Rydm/+Bo2iCg9w3konujw2/0XPXPLsoHYGOUxmyx8zqf+1Dz1fp5f75bQ7q6dZmxjenPE/rItfPPf46tvgXsuUCEeXEK4zbIVeyc6Qux3ihCCXOvVC9EM6Blv9nnnwLuv2vPMNLiqcB8cUr2Sb2loaaZQ7AA8h88YQd1R+SKgvH6CnYtiBJqWIeKJpf9VtFITb6C5hVXGm+Ep76F3PrnmkfD79+GLI9Y/y1CVWBZ3FLFM/bZViY49HCEw2St953PTuxjH/lJlvupf1gO2I+UKIDxjm5HfBZv/3CRF81H/wm9lcfaksgdBkGJ9hQzf5aX8DM314+QHHIey5v82SdK2hwWqUJqli4xywoDrngYBepxa2orAyf5bFEYs1yplx87O7p2L2ybTu9yJmq5+E6wNs0KOIsMb7+aDPN/YTjm/Wxv6/49tu9n6VWFb+OPfNo6oV6FnUCzGn2BDXSg9KN2RFZMzL+aSEXhQ8xOfddqvfwAR4Ypd1eE/1rRmbl3VXwNlUFW1bn4CVo0e67fM8d2QvCOFZ4e3SPMCFmjdXwpwxx3L1oK2lG6OzG7jAsSTK9Wl4mR0i3Z2BiyHuDL9vOtjGzJMdTPyn1VbB9d7TOYq7Is38LYUCm0Fv6V3WyVE+lBJoADuACwByZ9s0RjWRp67hTV9/3Qx/djLzWu1VzxrRovUgLF3VNFXzoB3fv0oajpLrWDgJq679j014HTUxhxerosJWl2kX4rLzWPauLwzw9QXdpZWUt0zNoFaNaM/5HX8qvcNkEGrBEOJ+UIlHMSxdkHkOkIP1bgOZCBDURMPx9vdVG0tNDffeGmSDN9Mr1i6vTxwTd8Ghj3FwleYvChUzGRRwj88x1nIlp4egmI/VC9/PsB9ENYKhdHRfYxLF6Z8Qpqex3+30EaGDCaRUdQIIApMuBRmpg4JEW3V4mYH3UTkhvCxgh+vbBXkEi+7AcWBWYvGANB08+N8++u0Oh6X8HQ+tCaevEITSopkCMn37enYcGH4PFxeTnUb8Tk7+pw6GPm9qOhpA69pIvPC4HVsJ3lNmo7NqakoyTXxCQchn27PvuwASbcpnkZK4QAQalcM7hogs1ecuMyI0W1yEzn0+cf8CiLreFr6XHZ95qQlRnuad5uovuFH/94SlWT8nrwGZSBUv8v4DISKKeRuJ+m1jHHd0n5c4hi6qw8Qgn0tmDwo+K4FvpDZ8nEU+ajuyK3BGP4uXIkDIdHJvFVMlcu58UwJrUdT1YB5+7pMfdbA3sHuGLV03Hi/WLaz0MLYer4BuURNiDSj2MQoRoyWnJ7URrq0R6b1i2EY2QpIz4F+c8K5CnWzHsZXz/4S683QWDzAaGxLKBdcv/aFiOu+Ka0vj5ft9rR04tzZIlRCCv7g6fMIevBpdbE8sqg+pKAlwiwHisyc2GqocNwS6t0rUuRZjkVmGAOPU3ZHoy2s12B+rcegwnsRER6xb3Koelq7a66mXQVLSPhMuUfNKJpkHlhJUan5EOJkxFtMFJP9s1/i8b+ynZEm9byK6x9fzvQR7Bg/Chn7TxeeohxiTWGcy0X1+ABztc+IPOElMbMXVusAcAwVVCENSVsxdVJklWUT/PB1ZLuCKaPZ706oFrR4y42nZKYUaPfywqQ+2v1m8onlhrsY5GgtQAqUyUpCnrsQnPpsocx6GAVzamvgE30KMFztpVoKtXPiGumO3wpnM7kYrRSu8sIsWASbSpwyWTyi5x54YdbT2rPQm/NjGUciLwSsiwHdszvd8nWuOQLcoeA9UEhoRgAS8AAPToMRuypQkTmZFc4EFQpTFgqe4lWTn8xaX2sVlpape6ajjcxf0CiqRvTePvEH2IbSVwpEtsS2m5k0692gwN5zQoeV1j/hLcZoKR8/HeMe1P7yztA5DXMvRmPAJDeu8xs3gAx+cJERkNkkk5PhUVplZc5JsyR8P2l8elZ6rL5QbeN5lePLjQ8do0Cpwki39WJ8JrdDzCmTqakqUEjC0Zu/31c8720grSD+VieYApCa9AMEj9obI7YY7YQHVJb+mqXbpVL3W+J4OBvOiXP1wvLmhg5JlYdlqLGmGbSRJEd0/S3Jo+mH9ykkNlCJ3ZjuoeTcf3jZmgL3XEGrs/f7QQ35pSjJMqEBtbKPD522zNZ1wV11NfHEaDIvb53xp1+HaDtVcUNMxpvlaPCZUTKbtajDK9DSzt8pCqm+/hZsUXt/qhEMGd4AAIuOlTbviprU7fFIjfIRzihR08RUt2jVj5ygvBmQDtVcF8GZ3VbEDoznCP+6MXcysIKnnxZ1omK9NYvLUeXjAfnHxO1GSgEJF0I44uPT4rbCmE2m804iTOzuXyGaOaMY7eq5a5KzWIQtG9TOc3JL8gQLNtC3tjv2nxRuG5Y+MOi/GWc/oBAgAYIIu+cunSBaWLTiWORC2H+cuGsX7okiTJQr1TjCGR1E4aA1/y5VGiGqT8OsAFKyg1d8TZV8xQp6JQPS341X58RlIdplemdTAEoqakFVA2RZTkQ1VvXfksb6ne3cfVdswGWDH6Q03HOTyrZKu9awOMkzROSvGo9yZuxjo8DaxgRV5I6sSK2JoqIxNqnHALsDZ8K7GGg1LYhG0jBKHndoCN+aIm5RpV7p+dZ4vt0seiSTBK4L4QKAxg6Gld/8CUkvPaXDySSV4Mc8PAuspT0KLbIccb0NLFz0wJp1HZ3BzTNElZzZ5q1PYzJULc5IXLaFHM10kj1EoF3FzcDz5oYYPpGh0/Yz0xgbLBmpbt6f06zjrc50Iyq0DEztvlgqz+NWT/TG+0plXUdFQVyxGOLvZUsRo2PeqN5hZAM+lXTgdInVPC8hWHPnRNyXNrTiAZJulvHUzv5ZDHksXbDsy/Ci0KnnH3hmYqlrragECOELLjLJGJll3mXHgNW6nfeut4qWki16P42nBNxy+F5et1hcHvJ7tNQRi/UPPL9yWOFq8y+FflsevECwaMH8SKc8Nc6+MBAqx2mxTf0g2jFhQIwrvzZcjXsEJl2bwswxGBIAcojIEHxLi8Ui9fJSgY1DLcDiw5I9GOhbPHcZ2sO7Fe84VFjPZCB1H4VOsJzhVVEU54owLeCHugfGpSAIwLlYZnf80p+54B/CnEw1ntkqjhm4J2cIghEjHQEIBM+LQHyNePlqkkjslGWYcOWIQ+slvNGdp1mddi8x+PLiNV5I4tERbH5otBHvMD0wITAJBgUrDgMCGgUABBRM4ih/Py00W8IYB4C0uucXDYIJjgQUWH+KmgKrv+VEeKDCU7IPTFTs5kYCAgQA\",\"Alias\":\"{alias}\",\"PrivateKeyPassword\":\"{privateKeyPwd}\"}},\"JobCancelled\":false,\"ServerError\":null,\"JobHistoryId\":298380,\"RequestStatus\":1,\"ServerUsername\":null,\"ServerPassword\":null,\"UseSSL\":true,\"JobProperties\":{{\"Scope\":\"{scope}\",\"Location\":\"{location}\",\"Certificate Map Name\":\"{mapEntry}\",\"Certificate Map Entry Name\":\"{mapEntryName}\"}},\"JobTypeId\":\"00000000-0000-0000-0000-000000000000\",\"JobId\":\"d9e6e40b-f9cf-4974-a8c3-822d2c4f394f\",\"Capability\":\"CertStores.GcpCertificateManager.Management\"}}";
            var result = JsonConvert.DeserializeObject<ManagementJobConfiguration>(jobConfigString);
            return result;
        }
    }
}