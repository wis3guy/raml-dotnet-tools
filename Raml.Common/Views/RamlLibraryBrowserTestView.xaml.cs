using System;
using System.Text;
using System.Windows;
using System.Windows.Navigation;
using CefSharp;
using CefSharp.Wpf;
using mshtml;

namespace Raml.Common.Views
{
    /// <summary>
    /// Interaction logic for RAMLLibraryBrowser.xaml
    /// </summary>
    public partial class RamlLibraryBrowserTestView : Window
    {
        private readonly string exchangeUrl;
        private const string PostData = "{\"perspective\":\"api\"}";
        private const string AdditionalHeaders = "User-Agent: studio\nX-Client-Id: vsnet1\nContent-Type: application/json";
        public string RAMLFileUrl { get; set; }

        public RamlLibraryBrowserTestView(string exchangeUrl)
        {
            this.exchangeUrl = exchangeUrl;
            InitializeComponent();
            //var browser = new ChromiumWebBrowser();
        }

        private void NavigateToLibaryBrowser()
        {
            //LibraryWebBrowser.Navigate(new Uri(exchangeUrl), null, Encoding.UTF8.GetBytes(PostData), AdditionalHeaders);
            LibraryWebBrowser.Address = exchangeUrl;
        }

        private void RAMLLibraryBrowser_OnLoaded(object sender, RoutedEventArgs e)
        {
            NavigateToLibaryBrowser(); 
        }
    }
}

