using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace ModernUI
{
    public sealed partial class SignupPage : Page
    {
        public SignupPage()
        {
            this.InitializeComponent();

            NameBox.TextChanging += NameBox_TextChanging;
            CnicBox.TextChanging += CnicBox_TextChanging;
            PhoneBox.TextChanging += PhoneBox_TextChanging;

            // Allow submitting from the confirm-password box via Enter
            ConfirmBox.KeyDown += (s, e) =>
            {
                if (e.Key == Windows.System.VirtualKey.Enter && SignupBtn.IsEnabled)
                    SignupBtn_Click(SignupBtn, null!);
            };

            this.Loaded += SignupPage_Loaded;
        }

        // ── Entrance animation ────────────────────────────────────────────────

        private void SignupPage_Loaded(object sender, RoutedEventArgs e)
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

        // ── Input filters ─────────────────────────────────────────────────────

        private void NameBox_TextChanging(TextBox sender, TextBoxTextChangingEventArgs args)
        {
            string current = sender.Text;
            string filtered = FilterLettersAndSpaces(current);

            if (filtered == current)
            {
                if (ErrorBar.IsOpen && ErrorBar.Message.Contains("Name may only"))
                    HideError();
                return;
            }

            int caret = sender.SelectionStart;
            sender.Text = filtered;
            sender.SelectionStart = Math.Max(0, Math.Min(
                caret - (current.Length - filtered.Length), filtered.Length));
            ShowError("Name may only contain letters and spaces.");
        }

        private void CnicBox_TextChanging(TextBox sender, TextBoxTextChangingEventArgs args)
        {
            string current = sender.Text;
            string filtered = FilterDigits(current);

            if (filtered != current)
            {
                int caret = sender.SelectionStart;
                sender.Text = filtered;
                sender.SelectionStart = Math.Max(0, Math.Min(
                    caret - (current.Length - filtered.Length), filtered.Length));
                ShowError("CNIC may only contain digits (0–9).");
                return;
            }

            if (filtered.Length > 0 && filtered.Length != 13)
                ShowError($"CNIC must be exactly 13 digits ({filtered.Length}/13 entered).");
            else if (filtered.Length == 13 && ErrorBar.IsOpen &&
                     (ErrorBar.Message.Contains("CNIC may only") || ErrorBar.Message.Contains("CNIC must be")))
                HideError();
        }

        private void PhoneBox_TextChanging(TextBox sender, TextBoxTextChangingEventArgs args)
        {
            string current = sender.Text;
            string filtered = FilterDigits(current);

            if (filtered != current)
            {
                int caret = sender.SelectionStart;
                sender.Text = filtered;
                sender.SelectionStart = Math.Max(0, Math.Min(
                    caret - (current.Length - filtered.Length), filtered.Length));
                ShowError("Phone number may only contain digits (0–9).");
                return;
            }

            if (filtered.Length > 0 && filtered.Length != 11)
                ShowError($"Phone must be exactly 11 digits ({filtered.Length}/11 entered).");
            else if (filtered.Length == 11 && ErrorBar.IsOpen &&
                     (ErrorBar.Message.Contains("Phone number may only") || ErrorBar.Message.Contains("Phone must be")))
                HideError();
        }

        // ── Filter helpers ────────────────────────────────────────────────────

        private static string FilterLettersAndSpaces(string input)
        {
            var sb = new System.Text.StringBuilder(input.Length);
            foreach (char c in input)
                if (char.IsLetter(c) || c == ' ') sb.Append(c);
            return sb.ToString();
        }

        private static string FilterDigits(string input)
        {
            var sb = new System.Text.StringBuilder(input.Length);
            foreach (char c in input)
                if (char.IsDigit(c)) sb.Append(c);
            return sb.ToString();
        }

        // ── Password strength ─────────────────────────────────────────────────

        private static bool IsPasswordStrong(string password, out string reason)
        {
            reason = string.Empty;

            if (password.Length < 8)
            {
                reason = "Password must be at least 8 characters long.";
                return false;
            }

            bool hasLetter = false, hasDigit = false, hasSymbol = false;
            foreach (char c in password)
            {
                if (char.IsLetter(c)) hasLetter = true;
                else if (char.IsDigit(c)) hasDigit = true;
                else hasSymbol = true;
            }

            if (!hasLetter) { reason = "Password must contain at least one letter (a–z or A–Z)."; return false; }
            if (!hasDigit) { reason = "Password must contain at least one number (0–9)."; return false; }
            if (!hasSymbol) { reason = "Password must contain at least one symbol (e.g. @, #, !, $)."; return false; }
            return true;
        }

        // ── Validation ────────────────────────────────────────────────────────

        private bool ValidateInputs(string name, string cnic, string phone,
                                    string password, string confirm)
        {
            if (string.IsNullOrWhiteSpace(name))
            { ShowError("Full name is required."); return false; }

            if (cnic.Length != 13 || !Regex.IsMatch(cnic, @"^\d{13}$"))
            { ShowError("CNIC must be exactly 13 digits (e.g., 4210112345678)."); return false; }

            if (phone.Length != 11 || !Regex.IsMatch(phone, @"^\d{11}$"))
            { ShowError("Phone number must be exactly 11 digits (e.g., 03001234567)."); return false; }

            if (!IsPasswordStrong(password, out string pwdReason))
            { ShowError(pwdReason); return false; }

            if (password != confirm)
            { ShowError("Passwords do not match."); return false; }

            return true;
        }

        // ── Signup handler ────────────────────────────────────────────────────

        private async void SignupBtn_Click(object sender, RoutedEventArgs? e)
        {
            string name = NameBox.Text.Trim();
            string cnic = CnicBox.Text.Trim();
            string phone = PhoneBox.Text.Trim();
            string password = PassBox.Password;
            string confirm = ConfirmBox.Password;

            HideError();
            if (!ValidateInputs(name, cnic, phone, password, confirm))
            {
                ShakeCard();
                return;
            }

            SignupBtn.IsEnabled = false;
            SignupBtn.Content = "Creating account…";

            try
            {
                string result = await Task.Run(() =>
                    App.Bank.Execute("signup", name, cnic, phone, password));

                bool success =
                    result.Contains("Success", StringComparison.OrdinalIgnoreCase) ||
                    result.Contains("Account created", StringComparison.OrdinalIgnoreCase);

                if (success)
                {
                    HideError();

                    // Parse Customer ID and Backup Code from result
                    string customerId = string.Empty;
                    string backupCode = string.Empty;

                    var idMatch = Regex.Match(result, @"Customer ID:\s*(CUST\d+)");
                    if (idMatch.Success) customerId = idMatch.Groups[1].Value;

                    var codeMatch = Regex.Match(result, @"Backup Code:\s*([A-Z0-9]+)");
                    if (codeMatch.Success) backupCode = codeMatch.Groups[1].Value;

                    // Show the beautiful success dialog
                    await ShowSignupSuccessDialog(customerId, backupCode);
                }
                else
                {
                    ShowError(result);
                    ShakeCard();
                }
            }
            catch (Exception ex)
            {
                ShowError($"Error: {ex.Message}");
                ShakeCard();
            }
            finally
            {
                SignupBtn.IsEnabled = true;
                SignupBtn.Content = "Create Account";
            }
        }

        // ── Show Signup Success Dialog ────────────────────────────────────────

        private async Task ShowSignupSuccessDialog(string customerId, string backupCode)
        {
            var dialog = new ContentDialog
            {
                Title = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
            {
                new FontIcon
                {
                    Glyph = "\uE73E",
                    FontSize = 18,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x10, 0xD9, 0x86))
                },
                new TextBlock
                {
                    Text = "Account Created Successfully",
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe UI Variable Display"),
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xF1, 0xF5, 0xFF))
                }
            }
                },
                Content = new StackPanel
                {
                    Spacing = 16,
                    Children =
            {
                new TextBlock
                {
                    Text = "Your account has been created successfully. Please save the following credentials securely:",
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe UI Variable Text"),
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x9C, 0xA3, 0xAF)),
                    FontSize = 13
                },
                new StackPanel
                {
                    Spacing = 6,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Customer ID",
                            FontSize = 10,
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe UI Variable Text"),
                            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x6B, 0x72, 0x80)),
                            CharacterSpacing = 100
                        },
                        new Border
                        {
                            CornerRadius = new CornerRadius(8),
                            Padding = new Thickness(16, 12, 16, 12),
                            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x20, 0x3B, 0x82, 0xF6)),
                            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x3B, 0x82, 0xF6)),
                            BorderThickness = new Thickness(1),
                            Child = new TextBlock
                            {
                                Text = customerId,
                                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Code, Consolas, monospace"),
                                FontSize = 18,
                                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x3B, 0x82, 0xF6)),
                                HorizontalAlignment = HorizontalAlignment.Center,
                                CharacterSpacing = 200
                            }
                        }
                    }
                },
                new StackPanel
                {
                    Spacing = 6,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Backup Code (Save This!)",
                            FontSize = 10,
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe UI Variable Text"),
                            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x6B, 0x72, 0x80)),
                            CharacterSpacing = 100
                        },
                        new Border
                        {
                            CornerRadius = new CornerRadius(8),
                            Padding = new Thickness(16, 12, 16, 12),
                            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x20, 0x10, 0xD9, 0x86)),
                            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x10, 0xD9, 0x86)),
                            BorderThickness = new Thickness(1),
                            Child = new TextBlock
                            {
                                Text = backupCode,
                                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Code, Consolas, monospace"),
                                FontSize = 20,
                                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x10, 0xD9, 0x86)),
                                HorizontalAlignment = HorizontalAlignment.Center,
                                CharacterSpacing = 200
                            }
                        }
                    }
                },
                new TextBlock
                {
                    Text = "⚠️ These credentials are shown only once. Save them securely!",
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe UI Variable Text"),
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xF5, 0x9E, 0x0B)),
                    FontSize = 12,
                    FontWeight = Microsoft.UI.Text.FontWeights.Medium
                }
            }
                },
                PrimaryButtonText = "Sign In Now",
                SecondaryButtonText = "Copy Credentials",
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot,
                RequestedTheme = ElementTheme.Dark
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                NavigateToLoginWithAnimation();
            }
            else if (result == ContentDialogResult.Secondary)
            {
                try
                {
                    var dataPackage = new DataPackage();
                    dataPackage.SetText($"Customer ID: {customerId}\nBackup Code: {backupCode}");
                    Clipboard.SetContent(dataPackage);

                    // ✅ GREEN success message
                    ShowSuccess("Credentials copied to clipboard!");

                    // ✅ Auto-navigate after 3 seconds
                    await Task.Delay(3000);
                    NavigateToLoginWithAnimation();
                }
                catch
                {
                    ShowError("Failed to copy credentials.");
                }
            }
        }

        private void LoginLink_Click(object sender, RoutedEventArgs e)
            => Frame.Navigate(
                   typeof(LoginPage),
                   null,
                   new SlideNavigationTransitionInfo
                   {
                       Effect = SlideNavigationTransitionEffect.FromLeft
                   });

        // ── Show Success Feedback (Green InfoBar) ─────────────────────────────
        private void ShowSuccess(string message)
        {
            ErrorBar.Severity = InfoBarSeverity.Success;  // ✅ Green!
            ErrorBar.Message = message;
            ErrorBar.IsOpen = true;
        }

        // ── Navigate to Login with Slide Animation ────────────────────────────
        private void NavigateToLoginWithAnimation()
        {
            Frame.Navigate(
                typeof(LoginPage),
                null,
                new SlideNavigationTransitionInfo
                {
                    Effect = SlideNavigationTransitionEffect.FromLeft  // ✅ Same animation as other navs
                });
        }

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

        // ── Feedback helpers ──────────────────────────────────────────────────

        private void ShowError(string message)
        {
            ErrorBar.Message = message;
            ErrorBar.IsOpen = true;
        }

        private void HideError() => ErrorBar.IsOpen = false;
    }
}