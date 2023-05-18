using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.Orchestrator.BoschIPCamera.Client
{
    public class BoschIpCameraClient
    {
        private static Dictionary<string, string> _sCsrSubject = new Dictionary<string, string>();
        private readonly string _cameraUrl;
        private readonly string _baseUrl;
        private readonly HttpClient _client;
        private readonly ILogger _logger;
        private HttpResponseMessage _response;
        private readonly string _userName;
        private readonly string _password;


        public BoschIpCameraClient(string cameraHostUrl, string userName, string password,
            Dictionary<string, string> csrSubject,
            ILogger logger)
        {
            _baseUrl = $"https://{cameraHostUrl}";
            _cameraUrl = $"https://{cameraHostUrl}/rcp.xml?";

            //This will ignore certificate errors in test mode since we don't have a valid cert for the camera on the public IP
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };

            _userName = userName;
            _password = password;
            var credentials = $"{_userName}:{password}";
            var encodedCredentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));

            _client = new HttpClient(handler);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encodedCredentials);

            _sCsrSubject = csrSubject;
            _logger = logger;
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
                Download(c,_userName,_password).Wait();
                files.Add(c, _response.Content.ReadAsStringAsync().Result);
            }

            return files;
        }

        //need to think through the parameters sent in here
        public string CertCreate(string certificateName)
        {
            try
            {
                var myCommon = HexadecimalEncoding.ToHexWithPadding(_sCsrSubject["CN"]);
                var myOrg = HexadecimalEncoding.ToHexWithPadding(_sCsrSubject["O"]);
                var myUnit = HexadecimalEncoding.ToHexWithPadding(_sCsrSubject["OU"]);
                var myCountry = HexadecimalEncoding.ToHexWithPadding(_sCsrSubject["C"]);
                var myCity = HexadecimalEncoding.ToHexWithPadding(_sCsrSubject["L"]);
                var myProvince = HexadecimalEncoding.ToHexWithPadding(_sCsrSubject["ST"]);
                var myId = HexadecimalEncoding.ToHexNoPadding(certificateName);

                var payload =
                    $"{HexadecimalEncoding.ToHexWithPrefix(certificateName, 4, '0')}0000{myId}00080001000000010008000200000000";
                payload +=
                    $"{HexadecimalEncoding.ToHexStringLengthWithPadding(_sCsrSubject["CN"], 4, '0')}0005{myCommon}";
                payload += $"{HexadecimalEncoding.ToHexStringLengthWithPadding(_sCsrSubject["O"], 4, '0')}0006{myOrg}";
                payload +=
                    $"{HexadecimalEncoding.ToHexStringLengthWithPadding(_sCsrSubject["OU"], 4, '0')}0007{myUnit}";
                payload += $"{HexadecimalEncoding.ToHexStringLengthWithPadding(_sCsrSubject["L"], 4, '0')}0008{myCity}";
                payload +=
                    $"{HexadecimalEncoding.ToHexStringLengthWithPadding(_sCsrSubject["C"], 4, '0')}0009{myCountry}";
                payload +=
                    $"{HexadecimalEncoding.ToHexStringLengthWithPadding(_sCsrSubject["ST"], 4, '0')}000A{myProvince}";

                GenerateCsrOnCameraAsync(payload).Wait();
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
        private static async Task GenerateCsrOnCameraAsync(string payload)
        {
            using var httpClient = new HttpClient();
            var queryParams = new Dictionary<string, string>
            {
                {"command", "0x0BEC"},
                {"type", "P_OCTET"},
                {"direction", "WRITE"},
                {"num", "1"},
                {"payload", payload}
            };

            var queryString = new FormUrlEncodedContent(queryParams).ReadAsStringAsync();
            var requestUri = "your_request_uri?" + await queryString;

            using var cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;
            var _ = await httpClient.GetAsync(requestUri, token);
        }

        public string DownloadCsrFromCamera(string cameraHostUrl, string userName, string password, string certName)
        {
            _logger.LogTrace("Download " + certName + " CSR from Camera: " + _cameraUrl);
            var haveCsr = false;
            var count = 0;
            //keep trying until we get the cert or try 30 times (wait 5 seconds each time)
            while (!haveCsr && count <= 30)
                try
                {
                    Thread.Sleep(5000);
                    count++;
                    Download(_userName, _password, certName, "?type=csr").Wait();
                    // _logger.LogInformation("CSR downloaded successfully for " + certName);
                    haveCsr = true;
                    return _response.Content.ReadAsStringAsync().Result;
                }
                catch (Exception ex)
                {
                    _logger.LogTrace("CSR Download failed with the following error: " + ex);
                }

            _logger.LogError("Failed to Download CSR");
            return null;
        }

        private async Task Download(string certName,string userName,string password,
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

            using var response = await _client.SendAsync(request, token);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Request failed with status code {response.StatusCode}");

            _response = response;
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

            using (var response = await _client.SendAsync(request, token))
            {
                if (!response.IsSuccessStatusCode)
                    throw new Exception($"Request failed with status code {response.StatusCode}");

                _response = response;
            }
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

            using var response = await _client.SendAsync(request, token);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Request failed with status code {response.StatusCode}");

            _response = response;
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
            var source = new CancellationTokenSource();
            var token = source.Token;

            var requestUri = $"{_cameraUrl}command=0x0BE9&type=P_OCTET&direction=WRITE&num=1&payload={payload}";

            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

            using (var response = await _client.SendAsync(request, token))
            {
                if (!response.IsSuccessStatusCode)
                    throw new Exception($"Request failed with status code {response.StatusCode}");

                _response = response;
            }
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
    }
}