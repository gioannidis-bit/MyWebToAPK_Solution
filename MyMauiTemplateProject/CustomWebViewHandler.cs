#if ANDROID
using Android.Webkit;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Platform;

namespace MyMauiTemplateProject
{
    public class CustomWebViewHandler : WebViewHandler
    {
        protected override Android.Webkit.WebView CreatePlatformView()
        {
            var nativeWebView = base.CreatePlatformView();
            // Enable JavaScript on Android's native WebView.
            nativeWebView.Settings.JavaScriptEnabled = true;
            // Disable caching by setting the cache mode to NoCache.
            nativeWebView.Settings.CacheMode = CacheModes.NoCache;
            return nativeWebView;
        }
    }
}
#endif
