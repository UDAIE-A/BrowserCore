using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using BrowserCore.Engine;

namespace BrowserCore
{
    /// <summary>
    /// Settings page for BrowserCore configuration
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        private GPUAccelerationEngine _gpuEngine;
        private DispatcherTimer _statsTimer;

        public SettingsPage()
        {
            this.InitializeComponent();
            this.Loaded += SettingsPage_Loaded;
        }

        private async void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            await InitializeSettingsAsync();
            StartStatsTimer();
        }

        private async System.Threading.Tasks.Task InitializeSettingsAsync()
        {
            try
            {
                // Initialize GPU engine for settings
                _gpuEngine = new GPUAccelerationEngine();
                _gpuEngine.GPUInfoUpdated += OnGPUInfoUpdated;
                _gpuEngine.GPUStateChanged += OnGPUStateChanged;
                
                await _gpuEngine.InitializeAsync();
                
                // Set default values
                RenderingModeCombo.SelectedIndex = 0; // Auto
                
                System.Diagnostics.Debug.WriteLine("Settings page initialized");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Settings init failed: {ex.Message}");
            }
        }

        private void StartStatsTimer()
        {
            _statsTimer = new DispatcherTimer();
            _statsTimer.Interval = TimeSpan.FromSeconds(2);
            _statsTimer.Tick += async (s, e) => await UpdatePerformanceStatsAsync();
            _statsTimer.Start();
        }

        private async System.Threading.Tasks.Task UpdatePerformanceStatsAsync()
        {
            try
            {
                if (_gpuEngine != null)
                {
                    var stats = await _gpuEngine.GetGPUPerformanceMetricsAsync();
                    PerformanceStatsText.Text = $"Performance: {stats}";
                }
            }
            catch (Exception ex)
            {
                PerformanceStatsText.Text = "Performance: Error";
                System.Diagnostics.Debug.WriteLine($"Stats update failed: {ex.Message}");
            }
        }

        private void OnGPUInfoUpdated(object sender, string gpuInfo)
        {
            GPUInfoText.Text = $"GPU: {gpuInfo}";
        }

        private void OnGPUStateChanged(object sender, bool isHardwareAccelerated)
        {
            GPUToggle.IsOn = isHardwareAccelerated;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private void GPUToggle_Toggled(object sender, RoutedEventArgs e)
        {
            var toggle = sender as ToggleSwitch;
            if (toggle != null && _gpuEngine != null)
            {
                // Toggle hardware acceleration
                var mode = toggle.IsOn ? 1 : 0; // Hardware or Software
                _gpuEngine.SetRenderingMode(mode);
                System.Diagnostics.Debug.WriteLine($"GPU acceleration toggled: {toggle.IsOn}");
            }
        }

        private void RenderingModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var combo = sender as ComboBox;
            var selectedItem = combo?.SelectedItem as ComboBoxItem;
            
            if (selectedItem != null && _gpuEngine != null)
            {
                var mode = int.Parse(selectedItem.Tag.ToString());
                _gpuEngine.SetRenderingMode(mode);
                System.Diagnostics.Debug.WriteLine($"Rendering mode changed to: {mode}");
            }
        }

        private void CLRNETToggle_Toggled(object sender, RoutedEventArgs e)
        {
            var toggle = sender as ToggleSwitch;
            System.Diagnostics.Debug.WriteLine($"CLRNET toggled: {toggle?.IsOn}");
            // TODO: Implement CLRNET toggle logic
        }

        private void CachingToggle_Toggled(object sender, RoutedEventArgs e)
        {
            var toggle = sender as ToggleSwitch;
            System.Diagnostics.Debug.WriteLine($"Caching toggled: {toggle?.IsOn}");
            // TODO: Implement caching toggle logic
        }

        private void ARMToggle_Toggled(object sender, RoutedEventArgs e)
        {
            var toggle = sender as ToggleSwitch;
            System.Diagnostics.Debug.WriteLine($"ARM optimizations toggled: {toggle?.IsOn}");
            // TODO: Implement ARM toggle logic
        }

        private void JavaScriptToggle_Toggled(object sender, RoutedEventArgs e)
        {
            var toggle = sender as ToggleSwitch;
            System.Diagnostics.Debug.WriteLine($"JavaScript toggled: {toggle?.IsOn}");
            // TODO: Implement JavaScript toggle logic
        }

        private void VideoOptToggle_Toggled(object sender, RoutedEventArgs e)
        {
            var toggle = sender as ToggleSwitch;
            System.Diagnostics.Debug.WriteLine($"Video optimization toggled: {toggle?.IsOn}");
            // TODO: Implement video optimization toggle logic
        }

        private async void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Reset all settings to defaults
                GPUToggle.IsOn = true;
                RenderingModeCombo.SelectedIndex = 0;
                CLRNETToggle.IsOn = true;
                CachingToggle.IsOn = true;
                ARMToggle.IsOn = true;
                JavaScriptToggle.IsOn = true;
                VideoOptToggle.IsOn = true;
                HomepageTextBox.Text = "https://www.google.com";
                
                // Reset GPU engine
                if (_gpuEngine != null)
                {
                    _gpuEngine.SetRenderingMode(2); // Auto
                }
                
                System.Diagnostics.Debug.WriteLine("Settings reset to defaults");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Reset failed: {ex.Message}");
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Save settings (in a real app, this would persist to storage)
                System.Diagnostics.Debug.WriteLine("Settings saved:");
                System.Diagnostics.Debug.WriteLine($"  GPU: {GPUToggle.IsOn}");
                System.Diagnostics.Debug.WriteLine($"  Rendering Mode: {RenderingModeCombo.SelectedIndex}");
                System.Diagnostics.Debug.WriteLine($"  CLRNET: {CLRNETToggle.IsOn}");
                System.Diagnostics.Debug.WriteLine($"  Homepage: {HomepageTextBox.Text}");
                
                // Show confirmation (simple approach)
                PerformanceStatsText.Text = "Settings saved successfully!";
                
                // Go back after a delay
                var timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(1);
                timer.Tick += (s, args) => {
                    timer.Stop();
                    if (Frame.CanGoBack)
                    {
                        Frame.GoBack();
                    }
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save failed: {ex.Message}");
                PerformanceStatsText.Text = "Save failed!";
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            _statsTimer?.Stop();
            _gpuEngine?.Dispose();
            base.OnNavigatedFrom(e);
        }
    }
}