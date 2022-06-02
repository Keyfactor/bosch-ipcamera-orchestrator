using System;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;


namespace Keyfactor.Extensions.Orchestrator.BoschIPCamera.Jobs
{
    public class Management : IManagementJobExtension
    {  
        private readonly ILogger<Management> _logger;
        public string ExtensionName => "BoschIPCamera";

        public Management(ILogger<Management> logger)
        {
            _logger = logger;
        }

        public JobResult ProcessJob(ManagementJobConfiguration jobConfiguration)
        {
            _logger.MethodEntry(LogLevel.Debug);
            return new JobResult();
        }
    }
}