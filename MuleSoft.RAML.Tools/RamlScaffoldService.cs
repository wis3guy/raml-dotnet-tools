using EnvDTE;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using MuleSoft.RAML.Tools.Properties;
using NuGet.VisualStudio;
using Raml.Common;
using Raml.Tools;
using Raml.Tools.WebApiGenerator;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace MuleSoft.RAML.Tools
{
    public class RamlScaffoldService
    {
        private const string RamlSpecVersion = "0.8";
        private const string ControllerBaseTemplateName = "ApiControllerBase.t4";
        private const string ControllerInterfaceTemplateName = "ApiControllerInterface.t4";
        private const string ControllerImplementationTemplateName = "ApiControllerImplementation.t4";
        private const string ModelTemplateName = "ApiModel.t4";
        private const string EnumTemplateName = "ApiEnum.t4";

        private readonly string ContractsFolderName = Settings.Default.ContractsFolderName;
        private readonly IT4Service t4Service;
        private readonly IServiceProvider serviceProvider;
        private readonly TemplatesManager templatesManager = new TemplatesManager();
        private static readonly string ContractsFolder = Path.DirectorySeparatorChar + Settings.Default.ContractsFolderName + Path.DirectorySeparatorChar;
        private static readonly string IncludesFolder = Path.DirectorySeparatorChar + "includes" + Path.DirectorySeparatorChar;

        private readonly string nugetPackagesSource = Settings.Default.NugetPackagesSource;
        private readonly string ramlApiCorePackageId = Settings.Default.RAMLApiCorePackageId;
        private readonly string ramlApiCorePackageVersion = Settings.Default.RAMLApiCorePackageVersion;
        private readonly string newtonsoftJsonPackageId = Settings.Default.NewtonsoftJsonPackageId;
        private readonly string newtonsoftJsonPackageVersion = Settings.Default.NewtonsoftJsonPackageVersion;
        private readonly string microsoftNetHttpPackageId = Settings.Default.MicrosoftNetHttpPackageId;
        private readonly string microsoftNetHttpPackageVersion = Settings.Default.MicrosoftNetHttpPackageVersion;
        private string templateSubFolder;

        public RamlScaffoldService(IT4Service t4Service, IServiceProvider serviceProvider)
        {
            this.t4Service = t4Service;
            this.serviceProvider = serviceProvider;
        }

        public void AddContract(RamlChooserActionParams parameters)
        {
            var dte = serviceProvider.GetService(typeof(SDTE)) as DTE;
            var proj = VisualStudioAutomationHelper.GetActiveProject(dte);

            InstallNugetDependencies(proj);
            AddXmlFormatterInWebApiConfig(proj);

            var folderItem = VisualStudioAutomationHelper.AddFolderIfNotExists(proj, ContractsFolderName);
            var contractsFolderPath = Path.GetDirectoryName(proj.FullName) + Path.DirectorySeparatorChar + ContractsFolderName + Path.DirectorySeparatorChar;

            var targetFolderPath = GetTargetFolderPath(contractsFolderPath, parameters.TargetFileName, proj);
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


        public void Scaffold(string ramlSource, RamlChooserActionParams parameters)
        {
            var data = RamlScaffolderHelper.GetRamlData(ramlSource, parameters.TargetNamespace);
            if (data == null || data.Model == null)
                return;

            var model = data.Model;

            var dte = serviceProvider.GetService(typeof(SDTE)) as DTE;
            var proj = VisualStudioAutomationHelper.GetActiveProject(dte);

            var contractsFolderItem = VisualStudioAutomationHelper.AddFolderIfNotExists(proj, ContractsFolderName);
            var ramlItem =
                contractsFolderItem.ProjectItems.Cast<ProjectItem>()
                    .First(i => i.Name.ToLowerInvariant() == parameters.TargetFileName.ToLowerInvariant());
            var contractsFolderPath = Path.GetDirectoryName(proj.FullName) + Path.DirectorySeparatorChar +
                                      ContractsFolderName + Path.DirectorySeparatorChar;

            if (VisualStudioAutomationHelper.IsAVisualStudio2015Project(proj))
                templateSubFolder = "AspNet5";
            else
                templateSubFolder = "RAMLWebApi2Scaffolder";

            var templates = new[]
            {
                ControllerBaseTemplateName, 
                ControllerInterfaceTemplateName, 
                ControllerImplementationTemplateName,
                ModelTemplateName, 
                EnumTemplateName
            };
            if (!templatesManager.ConfirmWhenIncompatibleServerTemplate(contractsFolderPath, templates))
                return;

            var extensionPath = Path.GetDirectoryName(GetType().Assembly.Location) + Path.DirectorySeparatorChar;

            AddOrUpdateModels(parameters, contractsFolderPath, ramlItem, model, contractsFolderItem, extensionPath);

            AddOrUpdateEnums(parameters, contractsFolderPath, ramlItem, model, contractsFolderItem, extensionPath);

            AddOrUpdateControllerBase(parameters, contractsFolderPath, ramlItem, model, contractsFolderItem, extensionPath);

            AddOrUpdateControllerInterfaces(parameters, contractsFolderPath, ramlItem, model, contractsFolderItem, extensionPath);

            AddOrUpdateControllerImplementations(parameters, contractsFolderPath, proj, model, contractsFolderItem, extensionPath);
        }

        public static void TriggerScaffoldOnRamlChanged(Document document)
        {
            if (!IsInContractsFolder(document)) 
                return;

            ScaffoldMainRamlFiles(GetMainRamlFiles(document));
        }

        private void InstallNugetDependencies(Project proj)
        {
            // RAML.Api.Core
            var componentModel = (IComponentModel)serviceProvider.GetService(typeof(SComponentModel));
            var installerServices = componentModel.GetService<IVsPackageInstallerServices>();
            var installer = componentModel.GetService<IVsPackageInstaller>();

            var packs = installerServices.GetInstalledPackages(proj).ToArray();
            NugetInstallerHelper.InstallPackageIfNeeded(proj, packs, installer, newtonsoftJsonPackageId, newtonsoftJsonPackageVersion, Settings.Default.NugetExternalPackagesSource);
            NugetInstallerHelper.InstallPackageIfNeeded(proj, packs, installer, microsoftNetHttpPackageId, microsoftNetHttpPackageVersion, Settings.Default.NugetExternalPackagesSource);

            // System.Xml.XmlSerializer 4.0.11-beta-23516
            // NugetInstallerHelper.InstallPackageIfNeeded(proj, packs, installer, "System.Xml.XmlSerializer", "4.0.11-beta-23516");

            // RAML.Api.Core
            if (!installerServices.IsPackageInstalled(proj, ramlApiCorePackageId))
            {
                installer.InstallPackage(nugetPackagesSource, proj, ramlApiCorePackageId, ramlApiCorePackageVersion, false);
            }
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
            for (var i = 0; i < lines.Count(); i++)
            {
                if (lines[i].Contains(find))
                    line = i;
            }
            return line;
        }

        private static void ScaffoldMainRamlFiles(IEnumerable<string> ramlFiles)
        {
            var globalProvider = ServiceProvider.GlobalProvider;
            var service = new RamlScaffoldService(new T4Service(globalProvider), ServiceProvider.GlobalProvider);
            foreach (var ramlFile in ramlFiles)
            {
                var refFilePath = InstallerServices.GetRefFilePath(ramlFile);
                var includeApiVersionInRoutePrefix = RamlReferenceReader.GetRamlIncludeApiVersionInRoutePrefix(refFilePath);
                var parameters = new RamlChooserActionParams(ramlFile, ramlFile, null, null, Path.GetFileName(ramlFile),
                    RamlReferenceReader.GetRamlNamespace(refFilePath), null)
                {
                    UseAsyncMethods = RamlReferenceReader.GetRamlUseAsyncMethods(refFilePath),
                    IncludeApiVersionInRoutePrefix = includeApiVersionInRoutePrefix,
                    ModelsFolder = RamlReferenceReader.GetModelsFolder(refFilePath),
                    ImplementationControllersFolder = RamlReferenceReader.GetImplementationControllersFolder(refFilePath),
                    BaseControllersFolder = RamlReferenceReader.GetBaseControllersFolder(refFilePath)
                };
                service.Scaffold(ramlFile, parameters);
            }
        }

        private static IEnumerable<string> GetMainRamlFiles(Document document)
        {
            var path = document.Path.ToLowerInvariant();

            if (IsMainRamlFile(document, path))
                return new [] {document.FullName};

            var ramlItems = GetMainRamlFileFromProject();
            return GetItemsWithReferenceFiles(ramlItems);
        }

        private static bool IsMainRamlFile(Document document, string path)
        {
            return !path.EndsWith(IncludesFolder) && document.Name.ToLowerInvariant().EndsWith(".raml") && HasReferenceFile(document.FullName);
        }

        private static IEnumerable<string> GetItemsWithReferenceFiles(IEnumerable<ProjectItem> ramlItems)
        {
            var items = new List<string>();
            foreach (var item in ramlItems)
            {
                if (HasReferenceFile(item.FileNames[0]))
                    items.Add(item.FileNames[0]);
            }
            return items;
        }

        private static bool HasReferenceFile(string ramlFilePath)
        {
            var refFilePath = InstallerServices.GetRefFilePath(ramlFilePath);
            var hasReferenceFile = !string.IsNullOrWhiteSpace(refFilePath) && File.Exists(refFilePath);
            return hasReferenceFile;
        }

        private static IEnumerable<ProjectItem> GetMainRamlFileFromProject()
        {
            var dte = ServiceProvider.GlobalProvider.GetService(typeof(SDTE)) as DTE;
            var proj = VisualStudioAutomationHelper.GetActiveProject(dte);
            var contractsItem =
                proj.ProjectItems.Cast<ProjectItem>().FirstOrDefault(i => i.Name == Settings.Default.ContractsFolderName);

            if (contractsItem == null)
                throw new InvalidOperationException("Could not find main RAML file");

            var ramlItems = contractsItem.ProjectItems.Cast<ProjectItem>().Where(i => i.Name.EndsWith(".raml")).ToArray();
            if (!ramlItems.Any())
                throw new InvalidOperationException("Could not find main RAML file");

            return ramlItems;
        }

        private static bool IsInContractsFolder(Document document)
        {
            return document.Path.ToLowerInvariant().Contains(ContractsFolder.ToLowerInvariant());
        }

        private void AddOrUpdateControllerImplementations(RamlChooserActionParams parameters, string contractsFolderPath, Project proj,
            WebApiGeneratorModel model, ProjectItem folderItem, string extensionPath)
        {
            templatesManager.CopyServerTemplateToProjectFolder(contractsFolderPath, ControllerImplementationTemplateName,
                Settings.Default.ControllerImplementationTemplateTitle, templateSubFolder);
            var controllersFolderItem = VisualStudioAutomationHelper.AddFolderIfNotExists(proj, "Controllers");
            
            var templatesFolder = Path.Combine(contractsFolderPath, "Templates");
            var controllerImplementationTemplateParams =
                new TemplateParams<ControllerObject>(
                    Path.Combine(templatesFolder, ControllerImplementationTemplateName),
                    controllersFolderItem, "controllerObject", model.Controllers, contractsFolderPath, folderItem,
                    extensionPath, parameters.TargetNamespace, "Controller", false,
                    GetVersionPrefix(parameters.IncludeApiVersionInRoutePrefix, model.ApiVersion))
                {
                    TargetFolder = TargetFolderResolver.GetImplementationControllersFolderPath(proj, parameters.ImplementationControllersFolder)
                };

            controllerImplementationTemplateParams.Title = Settings.Default.ControllerImplementationTemplateTitle;
            controllerImplementationTemplateParams.IncludeHasModels = true;
            controllerImplementationTemplateParams.HasModels = model.Objects.Any(o => o.IsScalar == false) || model.Enums.Any();
            controllerImplementationTemplateParams.UseAsyncMethods = parameters.UseAsyncMethods;
            controllerImplementationTemplateParams.IncludeApiVersionInRoutePrefix = parameters.IncludeApiVersionInRoutePrefix;
            controllerImplementationTemplateParams.ApiVersion = model.ApiVersion;
            GenerateCodeFromTemplate(controllerImplementationTemplateParams);
        }

        private static string GetVersionPrefix(bool includeApiVersionInRoutePrefix, string apiVersion)
        {
            return includeApiVersionInRoutePrefix ? NetNamingMapper.GetVersionName(apiVersion) : string.Empty;
        }

        private void AddOrUpdateControllerInterfaces(RamlChooserActionParams parameters, string contractsFolderPath, ProjectItem ramlItem,
            WebApiGeneratorModel model, ProjectItem folderItem, string extensionPath)
        {
            templatesManager.CopyServerTemplateToProjectFolder(contractsFolderPath, ControllerInterfaceTemplateName,
                Settings.Default.ControllerInterfaceTemplateTitle, templateSubFolder);
            var templatesFolder = Path.Combine(contractsFolderPath, "Templates");

            var targetFolderPath = GetTargetFolderPath(contractsFolderPath, ramlItem.FileNames[0], folderItem.ContainingProject);

            var controllerInterfaceParams =
                new TemplateParams<ControllerObject>(Path.Combine(templatesFolder, ControllerInterfaceTemplateName),
                    ramlItem, "controllerObject", model.Controllers, targetFolderPath, folderItem, extensionPath,
                    parameters.TargetNamespace, "Controller", true, "I" + GetVersionPrefix(parameters.IncludeApiVersionInRoutePrefix, model.ApiVersion));
            controllerInterfaceParams.Title = Settings.Default.ControllerInterfaceTemplateTitle;
            controllerInterfaceParams.IncludeHasModels = true;
            controllerInterfaceParams.HasModels = model.Objects.Any(o => o.IsScalar == false) || model.Enums.Any();
            controllerInterfaceParams.UseAsyncMethods = parameters.UseAsyncMethods;
            controllerInterfaceParams.IncludeApiVersionInRoutePrefix = parameters.IncludeApiVersionInRoutePrefix;
            controllerInterfaceParams.ApiVersion = model.ApiVersion;
            controllerInterfaceParams.TargetFolder =
                TargetFolderResolver.GetBaseAndInterfacesControllersTargetFolder(ramlItem.ContainingProject,
                    targetFolderPath, parameters.BaseControllersFolder);
            GenerateCodeFromTemplate(controllerInterfaceParams);
        }

        private void AddOrUpdateControllerBase(RamlChooserActionParams parameters, string contractsFolderPath, ProjectItem ramlItem,
            WebApiGeneratorModel model, ProjectItem folderItem, string extensionPath)
        {
            templatesManager.CopyServerTemplateToProjectFolder(contractsFolderPath, ControllerBaseTemplateName,
                Settings.Default.BaseControllerTemplateTitle, templateSubFolder);
            var templatesFolder = Path.Combine(contractsFolderPath, "Templates");

            var targetFolderPath = GetTargetFolderPath(contractsFolderPath, ramlItem.FileNames[0], folderItem.ContainingProject);

            var controllerBaseTemplateParams =
                new TemplateParams<ControllerObject>(Path.Combine(templatesFolder, ControllerBaseTemplateName),
                    ramlItem, "controllerObject", model.Controllers, targetFolderPath, folderItem, extensionPath,
                    parameters.TargetNamespace, "Controller", true, GetVersionPrefix(parameters.IncludeApiVersionInRoutePrefix, model.ApiVersion));
            controllerBaseTemplateParams.Title = Settings.Default.BaseControllerTemplateTitle;
            controllerBaseTemplateParams.IncludeHasModels = true;
            controllerBaseTemplateParams.HasModels = model.Objects.Any(o => o.IsScalar == false) || model.Enums.Any();
            controllerBaseTemplateParams.UseAsyncMethods = parameters.UseAsyncMethods;
            controllerBaseTemplateParams.IncludeApiVersionInRoutePrefix = parameters.IncludeApiVersionInRoutePrefix;
            controllerBaseTemplateParams.ApiVersion = model.ApiVersion;
            controllerBaseTemplateParams.TargetFolder =
                TargetFolderResolver.GetBaseAndInterfacesControllersTargetFolder(ramlItem.ContainingProject,
                    targetFolderPath, parameters.BaseControllersFolder);

            GenerateCodeFromTemplate(controllerBaseTemplateParams);
        }

        private void AddOrUpdateModels(RamlChooserActionParams parameters, string contractsFolderPath, ProjectItem ramlItem, WebApiGeneratorModel model, ProjectItem contractsFolderItem, string extensionPath)
        {
            templatesManager.CopyServerTemplateToProjectFolder(contractsFolderPath, ModelTemplateName,
                Settings.Default.ModelsTemplateTitle, templateSubFolder);
            var templatesFolder = Path.Combine(contractsFolderPath, "Templates");
            
            var models = model.Objects;
            // when is an XML model, skip empty objects
            if (model.Objects.Any(o => !string.IsNullOrWhiteSpace(o.GeneratedCode)))
                models = model.Objects.Where(o => o.Properties.Any() || !string.IsNullOrWhiteSpace(o.GeneratedCode));

            models = models.Where(o => !o.IsArray || o.Type == null); // skip array of primitives
            models = models.Where(o => !o.IsScalar); // skip scalar types

            var targetFolderPath = GetTargetFolderPath(contractsFolderPath, ramlItem.FileNames[0], contractsFolderItem.ContainingProject);

            var apiObjectTemplateParams = new TemplateParams<ApiObject>(
                Path.Combine(templatesFolder, ModelTemplateName), ramlItem, "apiObject", models,
                contractsFolderPath, contractsFolderItem, extensionPath, parameters.TargetNamespace,
                GetVersionPrefix(parameters.IncludeApiVersionInRoutePrefix, model.ApiVersion));

            apiObjectTemplateParams.Title = Settings.Default.ModelsTemplateTitle;
            apiObjectTemplateParams.TargetFolder = TargetFolderResolver.GetModelsTargetFolder(ramlItem.ContainingProject,
                targetFolderPath, parameters.ModelsFolder);
            GenerateCodeFromTemplate(apiObjectTemplateParams);
        }

        private void AddOrUpdateEnums(RamlChooserActionParams parameters, string contractsFolderPath, ProjectItem ramlItem, WebApiGeneratorModel model, ProjectItem folderItem, string extensionPath)
        {
            templatesManager.CopyServerTemplateToProjectFolder(contractsFolderPath, EnumTemplateName,
                Settings.Default.EnumsTemplateTitle, templateSubFolder);
            var templatesFolder = Path.Combine(contractsFolderPath, "Templates");

            var targetFolderPath = GetTargetFolderPath(contractsFolderPath, ramlItem.FileNames[0], folderItem.ContainingProject);

            var apiEnumTemplateParams = new TemplateParams<ApiEnum>(
                Path.Combine(templatesFolder, EnumTemplateName), ramlItem, "apiEnum", model.Enums,
                targetFolderPath, folderItem, extensionPath, parameters.TargetNamespace, GetVersionPrefix(parameters.IncludeApiVersionInRoutePrefix, model.ApiVersion));
            apiEnumTemplateParams.Title = Settings.Default.ModelsTemplateTitle;
            apiEnumTemplateParams.TargetFolder = TargetFolderResolver.GetModelsTargetFolder(ramlItem.ContainingProject,
                targetFolderPath, parameters.ModelsFolder);

            GenerateCodeFromTemplate(apiEnumTemplateParams);
        }


        public void UpdateRaml(string ramlFilePath)
        {
            var dte = serviceProvider.GetService(typeof(SDTE)) as DTE;
            var proj = VisualStudioAutomationHelper.GetActiveProject(dte);
            var contractsFolderPath = Path.GetDirectoryName(proj.FullName) + Path.DirectorySeparatorChar + ContractsFolderName + Path.DirectorySeparatorChar;

            var refFilePath = InstallerServices.GetRefFilePath(ramlFilePath);
            var includesFolderPath = contractsFolderPath + Path.DirectorySeparatorChar + InstallerServices.IncludesFolderName;
            var ramlSource = RamlReferenceReader.GetRamlSource(refFilePath);
            if (string.IsNullOrWhiteSpace(ramlSource))
                ramlSource = ramlFilePath;

            var includesManager = new RamlIncludesManager();
            var result = includesManager.Manage(ramlSource, includesFolderPath, contractsFolderPath + Path.DirectorySeparatorChar);
            if (result.IsSuccess)
            {
                File.WriteAllText(ramlFilePath, result.ModifiedContents);
                var parameters = new RamlChooserActionParams(ramlFilePath, ramlFilePath, null, null,
                    Path.GetFileName(ramlFilePath).ToLowerInvariant(), 
                    RamlReferenceReader.GetRamlNamespace(refFilePath), null)
                {
                    UseAsyncMethods = RamlReferenceReader.GetRamlUseAsyncMethods(refFilePath),
                    IncludeApiVersionInRoutePrefix = RamlReferenceReader.GetRamlIncludeApiVersionInRoutePrefix(refFilePath),
                    ModelsFolder = RamlReferenceReader.GetModelsFolder(refFilePath),
                    ImplementationControllersFolder = RamlReferenceReader.GetImplementationControllersFolder(refFilePath),
                    BaseControllersFolder = RamlReferenceReader.GetBaseControllersFolder(refFilePath)
                };
                Scaffold(ramlFilePath, parameters);
            }
        }

        private void AddContractFromFile(ProjectItem folderItem, string folderPath, RamlChooserActionParams parameters)
        {
            var includesFolderPath = folderPath + Path.DirectorySeparatorChar + InstallerServices.IncludesFolderName;

            var includesManager = new RamlIncludesManager();
            var result = includesManager.Manage(parameters.RamlSource, includesFolderPath, confirmOverrite: true, rootRamlPath: folderPath + Path.DirectorySeparatorChar);

            var includesFolderItem = folderItem.ProjectItems.Cast<ProjectItem>().FirstOrDefault(i => i.Name == InstallerServices.IncludesFolderName);
            if (includesFolderItem == null && !VisualStudioAutomationHelper.IsAVisualStudio2015Project(folderItem.ContainingProject))
                includesFolderItem = folderItem.ProjectItems.AddFolder(InstallerServices.IncludesFolderName);

            foreach (var file in result.IncludedFiles)
            {
                if(!VisualStudioAutomationHelper.IsAVisualStudio2015Project(folderItem.ContainingProject) || !File.Exists(file))
                    includesFolderItem.ProjectItems.AddFromFile(file);
            }

            //var existingIncludeItems = includesFolderItem.ProjectItems.Cast<ProjectItem>();
            //var oldIncludedFiles = existingIncludeItems.Where(item => !result.IncludedFiles.Contains(item.FileNames[0]));
            //InstallerServices.RemoveSubItemsAndAssociatedFiles(oldIncludedFiles);

            var ramlProjItem = AddOrUpdateRamlFile(result.ModifiedContents, folderItem, folderPath, parameters.TargetFileName);
            InstallerServices.RemoveSubItemsAndAssociatedFiles(ramlProjItem);

            var targetFolderPath = GetTargetFolderPath(folderPath, parameters.TargetFileName, folderItem.ContainingProject);

            RamlProperties props = Map(parameters);
            var refFilePath = InstallerServices.AddRefFile(parameters.RamlFilePath, targetFolderPath, parameters.TargetFileName, props);
            ramlProjItem.ProjectItems.AddFromFile(refFilePath);

            Scaffold(ramlProjItem.FileNames[0], parameters);
        }

        private RamlProperties Map(RamlChooserActionParams parameters)
        {
            return new RamlProperties
            {
                IncludeApiVersionInRoutePrefix = parameters.IncludeApiVersionInRoutePrefix,
                UseAsyncMethods = parameters.UseAsyncMethods,
                Namespace = parameters.TargetNamespace,
                Source = parameters.RamlSource,
                ClientName = parameters.ClientRootClassName,
                ModelsFolder = parameters.ModelsFolder,
                BaseControllersFolder = parameters.BaseControllersFolder,
                ImplementationControllersFolder = parameters.ImplementationControllersFolder
            };
        }

        private static ProjectItem AddOrUpdateRamlFile(string modifiedContents, ProjectItem folderItem, string folderPath, string ramlFileName)
        {
            ProjectItem ramlProjItem;
            var ramlDestFile = Path.Combine(folderPath, ramlFileName);

            if (File.Exists(ramlDestFile))
            {
                var dialogResult = InstallerServices.ShowConfirmationDialog(ramlFileName);

                if (dialogResult == MessageBoxResult.Yes)
                {
                    File.WriteAllText(ramlDestFile, modifiedContents);
                    ramlProjItem = folderItem.ProjectItems.AddFromFile(ramlDestFile);
                }
                else
                {
                    ramlProjItem = folderItem.ProjectItems.Cast<ProjectItem>().FirstOrDefault(i => i.Name == ramlFileName);
                    if (ramlProjItem == null)
                        ramlProjItem = folderItem.ProjectItems.AddFromFile(ramlDestFile);
                }
            }
            else
            {
                File.WriteAllText(ramlDestFile, modifiedContents);
                ramlProjItem = folderItem.ProjectItems.AddFromFile(ramlDestFile);
            }
            return ramlProjItem;
        }

        private void AddEmptyContract(ProjectItem folderItem, string folderPath, RamlChooserActionParams parameters)
        {
            
            var newContractFile = Path.Combine(folderPath, parameters.TargetFileName);
            var contents = CreateNewRamlContents(parameters.RamlTitle);

            ProjectItem ramlProjItem;
            if (File.Exists(newContractFile))
            {
                var dialogResult = InstallerServices.ShowConfirmationDialog(parameters.TargetFileName);
                if (dialogResult == MessageBoxResult.Yes)
                {
                    File.WriteAllText(newContractFile, contents);
                    ramlProjItem = folderItem.ProjectItems.AddFromFile(newContractFile);
                }
                else
                {
                    ramlProjItem = folderItem.ProjectItems.Cast<ProjectItem>().FirstOrDefault(i => i.Name == newContractFile);
                    if (ramlProjItem == null)
                        ramlProjItem = folderItem.ProjectItems.AddFromFile(newContractFile);
                }
            }
            else
            {
                File.WriteAllText(newContractFile, contents);
                ramlProjItem = folderItem.ProjectItems.AddFromFile(newContractFile);
            }

            var props = Map(parameters);
            var targetFolderPath = GetTargetFolderPath(folderPath, parameters.TargetFileName, folderItem.ContainingProject);
            var refFilePath = InstallerServices.AddRefFile(newContractFile, targetFolderPath, parameters.TargetFileName, props);
            ramlProjItem.ProjectItems.AddFromFile(refFilePath);
        }

        private static string GetTargetFolderPath(string folderPath, string targetFilename, Project proj)
        {
            var targetFolderPath = folderPath;
            if (VisualStudioAutomationHelper.IsAVisualStudio2015Project(proj))
                targetFolderPath += Path.GetFileNameWithoutExtension(targetFilename) + Path.DirectorySeparatorChar;

            return targetFolderPath;
        }

        private static string CreateNewRamlContents(string title)
        {
            var contents = "#%RAML " + RamlSpecVersion + Environment.NewLine +
                           "title: " + title + Environment.NewLine;
            return contents;
        }

        public class TemplateParams<TT> where TT : IHasName
        {
            private string _templatePath;
            private ProjectItem _projItem;
            private string _parameterName;
            private IEnumerable<TT> _parameterCollection;
            private string _folderPath;
            private ProjectItem _folderItem;
            private string _binPath;
            private string _targetNamespace;
            private string _suffix;
            private bool _ovewrite;
            private string _prefix;

            public TemplateParams(string templatePath, ProjectItem projItem, string parameterName, IEnumerable<TT> parameterCollection, string folderPath, ProjectItem folderItem, string binPath, string targetNamespace, string suffix = null, bool ovewrite = true, string prefix = null)
            {
                _templatePath = templatePath;
                _projItem = projItem;
                _parameterName = parameterName;
                _parameterCollection = parameterCollection;
                _folderPath = folderPath;
                _folderItem = folderItem;
                _binPath = binPath;
                _targetNamespace = targetNamespace;
                _suffix = suffix;
                _ovewrite = ovewrite;
                _prefix = prefix;
            }

            public string TemplatePath
            {
                get { return _templatePath; }
            }

            public ProjectItem ProjItem
            {
                get { return _projItem; }
            }

            public string ParameterName
            {
                get { return _parameterName; }
            }

            public IEnumerable<TT> ParameterCollection
            {
                get { return _parameterCollection; }
            }

            public string FolderPath
            {
                get { return _folderPath; }
            }

            public ProjectItem FolderItem
            {
                get { return _folderItem; }
            }

            public string BinPath
            {
                get { return _binPath; }
            }

            public string TargetNamespace
            {
                get { return _targetNamespace; }
            }

            public string Suffix
            {
                get { return _suffix; }
            }

            public bool Ovewrite
            {
                get { return _ovewrite; }
            }

            public string Prefix
            {
                get { return _prefix; }
            }

            public string Title { get; set; }

            public bool IncludeHasModels { get; set; }

            public bool HasModels { get; set; }
            public bool UseAsyncMethods { get; set; }
            public bool IncludeApiVersionInRoutePrefix { get; set; }
            public string ApiVersion { get; set; }

            public string TargetFolder { get; set; }
        }

        private void GenerateCodeFromTemplate<T>(TemplateParams<T> templateParams) where T : IHasName
        {
            if (!Directory.Exists(templateParams.TargetFolder))
                Directory.CreateDirectory(templateParams.TargetFolder);

            foreach (var parameter in templateParams.ParameterCollection)
            {
                var generatedFileName = GetGeneratedFileName(templateParams.Suffix, templateParams.Prefix, parameter);
                var destinationFile = Path.Combine(templateParams.TargetFolder, generatedFileName);

                var result = t4Service.TransformText(templateParams.TemplatePath, templateParams.ParameterName, parameter, templateParams.BinPath, templateParams.TargetNamespace,
                    templateParams.UseAsyncMethods, templateParams.IncludeHasModels, templateParams.HasModels, templateParams.IncludeApiVersionInRoutePrefix, templateParams.ApiVersion);
                
                var contents = templatesManager.AddServerMetadataHeader(result.Content, Path.GetFileNameWithoutExtension(templateParams.TemplatePath), templateParams.Title);
                
                if(templateParams.Ovewrite || !File.Exists(destinationFile))
                {
                    File.WriteAllText(destinationFile, contents);
                }

                if (templateParams.TargetFolder == templateParams.FolderPath)
                {
                    // add file if it does not exist
                    var fileItem = templateParams.ProjItem.ProjectItems.Cast<ProjectItem>()
                        .FirstOrDefault(i => i.Name == generatedFileName);
                    if (fileItem != null) continue;

                    if (templateParams.ProjItem.Name.EndsWith(".raml"))
                    {
                        var alreadyIncludedInProj = IsAlreadyIncludedInProject(templateParams.FolderPath, templateParams.FolderItem, generatedFileName, templateParams.ProjItem);
                        if (!alreadyIncludedInProj)
                            templateParams.ProjItem.ProjectItems.AddFromFile(destinationFile);
                    }
                    else
                    {
                        templateParams.ProjItem.ProjectItems.AddFromFile(destinationFile);
                    }
                }
                else
                {
                    var folder = templateParams.TargetFolder.TrimEnd(Path.DirectorySeparatorChar);
                    var folderName = folder.Substring(folder.LastIndexOf(Path.DirectorySeparatorChar) + 1);
                    var folderItem = VisualStudioAutomationHelper.AddFolderIfNotExists(templateParams.ProjItem.ContainingProject, folderName);
                    var fileItem = folderItem.ProjectItems.Cast<ProjectItem>().FirstOrDefault(i => i.Name == generatedFileName);
                    if (fileItem != null) continue;

                    folderItem.ProjectItems.AddFromFile(destinationFile);

                }
            }
        }

        private static bool IsAlreadyIncludedInProject(string folderPath, ProjectItem folderItem, string generatedFileName, ProjectItem fileItem)
        {
            if (VisualStudioAutomationHelper.IsAVisualStudio2015Project(fileItem.ContainingProject))
                return File.Exists(Path.Combine(folderPath, generatedFileName));

            var otherRamlFiles = GetOtherRamlFilesInProject(folderPath, fileItem);
            var alreadyIncludedInProj = false;
            foreach (var ramlFile in otherRamlFiles)
            {
                var fileName = Path.GetFileName(ramlFile);
                var otherRamlFileItem =
                    folderItem.ProjectItems.Cast<ProjectItem>().FirstOrDefault(i => i.Name == fileName);

                if (otherRamlFileItem == null) continue;
                var item = otherRamlFileItem.ProjectItems.Cast<ProjectItem>().FirstOrDefault(i => i.Name == generatedFileName);
                alreadyIncludedInProj = alreadyIncludedInProj || (item != null);
            }
            return alreadyIncludedInProj;
        }

        private static IEnumerable<string> GetOtherRamlFilesInProject(string folderPath, ProjectItem fileItem)
        {
            var ramlFiles = Directory.EnumerateFiles(folderPath, "*.raml").ToArray();
            var currentRamlFile = fileItem.FileNames[0];
            var otherRamlFiles =
                ramlFiles.Where(f => !String.Equals(f, currentRamlFile, StringComparison.InvariantCultureIgnoreCase));
            return otherRamlFiles;
        }

        private static string GetGeneratedFileName<T>(string suffix, string prefix, T parameter) where T : IHasName
        {
            var name = parameter.Name;
            if (!string.IsNullOrWhiteSpace(prefix))
                name = prefix + name;
            if (!string.IsNullOrWhiteSpace(suffix))
                name += suffix;

            var generatedFileName = name + ".cs";
            return generatedFileName;
        }


    }
}