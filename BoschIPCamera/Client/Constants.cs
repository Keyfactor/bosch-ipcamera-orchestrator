// Copyright 2023 Keyfactor
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Keyfactor.Extensions.Orchestrator.BoschIPCamera.Client
{
    public static class Constants
    {
        public enum CertificateUsage
        {
            None,
            HTTPS,          // 0000 0000
            EAP_TLS_Client, // 0000 0001
            TLS_DATE_Client // 0000 0002
        }

        public static CertificateUsage ParseCertificateUsage(string usageText)
        {
            switch (usageText)
            {
                case "00000000":
                case "HTTPS":
                    return CertificateUsage.HTTPS;
                case "00000001":
                case "EAP-TLS-client":
                    return CertificateUsage.EAP_TLS_Client;
                case "00000002":
                case "TLS-DATE-client":
                    return CertificateUsage.TLS_DATE_Client;
                case "":
                case null:
                default:
                    return CertificateUsage.None;
            }
        }

        public static string ToReadableText(this CertificateUsage usage)
        {
            switch (usage)
            {
                case CertificateUsage.HTTPS:
                    return "HTTPS";
                case CertificateUsage.EAP_TLS_Client:
                    return "EAP-TLS-client";
                case CertificateUsage.TLS_DATE_Client:
                    return "TLS-DATE-client";
                case CertificateUsage.None:
                default:
                    return "";
            }
        }

        public static string ToUsageCode(this CertificateUsage usage)
        {
            switch (usage)
            {
                case CertificateUsage.HTTPS:
                    return "00000000";
                case CertificateUsage.EAP_TLS_Client:
                    return "00000001";
                case CertificateUsage.TLS_DATE_Client:
                    return "00000002";
                case CertificateUsage.None:
                default:
                    return "";
            }
        }

        public static class API
        {
            public static class Endpoints
            {
                public static string CERTIFICATE = "0x0BE9";
                public static string CERTIFICATE_LIST = "0x0BEB";
                public static string CERTIFICATE_REQUEST = "0x0BEC";
                public static string CERTIFICATE_USAGE = "0x0BF2";
                public static string EAP_ENABLE = "0x09EB";
                public static string BOARD_RESET = "0x0811";
            }

            public static class Type
            {
                public static string T_OCTET = "T_OCTET";
                public static string P_OCTET = "P_OCTET";
                public static string F_FLAG = "F_FLAG";

            }

            public static class Direction
            {
                public static string READ = "READ";
                public static string WRITE = "WRITE";
            }

            public static string BuildRequestUri(string endpoint, string type, string direction, string payload = null)
            {
                string uri = $"command={endpoint}&type={type}&direction={direction}&num=1";

                if (!string.IsNullOrWhiteSpace(payload))
                {
                    uri = $"{uri}&payload={payload}";
                }

                return uri;
            }
        }
    }
}
