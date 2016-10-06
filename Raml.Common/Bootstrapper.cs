using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using Caliburn.Micro;
using Raml.Common.Views;

namespace Raml.Common
{
    public class Bootstrapper : BootstrapperBase
    {
        public Bootstrapper() : base(false)
        {
            Initialize();
        }

        protected override IEnumerable<Assembly> SelectAssemblies()
        {
            return new[]
            {
                new RamlChooserModel().GetType().Assembly
            };
        }

        protected override void OnStartup(object sender, StartupEventArgs e)
        {
            //DisplayRootViewFor<RamlChooserModel>();
        }

        public void DisplayChooser()
        {
            // DisplayRootViewFor<RamlChooserModel>();
            IWindowManager windowManager;

            try
            {
                windowManager = IoC.Get<IWindowManager>();
            }
            catch
            {
                windowManager = new WindowManager();
            }

            windowManager.ShowWindow(new RamlChooserModel());
        }

        public void DisplayPropertiesEditor()
        {
            DisplayRootViewFor<RamlPropertiesEditorView>();
        }
    }
}