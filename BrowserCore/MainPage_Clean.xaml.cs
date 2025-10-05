using System;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using BrowserCore.Engine;

namespace BrowserCore
{
    /// <summary>
    /// Clean MainPage with CustomHtmlEngine for ARM-optimized YouTube video playback
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private IBrowserEngine _engine;
        private readonly string _defaultUrl = "https://www.google.com";
        private string _currentUrl;
        private DispatcherTimer _performanceTimer;
        private GPUAccelerationEngine _gpuEngine;

        public MainPage()
        {
            this.InitializeComponent();
            this.Loaded += MainPage_Loaded;
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            await InitializeBrowserAsync();
            StartPerformanceMonitoring();
        }

        private async Task InitializeBrowserAsync()
        {
            try
            {
                StatusText.Text = "Initializing Custom HTML Engine...";
                
                // Initialize GPU acceleration first
                _gpuEngine = new GPUAccelerationEngine();
                _gpuEngine.GPUStateChanged += OnGPUStateChanged;
                await _gpuEngine.InitializeAsync();
                
                // Initialize Custom HTML Engine (bypasses WebView)
                _engine = new CustomHtmlEngine(ContentScrollViewer, ContentPanel);
                _engine.NavigationCompleted += OnNavigationCompleted;
                _engine.TitleChanged += OnTitleChanged;
                _engine.LoadingStateChanged += OnLoadingStateChanged;
                
                // Initialize the engine
                await _engine.InitializeAsync();
                
                // Navigate to default page
                await NavigateToUrlAsync(_defaultUrl);
                
                StatusText.Text = "Custom HTML Engine Ready - YouTube Optimized";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Initialization failed: {ex.Message}";
            }
        }

        private void OnNavigationCompleted(object sender, string url)
        {
            _currentUrl = url;
            AddressBar.Text = url;
            System.Diagnostics.Debug.WriteLine($"Navigation completed to: {url}");
            UpdateGpuStatus();
        }

        private void OnTitleChanged(object sender, string title)
        {
            // Could update window title if needed
            System.Diagnostics.Debug.WriteLine($"Page title: {title}");
        }

        private void OnLoadingStateChanged(object sender, bool isLoading)
        {
            LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
            StatusText.Text = isLoading ? "Loading..." : "Ready";
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Custom HTML engine doesn't support back/forward navigation yet
            // Could be implemented with history stack in future
            StatusText.Text = "Back navigation not supported in custom engine";
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            // Custom HTML engine doesn't support back/forward navigation yet
            // Could be implemented with history stack in future
            StatusText.Text = "Forward navigation not supported in custom engine";
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentUrl))
            {
                await _engine?.NavigateAsync(_currentUrl);
            }
        }

        private async void GoButton_Click(object sender, RoutedEventArgs e)
        {
            var url = AddressBar.Text.Trim();
            await NavigateToUrlAsync(url);
        }

        private async void AddressBar_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                var url = AddressBar.Text.Trim();
                await NavigateToUrlAsync(url);
                e.Handled = true;
            }
        }

        private async Task NavigateToUrlAsync(string url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url))
                    return;

                // Add protocol if missing
                if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                {
                    // Check if it looks like a search query
                    if (!url.Contains(".") || url.Contains(" "))
                    {
                        url = $"https://www.google.com/search?q={Uri.EscapeDataString(url)}";
                    }
                    else
                    {
                        url = "https://" + url;
                    }
                }

                _currentUrl = url;
                await _engine?.NavigateAsync(url);
                AddressBar.Text = url;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Navigation failed: {ex.Message}";
            }
        }

        private void StartPerformanceMonitoring()
        {
            _performanceTimer = new DispatcherTimer();
            _performanceTimer.Interval = TimeSpan.FromSeconds(2);
            _performanceTimer.Tick += (s, e) =>
            {
                try
                {
                    UpdateGpuStatus();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Performance monitoring error: {ex.Message}");
                }
            };
            _performanceTimer.Start();
        }

        private void UpdateGpuStatus()
        {
            if (_gpuEngine != null)
            {
                var status = _gpuEngine.IsHardwareAccelerated ? "GPU: ON" : "GPU: OFF";
                StatusText.Text = $"Custom Engine Ready - {status}";
            }
        }

        private void OnGPUStateChanged(object sender, bool isEnabled)
        {
            UpdateGpuStatus();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(SettingsPage));
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() => NavigateToUrlAsync(_defaultUrl));
        }

        // Bottom navigation event handlers
        private void BottomBackButton_Click(object sender, RoutedEventArgs e)
        {
            BackButton_Click(sender, e);
        }

        private void BottomForwardButton_Click(object sender, RoutedEventArgs e)
        {
            ForwardButton_Click(sender, e);
        }

        private void BottomRefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshButton_Click(sender, e);
        }

        private void BottomGPUButton_Click(object sender, RoutedEventArgs e)
        {
            // Toggle GPU acceleration or show GPU info
            UpdateGpuStatus();
        }

        private void BottomSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsButton_Click(sender, e);
        }
    }
}