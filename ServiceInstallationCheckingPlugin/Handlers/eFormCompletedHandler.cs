using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.eForm.Infrastructure;
using Microting.eForm.Infrastructure.Data.Entities;
using Microting.InstallationCheckingBase.Infrastructure.Data;
using Microting.InstallationCheckingBase.Infrastructure.Data.Entities;
using Microting.InstallationCheckingBase.Infrastructure.Enums;
using Rebus.Handlers;
using ServiceInstallationCheckingPlugin.Messages;
using OpenStack.NetCoreSwiftClient.Extensions;
using CheckListValue = Microting.eForm.Infrastructure.Models.CheckListValue;
using Field = Microting.eForm.Infrastructure.Models.Field;
using FieldValue = Microting.eForm.Infrastructure.Models.FieldValue;

namespace ServiceInstallationCheckingPlugin.Handlers
{
    public class EFormCompletedHandler : IHandleMessages<eFormCompleted>
    {
        private readonly eFormCore.Core _sdkCore;
        private readonly InstallationCheckingPnDbContext _dbContext;

        private const int DaysBeforeRemove = 61;

        public EFormCompletedHandler(eFormCore.Core sdkCore, InstallationCheckingPnDbContext dbContext)
        {
            _dbContext = dbContext;
            _sdkCore = sdkCore;
        }

        public async Task Handle(eFormCompleted message)
        {
            Console.WriteLine("[INF] EFormCompletedHandler.Handle: called");
            var installation = await _dbContext.Installations
                .FirstOrDefaultAsync(x =>
                    x.State == InstallationState.Assigned &&
                    (x.InstallationSdkCaseId == message.microtingUId || x.RemovalSdkCaseId == message.microtingUId)
                );

            if (installation == null) return;
            await using MicrotingDbContext microtingDbContext = _sdkCore.DbContextHelper.GetDbContext();
            Language language = await microtingDbContext.Languages.SingleAsync(x => x.LanguageCode == "da");

            Console.WriteLine("[INF] EFormCompletedHandler.Handle: installation != null");
            if (installation.Type == InstallationType.Installation)
            {
                Console.WriteLine("[INF] EFormCompletedHandler.Handle: installation.Type == InstallationType.Installation");
                var replyElement = await _sdkCore.CaseRead(message.microtingUId, message.checkUId, language);
                var checkListValue = (CheckListValue) replyElement.ElementList[0];
                var fields = checkListValue.DataItemList.Select(di => di as Field).ToList();
                if (fields.Any())
                {
                    if (!string.IsNullOrEmpty(fields[0]?.FieldValues[0]?.Value))
                    {
                        installation.CadastralNumber = fields[0]?.FieldValues[0]?.Value;
                    }

                    if (!string.IsNullOrEmpty(fields[1]?.FieldValues[0]?.Value))
                    {
                        installation.PropertyNumber = fields[1].FieldValues[0].Value;
                    }

                    if (!string.IsNullOrEmpty(fields[2]?.FieldValues[0]?.Value))
                    {
                        installation.ApartmentNumber = fields[2].FieldValues[0].Value;
                    }

                    if (!string.IsNullOrEmpty(fields[3]?.FieldValues[0]?.ValueReadable))
                    {
                        installation.CadastralType = fields[3].FieldValues[0].ValueReadable;
                    }

                    if (!string.IsNullOrEmpty(fields[4]?.FieldValues[0]?.Value))
                    {
                        installation.YearBuilt = int.Parse(fields[4].FieldValues[0].Value);
                    }

                    if (!string.IsNullOrEmpty(fields[5]?.FieldValues[0]?.Value))
                    {
                        installation.LivingFloorsNumber = int.Parse(fields[5].FieldValues[0].Value);
                    }

                    // This should be higher than 0 since the field is mandatory.
                    if (fields[6].FieldValues.Count > 0)
                    {
                        foreach (FieldValue fieldValue in fields[6].FieldValues)
                        {
                            if (installation.InstallationImageName == "")
                            {
                                installation.InstallationImageName = fieldValue.UploadedDataObj.FileName;
                            }
                            else
                            {
                                try
                                {
                                    installation.InstallationImageName += $",{fieldValue.UploadedDataObj.FileName}";
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"[ERR] EFormCompletedHandler.Handle: InstallationImageName += gave error {ex.Message}");
                                }

                            }
                        }
                    }

                    Console.WriteLine("[INF] EFormCompletedHandler.Handle: start looping fields");
                    foreach (var field in fields.Skip(8))
                    {

                        Console.WriteLine($"[INF] EFormCompletedHandler.Handle: parsing field {field.Label}");
                        var rgx = new Regex(@"Måler (?<Num>\d*) - (?<Name>.*)");
                        var match = rgx.Match(field.Label);

                        if (!match.Success || field.FieldValues[0].Value.IsNullOrEmpty()) continue;

                        var num = int.Parse(match.Groups["Num"]?.Value);
                        var name = match.Groups["Name"].Value;

                        var meter = installation.Meters.FirstOrDefault(m => m.Num == num)
                                    ?? new Meter() {Num = num};

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

                        if (installation.Meters.All(m => m.Num != num))
                        {
                            installation.Meters.Add(meter);
                        }
                    }
                }
                Console.WriteLine("[INF] EFormCompletedHandler.Handle: done looping fields");

                installation.DateInstall = DateTime.UtcNow;
                installation.DateRemove = DateTime.UtcNow.AddDays(DaysBeforeRemove);

                await _sdkCore.CaseDelete(message.microtingUId);

                installation.Type = InstallationType.Removal;
                installation.State = InstallationState.NotAssigned;
                await installation.Update(_dbContext);
            }
            else
            {
                installation.DateActRemove = DateTime.UtcNow;

                await _sdkCore.CaseDelete(message.microtingUId);

                installation.State = InstallationState.Completed;
                await installation.Update(_dbContext);
            }

        }
    }
}