using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.InstallationCheckingBase.Infrastructure.Data;
using Microting.InstallationCheckingBase.Infrastructure.Enums;
using Rebus.Handlers;
using ServiceInstallationCheckingPlugin.Messages;

namespace ServiceInstallationCheckingPlugin.Handlers
{
    public class EFormCompletedHandler : IHandleMessages<eFormCompleted>
    {
        private readonly eFormCore.Core _sdkCore;
        private readonly InstallationCheckingPnDbContext _dbContext;

        private const int DAYS_BEFORE_REMOVE = 61;

        public EFormCompletedHandler(eFormCore.Core sdkCore, InstallationCheckingPnDbContext dbContext)
        {
            _dbContext = dbContext;
            _sdkCore = sdkCore;
        }
        
        public async Task Handle(eFormCompleted message)
        {
            var installation = await _dbContext.Installations
                .FirstOrDefaultAsync(x => 
                    x.State == InstallationState.Assigned &&
                    x.SdkCaseId == message.caseId
                );

            if (installation == null) return;

            if (installation.Type == InstallationType.Installation)
            {
                installation.DateRemove = DateTime.UtcNow.AddDays(DAYS_BEFORE_REMOVE);
            } 
            else
            {
                installation.State = InstallationState.Completed;
                installation.DateActRemove = DateTime.UtcNow;

                await _sdkCore.CaseDelete(message.microtingUId);
            }

            await installation.Update(_dbContext);
        }
    }
}