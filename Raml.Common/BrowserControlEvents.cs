//using System.Security.Permissions;
//using Raml.Common.ViewModels;

//namespace Raml.Common
//{
//    [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
//    [System.Runtime.InteropServices.ComVisible(true)]
//    public class BrowserControlEvents
//    {
//        readonly RamlLibraryBrowserViewModel _externalWpf;
//        public BrowserControlEvents(RamlLibraryBrowserViewModel viewModel)
//        {
//            _externalWpf = viewModel;
//        }

//        public void SetRamlUrl(string url)
//        {
//            _externalWpf.NewUrlSelected(url);
//        }
//    }
//}

using System.Security.Permissions;
using Raml.Common.ViewModels;
using Raml.Common.Views;

namespace Raml.Common
{
    [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
    [System.Runtime.InteropServices.ComVisible(true)]
    public class BrowserControlEvents
    {
        readonly RamlLibraryBrowserView _externalWpf;
        public BrowserControlEvents(RamlLibraryBrowserView viewModel)
        {
            _externalWpf = viewModel;
        }

        public void SetRamlUrl(string url)
        {
            _externalWpf.NewUrlSelected(url);
        }
    }
}