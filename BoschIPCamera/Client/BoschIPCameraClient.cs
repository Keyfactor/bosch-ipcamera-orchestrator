using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.Orchestrator.BoschIPCamera.Client
{
    //todo better error handling and logging
    //todo, low priority do we need a client library wrapper for bosch calls so we don't have to reference hex right in the main code and have it more readable as to what it is doing?
    public class BoschIpCameraClient
    {
        private readonly string _cameraUrl;
        private readonly string _baseUrl;
        private readonly HttpClient _client;
        private readonly ILogger _logger;
        private readonly CredentialCache _digestCredential;
        private HttpResponseMessage _response;

        public BoschIpCameraClient(JobConfiguration config, CertificateStore store, IPAMSecretResolver pam, ILogger logger)
        {
            _logger = logger;
            _logger.LogTrace("Starting Bosch IP Camera Client config");

            if (config.UseSSL)
            {
                _baseUrl = $"https://{store.ClientMachine}";
                _cameraUrl = $"https://{store.ClientMachine}/rcp.xml?";
            }
            else
            {
                _baseUrl = $"http://{store.ClientMachine}";
                _cameraUrl = $"http://{store.ClientMachine}/rcp.xml?";
            }

            // TODO: validate SSL cert
            // This will ignore certificate errors in test mode since we don't have a valid cert for the camera on the public IP
            //var handler = new HttpClientHandler
            //{
            //    ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; }
            //};
            //ServicePointManager.ServerCertificateValidationCallback = (obj, certificate, chain, errors) => { return true; };

            var username = ResolvePamField(pam, config.ServerUsername, "Server Username");
            var password = ResolvePamField(pam, config.ServerPassword, "Server Password");

            var credentials = $"{username}:{password}";
            var encodedCredentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));

            //_client = new HttpClient(handler);
            _client = new HttpClient();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encodedCredentials);

            // for use in reenrollment cert upload calls
            _digestCredential = new CredentialCache
            {
                {new Uri(_baseUrl), "Digest", new NetworkCredential(username, password)}
            };
        }

        public Dictionary<string, string> ListCerts()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, _cameraUrl)
            {
                RequestUri = new Uri(_cameraUrl + "command=0x0BEB&type=P_OCTET&direction=READ&num=1")
            };

            var task = _client.SendAsync(request);
            task.Wait();
            var cameras = GetCameraCertList(task.Result.Content.ReadAsStringAsync().Result);
            var files = new Dictionary<string, string>();
            foreach (var c in cameras)
            {
                Download(c).Wait();
                files.Add(c, _response.Content.ReadAsStringAsync().Result);
            }

            return files;
        }

        public string CertCreate(Dictionary<string, string> subject, string certificateName)
        {
            try
            {
                var myId = HexadecimalEncoding.ToHexNoPadding(certificateName);
                var payload = $"{HexadecimalEncoding.ToHexWithPrefix(certificateName, 4, '0')}0000{myId}";

                // RAW HEX: "length" + "tag" + "content"
                // length is full byte count of header (length + tag) + content
                var keyType = "0008" + "0001" + "00000001";
                var requesttype = "0008" + "0002" + "00000000";

                payload += keyType;
                payload += requesttype;

                // CN is expected
                // TODO: add logging error if no CN added
                var myCommon = HexadecimalEncoding.ToHexWithPadding(subject["CN"]);
                payload += $"{HexadecimalEncoding.ToHexStringLengthWithPadding(subject["CN"], 4, '0')}0005{myCommon}";

                if (subject.ContainsKey("O"))
                {
                    var myOrg = HexadecimalEncoding.ToHexWithPadding(subject["O"]);
                    payload += $"{HexadecimalEncoding.ToHexStringLengthWithPadding(subject["O"], 4, '0')}0006{myOrg}";
                }

                if (subject.ContainsKey("OU"))
                {
                    var myUnit = HexadecimalEncoding.ToHexWithPadding(subject["OU"]);
                    payload += $"{HexadecimalEncoding.ToHexStringLengthWithPadding(subject["OU"], 4, '0')}0007{myUnit}";
                }

                if (subject.ContainsKey("L"))
                {
                    var myCity = HexadecimalEncoding.ToHexWithPadding(subject["L"]);
                    payload += $"{HexadecimalEncoding.ToHexStringLengthWithPadding(subject["L"], 4, '0')}0008{myCity}";
                }

                if (subject.ContainsKey("C"))
                {
                    var myCountry = HexadecimalEncoding.ToHexWithPadding(subject["C"]);
                    payload += $"{HexadecimalEncoding.ToHexStringLengthWithPadding(subject["C"], 4, '0')}0009{myCountry}";
                }

                if (subject.ContainsKey("ST"))
                {
                    var myProvince = HexadecimalEncoding.ToHexWithPadding(subject["ST"]);
                    payload += $"{HexadecimalEncoding.ToHexStringLengthWithPadding(subject["ST"], 4, '0')}000A{myProvince}";
                }

                GenerateCsrOnCameraAsync(payload,_cameraUrl,_client).Wait();
                var returnCode = parseCameraResponse(_response.Content.ReadAsStringAsync().Result);
                if (returnCode != null)
                {
                    _logger.LogError($"Camera failed to generate CSR with error code {returnCode}");
                    return returnCode;
                }

                _logger.LogInformation($"CSR call completed successfully for {certificateName}");
                return "pass";
            }
            catch (ProtocolException ex)
            {
                _logger.LogError($"CSR call failed with the following error: {ex}");
                return ex.ToString();
            }
        }


        //Call the camera to generate a CSR
        private static async Task GenerateCsrOnCameraAsync(string payload,string cameraUrl,HttpClient client)
        {
            var queryParams = new Dictionary<string, string>
            {
                {"command", "0x0BEC"},
                {"type", "P_OCTET"},
                {"direction", "WRITE"},
                {"num", "1"},
                {"payload", payload}
            };

            var queryString = new FormUrlEncodedContent(queryParams).ReadAsStringAsync();
            var requestUri = cameraUrl + await queryString;

            var cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;
            var _ = await client.GetAsync(requestUri, token);
        }

        public string DownloadCsrFromCamera(string certName)
        {
            _logger.LogTrace("Download " + certName + " CSR from Camera: " + _cameraUrl);
            var haveCsr = false;
            var count = 0;
            string csrResult = null;
            //keep trying until we get the cert or try 30 times (wait 5 seconds each time)
            while (!haveCsr && count <= 30)
                try
                {
                    //todo find a better way to do this or at least make sleep and count configurable
                    Thread.Sleep(5000);
                    count++;
                    Download(certName, "?type=csr").Wait();
                    csrResult= _response.Content.ReadAsStringAsync().Result;
                    if (csrResult.Contains("-----BEGIN CERTIFICATE REQUEST-----"))
                        haveCsr = true;
                }
                catch (Exception ex)
                {
                    _logger.LogTrace("CSR Download failed with the following error: " + ex);
                }

            return csrResult;
        }

        public void UploadCert(string fileName, string fileData)
        {
            _logger.LogTrace("Starting Cert upload to camera " + _baseUrl);

            var boundary = "----------" + DateTime.Now.Ticks.ToString("x");
            var fileHeader =
                $"Content-Disposition: form-data; name=\"certUsageUnspecified\"; filename=\"{fileName}\";\r\nContent-Type: application/x-x509-ca-cert\r\n\r\n";

            var authRequest = (HttpWebRequest)WebRequest.Create(_baseUrl + "/upload.htm");
            authRequest.Method = "GET";
            authRequest.Credentials = _digestCredential;
            authRequest.PreAuthenticate = true;

            try
            {
                _logger.LogTrace("Get Auth call to camera on " + _baseUrl);
                authRequest.GetResponse();
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
            }

            var count = 0;
            //keep trying until we get the cert on camera or try 5 times
            while (count <= 5)
            {
                try
                {
                    count++;
                    _logger.LogTrace("Post call to camera on " + _baseUrl);
                    var httpWebRequest = (HttpWebRequest)WebRequest.Create(_baseUrl + "/upload.htm");
                    httpWebRequest.Credentials = _digestCredential;
                    httpWebRequest.ContentType = "multipart/form-data; boundary=" + boundary;
                    httpWebRequest.Method = "POST";
                    //httpWebRequest.PreAuthenticate = true;

                    var requestStream = httpWebRequest.GetRequestStream();
                    WriteToStream(requestStream, "--" + boundary + "\r\n");
                    WriteToStream(requestStream, fileHeader);
                    WriteToStream(requestStream, fileData);
                    WriteToStream(requestStream, "\r\n--" + boundary + "--\r\n");
                    //requestStream.Close();

                    var myHttpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();


                    var responseStream = myHttpWebResponse.GetResponseStream();

                    var myStreamReader = new StreamReader(responseStream ?? throw new InvalidOperationException(), Encoding.Default);

                    myStreamReader.ReadToEnd();

                    myStreamReader.Close();
                    responseStream.Close();

                    myHttpWebResponse.Close();
                    return;
                }
                catch (Exception e)
                {
                    _logger.LogError(e.Message);
                    _logger.LogTrace("Failed to push cert on attempt " + count + " trying again if less than or equal to 5");
                }
            }
        }

        private static void WriteToStream(Stream s, string txt)
        {
            var bytes = Encoding.UTF8.GetBytes(txt);
            s.Write(bytes, 0, bytes.Length);
        }

        private async Task Download(string certName,
            string paramString = "")
        {
            var source = new CancellationTokenSource();
            var token = source.Token;

            _logger.LogTrace("Initializing HttpClient for CSR Download");
            var cameraUrl = $"{_baseUrl}/cert_download/{certName.Replace(" ", "%20")}.pem{paramString}";
            _logger.LogTrace("Camera URL: " + cameraUrl);

            _response = await _client.GetAsync(cameraUrl, token);

        }


        //Enable/Disable 802.1x setting on the camera
        public string Change8021XSettings(string onOffSwitch)
        {
            _logger.LogTrace("Changing Camera 802.1x setting to " + onOffSwitch + " on Camera: " + _cameraUrl);

            try
            {
                Change8021X(onOffSwitch).Wait();
                var returnCode = parseCameraResponse(_response.Content.ReadAsStringAsync().Result);
                if (returnCode != null)
                {
                    _logger.LogError("Camera failed to change 802.1x with error code " + returnCode);
                    return returnCode;
                }

                _logger.LogInformation("802.1x setting changed successfully for " + _cameraUrl);
                return "pass";
            }
            catch (Exception ex)
            {
                _logger.LogError("802.1x setting change failed with the following error: " + ex);
                return ex.ToString();
            }
        }

        //Enable/Disable 802.1x on the camera after the certs are in place
        //onOffSwitch - "0" means off, "1" means on
        private async Task Change8021X(string onOffSwitch)
        {
            var source = new CancellationTokenSource();
            var token = source.Token;

            var requestUri =
                $"{_cameraUrl}command=0x09EB&type=T_OCTET&direction=WRITE&num=1&payload={Uri.EscapeDataString(onOffSwitch)}";

            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

            _response = await _client.SendAsync(request, token);
            if (!_response.IsSuccessStatusCode)
                throw new Exception($"Request failed with status code {_response.StatusCode}");
        }


        public string RebootCamera()
        {
            _logger.LogTrace("Rebooting camera : " + _cameraUrl);

            try
            {
                Reboot().Wait();
                var returnCode = parseCameraResponse(_response.Content.ReadAsStringAsync().Result);
                if (returnCode != null)
                {
                    _logger.LogError("Camera failed to Reboot with error code " + returnCode);
                    return returnCode;
                }

                _logger.LogInformation("Camera rebooted successfully " + _cameraUrl);
                return "pass";
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to Reboot Camera " + _cameraUrl + " with the following error: " + ex);
                return ex.ToString();
            }
        }

        private async Task Reboot()
        {
            var source = new CancellationTokenSource();
            var token = source.Token;

            var requestUri = $"{_cameraUrl}command=0x0811&type=F_FLAG&direction=WRITE&num=1&payload=1";

            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

            _response = await _client.SendAsync(request, token);
             if(!_response.IsSuccessStatusCode)
                    throw new Exception($"Request failed with status code {_response.StatusCode}");

        }


        //set the cert usage on a cert
        public string SetCertUsage(string certName, string usageCode)
        {
            _logger.LogTrace("Setting cert usage to " + usageCode + " for cert " + certName + " for camera " +
                             _cameraUrl);
            var payload = "0x00080000" + usageCode;
            var myId = HexadecimalEncoding.ToHexNoPadding(certName);
            var additionalPayload = payload + HexadecimalEncoding.ToHex(certName, 4, '0') + "0001" + myId;

            try
            {
                SetCertUsage(additionalPayload).Wait();
                var returnCode = parseCameraResponse(_response.Content.ReadAsStringAsync().Result);
                if (returnCode != null)
                {
                    _logger.LogError("Setting cert usage to " + usageCode + " for cert " + certName + " for camera " +
                                     _cameraUrl + " failed with error code " + returnCode);
                    return returnCode;
                }

                _logger.LogInformation("Successfully changed cert usage to " + usageCode + " for cert " + certName +
                                       " for camera " + _cameraUrl);
                return "pass";
            }
            catch (Exception ex)
            {
                _logger.LogError("Cert usage change failed with the following error: " + ex);
                return ex.ToString();
            }
        }

        //can be used to reset/clear existing cert usage and to set cert usage on a specific cert
        private async Task SetCertUsage(string payload)
        {
            var source = new CancellationTokenSource();
            var token = source.Token;

            var requestUri =
                $"{_cameraUrl}command=0x0BF2&type=P_OCTET&direction=WRITE&num=1&payload={Uri.EscapeDataString(payload)}";

            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

            _response = await _client.SendAsync(request, token);
            if (!_response.IsSuccessStatusCode)
                throw new Exception($"Request failed with status code {_response.StatusCode}");
        }


        //Delete the cert by name
        public string DeleteCertByName(string certName)
        {
            _logger.LogTrace("Delete cert " + certName + " for camera " + _cameraUrl);
            var myId = HexadecimalEncoding.ToHexNoPadding(certName);
            var payload = HexadecimalEncoding.ToHexWithPrefix(certName, 4, '0') + "0000" + myId +
                          "0004000200080003000000FF";

            try
            {
                //first reset the cert usage
                DeleteCert(payload).Wait();
                var returnCode = parseCameraResponse(_response.Content.ReadAsStringAsync().Result);
                if (returnCode != null)
                {
                    _logger.LogError("Deleting cert " + certName + " for camera " + _cameraUrl +
                                     " failed with error code " + returnCode);
                    return returnCode;
                }

                _logger.LogInformation("Successfully deleted cert " + certName + " for camera " + _cameraUrl);
                return "pass";
            }
            catch (Exception ex)
            {
                _logger.LogError("Deleting cert failed with the following error: " + ex);
                return ex.ToString();
            }
        }

        //delete a cert on camera
        private async Task DeleteCert(string payload)
        {
            var requestUri = $"{_cameraUrl}command=0x0BE9&type=P_OCTET&direction=WRITE&num=1&payload={payload}";
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            _response = await _client.SendAsync(request);
        }


        //returns error code if camera call fails, blank if successful
        private string parseCameraResponse(string response)
        {
            string errorCode = null;
            var xmlResponse = new XmlDocument();
            xmlResponse.LoadXml(response);

            var errorList = xmlResponse.GetElementsByTagName("err");
            if (errorList.Count > 0) errorCode = errorList[0].InnerXml;
            return errorCode;
        }

        public List<string> GetCameraCertList(string response)
        {
            var xmlResponse = new XmlDocument();
            xmlResponse.LoadXml(response);

            // Parse raw hex content from the response
            var s =
                xmlResponse.GetElementsByTagName("str")[0].InnerText
                    .Replace(" ", "")
                    .Replace("\r", "")
                    .Replace("\n", "");

            // Record structure starts with 2 bytes representing length of the record, followed by 6 more bytes, then filename, then a zero byte.
            // Iterate through records by reading length tag, extracting the filename in hex and converting.
            var certNames = new List<string>();
            Func<string, int, string> getName = (s, start) => s.Substring(start, s.IndexOf("00", start) - start);
            for (var i = 0; i < s.Length; i += Convert.ToInt32(s.Substring(i, 4), 16) * 2)
                certNames.Add(HexadecimalEncoding.FromHex(getName(s, i + 16)));
            return certNames;
        }

        private string ResolvePamField(IPAMSecretResolver pam, string key, string fieldName)
        {
            _logger.LogDebug($"Attempting to resolve PAM eligible field: '{fieldName}'");
            return string.IsNullOrEmpty(key) ? key : pam.Resolve(key);
        }
    }
}