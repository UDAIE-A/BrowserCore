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
    /// Clean MainPage with CLRNET-enhanced browsing and YouTube video optimization
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private IBrowserEngine _engine;
        private readonly string _defaultUrl = "https://www.google.com";
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
            StartPersistentVideoMonitoring();
        }

        private async Task InitializeBrowserAsync()
        {
            try
            {
                StatusText.Text = "Initializing CLRNET Browser...";
                
                // Initialize GPU acceleration first
                _gpuEngine = new GPUAccelerationEngine();
                _gpuEngine.GPUStateChanged += OnGPUStateChanged;
                await _gpuEngine.InitializeAsync();
                
                // Initialize CLRNET engine
                _engine = new CLRNetEngine(MainWebView);
                _engine.NavigationCompleted += OnNavigationCompleted;
                _engine.TitleChanged += OnTitleChanged;
                _engine.LoadingStateChanged += OnLoadingStateChanged;
                
                // Enable GPU acceleration
                await _gpuEngine.EnableHardwareAccelerationAsync(MainWebView);
                
                // Navigate to default page
                await NavigateToUrlAsync(_defaultUrl);
                
                StatusText.Text = "CLRNET Browser Ready";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Initialization failed: {ex.Message}";
            }
        }

        private void OnNavigationCompleted(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Navigation completed to: {MainWebView.Source}");
            UpdateGpuStatus();
            
            // Apply video optimizations for video sites
            var url = MainWebView.Source?.ToString().ToLower();
            if (url != null && (url.Contains("youtube") || url.Contains("video") || url.Contains("stream")))
            {
                _ = OptimizeForVideoContentAsync();
                _ = HandleYouTubeSpecificFixesAsync();
            }
        }

        private void OnTitleChanged(object sender, string title)
        {
            // Could update window title if needed
        }

        private void OnLoadingStateChanged(object sender, bool isLoading)
        {
            LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
            StatusText.Text = isLoading ? "Loading..." : "Ready";
        }

        private void MainWebView_NavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            StatusText.Text = "Loading...";
        }

        private async void MainWebView_NavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            
            if (args.IsSuccess)
            {
                StatusText.Text = "Ready";
                AddressBar.Text = args.Uri?.ToString() ?? AddressBar.Text;
                
                // Inject video optimization for YouTube and other media sites
                try
                {
                    var url = args.Uri?.ToString().ToLower();
                    if (url != null && (url.Contains("youtube") || url.Contains("video")))
                    {
                        // Apply both general video optimization and YouTube-specific fixes
                        await OptimizeForVideoContentAsync();
                        _ = HandleYouTubeSpecificFixesAsync();
                    }
                    
                    // Apply GPU optimizations
                    if (_gpuEngine != null)
                    {
                        await _gpuEngine.OptimizeForCurrentPageAsync(MainWebView);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Post-navigation optimization failed: {ex.Message}");
                }
            }
            else
            {
                StatusText.Text = "Navigation failed";
            }
        }

        private async Task OptimizeForVideoContentAsync()
        {
            try
            {
                // Wait a moment for page to load completely
                await Task.Delay(1500);
                
                // Enhanced video optimization script for YouTube white screen fix
                var videoOptimizationScript = @"
                    (function() {
                        console.log('CLRNET: Starting aggressive video optimization for ARM...');
                        
                        // Function to aggressively fix YouTube videos
                        function fixYouTubeVideo() {
                            // Remove ALL overlay elements that block video
                            var overlaySelectors = [
                                '.ytp-pause-overlay', '.html5-endscreen', '.ytp-gradient-bottom',
                                '.ytp-chrome-top', '.ytp-show-cards-title', '.ytp-cards-teaser',
                                '.ytp-ce-element', '.ytp-suggested-action', '.ytp-watermark',
                                '.ytp-cards-button', '.ytp-endscreen-element', '.iv-drawer',
                                '#player-unavailable', '.ytp-error', '.ytp-spinner'
                            ];
                            
                            overlaySelectors.forEach(function(selector) {
                                var elements = document.querySelectorAll(selector);
                                for(var i = 0; i < elements.length; i++) {
                                    if(elements[i]) {
                                        elements[i].style.display = 'none !important';
                                        elements[i].style.visibility = 'hidden !important';
                                        elements[i].remove();
                                    }
                                }
                            });
                            
                            // Fix video elements
                            var videos = document.querySelectorAll('video');
                            console.log('Found ' + videos.length + ' video elements');
                            
                            for(var i = 0; i < videos.length; i++) {
                                var video = videos[i];
                                if (video) {
                                    // ARM-specific video attributes
                                    video.setAttribute('playsinline', 'true');
                                    video.setAttribute('webkit-playsinline', 'true');
                                    video.setAttribute('preload', 'metadata');
                                    video.setAttribute('controls', 'true');
                                    
                                    // Ensure video visibility
                                    video.style.display = 'block !important';
                                    video.style.visibility = 'visible !important';
                                    video.style.opacity = '1 !important';
                                    video.style.background = 'black';
                                    video.style.width = '100%';
                                    video.style.height = 'auto';
                                    
                                    // Force video to be interactive
                                    video.removeAttribute('disabled');
                                    video.style.pointerEvents = 'auto';
                                    
                                    // Override click events
                                    video.onclick = function(e) {
                                        e.stopPropagation();
                                        if(this.paused) {
                                            this.play();
                                        } else {
                                            this.pause();
                                        }
                                        return false;
                                    };
                                    
                                    console.log('Video element ' + i + ' optimized');
                                }
                            }
                            
                            // Fix YouTube player containers
                            var playerContainers = document.querySelectorAll(
                                '#player, #movie_player, .html5-video-player, .video-stream'
                            );
                            
                            for(var j = 0; j < playerContainers.length; j++) {
                                var container = playerContainers[j];
                                if(container) {
                                    container.style.background = 'black !important';
                                    container.style.position = 'relative !important';
                                    container.style.display = 'block !important';
                                    container.style.visibility = 'visible !important';
                                    container.style.opacity = '1 !important';
                                    
                                    // Remove any click blockers
                                    container.style.pointerEvents = 'auto';
                                    container.onclick = function(e) {
                                        var video = this.querySelector('video');
                                        if(video) {
                                            if(video.paused) {
                                                video.play();
                                            } else {
                                                video.pause();
                                            }
                                        }
                                    };
                                }
                            }
                            
                            // Force remove white overlays and error states
                            var whiteElements = document.querySelectorAll(
                                '.ytp-error-content, .ytp-error-content-wrap, [style*=""background: white""]'
                            );
                            for(var k = 0; k < whiteElements.length; k++) {
                                if(whiteElements[k]) {
                                    whiteElements[k].style.display = 'none !important';
                                }
                            }
                        }
                        
                        // Apply fixes immediately
                        fixYouTubeVideo();
                        
                        // Set up persistent monitoring for dynamic content
                        var observer = new MutationObserver(function(mutations) {
                            var shouldFix = false;
                            mutations.forEach(function(mutation) {
                                if(mutation.addedNodes.length > 0) {
                                    shouldFix = true;
                                }
                            });
                            if(shouldFix) {
                                setTimeout(fixYouTubeVideo, 500);
                            }
                        });
                        
                        if(document.body) {
                            observer.observe(document.body, {
                                childList: true,
                                subtree: true
                            });
                        }
                        
                        // Also apply fixes on various events
                        setTimeout(fixYouTubeVideo, 2000);
                        setTimeout(fixYouTubeVideo, 5000);
                        setTimeout(fixYouTubeVideo, 10000);
                        
                        console.log('CLRNET: Aggressive video optimization complete for ARM');
                        return 'Advanced video optimization applied';
                    })();
                ";
                
                await _engine?.ExecuteScriptAsync(videoOptimizationScript);
                System.Diagnostics.Debug.WriteLine("Advanced video optimization script injected for ARM");
                
                // Apply the optimization multiple times to catch dynamic content
                await Task.Delay(3000);
                await _engine?.ExecuteScriptAsync(videoOptimizationScript);
                System.Diagnostics.Debug.WriteLine("Video optimization re-applied");
                
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Video script injection failed: {ex.Message}");
            }
        }

        private async Task HandleYouTubeSpecificFixesAsync()
        {
            try
            {
                await Task.Delay(2000); // Wait for YouTube to load
                
                var youtubeFixScript = @"
                    (function() {
                        console.log('CLRNET: Applying YouTube-specific white screen fixes...');
                        
                        // Function to bypass YouTube's mobile restrictions
                        function bypassMobileRestrictions() {
                            // Override user agent detection
                            if(window.navigator) {
                                Object.defineProperty(navigator, 'userAgent', {
                                    get: function() { return 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36'; }
                                });
                            }
                            
                            // Force desktop mode for better video compatibility
                            var metaViewport = document.querySelector('meta[name=viewport]');
                            if(metaViewport) {
                                metaViewport.setAttribute('content', 'width=1024');
                            }
                            
                            // Remove mobile-specific CSS that might hide video
                            var style = document.createElement('style');
                            style.textContent = `
                                .ytp-error-content-wrap { display: none !important; }
                                .ytp-error { display: none !important; }
                                #player-unavailable { display: none !important; }
                                .ytp-spinner { display: none !important; }
                                video { 
                                    display: block !important; 
                                    visibility: visible !important; 
                                    opacity: 1 !important;
                                    background: black !important;
                                }
                                .html5-video-player {
                                    background: black !important;
                                    display: block !important;
                                }
                            `;
                            document.head.appendChild(style);
                        }
                        
                        // Function to handle iframe video players
                        function fixIframeVideos() {
                            var iframes = document.querySelectorAll('iframe');
                            for(var i = 0; i < iframes.length; i++) {
                                try {
                                    var iframe = iframes[i];
                                    if(iframe.src && iframe.src.includes('youtube')) {
                                        // Ensure iframe is visible and functional
                                        iframe.style.display = 'block';
                                        iframe.style.visibility = 'visible';
                                        iframe.style.opacity = '1';
                                        iframe.style.background = 'black';
                                        
                                        // Try to access iframe content (if same-origin)
                                        try {
                                            var iframeDoc = iframe.contentDocument || iframe.contentWindow.document;
                                            if(iframeDoc) {
                                                var iframeVideos = iframeDoc.querySelectorAll('video');
                                                for(var j = 0; j < iframeVideos.length; j++) {
                                                    var video = iframeVideos[j];
                                                    video.setAttribute('playsinline', 'true');
                                                    video.style.display = 'block';
                                                    video.onclick = function() { this.play(); };
                                                }
                                            }
                                        } catch(e) {
                                            console.log('Cross-origin iframe, cannot access content');
                                        }
                                    }
                                } catch(e) {
                                    console.log('Error processing iframe:', e);
                                }
                            }
                        }
                        
                        // Function to override YouTube's click handlers
                        function overrideClickHandlers() {
                            var clickTargets = document.querySelectorAll(
                                '#player, #movie_player, .html5-video-player, .ytp-large-play-button'
                            );
                            
                            for(var i = 0; i < clickTargets.length; i++) {
                                var target = clickTargets[i];
                                if(target) {
                                    // Remove existing event listeners
                                    var newTarget = target.cloneNode(true);
                                    target.parentNode.replaceChild(newTarget, target);
                                    
                                    // Add our own click handler
                                    newTarget.addEventListener('click', function(e) {
                                        e.preventDefault();
                                        e.stopPropagation();
                                        
                                        var video = this.querySelector('video') || 
                                                   document.querySelector('video') ||
                                                   this.closest('.html5-video-player').querySelector('video');
                                        
                                        if(video) {
                                            if(video.paused) {
                                                video.play();
                                                console.log('Video play triggered');
                                            } else {
                                                video.pause();
                                                console.log('Video pause triggered');
                                            }
                                        }
                                        return false;
                                    }, true);
                                }
                            }
                        }
                        
                        // Apply all fixes
                        bypassMobileRestrictions();
                        fixIframeVideos();
                        overrideClickHandlers();
                        
                        // Monitor for dynamically added content
                        var ytObserver = new MutationObserver(function(mutations) {
                            var hasNewContent = false;
                            mutations.forEach(function(mutation) {
                                if(mutation.addedNodes.length > 0) {
                                    hasNewContent = true;
                                }
                            });
                            
                            if(hasNewContent) {
                                setTimeout(function() {
                                    fixIframeVideos();
                                    overrideClickHandlers();
                                }, 1000);
                            }
                        });
                        
                        if(document.body) {
                            ytObserver.observe(document.body, {
                                childList: true,
                                subtree: true
                            });
                        }
                        
                        console.log('CLRNET: YouTube-specific fixes applied');
                        return 'YouTube white screen fixes complete';
                    })();
                ";
                
                await _engine?.ExecuteScriptAsync(youtubeFixScript);
                System.Diagnostics.Debug.WriteLine("YouTube-specific fixes applied");
                
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"YouTube fixes failed: {ex.Message}");
            }
        }

        private void StartPersistentVideoMonitoring()
        {
            try
            {
                var monitoringTimer = new DispatcherTimer();
                monitoringTimer.Interval = TimeSpan.FromSeconds(5);
                monitoringTimer.Tick += async (s, e) =>
                {
                    try
                    {
                        var url = MainWebView.Source?.ToString().ToLower();
                        if (url != null && (url.Contains("youtube") || url.Contains("video")))
                        {
                            var persistentVideoScript = @"
                                (function() {
                                    // Continuous video monitoring for YouTube white screen fix
                                    var videos = document.querySelectorAll('video');
                                    var fixedCount = 0;
                                    
                                    for(var i = 0; i < videos.length; i++) {
                                        var video = videos[i];
                                        if(video && video.style.display !== 'block') {
                                            // Force video visibility
                                            video.style.display = 'block !important';
                                            video.style.visibility = 'visible !important';
                                            video.style.opacity = '1 !important';
                                            video.style.background = 'black';
                                            video.setAttribute('playsinline', 'true');
                                            fixedCount++;
                                        }
                                    }
                                    
                                    // Remove any white overlays or error messages
                                    var errorElements = document.querySelectorAll(
                                        '.ytp-error, .ytp-error-content, #player-unavailable, .ytp-spinner'
                                    );
                                    for(var j = 0; j < errorElements.length; j++) {
                                        if(errorElements[j]) {
                                            errorElements[j].style.display = 'none !important';
                                        }
                                    }
                                    
                                    // Ensure player containers are visible
                                    var players = document.querySelectorAll('#player, .html5-video-player');
                                    for(var k = 0; k < players.length; k++) {
                                        if(players[k]) {
                                            players[k].style.background = 'black !important';
                                            players[k].style.display = 'block !important';
                                        }
                                    }
                                    
                                    return 'Persistent monitoring: Fixed ' + fixedCount + ' videos';
                                })();
                            ";
                            
                            await _engine?.ExecuteScriptAsync(persistentVideoScript);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Persistent video monitoring error: {ex.Message}");
                    }
                };
                
                monitoringTimer.Start();
                System.Diagnostics.Debug.WriteLine("Persistent video monitoring started");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to start video monitoring: {ex.Message}");
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainWebView.CanGoBack)
            {
                MainWebView.GoBack();
            }
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainWebView.CanGoForward)
            {
                MainWebView.GoForward();
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            MainWebView.Refresh();
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

                await _engine?.NavigateAsync(new Uri(url));
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
            _performanceTimer.Tick += async (s, e) =>
            {
                try
                {
                    if (_engine != null)
                    {
                        var metrics = await _engine.GetPerformanceMetricsAsync();
                        // Could display performance info if UI elements exist
                    }
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
                StatusText.Text = status;
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
            _ = NavigateToUrlAsync(_defaultUrl);
        }
    }
}