using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.eForm.Infrastructure.Models;
using Microting.InstallationCheckingBase.Infrastructure.Data;
using Microting.InstallationCheckingBase.Infrastructure.Data.Entities;
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
            Debugger.Break();
            var installation = await _dbContext.Installations
                .FirstOrDefaultAsync(x => 
                    x.State == InstallationState.Assigned &&
                    x.SdkCaseId == message.microtingUId
                );

            if (installation == null) return;

            if (installation.Type == InstallationType.Installation)
            {
                var replyElement = await _sdkCore.CaseRead(message.microtingUId, message.checkUId);
                var checkListValue = (CheckListValue)replyElement.ElementList[0];
                var fields = checkListValue.DataItemList;

                // TODO Fill installation information from fields

                foreach (Field field in fields)
                {
                    var rgx = new Regex(@"Måler (?<Num>\d*) - (?<Name>.*)");
                    var match = rgx.Match(field.Label);

                    if (match.Success)
                    {
                        var num = int.Parse(match.Groups["Num"].Value);
                        var name = match.Groups["Name"].Value;
                        var meter = installation.Meters.FirstOrDefault(m => m.Num == num);

                        if (meter == null)
                        {
                            meter = new Meter() { Num = num };
                        }

                        switch (name)
                        {
                            case "QR":
                                meter.QR = field.FieldValues[0].Value;
                                break;
                            case "Rumtype":
                                meter.RoomType = field.FieldValues[0].ValueReadable;
                                break;
                            case "Etage":
                                meter.Floor = int.Parse(field.FieldValues[0].Value);
                                break;
                            case "Rumnavn":
                                meter.RoomName = field.FieldValues[0].Value;
                                break;
                        }

                        if (!installation.Meters.Any(m => m.Num == num))
                        {
                            installation.Meters.Add(meter);
                        }
                    }
                }

                installation.DateRemove = DateTime.UtcNow.AddDays(DAYS_BEFORE_REMOVE);
            } 
            else
            {
                installation.DateActRemove = DateTime.UtcNow;
            }

            await _sdkCore.CaseDelete(message.microtingUId);

            installation.State = InstallationState.Completed;
            await installation.Update(_dbContext);
        }
    }
}