using System.Linq;
using System.Threading.Tasks;
using Microting.InstallationCheckingBase.Infrastructure.Data;
using Microting.InstallationCheckingBase.Infrastructure.Data.Entities;
using Rebus.Handlers;
using ServiceInstallationCheckingPlugin.Messages;

namespace ServiceInstallationCheckingPlugin.Handlers
{
    public class EFormRetrievedHandler : IHandleMessages<eFormRetrieved>
    {
        private readonly eFormCore.Core _sdkCore;
        private readonly InstallationCheckingPnDbContext _dbContext;

        public EFormRetrievedHandler(eFormCore.Core sdkCore, InstallationCheckingPnDbContext dbContext)
        {
            _dbContext = dbContext;
            _sdkCore = sdkCore;
        }

        public async Task Handle(eFormRetrieved message)
        {
            // TODO
        }
    }
}