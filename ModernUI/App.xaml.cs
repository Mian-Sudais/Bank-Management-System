using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;

namespace ModernUI
{
    public partial class App : Application
    {
        /// <summary>Singleton native bank handle, available for the lifetime of the app.</summary>
        public static BankHandle Bank { get; private set; } = null!;

        private Window? _window;

        // FIX: capture the UI dispatcher queue at construction time (on the UI thread)
        // so we can safely marshal back to it from ContinueWith() callbacks.
        private readonly DispatcherQueue _uiQueue = DispatcherQueue.GetForCurrentThread();

        public App()
        {
            this.InitializeComponent();
            this.UnhandledException += App_UnhandledException;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            // Create the main window first so we have an XamlRoot for dialogs.
            _window = new MainWindow();

            try
            {
                Bank = new BankHandle();
                Debug.WriteLine("[App] BankHandle initialised successfully.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[App] FATAL – BankHandle init failed: {ex}");

                // Activate the window so XamlRoot is available for the dialog.
                _window.Activate();

                var dialog = new ContentDialog
                {
                    Title = "Startup Error",
                    Content = $"Failed to load the banking engine:\n\n{ex.Message}\n\n" +
                                        "Ensure CoreLogicFixed.dll is present in the application " +
                                        "folder and matches the process architecture (x86/x64/ARM64).",
                    PrimaryButtonText = "Exit",
                    XamlRoot = _window.Content.XamlRoot,
                    RequestedTheme = ElementTheme.Dark
                };

                // FIX: use the captured _uiQueue instance instead of the non-existent
                //      static DispatcherQueue.GetForCurrentThread() call inside a
                //      threadpool ContinueWith callback (which has no UI thread context).
                _ = dialog.ShowAsync().AsTask().ContinueWith(_ =>
                {
                    _uiQueue.TryEnqueue(() => _window.Close());
                });

                return;
            }

            _window.Closed += (_, _) =>
            {
                Bank?.Dispose();
                Debug.WriteLine("[App] Window closed – BankHandle disposed.");
            };

            _window.Activate();
            Debug.WriteLine("[App] MainWindow activated.");
        }

        private void App_UnhandledException(object sender,
            Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Debug.WriteLine($"[App] Unhandled exception: {e.Exception}");
            // Let the debugger catch it in debug builds; swallow in release.
            e.Handled = Debugger.IsAttached;
        }
    }
}