using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace BrowserCore.Engine
{
    // Phase 1: build-stable CustomHtmlEngine with basic HTTP fetch + simple HTML rendering and images (no WebView)
    public class CustomHtmlEngine : IBrowserEngine, IDisposable
    {
        public event EventHandler<string> NavigationCompleted;
        public event EventHandler<string> TitleChanged;
        public event EventHandler<bool> LoadingStateChanged;

        private ScrollViewer _scroll;
        private StackPanel _panel;
        private HttpClient _http;
        private CookieContainer _cookies;
        private Uri _currentUri;
        private readonly Dictionary<string, byte[]> _imageCache = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        private class BackgroundSpec
        {
            public string Url { get; set; }
            public Stretch Stretch { get; set; } = Stretch.UniformToFill; // cover
            public AlignmentX AlignX { get; set; } = AlignmentX.Center;
            public AlignmentY AlignY { get; set; } = AlignmentY.Center;
            public double Height { get; set; } = 360; // default visual height (bigger hero)
            public bool NoRepeat { get; set; } = false; // background-repeat: no-repeat
        }

        public void Initialize(ScrollViewer scrollViewer, StackPanel contentPanel)
        {
            _scroll = scrollViewer;
            _panel = contentPanel;
        }

        public Task<bool> InitializeAsync()
        {
            var handler = new HttpClientHandler();
            try
            {
                _cookies = new CookieContainer();
                handler.UseCookies = true;
                handler.CookieContainer = _cookies;
            }
            catch { }
            try { handler.AllowAutoRedirect = true; } catch { }
            _http = new HttpClient(handler);
            try
            {
                var h = _http.DefaultRequestHeaders;
                h.UserAgent.ParseAdd("Mozilla/5.0 (Windows Phone 8.1; ARM) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/70.0 Mobile Safari/537.36");
                h.Accept.ParseAdd("text/html");
                h.Accept.ParseAdd("application/xhtml+xml");
                h.Accept.ParseAdd("application/xml; q=0.9");
                h.Accept.ParseAdd("*/*; q=0.8");
                h.AcceptLanguage.ParseAdd("en-US");
                h.AcceptLanguage.ParseAdd("en; q=0.9");
                h.AcceptEncoding.ParseAdd("gzip");
                h.AcceptEncoding.ParseAdd("deflate");
            }
            catch { }
            _http.Timeout = TimeSpan.FromSeconds(30);
            return Task.FromResult(true);
        }

        public async Task NavigateAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            url = NormalizeUrl(url);
            try { _currentUri = new Uri(url); } catch { _currentUri = null; }

            await ShowLoadingAsync(true, "Loading...");

            string html = null;
            try
            {
                if (url.StartsWith("ms-appx", StringComparison.OrdinalIgnoreCase))
                {
                    html = await LoadLocalAsync(url);
                }
                else
                {
                    html = await FetchHtmlWithFallbackAsync(url);
                }
            }
            catch (Exception ex)
            {
                await ReplaceContentAsync(new TextBlock { Text = "Network error: " + ex.Message, Foreground = new SolidColorBrush(Windows.UI.Colors.Red) });
            }

            // Gate-resolution: meta refresh, AMP/alternate, inline cookie set + retry, query variants
            try
            {
                var gate = await TryResolveGatesAsync(html, url);
                if (gate != null)
                {
                    if (!string.IsNullOrEmpty(gate.Item2))
                    {
                        url = gate.Item2;
                        try { _currentUri = new Uri(url); } catch { _currentUri = null; }
                    }
                    if (!string.IsNullOrEmpty(gate.Item1))
                    {
                        html = gate.Item1;
                    }
                }
            }
            catch { }

            if (string.IsNullOrEmpty(html))
            {
                await ShowLoadingAsync(false, "Ready");
                if (NavigationCompleted != null) NavigationCompleted(this, url);
                return;
            }

            // Title
            var title = TryExtractTitle(html);
            if (string.IsNullOrEmpty(title)) title = _currentUri != null ? _currentUri.Host : url;
            if (TitleChanged != null) { try { TitleChanged(this, title); } catch { } }

            // Build simple content with title, text, background visuals, and images
            var contentText = TryExtractVisibleText(html);
            var root = new StackPanel();
            // Always add a small diagnostic line to avoid completely blank UI if parsing fails
            root.Children.Add(new TextBlock { Text = ($"URL: {(_currentUri != null ? _currentUri.ToString() : url)}  | HTML: {html.Length} bytes"), FontSize = 12, Foreground = new SolidColorBrush(Windows.UI.Colors.Gray), Margin = new Thickness(0,0,0,4) });
            root.Children.Add(new TextBlock { Text = title, FontSize = 22, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0,0,0,6) });

            int visualCount = 0;
            foreach (var bg in ExtractBackgroundSpecs(html, _currentUri))
            {
                var border = await CreateBackgroundVisualAsync(bg);
                if (border != null) { root.Children.Add(border); visualCount++; }
            }
            foreach (var img in ExtractImageUrls(html, _currentUri))
            {
                var image = await CreateImageElementAsync(img);
                if (image != null) { root.Children.Add(image); visualCount++; }
            }

            int readerChildren = 0;
            if (!string.IsNullOrWhiteSpace(contentText))
            {
                root.Children.Add(new TextBlock { Text = contentText, TextWrapping = TextWrapping.Wrap });
            }
            else if (visualCount == 0)
            {
                // Try reader mode: article/main
                var reader = await TryBuildReaderViewAsync(html, _currentUri);
                if (reader == null)
                {
                    // role="main" / #content / .content/.main containers
                    reader = await TryBuildRoleMainReaderAsync(html, _currentUri);
                }
                if (reader == null)
                {
                    // Try largest-text block fallback
                    reader = await TryBuildLargestBlockReaderAsync(html, _currentUri);
                }
                if (reader == null)
                {
                    // Try noscript content
                    reader = await TryBuildNoscriptReaderAsync(html, _currentUri);
                }
                if (reader == null)
                {
                    // Try meta preview and link directory for quick navigation
                    reader = await TryBuildMetaAndLinksAsync(html, _currentUri);
                }
                if (reader != null)
                {
                    readerChildren = reader.Children != null ? reader.Children.Count : 0;
                    root.Children.Add(reader);
                }
                else
                {
                    // Fallback if nothing visible: show a small HTML snippet for diagnostics
                    var snippet = html.Length > 1500 ? html.Substring(0, 1500) + "..." : html;
                    root.Children.Add(new TextBlock
                    {
                        Text = "No visible content detected. HTML snippet:\n\n" + snippet,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = new SolidColorBrush(Windows.UI.Colors.Gray)
                    });
                }
            }
            // Add a tiny summary line for diagnostics
            root.Children.Add(new TextBlock { Text = $"diag: imgs={ExtractImageUrls(html, _currentUri).Count} bg={ExtractBackgroundSpecs(html, _currentUri).Count} textLen={(contentText ?? "").Length} readerChildren={readerChildren}", FontSize = 11, Foreground = new SolidColorBrush(Windows.UI.Colors.Gray) });

            await ReplaceContentAsync(root);
            await ShowLoadingAsync(false, "Ready");
            if (NavigationCompleted != null) NavigationCompleted(this, url);
        }

        public async Task<string> ExecuteScriptAsync(string script)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(script)) return string.Empty;
                // Minimal pattern: fetch('url') or fetch("url")
                var m = Regex.Match(script, "fetch\\s*\\(\\s*['\"]([^'\"]+)['\"]", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    var url = m.Groups[1].Value;
                    var body = await HttpGetAsync(url);
                    return body ?? string.Empty;
                }

                // Minimal XMLHttpRequest pattern: new XMLHttpRequest(); open('GET','url'); send();
                var xm = Regex.Match(script, "open\\s*\\(\\s*['\"](GET|POST)['\"]\\s*,\\s*['\"]([^'\"]+)['\"]", RegexOptions.IgnoreCase);
                if (xm.Success && script.IndexOf("send(", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var method = xm.Groups[1].Value.ToUpperInvariant();
                    var url2 = xm.Groups[2].Value;
                    if (method == "GET")
                    {
                        var body2 = await HttpGetAsync(url2);
                        return body2 ?? string.Empty;
                    }
                }
            }
            catch { }
            return string.Empty;
        }

        public Task<string> TestPhase1APIs() { return Task.FromResult("OK"); }
        public Task<string> TestPhase2APIs() { return Task.FromResult("OK"); }

        public void Dispose()
        {
            try { if (_http != null) _http.Dispose(); } catch { }
        }

        // UI helpers
        private async Task ReplaceContentAsync(UIElement element)
        {
            if (_panel == null) return;
            await RunOnUIThread(async () =>
            {
                _panel.Children.Clear();
                _panel.Children.Add(element);
                await Task.FromResult(0);
            });
        }

        private async Task ShowLoadingAsync(bool isLoading, string status)
        {
            try { if (LoadingStateChanged != null) LoadingStateChanged(this, isLoading); } catch { }
            await Task.Yield();
        }

        private async Task RunOnUIThread(DispatchedHandler action)
        {
            var dispatcher = Window.Current != null ? Window.Current.Dispatcher : null;
            if (dispatcher != null && !dispatcher.HasThreadAccess)
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, action);
            else
                action();
        }

        // Network helpers
        private string NormalizeUrl(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;
            if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return input;
            if (input.StartsWith("ms-appx", StringComparison.OrdinalIgnoreCase)) return input;
            return "https://" + input.Trim();
        }

        private Encoding GetEncodingFromContentType(string charset)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(charset)) return null;
                var sanitized = charset.Trim().Trim('"', '\'', ' ');
                return Encoding.GetEncoding(sanitized);
            }
            catch { return null; }
        }

        private async Task<string> FetchHtmlWithFallbackAsync(string url)
        {
            try
            {
                // First attempt with default client
                var resp = await _http.GetAsync(url);
                var body = await ReadResponseAsStringAsync(resp);
                if (!LooksLikeBinary(body)) return body;

                // Fallback attempt: force Accept-Encoding to gzip,deflate only
                var req = new HttpRequestMessage(HttpMethod.Get, url);
                try
                {
                    req.Headers.Accept.Clear();
                    req.Headers.Accept.ParseAdd("text/html");
                    req.Headers.Accept.ParseAdd("application/xhtml+xml");
                    req.Headers.Accept.ParseAdd("application/xml; q=0.9");
                    req.Headers.Accept.ParseAdd("*/*; q=0.8");
                    req.Headers.AcceptLanguage.ParseAdd("en-US");
                    req.Headers.AcceptLanguage.ParseAdd("en; q=0.9");
                    req.Headers.AcceptEncoding.Clear();
                    req.Headers.AcceptEncoding.ParseAdd("gzip");
                    req.Headers.AcceptEncoding.ParseAdd("deflate");
                    if (_currentUri != null) req.Headers.Referrer = _currentUri;
                    req.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
                    req.Headers.Pragma.TryParseAdd("no-cache");
                }
                catch { }
                var resp2 = await _http.SendAsync(req);
                var body2 = await ReadResponseAsStringAsync(resp2);
                return body2;
            }
            catch { return null; }
        }

        private async Task<string> ReadResponseAsStringAsync(HttpResponseMessage resp)
        {
            var bytes = await resp.Content.ReadAsByteArrayAsync();
            bytes = TryDecompress(bytes, resp.Content.Headers.ContentEncoding);
            var enc = GetEncodingFromContentType(resp.Content.Headers.ContentType != null ? resp.Content.Headers.ContentType.CharSet : null) ?? Encoding.UTF8;
            try { return enc.GetString(bytes, 0, bytes.Length); } catch { return Encoding.UTF8.GetString(bytes, 0, bytes.Length); }
        }

        private bool LooksLikeBinary(string text)
        {
            if (string.IsNullOrEmpty(text)) return true;
            int sample = Math.Min(text.Length, 2048);
            if (sample <= 0) return true;
            int bad = 0;
            for (int i = 0; i < sample; i++)
            {
                char c = text[i];
                if (c == '\uFFFD') bad++;
                else if (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t') bad++;
            }
            return (bad / (double)sample) > 0.15;
        }

        private byte[] TryDecompress(byte[] data, IEnumerable<string> encodings)
        {
            if (data == null || data.Length == 0) return data;
            if (encodings == null) return data;
            foreach (var enc in encodings)
            {
                var e = (enc ?? string.Empty).Trim().ToLowerInvariant();
                try
                {
                    if (e == "gzip" || e == "x-gzip")
                    {
                        using (var input = new MemoryStream(data))
                        using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                        using (var output = new MemoryStream())
                        { gzip.CopyTo(output); return output.ToArray(); }
                    }
                    if (e == "deflate" || e == "x-deflate")
                    {
                        using (var input = new MemoryStream(data))
                        using (var def = new DeflateStream(input, CompressionMode.Decompress))
                        using (var output = new MemoryStream())
                        { def.CopyTo(output); return output.ToArray(); }
                    }
                }
                catch { return data; }
            }
            return data;
        }

        // HTML helpers
        private string TryExtractTitle(string html)
        {
            try
            {
                var m = Regex.Match(html, "<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (m.Success) return WebUtility.HtmlDecode(m.Groups[1].Value.Trim());
            }
            catch { }
            return null;
        }

        private string TryExtractVisibleText(string html)
        {
            try
            {
                html = Regex.Replace(html, "<script[\\s\\S]*?</script>", string.Empty, RegexOptions.IgnoreCase);
                html = Regex.Replace(html, "<style[\\s\\S]*?</style>", string.Empty, RegexOptions.IgnoreCase);
                var text = Regex.Replace(html, "<[^>]+>", " ");
                text = WebUtility.HtmlDecode(text);
                text = Regex.Replace(text, "\\s+", " ").Trim();
                if (text.Length > 4000) text = text.Substring(0, 4000) + "...";
                text = Regex.Replace(text, "new\\s*Date\\s*\\(\\s*\\)\\s*\\.\\s*getFullYear\\s*\\(\\s*\\)", DateTime.Now.Year.ToString(), RegexOptions.IgnoreCase);
                return text;
            }
            catch { return string.Empty; }
        }

        private async Task<string> LoadLocalAsync(string url)
        {
            try
            {
                var u = new Uri(url.StartsWith("ms-appx-web", StringComparison.OrdinalIgnoreCase) ? url.Replace("ms-appx-web", "ms-appx") : url);
                var file = await StorageFile.GetFileFromApplicationUriAsync(u);
                return await FileIO.ReadTextAsync(file);
            }
            catch { return null; }
        }

        // Gate-resolution helpers
        private async Task<Tuple<string, string>> TryResolveGatesAsync(string html, string currentUrl)
        {
            if (string.IsNullOrWhiteSpace(currentUrl)) return null;
            if (string.IsNullOrWhiteSpace(html)) return null;

            // 1) Meta refresh
            var refreshUrl = ExtractMetaRefreshUrl(html);
            if (!string.IsNullOrEmpty(refreshUrl))
            {
                var abs = ToAbsoluteUrl(_currentUri, refreshUrl) ?? NormalizeUrl(refreshUrl);
                var body = await HttpGetAsync(abs);
                if (!string.IsNullOrEmpty(body)) return Tuple.Create(body, abs);
            }

            // 2) AMP alternate
            var amp = ExtractAmpUrl(html);
            if (!string.IsNullOrEmpty(amp))
            {
                var abs = ToAbsoluteUrl(_currentUri, amp) ?? NormalizeUrl(amp);
                var body = await HttpGetAsync(abs);
                if (!string.IsNullOrEmpty(body)) return Tuple.Create(body, abs);
            }

            // 3) rel="alternate"
            var alt = ExtractAlternateUrl(html);
            if (!string.IsNullOrEmpty(alt))
            {
                var abs = ToAbsoluteUrl(_currentUri, alt) ?? NormalizeUrl(alt);
                var body = await HttpGetAsync(abs);
                if (!string.IsNullOrEmpty(body)) return Tuple.Create(body, abs);
            }

            // 4) Inline document.cookie set + retry
            bool cookiesApplied = ApplyInlineCookies(html, _currentUri);
            if (cookiesApplied)
            {
                var body = await HttpGetAsync(currentUrl);
                if (!string.IsNullOrEmpty(body)) return Tuple.Create(body, currentUrl);
            }

            // 5) Heuristic query variants
            foreach (var q in new[] { "amp=1", "m=1", "lite=1", "simple=1", "mobile=1" })
            {
                try
                {
                    string basePath = currentUrl;
                    string existing = "";
                    int qm = currentUrl.IndexOf('?');
                    if (qm >= 0)
                    {
                        basePath = currentUrl.Substring(0, qm);
                        existing = currentUrl.Substring(qm + 1);
                    }
                    string query = string.IsNullOrEmpty(existing) ? q : (existing + "&" + q);
                    var tryUrl = basePath + "?" + query;
                    var body = await HttpGetAsync(tryUrl);
                    if (!string.IsNullOrEmpty(body) && body.Length > 200)
                        return Tuple.Create(body, tryUrl);
                }
                catch { }
            }

            return null;
        }

        private string ExtractMetaRefreshUrl(string html)
        {
            try
            {
                var m = Regex.Match(html, "<meta[^>]*http-equiv\\s*=\\s*['\"]refresh['\"][^>]*content\\s*=\\s*['\"][^>]*url=([^'\">]+)", RegexOptions.IgnoreCase);
                if (m.Success) return WebUtility.HtmlDecode(m.Groups[1].Value.Trim());
            }
            catch { }
            return null;
        }

        private string ExtractAmpUrl(string html)
        {
            try
            {
                var m = Regex.Match(html, "<link[^>]*rel\\s*=\\s*['\"]amphtml['\"][^>]*href\\s*=\\s*['\"]([^'\"]+)['\"]", RegexOptions.IgnoreCase);
                if (m.Success) return WebUtility.HtmlDecode(m.Groups[1].Value.Trim());
            }
            catch { }
            return null;
        }

        private string ExtractAlternateUrl(string html)
        {
            try
            {
                var m = Regex.Match(html, "<link[^>]*rel\\s*=\\s*['\"]alternate['\"][^>]*href\\s*=\\s*['\"]([^'\"]+)['\"]", RegexOptions.IgnoreCase);
                if (m.Success) return WebUtility.HtmlDecode(m.Groups[1].Value.Trim());
            }
            catch { }
            return null;
        }

        private bool ApplyInlineCookies(string html, Uri baseUri)
        {
            if (_cookies == null || baseUri == null || string.IsNullOrEmpty(html)) return false;
            bool any = false;
            try
            {
                foreach (Match m in Regex.Matches(html, "document\\.cookie\\s*=\\s*(['\"])(.*?)\\1", RegexOptions.IgnoreCase))
                {
                    var raw = m.Groups[2].Value;
                    if (TryAddCookieFromString(raw, baseUri)) any = true;
                }
            }
            catch { }
            return any;
        }

        private bool TryAddCookieFromString(string cookieString, Uri baseUri)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(cookieString) || baseUri == null) return false;
                var parts = cookieString.Split(';');
                if (parts.Length == 0) return false;
                var kv = parts[0].Trim();
                var eq = kv.IndexOf('='); if (eq <= 0) return false;
                var name = kv.Substring(0, eq).Trim();
                var value = kv.Substring(eq + 1).Trim();
                string domain = baseUri.Host; string path = "/"; DateTime? exp = null; bool secure = false;
                for (int i = 1; i < parts.Length; i++)
                {
                    var p = parts[i].Trim();
                    if (p.StartsWith("domain=", StringComparison.OrdinalIgnoreCase)) domain = p.Substring(7).Trim('.');
                    else if (p.StartsWith("path=", StringComparison.OrdinalIgnoreCase)) path = p.Substring(5);
                    else if (p.StartsWith("expires=", StringComparison.OrdinalIgnoreCase)) { DateTime dt; if (DateTime.TryParse(p.Substring(8), out dt)) exp = dt; }
                    else if (string.Equals(p, "secure", StringComparison.OrdinalIgnoreCase)) secure = true;
                }
                var c = new Cookie(name, value, path, domain);
                if (exp.HasValue) c.Expires = exp.Value;
                c.Secure = secure;
                _cookies.Add(baseUri, c);
                return true;
            }
            catch { return false; }
        }

        // Image parsing and rendering
        private List<string> ExtractImageUrls(string html, Uri baseUri)
        {
            var urls = new List<string>();
            try
            {
                foreach (Match m in Regex.Matches(html, "<img[^>]*src\\s*=\\s*['\"]([^'\"]+)['\"]", RegexOptions.IgnoreCase))
                {
                    var src = WebUtility.HtmlDecode(m.Groups[1].Value.Trim());
                    var abs = ToAbsoluteUrl(baseUri, src) ?? src;
                    if (!string.IsNullOrWhiteSpace(abs)) urls.Add(abs);
                }

                // AMP images: <amp-img src=...> or srcset/data-srcset
                foreach (Match m in Regex.Matches(html, "<amp-img[^>]*>", RegexOptions.IgnoreCase))
                {
                    var tag = m.Value;
                    var src = ExtractFirstAttr(tag, "src") ?? ExtractFirstFromSrcset(tag);
                    if (!string.IsNullOrWhiteSpace(src))
                    {
                        var abs = ToAbsoluteUrl(baseUri, WebUtility.HtmlDecode(src)) ?? src;
                        if (!string.IsNullOrWhiteSpace(abs)) urls.Add(abs);
                    }
                }

                // Open Graph hero image
                var og = Regex.Match(html, "<meta[^>]*property=['\"]og:image['\"][^>]*content=['\"]([^'\"]+)['\"]", RegexOptions.IgnoreCase);
                if (og.Success)
                {
                    var abs = ToAbsoluteUrl(baseUri, WebUtility.HtmlDecode(og.Groups[1].Value.Trim())) ?? WebUtility.HtmlDecode(og.Groups[1].Value.Trim());
                    if (!string.IsNullOrWhiteSpace(abs)) urls.Add(abs);
                }
            }
            catch { }
            return urls;
        }

        private string ExtractFirstAttr(string tag, string name)
        {
            try
            {
                var m = Regex.Match(tag, name + "\\s*=\\s*['\"]([^'\"]+)['\"]", RegexOptions.IgnoreCase);
                if (m.Success) return m.Groups[1].Value.Trim();
            }
            catch { }
            return null;
        }

        private string ExtractFirstFromSrcset(string tag)
        {
            try
            {
                var m = Regex.Match(tag, "(srcset|data-srcset)\\s*=\\s*['\"]([^'\"]+)['\"]", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    var list = m.Groups[2].Value.Trim();
                    var first = list.Split(',')[0].Trim();
                    var url = first.Split(' ')[0].Trim();
                    return url;
                }
            }
            catch { }
            return null;
        }

        private async Task<StackPanel> TryBuildReaderViewAsync(string html, Uri baseUri)
        {
            try
            {
                // Prefer <article> then <main>
                string region = null;
                var ma = Regex.Match(html, "<article[^>]*>([\\s\\S]*?)</article>", RegexOptions.IgnoreCase);
                if (ma.Success) region = ma.Groups[1].Value;
                if (region == null)
                {
                    var mm = Regex.Match(html, "<main[^>]*>([\\s\\S]*?)</main>", RegexOptions.IgnoreCase);
                    if (mm.Success) region = mm.Groups[1].Value;
                }
                if (region == null) return null;

                var panel = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };

                // Extract heading
                var h = Regex.Match(region, "<h1[^>]*>([\\s\\S]*?)</h1>", RegexOptions.IgnoreCase);
                if (h.Success)
                {
                    var heading = WebUtility.HtmlDecode(Regex.Replace(h.Groups[1].Value, "<[^>]+>", " ").Trim());
                    if (!string.IsNullOrWhiteSpace(heading))
                        panel.Children.Add(new TextBlock { Text = heading, FontSize = 20, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
                }

                // Images within the region
                foreach (Match im in Regex.Matches(region, "<img[^>]*src\\s*=\\s*['\"]([^'\"]+)['\"]", RegexOptions.IgnoreCase))
                {
                    var src = ToAbsoluteUrl(baseUri, WebUtility.HtmlDecode(im.Groups[1].Value.Trim()));
                    var el = await CreateImageElementAsync(src);
                    if (el != null) panel.Children.Add(el);
                }

                // Paragraphs
                int added = 0;
                foreach (Match p in Regex.Matches(region, "<p[^>]*>([\\s\\S]*?)</p>", RegexOptions.IgnoreCase))
                {
                    var text = WebUtility.HtmlDecode(Regex.Replace(p.Groups[1].Value, "<[^>]+>", " ").Trim());
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        panel.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2) });
                        if (++added >= 20) break; // keep it tight
                    }
                }
                if (panel.Children.Count == 0) return null;
                return panel;
            }
            catch { return null; }
        }

        // Reader fallback: pick the largest text block from <div>/<section>
        private async Task<StackPanel> TryBuildLargestBlockReaderAsync(string html, Uri baseUri)
        {
            try
            {
                // Remove scripts/styles first
                string clean = Regex.Replace(html, "<script[\\s\\S]*?</script>", string.Empty, RegexOptions.IgnoreCase);
                clean = Regex.Replace(clean, "<style[\\s\\S]*?</style>", string.Empty, RegexOptions.IgnoreCase);

                // Find div/section blocks
                var matches = Regex.Matches(clean, "<(div|section)([^>]*)>([\\s\\S]*?)</\\1>", RegexOptions.IgnoreCase);
                string best = null; int bestScore = 0;
                foreach (Match m in matches)
                {
                    var attrs = m.Groups[2].Value.ToLowerInvariant();
                    if (attrs.Contains("header") || attrs.Contains("footer") || attrs.Contains("nav")) continue;
                    var inner = m.Groups[3].Value;
                    var text = Regex.Replace(inner, "<[^>]+>", " ").Trim();
                    text = WebUtility.HtmlDecode(Regex.Replace(text, "\\s+", " "));
                    int score = text.Length;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = inner;
                    }
                }
                if (string.IsNullOrWhiteSpace(best) || bestScore < 50) return null;

                var panel = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };

                // heading candidates inside block
                var h = Regex.Match(best, "<h1[^>]*>([\\s\\S]*?)</h1>", RegexOptions.IgnoreCase);
                if (!h.Success) h = Regex.Match(best, "<h2[^>]*>([\\s\\S]*?)</h2>", RegexOptions.IgnoreCase);
                if (h.Success)
                {
                    var heading = WebUtility.HtmlDecode(Regex.Replace(h.Groups[1].Value, "<[^>]+>", " ").Trim());
                    if (!string.IsNullOrWhiteSpace(heading))
                        panel.Children.Add(new TextBlock { Text = heading, FontSize = 20, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
                }

                foreach (Match im in Regex.Matches(best, "<img[^>]*src\\s*=\\s*['\"]([^'\"]+)['\"]", RegexOptions.IgnoreCase))
                {
                    var src = ToAbsoluteUrl(baseUri, WebUtility.HtmlDecode(im.Groups[1].Value.Trim()));
                    var el = await CreateImageElementAsync(src);
                    if (el != null) panel.Children.Add(el);
                }

                int added = 0;
                foreach (Match p in Regex.Matches(best, "<p[^>]*>([\\s\\S]*?)</p>", RegexOptions.IgnoreCase))
                {
                    var text = WebUtility.HtmlDecode(Regex.Replace(p.Groups[1].Value, "<[^>]+>", " ").Trim());
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        panel.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2) });
                        if (++added >= 20) break;
                    }
                }
                if (panel.Children.Count == 0) return null;
                return panel;
            }
            catch { return null; }
        }

        // Reader fallback: use <noscript> content when present
        private async Task<StackPanel> TryBuildNoscriptReaderAsync(string html, Uri baseUri)
        {
            try
            {
                var ns = Regex.Matches(html, "<noscript[^>]*>([\\s\\S]*?)</noscript>", RegexOptions.IgnoreCase);
                string best = null; int bestLen = 0;
                foreach (Match m in ns)
                {
                    var inner = m.Groups[1].Value;
                    if (inner.Length > bestLen) { bestLen = inner.Length; best = inner; }
                }
                if (string.IsNullOrWhiteSpace(best)) return null;
                var panel = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
                // Inline images in noscript
                foreach (Match im in Regex.Matches(best, "<img[^>]*src\\s*=\\s*['\"]([^'\"]+)['\"]", RegexOptions.IgnoreCase))
                {
                    var src = ToAbsoluteUrl(baseUri, WebUtility.HtmlDecode(im.Groups[1].Value.Trim()));
                    var el = await CreateImageElementAsync(src);
                    if (el != null) panel.Children.Add(el);
                }
                var text = WebUtility.HtmlDecode(Regex.Replace(best, "<[^>]+>", " ").Trim());
                if (!string.IsNullOrWhiteSpace(text)) panel.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap });
                if (panel.Children.Count == 0) return null;
                return panel;
            }
            catch { return null; }
        }

        // Reader fallback: meta preview and a simple links directory
        private async Task<StackPanel> TryBuildMetaAndLinksAsync(string html, Uri baseUri)
        {
            try
            {
                var panel = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };

                var mt = Regex.Match(html, "<meta[^>]*property=['\"]og:title['\"][^>]*content=['\"]([^'\"]+)['\"]", RegexOptions.IgnoreCase);
                if (mt.Success)
                {
                    var ogTitle = WebUtility.HtmlDecode(mt.Groups[1].Value.Trim());
                    if (!string.IsNullOrWhiteSpace(ogTitle))
                        panel.Children.Add(new TextBlock { Text = ogTitle, FontSize = 18, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
                }
                var md = Regex.Match(html, "<meta[^>]*name=['\"]description['\"][^>]*content=['\"]([^'\"]+)['\"]", RegexOptions.IgnoreCase);
                if (md.Success)
                {
                    var desc = WebUtility.HtmlDecode(md.Groups[1].Value.Trim());
                    if (!string.IsNullOrWhiteSpace(desc))
                        panel.Children.Add(new TextBlock { Text = desc, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
                }

                var links = new List<Tuple<string, string>>(); // text, href
                foreach (Match a in Regex.Matches(html, "<a[^>]*href=['\"]([^'\"]+)['\"][^>]*>([\\s\\S]*?)</a>", RegexOptions.IgnoreCase))
                {
                    var href = a.Groups[1].Value.Trim();
                    var text = WebUtility.HtmlDecode(Regex.Replace(a.Groups[2].Value, "<[^>]+>", " ").Trim());
                    if (string.IsNullOrWhiteSpace(text)) text = href;
                    var abs = ToAbsoluteUrl(baseUri, href) ?? href;
                    if (!string.IsNullOrWhiteSpace(abs)) links.Add(Tuple.Create(text, abs));
                    if (links.Count >= 30) break;
                }
                if (links.Count > 0)
                {
                    panel.Children.Add(new TextBlock { Text = "Links", FontSize = 16, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 4) });
                    foreach (var l in links)
                    {
                        var btn = new Button { Content = l.Item1, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 0) };
                        btn.Click += async (s, e) => { await NavigateAsync(l.Item2); };
                        panel.Children.Add(btn);
                    }
                }

                return panel.Children.Count > 0 ? panel : null;
            }
            catch { return null; }
        }

        // Reader fallback: role="main" / #content / .content/.main containers
        private async Task<StackPanel> TryBuildRoleMainReaderAsync(string html, Uri baseUri)
        {
            try
            {
                string region = null;
                // role="main"
                var r = Regex.Match(html, "<(div|section|main)[^>]*role\\s*=\\s*['\"]main['\"][^>]*>([\\s\\S]*?)</\\1>", RegexOptions.IgnoreCase);
                if (r.Success) region = r.Groups[2].Value;
                // id contains content/main
                if (region == null)
                {
                    var idm = Regex.Match(html, "<(div|section)[^>]*id\\s*=\\s*['\"][^'\"]*(content|main)[^'\"]*['\"][^>]*>([\\s\\S]*?)</\\1>", RegexOptions.IgnoreCase);
                    if (idm.Success) region = idm.Groups[3].Value;
                }
                // class contains content/main/article/post
                if (region == null)
                {
                    var clm = Regex.Match(html, "<(div|section)[^>]*class\\s*=\\s*['\"][^'\"]*(content|main|article|post)[^'\"]*['\"][^>]*>([\\s\\S]*?)</\\1>", RegexOptions.IgnoreCase);
                    if (clm.Success) region = clm.Groups[3].Value;
                }
                if (string.IsNullOrWhiteSpace(region)) return null;

                var panel = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };

                // Heading
                var h = Regex.Match(region, "<h1[^>]*>([\\s\\S]*?)</h1>", RegexOptions.IgnoreCase);
                if (!h.Success) h = Regex.Match(region, "<h2[^>]*>([\\s\\S]*?)</h2>", RegexOptions.IgnoreCase);
                if (h.Success)
                {
                    var heading = WebUtility.HtmlDecode(Regex.Replace(h.Groups[1].Value, "<[^>]+>", " ").Trim());
                    if (!string.IsNullOrWhiteSpace(heading))
                        panel.Children.Add(new TextBlock { Text = heading, FontSize = 20, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
                }

                // Images in region
                foreach (Match im in Regex.Matches(region, "<img[^>]*src\\s*=\\s*['\"]([^'\"]+)['\"]", RegexOptions.IgnoreCase))
                {
                    var src = ToAbsoluteUrl(baseUri, WebUtility.HtmlDecode(im.Groups[1].Value.Trim()));
                    var el = await CreateImageElementAsync(src);
                    if (el != null) panel.Children.Add(el);
                }

                // Paragraphs and list items as paragraphs
                int added = 0;
                foreach (Match p in Regex.Matches(region, "<(p|li)[^>]*>([\\s\\S]*?)</\\1>", RegexOptions.IgnoreCase))
                {
                    var text = WebUtility.HtmlDecode(Regex.Replace(p.Groups[2].Value, "<[^>]+>", " ").Trim());
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        panel.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2) });
                        if (++added >= 24) break;
                    }
                }
                return panel.Children.Count > 0 ? panel : null;
            }
            catch { return null; }
        }

        private List<BackgroundSpec> ExtractBackgroundSpecs(string html, Uri baseUri)
        {
            var specs = new List<BackgroundSpec>();
            try
            {
                // Capture entire style attribute for parsing
                foreach (Match m in Regex.Matches(html, "style=\\\"([^\\\"]+)\\\"", RegexOptions.IgnoreCase))
                {
                    var style = m.Groups[1].Value;
                    if (style.IndexOf("background-image", StringComparison.OrdinalIgnoreCase) < 0) continue;

                    // url(...)
                    var um = Regex.Match(style, "background-image\\s*:\\s*url\\((?:'|\")?([^'\")]*)", RegexOptions.IgnoreCase);
                    if (!um.Success) continue;
                    var src = WebUtility.HtmlDecode(um.Groups[1].Value.Trim());
                    var abs = ToAbsoluteUrl(baseUri, src) ?? src;
                    if (string.IsNullOrWhiteSpace(abs)) continue;

                    var spec = new BackgroundSpec { Url = abs };

                    // background-size
                    var sm = Regex.Match(style, "background-size\\s*:\\s*([^;]+)", RegexOptions.IgnoreCase);
                    if (sm.Success)
                    {
                        var val = sm.Groups[1].Value.Trim().ToLowerInvariant();
                        if (val.Contains("cover")) spec.Stretch = Stretch.UniformToFill; // cover
                        else if (val.Contains("contain")) spec.Stretch = Stretch.Uniform; // contain
                        else spec.Stretch = Stretch.UniformToFill;
                    }

                    // background-position
                    var pm = Regex.Match(style, "background-position\\s*:\\s*([^;]+)", RegexOptions.IgnoreCase);
                    if (pm.Success)
                    {
                        var val = pm.Groups[1].Value.Trim().ToLowerInvariant();
                        if (val.Contains("left")) spec.AlignX = AlignmentX.Left; else if (val.Contains("right")) spec.AlignX = AlignmentX.Right; else spec.AlignX = AlignmentX.Center;
                        if (val.Contains("top")) spec.AlignY = AlignmentY.Top; else if (val.Contains("bottom")) spec.AlignY = AlignmentY.Bottom; else spec.AlignY = AlignmentY.Center;
                    }

                    // background-repeat
                    var rm = Regex.Match(style, "background-repeat\\s*:\\s*([^;]+)", RegexOptions.IgnoreCase);
                    if (rm.Success)
                    {
                        var val = rm.Groups[1].Value.Trim().ToLowerInvariant();
                        if (val.Contains("no-repeat"))
                        {
                            spec.NoRepeat = true;
                        }
                    }

                    // height (optional visual hint)
                    var hm = Regex.Match(style, "height\\s*:\\s*([0-9]+)px", RegexOptions.IgnoreCase);
                    if (hm.Success)
                    {
                        double h; if (double.TryParse(hm.Groups[1].Value, out h) && h > 40) spec.Height = Math.Min(h, 480);
                    }

                    specs.Add(spec);
                }
            }
            catch { }
            return specs;
        }

        private string ToAbsoluteUrl(Uri baseUri, string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return url;
            if (baseUri == null) return null;
            try { return new Uri(baseUri, url).ToString(); } catch { return null; }
        }

        private async Task<Border> CreateBackgroundVisualAsync(BackgroundSpec spec)
        {
            try
            {
                if (spec == null || string.IsNullOrWhiteSpace(spec.Url)) return null;
                var bytes = await FetchImageBytesAsync(spec.Url);
                if (bytes == null || bytes.Length == 0) return null;
                var ras = new InMemoryRandomAccessStream();
                using (var w = ras.AsStreamForWrite()) { w.Write(bytes, 0, bytes.Length); w.Flush(); }
                ras.Seek(0);
                var bmp = new BitmapImage();
                await bmp.SetSourceAsync(ras);
                var brush = new ImageBrush { ImageSource = bmp, Stretch = spec.NoRepeat ? Stretch.None : spec.Stretch, AlignmentX = spec.AlignX, AlignmentY = spec.AlignY };
                return new Border
                {
                    Background = brush,
                    Height = spec.Height,
                    Margin = new Thickness(0, 8, 0, 8)
                };
            }
            catch { return null; }
        }

        // Minimal AJAX helpers (engine-side)
        public async Task<string> HttpGetAsync(string url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url)) return null;
                var abs = ToAbsoluteUrl(_currentUri, url) ?? NormalizeUrl(url);
                var req = new HttpRequestMessage(HttpMethod.Get, abs);
                try { if (_currentUri != null) req.Headers.Referrer = _currentUri; } catch { }
                var resp = await _http.SendAsync(req);
                var bytes = await resp.Content.ReadAsByteArrayAsync();
                bytes = TryDecompress(bytes, resp.Content.Headers.ContentEncoding);
                var enc = GetEncodingFromContentType(resp.Content.Headers.ContentType != null ? resp.Content.Headers.ContentType.CharSet : null) ?? Encoding.UTF8;
                return enc.GetString(bytes, 0, bytes.Length);
            }
            catch { return null; }
        }

        private async Task<Image> CreateImageElementAsync(string url)
        {
            try
            {
                var bytes = await FetchImageBytesAsync(url);
                if (bytes == null || bytes.Length == 0) return null;
                var ras = new InMemoryRandomAccessStream();
                using (var w = ras.AsStreamForWrite()) { w.Write(bytes, 0, bytes.Length); w.Flush(); }
                ras.Seek(0);
                var bmp = new BitmapImage();
                await bmp.SetSourceAsync(ras);
                return new Image { Source = bmp, MaxWidth = 480, Stretch = Stretch.Uniform, Margin = new Thickness(0, 8, 0, 8) };
            }
            catch { return null; }
        }

        private async Task<byte[]> FetchImageBytesAsync(string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl)) return null;
            if (_imageCache.ContainsKey(imageUrl)) return _imageCache[imageUrl];
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, imageUrl);
                req.Headers.Accept.Clear();
                req.Headers.Accept.ParseAdd("image/png");
                req.Headers.Accept.ParseAdd("image/jpeg");
                req.Headers.Accept.ParseAdd("image/jpg");
                req.Headers.Accept.ParseAdd("image/gif");
                req.Headers.Accept.ParseAdd("image/apng");
                try { if (_currentUri != null) req.Headers.Referrer = _currentUri; } catch { }

                var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                if (!resp.IsSuccessStatusCode) return null;
                var ct = resp.Content != null && resp.Content.Headers.ContentType != null ? resp.Content.Headers.ContentType.MediaType : string.Empty;
                using (var s = await resp.Content.ReadAsStreamAsync())
                using (var ms = new MemoryStream())
                {
                    await s.CopyToAsync(ms);
                    var bytes = ms.ToArray();
                    // If server sent WebP or other unsupported type, try heuristic fallbacks
                    if (!string.IsNullOrEmpty(ct) && ct.IndexOf("image/webp", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var alt = BuildNonWebpImageUrl(imageUrl);
                        if (!string.IsNullOrEmpty(alt) && !string.Equals(alt, imageUrl, StringComparison.OrdinalIgnoreCase))
                        {
                            var altBytes = await FetchImageBytesAsync(alt);
                            if (altBytes != null && altBytes.Length > 0) { _imageCache[imageUrl] = altBytes; return altBytes; }
                        }
                    }
                    _imageCache[imageUrl] = bytes;
                    return bytes;
                }
            }
            catch { return null; }
        }

        private string BuildNonWebpImageUrl(string url)
        {
            try
            {
                var u = url;
                // Replace extension .webp → .jpg
                if (u.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                {
                    return u.Substring(0, u.Length - 5) + ".jpg";
                }
                // If query contains format=webp, try format=jpg
                if (u.IndexOf("format=webp", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return Regex.Replace(u, "format=webp", "format=jpg", RegexOptions.IgnoreCase);
                }
                // Generic: append a hint that many CDNs recognize
                if (u.IndexOf("?", StringComparison.Ordinal) >= 0)
                    return u + "&format=jpg";
                else
                    return u + "?format=jpg";
            }
            catch { return url; }
        }
    }
}
