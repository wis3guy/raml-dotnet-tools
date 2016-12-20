using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using EnvDTE;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using MuleSoft.RAML.Tools.Properties;
using NuGet.VisualStudio;
using Raml.Common;
using Raml.Tools.ClientGenerator;

namespace MuleSoft.RAML.Tools
{
    public class RamlReferenceService
    {
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger logger;
        private readonly string nugetPackagesSource = Settings.Default.NugetPackagesSource;
        private readonly string newtonsoftJsonPackageId = Settings.Default.NewtonsoftJsonPackageId;
        private readonly string newtonsoftJsonPackageVersion = Settings.Default.NewtonsoftJsonPackageVersion;
        private readonly string webApiCorePackageId = Settings.Default.WebApiCorePackageId;
        private readonly string webApiCorePackageVersion = Settings.Default.WebApiCorePackageVersion;
        private readonly string ramlApiCorePackageId = Settings.Default.RAMLApiCorePackageId;
        private readonly string ramlApiCorePackageVersion = Settings.Default.RAMLApiCorePackageVersion;
        public readonly static string ApiReferencesFolderName = Settings.Default.ApiReferencesFolderName;
        private readonly string microsoftNetHttpPackageId = Settings.Default.MicrosoftNetHttpPackageId;
        private readonly string microsoftNetHttpPackageVersion = Settings.Default.MicrosoftNetHttpPackageVersion;

        private readonly string clientT4TemplateName = Settings.Default.ClientT4TemplateName;
        private readonly TemplatesManager templatesManager = new TemplatesManager();

        public RamlReferenceService(IServiceProvider serviceProvider, ILogger logger)
        {
            this.serviceProvider = serviceProvider;
            this.logger = logger;
        }

        public void AddRamlReference(RamlChooserActionParams parameters)
        {
            try
            {
                logger.LogInformation("Add RAML Reference process started");
                var dte = serviceProvider.GetService(typeof (SDTE)) as DTE;
                var proj = VisualStudioAutomationHelper.GetActiveProject(dte);

                if (VisualStudioAutomationHelper.IsAVisualStudio2015Project(proj))
                    AddPortableImports(proj);

                InstallNugetDependencies(proj);
                logger.LogInformation("Nuget Dependencies installed");

                AddFilesToProject(parameters.RamlFilePath, proj, parameters.TargetNamespace, parameters.RamlSource, parameters.TargetFileName, parameters.ClientRootClassName);
                logger.LogInformation("Files added to project");
            }
            catch (Exception ex)
            {
                logger.LogError(ex);
                MessageBox.Show("Error when trying to add the RAML reference. " + ex.Message);
                throw;
            }
        }

        private void AddPortableImports(Project proj)
        {
            var projectFile = GetProjectFilePath(proj);
            var lines = File.ReadAllLines(projectFile).ToList();
            if (!lines.Any(l => l.Contains("\"portable-net45+win8\"")))
            {
                if (lines.Any(l => l.Contains("\"imports\": \"dnxcore50\"")))
                {
                    var index = TextFileHelper.FindLineWith(lines, "\"imports\": \"dnxcore50\"");
                    lines.RemoveAt(index);
                    lines.Insert(index, "\"imports\": [\"dnxcore50\", \"portable-net45+win8\"]");
                }
                else if (lines.Any(l => l.Contains("\"dnxcore50\"")))
                {
                    var index = TextFileHelper.FindLineWith(lines, "\"dnxcore50\"");
                    lines[index] = lines[index].Replace("\"dnxcore50\"", "\"dnxcore50\", \"portable-net45+win8\"");
                }
                // dotnet5.6
                else if (lines.Any(l => l.Contains("\"imports\": \"dotnet5.6\"")))
                {
                    var index = TextFileHelper.FindLineWith(lines, "\"imports\": \"dotnet5.6\"");
                    lines.RemoveAt(index);
                    lines.Insert(index, "\"imports\": [\"dotnet5.6\", \"portable-net45+win8\"]");
                }
                else if (lines.Any(l => l.Contains("\"dotnet5.6\"")))
                {
                    var index = TextFileHelper.FindLineWith(lines, "\"dotnet5.6\"");
                    lines[index] = lines[index].Replace("\"dotnet5.6\"", "\"dotnet5.6\", \"portable-net45+win8\"");
                }

                File.WriteAllText(projectFile, string.Join(Environment.NewLine, lines));
            }
        }

        private static string GetProjectFilePath(Project proj)
        {
            var path = Path.GetDirectoryName(proj.FileName);
            var projectFile = Path.Combine(path, "project.json");
            return projectFile;
        }


        private void InstallNugetDependencies(Project proj)
        {
            var componentModel = (IComponentModel)serviceProvider.GetService(typeof(SComponentModel));
            var installerServices = componentModel.GetService<IVsPackageInstallerServices>();
            var installer = componentModel.GetService<IVsPackageInstaller>();

            var packs = installerServices.GetInstalledPackages(proj).ToArray();
            NugetInstallerHelper.InstallPackageIfNeeded(proj, packs, installer, microsoftNetHttpPackageId, microsoftNetHttpPackageVersion, Settings.Default.NugetExternalPackagesSource);

            if (VisualStudioAutomationHelper.IsAVisualStudio2015Project(proj))
            {
                NugetInstallerHelper.InstallPackageIfNeeded(proj, packs, installer, newtonsoftJsonPackageId, "9.0.1", Settings.Default.NugetExternalPackagesSource);
                NugetInstallerHelper.InstallPackageIfNeeded(proj, packs, installer, "Microsoft.AspNet.WebApi.Client", "5.2.3", Settings.Default.NugetExternalPackagesSource);
                NugetInstallerHelper.InstallPackageIfNeeded(proj, packs, installer, "System.Xml.XmlSerializer", "4.3.0", Settings.Default.NugetExternalPackagesSource);
                NugetInstallerHelper.InstallPackageIfNeeded(proj, packs, installer, "System.Runtime.Serialization.Xml", "4.3.0", Settings.Default.NugetExternalPackagesSource);
            }
            else
            {
                NugetInstallerHelper.InstallPackageIfNeeded(proj, packs, installer, newtonsoftJsonPackageId, newtonsoftJsonPackageVersion, Settings.Default.NugetExternalPackagesSource);
                NugetInstallerHelper.InstallPackageIfNeeded(proj, packs, installer, webApiCorePackageId, webApiCorePackageVersion, Settings.Default.NugetExternalPackagesSource);
            }

            // RAML.Api.Core
            if (!installerServices.IsPackageInstalled(proj, ramlApiCorePackageId))
            {
                installer.InstallPackage(nugetPackagesSource, proj, ramlApiCorePackageId, ramlApiCorePackageVersion, false);
            }
        }

        private void InstallPackageIfNeeded(Project proj, string packageId, string packageVersion)
        {
            var projectFile = GetProjectFilePath(proj);
            var lines = File.ReadAllLines(projectFile).ToList();
            var packageString = string.Format("\"{0}\": \"{1}\",", packageId, packageVersion);
            if (!lines.Any(l => l.Contains(packageId)))
            {
                var index = TextFileHelper.FindLineWith(lines, "dependencies");
                lines.Insert(index + 1, packageString);
            }
            else
            {
                var index = TextFileHelper.FindLineWith(lines, packageId);
                if (IsVersionLesser(lines[index], packageId, packageVersion))
                {
                    lines.RemoveAt(index);
                    lines.Insert(index, packageString);
                }
            }
        }

        private bool IsVersionLesser(string line, string packageId, string packageVersion)
        {
            var version = line.Replace(packageId, string.Empty);
            version = version.Replace(":", string.Empty);
            version = version.Replace("\"", string.Empty);

            var installedVersionNumbers = version.Split(new []{"."}, StringSplitOptions.RemoveEmptyEntries);
            var toInstallVersionNumbers = packageVersion.Split(new[] { "." }, StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < installedVersionNumbers.Length; index++)
            {
                var installedNumber = int.Parse(installedVersionNumbers[index]);
                var toInstallNumber = int.Parse(toInstallVersionNumbers[index]);
                if (installedNumber > toInstallNumber)
                    return false;
            }
            return true;
        }

        private void AddFilesToProject(string ramlSourceFile, Project proj, string targetNamespace, string ramlOriginalSource, string targetFileName, string clientRootClassName)
        {
            if(!File.Exists(ramlSourceFile))
                throw new FileNotFoundException("RAML file not found " + ramlSourceFile);

            if(Path.GetInvalidFileNameChars().Any(targetFileName.Contains))
                throw new ArgumentException("Specified filename has invalid chars: " + targetFileName);

            var destFolderName = Path.GetFileNameWithoutExtension(targetFileName);
            var apiRefsFolderPath = Path.GetDirectoryName(proj.FullName) + Path.DirectorySeparatorChar + ApiReferencesFolderName + Path.DirectorySeparatorChar;
            var destFolderPath = apiRefsFolderPath + destFolderName + Path.DirectorySeparatorChar;
            var apiRefsFolderItem = VisualStudioAutomationHelper.AddFolderIfNotExists(proj, ApiReferencesFolderName);

            var destFolderItem = VisualStudioAutomationHelper.AddFolderIfNotExists(apiRefsFolderItem, destFolderName, destFolderPath);

            var includesManager = new RamlIncludesManager();
            var result = includesManager.Manage(ramlOriginalSource, destFolderPath, ramlSourceFile);

            var ramlDestFile = Path.Combine(destFolderPath, targetFileName);
            if (File.Exists(ramlDestFile))
                new FileInfo(ramlDestFile).IsReadOnly = false;
            File.WriteAllText(ramlDestFile, result.ModifiedContents);

            var ramlProjItem = InstallerServices.AddOrUpdateRamlFile(ramlDestFile, destFolderPath, destFolderItem, targetFileName);
            var props = new RamlProperties
            {
                ClientName = clientRootClassName,
                Source = ramlOriginalSource,
                Namespace = targetNamespace
            };
            var refFilePath = InstallerServices.AddRefFile(ramlSourceFile, destFolderPath, targetFileName, props);
            ramlProjItem.ProjectItems.AddFromFile(refFilePath);

            if (VisualStudioAutomationHelper.IsAVisualStudio2015Project(proj))
            {
                templatesManager.CopyClientTemplateToProjectFolder(apiRefsFolderPath);

                var ramlInfo = RamlInfoService.GetRamlInfo(ramlDestFile);
                if (ramlInfo.HasErrors)
                {
                    ActivityLog.LogError(VisualStudioAutomationHelper.RamlVsToolsActivityLogSource, ramlInfo.ErrorMessage);
                    MessageBox.Show(ramlInfo.ErrorMessage);
                    return;
                }

                var model = new ClientGeneratorService(ramlInfo.RamlDocument, clientRootClassName, targetNamespace).BuildModel();
                var directoryName = Path.GetDirectoryName(ramlDestFile).TrimEnd(Path.DirectorySeparatorChar);
                var templateFolder = directoryName.Substring(0, directoryName.LastIndexOf(Path.DirectorySeparatorChar)) + Path.DirectorySeparatorChar + "Templates";

                var templateFilePath = Path.Combine(templateFolder, clientT4TemplateName);
                var extensionPath = Path.GetDirectoryName(GetType().Assembly.Location) + Path.DirectorySeparatorChar;
                var t4Service = new T4Service(serviceProvider);
                var res = t4Service.TransformText(templateFilePath, model, extensionPath, ramlDestFile, targetNamespace);

                if (res.HasErrors)
                {
                    ActivityLog.LogError(VisualStudioAutomationHelper.RamlVsToolsActivityLogSource, res.Errors);
                    MessageBox.Show(res.Errors);
                    return;
                }

                var content = templatesManager.AddClientMetadataHeader(res.Content);
                var csTargetFile = Path.Combine(destFolderPath, destFolderName + ".cs");
                File.WriteAllText(csTargetFile, content);
            }
            else
            {
                ramlProjItem.Properties.Item("CustomTool").Value = string.Empty; // to cause a refresh when file already exists
                ramlProjItem.Properties.Item("CustomTool").Value = "RamlClientTool";
            }
        }

    }
}