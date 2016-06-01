using EnvDTE;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using MuleSoft.RAML.Tools.Properties;
using NuGet.VisualStudio;
using Raml.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MuleSoft.RAML.Tools
{
    public class ReverseEngineeringService
    {
        private readonly string nugetPackagesSource = Settings.Default.NugetPackagesSource;
        private readonly string ramlWebApiExplorerPackageId = Settings.Default.RAMLWebApiExplorerPackageId;
        private readonly string ramlWebApiExplorerPackageVersion = Settings.Default.RAMLWebApiExplorerPackageVersion;
        private readonly string ramlParserPackageId = Settings.Default.RAMLParserPackageId;
        private readonly string ramlParserPackageVersion = Settings.Default.RAMLParserPackageVersion;
        private readonly string ramlApiCorePackageId = Settings.Default.RAMLApiCorePackageId;
        private readonly string ramlApiCorePackageVersion = Settings.Default.RAMLApiCorePackageVersion;
        private readonly string newtonsoftJsonPackageId = Settings.Default.NewtonsoftJsonPackageId;
        private readonly string newtonsoftJsonPackageVersion = Settings.Default.NewtonsoftJsonPackageVersion;
        private readonly string edgePackageId = Settings.Default.EdgePackageId;
        private readonly string edgePackageVersion = Settings.Default.EdgePackageVersion;

        private readonly IServiceProvider serviceProvider;
        

        public ReverseEngineeringService(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }


        public void AddReverseEngineering()
        {
            try
            {
                ActivityLog.LogInformation(VisualStudioAutomationHelper.RamlVsToolsActivityLogSource, "Enable RAML metadata output process started");
                var dte = serviceProvider.GetService(typeof(SDTE)) as DTE;
                var proj = VisualStudioAutomationHelper.GetActiveProject(dte);

                InstallNugetAndDependencies(proj);
                ActivityLog.LogInformation(VisualStudioAutomationHelper.RamlVsToolsActivityLogSource, "Nuget package and dependencies installed");

                AddXmlCommentsDocumentation(proj);
                ActivityLog.LogInformation(VisualStudioAutomationHelper.RamlVsToolsActivityLogSource, "XML comments documentation added");
            }
            catch (Exception ex)
            {
                ActivityLog.LogError(VisualStudioAutomationHelper.RamlVsToolsActivityLogSource,
                    VisualStudioAutomationHelper.GetExceptionInfo(ex));
                MessageBox.Show("Error when trying to enable RAML metadata output. " + ex.Message);
                throw;
            }
        }

        private void AddXmlCommentsDocumentation(Project proj)
        {
            ConfigureXmlDocumentationFileInProject(proj);
            AddIncludeXmlCommentsInWebApiConfig(proj);
        }

        private static void AddIncludeXmlCommentsInWebApiConfig(Project proj)
        {
            var appStart = proj.ProjectItems.Cast<ProjectItem>().FirstOrDefault(i => i.Name == "App_Start");
            if (appStart == null) return;

            var webApiConfig = appStart.ProjectItems.Cast<ProjectItem>().FirstOrDefault(i => i.Name == "WebApiConfig.cs");
            if (webApiConfig == null) return;

            var path = webApiConfig.FileNames[0];
            var lines = File.ReadAllLines(path).ToList();

            if (lines.Any(l => l.Contains("DocumentationProviderConfig.IncludeXmlComments")))
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
                    lines.Insert(line + 2, "\t\t\tRAML.WebApiExplorer.DocumentationProviderConfig.IncludeXmlComments();");
                    inserted = true;
                }
            }

            if (!inserted)
            {
                line = FindLineWith(lines, ".MapHttpAttributeRoutes();");
                if (line != -1)
                    lines.Insert(line + 1, "\t\t\tRAML.WebApiExplorer.DocumentationProviderConfig.IncludeXmlComments();");
            }
        }

        private static int FindLineWith(IReadOnlyList<string> lines, string find)
        {
            var line = -1;
            for (var i = 0; i < lines.Count(); i++)
            {
                if (lines[i].Contains(find))
                    line = i;
            }
            return line;
        }

        private static void ConfigureXmlDocumentationFileInProject(Project proj)
        {
            var config = proj.ConfigurationManager.ActiveConfiguration;
            var configProps = config.Properties;
            var prop = configProps.Item("DocumentationFile");
            prop.Value = string.Format("bin\\{0}.XML", proj.Name);
        }

        private void InstallNugetAndDependencies(Project proj)
        {
            var componentModel = (IComponentModel)serviceProvider.GetService(typeof(SComponentModel));
            var installerServices = componentModel.GetService<IVsPackageInstallerServices>();
            var installer = componentModel.GetService<IVsPackageInstaller>();

            var packs = installerServices.GetInstalledPackages(proj).ToArray();
            NugetInstallerHelper.InstallPackageIfNeeded(proj, packs, installer, newtonsoftJsonPackageId, newtonsoftJsonPackageVersion, Settings.Default.NugetExternalPackagesSource);
            NugetInstallerHelper.InstallPackageIfNeeded(proj, packs, installer, edgePackageId, edgePackageVersion, Settings.Default.NugetExternalPackagesSource);
            NugetInstallerHelper.InstallPackageIfNeeded(proj, packs, installer, "System.ComponentModel.Annotations", "4.0.0", Settings.Default.NugetExternalPackagesSource);

            // RAML.Parser
            if (!installerServices.IsPackageInstalled(proj, ramlParserPackageId))
            {
                installer.InstallPackage(nugetPackagesSource, proj, ramlParserPackageId, ramlParserPackageVersion, false);
            }

            // RAML.Api.Core
            if (!installerServices.IsPackageInstalled(proj, ramlApiCorePackageId))
            {
                //installer.InstallPackage(nugetPackagesSource, proj, ramlApiCorePackageId, ramlApiCorePackageVersion, false);
                installer.InstallPackage(nugetPackagesSource, proj, ramlApiCorePackageId, ramlApiCorePackageVersion, false);
            }

            // RAML.WebApiExplorer
            if (!installerServices.IsPackageInstalled(proj, ramlWebApiExplorerPackageId))
            {
                installer.InstallPackage(nugetPackagesSource, proj, ramlWebApiExplorerPackageId, ramlWebApiExplorerPackageVersion, false);
            }
        }

        public void ExtractRAML()
        {
            throw new NotImplementedException();
        }

        public void RemoveReverseEngineering()
        {
            try
            {
                ActivityLog.LogInformation(VisualStudioAutomationHelper.RamlVsToolsActivityLogSource, "Disable RAML metadata output process started");
                var dte = serviceProvider.GetService(typeof(SDTE)) as DTE;
                var proj = VisualStudioAutomationHelper.GetActiveProject(dte);

                UninstallNugetAndDependencies(proj);
                ActivityLog.LogInformation(VisualStudioAutomationHelper.RamlVsToolsActivityLogSource, "Nuget package uninstalled");

                RemovXmlCommentsDocumentation(proj);
                ActivityLog.LogInformation(VisualStudioAutomationHelper.RamlVsToolsActivityLogSource, "XML comments documentation removed");
            }
            catch (Exception ex)
            {
                ActivityLog.LogError(VisualStudioAutomationHelper.RamlVsToolsActivityLogSource,
                    VisualStudioAutomationHelper.GetExceptionInfo(ex));
                MessageBox.Show("Error when trying to disable RAML metadata output. " + ex.Message);
                throw;
            }
        }

        private void UninstallNugetAndDependencies(Project proj)
        {
            var componentModel = (IComponentModel)serviceProvider.GetService(typeof(SComponentModel));
            var installerServices = componentModel.GetService<IVsPackageInstallerServices>();
            var installer = componentModel.GetService<IVsPackageUninstaller>();

            // Uninstall RAML.WebApiExplorer
            if (installerServices.IsPackageInstalled(proj, ramlWebApiExplorerPackageId))
            {
                installer.UninstallPackage(proj, ramlWebApiExplorerPackageId, false);
            }
        }

        private void RemovXmlCommentsDocumentation(Project proj)
        {
            RemoveXmlCommentsInWebApiConfig(proj);
            RemoveXmlDocumentationFileInProject(proj);
        }

        private static void RemoveXmlCommentsInWebApiConfig(Project proj)
        {
            var appStart = proj.ProjectItems.Cast<ProjectItem>().FirstOrDefault(i => i.Name == "App_Start");
            if (appStart == null) return;

            var webApiConfig = appStart.ProjectItems.Cast<ProjectItem>().FirstOrDefault(i => i.Name == "WebApiConfig.cs");
            if (webApiConfig == null) return;

            var path = webApiConfig.FileNames[0];
            var content = File.ReadAllText(path);

            if (!content.Contains("DocumentationProviderConfig.IncludeXmlComments"))
                return;

            content = content.Replace("RAML.WebApiExplorer.DocumentationProviderConfig.IncludeXmlComments();", string.Empty);

            File.WriteAllText(path, content);
        }

        private static void RemoveXmlDocumentationFileInProject(Project proj)
        {
            var config = proj.ConfigurationManager.ActiveConfiguration;
            var configProps = config.Properties;
            var prop = configProps.Item("DocumentationFile");
            prop.Value = string.Empty;
        }

    }
}