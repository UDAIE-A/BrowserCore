using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace BrowserCore
{
    /// <summary>
    /// Clean BrowserCore Application with ARM-first architecture
    /// </summary>
    sealed partial class App : Application
    {
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
            
            // Add global exception handler
            this.UnhandledException += App_UnhandledException;
        }

        private void App_UnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            // Log the exception
            System.Diagnostics.Debug.WriteLine($"UNHANDLED EXCEPTION: {e.Exception.Message}");
            System.Diagnostics.Debug.WriteLine($"STACK TRACE: {e.Exception.StackTrace}");
            
            // Mark as handled to prevent crash
            e.Handled = true;
            
            // Show user-friendly message (if UI is available)
            try
            {
                var rootFrame = Window.Current.Content as Frame;
                if (rootFrame != null && rootFrame.Content != null)
                {
                    var mainPage = rootFrame.Content as MainPage;
                    if (mainPage != null)
                    {
                        // Try to show error in status if available
                        System.Diagnostics.Debug.WriteLine("Attempting to show error in UI");
                    }
                }
            }
            catch
            {
                // If we can't show in UI, at least we logged it
                System.Diagnostics.Debug.WriteLine("Could not show error in UI");
            }
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            if (rootFrame == null)
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                Window.Current.Content = rootFrame;
            }

            if (rootFrame.Content == null)
            {
                rootFrame.Navigate(typeof(MainPage), e.Arguments);
            }

            Window.Current.Activate();
        }

        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            deferral.Complete();
        }
    }
}