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
using System.IO;
using System.Reflection;
using System.Text;

namespace ServiceInstallationCheckingPlugin.Scheduler.Jobs
{
    using Microting.eForm.Infrastructure.Constants;

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
                
                MainElement removalForm;

                var assembly = Assembly.GetExecutingAssembly();
                var assemblyName = assembly.GetName().Name;
                
                using (var formStream = assembly.GetManifestResourceStream($"{assemblyName}.Resources.removal_form.xml"))
                using (var reader = new StreamReader(formStream, Encoding.UTF8))
                {
                    var formString = await reader.ReadToEndAsync();
                    removalForm = await _sdkCore.TemplateFromXml(formString);
                }
                
                var dataElement = (DataElement) removalForm.ElementList[0];
                
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
                    
                    var label = $"Måler {nextItemUid} - QR";
                    var sourceId = entityGroup.Id;
                    
                    // TODO Find out how to create DataItem
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