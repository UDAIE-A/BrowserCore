using System;

namespace BrowserCore.Engine
{
    /// <summary>
    /// Callbacks that the host HTML engine exposes to the JavaScript runtime so scripts can mutate the document.
    /// </summary>
    public interface IJavaScriptDomCallbacks
    {
        /// <summary>
        /// Replace the current document body contents with the provided HTML snippet.
        /// </summary>
        /// <param name="html">Raw HTML fragment provided by script.</param>
        void SetBodyInnerHtml(string html);

        /// <summary>
        /// Returns the host's current representation of the document body's markup.
        /// </summary>
        /// <returns>HTML fragment currently rendered.</returns>
        string GetBodyInnerHtml();
    }
}
