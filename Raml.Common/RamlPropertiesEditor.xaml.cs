using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Raml.Common.Annotations;

namespace Raml.Common
{
    /// <summary>
    /// Interaction logic for RamlPropertiesEditor.xaml
    /// </summary>
    public partial class RamlPropertiesEditor : INotifyPropertyChanged
    {
        private bool isServerUseCase;

        private string ramlPath;
        private string ns;
        private string source;
        private string clientName;
        private bool useAsyncMethods;
        private bool includeApiVersionInRoutePrefix;
        private string baseControllersFolder;
        private string implementationControllersFolder;
        private string modelsFolder;

        public string Namespace
        {
            get { return ns; }
            set
            {
                ns = value;
                OnPropertyChanged();
            }
        }

        public string Source
        {
            get { return source; }
            set
            {
                source = value; 
                OnPropertyChanged();
            }
        }

        public string ClientName
        {
            get { return clientName; }
            set
            {
                clientName = value;
                OnPropertyChanged();
            }
        }

        public bool UseAsyncMethods
        {
            get { return useAsyncMethods; }
            set
            {
                useAsyncMethods = value;
                OnPropertyChanged();
            }
        }

        public bool IncludeApiVersionInRoutePrefix
        {
            get { return includeApiVersionInRoutePrefix; }
            set
            {
                includeApiVersionInRoutePrefix = value;
                OnPropertyChanged();
            }
        }

        public string ModelsFolder
        {
            get { return modelsFolder; }
            set
            {
                modelsFolder = value; 
                OnPropertyChanged();
            }
        }

        public string BaseControllersFolder
        {
            get { return baseControllersFolder; }
            set
            {
                baseControllersFolder = value;
                OnPropertyChanged();
            }
        }

        public string ImplementationControllersFolder
        {
            get { return implementationControllersFolder; }
            set
            {
                implementationControllersFolder = value;
                OnPropertyChanged();
            }
        }

        public Visibility ServerVisibility
        {
            get { return isServerUseCase ? Visibility.Visible : Visibility.Collapsed; }
        }

        public Visibility ClientVisibility
        {
            get { return isServerUseCase ? Visibility.Collapsed : Visibility.Visible; }
        }

        public RamlPropertiesEditor()
        {
            InitializeComponent();
        }

        public void Load(string ramlPath, string serverPath, string clientPath)
        {
            this.ramlPath = ramlPath;
            if (ramlPath.Contains(serverPath) && !ramlPath.Contains(clientPath))
                isServerUseCase = true;

            var ramlProperties = RamlPropertiesManager.Load(ramlPath);
            Namespace = ramlProperties.Namespace;
            Source = ramlProperties.Source;
            if (isServerUseCase)
            {
                UseAsyncMethods = ramlProperties.UseAsyncMethods.HasValue && ramlProperties.UseAsyncMethods.Value;
                IncludeApiVersionInRoutePrefix = ramlProperties.IncludeApiVersionInRoutePrefix.HasValue &&
                                                 ramlProperties.IncludeApiVersionInRoutePrefix.Value;
                ModelsFolder = ramlProperties.ModelsFolder;
                BaseControllersFolder = ramlProperties.BaseControllersFolder;
                ImplementationControllersFolder = ramlProperties.ImplementationControllersFolder;

            }
            else
                ClientName = ramlProperties.ClientName;

            OnPropertyChanged("ServerVisibility");
            OnPropertyChanged("ClientVisibility");
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var ramlProperties = new RamlProperties
            {
                Namespace = Namespace,
                Source = Source,
                ClientName = ClientName,
                UseAsyncMethods = UseAsyncMethods,
                IncludeApiVersionInRoutePrefix = IncludeApiVersionInRoutePrefix,
                ModelsFolder = ModelsFolder,
                BaseControllersFolder = BaseControllersFolder,
                ImplementationControllersFolder = ImplementationControllersFolder
            };
            
            RamlPropertiesManager.Save(ramlProperties, ramlPath);
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
