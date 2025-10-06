using System.Net.Http;

namespace BrowserCore.Engine
{
    public static class JavaScriptEngineFactory
    {
        public static IJavaScriptEngine Create(HttpClient httpClient = null)
        {
            return new JavaScriptEngine();
        }
    }
}
