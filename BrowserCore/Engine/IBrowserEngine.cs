using System;
using System.Threading.Tasks;

namespace BrowserCore.Engine
{
    /// <summary>
    /// Clean browser engine interface for ARM-optimized browsing
    /// </summary>
    public interface IBrowserEngine
    {
        event EventHandler<string> NavigationCompleted;
        event EventHandler<string> TitleChanged;
        event EventHandler<bool> LoadingStateChanged;

        Task<bool> InitializeAsync();
        Task NavigateAsync(string url);
        Task<string> ExecuteScriptAsync(string script);
        void Dispose();
    }
}