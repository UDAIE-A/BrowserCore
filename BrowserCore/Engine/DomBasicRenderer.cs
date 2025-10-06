using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace BrowserCore.Engine
{
    internal static class DomBasicRenderer
    {
        public static async Task<UIElement> BuildAsync(LiteElement root, Uri baseUri, System.Func<string, Windows.UI.Xaml.Controls.Image> legacySyncImageFactory, System.Func<string, System.Threading.Tasks.Task<Windows.UI.Xaml.Controls.Image>> imageFactory, System.Func<string, System.Threading.Tasks.Task> navigate)
        {
            try
            {
                var stack = new StackPanel();
                await BuildElementsAsync(stack, root != null ? root.Children : null, baseUri, legacySyncImageFactory, imageFactory, navigate);
                return stack;
            }
            catch { return null; }
        }

        private static async Task BuildElementsAsync(Panel parent, List<LiteElement> nodes, Uri baseUri, System.Func<string, Windows.UI.Xaml.Controls.Image> legacySyncImageFactory, System.Func<string, System.Threading.Tasks.Task<Windows.UI.Xaml.Controls.Image>> imageFactory, System.Func<string, System.Threading.Tasks.Task> navigate)
        {
            if (parent == null || nodes == null) return;
            foreach (var node in nodes)
            {
                var tag = (node.Tag ?? string.Empty).ToLowerInvariant();
                if (tag == "h1" || tag == "h2" || tag == "h3" || tag == "h4" || tag == "h5" || tag == "h6")
                {
                    double size = tag == "h1" ? 24 : tag == "h2" ? 22 : tag == "h3" ? 20 : tag == "h4" ? 18 : tag == "h5" ? 16 : 15;
                    var tb = new TextBlock { Text = node.Text.ToString().Trim(), FontSize = size, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0,6,0,6), TextWrapping = TextWrapping.Wrap };
                    parent.Children.Add(tb);
                }
                else if (tag == "p" || tag == "span" || tag == "div" || tag == "section" || tag == "article" || tag == "main")
                {
                    var text = node.Text.ToString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        var tb = new TextBlock { Text = text.Trim(), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,2,0,8) };
                        parent.Children.Add(tb);
                    }
                    if (node.Children != null && node.Children.Count > 0)
                        await BuildElementsAsync(parent, node.Children, baseUri, legacySyncImageFactory, imageFactory, navigate);
                }
                else if (tag == "ul" || tag == "ol")
                {
                    bool ordered = tag == "ol";
                    int idx = 1;
                    foreach (var li in node.Children)
                    {
                        if (!string.Equals(li.Tag, "li", StringComparison.OrdinalIgnoreCase)) continue;
                        var prefix = ordered ? (idx++.ToString() + ". ") : "? ";
                        var tb = new TextBlock { Text = prefix + li.Text.ToString().Trim(), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,2,0,2) };
                        parent.Children.Add(tb);
                    }
                }
                else if (tag == "img")
                {
                    string src; node.Attributes.TryGetValue("src", out src);
                    if (!string.IsNullOrWhiteSpace(src))
                    {
                        var abs = ToAbsoluteUrl(baseUri, src) ?? src;
                        var img = imageFactory != null ? await imageFactory(abs) : (legacySyncImageFactory != null ? legacySyncImageFactory(abs) : null);
                        if (img != null) parent.Children.Add(img);
                    }
                }
                else if (tag == "a")
                {
                    string href; node.Attributes.TryGetValue("href", out href);
                    var linkText = node.Text.ToString().Trim();
                    if (string.IsNullOrWhiteSpace(linkText)) linkText = href;
                    var btn = new Button { Content = linkText, Background = new SolidColorBrush(Windows.UI.Colors.Transparent), BorderBrush = new SolidColorBrush(Windows.UI.Colors.Transparent), Foreground = new SolidColorBrush(Windows.UI.Colors.Blue), Padding = new Thickness(0), Margin = new Thickness(0,2,0,2) };
                    btn.Click += async (s,e) => { try { if (!string.IsNullOrWhiteSpace(href) && navigate != null) await navigate(href); } catch { } };
                    parent.Children.Add(btn);
                }
                else if (tag == "br")
                {
                    parent.Children.Add(new TextBlock { Text = "\u00A0", Margin = new Thickness(0,2,0,2) });
                }
                else if (tag == "hr")
                {
                    parent.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Windows.UI.Colors.LightGray), Margin = new Thickness(0,6,0,6) });
                }
                else
                {
                    if (node.Children != null && node.Children.Count > 0)
                        await BuildElementsAsync(parent, node.Children, baseUri, legacySyncImageFactory, imageFactory, navigate);
                }
            }
        }

        ;
                await Task.FromResult(0);
                return img;
            }
            catch { return null; }
        }

        internal static string ToAbsoluteUrl(Uri baseUri, string url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url)) return null;
                if (url.StartsWith("http://") || url.StartsWith("https://")) return url;
                if (baseUri == null) return url;
                return new Uri(baseUri, url).ToString();
            }
            catch { return url; }
        }

        // Navigation bridge (will be wired by caller if needed)
        
        {
            await Task.FromResult(0); // placeholder; real navigation is performed by the host engine
        }
    }
}

