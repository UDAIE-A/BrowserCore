            // Experimental Phase-1 DOM rendering path (lite DOM to XAML) before legacy renderer
            if (_experimentalDomRenderer)
            {
                try
                {
                    LiteElement liteRoot = HtmlLiteParser.Parse(html ?? string.Empty);
                    UIElement view = await DomBasicRenderer.BuildAsync(
                        liteRoot,
                        _currentUri,
                        null,
                        async (string abs) => { try { return await CreateImageElementAsync(abs); } catch { return null; } },
                        async (string href) => { try { await NavigateAsync(href); } catch { } }
                    );
                    if (view != null)
                    {
                        await ReplaceContentAsync(view);
                        await ShowLoadingAsync(false, "Ready");
                        if (NavigationCompleted != null) NavigationCompleted(this, url);
                        return;
                    }
                }
                catch { }
            }

