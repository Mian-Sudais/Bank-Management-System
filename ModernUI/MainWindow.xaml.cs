using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics;
using Windows.Graphics;
using Windows.UI;

namespace ModernUI
{
    public sealed partial class MainWindow : Window
    {
        private const int DefaultWindowWidth = 1200;
        private const int DefaultWindowHeight = 800;
        private const int MinWindowWidth = 900;
        private const int MinWindowHeight = 600;
        private AppWindow _appWindow;

        public MainWindow()
        {
            this.InitializeComponent();

            // Get AppWindow reference (WinUI 1.3+)
            _appWindow = this.AppWindow;

            // ✅ EXTEND CONTENT INTO TITLE BAR (MANDATORY for acrylic transparency)
            ExtendsContentIntoTitleBar = true;

            // ✅ Make caption buttons transparent to show acrylic backdrop
            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                var titleBar = _appWindow.TitleBar;

                // Transparent button backgrounds (alpha channel respected when ExtendsContentIntoTitleBar=true) [[3]]
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonHoverBackgroundColor = Color.FromArgb(51, 255, 255, 255); // 20% white overlay
                titleBar.ButtonPressedBackgroundColor = Color.FromArgb(76, 255, 255, 255); // 30% white overlay

                // Button foreground (icons) - adjust for your theme
                titleBar.ButtonForegroundColor = Colors.White;
                titleBar.ButtonHoverForegroundColor = Colors.White;
                titleBar.ButtonPressedForegroundColor = Colors.White;

                // Inactive state colors
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveForegroundColor = Color.FromArgb(180, 255, 255, 255);
            }

            // ✅ Set tall title bar for better touch/visual spacing (optional but recommended)
            if (ExtendsContentIntoTitleBar)
            {
                _appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            }

            // ✅ Initialize padding columns AFTER window is loaded
            AppTitleBar.Loaded += AppTitleBar_Loaded;
            AppTitleBar.SizeChanged += AppTitleBar_SizeChanged;

            // Window sizing
            _appWindow.Resize(new SizeInt32(DefaultWindowWidth, DefaultWindowHeight));
            _appWindow.Title = "Bank Management System";

            // Application icon
            try { _appWindow.SetIcon("Assets\\app.ico"); }
            catch { /* non-critical */ }

            // Enforce minimum size
            _appWindow.Changed += AppWindow_Changed;

            // Handle window activation for visual feedback
            this.Activated += MainWindow_Activated;

            // Navigate to login
            MainFrame.Navigate(typeof(LoginPage));

            this.Closed += MainWindow_Closed;
            Debug.WriteLine("[MainWindow] Initialised → LoginPage with modern title bar.");
        }

        // ✅ Calculate and apply padding for caption buttons (critical for proper layout)
        private void AppTitleBar_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateTitleBarPadding();
        }

        private void AppTitleBar_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateTitleBarPadding();
        }

        private void UpdateTitleBarPadding()
        {
            if (!ExtendsContentIntoTitleBar) return;

            // Account for display scaling (DPI) [[3]]
            double scale = AppTitleBar.XamlRoot.RasterizationScale;
            var titleBar = _appWindow.TitleBar;

            LeftPaddingColumn.Width = new GridLength(titleBar.LeftInset / scale);
            RightPaddingColumn.Width = new GridLength(titleBar.RightInset / scale);
        }

        // ✅ Visual feedback when window loses/gains focus
        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            var resource = args.WindowActivationState == WindowActivationState.Deactivated
                ? "WindowCaptionForegroundDisabled"
                : "WindowCaptionForeground";

            if (App.Current.Resources.TryGetValue(resource, out var brush))
            {
                TitleBarText.Foreground = (SolidColorBrush)brush;
            }
        }

        private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (args.DidSizeChange)
            {
                var sz = sender.Size;
                int w = sz.Width < MinWindowWidth ? MinWindowWidth : sz.Width;
                int h = sz.Height < MinWindowHeight ? MinWindowHeight : sz.Height;
                if (w != sz.Width || h != sz.Height)
                    sender.Resize(new SizeInt32(w, h));
            }
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            Debug.WriteLine("[MainWindow] Closed.");
        }
    }
}