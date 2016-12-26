using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EnvDTE;
using Microsoft.VisualStudio.Shell.Interop;
using MuleSoft.RAML.Tools.Properties;
using Raml.Common;

namespace MuleSoft.RAML.Tools
{
    public class RamlScaffoldServiceWebApi : RamlScaffoldServiceBase
    {
        private readonly string newtonsoftJsonPackageVersion = Settings.Default.NewtonsoftJsonPackageVersion;

        public override string TemplateSubFolder
        {
            get { return "RAMLWebApi2Scaffolder"; }
        }

        public RamlScaffoldServiceWebApi(IT4Service t4Service, IServiceProvider serviceProvider): base(t4Service, serviceProvider){}

        public override void AddContract(RamlChooserActionParams parameters)
        {
            var dte = ServiceProvider.GetService(typeof(SDTE)) as DTE;
            var proj = VisualStudioAutomationHelper.GetActiveProject(dte);

            InstallNugetDependencies(proj, newtonsoftJsonPackageVersion);
            AddXmlFormatterInWebApiConfig(proj);

            var folderItem = VisualStudioAutomationHelper.AddFolderIfNotExists(proj, ContractsFolderName);
            var contractsFolderPath = Path.GetDirectoryName(proj.FullName) + Path.DirectorySeparatorChar + ContractsFolderName + Path.DirectorySeparatorChar;

            var targetFolderPath = GetTargetFolderPath(contractsFolderPath, parameters.TargetFileName);
            if (!Directory.Exists(targetFolderPath))
                Directory.CreateDirectory(targetFolderPath);

            if (string.IsNullOrWhiteSpace(parameters.RamlSource) && !string.IsNullOrWhiteSpace(parameters.RamlTitle))
            {
                AddEmptyContract(folderItem, contractsFolderPath, parameters);
            }
            else
            {
                AddContractFromFile(folderItem, contractsFolderPath, parameters);
            }

        }

        protected override string GetTargetFolderPath(string folderPath, string targetFilename)
        {
            return folderPath;
        }

        private static void AddXmlFormatterInWebApiConfig(Project proj)
        {
            var appStart = proj.ProjectItems.Cast<ProjectItem>().FirstOrDefault(i => i.Name == "App_Start");
            if (appStart == null) return;

            var webApiConfig = appStart.ProjectItems.Cast<ProjectItem>().FirstOrDefault(i => i.Name == "WebApiConfig.cs");
            if (webApiConfig == null) return;

            var path = webApiConfig.FileNames[0];
            var lines = File.ReadAllLines(path).ToList();

            if (lines.Any(l => l.Contains("XmlSerializerFormatter")))
                return;

            InsertLine(lines);

            File.WriteAllText(path, string.Join(Environment.NewLine, lines));
        }

        private static void InsertLine(List<string> lines)
        {
            var line = FindLineWith(lines, "Register(HttpConfiguration config)");
            var inserted = false;

            if (line != -1)
            {
                if (lines[line + 1].Contains("{"))
                {
                    InsertLines(lines, line + 2);
                    inserted = true;
                }
            }

            if (inserted) return;

            line = FindLineWith(lines, ".MapHttpAttributeRoutes();");
            if (line != -1)
            {
                InsertLines(lines, line + 1);
            }
        }

        private static void InsertLines(IList<string> lines, int index)
        {
            lines.Insert(index, "\t\t\tconfig.Formatters.Remove(config.Formatters.XmlFormatter);");
            lines.Insert(index, "\t\t\tconfig.Formatters.Add(new RAML.Api.Core.XmlSerializerFormatter());");
        }

        private static int FindLineWith(IReadOnlyList<string> lines, string find)
        {
            var line = -1;
            for (var i = 0; i < lines.Count; i++)
            {
                if (lines[i].Contains(find))
                    line = i;
            }
            return line;
        }
    }
}