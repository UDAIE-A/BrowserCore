using System;
using System.Threading.Tasks;

namespace BrowserCore.Engine
{
    /// <summary>
    /// Minimal no-op JavaScript engine used on platforms where we cannot host a real JS runtime.
    /// </summary>
    public class JavaScriptEngine : IJavaScriptEngine
    {
        public event EventHandler<string> NavigationCompleted;
        public event EventHandler<string> TitleChanged;
        public event EventHandler<bool> LoadingStateChanged;

        public JavaScriptEngine()
        {
            System.Diagnostics.Debug.WriteLine("JavaScriptEngine: initialized in no-op mode (no embedded JS runtime available).");
        }

        public void SetDomCallbacks(IJavaScriptDomCallbacks callbacks){
            // No-op: there is no scripting surface to bridge into the DOM.
        }
        public bool CanExecuteScripts { get { return false; } }

        public Task<bool> InitializeAsync()
        {
            return Task.FromResult(true);
        }

        public async Task NavigateAsync(string url)
        {
            // Nothing to do; caller keeps state.
            await Task.Yield();
        }

        public Task<string> ExecuteScriptAsync(string script)
        {
            System.Diagnostics.Debug.WriteLine("JavaScriptEngine: ExecuteScriptAsync ignored because engine is disabled.");
            return Task.FromResult<string>(null);
        }

        public void Dispose()
        {
            // Nothing to dispose.
        }
    }
}



