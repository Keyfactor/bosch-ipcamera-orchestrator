// Copyright 2026 Keyfactor
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

using System;
using System.Globalization;
using System.Text.RegularExpressions;

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

        public enum CertificateKeyType
        {
            Unknown,
            RSA1024,    // 0000 0000
            RSA2048,    // 0000 0001
            ECC256,     // 0000 0002
            RSA4096     // 0000 0003
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
        
        /// <summary>
        /// Maps the Keyfactor Command-provided key algorithm and size to the equivalent
        /// CertificateKeyType enum.
        /// ** NOTE: These values may need updated depending on the target camera OS.
        /// </summary>
        /// <param name="keyAlgorithm">i.e. RSA, ECP</param>
        /// <param name="keySize">i.e. 2048, 256</param>
        /// <returns>Enum representation of the [key algorithm]-[key size] the device API can interpret</returns>
        public static CertificateKeyType MapKeyType(string keyAlgorithm, string keySize)
        {
            return keyAlgorithm switch
            {
                "RSA" when keySize == "1024" => CertificateKeyType.RSA1024,
                "RSA" when keySize == "2048" => CertificateKeyType.RSA2048,
                "ECDSA" when keySize == "256" => CertificateKeyType.ECC256,
                "RSA" when keySize == "4096" => CertificateKeyType.RSA4096,
                _ => CertificateKeyType.Unknown
            };
        }
        
        /// <summary>
        /// Maps the CertificateKeyType enumeration to a human-readable string representation
        /// of the key algorithm and size.
        /// ** NOTE: These values may need updated depending on the target camera OS.
        /// </summary>
        public static string ToReadableText(this CertificateKeyType keyType)
        {
            switch (keyType)
            {
                case CertificateKeyType.RSA1024:
                    return "RSA 1024";
                case CertificateKeyType.RSA2048:
                    return "RSA 2048";
                case CertificateKeyType.ECC256:
                    return "Elliptic Curve P256";
                case CertificateKeyType.RSA4096:
                    return "RSA 4096";
                case CertificateKeyType.Unknown:
                    return "Unknown";
                default:
                    return "";
            }
        }
        
        /// <summary>
        /// Maps the CertificateKeyType enumeration to the 8-digit hex code that the device can intepret.
        /// ** NOTE: These values may need updated depending on the target camera OS.
        /// </summary>
        public static string ToKeyTypeCode(this CertificateKeyType keyType)
        {
            switch (keyType)
            {
                case CertificateKeyType.RSA1024:
                    return "00000000";
                case CertificateKeyType.RSA2048:
                    return "00000001";
                case CertificateKeyType.ECC256:
                    return "00000002";
                case CertificateKeyType.RSA4096:
                    return "00000003";
                case CertificateKeyType.Unknown:
                default:
                    return "";
            }
        }
        
        public static class CertName
        {
            /// <summary>
            /// Returns a UTC-based suffix, i.e. "2602171544"
            /// </summary>
            public static string GetUtcSuffix() =>
                DateTime.UtcNow.ToString("yyMMddHHmm", CultureInfo.InvariantCulture);

            /// <summary>
            /// Creates a unique certificate name by appending ['_' + Utc DateTime suffix] to the end of the user-supplied certificate name.
            /// Example: "_2602171544"
            /// </summary>
            public static string CreateUniqueCertName(string certName)
            {
                Regex rgx = new Regex(@"_[0-9]{10}$",RegexOptions.CultureInvariant);
                var m = Regex.Match(certName,@"_[0-9]{10}$");
                if (m.Success)
                {
                    return certName.Remove(m.Index, m.Length) + "_" + GetUtcSuffix();
                }

                return certName + "_" + GetUtcSuffix();
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
                public static string CERTIFICATE_OPTIONS = "0x0BED";
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
