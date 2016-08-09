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
    public partial class RamlScaffoldService
    {
        private const string RamlSpecVersion = "0.8";
        private const string ControllerBaseTemplateName = "ApiControllerBase.t4";
        private const string ControllerInterfaceTemplateName = "ApiControllerInterface.t4";
        private const string ControllerImplementationTemplateName = "ApiControllerImplementation.t4";
        private const string ModelTemplateName = "ApiModel.t4";
        private const string EnumTemplateName = "ApiEnum.t4";
        
        private readonly string contractsFolderName = Settings.Default.ContractsFolderName;
        private readonly IServiceProvider serviceProvider;
        private readonly TemplatesManager templatesManager = new TemplatesManager();
        private static readonly string ContractsFolder = Path.DirectorySeparatorChar + Settings.Default.ContractsFolderName + Path.DirectorySeparatorChar;
        private static readonly string IncludesFolder = Path.DirectorySeparatorChar + "includes" + Path.DirectorySeparatorChar;

        private readonly string nugetPackagesSource = Settings.Default.NugetPackagesSource;
        private readonly string ramlApiCorePackageId = Settings.Default.RAMLApiCorePackageId;
        private readonly string ramlApiCorePackageVersion = Settings.Default.RAMLApiCorePackageVersion;
        private readonly string newtonsoftJsonPackageId = Settings.Default.NewtonsoftJsonPackageId;
        private readonly string newtonsoftJsonPackageVersion = Settings.Default.NewtonsoftJsonPackageVersion;
        private readonly string newtonsoftJsonForCorePackageVersion = Settings.Default.NewtonsoftJsonForCorePackageVersion;
        private readonly string microsoftNetHttpPackageId = Settings.Default.MicrosoftNetHttpPackageId;
        private readonly string microsoftNetHttpPackageVersion = Settings.Default.MicrosoftNetHttpPackageVersion;

        private string templateSubFolder;

        private readonly CodeGenerator codeGenerator;

        public RamlScaffoldService(IT4Service t4Service, IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
            codeGenerator = new CodeGenerator(t4Service);
        }

        public void AddContract(RamlChooserActionParams parameters)
        {
            var dte = serviceProvider.GetService(typeof(SDTE)) as DTE;
            var proj = VisualStudioAutomationHelper.GetActiveProject(dte);

            InstallNugetDependencies(proj);
            if(VisualStudioAutomationHelper.IsAVisualStudio2015Project(proj))
                AddXmlFormatterInWebApiConfig(proj);

            var folderItem = VisualStudioAutomationHelper.AddFolderIfNotExists(proj, contractsFolderName);
            var contractsFolderPath = Path.GetDirectoryName(proj.FullName) + Path.DirectorySeparatorChar + contractsFolderName + Path.DirectorySeparatorChar;

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

            var contractsFolderItem = VisualStudioAutomationHelper.AddFolderIfNotExists(proj, contractsFolderName);
            var ramlItem =
                contractsFolderItem.ProjectItems.Cast<ProjectItem>()
                    .First(i => i.Name.ToLowerInvariant() == parameters.TargetFileName.ToLowerInvariant());
            var contractsFolderPath = Path.GetDirectoryName(proj.FullName) + Path.DirectorySeparatorChar +
                                      contractsFolderName + Path.DirectorySeparatorChar;

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
            var packageVersion = VisualStudioAutomationHelper.IsAVisualStudio2015Project(proj) ? newtonsoftJsonForCorePackageVersion : newtonsoftJsonPackageVersion;

            var componentModel = (IComponentModel)serviceProvider.GetService(typeof(SComponentModel));
            var installerServices = componentModel.GetService<IVsPackageInstallerServices>();
            var installer = componentModel.GetService<IVsPackageInstaller>();

            var packs = installerServices.GetInstalledPackages(proj).ToArray();

            // RAML.Api.Core dependencies
            NugetInstallerHelper.InstallPackageIfNeeded(proj, packs, installer, newtonsoftJsonPackageId, packageVersion, Settings.Default.NugetExternalPackagesSource);
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
            for (var i = 0; i < lines.Count; i++)
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
                    AddGeneratedSuffixToFiles = RamlReferenceReader.GetAddGeneratedSuffix(refFilePath)
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
            return (from item in ramlItems where HasReferenceFile(item.FileNames[0]) select item.FileNames[0]).ToList();
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
                    TargetFolder = TargetFolderResolver.GetImplementationControllersFolderPath(proj, parameters.ImplementationControllersFolder),
                    RelativeFolder = parameters.ImplementationControllersFolder,
                    Title = Settings.Default.ControllerImplementationTemplateTitle,
                    IncludeHasModels = true,
                    HasModels = model.Objects.Any(o => o.IsScalar == false) || model.Enums.Any(),
                    UseAsyncMethods = parameters.UseAsyncMethods,
                    IncludeApiVersionInRoutePrefix = parameters.IncludeApiVersionInRoutePrefix,
                    ApiVersion = model.ApiVersion
                };

            codeGenerator.GenerateCodeFromTemplate(controllerImplementationTemplateParams);
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
                    parameters.TargetNamespace, "Controller", true,
                    "I" + GetVersionPrefix(parameters.IncludeApiVersionInRoutePrefix, model.ApiVersion))
                {
                    Title = Settings.Default.ControllerInterfaceTemplateTitle,
                    IncludeHasModels = true,
                    HasModels = model.Objects.Any(o => o.IsScalar == false) || model.Enums.Any(),
                    UseAsyncMethods = parameters.UseAsyncMethods,
                    IncludeApiVersionInRoutePrefix = parameters.IncludeApiVersionInRoutePrefix,
                    ApiVersion = model.ApiVersion,
                    TargetFolder = targetFolderPath
                };
            codeGenerator.GenerateCodeFromTemplate(controllerInterfaceParams);
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
                    parameters.TargetNamespace, "Controller", true,
                    GetVersionPrefix(parameters.IncludeApiVersionInRoutePrefix, model.ApiVersion))
                {
                    Title = Settings.Default.BaseControllerTemplateTitle,
                    IncludeHasModels = true,
                    HasModels = model.Objects.Any(o => o.IsScalar == false) || model.Enums.Any(),
                    UseAsyncMethods = parameters.UseAsyncMethods,
                    IncludeApiVersionInRoutePrefix = parameters.IncludeApiVersionInRoutePrefix,
                    ApiVersion = model.ApiVersion,
                    TargetFolder = targetFolderPath
                };
            codeGenerator.GenerateCodeFromTemplate(controllerBaseTemplateParams);
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
                GetVersionPrefix(parameters.IncludeApiVersionInRoutePrefix, model.ApiVersion) +
                (parameters.AddGeneratedSuffixToFiles ? ".generated" : string.Empty))
            {
                Title = Settings.Default.ModelsTemplateTitle,
                RelativeFolder = parameters.ModelsFolder,
                TargetFolder = TargetFolderResolver.GetModelsTargetFolder(ramlItem.ContainingProject,
                    targetFolderPath, parameters.ModelsFolder)
            };

            codeGenerator.GenerateCodeFromTemplate(apiObjectTemplateParams);
        }

        private void AddOrUpdateEnums(RamlChooserActionParams parameters, string contractsFolderPath, ProjectItem ramlItem, WebApiGeneratorModel model, ProjectItem folderItem, string extensionPath)
        {
            templatesManager.CopyServerTemplateToProjectFolder(contractsFolderPath, EnumTemplateName,
                Settings.Default.EnumsTemplateTitle, templateSubFolder);
            var templatesFolder = Path.Combine(contractsFolderPath, "Templates");

            var targetFolderPath = GetTargetFolderPath(contractsFolderPath, ramlItem.FileNames[0], folderItem.ContainingProject);

            var apiEnumTemplateParams = new TemplateParams<ApiEnum>(
                Path.Combine(templatesFolder, EnumTemplateName), ramlItem, "apiEnum", model.Enums,
                targetFolderPath, folderItem, extensionPath, parameters.TargetNamespace,
                GetVersionPrefix(parameters.IncludeApiVersionInRoutePrefix, model.ApiVersion))
            {
                Title = Settings.Default.ModelsTemplateTitle,
                RelativeFolder = parameters.ModelsFolder,
                TargetFolder = TargetFolderResolver.GetModelsTargetFolder(ramlItem.ContainingProject,
                    targetFolderPath, parameters.ModelsFolder)
            };

            codeGenerator.GenerateCodeFromTemplate(apiEnumTemplateParams);
        }


        public void UpdateRaml(string ramlFilePath)
        {
            var dte = serviceProvider.GetService(typeof(SDTE)) as DTE;
            var proj = VisualStudioAutomationHelper.GetActiveProject(dte);
            var contractsFolderPath = Path.GetDirectoryName(proj.FullName) + Path.DirectorySeparatorChar + contractsFolderName + Path.DirectorySeparatorChar;

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
                    AddGeneratedSuffixToFiles = RamlReferenceReader.GetAddGeneratedSuffix(refFilePath)
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
                ImplementationControllersFolder = parameters.ImplementationControllersFolder,
                AddGeneratedSuffix = parameters.AddGeneratedSuffixToFiles
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




    }
}