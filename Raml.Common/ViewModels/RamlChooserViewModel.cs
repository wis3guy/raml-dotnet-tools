using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Caliburn.Micro;
using Microsoft.Win32;

namespace Raml.Common.ViewModels
{
    public class RamlChooserViewModel : Screen
    {
        private const string RamlFileExtension = ".raml";
        // action to execute when clicking Ok button (add RAML Reference, Scaffold Web Api, etc.)
        private Action<RamlChooserActionParams> action;
        private string exchangeUrl;
        public string RamlTempFilePath { get; private set; }
        public string RamlOriginalSource { get; set; }

        public IServiceProvider ServiceProvider { get; set; }

        private bool isContractUseCase;
        private bool canAddNewContract;
        private int height;
        private bool canLibraryButton;
        private bool canBrowseButton;
        private string url;
        private string title1;
        private bool canTitle;
        private bool canUrl;
        private bool canGoButton;
        private Visibility progressBarVisibility;
        private bool isNewRamlOption;
        private bool existingRamlOption;

        private bool IsContractUseCase
        {
            get { return isContractUseCase; }
            set
            {
                if (value.Equals(isContractUseCase)) return;
                isContractUseCase = value;
                NotifyOfPropertyChange("ContractUseCaseVisibility");
            }
        }

        public Visibility ContractUseCaseVisibility
        {
            get
            {
                return IsContractUseCase ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public void Load(IServiceProvider serviceProvider, Action<RamlChooserActionParams> action, string title, bool isContractUseCase, string exchangeUrl)
        {
            this.action = action;
            this.exchangeUrl = exchangeUrl; // "https://qa.anypoint.mulesoft.com/exchange/#!/?types=api"; // testing URL
            ServiceProvider = serviceProvider;
            DisplayName = title;
            IsContractUseCase = isContractUseCase;
            CanAddNewContract = false;
            Height = isContractUseCase ? 570 : 475;
            NotifyOfPropertyChange(() => Height);
        }

        public int Height
        {
            get { return height; }
            set
            {
                if (value == height) return;
                height = value;
                NotifyOfPropertyChange(() => Height);
            }
        }

        public bool CanAddNewContract
        {
            get { return canAddNewContract; }
            set
            {
                if (value == canAddNewContract) return;
                canAddNewContract = value;
                NotifyOfPropertyChange(() => CanAddNewContract);
            }
        }

        public async void BrowseButton()
        {
            SelectExistingRamlOption();
            FileDialog fd = new OpenFileDialog();
            fd.DefaultExt = ".raml;*.rml";
            fd.Filter = "RAML files |*.raml;*.rml";

            var opened = fd.ShowDialog();

            if (opened != true)
            {
                return;
            }

            RamlTempFilePath = fd.FileName;
            RamlOriginalSource = fd.FileName;

            var title = Path.GetFileName(fd.FileName);

            
            var preview = new RamlPreview(ServiceProvider, action, RamlTempFilePath, RamlOriginalSource, title, isContractUseCase);
            
            StartProgress();
            await preview.FromFile();
            StopProgress();

            var dialogResult = preview.ShowDialog();
            if(dialogResult == true)
                TryClose();
        }

        private void StartProgress()
        {
            ProgressBarVisibility = Visibility.Visible;
            CanAddNewContract = false;
            CanBrowseButton = false;
            CanLibraryButton = false;
            Mouse.OverrideCursor = Cursors.Wait;
        }

        public Visibility ProgressBarVisibility
        {
            get { return progressBarVisibility; }
            set
            {
                if (value == progressBarVisibility) return;
                progressBarVisibility = value;
                NotifyOfPropertyChange(() => ProgressBarVisibility);
            }
        }

        private void StopProgress()
        {
            ProgressBarVisibility = Visibility.Hidden;
            CanAddNewContract = true;
            CanBrowseButton = true;
            CanLibraryButton = true;
            Mouse.OverrideCursor = null;
        }

        public bool CanBrowseButton
        {
            get { return canBrowseButton; }
            set
            {
                if (value == canBrowseButton) return;
                canBrowseButton = value;
                NotifyOfPropertyChange(() => CanBrowseButton);
            }
        }

        public bool CanLibraryButton
        {
            get { return canLibraryButton; }
            set
            {
                if (value == canLibraryButton) return;
                canLibraryButton = value;
                NotifyOfPropertyChange(() => CanLibraryButton);
            }
        }

        public async void LibraryButton()
        {
            SelectExistingRamlOption();
            var rmlLibrary = new RAMLLibraryBrowser(exchangeUrl);
            var selectedRamlFile = rmlLibrary.ShowDialog();

            if (selectedRamlFile.HasValue && selectedRamlFile.Value)
            {
                var url = rmlLibrary.RAMLFileUrl;

                Url = url;

                var preview = new RamlPreview(ServiceProvider, action, RamlTempFilePath, url, "title", isContractUseCase);
                
                StartProgress();
                await preview.FromURL();
                StopProgress();

                var dialogResult = preview.ShowDialog();
                if (dialogResult == true)
                    TryClose();
            }
        }

        public string Url
        {
            get { return url; }
            set
            {
                if (value == url) return;
                url = value;
                NotifyOfPropertyChange(() => Url);
            }
        }


        public async void GoButton()
        {
            SelectExistingRamlOption();
            var preview = new RamlPreview(ServiceProvider, action, RamlTempFilePath, Url, "title", isContractUseCase);
            
            StartProgress();
            await preview.FromURL();
            StopProgress();

            var dialogResult = preview.ShowDialog();
            if(dialogResult == true)
                TryClose();
        }

        public void NewRaml_Checked()
        {
            NewOrExistingRamlOptionChanged(IsNewRamlOption);
        }

        public bool IsNewRamlOption
        {
            get { return isNewRamlOption; }
            set
            {
                if (value == isNewRamlOption) return;
                isNewRamlOption = value;
                NotifyOfPropertyChange(() => IsNewRamlOption);
            }
        }

        private void NewOrExistingRamlOptionChanged(bool newRamlIsChecked)
        {
            CanTitle = newRamlIsChecked;

            CanUrl = !newRamlIsChecked;
            CanGoButton = !newRamlIsChecked;
            CanBrowseButton = !newRamlIsChecked;
        }

        public bool CanGoButton
        {
            get { return canGoButton; }
            set
            {
                if (value == canGoButton) return;
                canGoButton = value;
                NotifyOfPropertyChange(() => CanGoButton);
            }
        }

        public bool CanUrl
        {
            get { return canUrl; }
            set
            {
                if (value == canUrl) return;
                canUrl = value;
                NotifyOfPropertyChange(() => CanUrl);
            }
        }

        public bool CanTitle
        {
            get { return canTitle; }
            set
            {
                if (value == canTitle) return;
                canTitle = value;
                NotifyOfPropertyChange(() => CanTitle);
            }
        }

        public void BrowseExisting_Checked()
        {
            NewOrExistingRamlOptionChanged(ExistingRamlOption);
        }

        public bool ExistingRamlOption
        {
            get { return existingRamlOption; }
            set
            {
                if (value == existingRamlOption) return;
                existingRamlOption = value;
                NotifyOfPropertyChange(() => ExistingRamlOption);
            }
        }

        public void Title_OnTextChanged()
        {
            CanAddNewContract = false;
            if (string.IsNullOrWhiteSpace(Title)) 
                return;

            SelectNewRamlOption();
            NewRamlFilename = NetNamingMapper.RemoveIndalidChars(Title) + RamlFileExtension;
            NewRamlNamespace = GetNamespace(NewRamlFilename);
            CanAddNewContract = true;
        }

        public string Title
        {
            get { return title1; }
            set
            {
                if (value == title1) return;
                title1 = value;
                NotifyOfPropertyChange(() => Title);
            }
        }

        public string NewRamlNamespace { get; set; }

        public string NewRamlFilename { get; set; }

        private string GetNamespace(string fileName)
        {
            return VisualStudioAutomationHelper.GetDefaultNamespace(ServiceProvider) + "." +
                     NetNamingMapper.GetObjectName(Path.GetFileNameWithoutExtension(fileName));
        }

        private void SelectExistingRamlOption()
        {
            ExistingRamlOption = true;
            IsNewRamlOption = false;
        }

        private void SelectNewRamlOption()
        {
            IsNewRamlOption = true;
            ExistingRamlOption = false;
        }

        public void btnCancel()
        {
            TryClose();
        }

        public void AddNewContract()
        {
            var preview = new RamlPreview(ServiceProvider, action, Title);
            preview.NewContract();
            preview.ShowDialog();
            TryClose();
        }
    }
}