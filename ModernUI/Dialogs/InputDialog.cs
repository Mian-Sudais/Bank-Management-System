using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics;
using Windows.UI;

namespace ModernUI.Dialogs
{
    /// <summary>
    /// Reusable input dialog utilities for Admin and Customer workflows.
    /// Obsidian-glass styled, consistent with the 2030 design system.
    /// </summary>
    internal static class InputDialog
    {
        // ── Design tokens (BRIGHTER for better visibility) ─────────────────

        private static readonly SolidColorBrush BrushTextPrimary =
            new SolidColorBrush(Color.FromArgb(0xFF, 0xF1, 0xF5, 0xFF));

        private static readonly SolidColorBrush BrushTextSecondary =
            new SolidColorBrush(Color.FromArgb(0xFF, 0xA8, 0xB2, 0xC7)); // Brighter: was #7A8BAA

        private static readonly SolidColorBrush BrushTextMuted =
            new SolidColorBrush(Color.FromArgb(0xFF, 0x6B, 0x72, 0x80)); // Brighter: was #3D4F6B

        private static readonly FontFamily DefaultFont =
            new FontFamily("Segoe UI Variable Text");

        private static readonly FontFamily DisplayFont =
            new FontFamily("Segoe UI Variable Display");

        // ── Field factories ──────────────────────────────────────────────────

        private static TextBox CreateStyledTextBox(string placeholder,
            Thickness margin = default)
        {
            var tb = new TextBox
            {
                PlaceholderText = placeholder,
                Margin = margin,
                FontSize = 14,
                FontFamily = DefaultFont,
                Foreground = BrushTextPrimary,
                Background = new SolidColorBrush(Colors.Transparent),
                MinHeight = 46,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            if (Application.Current.Resources.TryGetValue("StyledTextBox", out var style))
                tb.Style = style as Style;
            return tb;
        }

        private static PasswordBox CreateStyledPasswordBox(string placeholder,
            Thickness margin = default)
        {
            var pb = new PasswordBox
            {
                PlaceholderText = placeholder,
                Margin = margin,
                FontSize = 14,
                FontFamily = DefaultFont,
                Foreground = BrushTextPrimary,
                Background = new SolidColorBrush(Colors.Transparent),
                MinHeight = 46,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            if (Application.Current.Resources.TryGetValue("StyledPasswordBox", out var style))
                pb.Style = style as Style;
            return pb;
        }

        // ── Label factory ────────────────────────────────────────────────────

        private static TextBlock CreateFieldLabel(string text) => new TextBlock
        {
            Text = text.ToUpperInvariant(),
            FontSize = 10,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontFamily = DefaultFont,
            Foreground = BrushTextSecondary, // Brighter: was BrushTextMuted
            CharacterSpacing = 100,
            Margin = new Thickness(2, 0, 0, 6)
        };

        // ── Dialog factory ───────────────────────────────────────────────────

        private static ContentDialog CreateStyledDialog(
            XamlRoot xamlRoot,
            string title,
            UIElement content,
            string primaryButtonText,
            string closeButtonText = "Cancel")
        {
            return new ContentDialog
            {
                Title = new TextBlock
                {
                    Text = title,
                    FontSize = 17,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    FontFamily = DisplayFont,
                    Foreground = BrushTextPrimary,
                    TextWrapping = TextWrapping.Wrap
                },
                Content = content,
                PrimaryButtonText = primaryButtonText,
                CloseButtonText = closeButtonText,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = xamlRoot,
                RequestedTheme = ElementTheme.Dark,
            };
        }

        // ════════════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ════════════════════════════════════════════════════════════════════

        public static async System.Threading.Tasks.Task<string?> ShowAsync(
            XamlRoot xamlRoot,
            string title,
            string placeholder,
            string confirmLabel = "OK")
        {
            try
            {
                Debug.WriteLine($"[InputDialog] Single › '{title}'");
                var input = CreateStyledTextBox(placeholder, new Thickness(0, 14, 0, 0));
                var dialog = CreateStyledDialog(xamlRoot, title, input, confirmLabel);
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    string v = input.Text.Trim();
                    return string.IsNullOrWhiteSpace(v) ? null : v;
                }
                return null;
            }
            catch (Exception ex)
            { Debug.WriteLine($"[InputDialog] ERROR: {ex.Message}"); return null; }
        }

        public static async System.Threading.Tasks.Task<(string First, string Second)?> ShowDoubleAsync(
            XamlRoot xamlRoot,
            string title,
            string label1,
            string placeholder1,
            string label2,
            string placeholder2,
            string confirmLabel = "OK")
        {
            try
            {
                Debug.WriteLine($"[InputDialog] Double › '{title}'");
                var box1 = CreateStyledTextBox(placeholder1);
                var box2 = CreateStyledTextBox(placeholder2);

                var lbl2 = CreateFieldLabel(label2);
                lbl2.Margin = new Thickness(2, 18, 0, 6);

                var panel = new StackPanel
                {
                    Spacing = 0,
                    Margin = new Thickness(0, 10, 0, 0),
                    Children = { CreateFieldLabel(label1), box1, lbl2, box2 }
                };

                var dialog = CreateStyledDialog(xamlRoot, title, panel, confirmLabel);
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                    return (box1.Text.Trim(), box2.Text.Trim());
                return null;
            }
            catch (Exception ex)
            { Debug.WriteLine($"[InputDialog] ERROR: {ex.Message}"); return null; }
        }

        public static System.Threading.Tasks.Task<(string First, string Second)?> ShowDoubleAsync(
            XamlRoot xamlRoot,
            string title,
            string placeholder1,
            string placeholder2,
            string confirmLabel = "OK")
            => ShowDoubleAsync(xamlRoot, title,
                   placeholder1, placeholder1,
                   placeholder2, placeholder2,
                   confirmLabel);

        public static async System.Threading.Tasks.Task<(string A, string B, string C)?> ShowTripleAsync(
            XamlRoot xamlRoot,
            string title,
            string label1,
            string placeholder1,
            string label2,
            string placeholder2,
            string label3,
            string placeholder3,
            string confirmLabel = "OK")
        {
            try
            {
                Debug.WriteLine($"[InputDialog] Triple › '{title}'");
                var b1 = CreateStyledTextBox(placeholder1);
                var b2 = CreateStyledTextBox(placeholder2);
                var b3 = CreateStyledTextBox(placeholder3);

                var lbl2 = CreateFieldLabel(label2); lbl2.Margin = new Thickness(2, 18, 0, 6);
                var lbl3 = CreateFieldLabel(label3); lbl3.Margin = new Thickness(2, 18, 0, 6);

                var panel = new StackPanel
                {
                    Spacing = 0,
                    Margin = new Thickness(0, 10, 0, 0),
                    Children = { CreateFieldLabel(label1), b1, lbl2, b2, lbl3, b3 }
                };

                var dialog = CreateStyledDialog(xamlRoot, title, panel, confirmLabel);
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                    return (b1.Text.Trim(), b2.Text.Trim(), b3.Text.Trim());
                return null;
            }
            catch (Exception ex)
            { Debug.WriteLine($"[InputDialog] ERROR: {ex.Message}"); return null; }
        }

        public static System.Threading.Tasks.Task<(string A, string B, string C)?> ShowTripleAsync(
            XamlRoot xamlRoot,
            string title,
            string placeholder1,
            string placeholder2,
            string placeholder3,
            string confirmLabel = "OK")
            => ShowTripleAsync(xamlRoot, title,
                   placeholder1, placeholder1,
                   placeholder2, placeholder2,
                   placeholder3, placeholder3,
                   confirmLabel);

        public static async System.Threading.Tasks.Task<string?> ShowPasswordAsync(
            XamlRoot xamlRoot,
            string title,
            string placeholder = "Enter password",
            string label = "Password",
            string confirmLabel = "Confirm")
        {
            try
            {
                Debug.WriteLine($"[InputDialog] Password › '{title}'");
                var pwBox = CreateStyledPasswordBox(placeholder);
                var panel = new StackPanel
                {
                    Spacing = 0,
                    Margin = new Thickness(0, 10, 0, 0),
                    Children = { CreateFieldLabel(label), pwBox }
                };
                var dialog = CreateStyledDialog(xamlRoot, title, panel, confirmLabel);
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    string v = pwBox.Password;
                    return string.IsNullOrWhiteSpace(v) ? null : v;
                }
                return null;
            }
            catch (Exception ex)
            { Debug.WriteLine($"[InputDialog] ERROR: {ex.Message}"); return null; }
        }

        public static async System.Threading.Tasks.Task<bool> ShowConfirmAsync(
            XamlRoot xamlRoot,
            string title,
            string message,
            string confirmLabel = "Confirm",
            string cancelLabel = "Cancel")
        {
            try
            {
                var body = new TextBlock
                {
                    Text = message,
                    FontSize = 14,
                    FontFamily = DefaultFont,
                    Foreground = BrushTextSecondary, // Brighter
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 10, 0, 0)
                };
                var dialog = CreateStyledDialog(xamlRoot, title, body, confirmLabel, cancelLabel);
                var result = await dialog.ShowAsync();
                return result == ContentDialogResult.Primary;
            }
            catch (Exception ex)
            { Debug.WriteLine($"[InputDialog] ERROR: {ex.Message}"); return false; }
        }
    }
}