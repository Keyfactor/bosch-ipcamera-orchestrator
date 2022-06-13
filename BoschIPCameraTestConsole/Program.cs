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
using Keyfactor.Extensions.Orchestrator.BoschIPCamera.Jobs;

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

        private static Dictionary<string, string> s_csrSubject = new Dictionary<string, string>();

        private static void Upload(string host, string username, string password, string fileName, string fileData)
        {
            string boundary = "----------" + DateTime.Now.Ticks.ToString("x");
            string fileHeader = string.Format("Content-Disposition: form-data; name=\"certUsageUnspecified\"; filename=\"{0}\";\r\nContent-Type: application/x-x509-ca-cert\r\n\r\n", fileName);
            CredentialCache credCache = new CredentialCache();
            credCache.Add(new Uri("http://" + host), "Digest", new NetworkCredential(username, password));

            HttpWebRequest authRequest = (HttpWebRequest)WebRequest.Create("http://" + host + "/upload.htm");
            authRequest.Method = "GET";
            authRequest.Credentials = credCache;
            authRequest.PreAuthenticate = true;
            authRequest.GetResponse();

            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create("http://" + host + "/upload.htm");
            httpWebRequest.Credentials = credCache;
            httpWebRequest.ContentType = "multipart/form-data; boundary=" + boundary;
            httpWebRequest.Method = "POST";
            httpWebRequest.PreAuthenticate = true;

            IAsyncResult y = null;
            var x = httpWebRequest.BeginGetRequestStream((result) =>
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
                    y = request.BeginGetResponse(a =>
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

                        }
                    }, null);
                    while (!y.IsCompleted)
                    {
                        Thread.Sleep(1000);
                    }
                }
                catch (Exception e)
                {
                    throw;
                }
            }, httpWebRequest);
            while (y == null || !y.IsCompleted)
            {
                Thread.Sleep(100);
            }
            while (!x.IsCompleted)
            {
                Thread.Sleep(100);
            }
        }

        private static void Upload2(string host, string username, string password, string fileName, string fileData)
        {
            string boundary = "----------" + DateTime.Now.Ticks.ToString("x");
            string fileHeader = string.Format("Content-Disposition: form-data; name=\"certUsageUnspecified\"; filename=\"{0}\";\r\nContent-Type: application/x-x509-ca-cert\r\n\r\n", fileName);
            CredentialCache credCache = new CredentialCache();
            credCache.Add(new Uri("http://" + host), "Digest", new NetworkCredential(username, password));

            HttpWebRequest authRequest = (HttpWebRequest)WebRequest.Create("http://" + host + "/upload.htm");
            authRequest.Method = "GET";
            authRequest.Credentials = credCache;
            authRequest.PreAuthenticate = true;

            try
            {
                WebResponse response = authRequest.GetResponse();
            }
            catch (Exception e)
            {

            }
            bool certOnCamera = false;
            int count = 0;
            //keep trying until we get the cert on camera or try 5 times
            while (!certOnCamera && count <= 1)
            {
                try
                {
                    count++;
                    HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create("http://" + host + "/upload.htm");
                    httpWebRequest.Credentials = credCache;
                    httpWebRequest.ContentType = "multipart/form-data; boundary=" + boundary;
                    httpWebRequest.Method = "POST";
                    httpWebRequest.PreAuthenticate = true;

                    Stream requestStream = httpWebRequest.GetRequestStream();
                    WriteToStream(requestStream, "--" + boundary + "\r\n");
                    WriteToStream(requestStream, fileHeader);
                    WriteToStream(requestStream, fileData);
                    WriteToStream(requestStream, "\r\n--" + boundary + "--\r\n");
                    // requestStream.Close();

                    HttpWebResponse myHttpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();


                    Stream responseStream = myHttpWebResponse.GetResponseStream();

                    StreamReader myStreamReader = new StreamReader(responseStream, Encoding.Default);

                    string pageContent = myStreamReader.ReadToEnd();

                    myStreamReader.Close();
                    responseStream.Close();

                    myHttpWebResponse.Close();
                }
                catch (Exception e)
                {
                }
            }

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
            string cert = "-----BEGIN CERTIFICATE-----\nMIIDeTCCAmGgAwIBAgIQSVQNM9+tTo9Dd52qg4MI1DANBgkqhkiG9w0BAQsFADBP\nMRMwEQYKCZImiZPyLGQBGRYDbGFiMRkwFwYKCZImiZPyLGQBGRYJa2V5ZmFjdG9y\nMR0wGwYDVQQDExRrZXlmYWN0b3ItS0ZUUkFJTi1DQTAeFw0xOTA1MTAwMzMyMzJa\nFw0yNDA1MTAwMzQyMzFaME8xEzARBgoJkiaJk/IsZAEZFgNsYWIxGTAXBgoJkiaJ\nk/IsZAEZFglrZXlmYWN0b3IxHTAbBgNVBAMTFGtleWZhY3Rvci1LRlRSQUlOLUNB\nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAmqN1+RED9SuRsLnIF4AB\n7uFkaismnxhGXc9LWAVBPc8bt8McchMlHmJqVN1DPR0ZT8tVT8jqIODBULrcWZVo\n6ox15BTrFqzrFUiIuuq16NDW+WYu2rljoMBaOTegkmWs7ZoME+w/MHqFFqPBBvg7\nuDSZW/w+1VKyn7aRA2Bywy6o5UHpladsokVKwNhyMQvfJnJQ2xJio8mhXV1AM15F\nCp8hQZ8dXj/cAPKQxk31M1thIP7M8yx779QbxIs6PKLNxarmY+D73r8Q3t8scO+G\nVQUwSvbDZiF+kzpl/5YTkeD6gLqfQsQr86YiK5nV5xCb2PL8KwnmMCocVImX2fm3\nvQIDAQABo1EwTzALBgNVHQ8EBAMCAYYwDwYDVR0TAQH/BAUwAwEB/zAdBgNVHQ4E\nFgQUcBUzPW7ZQuqUMP3RFTCbDU1hTGUwEAYJKwYBBAGCNxUBBAMCAQAwDQYJKoZI\nhvcNAQELBQADggEBAIYye4+Gd8piML1BXzkMNgt6aNOu7hS4h3sYfojtpV40OdJ6\n4/Pt9pC5NecMt8B0ikiZvfu9c+xO20VB3uFDGNWVLqfoaZi+cvMAYH9gMrK8KiNe\n21jekbG1uTuIPZ0oJtEDnn7aJ+rXzVTEe6QHZ/gjVcZoPy1/rdCnzMRdH0NS6xpn\n0HqWpy/IxjnJP0Ux6ZPNzrEmhsUGruVJwF8u5+FTlD9pF55eHqI4COtEqJ8YEMb2\n5s8xCCJVL0al+LbydR0neG4Ic/zA0QEwB7ixFsuytaBUOXv4QVpsu7R4mtWQHdSo\nJz3I+g117tHDlJfGEoQpsc/gHBwMptPQCobpI30=\n-----END CERTIFICATE-----";
            //Upload2("172.78.231.174:44130", "mizell", "Keyfactor1!", "KFTrainRoot.cer", cert);\
            Upload2("166.145.144.73:10080", "service", "DHStrp2022!", "KFTrainRoot.cer", cert);

            /*
            string templateName = "WebServer";
            string CSR = "-----BEGIN CERTIFICATE REQUEST-----MIICbTCCAVUCAQAwKjELMAkGA1UEAxMCdDIxGzAZBgNVBAUTEjA5NDY4ODQzMTA2NTE2MDAyMDCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBAN+dDhcIjZktRRw3Oz0ztLjv4USn1aBgu4T/RjUPIHpPO1mm0W075xfECISr95bn5QLSITrrHvu3iqa/t1qDVcDzbfQc2GhWZrP1yRR7n5C8yh2VpAk7GR5WkzOwakeSOuqlWnLIMIjKWRXi0Yd6gKlHbx2F57TfwIVrrVaW048BzwfGWcpsHK5weqJVi6Oq8aDIwELCnVb72vQTJOpiVsKXi4acOqU2P/0c5+Ke+jLnJPfoVQ6T9TO2HOwBBJQQj287BniJ+/wS3NigGHe8IzLGhTlIOxW+lnIDr/L1IRrqg0TLHmiOeXDrZ1u3NayOQY6IxbEYeNzYAZpL9u6TMgkCAwEAATANBgkqhkiG9w0BAQsFAAOCAQEAWynB+eRuit1RrDrImKLFLOklfHGk4vvRE/s7gklrkx4aaZ/FzP1sJM4AbuMynNd0VMGmtDQAR+HARkEWkkOp79JXwBPbDs3TwMTqrguK03pHvZ5AYbYaifWS541M3qacDu4BcMoHEjTszwCtZku5667XNkLq0ltOhHPNBOGhI0G4BusaZcvs9m81nV3DVdJXkRezL28Fd7MtEtbyhAZG0oHmU7d6wvfs11UmgGd2mfiogHC2/2Wg8kKipjFN7E6r5npAMD/IK0RQ9AZzCyq9TMqxfnvtpEDc+f1FOcxpPwZxRVQrY5AQfyadR0ehpCLVxYvdWL184xo46rPrYXTgWg==-----END CERTIFICATE REQUEST-----";
            string body = $"{{\"CSR\": \"{CSR}\",\"CertificateAuthority\": \"KFTrain.keyfactor.lab\\\\keyfactor-KFTRAIN-CA\",  \"IncludeChain\": false,  \"Metadata\": {{}},  \"Timestamp\": \"{DateTime.UtcNow.ToString("s")}\",  \"Template\": \"{templateName}\"}}";
            enrollResponse resp = MakeWebRequest<enrollResponse>("192.168.78.138/KeyfactorAPI/Enrollment/CSR", "KEYFACTOR\\Administrator", "Password1", body);
            Console.WriteLine(resp);*/
            //Console.ReadLine();

            //  ILoggerFactory invLoggerFactory = new LoggerFactory();
            //   ILogger<Reenrollment> invLogger = invLoggerFactory.CreateLogger<Reenrollment>();

            BoschIPcameraClient client = new BoschIPcameraClient();
            String returnCode = "";

            //generate the CSR on the camera
            //         client.setupStandardBoschIPcameraClient("172.78.231.174:44130", "mizell", "Keyfactor1!", setupCSRSubject(), invLogger);
            //client.setupStandardBoschIPcameraClient("166.145.144.73:10080", "service", "DHStrp2022!", setupCSRSubject());

            //delete cert if it exists
            //returnCode = client.deleteCertByName("keyfactor");

            //    returnCode = client.certCreate("keyfactor");

            if (returnCode == "pass")
            {
                //get the CSR from the camera
           //      string responseContent = client.downloadCSRFromCamera("172.78.231.174:44130", "mizell", "Keyfactor1!", "keyfactor");
            }

            //upload the cert
          //  returnCode = client.uploadCertToCamera("172.78.231.174:44130", "mizell", "Keyfactor1!", "KFTrainRoot.cer", cert);

            //turn on 802.1x
            //returnCode = client.change8021xSettings("0");

            if (returnCode == "pass")
            {
                //set cert usage
           //   returnCode = client.setCertUsage("HTTPSCert", "80000000");

                if (returnCode == "pass")
                {
                    //reboot camera
              //      returnCode = client.rebootCamera();
                }
            }

            
        }

        //define standard Subject for CSR. IP address will vary by device to be passed in at runtime
        //likely all this data could be captured in the cert store definition - should not be hardcoded
        private static Dictionary<string, string> setupCSRSubject()
        {
            Dictionary<string, string> csrSubject = new Dictionary<string, string>();

            csrSubject.Add("C", "US");
            csrSubject.Add("ST", "North Carolina");
            csrSubject.Add("L", "Apex");
            csrSubject.Add("O", "Homecheese");
            csrSubject.Add("OU", "IT");
            csrSubject.Add("CN", "172.78.231.174");

            return csrSubject;
        }

      
    }
}