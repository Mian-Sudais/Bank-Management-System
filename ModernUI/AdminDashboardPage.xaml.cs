using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using ModernUI.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI;

namespace ModernUI
{
    public sealed partial class AdminDashboardPage : Page
    {
        public AdminDashboardPage() { this.InitializeComponent(); }

        // ════════════════════════════════════════════════════════════════
        //  TABLE RENDERING ENGINE
        //  Parses the pipe-delimited text output from the C++ backend
        //  and builds a proper WinUI 3 table inside TablePanel.
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Column definitions for each command type.
        /// Key   = the command name passed to ShowResult().
        /// Value = ordered list of (Header label, field index in the pipe row).
        ///
        /// NOTE: "customers" has a virtual "Accounts" column (index -1) that is
        /// handled specially in BuildTable — it renders a clickable badge instead
        /// of a plain text cell and is NOT backed by a parsed field index.
        /// </summary>
        private static readonly Dictionary<string, (string Label, int FieldIndex)[]> ColumnMap =
            new()
            {
                // "list" / "search" → customer rows:
                // backend format:  CUSTXXX | Name | CNIC
                // Column index -1  = special "Accounts" badge column (no pipe field)
                ["customers"] = new[]
                {
                    ("Customer ID",  0),
                    ("Name",         1),
                    ("CNIC",         2),
                    ("Accounts",    -1),   // ← virtual column: clickable account-count badge
                },

                // "list_accounts" → account rows:
                // backend format:  ACC2000 | Savings | Balance: $5000 | Active | Opened: ...
                ["accounts"] = new[]
                {
                    ("Account No",   0),
                    ("Type",         1),
                    ("Balance",      2),
                    ("Status",       3),
                },

                // "history" → transaction rows:
                // backend format:  TXN5000 | Deposit | 500 | 2026-05-17 | ACC2000
                ["transactions"] = new[]
                {
                    ("Txn ID",       0),
                    ("Type",         1),
                    ("Amount",       2),
                    ("Date",         3),
                    ("Account",      4),
                },

                // "pending_loans" → parsed from the block format
                ["loans"] = new[]
                {
                    ("Loan ID",      0),
                    ("Customer ID",  1),
                    ("Account",      2),
                    ("Principal",    3),
                    ("Rate %",       4),
                    ("Tenure (mo)",  5),
                    ("EMI",          6),
                    ("Status",       7),
                },
            };

        // ────────────────────────────────────────────────────────────────
        //  ShowResult — the single entry point used by every action handler
        //
        //  mode:    one of "customers", "accounts", "transactions",
        //           "loans", or "" (plain text fallback)
        //  rawText: whatever the C++ backend returned
        // ────────────────────────────────────────────────────────────────
        private void ShowResult(string mode, string rawText)
        {
            // Always reset first
            TablePanel.Visibility = Visibility.Collapsed;
            PlainTextPanel.Visibility = Visibility.Visible;
            RowCountBadge.Visibility = Visibility.Collapsed;
            TableRows.Items.Clear();
            HeaderGrid.ColumnDefinitions.Clear();
            HeaderGrid.Children.Clear();

            if (string.IsNullOrWhiteSpace(rawText) ||
                rawText.StartsWith("Error:", StringComparison.OrdinalIgnoreCase) ||
                rawText.StartsWith("Warning:", StringComparison.OrdinalIgnoreCase) ||
                !ColumnMap.ContainsKey(mode))
            {
                ResultText.Text = rawText;
                UpdateTableTitle(mode, isTable: false);
                return;
            }

            List<string[]> rows = mode switch
            {
                "loans" => ParseLoanBlocks(rawText),
                "customers" => ParsePipeLines(rawText, prefixStrip: true),
                "accounts" => ParseAccountLines(rawText),
                "transactions" => ParseTransactionLines(rawText),
                _ => new List<string[]>()
            };

            if (rows.Count == 0)
            {
                ResultText.Text = rawText;
                UpdateTableTitle(mode, isTable: false);
                return;
            }

            BuildTable(mode, rows);
            UpdateTableTitle(mode, isTable: true, rowCount: rows.Count);

            TablePanel.Visibility = Visibility.Visible;
            PlainTextPanel.Visibility = Visibility.Collapsed;
        }

        // ────────────────────────────────────────────────────────────────
        //  Table builder
        // ────────────────────────────────────────────────────────────────
        private void BuildTable(string mode, List<string[]> rows)
        {
            var cols = ColumnMap[mode];
            int colCount = cols.Length;

            // ── Column widths (star layout) ──────────────────────────────
            for (int c = 0; c < colCount; c++)
                HeaderGrid.ColumnDefinitions.Add(
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // ── Header cells ─────────────────────────────────────────────
            for (int c = 0; c < colCount; c++)
            {
                var tb = new TextBlock
                {
                    Text = cols[c].Label,
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    FontFamily = new FontFamily("Segoe UI Variable Text"),
                    Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x6B, 0x72, 0x80)),
                    CharacterSpacing = 80,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(c == 0 ? 0 : 8, 0, 8, 0)
                };
                Grid.SetColumn(tb, c);
                HeaderGrid.Children.Add(tb);
            }

            // ── Data rows ────────────────────────────────────────────────
            bool alternate = false;
            foreach (var row in rows)
            {
                var rowGrid = new Grid { Margin = new Thickness(20, 0, 20, 0) };
                for (int c = 0; c < colCount; c++)
                    rowGrid.ColumnDefinitions.Add(
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                for (int c = 0; c < colCount; c++)
                {
                    int fieldIndex = cols[c].FieldIndex;
                    string cellValue = (fieldIndex >= 0 && fieldIndex < row.Length)
                                        ? row[fieldIndex]
                                        : "—";

                    // ── Special virtual column: Accounts badge ────────────
                    // FieldIndex == -1 means this is the clickable accounts
                    // count badge on the customers table.
                    if (fieldIndex == -1 && mode == "customers")
                    {
                        // Customer ID is always field 0
                        string custId = row.Length > 0 ? row[0] : "";

                        // Count accounts for this customer from the backend
                        int accountCount = GetCustomerAccountCount(custId);

                        // Build the clickable badge
                        var badgeText = new TextBlock
                        {
                            Text = accountCount.ToString(),
                            FontSize = 11,
                            FontWeight = FontWeights.SemiBold,
                            FontFamily = new FontFamily("Segoe UI Variable Text"),
                            Foreground = new SolidColorBrush(
                                accountCount == 0
                                ? Color.FromArgb(0xFF, 0x6B, 0x72, 0x80)   // grey if zero
                                : Color.FromArgb(0xFF, 0x3B, 0x82, 0xF6)), // blue if has accounts
                        };

                        var badge = new Border
                        {
                            CornerRadius = new CornerRadius(6),
                            Padding = new Thickness(10, 3, 10, 3),
                            Background = new SolidColorBrush(
                                accountCount == 0
                                ? Color.FromArgb(0x18, 0x6B, 0x72, 0x80)
                                : Color.FromArgb(0x20, 0x3B, 0x82, 0xF6)),
                            HorizontalAlignment = HorizontalAlignment.Left,
                            VerticalAlignment = VerticalAlignment.Center,
                            Child = badgeText,
                        };

                        // Note: WinUI 3 does not expose a public Cursor API on Border.
                        // The hover background shift below gives sufficient click affordance.

                        // Wire up click only when the customer has accounts
                        if (accountCount > 0)
                        {
                            string capturedCustId = custId; // capture for lambda
                            badge.PointerEntered += (s, e) =>
                            {
                                ((Border)s).Background = new SolidColorBrush(
                                    Color.FromArgb(0x35, 0x3B, 0x82, 0xF6));
                            };
                            badge.PointerExited += (s, e) =>
                            {
                                ((Border)s).Background = new SolidColorBrush(
                                    Color.FromArgb(0x20, 0x3B, 0x82, 0xF6));
                            };
                            badge.PointerPressed += async (s, e) =>
                            {
                                await ShowAccountsDialogAsync(capturedCustId);
                            };
                        }

                        Grid.SetColumn(badge, c);
                        rowGrid.Children.Add(badge);
                        continue; // skip the normal cell-building below
                    }

                    // ── Normal cells ──────────────────────────────────────

                    // Status / type pill coloring
                    bool isPill = false;
                    Color pillColor = Color.FromArgb(0xFF, 0x1E, 0x2D, 0x4A);
                    Color pillText = Color.FromArgb(0xFF, 0x9C, 0xA3, 0xAF);

                    if (mode == "accounts" && c == 3) // Status column
                    {
                        isPill = true;
                        if (cellValue.Equals("active", StringComparison.OrdinalIgnoreCase))
                        {
                            pillColor = Color.FromArgb(0x20, 0x10, 0xD9, 0x86);
                            pillText = Color.FromArgb(0xFF, 0x10, 0xD9, 0x86);
                        }
                        else if (cellValue.Equals("frozen", StringComparison.OrdinalIgnoreCase))
                        {
                            pillColor = Color.FromArgb(0x20, 0xF5, 0x9E, 0x0B);
                            pillText = Color.FromArgb(0xFF, 0xF5, 0x9E, 0x0B);
                        }
                    }
                    else if (mode == "loans" && c == 7) // Loan status column
                    {
                        isPill = true;
                        switch (cellValue.ToLowerInvariant())
                        {
                            case "pending":
                                pillColor = Color.FromArgb(0x20, 0x3B, 0x82, 0xF6);
                                pillText = Color.FromArgb(0xFF, 0x3B, 0x82, 0xF6);
                                break;
                            case "approved":
                                pillColor = Color.FromArgb(0x20, 0x10, 0xD9, 0x86);
                                pillText = Color.FromArgb(0xFF, 0x10, 0xD9, 0x86);
                                break;
                            case "rejected":
                            case "failed":
                                pillColor = Color.FromArgb(0x20, 0xF4, 0x3F, 0x5E);
                                pillText = Color.FromArgb(0xFF, 0xF4, 0x3F, 0x5E);
                                break;
                        }
                    }
                    else if (mode == "transactions" && c == 1) // Txn type column
                    {
                        isPill = true;
                        switch (cellValue.ToLowerInvariant())
                        {
                            case "deposit":
                                pillColor = Color.FromArgb(0x20, 0x10, 0xD9, 0x86);
                                pillText = Color.FromArgb(0xFF, 0x10, 0xD9, 0x86);
                                break;
                            case "withdraw":
                                pillColor = Color.FromArgb(0x20, 0xF4, 0x3F, 0x5E);
                                pillText = Color.FromArgb(0xFF, 0xF4, 0x3F, 0x5E);
                                break;
                            case "transfer":
                                pillColor = Color.FromArgb(0x20, 0x3B, 0x82, 0xF6);
                                pillText = Color.FromArgb(0xFF, 0x3B, 0x82, 0xF6);
                                break;
                        }
                    }

                    // FIX CS1503: cell must be FrameworkElement, not UIElement,
                    // because it is added to a Grid which requires FrameworkElement
                    // for Grid.SetColumn() to work.
                    FrameworkElement cell;
                    if (isPill)
                    {
                        cell = new Border
                        {
                            CornerRadius = new CornerRadius(6),
                            Padding = new Thickness(8, 3, 8, 3),
                            Background = new SolidColorBrush(pillColor),
                            HorizontalAlignment = HorizontalAlignment.Left,
                            VerticalAlignment = VerticalAlignment.Center,
                            Child = new TextBlock
                            {
                                Text = cellValue,
                                FontSize = 11,
                                FontWeight = FontWeights.SemiBold,
                                FontFamily = new FontFamily("Segoe UI Variable Text"),
                                Foreground = new SolidColorBrush(pillText),
                            }
                        };
                    }
                    else
                    {
                        cell = new TextBlock
                        {
                            Text = cellValue,
                            FontSize = 13,
                            FontFamily = new FontFamily("Segoe UI Variable Text"),
                            Foreground = new SolidColorBrush(
                                c == 0
                                ? Color.FromArgb(0xFF, 0xF1, 0xF5, 0xFF)   // first col brighter
                                : Color.FromArgb(0xFF, 0xA0, 0xAE, 0xC0)), // rest dimmer
                            VerticalAlignment = VerticalAlignment.Center,
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            Margin = new Thickness(c == 0 ? 0 : 8, 0, 8, 0)
                        };
                    }

                    Grid.SetColumn(cell, c);
                    rowGrid.Children.Add(cell);
                }

                // Wrap the row in a border (alternating shade)
                var rowBorder = new Border
                {
                    Padding = new Thickness(0, 10, 0, 10),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0x1E, 0x2D, 0x4A)),
                    Background = new SolidColorBrush(
                        alternate
                        ? Color.FromArgb(0x0A, 0x1E, 0x2D, 0x4A)
                        : Colors.Transparent),
                    Child = rowGrid
                };

                // FIX CS1503: ItemsControl.Items.Add expects object.
                // Wrapping in ContentPresenter ensures WinUI 3 treats it as
                // FrameworkElement during measure/arrange.
                TableRows.Items.Add(new ContentPresenter { Content = rowBorder });
                alternate = !alternate;
            }
        }

        // ────────────────────────────────────────────────────────────────
        //  GetCustomerAccountCount
        //  Calls the backend list_accounts command and counts the returned
        //  rows so we can display the badge number without a separate API.
        // ────────────────────────────────────────────────────────────────
        private static int GetCustomerAccountCount(string custId)
        {
            if (string.IsNullOrWhiteSpace(custId)) return 0;
            try
            {
                string raw = App.Bank.Execute("list_accounts", custId, "", "", "");
                if (string.IsNullOrWhiteSpace(raw)
                    || raw.StartsWith("Error:", StringComparison.OrdinalIgnoreCase)
                    || raw.StartsWith("No accounts", StringComparison.OrdinalIgnoreCase))
                    return 0;

                return ParseAccountLines(raw).Count;
            }
            catch { return 0; }
        }

        // ────────────────────────────────────────────────────────────────
        //  ShowAccountsDialogAsync
        //  Pops up a ContentDialog with a fully styled accounts table
        //  that exactly mirrors the main table's visual language.
        // ────────────────────────────────────────────────────────────────
        private async System.Threading.Tasks.Task ShowAccountsDialogAsync(string custId)
        {
            // ── Fetch & parse account data ────────────────────────────────
            string raw = "";
            List<string[]> accountRows = new();
            try
            {
                raw = App.Bank.Execute("list_accounts", custId, "", "", "");
                if (!string.IsNullOrWhiteSpace(raw)
                    && !raw.StartsWith("Error:", StringComparison.OrdinalIgnoreCase))
                    accountRows = ParseAccountLines(raw);
            }
            catch { /* fall through to empty state */ }

            // ── Column definitions for the accounts table ─────────────────
            var accountCols = new[]
            {
                ("Account No", 0),
                ("Type",       1),
                ("Balance",    2),
                ("Status",     3),
            };

            // ════════════════════════════════════════════════════════════
            //  Build the dialog content panel
            // ════════════════════════════════════════════════════════════
            var rootPanel = new StackPanel();

            // ── Header bar (mirrors main card header) ─────────────────────
            var headerBar = new Border
            {
                Padding = new Thickness(0, 0, 0, 12),
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x1E, 0x2D, 0x4A)),
                Margin = new Thickness(0, 0, 0, 0),
            };

            var headerStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
            };

            headerStack.Children.Add(new Border
            {
                Width = 6,
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x3B, 0x82, 0xF6)),
                VerticalAlignment = VerticalAlignment.Center,
            });

            headerStack.Children.Add(new TextBlock
            {
                Text = "ACCOUNTS",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Segoe UI Variable Text"),
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x6B, 0x72, 0x80)),
                CharacterSpacing = 120,
                VerticalAlignment = VerticalAlignment.Center,
            });

            // Record count badge
            var countBadge = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(4, 0, 0, 0),
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x1E, 0x2D, 0x4A)),
                Child = new TextBlock
                {
                    Text = $"{accountRows.Count} {(accountRows.Count == 1 ? "account" : "accounts")}",
                    FontSize = 10,
                    FontFamily = new FontFamily("Segoe UI Variable Text"),
                    Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x6B, 0x72, 0x80)),
                }
            };
            headerStack.Children.Add(countBadge);
            headerBar.Child = headerStack;
            rootPanel.Children.Add(headerBar);

            // ── Empty state ───────────────────────────────────────────────
            if (accountRows.Count == 0)
            {
                rootPanel.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(raw) || raw.StartsWith("Error:")
                                    ? "Could not load accounts."
                                    : "This customer has no accounts.",
                    FontSize = 13,
                    FontFamily = new FontFamily("Segoe UI Variable Text"),
                    Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x7A, 0x8B, 0xAA)),
                    Margin = new Thickness(0, 16, 0, 8),
                    TextWrapping = TextWrapping.Wrap,
                });
            }
            else
            {
                // ── Table header row ──────────────────────────────────────
                var tableHeaderBorder = new Border
                {
                    Padding = new Thickness(0, 10, 0, 10),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x1E, 0x2D, 0x4A)),
                    Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x06, 0x0B, 0x14)),
                    Margin = new Thickness(0, 8, 0, 0),
                };

                var headerGrid = new Grid { Margin = new Thickness(4, 0, 4, 0) };
                for (int c = 0; c < accountCols.Length; c++)
                    headerGrid.ColumnDefinitions.Add(
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                for (int c = 0; c < accountCols.Length; c++)
                {
                    var th = new TextBlock
                    {
                        Text = accountCols[c].Item1,
                        FontSize = 10,
                        FontWeight = FontWeights.SemiBold,
                        FontFamily = new FontFamily("Segoe UI Variable Text"),
                        Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x6B, 0x72, 0x80)),
                        CharacterSpacing = 80,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(c == 0 ? 0 : 8, 0, 8, 0),
                    };
                    Grid.SetColumn(th, c);
                    headerGrid.Children.Add(th);
                }

                tableHeaderBorder.Child = headerGrid;
                rootPanel.Children.Add(tableHeaderBorder);

                // ── Data rows ─────────────────────────────────────────────
                bool alternate = false;
                foreach (var row in accountRows)
                {
                    var rowGrid = new Grid { Margin = new Thickness(4, 0, 4, 0) };
                    for (int c = 0; c < accountCols.Length; c++)
                        rowGrid.ColumnDefinitions.Add(
                            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    for (int c = 0; c < accountCols.Length; c++)
                    {
                        string cellValue = (c < row.Length) ? row[c] : "—";

                        // Status pill (column 3)
                        FrameworkElement cell;
                        if (c == 3)
                        {
                            Color pillBg, pillFg;
                            if (cellValue.Equals("active", StringComparison.OrdinalIgnoreCase))
                            {
                                pillBg = Color.FromArgb(0x20, 0x10, 0xD9, 0x86);
                                pillFg = Color.FromArgb(0xFF, 0x10, 0xD9, 0x86);
                            }
                            else // frozen
                            {
                                pillBg = Color.FromArgb(0x20, 0xF5, 0x9E, 0x0B);
                                pillFg = Color.FromArgb(0xFF, 0xF5, 0x9E, 0x0B);
                            }

                            cell = new Border
                            {
                                CornerRadius = new CornerRadius(6),
                                Padding = new Thickness(8, 3, 8, 3),
                                Background = new SolidColorBrush(pillBg),
                                HorizontalAlignment = HorizontalAlignment.Left,
                                VerticalAlignment = VerticalAlignment.Center,
                                Child = new TextBlock
                                {
                                    Text = cellValue,
                                    FontSize = 11,
                                    FontWeight = FontWeights.SemiBold,
                                    FontFamily = new FontFamily("Segoe UI Variable Text"),
                                    Foreground = new SolidColorBrush(pillFg),
                                }
                            };
                        }
                        else
                        {
                            cell = new TextBlock
                            {
                                Text = cellValue,
                                FontSize = 13,
                                FontFamily = new FontFamily("Segoe UI Variable Text"),
                                Foreground = new SolidColorBrush(
                                    c == 0
                                    ? Color.FromArgb(0xFF, 0xF1, 0xF5, 0xFF)
                                    : Color.FromArgb(0xFF, 0xA0, 0xAE, 0xC0)),
                                VerticalAlignment = VerticalAlignment.Center,
                                TextTrimming = TextTrimming.CharacterEllipsis,
                                Margin = new Thickness(c == 0 ? 0 : 8, 0, 8, 0),
                            };
                        }

                        Grid.SetColumn(cell, c);
                        rowGrid.Children.Add(cell);
                    }

                    var rowBorder = new Border
                    {
                        Padding = new Thickness(0, 10, 0, 10),
                        BorderThickness = new Thickness(0, 0, 0, 1),
                        BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0x1E, 0x2D, 0x4A)),
                        Background = new SolidColorBrush(
                            alternate
                            ? Color.FromArgb(0x0A, 0x1E, 0x2D, 0x4A)
                            : Colors.Transparent),
                        Child = rowGrid,
                    };

                    rootPanel.Children.Add(rowBorder);
                    alternate = !alternate;
                }
            }

            // ════════════════════════════════════════════════════════════
            //  Wrap in ScrollViewer so long account lists stay scrollable
            // ════════════════════════════════════════════════════════════
            var scrollViewer = new ScrollViewer
            {
                Content = rootPanel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = 420,
                MinWidth = 560,
            };

            // ════════════════════════════════════════════════════════════
            //  Build the ContentDialog with matching dark theme
            // ════════════════════════════════════════════════════════════
            var dialog = new ContentDialog
            {
                // Title bar — customer ID + label
                Title = BuildDialogTitle(custId),

                Content = scrollViewer,
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot,
                RequestedTheme = ElementTheme.Dark,
            };

            await dialog.ShowAsync();
        }

        /// <summary>
        /// Builds the rich title element for the accounts dialog.
        /// "Accounts — CUSTXXX"
        /// </summary>
        private static FrameworkElement BuildDialogTitle(string custId)
        {
            var titlePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(0, 0, 0, 0),
            };

            titlePanel.Children.Add(new TextBlock
            {
                Text = "Accounts",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Segoe UI Variable Display"),
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xF1, 0xF5, 0xFF)),
                VerticalAlignment = VerticalAlignment.Center,
            });

            // Separator dash
            titlePanel.Children.Add(new TextBlock
            {
                Text = "—",
                FontSize = 14,
                FontFamily = new FontFamily("Segoe UI Variable Text"),
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x6B, 0x72, 0x80)),
                VerticalAlignment = VerticalAlignment.Center,
            });

            // Customer ID pill
            titlePanel.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 3, 8, 3),
                Background = new SolidColorBrush(Color.FromArgb(0x25, 0x3B, 0x82, 0xF6)),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = custId,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    FontFamily = new FontFamily("Cascadia Code, Consolas, Courier New"),
                    Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x3B, 0x82, 0xF6)),
                }
            });

            return titlePanel;
        }

        // ────────────────────────────────────────────────────────────────
        //  Title / badge helpers
        // ────────────────────────────────────────────────────────────────
        private void UpdateTableTitle(string mode, bool isTable, int rowCount = 0)
        {
            string label = mode switch
            {
                "customers" => "CUSTOMERS",
                "accounts" => "ACCOUNTS",
                "transactions" => "TRANSACTIONS",
                "loans" => "LOAN APPLICATIONS",
                _ => "OUTPUT"
            };
            TableTitleText.Text = label;

            if (isTable && rowCount > 0)
            {
                RowCountBadge.Visibility = Visibility.Visible;
                RowCountText.Text = $"{rowCount} {(rowCount == 1 ? "record" : "records")}";
            }
            else
            {
                RowCountBadge.Visibility = Visibility.Collapsed;
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  PARSERS — one per output format from the C++ backend
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Parses "CUSTXXX | Name | CNIC" lines.
        /// prefixStrip strips residual "ID: " / "CNIC: " label prefixes.
        /// </summary>
        private static List<string[]> ParsePipeLines(string raw, bool prefixStrip)
        {
            var result = new List<string[]>();
            foreach (var line in raw.Split('\n'))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                var parts = trimmed.Split('|');
                if (parts.Length < 2) continue;

                for (int i = 0; i < parts.Length; i++)
                    parts[i] = prefixStrip
                        ? StripKnownPrefixes(parts[i].Trim())
                        : parts[i].Trim();

                result.Add(parts);
            }
            return result;
        }

        /// <summary>
        /// Parses account display lines:
        /// "ACC2000 | Savings | Balance: $5000 | Active | Opened: ..."
        /// Produces [AccNum, Type, Balance, Status].
        /// No TrimStart hacks — backend outputs clean lines.
        /// </summary>
        private static List<string[]> ParseAccountLines(string raw)
        {
            var result = new List<string[]>();
            foreach (var line in raw.Split('\n'))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                var parts = trimmed.Split('|');
                if (parts.Length < 4) continue;

                string accNum = parts[0].Trim();
                string type = parts[1].Trim();

                // ✅ FIX: Format balance with FormatCurrency
                string balanceRaw = StripKnownPrefixes(parts[2].Trim());
                string balance = FormatCurrency(balanceRaw);  // ← ADDED

                string status = StripKnownPrefixes(parts[3].Trim());

                if (!accNum.StartsWith("ACC", StringComparison.OrdinalIgnoreCase)) continue;
                result.Add(new[] { accNum, type, balance, status });
            }
            return result;
        }

        /// <summary>
        /// 100% robust currency formatter. Uses C# decimal for exact rounding,
        /// adds thousand separators, and guarantees exactly 2 decimal places.
        /// </summary>
        private static string FormatCurrency(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "0.00";

            // Strip non-numeric chars except '.', '-', and '+'
            string cleaned = new string(raw.Where(c => char.IsDigit(c) || c == '.' || c == '-' || c == '+').ToArray());
            if (string.IsNullOrEmpty(cleaned) || cleaned is "-" or "+") return "0.00";

            if (decimal.TryParse(cleaned, System.Globalization.NumberStyles.Float,
                                 System.Globalization.CultureInfo.InvariantCulture, out decimal value))
            {
                // Snap to 2 decimals (AwayFromZero matches banking standards)
                value = Math.Round(value, 2, MidpointRounding.AwayFromZero);
                // Output with thousand separators and exactly 2 decimals
                return value.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
            }
            return "0.00";
        }

        /// <summary>
        /// Parses transaction receipt lines:
        /// "TXN5000 | Deposit | 500 | 2026-05-17 12:00:00 | ACC2000"
        /// </summary>
        private static List<string[]> ParseTransactionLines(string raw)
        {
            var result = new List<string[]>();
            foreach (var line in raw.Split('\n'))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                var parts = trimmed.Split('|');
                if (parts.Length < 4) continue;

                string txnId = parts[0].Trim();
                string type = parts[1].Trim();

                // ✅ FIX: Format amount with FormatCurrency
                string amountRaw = parts[2].Trim();
                string amount = FormatCurrency(amountRaw);  // ← ADDED

                string date = parts[3].Trim();
                string account = parts.Length > 4
                    ? StripKnownPrefixes(parts[4].Trim())
                    : "";

                if (!txnId.StartsWith("TXN", StringComparison.OrdinalIgnoreCase)) continue;
                result.Add(new[] { txnId, type, amount, date, account });
            }
            return result;
        }

        /// <summary>
        /// Parses the loan block format (delimited by ===... separators).
        /// </summary>
        private static List<string[]> ParseLoanBlocks(string raw)
        {
            var result = new List<string[]>();
            var blocks = raw.Split(
                new[] { "========================================" },
                StringSplitOptions.RemoveEmptyEntries);

            foreach (var block in blocks)
            {
                var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                string loanId = "", custId = "", account = "",
                       principal = "", rate = "", tenure = "", emi = "", status = "";

                foreach (var line in lines)
                {
                    var l = line.Trim();
                    if (l.StartsWith("Loan ID:")) loanId = l.Replace("Loan ID:", "").Trim();
                    else if (l.StartsWith("Customer ID:")) custId = l.Replace("Customer ID:", "").Trim();
                    else if (l.StartsWith("Account:")) account = l.Replace("Account:", "").Trim();
                    else if (l.StartsWith("Principal:"))
                    {
                        // ✅ FIX: Format principal with FormatCurrency
                        string pRaw = l.Replace("Principal:", "").Trim();
                        principal = FormatCurrency(pRaw);
                    }
                    else if (l.StartsWith("Interest Rate:")) rate = l.Replace("Interest Rate:", "").Trim();
                    else if (l.StartsWith("Tenure:")) tenure = l.Replace("Tenure:", "").Trim();
                    else if (l.StartsWith("Monthly EMI:"))
                    {
                        // ✅ FIX: Format EMI with FormatCurrency
                        string eRaw = l.Replace("Monthly EMI:", "").Trim();
                        emi = FormatCurrency(eRaw);
                    }
                    else if (l.StartsWith("Status:")) status = l.Replace("Status:", "").Trim();
                }

                if (string.IsNullOrEmpty(loanId)) continue;
                result.Add(new[] { loanId, custId, account, principal, rate, tenure, emi, status });
            }
            return result;
        }

        private static string StripKnownPrefixes(string s)
        {
            string[] prefixes =
            {
                "Balance: $", "Balance: ",
                "ID: ", "CNIC: ",
                "Status: ", "Account: ", "Opened: ", "Type: "
            };
            foreach (var p in prefixes)
                if (s.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                    return s.Substring(p.Length).Trim();
            return s;
        }

        // ════════════════════════════════════════════════════════════════
        //  Sidebar helpers
        // ════════════════════════════════════════════════════════════════
        private void SetPage(string subtitle) => PageSubtitleText.Text = subtitle;

        // ════════════════════════════════════════════════════════════════
        //  Action handlers
        // ════════════════════════════════════════════════════════════════

        private void ViewAllBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SetPage("All Customers");
                var raw = App.Bank.Execute("list", "", "", "", "");
                ShowResult("customers", raw);
            }
            catch (Exception ex) { ShowResult("", $"Error: {ex.Message}"); }
        }

        private void SearchBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SetPage("Search Customer");
                string query = SearchBox.Text.Trim();

                if (string.IsNullOrWhiteSpace(query))
                {
                    ShowResult("", "Please enter a customer name or CNIC to search.");
                    return;
                }

                var raw = App.Bank.Execute("search", query, "", "", "");
                ShowResult("customers", raw);
            }
            catch (Exception ex) { ShowResult("", $"Error: {ex.Message}"); }
        }

        private void SearchBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
                SearchBtn_Click(sender, e);
        }

        private void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = string.Empty;
            PageSubtitleText.Text = "Overview";
            ShowResult("", "Select an action from the sidebar to begin administrative tasks.");
        }

        private async void FreezeBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SetPage("Freeze Account");
                string? accNum = await InputDialog.ShowAsync(
                    this.XamlRoot, "Freeze Account",
                    "Enter account number to freeze (e.g. ACC2000)", "Freeze");

                if (string.IsNullOrWhiteSpace(accNum)) return;
                var raw = App.Bank.Execute("freeze", accNum.Trim(), "", "", "");
                ShowResult("", raw);
            }
            catch (Exception ex) { ShowResult("", $"Error: {ex.Message}"); }
        }

        private async void UnfreezeBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SetPage("Unfreeze Account");
                string? accNum = await InputDialog.ShowAsync(
                    this.XamlRoot, "Unfreeze Account",
                    "Enter account number to unfreeze (e.g. ACC2000)", "Unfreeze");

                if (string.IsNullOrWhiteSpace(accNum)) return;
                var raw = App.Bank.Execute("unfreeze", accNum.Trim(), "", "", "");
                ShowResult("", raw);
            }
            catch (Exception ex) { ShowResult("", $"Error: {ex.Message}"); }
        }

        private async void ApproveLoanBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SetPage("Loan Applications");
                var pendingRaw = App.Bank.Execute("pending_loans", "", "", "", "");

                // Show the table first so the admin can read loan IDs
                ShowResult("loans", pendingRaw);

                if (string.IsNullOrWhiteSpace(pendingRaw) ||
                    pendingRaw.Contains("No pending", StringComparison.OrdinalIgnoreCase))
                    return;

                string? loanId = await InputDialog.ShowAsync(
                    this.XamlRoot,
                    "Process Loan",
                    "Enter the Loan ID you want to process:",
                    "Process");

                if (string.IsNullOrWhiteSpace(loanId)) return;

                var dialog = new ContentDialog
                {
                    Title = new TextBlock
                    {
                        Text = $"Process Loan: {loanId.Trim()}",
                        FontWeight = FontWeights.SemiBold,
                        FontFamily = new FontFamily("Segoe UI Variable Display"),
                        Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xF1, 0xF5, 0xFF))
                    },
                    Content = new TextBlock
                    {
                        Text = "What would you like to do with this loan application?",
                        TextWrapping = TextWrapping.Wrap,
                        FontFamily = new FontFamily("Segoe UI Variable Text"),
                        Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x7A, 0x8B, 0xAA)),
                        Margin = new Thickness(0, 10, 0, 0)
                    },
                    PrimaryButtonText = "Approve and Disburse",
                    SecondaryButtonText = "Reject",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.XamlRoot,
                    RequestedTheme = ElementTheme.Dark,
                };

                var dlgResult = await dialog.ShowAsync();

                if (dlgResult == ContentDialogResult.Primary)
                    ShowResult("", App.Bank.Execute("approve_loan", loanId.Trim(), "", "", ""));
                else if (dlgResult == ContentDialogResult.Secondary)
                    ShowResult("", App.Bank.Execute("reject_loan", loanId.Trim(), "", "", ""));
            }
            catch (Exception ex) { ShowResult("", $"Error: {ex.Message}"); }
        }

        private void LogoutBtn_Click(object sender, RoutedEventArgs e)
        {
            try { App.Bank.Execute("logout", "", "", "", ""); }
            catch { /* best-effort */ }

            Frame.Navigate(
                typeof(LoginPage),
                null,
                new SlideNavigationTransitionInfo
                {
                    Effect = SlideNavigationTransitionEffect.FromLeft
                });
        }
    }
}