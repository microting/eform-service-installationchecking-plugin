/*
The MIT License (MIT)
Copyright (c) 2007 - 2019 Microting A/S
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using Microsoft.EntityFrameworkCore;
using Microting.eForm.Infrastructure.Models;
using Microting.InstallationCheckingBase.Infrastructure.Data;
using Microting.InstallationCheckingBase.Infrastructure.Enums;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microting.eForm.Infrastructure.Constants;

namespace ServiceInstallationCheckingPlugin.Scheduler.Jobs
{
    public class Job
    {
        private readonly eFormCore.Core _sdkCore;
        private readonly InstallationCheckingPnDbContext _dbContext;

        public Job(eFormCore.Core sdkCore, InstallationCheckingPnDbContext dbContext)
        {
            _dbContext = dbContext;
            _sdkCore = sdkCore;
        }

        public async Task Execute()
        {
            // Get installations to be moved to removals page
            var installations = await _dbContext.Installations
                .Where(x =>
                    x.DateRemove != null &&
                    x.DateRemove < DateTime.UtcNow &&
                    x.Type == InstallationType.Installation
                ).ToListAsync();

            foreach (var installation in installations)
            {
                var caseDto = await _sdkCore.CaseReadByCaseId(installation.SdkCaseId.GetValueOrDefault());

                if (caseDto != null)
                {
                    await _sdkCore.CaseDelete(caseDto.MicrotingUId.GetValueOrDefault());
                }

                var removalForm = new MainElement
                {
                    Id = 141709,
                    Repeated = 0,
                    Label = "(2) Radonmålinger Nedtagning",
                    StartDate = new DateTime(2019, 11, 4),
                    EndDate = new DateTime(2029, 11, 4),
                    Language = "da",
                    MultiApproval = false,
                    FastNavigation = false,
                    DisplayOrder = 0,
                };

                var entityGroup = await _sdkCore.EntityGroupCreate(
                    Constants.FieldTypes.EntitySearch,
                    "Removal devices " + installation.Id
                );

                var nextItemUid = 0;
                foreach (var meter in installation.Meters)
                {
                    await _sdkCore.EntitySearchItemCreate(
                        entityGroup.Id,
                        meter.QR,
                        "",
                        nextItemUid++.ToString()
                    );

                    var dataItems = new List<DataItem>();
                    var showPdf = new ShowPdf(
                        1,
                        true,
                        false,
                        "Vis PDF med kort over placering af målere",
                        "Det kan være en fordel at udskrive oversigten, før nedtagningsadresser besøges.",
                        "e8eaf6",
                        0,
                        false,
                        "https://eform.microting.com/app_files/uploads/20191008131612_14874_acb5333050e476e81c83bbcf5acd442c.pdf");

                    var saveButton = new SaveButton(
                        2,
                        true,
                        false,
                        "Tryk GEM DATA, når alle målere er QR-scannet",
                        "",
                        "f0f8db",
                        999,
                        false,
                        "GEM DATA");

                    dataItems.Add(showPdf);
                    dataItems.Add(saveButton);

                    var removalDate = DateTime.Now.ToString("yyyy-MM-dd");
                    var descriptionString = $"{installation.CompanyAddress}<br>{installation.CompanyAddress2}<br>{installation.ZipCode}<br>{installation.CityName}<br>{installation.CountryCode}<br><b>Nedtagningsdato: {removalDate}</b>";
                    var label = $"Måler {nextItemUid} - QR";

                    var dataElement = new DataElement(
                        nextItemUid,
                        label,
                        0,
                        descriptionString,
                        false,
                        false,
                        true,
                        false,
                        "",
                        false,
                        new List<DataItemGroup>(),
                        dataItems);

                    removalForm.ElementList.Add(dataElement);
                }

                removalForm = await _sdkCore.TemplateUploadData(removalForm);
                installation.RemovalFormId = await _sdkCore.TemplateCreate(removalForm);
                installation.Type = InstallationType.Removal;
                installation.SdkCaseId = null;
                await installation.Update(_dbContext);
            }
        }
    }
}