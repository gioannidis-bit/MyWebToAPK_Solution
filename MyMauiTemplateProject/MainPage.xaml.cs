using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using Microsoft.Maui.Controls;

namespace MyMauiTemplateProject
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        protected async override void OnAppearing()
        {
            base.OnAppearing();

            try
            {
                // Assume the file is in the app bundle.
                using (var stream = await FileSystem.OpenAppPackageFileAsync("Resources/Assets/config.json"))
                using (var reader = new StreamReader(stream))
                {
                    string json = await reader.ReadToEndAsync();
                    var config = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (config != null && config.TryGetValue("url", out string newUrl))
                    {
                        MyWebView.Source = new UrlWebViewSource { Url = newUrl };
                        return;
                    }
                    else
                    {
                        MyWebView.Source = new UrlWebViewSource { Url = "https://default-url.com" };
                    }
                }
            }
            catch (Exception ex)
            {
                // Fallback in case of any error
                MyWebView.Source = new UrlWebViewSource { Url = "https://default-url.com" };
            }
        }
    }
}
