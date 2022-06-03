﻿using System;
using System.Collections.Generic;
using RestSharp;
using System.Threading.Tasks;
using RestSharp.Authenticators.Digest;
using System.Threading;
using System.ServiceModel;
using System.Xml;

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
        public string certCreate(string certificateName)
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
                String returnCode = parseCameraResponse(_response.Content);
                if (returnCode != null)
                {
                    //  _logger.LogError("Camera failed to generate CSR with error code " + returnCode);
                    return "fail";
                }
                //    _logger.LogInformation("CSR call completed successfully for " + certificateName);
                return "pass";
            } 
            catch (ProtocolException ex)
            {
             //   _logger.LogError("CSR call failed with the following error: "+ ex.ToString());
            };

            return "fail";
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

            _response = await _client.GetAsync(request, token);
        }

        public string downloadCSRFromCamera(string cameraHostURL, string userName, string password, string certName)
        {
            //_logger.LogTrace("Download " + certName + " CSR from Camera: " + _cameraURL);
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

        //Enable/Disable 802.1x setting on the camera
        public string change8021xSettings(string onOffSwitch)
        {
            //_logger.LogTrace("Changing Camera 802.1x setting to " + onOffSwitch + " on Camera: " + _cameraURL);

            try
            {
                change8021x(onOffSwitch).Wait();
                String returnCode = parseCameraResponse(_response.Content);
                if (returnCode != null)
                {
                    // _logger.LogError("Camera failed to change 802.1x with error code " + returnCode);
                    return "fail";
                }
                return "pass";
                // _logger.LogInformation("802.1x setting changed successfully for " + _cameraURL);
            }
            catch (Exception ex)
            {
                //_logger.LogError("802.1x setting change failed with the following error: " + ex.ToString());
            };

            return "fail";
        }

        //Enable/Disable 802.1x on the camera after the certs are in place
        //onOffSwitch - "0" means off, "1" means on
        private async Task change8021x(string onOffSwitch)
        {
            CancellationTokenSource source = new CancellationTokenSource();
            CancellationToken token = source.Token;

            var request = new RestRequest()
                .AddQueryParameter("command", "0x09EB")
                .AddQueryParameter("type", "T_OCTET")
                .AddQueryParameter("direction", "WRITE")
                .AddQueryParameter("num", "1")
                .AddQueryParameter("payload", onOffSwitch);

            string requestValue = request.Resource;

            _response = await _client.GetAsync(request, token);

            string responeValue = _response.Content;
        }

        public string rebootCamera()
        {
            //_logger.LogTrace("Rebooting camera : " + _cameraURL);

            try
            {
                reboot().Wait();
                String returnCode = parseCameraResponse(_response.Content);
                if(returnCode != null)
                {
                    // _logger.LogError("Camera failed to reboot with error code " + returnCode);
                    return "fail";
                }
                return "pass";
                // _logger.LogInformation("Camera rebooted sucessfully " + _cameraURL);
            }
            catch (Exception ex)
            {
                //_logger.LogError("Failed to reboot Camera " + _cameraURL + " with the following error: " + ex.ToString());
            };

            return "fail";
        }

        private async Task reboot()
        {
            CancellationTokenSource source = new CancellationTokenSource();
            CancellationToken token = source.Token;

            var request = new RestRequest()
                .AddQueryParameter("command", "0x0811")
                .AddQueryParameter("type", "F_FLAG")
                .AddQueryParameter("direction", "WRITE")
                .AddQueryParameter("num", "1")
                .AddQueryParameter("payload", "1");

            string requestValue = request.Resource;

            _response = await _client.GetAsync(request, token);

        }

        //returns error code if camera call fails, blank if successful
        private string parseCameraResponse(String response)
        {
            String errorCode = null;
            XmlDocument xmlResponse = new XmlDocument();
            xmlResponse.LoadXml(response);

            XmlNodeList errorList = xmlResponse.GetElementsByTagName("err");
            if (errorList.Count > 0)
            {
                errorCode = errorList[0].InnerXml;
            }
            return errorCode;
        }
    }
}
