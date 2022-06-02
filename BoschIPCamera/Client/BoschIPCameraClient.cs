using System;
using System.Collections.Generic;
using RestSharp;
using System.Threading.Tasks;
using RestSharp.Authenticators.Digest;
using System.Threading;
using System.ServiceModel;

namespace Keyfactor.Extensions.Orchestrator.BoschIPCamera.Client
{
    public class BoschIPcameraClient
    {
        private static Dictionary<string, string> s_csrSubject = new Dictionary<string, string>();
        private string _cameraURL;
        private RestClient _client = null;
        private RestResponse _response = null;


        public void setupStandardBoschIPcameraClient(string cameraHostURL, string userName, string password)
        {
            ///_logger.LogTrace("Initializing RestSharp Client");
            _cameraURL = "http://" + cameraHostURL + "/rcp.xml?";
            //_logger.LogTrace("Camera URL: " + _cameraURL);
          
            _client = new RestClient(_cameraURL)
            {
                Authenticator = new DigestAuthenticator(userName, password)
            };
            setupCSRSubject();
        }

        //define standard Subject for CSR. IP address will vary by device to be passed in at runtime
        //likely all this data could be captured in the cert store definition - should not be hardcoded
        private static void setupCSRSubject()
        {
            s_csrSubject.Add("C", "US");
            s_csrSubject.Add("ST", "North Carolina");
            s_csrSubject.Add("L", "Apex");
            s_csrSubject.Add("O", "Homecheese");
            s_csrSubject.Add("OU", "IT");
            s_csrSubject.Add("CN", "172.78.231.174");
        }

        //need to think through the parameters sent in here
        public void certCreate(string certificateName)
        {
            string myCommon = HexadecimalEncoding.ToHexWithPadding(s_csrSubject["CN"]);
            string myOrg = HexadecimalEncoding.ToHexWithPadding(s_csrSubject["O"]);
            string myUnit = HexadecimalEncoding.ToHexWithPadding(s_csrSubject["OU"]);
            string myCountry = HexadecimalEncoding.ToHexWithPadding(s_csrSubject["C"]);
            string myCity = HexadecimalEncoding.ToHexWithPadding(s_csrSubject["L"]);
            string myProvince = HexadecimalEncoding.ToHexWithPadding(s_csrSubject["ST"]);
            string myId = HexadecimalEncoding.ToHexNoPadding(certificateName);

            string payload = HexadecimalEncoding.ToHexWithPrefix(certificateName, 4, '0') + "0000" + myId + "0008000100000001000800020000" + "0000";

            payload = payload + HexadecimalEncoding.ToHexStringLengthWithPadding(s_csrSubject["CN"], 4, '0') + "0005" + myCommon;
            payload = payload + HexadecimalEncoding.ToHexStringLengthWithPadding(s_csrSubject["O"], 4, '0') + "0006" + myOrg;
            payload = payload + HexadecimalEncoding.ToHexStringLengthWithPadding(s_csrSubject["OU"], 4, '0') + "0007" + myUnit;
            payload = payload + HexadecimalEncoding.ToHexStringLengthWithPadding(s_csrSubject["L"], 4, '0') + "0008" + myCity;
            payload = payload + HexadecimalEncoding.ToHexStringLengthWithPadding(s_csrSubject["C"], 4, '0') + "0009" + myCountry;
            payload = payload + HexadecimalEncoding.ToHexStringLengthWithPadding(s_csrSubject["ST"], 4, '0') + "000A" + myProvince;
           // _logger.LogTrace("Payload for CSR request: " + payload);

            try
            {
                generateCSROnCameraAsync(payload).Wait();
            //    _logger.LogInformation("CSR call completed successfully for " + certificateName);
            } 
            catch (ProtocolException ex)
            {
             //   _logger.LogError("CSR call failed with the following error: "+ ex.ToString());
            };
        }

        //Call the camera to generate a CSR
        private async Task generateCSROnCameraAsync(string payload)
        {
            CancellationTokenSource source = new CancellationTokenSource();
            CancellationToken token = source.Token;

            var request = new RestRequest()
                .AddQueryParameter("command", "0x0BEC")
                .AddQueryParameter("type", "P_OCTET")
                .AddQueryParameter("direction", "WRITE")
                .AddQueryParameter("num", "1")
                .AddQueryParameter("payload", payload);

            string requestValue = request.Resource;

            RestResponse response = await _client.GetAsync(request, token);

            string responeValue = response.Content;
        }

        public string downloadCSRFromCamera(string cameraHostURL, string userName, string password, string certName)
        {
            //_logger.LogTrace("Download " + certName + "CSR from Camera: " + cameraHostURL);
            bool haveCSR = false;
            int count = 0;
            //keep trying until we get the cert or try 10 times (wait 20 seconds each time)
            while (!haveCSR && count <= 10)
            {
                try
                {
                    Thread.Sleep(20000);
                    count++;
                    downloadCSR(cameraHostURL, userName, password, certName).Wait();
                   // _logger.LogInformation("CSR downloaded successfully for " + certName);
                    haveCSR = true;
                    return _response.Content;
                }
                catch (Exception ex)
                {
                    //_logger.LogError("CSR download failed with the following error: " + ex.ToString());
                };
            }

           // _logger.LogError("Failed to download CSR");
            return null;
        }
        private async Task downloadCSR(string cameraHostURL, string userName, string password, string certName)
        {
            CancellationTokenSource source = new CancellationTokenSource();
            CancellationToken token = source.Token;

            //_logger.LogTrace("Initializing RestSharp Client for CSR Download");
            string cameraURL = "http://" + cameraHostURL + "/cert_download/" + certName.Replace(" ", "%20") + ".pem?type=csr";
            //_logger.LogTrace("Camera URL: " + cameraURL);

            RestClient client = new RestClient(cameraURL)
            {
                Authenticator = new DigestAuthenticator(userName, password)
            };

            var request = new RestRequest();

            _response = await client.GetAsync(request, token);
        }

    }
}
