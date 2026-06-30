using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Threading.Tasks;

namespace ModernUI
{
    public sealed partial class LoginPage : Page
    {
        public LoginPage()
        {
            this.InitializeComponent();
            IdBox.TextChanging += IdBox_TextChanging;
            PassBox.KeyDown += (s, e) =>
            {
                if (e.Key == Windows.System.VirtualKey.Enter && LoginBtn.IsEnabled)
                    LoginBtn_Click(LoginBtn, null!);
            };
            this.Loaded += LoginPage_Loaded;
        }

        // ── Entrance animation ────────────────────────────────────────────

        private void LoginPage_Loaded(object sender, RoutedEventArgs e)
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

        // ── Input filter ──────────────────────────────────────────────────

        private void IdBox_TextChanging(TextBox sender, TextBoxTextChangingEventArgs args)
        {
            string current = sender.Text;
            string filtered = FilterId(current);
            if (filtered == current)
            {
                if (ErrorBar.IsOpen && ErrorBar.Message.Contains("ID may only"))
                    HideError();
                return;
            }
            int caret = sender.SelectionStart;
            sender.Text = filtered;
            sender.SelectionStart = Math.Max(0,
                Math.Min(caret - (current.Length - filtered.Length), filtered.Length));
            ShowError("ID may only contain letters, digits, hyphens, and underscores.");
        }

        private static string FilterId(string input)
        {
            var sb = new System.Text.StringBuilder(input.Length);
            foreach (char c in input)
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
                    sb.Append(c);
            return sb.ToString();
        }

        // ── Login handler ─────────────────────────────────────────────────

        private async void LoginBtn_Click(object sender, RoutedEventArgs? e)
        {
            HideError();
            LoginBtn.IsEnabled = false;
            LoginBtn.Content = "Signing in…";

            try
            {
                string role = RoleCombo.SelectedIndex == 0 ? "customer" : "admin";
                string id = IdBox.Text.Trim();
                string pass = PassBox.Password;

                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(pass))
                {
                    ShowError("Please enter your ID and password.");
                    ShakeCard(); // SHAKE on empty field submission
                    return;
                }

                await Task.Delay(180);

                string result = App.Bank.Execute("login", id, pass, role);

                // Detect lockout message from the DLL
                if (result.StartsWith("Error: Too many", StringComparison.OrdinalIgnoreCase))
                {
                    ShowError(result.Substring(7)); // strip "Error: " prefix
                    ShakeCard();
                    return;
                }

                if (result.StartsWith("Error:", StringComparison.OrdinalIgnoreCase) ||
                    result.StartsWith("Invalid", StringComparison.OrdinalIgnoreCase))
                {
                    ShakeCard();
                    ShowError("Invalid credentials. Please verify your ID, password, and login type.");
                    return;
                }

                await AnimateCardExitAsync();

                if (role == "admin")
                {
                    Frame.Navigate(typeof(AdminDashboardPage), null,
                        new SlideNavigationTransitionInfo
                        { Effect = SlideNavigationTransitionEffect.FromRight });
                }
                else
                {
                    string displayName = ExtractCustomerName(result, id);
                    Frame.Navigate(
                        typeof(CustomerDashboardPage),
                        new CustomerDashboardPage.NavigationArgs(id, displayName),
                        new SlideNavigationTransitionInfo
                        { Effect = SlideNavigationTransitionEffect.FromRight });
                }
            }
            catch (Exception ex)
            {
                ShowError($"Unexpected error: {ex.Message}");
                ShakeCard(); // SHAKE on unexpected errors too
            }
            finally
            {
                if (Frame.CurrentSourcePageType == typeof(LoginPage))
                {
                    LoginBtn.IsEnabled = true;
                    LoginBtn.Content = "Sign In";
                }
            }
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

        // ── Exit animation ────────────────────────────────────────────────

        private async Task AnimateCardExitAsync()
        {
            if (MainCard is null) return;
            if (MainCard.RenderTransform is not Microsoft.UI.Xaml.Media.TranslateTransform)
                MainCard.RenderTransform = new Microsoft.UI.Xaml.Media.TranslateTransform();

            var tcs = new TaskCompletionSource<bool>();
            var sb = new Storyboard();

            var fadeOut = new DoubleAnimation
            {
                To = 0,
                Duration = new Duration(TimeSpan.FromSeconds(0.28)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(fadeOut, MainCard);
            Storyboard.SetTargetProperty(fadeOut, "Opacity");
            sb.Children.Add(fadeOut);

            var slideUp = new DoubleAnimation
            {
                To = -20,
                Duration = new Duration(TimeSpan.FromSeconds(0.28)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(slideUp, MainCard.RenderTransform);
            Storyboard.SetTargetProperty(slideUp, "Y");
            sb.Children.Add(slideUp);

            sb.Completed += (_, _) => tcs.TrySetResult(true);
            sb.Begin();
            await tcs.Task;
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static string ExtractCustomerName(string response, string fallbackId)
        {
            if (string.IsNullOrWhiteSpace(response)) return fallbackId;
            const string prefix = "Success:";
            if (!response.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return fallbackId;
            string name = response.Substring(prefix.Length).Trim();
            return string.IsNullOrWhiteSpace(name) ||
                   name.Equals("Login successful", StringComparison.OrdinalIgnoreCase)
                ? fallbackId : name;
        }

        private void SignupLink_Click(object sender, RoutedEventArgs e)
            => Frame.Navigate(typeof(SignupPage), null,
                   new SlideNavigationTransitionInfo
                   { Effect = SlideNavigationTransitionEffect.FromRight });

        private void ForgotLink_Click(object sender, RoutedEventArgs e)
            => Frame.Navigate(typeof(ForgotPasswordPage), null,
                   new SlideNavigationTransitionInfo
                   { Effect = SlideNavigationTransitionEffect.FromRight });

        private void ShowError(string message)
        {
            ErrorBar.Severity = InfoBarSeverity.Error;
            ErrorBar.Message = message;
            ErrorBar.IsOpen = true;
            // FIX: ensure dark theme so InfoBar renders correctly on light system themes
            ErrorBar.RequestedTheme = ElementTheme.Dark;
        }

        private void HideError() => ErrorBar.IsOpen = false;
    }
}