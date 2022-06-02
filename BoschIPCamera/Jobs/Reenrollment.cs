using Keyfactor.Extensions.Orchestrator.BoschIPCamera.Client;
using Keyfactor.Extensions.Orchestrator.GcpCertManager;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Keyfactor.Extensions.Orchestrator.BoschIPCamera.Jobs
{
    public class Reenrollment : IReenrollmentJobExtension
    {
        public string ExtensionName => "BoschIPCamera";
        private readonly ILogger<Reenrollment> _logger;

        public Reenrollment(ILogger<Reenrollment> logger)
        {
            _logger = logger;
        }

        public JobResult ProcessJob(ReenrollmentJobConfiguration jobConfiguration, SubmitReenrollmentCSR submitReenrollmentUpdate)
        {
            _logger.MethodEntry(LogLevel.Debug);
            return PerformReenrollment(jobConfiguration, submitReenrollmentUpdate);
        }

        private JobResult PerformReenrollment(ReenrollmentJobConfiguration jobConfiguration, SubmitReenrollmentCSR submitReenrollmentUpdate)
        {

            try
            {

                var sb = new StringBuilder();
                sb.Append("");

                _logger.MethodEntry(LogLevel.Debug);
                _logger.LogTrace($"Reenrollment Config {JsonConvert.SerializeObject(jobConfiguration)}");
                _logger.LogTrace($"Reenrollment CSR Config {JsonConvert.SerializeObject(submitReenrollmentUpdate)}");

                BoschIPcameraClient client = new BoschIPcameraClient();

                //need to parse the jobConfiguration for the cert details - create a map like in the BoschIPCamera class and pass it in

                //generate the CSR on the camera
                client.setupStandardBoschIPcameraClient(jobConfiguration.CertificateStoreDetails.ClientMachine, jobConfiguration.ServerUsername,
                    jobConfiguration.ServerPassword);
                client.certCreate("keyfactor");

                //get the CSR from the camera
                string responseContent = client.downloadCSRFromCamera(jobConfiguration.CertificateStoreDetails.ClientMachine, jobConfiguration.ServerUsername,
                    jobConfiguration.ServerPassword, "keyfactor");


                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Success,
                    JobHistoryId = jobConfiguration.JobHistoryId,
                    FailureMessage = sb.ToString()
                };


            }
            catch (Exception e)
            {
                _logger.LogError($"PerformInventory Error: {e.Message}");
                throw;
            }
            
        }

    }
}
