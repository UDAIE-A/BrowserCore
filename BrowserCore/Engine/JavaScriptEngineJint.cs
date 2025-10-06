#if JINT
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Jint;
using Jint.Native;

namespace BrowserCore.Engine
{
    // Jint-backed engine; compiled only when JINT symbol is defined and Jint is available
    public class JavaScriptEngineJint : IJavaScriptEngine
    {
        public event EventHandler<string> NavigationCompleted;
        public event EventHandler<string> TitleChanged;
        public event EventHandler<bool> LoadingStateChanged;

        private readonly Jint.Engine _engine;
        private readonly HttpClient _http;
        private IJavaScriptDomCallbacks _callbacks;
        private DocumentShim _documentShim;

        public JavaScriptEngineJint(HttpClient httpClient = null)
        {
            _http = httpClient ?? new HttpClient();
            _engine = new Jint.Engine(cfg => cfg.LimitRecursion(64).Strict().TimeoutInterval(TimeSpan.FromSeconds(5)));
            _engine.SetValue("console", new { log = new Action<object>(o => System.Diagnostics.Debug.WriteLine("[JS] " + (o?.ToString() ?? "(null)"))) });
            var doc = new DocumentShim(_callbacks);
            _documentShim = doc;
            doc.TitleChanged = (s) => { try { TitleChanged?.Invoke(this, s); } catch { } };
            _engine.SetValue("document", doc);
            _engine.SetValue("window", _engine.Global);
        }

        public void SetDomCallbacks(IJavaScriptDomCallbacks callbacks)
        {
            _callbacks = callbacks;
            try { _documentShim?.SetCallbacks(callbacks); } catch { }
        }

        public Task<bool> InitializeAsync() => Task.FromResult(true);

        public async Task NavigateAsync(string url)
        {
            await Task.Yield();
        }

        public Task<string> ExecuteScriptAsync(string script)
        {
            try
            {
                var value = _engine.Evaluate(script);
                return Task.FromResult(value.IsNull() || value.IsUndefined() ? null : value.ToString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Jint exec failed: {ex.Message}");
                return Task.FromResult<string>(null);
            }
        }

        public void Dispose() { }

        public class DocumentShim
        {
            private string _title;
            public Action<string> TitleChanged;
            private IJavaScriptDomCallbacks _callbacks;
            public BodyShim body { get; }

            public DocumentShim(IJavaScriptDomCallbacks callbacks = null)
            {
                _callbacks = callbacks;
                body = new BodyShim(this);
            }

            internal void SetCallbacks(IJavaScriptDomCallbacks callbacks)
            {
                _callbacks = callbacks;
                body?.SetCallbacks(callbacks);
            }

            public string title
            {
                get { return _title; }
                set
                {
                    _title = value;
                    try { TitleChanged?.Invoke(value); } catch { }
                }
            }

            public object createElement(string t) => new { t };

            public class BodyShim
            {
                private IJavaScriptDomCallbacks _callbacks;

                public BodyShim(DocumentShim owner)
                {
                    _callbacks = owner?._callbacks;
                }

                internal void SetCallbacks(IJavaScriptDomCallbacks callbacks)
                {
                    _callbacks = callbacks;
                }

                public string innerHTML
                {
                    get
                    {
                        try { return _callbacks != null ? _callbacks.GetBodyInnerHtml() : null; } catch { return null; }
                    }
                    set
                    {
                        try { _callbacks?.SetBodyInnerHtml(value ?? string.Empty); } catch { }
                    }
                }
            }
        }
    }
}
#else
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace BrowserCore.Engine
{
    public class JavaScriptEngineJint : IJavaScriptEngine
    {
        public event EventHandler<string> NavigationCompleted;
        public event EventHandler<string> TitleChanged;
        public event EventHandler<bool> LoadingStateChanged;

        public JavaScriptEngineJint(HttpClient httpClient = null) { }
        public Task<bool> InitializeAsync() => Task.FromResult(true);
        public Task NavigateAsync(string url) { return Task.FromResult(0); }
        public Task<string> ExecuteScriptAsync(string script) { return Task.FromResult<string>(null); }
        public void Dispose() { }
        public bool CanExecuteScripts { get { return false; } }
        public void SetDomCallbacks(IJavaScriptDomCallbacks callbacks) { }
    }
}
#endif




