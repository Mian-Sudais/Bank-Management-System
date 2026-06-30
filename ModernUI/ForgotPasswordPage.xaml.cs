using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Threading.Tasks;
using Windows.UI;  // Add this line at the top with other using statements
using Windows.ApplicationModel.DataTransfer;

namespace ModernUI
{
    public sealed partial class ForgotPasswordPage : Page
    {
        // Guards against the auto-redirect firing after the user has already
        // navigated away manually (e.g. pressed "Back to Login" before the 2-s
        // timer elapses).
        private bool _navigationPending = false;

        // Kept so we can Stop() it if the user navigates away manually.
        private DispatcherTimer? _redirectTimer;

        public ForgotPasswordPage()
        {
            this.InitializeComponent();

            // Allow submitting via Enter from the new-password box.
            NewPassBox.KeyDown += (s, e) =>
            {
                if (e.Key == Windows.System.VirtualKey.Enter && ResetBtn.IsEnabled)
                    ResetBtn_Click(ResetBtn, null!);
            };

            this.Loaded += ForgotPasswordPage_Loaded;
        }

        // ── Entrance animation ────────────────────────────────────────────────

        private void ForgotPasswordPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (MainCard is null) return;

            MainCard.Opacity = 0;
            MainCard.RenderTransform = new Microsoft.UI.Xaml.Media.TranslateTransform { Y = 28 };
            MainCard.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);

            var sb = new Storyboard();

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromSeconds(0.55)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                BeginTime = TimeSpan.FromSeconds(0.05)
            };
            Storyboard.SetTarget(fadeIn, MainCard);
            Storyboard.SetTargetProperty(fadeIn, "Opacity");
            sb.Children.Add(fadeIn);

            var slideUp = new DoubleAnimation
            {
                From = 28,
                To = 0,
                Duration = new Duration(TimeSpan.FromSeconds(0.65)),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.25 },
                BeginTime = TimeSpan.FromSeconds(0.05)
            };
            Storyboard.SetTarget(slideUp, MainCard.RenderTransform);
            Storyboard.SetTargetProperty(slideUp, "Y");
            sb.Children.Add(slideUp);

            sb.Begin();
        }

        // ── Password strength (mirrors SignupPage logic) ───────────────────────

        private static bool IsPasswordStrong(string password, out string reason)
        {
            reason = string.Empty;

            if (password.Length < 8)
            {
                reason = "New password must be at least 8 characters long.";
                return false;
            }

            bool hasLetter = false, hasDigit = false, hasSymbol = false;
            foreach (char c in password)
            {
                if (char.IsLetter(c)) hasLetter = true;
                else if (char.IsDigit(c)) hasDigit = true;
                else hasSymbol = true;
            }

            if (!hasLetter) { reason = "New password must contain at least one letter (a–z or A–Z)."; return false; }
            if (!hasDigit) { reason = "New password must contain at least one number (0–9)."; return false; }
            if (!hasSymbol) { reason = "New password must contain at least one symbol (e.g. @, #, !, $)."; return false; }
            return true;
        }

        // ── Reset handler ─────────────────────────────────────────────────────

        private async void ResetBtn_Click(object sender, RoutedEventArgs? e)
        {
            HideFeedback();
            ResetBtn.IsEnabled = false;
            ResetBtn.Content = "Resetting…";

            try
            {
                // ── Field presence check ──────────────────────────────────────
                if (string.IsNullOrWhiteSpace(IdBox.Text) ||
                    string.IsNullOrWhiteSpace(CnicBox.Text) ||
                    string.IsNullOrWhiteSpace(BackupBox.Text) ||
                    string.IsNullOrWhiteSpace(NewPassBox.Password))
                {
                    ShowFeedback("All fields are required.", success: false);
                    ShakeCard();
                    return;
                }

                // FIX: enforce full strength policy on reset (was length-only check)
                if (!IsPasswordStrong(NewPassBox.Password, out string reason))
                {
                    ShowFeedback(reason, success: false);
                    ShakeCard();
                    return;
                }

                // Tiny artificial delay so the button state is visible
                await Task.Delay(180);

                string result = App.Bank.Execute(
                    "reset_password",
                    IdBox.Text.Trim(),
                    CnicBox.Text.Trim(),
                    BackupBox.Text.Trim(),
                    NewPassBox.Password);

                bool success = result.Contains("Success", StringComparison.OrdinalIgnoreCase);

                if (!success)
                {
                    ShowFeedback(result, success: false);
                    ShakeCard();
                }
                else
                {
                    // Parse the new backup code from the response
                    string newBackupCode = string.Empty;
                    const string codeMarker = "New backup code: ";
                    int codeIdx = result.IndexOf(codeMarker, StringComparison.OrdinalIgnoreCase);

                    if (codeIdx >= 0)
                    {
                        newBackupCode = result[(codeIdx + codeMarker.Length)..].Trim();
                        // Extract only the 6-character code
                        if (newBackupCode.Length > 6)
                            newBackupCode = newBackupCode[..6];
                    }

                    // Show the new backup code dialog BEFORE redirecting
                    if (!string.IsNullOrEmpty(newBackupCode))
                    {
                        await ShowNewBackupCodeDialog(newBackupCode);
                    }

                    // Now proceed with redirect
                    _navigationPending = true;
                    _redirectTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                    _redirectTimer.Tick += (s, args) =>
                    {
                        _redirectTimer!.Stop();
                        if (_navigationPending)
                            NavigateToLogin();
                    };
                    _redirectTimer.Start();
                }
            }
            catch (Exception ex)
            {
                ShowFeedback($"Unexpected error: {ex.Message}", success: false);
                ShakeCard();
            }
            finally
            {
                ResetBtn.IsEnabled = true;
                ResetBtn.Content = "Reset Password";
            }
        }

        private void BackLink_Click(object sender, RoutedEventArgs e)
        {
            // FIX: stop the auto-redirect timer before navigating manually
            _redirectTimer?.Stop();
            _navigationPending = false;
            NavigateToLogin();
        }

        private async Task ShowNewBackupCodeDialog(string newBackupCode)
        {
            var dialog = new ContentDialog
            {
                Title = "New Backup Code Generated",
                Content = new StackPanel
                {
                    Spacing = 12,
                    Children =
            {
                new TextBlock
                {
                    Text = "Your password has been reset successfully. A new backup code has been generated for future password recovery.",
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = new FontFamily("Segoe UI Variable Text"),
                    Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x9C, 0xA3, 0xAF)),
                    FontSize = 13
                },
                new Border
                {
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(16, 12, 16, 12),
                    Background = new SolidColorBrush(Color.FromArgb(0x20, 0x10, 0xD9, 0x86)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x10, 0xD9, 0x86)),
                    BorderThickness = new Thickness(1),
                    Child = new TextBlock
                    {
                        Text = newBackupCode,
                        FontFamily = new FontFamily("Cascadia Code, Consolas, monospace"),
                        FontSize = 20,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x10, 0xD9, 0x86)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        CharacterSpacing = 200
                    }
                },
                new TextBlock
                {
                    Text = "️ Save this code securely. It will NOT be shown again.",
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = new FontFamily("Segoe UI Variable Text"),
                    Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xF5, 0x9E, 0x0B)),
                    FontSize = 12,
                    FontWeight = FontWeights.Medium
                }
            }
                },
                // ✅ PRIMARY: Proceed to Login
                PrimaryButtonText = "I've Saved It",
                // ✅ SECONDARY: Copy to Clipboard (This triggers the copy logic!)
                SecondaryButtonText = "Copy to Clipboard",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot,
                RequestedTheme = ElementTheme.Dark
            };

            var result = await dialog.ShowAsync();

            // ✅ Catch the Secondary Button Click (Copy)
            if (result == ContentDialogResult.Secondary)
            {
                try
                {
                    var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                    dataPackage.SetText(newBackupCode);
                    Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
                    ShowFeedback("Backup code copied to clipboard!", success: true);
                }
                catch (Exception ex)
                {
                    ShowFeedback("Failed to copy code.", success: false);
                }
            }
        }

        // ── Navigation helper ─────────────────────────────────────────────────

        private void NavigateToLogin()
            => Frame.Navigate(
                   typeof(LoginPage),
                   null,
                   new SlideNavigationTransitionInfo
                   {
                       Effect = SlideNavigationTransitionEffect.FromLeft
                   });

        // ── Shake animation ───────────────────────────────────────────────

        private void ShakeCard()
        {
            if (MainCard is null) return;

            if (MainCard.RenderTransform is not Microsoft.UI.Xaml.Media.TranslateTransform)
                MainCard.RenderTransform = new Microsoft.UI.Xaml.Media.TranslateTransform();

            var sb = new Storyboard();

            // Use keyframe animation instead of multiple separate animations
            var shakeAnimation = new DoubleAnimationUsingKeyFrames();
            Storyboard.SetTarget(shakeAnimation, MainCard.RenderTransform);
            Storyboard.SetTargetProperty(shakeAnimation, "X");

            double[] keys = { 0, -8, 7, -5, 4, -2, 0 };
            double step = 0.07;

            for (int i = 0; i < keys.Length; i++)
            {
                var keyFrame = new LinearDoubleKeyFrame
                {
                    Value = keys[i],
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(i * step))
                };
                shakeAnimation.KeyFrames.Add(keyFrame);
            }

            sb.Children.Add(shakeAnimation);
            sb.Begin();
        }

        // ── Show New Backup Code Dialog ─────────────────────────────────────────

        // ── Feedback helpers ──────────────────────────────────────────────────

        private void ShowFeedback(string message, bool success)
        {
            FeedbackBar.Severity = success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
            FeedbackBar.Message = message;
            FeedbackBar.IsOpen = true;
        }

        private void HideFeedback() => FeedbackBar.IsOpen = false;
    }
}