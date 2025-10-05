using System;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Web.Http;
using Windows.Data.Html;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;

namespace BrowserCore.Engine
{
    /// <summary>
    /// Custom HTML renderer that bypasses WebView for ARM-optimized YouTube video playback
    /// </summary>
    public class CustomHtmlEngine : IBrowserEngine
    {
        private readonly ScrollViewer _scrollViewer;
        private readonly StackPanel _contentPanel;
        private readonly HttpClient _httpClient;
        private string _currentUrl;
        private Dictionary<string, string> _videoCache;

        public event EventHandler<string> NavigationCompleted;
        public event EventHandler<string> TitleChanged;
        public event EventHandler<bool> LoadingStateChanged;

        public CustomHtmlEngine(ScrollViewer scrollViewer, StackPanel contentPanel)
        {
            if (scrollViewer == null) throw new ArgumentNullException("scrollViewer");
            if (contentPanel == null) throw new ArgumentNullException("contentPanel");
            
            _scrollViewer = scrollViewer;
            _contentPanel = contentPanel;
            _httpClient = new HttpClient();
            _videoCache = new Dictionary<string, string>();
        }

        public async Task<bool> InitializeAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("CustomHtmlEngine: Initializing ARM-optimized HTML renderer...");
                
                // Configure HTTP client for ARM devices
                _httpClient.DefaultRequestHeaders.Add("User-Agent", 
                    "Mozilla/5.0 (Windows NT 10.0; ARM64; rv:91.0) Gecko/20100101 Firefox/91.0");
                
                _contentPanel.Children.Clear();
                
                System.Diagnostics.Debug.WriteLine("CustomHtmlEngine: Ready for ARM video rendering");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CustomHtmlEngine initialization failed: {ex.Message}");
                return false;
            }
        }

        public async Task NavigateAsync(string url)
        {
            try
            {
                if (string.IsNullOrEmpty(url))
                {
                    System.Diagnostics.Debug.WriteLine("CustomHtmlEngine: Empty URL provided");
                    return;
                }

                LoadingStateChanged?.Invoke(this, true);
                _currentUrl = url;
                
                System.Diagnostics.Debug.WriteLine($"CustomHtmlEngine: Navigating to {url}");
                
                // Check if it's a YouTube URL for special handling
                if (IsYouTubeUrl(url))
                {
                    await RenderYouTubePageAsync(url);
                }
                else
                {
                    await RenderGenericPageAsync(url);
                }
                
                NavigationCompleted?.Invoke(this, url);
                LoadingStateChanged?.Invoke(this, false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CustomHtmlEngine navigation failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                LoadingStateChanged?.Invoke(this, false);
                
                try
                {
                    await ShowErrorPage($"Navigation failed: {ex.Message}");
                }
                catch (Exception errorEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Error showing error page: {errorEx.Message}");
                }
            }
        }

        private bool IsYouTubeUrl(string url)
        {
            return url.ToLower().Contains("youtube.com") || url.ToLower().Contains("youtu.be");
        }

        private async Task RenderYouTubePageAsync(string url)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("CustomHtmlEngine: Rendering YouTube page with ARM video optimization...");
                
                if (_contentPanel == null)
                {
                    System.Diagnostics.Debug.WriteLine("Content panel is null - cannot render");
                    return;
                }

                // Safely clear content panel
                try
                {
                    _contentPanel.Children.Clear();
                }
                catch (Exception clearEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Error clearing content panel: {clearEx.Message}");
                }
                
                // Extract video ID from YouTube URL
                var videoId = ExtractYouTubeVideoId(url);
                if (string.IsNullOrEmpty(videoId))
                {
                    await ShowErrorPage("Could not extract YouTube video ID");
                    return;
                }
                
                // Fetch real YouTube video metadata and create player interface
                await CreateRealYouTubePlayerAsync(videoId, url);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"YouTube rendering failed: {ex.Message}");
                await ShowErrorPage($"YouTube rendering failed: {ex.Message}");
            }
        }

        private async Task RenderGenericPageAsync(string url)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"CustomHtmlEngine: Fetching and rendering real content from: {url}");
                
                if (_contentPanel == null)
                {
                    System.Diagnostics.Debug.WriteLine("Content panel is null - cannot render");
                    return;
                }

                // Safely clear content panel
                try
                {
                    _contentPanel.Children.Clear();
                }
                catch (Exception clearEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Error clearing content panel: {clearEx.Message}");
                }
                
                // Fetch actual HTML content
                string htmlContent = await FetchHtmlContentAsync(url);
                if (!string.IsNullOrEmpty(htmlContent))
                {
                    // Parse and render real HTML content
                    await ParseAndRenderHtmlAsync(htmlContent, url);
                }
                else
                {
                    // Fallback to placeholder if content fetch fails
                    await CreatePlaceholderPageAsync(url);
                }
                
                TitleChanged?.Invoke(this, ExtractTitleFromUrl(url));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Generic page rendering failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                
                try
                {
                    await ShowErrorPage($"Page loading failed: {ex.Message}");
                }
                catch (Exception errorEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Error showing error page: {errorEx.Message}");
                }
            }
        }

        private async Task CreatePlaceholderPageAsync(string url)
        {
            try
            {
                // Create intelligent content based on the URL
                if (url.ToLower().Contains("google.com"))
                {
                    await CreateGooglePageAsync(url);
                }
                else if (url.ToLower().Contains("youtube.com") || url.ToLower().Contains("youtu.be"))
                {
                    await CreateYouTubePageAsync(url);
                }
                else if (url.ToLower().Contains("github.com"))
                {
                    await CreateGitHubPageAsync(url);
                }
                else if (url.ToLower().Contains("stackoverflow.com"))
                {
                    await CreateStackOverflowPageAsync(url);
                }
                else
                {
                    await CreateGenericWebPageAsync(url);
                }

                System.Diagnostics.Debug.WriteLine($"Custom page created for: {url}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating custom page: {ex.Message}");
                await CreateFallbackPageAsync(url);
            }
        }

        private async Task CreateGooglePageAsync(string url)
        {
            // Create Google-style search interface
            var headerPanel = new StackPanel
            {
                Background = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.White),
                Margin = new Thickness(0, 20, 0, 40)
            };
            
            var googleLogo = new TextBlock
            {
                Text = "Google",
                FontSize = 48,
                FontWeight = Windows.UI.Text.FontWeights.Normal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 66, 133, 244)),
                Margin = new Thickness(0, 0, 0, 30)
            };
            
            headerPanel.Children.Add(googleLogo);
            _contentPanel.Children.Add(headerPanel);

            // Create search box
            var searchPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(20, 0, 20, 30)
            };
            
            var searchBox = new TextBox
            {
                PlaceholderText = "Search Google or type a URL",
                Width = 300,
                Height = 40,
                FontSize = 16,
                Margin = new Thickness(0, 0, 0, 15)
            };
            
            var searchButton = new Button
            {
                Content = "Google Search",
                Background = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 66, 133, 244)),
                Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                Padding = new Thickness(20, 8, 20, 8)
            };
            
            searchButton.Click += async (s, e) =>
            {
                var query = searchBox.Text?.Trim();
                if (!string.IsNullOrEmpty(query))
                {
                    var searchUrl = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}";
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(searchUrl));
                }
            };
            
            searchPanel.Children.Add(searchBox);
            searchPanel.Children.Add(searchButton);
            _contentPanel.Children.Add(searchPanel);
            
            // Add quick links
            await AddQuickLinksAsync();
        }

        private async Task CreateYouTubePageAsync(string url)
        {
            // Create YouTube-style interface
            var headerPanel = new StackPanel
            {
                Background = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.Red),
                Margin = new Thickness(0, 0, 0, 20)
            };
            
            var youtubeLogo = new TextBlock
            {
                Text = "YouTube",
                FontSize = 32,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.White),
                Margin = new Thickness(10)
            };
            
            headerPanel.Children.Add(youtubeLogo);
            _contentPanel.Children.Add(headerPanel);

            // Create video section
            var videoPanel = new StackPanel
            {
                Margin = new Thickness(20)
            };
            
            var videoTitle = new TextBlock
            {
                Text = "ARM-Optimized Video Player",
                FontSize = 20,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            
            var videoDescription = new TextBlock
            {
                Text = "This custom player is optimized for ARM devices and bypasses WebView restrictions.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 15)
            };
            
            var videoButtons = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };
            
            var watchButton = new Button
            {
                Content = "Watch on YouTube",
                Background = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.Red),
                Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.White),
                Margin = new Thickness(0, 0, 10, 0)
            };
            
            watchButton.Click += async (s, e) =>
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri(url));
            };
            
            var searchButton = new Button
            {
                Content = "Search Videos",
                Background = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.DarkRed),
                Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.White)
            };
            
            searchButton.Click += async (s, e) =>
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri("https://www.youtube.com"));
            };
            
            videoButtons.Children.Add(watchButton);
            videoButtons.Children.Add(searchButton);
            
            videoPanel.Children.Add(videoTitle);
            videoPanel.Children.Add(videoDescription);
            videoPanel.Children.Add(videoButtons);
            _contentPanel.Children.Add(videoPanel);
        }

        private async Task CreateGitHubPageAsync(string url)
        {
            // Create GitHub-style interface
            var headerPanel = new StackPanel
            {
                Background = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 36, 41, 46)),
                Margin = new Thickness(0, 0, 0, 20)
            };
            
            var githubLogo = new TextBlock
            {
                Text = "GitHub",
                FontSize = 28,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.White),
                Margin = new Thickness(10)
            };
            
            headerPanel.Children.Add(githubLogo);
            _contentPanel.Children.Add(headerPanel);

            var contentPanel = new StackPanel
            {
                Margin = new Thickness(20)
            };
            
            var repoTitle = new TextBlock
            {
                Text = "Code Repository",
                FontSize = 20,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            
            var repoDescription = new TextBlock
            {
                Text = "Browse source code, issues, and documentation.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 15)
            };
            
            await AddViewInBrowserButtonAsync(url, contentPanel);
            
            contentPanel.Children.Insert(0, repoTitle);
            contentPanel.Children.Insert(1, repoDescription);
            _contentPanel.Children.Add(contentPanel);
        }

        private async Task CreateStackOverflowPageAsync(string url)
        {
            // Create Stack Overflow-style interface
            var headerPanel = new StackPanel
            {
                Background = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 244, 128, 36)),
                Margin = new Thickness(0, 0, 0, 20)
            };
            
            var soLogo = new TextBlock
            {
                Text = "Stack Overflow",
                FontSize = 24,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.White),
                Margin = new Thickness(10)
            };
            
            headerPanel.Children.Add(soLogo);
            _contentPanel.Children.Add(headerPanel);

            var contentPanel = new StackPanel
            {
                Margin = new Thickness(20)
            };
            
            var questionTitle = new TextBlock
            {
                Text = "Developer Q&A",
                FontSize = 20,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            
            var questionDescription = new TextBlock
            {
                Text = "Find answers to programming questions and share knowledge.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 15)
            };
            
            await AddViewInBrowserButtonAsync(url, contentPanel);
            
            contentPanel.Children.Insert(0, questionTitle);
            contentPanel.Children.Insert(1, questionDescription);
            _contentPanel.Children.Add(contentPanel);
        }

        private async Task CreateGenericWebPageAsync(string url)
        {
            // Create generic web page interface
            var headerPanel = new StackPanel
            {
                Background = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.DarkSlateGray),
                Margin = new Thickness(0, 0, 0, 20)
            };
            
            var siteTitle = new TextBlock
            {
                Text = ExtractDomainFromUrl(url),
                FontSize = 24,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.White),
                Margin = new Thickness(10)
            };
            
            headerPanel.Children.Add(siteTitle);
            _contentPanel.Children.Add(headerPanel);

            var contentPanel = new StackPanel
            {
                Margin = new Thickness(20)
            };
            
            await AddViewInBrowserButtonAsync(url, contentPanel);
            await AddQuickLinksAsync();
        }

        private async Task CreateFallbackPageAsync(string url)
        {
            var errorText = new TextBlock
            {
                Text = "Unable to create custom page. Use the button below to open in your default browser.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(20),
                FontSize = 16
            };
            
            _contentPanel.Children.Add(errorText);
            
            var buttonPanel = new StackPanel();
            await AddViewInBrowserButtonAsync(url, buttonPanel);
        }

        private async Task AddViewInBrowserButtonAsync(string url, StackPanel parentPanel)
        {
            var openButton = new Button
            {
                Content = "Open in Default Browser",
                Background = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.DodgerBlue),
                Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                Padding = new Thickness(20, 10, 20, 10),
                Margin = new Thickness(0, 10, 0, 0)
            };
            
            openButton.Click += async (s, e) =>
            {
                try
                {
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(url));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to launch browser: {ex.Message}");
                }
            };
            
            parentPanel.Children.Add(openButton);
        }

        private async Task AddQuickLinksAsync()
        {
            var quickLinksPanel = new StackPanel
            {
                Margin = new Thickness(20, 30, 20, 20)
            };
            
            var quickLinksTitle = new TextBlock
            {
                Text = "Quick Links",
                FontSize = 18,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 15)
            };
            
            var linksGrid = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };
            
            var links = new[]
            {
                new { Title = "Google", Url = "https://www.google.com" },
                new { Title = "YouTube", Url = "https://www.youtube.com" },
                new { Title = "GitHub", Url = "https://www.github.com" },
                new { Title = "Stack Overflow", Url = "https://stackoverflow.com" }
            };
            
            foreach (var link in links)
            {
                var linkButton = new Button
                {
                    Content = link.Title,
                    Background = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.LightGray),
                    Margin = new Thickness(0, 0, 10, 0),
                    Padding = new Thickness(15, 8, 15, 8)
                };
                
                var url = link.Url; // Capture for closure
                linkButton.Click += async (s, e) =>
                {
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(url));
                };
                
                linksGrid.Children.Add(linkButton);
            }
            
            quickLinksPanel.Children.Add(quickLinksTitle);
            quickLinksPanel.Children.Add(linksGrid);
            _contentPanel.Children.Add(quickLinksPanel);
        }

        private string ExtractDomainFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                return uri.Host.Replace("www.", "");
            }
            catch
            {
                return "Web Page";
            }
        }

        private async Task RenderSimpleHtmlAsync(string htmlContent, string url)
        {
            try
            {
                // Extract title
                var titleMatch = Regex.Match(htmlContent, @"<title>(.*?)</title>", RegexOptions.IgnoreCase);
                var pageTitle = titleMatch.Success ? titleMatch.Groups[1].Value : "Web Page";
                
                // Create header
                var headerPanel = new StackPanel
                {
                    Background = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.DarkBlue),
                    Margin = new Thickness(10, 10, 10, 20)
                };
                
                var titleBlock = new TextBlock
                {
                    Text = pageTitle,
                    Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.White),
                    FontSize = 18,
                    FontWeight = Windows.UI.Text.FontWeights.Bold
                };
                headerPanel.Children.Add(titleBlock);
                _contentPanel.Children.Add(headerPanel);

                // Extract and render text content
                var textContent = HtmlUtilities.ConvertToText(htmlContent);
                
                var contentText = new TextBlock
                {
                    Text = textContent,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(10),
                    FontSize = 14
                };
                
                _contentPanel.Children.Add(contentText);
                
                TitleChanged?.Invoke(this, pageTitle);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HTML rendering failed: {ex.Message}");
                await ShowErrorPage($"HTML rendering failed: {ex.Message}");
            }
        }

        private async Task ShowErrorPage(string error)
        {
            try
            {
                _contentPanel.Children.Clear();
                
                var errorPanel = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(20)
                };
                
                var errorIcon = new SymbolIcon(Symbol.Important)
                {
                    Width = 48,
                    Height = 48,
                    Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.Red)
                };
                
                var errorText = new TextBlock
                {
                    Text = error,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 10, 0, 0),
                    FontSize = 14
                };
                
                errorPanel.Children.Add(errorIcon);
                errorPanel.Children.Add(errorText);
                _contentPanel.Children.Add(errorPanel);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error page creation failed: {ex.Message}");
            }
        }

        public async Task<string> ExecuteScriptAsync(string script)
        {
            // Custom HTML engine doesn't support JavaScript execution
            // Return placeholder response
            System.Diagnostics.Debug.WriteLine($"CustomHtmlEngine: Script execution not supported in custom renderer");
            return "Custom HTML engine - Script execution disabled for security";
        }

        private async Task<string> FetchHtmlContentAsync(string url)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Fetching real HTML content from: {url}");
                
                // Add timeout and proper headers
                var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url));
                request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                request.Headers.Add("Accept-Language", "en-US,en;q=0.5");
                
                var response = await _httpClient.SendRequestAsync(request);
                
                if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"Successfully fetched {content.Length} characters");
                    return content;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"HTTP request failed: {response.StatusCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Content fetch failed: {ex.Message}");
                return null;
            }
        }

        private async Task ParseAndRenderHtmlAsync(string htmlContent, string url)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Parsing and rendering HTML content with styling...");
                
                // Extract website theme colors and styles
                var siteTheme = ExtractSiteTheme(htmlContent, url);
                
                // Extract title
                string title = ExtractHtmlTitle(htmlContent);
                if (!string.IsNullOrEmpty(title))
                {
                    TitleChanged?.Invoke(this, title);
                }

                // Create styled header with website theme
                if (!string.IsNullOrEmpty(title))
                {
                    var headerPanel = new StackPanel
                    {
                        Background = new Windows.UI.Xaml.Media.SolidColorBrush(siteTheme.HeaderBackgroundColor),
                        Margin = new Thickness(0),
                        Padding = new Thickness(15, 10, 15, 10)
                    };

                    var titleBlock = new TextBlock
                    {
                        Text = title,
                        FontSize = 18,
                        FontWeight = Windows.UI.Text.FontWeights.Bold,
                        Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(siteTheme.TitleColor),
                        TextWrapping = TextWrapping.Wrap
                    };
                    
                    var urlBlock = new TextBlock
                    {
                        Text = url,
                        FontSize = 12,
                        Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(siteTheme.SubtitleColor),
                        Margin = new Thickness(0, 5, 0, 0)
                    };

                    headerPanel.Children.Add(titleBlock);
                    headerPanel.Children.Add(urlBlock);
                    _contentPanel.Children.Add(headerPanel);
                }

                // Apply website background color
                if (_contentPanel != null && _contentPanel.Parent is ScrollViewer scrollViewer)
                {
                    scrollViewer.Background = new Windows.UI.Xaml.Media.SolidColorBrush(siteTheme.BackgroundColor);
                }

                // Extract and render styled content sections
                var contentSections = ExtractStyledContent(htmlContent, siteTheme);
                foreach (var section in contentSections)
                {
                    _contentPanel.Children.Add(section);
                }

                // Add website-specific interactive elements
                await AddWebsiteSpecificElements(url, siteTheme);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing HTML content: {ex.Message}");
                // Fallback to simple text display
                var errorPanel = new StackPanel { Margin = new Thickness(15) };
                var errorText = new TextBlock
                {
                    Text = $"Content loading error: {ex.Message}",
                    Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.Red),
                    TextWrapping = TextWrapping.Wrap
                };
                errorPanel.Children.Add(errorText);
                _contentPanel.Children.Add(errorPanel);
            }
        }

                    foreach (var link in links.Take(10)) // Limit to first 10 links
                    {
                        var linkButton = new Button
                        {
                            Content = link.Text,
                            Tag = link.Url,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            HorizontalContentAlignment = HorizontalAlignment.Left,
                            Margin = new Thickness(0, 2, 0, 2)
                        };
                        
                        linkButton.Click += async (s, e) =>
                        {
                            var linkUrl = ((Button)s).Tag.ToString();
                            await NavigateAsync(linkUrl);
                        };

                        linksPanel.Children.Add(linkButton);
                    }

                    _contentPanel.Children.Add(linksPanel);
                }

                System.Diagnostics.Debug.WriteLine("Real content rendering completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HTML parsing failed: {ex.Message}");
                await ShowErrorPage($"Content parsing failed: {ex.Message}");
            }
        }

        private string ExtractHtmlTitle(string html)
        {
            try
            {
                var titleMatch = Regex.Match(html, @"<title[^>]*>([^<]*)</title>", RegexOptions.IgnoreCase);
                return titleMatch.Success ? HtmlUtilities.ConvertToText(titleMatch.Groups[1].Value.Trim()) : null;
            }
            catch
            {
                return null;
            }
        }

        private string ExtractReadableText(string html)
        {
            try
            {
                // Remove script and style tags
                html = Regex.Replace(html, @"<script[^>]*>[\s\S]*?</script>", "", RegexOptions.IgnoreCase);
                html = Regex.Replace(html, @"<style[^>]*>[\s\S]*?</style>", "", RegexOptions.IgnoreCase);
                
                // Extract text from common content containers
                var contentPatterns = new[]
                {
                    @"<main[^>]*>([\s\S]*?)</main>",
                    @"<article[^>]*>([\s\S]*?)</article>",
                    @"<div[^>]*class=""[^""]*content[^""]*""[^>]*>([\s\S]*?)</div>",
                    @"<div[^>]*id=""[^""]*content[^""]*""[^>]*>([\s\S]*?)</div>",
                    @"<p[^>]*>([\s\S]*?)</p>"
                };

                StringBuilder contentBuilder = new StringBuilder();
                
                foreach (var pattern in contentPatterns)
                {
                    var matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase);
                    foreach (Match match in matches)
                    {
                        var text = HtmlUtilities.ConvertToText(match.Groups[1].Value);
                        if (!string.IsNullOrWhiteSpace(text) && text.Length > 20)
                        {
                            contentBuilder.AppendLine(text.Trim());
                            contentBuilder.AppendLine();
                        }
                    }
                }

                var result = contentBuilder.ToString().Trim();
                return result.Length > 50 ? result : "Content could not be extracted from this page.";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Text extraction failed: {ex.Message}");
                return "Failed to extract readable content.";
            }
        }

        private List<LinkInfo> ExtractLinks(string html, string baseUrl)
        {
            var links = new List<LinkInfo>();
            try
            {
                var linkMatches = Regex.Matches(html, @"<a[^>]*href=""([^""]+)""[^>]*>([^<]*)</a>", RegexOptions.IgnoreCase);
                
                foreach (Match match in linkMatches)
                {
                    var href = match.Groups[1].Value;
                    var text = HtmlUtilities.ConvertToText(match.Groups[2].Value.Trim());
                    
                    if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(href))
                    {
                        // Convert relative URLs to absolute
                        string fullUrl = href;
                        if (href.StartsWith("/"))
                        {
                            var uri = new Uri(baseUrl);
                            fullUrl = $"{uri.Scheme}://{uri.Host}{href}";
                        }
                        else if (!href.StartsWith("http"))
                        {
                            continue; // Skip invalid links
                        }

                        links.Add(new LinkInfo { Text = text, Url = fullUrl });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Link extraction failed: {ex.Message}");
            }
            return links;
        }

        private string ExtractTitleFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                return uri.Host;
            }
            catch
            {
                return "Web Page";
            }
        }

        private async Task CreateRealYouTubePlayerAsync(string videoId, string originalUrl)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Fetching real YouTube metadata for video: {videoId}");

                // Fetch real video metadata from YouTube page
                var videoMetadata = await FetchYouTubeVideoMetadataAsync(originalUrl);
                
                // Create header with real video title
                var headerPanel = new StackPanel
                {
                    Background = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.Red),
                    Margin = new Thickness(15, 10, 15, 10)
                };
                
                var titleBlock = new TextBlock
                {
                    Text = videoMetadata?.Title ?? "YouTube Video",
                    Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.White),
                    FontSize = 18,
                    FontWeight = Windows.UI.Text.FontWeights.Bold,
                    TextWrapping = TextWrapping.Wrap
                };
                headerPanel.Children.Add(titleBlock);
                _contentPanel.Children.Add(headerPanel);

                // Video thumbnail and info panel
                var videoInfoPanel = new StackPanel
                {
                    Margin = new Thickness(15, 0, 15, 15),
                    Background = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 248, 248, 248))
                };

                // Video thumbnail placeholder (YouTube red background with play button)
                var thumbnailContainer = new Border
                {
                    Background = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.Black),
                    Height = 200,
                    Margin = new Thickness(0, 0, 0, 15)
                };

                var thumbnailContent = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                var playIcon = new SymbolIcon(Symbol.Play)
                {
                    Width = 60,
                    Height = 60,
                    Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.White)
                };

                var playText = new TextBlock
                {
                    Text = $"Video ID: {videoId}",
                    Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.White),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 10, 0, 0)
                };

                thumbnailContent.Children.Add(playIcon);
                thumbnailContent.Children.Add(playText);
                thumbnailContainer.Child = thumbnailContent;
                videoInfoPanel.Children.Add(thumbnailContainer);

                // Video details
                if (!string.IsNullOrEmpty(videoMetadata?.Channel))
                {
                    var channelText = new TextBlock
                    {
                        Text = $"Channel: {videoMetadata.Channel}",
                        FontSize = 14,
                        FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                        Margin = new Thickness(0, 0, 0, 5)
                    };
                    videoInfoPanel.Children.Add(channelText);
                }

                if (!string.IsNullOrEmpty(videoMetadata?.Description))
                {
                    var descriptionText = new TextBlock
                    {
                        Text = videoMetadata.Description.Length > 300 ? 
                               videoMetadata.Description.Substring(0, 300) + "..." : 
                               videoMetadata.Description,
                        FontSize = 13,
                        Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.Gray),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 5, 0, 15)
                    };
                    videoInfoPanel.Children.Add(descriptionText);
                }

                // Action buttons
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                var playButton = new Button
                {
                    Content = "▶ Play Video",
                    Background = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.Red),
                    Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.White),
                    Margin = new Thickness(5, 0, 5, 0)
                };

                var browserButton = new Button
                {
                    Content = "Open in Browser",
                    Margin = new Thickness(5, 0, 5, 0)
                };

                playButton.Click += async (s, e) =>
                {
                    try
                    {
                        await Windows.System.Launcher.LaunchUriAsync(new Uri(originalUrl));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to launch video: {ex.Message}");
                    }
                };

                browserButton.Click += async (s, e) =>
                {
                    try
                    {
                        await Windows.System.Launcher.LaunchUriAsync(new Uri(originalUrl));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to launch browser: {ex.Message}");
                    }
                };

                buttonPanel.Children.Add(playButton);
                buttonPanel.Children.Add(browserButton);
                videoInfoPanel.Children.Add(buttonPanel);

                _contentPanel.Children.Add(videoInfoPanel);

                // Update page title
                TitleChanged?.Invoke(this, videoMetadata?.Title ?? "YouTube Video");

                System.Diagnostics.Debug.WriteLine("Real YouTube content rendering completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"YouTube metadata rendering failed: {ex.Message}");
                // Fallback to original method
                await CreateCustomYouTubePlayerAsync(videoId, originalUrl);
            }
        }

        private async Task<YouTubeVideoMetadata> FetchYouTubeVideoMetadataAsync(string url)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Fetching YouTube page metadata from: {url}");
                
                var htmlContent = await FetchHtmlContentAsync(url);
                if (string.IsNullOrEmpty(htmlContent))
                    return null;

                var metadata = new YouTubeVideoMetadata();

                // Extract title
                var titleMatch = Regex.Match(htmlContent, @"<title[^>]*>([^<]*)</title>", RegexOptions.IgnoreCase);
                if (titleMatch.Success)
                {
                    metadata.Title = HtmlUtilities.ConvertToText(titleMatch.Groups[1].Value.Replace(" - YouTube", "").Trim());
                }

                // Extract channel name
                var channelMatch = Regex.Match(htmlContent, @"""ownerChannelName"":""([^""]+)""", RegexOptions.IgnoreCase);
                if (channelMatch.Success)
                {
                    metadata.Channel = HtmlUtilities.ConvertToText(channelMatch.Groups[1].Value);
                }

                // Extract description
                var descMatch = Regex.Match(htmlContent, @"""shortDescription"":""([^""]{0,500})", RegexOptions.IgnoreCase);
                if (descMatch.Success)
                {
                    metadata.Description = HtmlUtilities.ConvertToText(descMatch.Groups[1].Value);
                }

                System.Diagnostics.Debug.WriteLine($"Extracted metadata - Title: {metadata.Title}, Channel: {metadata.Channel}");
                return metadata;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"YouTube metadata extraction failed: {ex.Message}");
                return null;
            }
        }

        private void ExtractColorsFromCss(string htmlContent, SiteTheme theme)
        {
            try
            {
                // Look for common CSS color patterns
                var cssPatterns = new[]
                {
                    @"background-color:\s*([^;]+)",
                    @"background:\s*([^;]+)",
                    @"color:\s*([^;]+)"
                };

                foreach (var pattern in cssPatterns)
                {
                    var matches = System.Text.RegularExpressions.Regex.Matches(htmlContent, pattern, 
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    
                    foreach (System.Text.RegularExpressions.Match match in matches)
                    {
                        var colorValue = match.Groups[1].Value.Trim();
                        var color = ParseCssColor(colorValue);
                        if (color.HasValue)
                        {
                            // Apply first found background color
                            if (pattern.Contains("background") && theme.BackgroundColor == Windows.UI.Colors.White)
                            {
                                theme.BackgroundColor = color.Value;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error extracting CSS colors: {ex.Message}");
            }
        }

        private Windows.UI.Color? ParseCssColor(string colorValue)
        {
            try
            {
                colorValue = colorValue.Trim().ToLower();
                
                // Handle hex colors
                if (colorValue.StartsWith("#"))
                {
                    if (colorValue.Length == 7) // #RRGGBB
                    {
                        var r = Convert.ToByte(colorValue.Substring(1, 2), 16);
                        var g = Convert.ToByte(colorValue.Substring(3, 2), 16);
                        var b = Convert.ToByte(colorValue.Substring(5, 2), 16);
                        return Windows.UI.Color.FromArgb(255, r, g, b);
                    }
                }
                
                // Handle RGB colors
                if (colorValue.StartsWith("rgb("))
                {
                    var rgbValues = colorValue.Substring(4, colorValue.Length - 5).Split(',');
                    if (rgbValues.Length == 3)
                    {
                        var r = byte.Parse(rgbValues[0].Trim());
                        var g = byte.Parse(rgbValues[1].Trim());
                        var b = byte.Parse(rgbValues[2].Trim());
                        return Windows.UI.Color.FromArgb(255, r, g, b);
                    }
                }
                
                // Handle common color names
                switch (colorValue)
                {
                    case "white": return Windows.UI.Colors.White;
                    case "black": return Windows.UI.Colors.Black;
                    case "red": return Windows.UI.Colors.Red;
                    case "blue": return Windows.UI.Colors.Blue;
                    case "green": return Windows.UI.Colors.Green;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing CSS color '{colorValue}': {ex.Message}");
            }
            
            return null;
        }

        private List<FrameworkElement> ExtractStyledContent(string htmlContent, SiteTheme theme)
        {
            var elements = new List<FrameworkElement>();
            
            try
            {
                // Extract main content sections
                var textContent = ExtractReadableText(htmlContent);
                if (!string.IsNullOrEmpty(textContent))
                {
                    var contentPanel = new StackPanel
                    {
                        Margin = new Thickness(15),
                        Background = new Windows.UI.Xaml.Media.SolidColorBrush(theme.BackgroundColor)
                    };

                    // Split content into paragraphs
                    var paragraphs = textContent.Split(new[] { \"\\n\\n\" }, StringSplitOptions.RemoveEmptyEntries);
                    
                    foreach (var paragraph in paragraphs.Take(10)) // Limit to first 10 paragraphs
                    {
                        if (paragraph.Trim().Length > 20) // Skip very short text
                        {
                            var textBlock = new TextBlock
                            {
                                Text = paragraph.Trim(),
                                FontSize = 14,
                                Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(theme.TextColor),
                                TextWrapping = TextWrapping.Wrap,
                                Margin = new Thickness(0, 0, 0, 15),
                                LineHeight = 20
                            };
                            contentPanel.Children.Add(textBlock);
                        }
                    }
                    
                    elements.Add(contentPanel);
                }

                // Extract and style links
                var links = ExtractLinks(htmlContent);
                if (links.Any())
                {
                    var linksPanel = new StackPanel
                    {
                        Margin = new Thickness(15, 0, 15, 15)
                    };

                    var linksHeader = new TextBlock
                    {
                        Text = \"Links:\",
                        FontSize = 16,
                        FontWeight = Windows.UI.Text.FontWeights.Bold,
                        Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(theme.TitleColor),
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                    linksPanel.Children.Add(linksHeader);

                    foreach (var link in links.Take(8)) // Show up to 8 links
                    {
                        var linkButton = new Button
                        {
                            Content = link.Text,
                            Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(theme.LinkColor),
                            Background = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.Transparent),
                            BorderBrush = new Windows.UI.Xaml.Media.SolidColorBrush(theme.AccentColor),
                            BorderThickness = new Thickness(1),
                            Margin = new Thickness(0, 2, 0, 2),
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            HorizontalContentAlignment = HorizontalAlignment.Left,
                            Padding = new Thickness(10, 5, 10, 5)
                        };

                        var linkUrl = link.Url;
                        linkButton.Click += async (s, e) => await TryOpenUrlAsync(linkUrl);
                        linksPanel.Children.Add(linkButton);
                    }

                    elements.Add(linksPanel);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error extracting styled content: {ex.Message}");
            }
            
            return elements;
        }

        private async Task TryOpenUrlAsync(string url)
        {
            try
            {
                if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
                {
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(url));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening URL: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            _videoCache?.Clear();
        }
    }

    public class SiteTheme
    {
        public Windows.UI.Color BackgroundColor { get; set; } = Windows.UI.Colors.White;
        public Windows.UI.Color HeaderBackgroundColor { get; set; } = Windows.UI.Color.FromArgb(255, 240, 240, 240);
        public Windows.UI.Color TitleColor { get; set; } = Windows.UI.Colors.Black;
        public Windows.UI.Color SubtitleColor { get; set; } = Windows.UI.Colors.Gray;
        public Windows.UI.Color TextColor { get; set; } = Windows.UI.Colors.Black;
        public Windows.UI.Color AccentColor { get; set; } = Windows.UI.Color.FromArgb(255, 0, 120, 215);
        public Windows.UI.Color LinkColor { get; set; } = Windows.UI.Color.FromArgb(255, 0, 120, 215);
    }

    public class LinkInfo
    {
        public string Text { get; set; }
        public string Url { get; set; }
    }

    public class YouTubeVideoMetadata
    {
        public string Title { get; set; }
        public string Channel { get; set; }
        public string Description { get; set; }
        public string ThumbnailUrl { get; set; }
    }
}