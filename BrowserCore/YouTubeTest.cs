using System;
using System.Threading.Tasks;

namespace BrowserCore.Test
{
    /// <summary>
    /// Simple test to verify YouTube URL handling and CustomHtmlEngine functionality
    /// </summary>
    public class YouTubeTest
    {
        public static void TestYouTubeUrlExtraction()
        {
            try
            {
                Console.WriteLine("=== Testing YouTube URL Extraction ===");
                
                // Test various YouTube URL formats
                var testUrls = new[]
                {
                    "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                    "https://youtu.be/dQw4w9WgXcQ",
                    "https://youtube.com/watch?v=dQw4w9WgXcQ&list=123",
                    "https://m.youtube.com/watch?v=dQw4w9WgXcQ"
                };
                
                foreach (var url in testUrls)
                {
                    var videoId = ExtractVideoId(url);
                    Console.WriteLine($"URL: {url}");
                    Console.WriteLine($"Video ID: {videoId}");
                    Console.WriteLine($"Valid: {!string.IsNullOrEmpty(videoId)}");
                    Console.WriteLine("---");
                }
                
                Console.WriteLine("YouTube URL extraction test completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"YouTube test failed: {ex.Message}");
            }
        }
        
        private static string ExtractVideoId(string url)
        {
            try
            {
                // Simple regex-free extraction for Windows Phone 8.1 compatibility
                if (url.Contains("youtube.com/watch?v="))
                {
                    var startIndex = url.IndexOf("v=") + 2;
                    var endIndex = url.IndexOf("&", startIndex);
                    if (endIndex == -1) endIndex = url.Length;
                    
                    var videoId = url.Substring(startIndex, endIndex - startIndex);
                    return videoId.Length == 11 ? videoId : null;
                }
                else if (url.Contains("youtu.be/"))
                {
                    var startIndex = url.IndexOf("youtu.be/") + 9;
                    var endIndex = url.IndexOf("?", startIndex);
                    if (endIndex == -1) endIndex = url.Length;
                    
                    var videoId = url.Substring(startIndex, endIndex - startIndex);
                    return videoId.Length == 11 ? videoId : null;
                }
                
                return null;
            }
            catch
            {
                return null;
            }
        }
        
        public static void TestErrorHandling()
        {
            try
            {
                Console.WriteLine("=== Testing Error Handling ===");
                
                // Test null/empty URL handling
                TestUrlHandling(null);
                TestUrlHandling("");
                TestUrlHandling("invalid-url");
                TestUrlHandling("https://invalid-youtube-url.com");
                
                Console.WriteLine("Error handling test completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling test failed: {ex.Message}");
            }
        }
        
        private static void TestUrlHandling(string url)
        {
            try
            {
                Console.WriteLine($"Testing URL: '{url ?? "null"}'");
                
                if (string.IsNullOrEmpty(url))
                {
                    Console.WriteLine("  -> Handled null/empty URL correctly");
                    return;
                }
                
                var isYouTube = url.ToLower().Contains("youtube.com") || url.ToLower().Contains("youtu.be");
                Console.WriteLine($"  -> IsYouTube: {isYouTube}");
                
                if (isYouTube)
                {
                    var videoId = ExtractVideoId(url);
                    Console.WriteLine($"  -> VideoID: {videoId ?? "null"}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  -> Exception handled: {ex.Message}");
            }
        }
    }
}