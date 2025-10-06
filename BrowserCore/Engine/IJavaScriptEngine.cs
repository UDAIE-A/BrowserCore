using System;

namespace BrowserCore.Engine
{
    /// <summary>
    /// Extended browser engine contract for JavaScript runtimes that need to integrate with the host DOM.
    /// </summary>
    public interface IJavaScriptEngine : IBrowserEngine
    {
        /// <summary>
        /// Provides the engine with callbacks to communicate DOM mutations back to the host renderer.
        /// </summary>
        /// <param name="callbacks">Callback implementation supplied by the HTML engine.</param>
        void SetDomCallbacks(IJavaScriptDomCallbacks callbacks);
        // Indicates whether this engine can execute JS on this platform.
        bool CanExecuteScripts { get; }
    }
}

