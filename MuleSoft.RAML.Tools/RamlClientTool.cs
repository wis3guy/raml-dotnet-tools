using System.Net;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Designer.Interfaces;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using MuleSoft.RAML.Tools.Properties;
using Raml.Common;
using Raml.Tools.ClientGenerator;
using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using IServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace MuleSoft.RAML.Tools
{
    
    [ComVisible(true)]
    [Guid("91585B26-E0B4-4BEE-B4A5-12345678ABCD")]
    [CodeGeneratorRegistration(typeof(RamlClientTool), "Raml Client Generator Custom Tool", "{FAE04EC1-301F-11D3-BF4B-00C04F79EFBC}", GeneratesDesignTimeSource = true)]
    [ProvideObject(typeof(RamlClientTool))]
    public class RamlClientTool : IVsSingleFileGenerator, IObjectWithSite
    {
        private object site;
        private CodeDomProvider codeDomProvider;
        private ServiceProvider serviceProvider;
        private readonly string ClientT4TemplateName = Settings.Default.ClientT4TemplateName;
        private readonly TemplatesManager templatesManager = new TemplatesManager();

        private CodeDomProvider CodeProvider
        {
            get
            {
                if (codeDomProvider == null)
                {
                    var provider = (IVSMDCodeDomProvider)SiteServiceProvider.GetService(typeof(IVSMDCodeDomProvider).GUID);
                    if (provider != null)
                        codeDomProvider = (CodeDomProvider)provider.CodeDomProvider;
                }
                return codeDomProvider;
            }
        }

        private ServiceProvider SiteServiceProvider
        {
            get
            {
                if (serviceProvider == null)
                {
                    var oleServiceProvider = site as IServiceProvider;
                    serviceProvider = new ServiceProvider(oleServiceProvider);
                }
                return serviceProvider;
            }
        }

        #region IVsSingleFileGenerator

        public int DefaultExtension(out string pbstrDefaultExtension)
        {
            pbstrDefaultExtension = "." + CodeProvider.FileExtension;
            return VSConstants.S_OK;
        }

        public int Generate(string wszInputFilePath, string bstrInputFileContents, string wszDefaultNamespace,
            IntPtr[] rgbOutputFileContents, out uint pcbOutput, IVsGeneratorProgress pGenerateProgress)
        {
            try
            {
                if (bstrInputFileContents == null)
                    throw new ArgumentNullException("bstrInputFileContents");

                var containingFolder = Path.GetDirectoryName(wszInputFilePath);
                var refFilePath = InstallerServices.GetRefFilePath(wszInputFilePath);
                
                var ramlSource = RamlReferenceReader.GetRamlSource(refFilePath);
                if (string.IsNullOrWhiteSpace(ramlSource))
                    ramlSource = wszInputFilePath;

                var clientRootClassName = RamlReferenceReader.GetClientRootClassName(refFilePath);

                var globalProvider = ServiceProvider.GlobalProvider;
                var destFolderItem = GetDestinationFolderItem(wszInputFilePath, globalProvider);
                var result = UpdateRamlAndIncludedFiles(wszInputFilePath, destFolderItem, ramlSource, containingFolder);
                if (!result.IsSuccess)
                {
                    MessageBox.Show("Error when tryng to download " + ramlSource + " - Status Code: " + Enum.GetName(typeof(HttpStatusCode), result.StatusCode));
                    pcbOutput = 0;
                    return VSConstants.E_ABORT;
                }

                var dte = ServiceProvider.GlobalProvider.GetService(typeof(SDTE)) as DTE;
                var proj = VisualStudioAutomationHelper.GetActiveProject(dte);
                var apiRefsFolderPath = Path.GetDirectoryName(proj.FullName) + Path.DirectorySeparatorChar +
                                        RamlReferenceService.ApiReferencesFolderName + Path.DirectorySeparatorChar;

                templatesManager.CopyClientTemplateToProjectFolder(apiRefsFolderPath);

                var ramlInfo = RamlInfoService.GetRamlInfo(wszInputFilePath);
                if (ramlInfo.HasErrors)
                {
                    MessageBox.Show(ramlInfo.ErrorMessage);
                    pcbOutput = 0;
                    return VSConstants.E_ABORT;
                }

                var res = GenerateCodeUsingTemplate(wszInputFilePath, ramlInfo, globalProvider, refFilePath, clientRootClassName);

                if (res.HasErrors)
                {
                    ActivityLog.LogError(VisualStudioAutomationHelper.RamlVsToolsActivityLogSource, res.Errors);
                    MessageBox.Show(res.Errors);
                    pcbOutput = 0;
                    return VSConstants.E_ABORT;
                }


                var content = templatesManager.AddClientMetadataHeader(res.Content);

                var bytes = Encoding.UTF8.GetBytes(content);
                rgbOutputFileContents[0] = Marshal.AllocCoTaskMem(bytes.Length);
                Marshal.Copy(bytes, 0, rgbOutputFileContents[0], bytes.Length);
                pcbOutput = (uint) bytes.Length;
                return VSConstants.S_OK;
            }
            catch (Exception ex)
            {
                ActivityLog.LogError(VisualStudioAutomationHelper.RamlVsToolsActivityLogSource,
                    VisualStudioAutomationHelper.GetExceptionInfo(ex));

                var errorMessage = ex.Message;
                if (ex.InnerException != null)
                    errorMessage += " - " + ex.InnerException.Message;

                MessageBox.Show(errorMessage);
                pcbOutput = 0;
                return VSConstants.E_ABORT;
            }
        }

		private Result GenerateCodeUsingTemplate(string wszInputFilePath, RamlInfo ramlInfo, System.IServiceProvider globalProvider,
            string refFilePath, string clientRootClassName)
        {
            var targetNamespace = RamlReferenceReader.GetRamlNamespace(refFilePath);
            var model = GetGeneratorModel(clientRootClassName, ramlInfo, targetNamespace);
            var templateFolder = GetTemplateFolder(wszInputFilePath);
            var templateFilePath = Path.Combine(templateFolder, ClientT4TemplateName);
            var extensionPath = Path.GetDirectoryName(GetType().Assembly.Location) + Path.DirectorySeparatorChar;
            var t4Service = new T4Service(globalProvider);
            var res = t4Service.TransformText(templateFilePath, model, extensionPath, wszInputFilePath, targetNamespace);
            return res;
        }

        private static string GetTemplateFolder(string wszInputFilePath)
        {
            var directoryName = Path.GetDirectoryName(wszInputFilePath).TrimEnd(Path.DirectorySeparatorChar);
            return directoryName.Substring(0, directoryName.LastIndexOf(Path.DirectorySeparatorChar)) + Path.DirectorySeparatorChar + "Templates";
        }

        private static RamlIncludesManagerResult UpdateRamlAndIncludedFiles(string ramlFilePath, ProjectItem destFolderItem, string ramlSource, string containingFolder)
        {
            var includesFolderItem = destFolderItem.ProjectItems.Cast<ProjectItem>().FirstOrDefault(i => i.Name == InstallerServices.IncludesFolderName);

            InstallerServices.RemoveSubItemsAndAssociatedFiles(includesFolderItem);

            var includeManager = new RamlIncludesManager();
            var result = includeManager.Manage(ramlSource, containingFolder + Path.DirectorySeparatorChar + InstallerServices.IncludesFolderName, containingFolder + Path.DirectorySeparatorChar);

            if (!result.IsSuccess) 
                return result;

            UpdateRamlFile(ramlFilePath, result.ModifiedContents);

            InstallerServices.AddNewIncludedFiles(result, includesFolderItem, destFolderItem);
            return result;
        }


        private static ClientGeneratorModel GetGeneratorModel(string clientRootClassName, RamlInfo ramlInfo, string targetNamespace)
        {
            var model = new ClientGeneratorService(ramlInfo.RamlDocument, clientRootClassName, targetNamespace).BuildModel();
            return model;
        }

        private static void UpdateRamlFile(string ramlFilePath, string contents)
        {
            new FileInfo(ramlFilePath).IsReadOnly = false;
            File.WriteAllText(ramlFilePath, contents);
            new FileInfo(ramlFilePath).IsReadOnly = true;
        }



        private static ProjectItem GetDestinationFolderItem(string wszInputFilePath, System.IServiceProvider globalProvider)
        {
            var destFolderName = Path.GetFileNameWithoutExtension(wszInputFilePath);
            var dte = globalProvider.GetService(typeof (SDTE)) as DTE;
            var proj = VisualStudioAutomationHelper.GetActiveProject(dte);
            var apiRefsFolderItem =
                proj.ProjectItems.Cast<ProjectItem>().First(i => i.Name == RamlReferenceService.ApiReferencesFolderName);
            var destFolderItem = apiRefsFolderItem.ProjectItems.Cast<ProjectItem>().First(i => i.Name == destFolderName);
            return destFolderItem;
        }

        #endregion IVsSingleFileGenerator

        #region IObjectWithSite

        public void GetSite(ref Guid riid, out IntPtr ppvSite)
        {
            if (site == null)
                Marshal.ThrowExceptionForHR(VSConstants.E_NOINTERFACE);

            // Query for the interface using the site object initially passed to the generator
            IntPtr punk = Marshal.GetIUnknownForObject(site);
            int hr = Marshal.QueryInterface(punk, ref riid, out ppvSite);
            Marshal.Release(punk);
            ErrorHandler.ThrowOnFailure(hr);
        }

        public void SetSite(object pUnkSite)
        {
            // Save away the site object for later use
            site = pUnkSite;

            // These are initialized on demand via our private CodeProvider and SiteServiceProvider properties
            codeDomProvider = null;
            serviceProvider = null;
        }

        #endregion IObjectWithSite
    }
}
