using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.Shell;
using Raml.Parser;
using Raml.Parser.Expressions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Permissions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Task = System.Threading.Tasks.Task;

namespace Raml.Common
{
    /// <summary>
    /// Interaction logic for RamlPreview.xaml
    /// </summary>
    public partial class RamlPreview : INotifyPropertyChanged
    {
        private const string RamlFileExtension = ".raml";
        private readonly RamlIncludesManager includesManager = new RamlIncludesManager();
        // action to execute when clicking Ok button (add RAML Reference, Scaffold Web Api, etc.)
        private readonly Action<RamlChooserActionParams> action;
        private readonly bool isNewContract = false;
        private bool isContractUseCase;

        public string RamlTempFilePath { get; private set; }
        public string RamlOriginalSource { get; private set; }
        public string RamlTitle { get; private set; }

        public IServiceProvider ServiceProvider { get; set; }

        private bool IsContractUseCase
        {
            get { return isContractUseCase; }
            set
            {
                isContractUseCase = value;
                OnPropertyChanged("ClientUseCaseVisibility");
                OnPropertyChanged("ContractUseCaseVisibility");
            }
        }

        public Visibility ClientUseCaseVisibility { get { return isContractUseCase ? Visibility.Collapsed : Visibility.Visible; } }
        public Visibility ContractUseCaseVisibility { get { return isContractUseCase ? Visibility.Visible : Visibility.Collapsed; } }

        public Visibility NewContractVisibility { get { return isNewContract ? Visibility.Collapsed : Visibility.Visible; } }

        public RamlPreview(IServiceProvider serviceProvider, Action<RamlChooserActionParams> action, string ramlTempFilePath, string ramlOriginalSource, string ramlTitle, bool isContractUseCase)
        {
            ServiceProvider = serviceProvider;
            RamlTempFilePath = ramlTempFilePath;
            RamlOriginalSource = ramlOriginalSource;
            RamlTitle = ramlTitle;
            IsContractUseCase = isContractUseCase;
            this.action = action;
            InitializeComponent();
        }

        public RamlPreview(IServiceProvider serviceProvider, Action<RamlChooserActionParams> action, string ramlTitle)
        {
            ServiceProvider = serviceProvider;
            RamlTitle = ramlTitle;
            IsContractUseCase = true;
            this.action = action;
            isNewContract = true;
            Height = 250;
            OnPropertyChanged("NewContractVisibility");
            InitializeComponent();
        }

        private void SetPreview(RamlDocument document)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    
                    ResourcesLabel.Text = GetResourcesPreview(document);
                    StopProgress();
                    btnOk.IsEnabled = true;
                    SetNamespace(RamlTempFilePath);

                    if (NetNamingMapper.HasIndalidChars(txtFileName.Text))
                    {
                        ShowErrorStopProgressAndDisableOk("The specied file name has invalid chars");
                        txtFileName.Focus();
                    }
                }
                catch (Exception ex)
                {
                    ShowErrorStopProgressAndDisableOk("Error while parsing raml file. " + ex.Message);
                    ActivityLog.LogError(VisualStudioAutomationHelper.RamlVsToolsActivityLogSource, VisualStudioAutomationHelper.GetExceptionInfo(ex));
                }
            });
        }

        private void SetNamespace(string fileName)
        {
            var ns = VisualStudioAutomationHelper.GetDefaultNamespace(ServiceProvider) + "." +
                     NetNamingMapper.GetObjectName(Path.GetFileNameWithoutExtension(fileName));
            txtNamespace.Text = ns;
        }

        private static string GetResourcesPreview(RamlDocument ramlDoc)
        {
            return GetChildResources(ramlDoc.Resources, 0);
        }

        const int IndentationSpaces = 4;
        private static string GetChildResources(IEnumerable<Resource> resources, int level)
        {
            var output = string.Empty;
            foreach (var childResource in resources)
            {
                
                output += new string(' ', level * IndentationSpaces) + childResource.RelativeUri;
                if (childResource.Resources.Any())
                {
                    output += Environment.NewLine;
                    output += GetChildResources(childResource.Resources, level + 1);
                }
                else
                {
                    output += Environment.NewLine;
                }
            }
            return output;
        }

        private void StartProgress()
        {
            progressBar.Visibility = Visibility.Visible;
            btnOk.IsEnabled = false;
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
        }

        private void ShowErrorStopProgressAndDisableOk(string errorMessage)
        {
            ShowErrorAndStopProgress(errorMessage);
            btnOk.IsEnabled = false;
        }

        private void ShowErrorAndStopProgress(string errorMessage)
        {
            ResourcesLabel.Text = errorMessage;
            StopProgress();
        }

        private void StopProgress()
        {
            progressBar.Visibility = Visibility.Hidden;
            Mouse.OverrideCursor = null;
        }

        private async Task GetRamlFromURL()
        {
            //StartProgress();
            //DoEvents();

            try
            {
                var url = RamlOriginalSource;
                var result = includesManager.Manage(url, Path.GetTempPath(), Path.GetTempPath());

                var raml = result.ModifiedContents;
                var parser = new RamlParser();

                var tempPath = Path.GetTempFileName();
                File.WriteAllText(tempPath, raml);

                var ramlDocument = await parser.LoadAsync(tempPath);

                var filename = SetFilename(url);

                var path = Path.Combine(Path.GetTempPath(), filename);
                File.WriteAllText(path, raml);
                RamlTempFilePath = path;
                RamlOriginalSource = url;

                SetPreview(ramlDocument);

                btnOk.IsEnabled = true;
                //StopProgress();
            }
            catch (UriFormatException uex)
            {
                ShowErrorAndDisableOk(uex.Message);
            }
            catch (HttpRequestException rex)
            {
                ShowErrorAndDisableOk(GetFriendlyMessage(rex));
                ActivityLog.LogError(VisualStudioAutomationHelper.RamlVsToolsActivityLogSource,
                    VisualStudioAutomationHelper.GetExceptionInfo(rex));
            }
            catch (Exception ex)
            {
                ShowErrorAndDisableOk(ex.Message);
                ActivityLog.LogError(VisualStudioAutomationHelper.RamlVsToolsActivityLogSource,
                    VisualStudioAutomationHelper.GetExceptionInfo(ex));
            }
        }

        private string SetFilename(string url)
        {
            var filename = GetFilename(url);

            txtFileName.Text = filename;
            return filename;
        }

        private static string GetFilename(string url)
        {
            var filename = Path.GetFileName(url);

            if (string.IsNullOrEmpty(filename))
                filename = "reference.raml";

            if (!filename.ToLowerInvariant().EndsWith(RamlFileExtension))
                filename += RamlFileExtension;

            filename = NetNamingMapper.RemoveIndalidChars(Path.GetFileNameWithoutExtension(filename)) +
                       RamlFileExtension;
            return filename;
        }

        private static string GetFriendlyMessage(HttpRequestException rex)
        {
            if (rex.Message.Contains("404"))
                return "Could not find specified URL. Server responded with Not Found (404) status code";

            return rex.Message;
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            StartProgress();
            DoEvents();

            if (!txtFileName.Text.ToLowerInvariant().EndsWith(RamlFileExtension))
            {
                ShowErrorStopProgressAndDisableOk("Error: the file must have the .raml extension.");
                DialogResult = false;
                return;
            }

            if (!IsContractUseCase && !File.Exists(RamlTempFilePath))
            {
                ShowErrorStopProgressAndDisableOk("Error: the specified file does not exist.");
                DialogResult = false;
                return;
            }

            var path = Path.GetDirectoryName(GetType().Assembly.Location) + Path.DirectorySeparatorChar;

            try
            {
                ResourcesLabel.Text = "Processing. Please wait..." + Environment.NewLine + Environment.NewLine;

                // Execute action (add RAML Reference, Scaffold Web Api, etc)
                var parameters = new RamlChooserActionParams(RamlOriginalSource, RamlTempFilePath, RamlTitle, path,
                    txtFileName.Text, txtNamespace.Text, doNotScaffold: isNewContract);

                if(isContractUseCase)
                    parameters.UseAsyncMethods = CheckBoxUseAsync.IsChecked.HasValue && CheckBoxUseAsync.IsChecked.Value;
                
                if(!isContractUseCase)
                    parameters.ClientRootClassName = txtClientName.Text;

                action(parameters);

                ResourcesLabel.Text += "Succeeded";
                StopProgress();
                btnOk.IsEnabled = true;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ShowErrorStopProgressAndDisableOk("Error: " + ex.Message);

                ActivityLog.LogError(VisualStudioAutomationHelper.RamlVsToolsActivityLogSource, VisualStudioAutomationHelper.GetExceptionInfo(ex));
            }
        }



        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ShowErrorAndDisableOk(string errorMessage)
        {
            ShowError(errorMessage);
            btnOk.IsEnabled = false;
        }

        private void ShowError(string errorMessage)
        {
            ResourcesLabel.Text = errorMessage;
        }



        #region refresh UI
        [SecurityPermission(SecurityAction.Demand, Flags = SecurityPermissionFlag.UnmanagedCode)]
        public void DoEvents()
        {
            Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ContextIdle, null);

            //DispatcherFrame frame = new DispatcherFrame();
            //Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
            //    new DispatcherOperationCallback(ExitFrame), frame);
            //Dispatcher.PushFrame(frame);
        }

        public object ExitFrame(object f)
        {
            ((DispatcherFrame)f).Continue = false;

            return null;
        }
        #endregion

        public void NewContract()
        {
            SetFilename(RamlTitle + RamlFileExtension);
            SetNamespace(txtFileName.Text);
        }

        public async Task FromFile()
        {
            //TODO: check
            try
            {
                txtFileName.Text = Path.GetFileName(RamlTempFilePath);

                SetDefaultClientRootClassName();

                var result = includesManager.Manage(RamlTempFilePath, Path.GetTempPath(), Path.GetTempPath());
                var parser = new RamlParser();

                var tempPath = Path.GetTempFileName();
                File.WriteAllText(tempPath, result.ModifiedContents);

                var document = await parser.LoadAsync(tempPath);

                SetPreview(document);
            }
            catch (Exception ex)
            {
                ShowErrorStopProgressAndDisableOk("Error while parsing raml file. " + ex.Message);
                ActivityLog.LogError(VisualStudioAutomationHelper.RamlVsToolsActivityLogSource,
                    VisualStudioAutomationHelper.GetExceptionInfo(ex));
            }
        }

        private void SetDefaultClientRootClassName()
        {
            var rootName = NetNamingMapper.GetObjectName(Path.GetFileNameWithoutExtension(RamlTempFilePath));
            if (!rootName.ToLower().Contains("client"))
                rootName += "Client";
            txtClientName.Text = rootName;
        }

        public async Task FromURL()
        {
            SetDefaultClientRootClassName();
            await GetRamlFromURL();
        }


        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }

    }
}
