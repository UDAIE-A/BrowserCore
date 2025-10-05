using System;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

namespace BrowserCore.Engine
{
    /// <summary>
    /// Clean Chromium engine with WebView backend
    /// </summary>
    public class ChromiumEngine : IBrowserEngine
    {
        private WebView _webView;
        
        public event EventHandler<string> NavigationCompleted;
        public event EventHandler<string> TitleChanged;
        public event EventHandler<bool> LoadingStateChanged;

        public ChromiumEngine(WebView webView)
        {
            _webView = webView;
        }

        public async Task<bool> InitializeAsync()
        {
            try
            {
                // Initialize WebView events
                _webView.NavigationCompleted += OnNavigationCompleted;
                _webView.NavigationStarting += OnNavigationStarting;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ChromiumEngine init failed: {ex.Message}");
                return false;
            }
        }

        public async Task NavigateAsync(string url)
        {
            try
            {
                LoadingStateChanged?.Invoke(this, true);
                
                if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
                {
                    _webView.Navigate(new Uri(url));
                }
                else
                {
                    // Search if not a URL
                    var searchUrl = $"https://www.google.com/search?q={Uri.EscapeDataString(url)}";
                    _webView.Navigate(new Uri(searchUrl));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation failed: {ex.Message}");
                LoadingStateChanged?.Invoke(this, false);
            }
        }

        public async Task<string> ExecuteScriptAsync(string script)
        {
            try
            {
                return await _webView.InvokeScriptAsync("eval", new[] { script });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Script execution failed: {ex.Message}");
                return null;
            }
        }

        private void OnNavigationStarting(object sender, WebViewNavigationStartingEventArgs e)
        {
            LoadingStateChanged?.Invoke(this, true);
        }

        private void OnNavigationCompleted(object sender, WebViewNavigationCompletedEventArgs e)
        {
            LoadingStateChanged?.Invoke(this, false);
            NavigationCompleted?.Invoke(this, e.Uri?.ToString());
            
            // Get page title
            var titleTask = GetPageTitleAsync();
        }

        private async Task GetPageTitleAsync()
        {
            try
            {
                var title = await ExecuteScriptAsync("document.title");
                if (!string.IsNullOrEmpty(title))
                {
                    TitleChanged?.Invoke(this, title);
                }
            }
            catch { /* Ignore title errors */ }
        }

        public void Dispose()
        {
            if (_webView != null)
            {
                _webView.NavigationCompleted -= OnNavigationCompleted;
                _webView.NavigationStarting -= OnNavigationStarting;
            }
        }
    }
}