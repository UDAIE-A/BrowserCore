using System;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.Core;
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
        private CustomHtmlEngine _engine;
        private readonly string _defaultUrl = "https://www.google.com";
        private string _currentUrl;
        private DispatcherTimer _performanceTimer;
        private GPUAccelerationEngine _gpuEngine;

        public MainPage()
        {
            try
            {
                this.InitializeComponent();
                this.Loaded += MainPage_Loaded;
                System.Diagnostics.Debug.WriteLine("MainPage constructor completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainPage constructor failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw; // Re-throw to allow app to handle
            }
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Add delay to ensure UI elements are fully initialized
                await Task.Delay(100);
                await InitializeBrowserAsync();
                StartPerformanceMonitoring();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainPage_Loaded error: {ex.Message}");
                if (StatusText != null)
                {
                    StatusText.Text = $"Load error: {ex.Message}";
                }
            }
        }

        private async Task InitializeBrowserAsync()
        {
            try
            {
                // Ensure StatusText exists before using it
                if (StatusText != null)
                {
                    StatusText.Text = "Initializing Custom HTML Engine...";
                }
                
                System.Diagnostics.Debug.WriteLine("Starting browser initialization...");
                
                // Check UI elements exist before proceeding
                if (ContentScrollViewer == null)
                {
                    System.Diagnostics.Debug.WriteLine("ContentScrollViewer is null!");
                    if (StatusText != null) StatusText.Text = "ContentScrollViewer not found";
                    return;
                }
                
                if (ContentPanel == null)
                {
                    System.Diagnostics.Debug.WriteLine("ContentPanel is null!");
                    if (StatusText != null) StatusText.Text = "ContentPanel not found";
                    return;
                }
                
                System.Diagnostics.Debug.WriteLine("UI elements verified, initializing GPU engine...");
                
                // Initialize GPU acceleration first
                _gpuEngine = new GPUAccelerationEngine();
                if (_gpuEngine != null)
                {
                    _gpuEngine.GPUStateChanged += OnGPUStateChanged;
                    await _gpuEngine.InitializeAsync();
                }
                
                System.Diagnostics.Debug.WriteLine("GPU engine initialized, creating HTML engine...");
                
                // Initialize Custom HTML Engine (bypasses WebView)
                _engine = new CustomHtmlEngine();
                if (_engine != null)
                {
                    _engine.NavigationCompleted += OnNavigationCompleted;
                    _engine.TitleChanged += OnTitleChanged;
                    _engine.LoadingStateChanged += OnLoadingStateChanged;
                    
                    // Initialize the engine with UI elements
                    _engine.Initialize(ContentScrollViewer, ContentPanel);
                    await _engine.InitializeAsync();
                    
                    System.Diagnostics.Debug.WriteLine("HTML engine initialized, navigating to default URL...");
                    
                    // Navigate to default page
                    await NavigateToUrlAsync(_defaultUrl);
                }

                // WebView fallback removed to enforce no-WebView policy
                
                if (StatusText != null)
                {
                    StatusText.Text = "Custom HTML Engine Ready - YouTube Optimized";
                }
                
                System.Diagnostics.Debug.WriteLine("Browser initialization completed successfully!");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Browser initialization failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                if (StatusText != null)
                {
                    StatusText.Text = $"Initialization failed: {ex.Message}";
                }
            }
        }

        private void OnNavigationCompleted(object sender, string url)
        {
            try
            {
                _currentUrl = url;
                if (AddressBar != null)
                {
                    AddressBar.Text = url ?? "";
                }
                System.Diagnostics.Debug.WriteLine($"Navigation completed to: {url}");
                UpdateGpuStatus();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnNavigationCompleted error: {ex.Message}");
            }
        }

        private void OnTitleChanged(object sender, string title)
        {
            // Could update window title if needed
            System.Diagnostics.Debug.WriteLine($"Page title: {title}");
        }

        private void OnLoadingStateChanged(object sender, bool isLoading)
        {
            try
            {
                if (LoadingOverlay != null)
                {
                    LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
                }
                if (StatusText != null)
                {
                    StatusText.Text = isLoading ? "Loading..." : "Ready";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnLoadingStateChanged error: {ex.Message}");
            }
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
                System.Diagnostics.Debug.WriteLine($"NavigateToUrlAsync called with: {url}");
                
                if (string.IsNullOrWhiteSpace(url))
                {
                    System.Diagnostics.Debug.WriteLine("Empty URL provided to NavigateToUrlAsync");
                    return;
                }

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

                System.Diagnostics.Debug.WriteLine($"Final URL for navigation: {url}");
                _currentUrl = url;
                
                if (AddressBar != null)
                {
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => AddressBar.Text = url);
                }
                
                // Always use Custom HTML engine (no WebView)
                if (_engine != null)
                {
                    System.Diagnostics.Debug.WriteLine("Using Custom engine for: " + url);
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        ContentScrollViewer.Visibility = Visibility.Visible;
                        if (StatusText != null) StatusText.Text = "Loading (Custom) ...";
                    });
                    await _engine.NavigateAsync(url);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Engine is null!");
                    if (StatusText != null)
                    {
                        await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => StatusText.Text = "Engine not initialized");
                    }
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"Navigation failed: {ex.Message}";
                System.Diagnostics.Debug.WriteLine(errorMessage);
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                
                if (StatusText != null)
                {
                    StatusText.Text = errorMessage;
                }
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

        // WebView selection logic removed to enforce no-WebView policy

        private void UpdateGpuStatus()
        {
            try
            {
                if (_gpuEngine != null && StatusText != null)
                {
                    var status = _gpuEngine.IsHardwareAccelerated ? "GPU: ON" : "GPU: OFF";
                    StatusText.Text = $"Custom Engine Ready - {status}";
                    // Also update the small GPU status text on the bottom button (if present)
                    try
                    {
                        if (GpuStatusText != null)
                        {
                            GpuStatusText.Text = status;
                        }
                    }
                    catch { /* ignore UI update errors */ }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateGpuStatus error: {ex.Message}");
            }
        }

        private void OnGPUStateChanged(object sender, bool isEnabled)
        {
            UpdateGpuStatus();
        }

        private void BottomGPUButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_gpuEngine != null)
                {
                    // Toggle between Hardware (1) and Software (0)
                    int newMode = _gpuEngine.RenderingMode == 1 ? 0 : 1;
                    _gpuEngine.SetRenderingMode(newMode);
                    UpdateGpuStatus();
                    if (StatusText != null)
                    {
                        var modeText = newMode == 1 ? "Hardware" : "Software";
                        StatusText.Text = $"GPU rendering set to {modeText}";
                    }
                }
                else
                {
                    if (StatusText != null)
                        StatusText.Text = "GPU engine not initialized";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BottomGPUButton_Click error: {ex.Message}");
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(SettingsPage));
        }

        private async void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            await NavigateToUrlAsync(_defaultUrl);
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

        private async void BottomPhase1Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Navigate to Phase 1 test page and run APIs
                await NavigateToUrlAsync("ms-appx-web:///websites/phase1-test.html");
                
                // Also run the API tests
                if (_engine != null)
                {
                    var result = await _engine.TestPhase1APIs();
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                        if (StatusText != null)
                        {
                            StatusText.Text = "Phase 1 APIs Tested Successfully";
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Phase 1 test error: {ex.Message}");
            }
        }

        private async void BottomPhase2Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Navigate to Phase 2 test page and run APIs
                await NavigateToUrlAsync("ms-appx-web:///websites/phase2-test.html");
                
                // Also run the API tests
                if (_engine != null)
                {
                    var result = await _engine.TestPhase2APIs();
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                        if (StatusText != null)
                        {
                            StatusText.Text = "Phase 2 APIs Tested Successfully";
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Phase 2 test error: {ex.Message}");
            }
        }
    }
}
