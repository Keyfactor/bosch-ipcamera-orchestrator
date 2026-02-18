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

using Keyfactor.Extensions.Orchestrator.BoschIPCamera.Client;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Keyfactor.Extensions.Orchestrator.BoschIPCamera.Jobs
{
    public class Reenrollment : IReenrollmentJobExtension
    {
        public string ExtensionName => "BoschIPCamera";
        private readonly ILogger _logger;
        private readonly IPAMSecretResolver _pam;

        public Reenrollment(IPAMSecretResolver pam)
        {
            _logger = LogHandler.GetClassLogger<Reenrollment>();
            _pam = pam;
        }

        public JobResult ProcessJob(ReenrollmentJobConfiguration jobConfiguration, SubmitReenrollmentCSR submitReenrollmentUpdate)
        {
            _logger.MethodEntry(LogLevel.Debug);
            return PerformReenrollment(jobConfiguration, submitReenrollmentUpdate);
        }

        private JobResult PerformReenrollment(ReenrollmentJobConfiguration jobConfiguration, SubmitReenrollmentCSR submitReenrollment)
        {

            try
            {
                _logger.MethodEntry(LogLevel.Debug);

                var client = new BoschIpCameraClient(jobConfiguration, jobConfiguration.CertificateStoreDetails, _pam, _logger);

                string certName = GetRequiredReenrollmentField(jobConfiguration.JobProperties, "Name").ToString();
                bool overwrite = (bool) GetRequiredReenrollmentField(jobConfiguration.JobProperties, "Overwrite");
                string csrInput = GetRequiredReenrollmentField(jobConfiguration.JobProperties, "subjectText").ToString();
                string certUsage = GetRequiredReenrollmentField(jobConfiguration.JobProperties, "CertificateUsage").ToString();
                string keyAlgorithm = GetRequiredReenrollmentField(jobConfiguration.JobProperties,"keyType").ToString();
                string keySize = GetRequiredReenrollmentField(jobConfiguration.JobProperties,"keySize").ToString();

                string returnCode;
                string errorMessage;
                string cameraUrl = jobConfiguration.CertificateStoreDetails.ClientMachine;
                bool oldCertExists = false;
                
                // get the existing certificate name associated with the supplied cert usage
                Constants.CertificateUsage certUsageEnum = Constants.ParseCertificateUsage(certUsage);
                string oldCertName = client.GetCertWithUsage(certUsageEnum);
                if(!string.IsNullOrEmpty(oldCertName))
                {
                    oldCertExists = true;
                    _logger.LogDebug($"Found Existing cert name '{oldCertName}' with certificate usage '{certUsage}'");
                    
                    // compare the old certificate name with the new certificate name ---
                    // 1) if the names are the same, append a reserved time-based suffix to the end of the name
                    // this new name [CertA_Timestamp] will be used to create the new cert
                    // OR
                    // 2) EDGE CASE: if the old certificate name currently tied to the cert usage does NOT match the new certificate name,
                    // also create a new name [CertB_Timestamp] for the new cert in case the user-supplied cert name is already
                    // associated with an existing certificate that is NOT bound to a cert usage
                    certName = Constants.CertName.CreateUniqueCertName(certName);
                    _logger.LogDebug($"Name for new certificate has been updated to '{certName}' to ensure uniqueness");
                   
                    
                }
                else
                {
                    _logger.LogDebug($"No existing certificate found with certificate usage '{certUsage}'");
                    
                    // if overwrite is checked, delete the existing certificate (if one exists)
                    // this is done to avoid errors generating a CSR with a name that is already in use;
                    // since the existing certificate is not currently bound, this will not cause an outage
                    if (overwrite)
                    {
                        returnCode = client.DeleteCertByName(certName);

                        if (returnCode != "pass")
                        {
                            errorMessage = $"Error deleting existing certificate {certName} on camera {cameraUrl} with error code {returnCode}";
                            _logger.LogError(errorMessage);
                            return new JobResult
                            {
                                Result = OrchestratorJobStatusJobResult.Failure,
                                JobHistoryId = jobConfiguration.JobHistoryId,
                                FailureMessage = errorMessage
                            };
                        }
                    }
                }

                // setup the CSR details
                var csrSubject = SetupCsrSubject(csrInput);
                
                // map the key type and key size from the job properties to a corresponding key type available on the device
                Constants.CertificateKeyType keyEnum = Constants.MapKeyType(keyAlgorithm,keySize);
                
                _logger.LogDebug($"Mapped Key Type: {keyEnum.ToReadableText()}");
                if (keyEnum == Constants.CertificateKeyType.Unknown)
                {
                    errorMessage = $"The requested enrollment key algorithm '{keyAlgorithm}' and key size '{keySize}' is Unknown and cannot be used to create a CSR.";
                    _logger.LogError(errorMessage);
                    return new JobResult
                    {
                        Result = OrchestratorJobStatusJobResult.Failure,
                        JobHistoryId = jobConfiguration.JobHistoryId,
                        FailureMessage = errorMessage
                    };
                }

                //generate the CSR on the camera
                returnCode = client.CertCreate(csrSubject, certName, keyEnum);

                if (returnCode != "pass")
                {
                    errorMessage = $"Error generating CSR for {certName} on camera {cameraUrl} with error code {returnCode}";
                    _logger.LogError(errorMessage);
                    return new JobResult
                    {
                        Result = OrchestratorJobStatusJobResult.Failure,
                        JobHistoryId = jobConfiguration.JobHistoryId,
                        FailureMessage = errorMessage
                    };
                }

                //get the CSR from the camera
                var csr = client.DownloadCsrFromCamera(certName);
                _logger.LogTrace("Downloaded CSR: " + csr);

                // check that csr meets csr format
                // 404 message response can be returned instead
                if (!csr.StartsWith("-----BEGIN"))
                {
                    // error downloaded, no CSR present
                    // likely due to existing cert that was not marked to ovewrite (delete)
                    errorMessage = $"Error retrieving CSR from camera {cameraUrl} - got response: {csr}. " +
                                   $"Possible reasons for error --- The requested enrollment key algorithm '{keyAlgorithm}' and key size '{keySize}' is not supported on this specific device; " +
                                   $"The 'Name' provided for the new certificate already exists on the camera and Overwrite was not checked.";
                    _logger.LogError(errorMessage);
                    return new JobResult
                    {
                        Result = OrchestratorJobStatusJobResult.Failure,
                        JobHistoryId = jobConfiguration.JobHistoryId,
                        FailureMessage = errorMessage
                    };
                }

                // sign CSR in Keyfactor
                var x509Cert = submitReenrollment.Invoke(csr);

                if (x509Cert == null)
                {
                    errorMessage = $"Error submitting CSR to Keyfactor. Certificate not received. CSR submitted: {csr}";
                    _logger.LogError(errorMessage);
                    return new JobResult
                    {
                        Result = OrchestratorJobStatusJobResult.Failure,
                        JobHistoryId = jobConfiguration.JobHistoryId,
                        FailureMessage = errorMessage
                    };
                }

                // build PEM content
                StringBuilder pemBuilder = new StringBuilder();
                pemBuilder.AppendLine("-----BEGIN CERTIFICATE-----");
                pemBuilder.AppendLine(Convert.ToBase64String(x509Cert.RawData, Base64FormattingOptions.InsertLineBreaks));
                pemBuilder.AppendLine("-----END CERTIFICATE-----");
                var pemCert = pemBuilder.ToString();

                pemCert = pemCert.Replace("\r", "");
                _logger.LogTrace("Uploading cert: " + pemCert);

                // upload the signed cert to the camera
                client.UploadCert(certName +".cer", pemCert);

                // turn on 802.1x
                returnCode = client.Change8021XSettings(true);
                if (returnCode != "pass")
                {
                     errorMessage = $"Error setting 802.1x to on for {certName} on camera {cameraUrl} with error code {returnCode}";
                    _logger.LogError(errorMessage);
                    return new JobResult
                    {
                        Result = OrchestratorJobStatusJobResult.Failure,
                        JobHistoryId = jobConfiguration.JobHistoryId,
                        FailureMessage = errorMessage
                    };
                }

                // set cert usage
                Constants.CertificateUsage usageEnum = Constants.ParseCertificateUsage(certUsage);

                returnCode = client.SetCertUsage(certName, usageEnum);
                if (returnCode != "pass")
                {
                    errorMessage = $"Error setting certUsage of {certUsage} for certificate {certName} on camera {cameraUrl} with error code {returnCode}";
                    _logger.LogError(errorMessage);
                    return new JobResult
                    {
                        Result = OrchestratorJobStatusJobResult.Failure,
                        JobHistoryId = jobConfiguration.JobHistoryId,
                        FailureMessage = errorMessage
                    };
                }
                
                // delete existing certificate if overwriting and an existing certificate was previously bound to the cert usage
                if (overwrite && oldCertExists)
                {
                    returnCode = client.DeleteCertByName(oldCertName);

                    if (returnCode != "pass")
                    {
                        errorMessage = $"Error deleting existing certificate {oldCertName} on camera {cameraUrl} with error code {returnCode}";
                        _logger.LogError(errorMessage);
                        return new JobResult
                        {
                            Result = OrchestratorJobStatusJobResult.Failure,
                            JobHistoryId = jobConfiguration.JobHistoryId,
                            FailureMessage = errorMessage
                        };
                    }
                }

                //reboot the camera
                client.RebootCamera();
                if (returnCode != "pass")
                {
                    errorMessage = $"Error rebooting camera {cameraUrl} with error code {returnCode}";
                    _logger.LogError(errorMessage);
                    return new JobResult
                    {
                        Result = OrchestratorJobStatusJobResult.Failure,
                        JobHistoryId = jobConfiguration.JobHistoryId,
                        FailureMessage = errorMessage
                    };
                }

                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Success,
                    JobHistoryId = jobConfiguration.JobHistoryId,
                    FailureMessage = ""
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"PerformReenrollment Error: {e.Message}");
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Failure,
                    JobHistoryId = jobConfiguration.JobHistoryId,
                    FailureMessage = e.Message
                };
            }
            
        }

        private object GetRequiredReenrollmentField(Dictionary<string, object> jobProperties, string fieldName)
        {
            _logger.LogTrace($"Checking for required field '{fieldName}' in Reenrollment Job Properties");

            if (jobProperties.ContainsKey(fieldName))
            {
                var requiredField = jobProperties[fieldName];
                if (requiredField != null)
                {
                    _logger.LogTrace($"Required field '{fieldName}' found with value '{requiredField}'");
                    return requiredField;
                }
                else
                {
                    string message = $"Required field '{fieldName}' was present in Reenrollment Job Properties but had no value";
                    _logger.LogError(message);
                    throw new MissingFieldException(message);
                }
            }
            else
            {
                string message = $"Required field '{fieldName}' was not present in the Reenrollment Job Properties";
                _logger.LogError(message);
                throw new MissingFieldException(message);
            }
        }

        private Dictionary<string, string> SetupCsrSubject(string subjectText)
        {
            var csrSubject = new Dictionary<string, string>();
            _logger.LogTrace($"Parsing subject text: {subjectText}");
            var splitSubject = subjectText.Split(',');
            foreach (string subjectElement in splitSubject)
            {
                _logger.LogTrace($"Splitting subject element: {subjectElement}");
                var splitSubjectElement = subjectElement.Split('=');
                var name = splitSubjectElement[0].Trim();
                var value = splitSubjectElement[1].Trim();
                
                _logger.LogTrace($"Adding subject element: '{name}' with value '{value}'");
                csrSubject.Add(name, value);
            }

            return csrSubject;
        }
    }
}
