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
using System.Text.RegularExpressions;
using Windows.UI;

namespace ModernUI
{
    public sealed partial class CustomerDashboardPage : Page
    {
        public sealed record NavigationArgs(string CustomerId, string WelcomeMessage);

        private string _customerId = string.Empty;

        // ════════════════════════════════════════════════════════════════
        //  SESSION LOAN HISTORY — persists for the lifetime of the session
        //  Each entry: [LoanID, Account, Principal, Rate%, Tenure, EMI, Status]
        // ════════════════════════════════════════════════════════════════
        private readonly List<string[]> _sessionLoanHistory = new();

        public CustomerDashboardPage() { this.InitializeComponent(); }

        // ════════════════════════════════════════════════════════════════
        //  TABLE RENDERING ENGINE — COLUMN MAPPING
        // ════════════════════════════════════════════════════════════════

        private static readonly Dictionary<string, (string Label, int FieldIndex)[]> ColumnMap =
            new()
            {
                ["accounts"] = new[]
                {
                    ("Account No", 0), ("Type", 1), ("Balance", 2), ("Status", 3), ("Opened", 4)
                },
                ["transactions"] = new[]
                {
                    ("Txn ID", 0), ("Type", 1), ("Amount", 2), ("Date", 3), ("Account", 4)
                },
                ["open_account"] = new[]
                {
                    ("Account No", 0), ("Type", 1), ("Balance", 2), ("Status", 3), ("Opened", 4)
                },
                ["balance"] = new[]
                {
                    ("Account No", 0), ("Type", 1), ("Balance", 2), ("Status", 3)
                },
                ["deposit"] = new[]
                {
                    ("Txn ID", 0), ("Type", 1), ("Amount", 2), ("Account", 3), ("New Balance", 4), ("Status", 5)
                },
                ["withdraw"] = new[]
                {
                    ("Txn ID", 0), ("Type", 1), ("Amount", 2), ("Account", 3), ("New Balance", 4), ("Status", 5)
                },
                ["transfer"] = new[]
                {
                    ("Txn ID", 0), ("Type", 1), ("Amount", 2), ("From", 3), ("To", 4), ("Status", 5)
                },
                ["loan"] = new[]
                {
                    ("Loan ID", 0), ("Account", 1), ("Principal", 2), ("Rate %", 3), ("Tenure (mo)", 4), ("EMI", 5), ("Status", 6)
                },
            };

        // ════════════════════════════════════════════════════════════════
        //  ShowResult — Unified entry point
        // ════════════════════════════════════════════════════════════════
        private void ShowResult(string mode, string rawText, string contextAccount = "")
        {
            TablePanel.Visibility = Visibility.Collapsed;
            PlainTextPanel.Visibility = Visibility.Visible;
            RowCountBadge.Visibility = Visibility.Collapsed;
            TableRows.Items.Clear();
            HeaderGrid.ColumnDefinitions.Clear();
            HeaderGrid.Children.Clear();

            if (string.IsNullOrWhiteSpace(rawText) ||
                rawText.StartsWith("Error:", StringComparison.OrdinalIgnoreCase) ||
                rawText.StartsWith("Warning:", StringComparison.OrdinalIgnoreCase))
            {
                RenderPlainText(rawText, isSuccess: false, mode: mode);
                return;
            }

            bool hasTableDef = ColumnMap.ContainsKey(mode);

            List<string[]> parsedRows = hasTableDef ? mode switch
            {
                "accounts" => ParseAccountLines(rawText),
                "transactions" => ParseTransactionLines(rawText),
                "open_account" => ParseAccountLines(rawText),
                "balance" => ParseBalanceBlock(rawText),

                // ── Deposit / Withdraw: parse everything from the C++ response
                //    directly.  No extra backend calls needed.
                //    C++ format: "Success: Deposited: 500 to ACC2000 | New Balance: 28500"
                //    C++ format: "Success: Withdrawn: 500 from ACC2000 | New Balance: 28000"
                "deposit" => ParseDepositWithdrawResponse(rawText, "Deposit", contextAccount),
                "withdraw" => ParseDepositWithdrawResponse(rawText, "Withdraw", contextAccount),

                // ── Transfer: parse everything from the C++ response directly.
                //    C++ format: "Success: Transfer completed. 200 from ACC2000 to ACC2001"
                "transfer" => ParseTransferResponse(rawText, contextAccount),

                "loan" => new List<string[]>(_sessionLoanHistory),
                _ => new List<string[]>()
            } : new List<string[]>();

            if (!hasTableDef || parsedRows.Count == 0)
            {
                bool isSuccess = rawText.StartsWith("Success:", StringComparison.OrdinalIgnoreCase);
                RenderPlainText(rawText, isSuccess: isSuccess, mode: mode);
                return;
            }

            BuildTable(mode, parsedRows);
            UpdateTableTitle(mode, isTable: true, rowCount: parsedRows.Count);
            TablePanel.Visibility = Visibility.Visible;
            PlainTextPanel.Visibility = Visibility.Collapsed;
        }

        // ════════════════════════════════════════════════════════════════
        //  ShowLoanHistory
        // ════════════════════════════════════════════════════════════════
        private void ShowLoanHistory()
        {
            TablePanel.Visibility = Visibility.Collapsed;
            PlainTextPanel.Visibility = Visibility.Visible;
            RowCountBadge.Visibility = Visibility.Collapsed;
            TableRows.Items.Clear();
            HeaderGrid.ColumnDefinitions.Clear();
            HeaderGrid.Children.Clear();

            UpdateTableTitle("loan", isTable: false);

            // Fetch persisted loans from backend
            string raw = App.Bank.Execute("my_loans", "", "", "", "");
            List<string[]> rows = ParseMyLoanLines(raw);

            // Merge session rows not yet persisted (de-duplicate by Loan ID)
            foreach (var sr in _sessionLoanHistory)
            {
                bool alreadyIn = rows.Exists(r => r[0] == sr[0]);
                if (!alreadyIn) rows.Add(sr);
            }

            if (rows.Count == 0)
            {
                ResultText.FontFamily = new FontFamily("Segoe UI Variable Text");
                ResultText.FontSize = 13;
                ResultText.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x9C, 0xA3, 0xAF));
                ResultText.Text = "No loan applications found.\nFill in the form above to apply for your first loan.";
                PlainTextPanel.Visibility = Visibility.Visible;
                return;
            }

            BuildTable("loan", rows);
            UpdateTableTitle("loan", isTable: true, rowCount: rows.Count);
            TablePanel.Visibility = Visibility.Visible;
            PlainTextPanel.Visibility = Visibility.Collapsed;
        }

        private void RenderPlainText(string rawText, bool isSuccess, string mode)
        {
            ResultText.FontFamily = isSuccess
                ? new FontFamily("Segoe UI Variable Text")
                : new FontFamily("Cascadia Code, Consolas, Courier New");
            ResultText.FontSize = isSuccess ? 14 : 13;
            ResultText.Foreground = new SolidColorBrush(
                isSuccess
                    ? Color.FromArgb(0xFF, 0x10, 0xD9, 0x86)
                    : Color.FromArgb(0xFF, 0xF1, 0xF5, 0xFF));

            string display = rawText;
            if (isSuccess && rawText.StartsWith("Success: ", StringComparison.OrdinalIgnoreCase))
                display = rawText["Success: ".Length..].Trim();

            ResultText.Text = display;
            UpdateTableTitle(mode, isTable: false);
        }

        // ════════════════════════════════════════════════════════════════
        //  BuildTable
        // ════════════════════════════════════════════════════════════════
        private void BuildTable(string mode, List<string[]> rows)
        {
            var cols = ColumnMap[mode];
            int colCount = cols.Length;

            for (int c = 0; c < colCount; c++)
                HeaderGrid.ColumnDefinitions.Add(
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

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
                    string cellValue = (fieldIndex >= 0 && fieldIndex < row.Length) ? row[fieldIndex] : "—";

                    bool isPill = false;
                    Color pillColor = Color.FromArgb(0xFF, 0x1E, 0x2D, 0x4A);
                    Color pillText = Color.FromArgb(0xFF, 0x9C, 0xA3, 0xAF);

                    // Status pill — accounts / balance
                    if ((mode is "accounts" or "open_account" or "balance") && c == 3)
                    {
                        isPill = true;
                        if (cellValue.Equals("Active", StringComparison.OrdinalIgnoreCase))
                        { pillColor = Color.FromArgb(0x20, 0x10, 0xD9, 0x86); pillText = Color.FromArgb(0xFF, 0x10, 0xD9, 0x86); }
                        else if (cellValue.Equals("Frozen", StringComparison.OrdinalIgnoreCase))
                        { pillColor = Color.FromArgb(0x20, 0xF5, 0x9E, 0x0B); pillText = Color.FromArgb(0xFF, 0xF5, 0x9E, 0x0B); }
                        else
                        { pillColor = Color.FromArgb(0x20, 0x6B, 0x72, 0x80); pillText = Color.FromArgb(0xFF, 0x9C, 0xA3, 0xAF); }
                    }
                    // Type pill — transactions
                    else if (mode is "transactions" && c == 1)
                    {
                        isPill = true;
                        switch (cellValue.ToLowerInvariant())
                        {
                            case "deposit": pillColor = Color.FromArgb(0x20, 0x10, 0xD9, 0x86); pillText = Color.FromArgb(0xFF, 0x10, 0xD9, 0x86); break;
                            case "withdraw": pillColor = Color.FromArgb(0x20, 0xF4, 0x3F, 0x5E); pillText = Color.FromArgb(0xFF, 0xF4, 0x3F, 0x5E); break;
                            case "transfer": pillColor = Color.FromArgb(0x20, 0x3B, 0x82, 0xF6); pillText = Color.FromArgb(0xFF, 0x3B, 0x82, 0xF6); break;
                            default: pillColor = Color.FromArgb(0x20, 0x6B, 0x72, 0x80); pillText = Color.FromArgb(0xFF, 0x9C, 0xA3, 0xAF); break;
                        }
                    }
                    // Status pill — deposit / withdraw (col 5)
                    else if (mode is "deposit" or "withdraw" && c == 5)
                    {
                        isPill = true;
                        if (cellValue.Equals("Success", StringComparison.OrdinalIgnoreCase))
                        { pillColor = Color.FromArgb(0x20, 0x10, 0xD9, 0x86); pillText = Color.FromArgb(0xFF, 0x10, 0xD9, 0x86); }
                        else
                        { pillColor = Color.FromArgb(0x20, 0xF4, 0x3F, 0x5E); pillText = Color.FromArgb(0xFF, 0xF4, 0x3F, 0x5E); }
                    }
                    // Status pill — transfer (col 5)
                    else if (mode is "transfer" && c == 5)
                    {
                        isPill = true;
                        if (cellValue.Equals("Success", StringComparison.OrdinalIgnoreCase))
                        { pillColor = Color.FromArgb(0x20, 0x10, 0xD9, 0x86); pillText = Color.FromArgb(0xFF, 0x10, 0xD9, 0x86); }
                        else
                        { pillColor = Color.FromArgb(0x20, 0xF4, 0x3F, 0x5E); pillText = Color.FromArgb(0xFF, 0xF4, 0x3F, 0x5E); }
                    }
                    // Status pill — loan (col 6)
                    else if (mode is "loan" && c == 6)
                    {
                        isPill = true;
                        if (cellValue.Equals("Approved", StringComparison.OrdinalIgnoreCase))
                        { pillColor = Color.FromArgb(0x20, 0x10, 0xD9, 0x86); pillText = Color.FromArgb(0xFF, 0x10, 0xD9, 0x86); }
                        else if (cellValue.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                        { pillColor = Color.FromArgb(0x20, 0xF5, 0x9E, 0x0B); pillText = Color.FromArgb(0xFF, 0xF5, 0x9E, 0x0B); }
                        else
                        { pillColor = Color.FromArgb(0x20, 0xF4, 0x3F, 0x5E); pillText = Color.FromArgb(0xFF, 0xF4, 0x3F, 0x5E); }
                    }

                    FrameworkElement cell = isPill
                        ? (FrameworkElement)new Border
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
                        }
                        : new TextBlock
                        {
                            Text = cellValue,
                            FontSize = 13,
                            FontFamily = new FontFamily("Segoe UI Variable Text"),
                            Foreground = new SolidColorBrush(
                                c == 0
                                    ? Color.FromArgb(0xFF, 0xF1, 0xF5, 0xFF)
                                    : Color.FromArgb(0xFF, 0xA0, 0xAE, 0xC0)),
                            VerticalAlignment = VerticalAlignment.Center,
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(c == 0 ? 0 : 8, 0, 8, 0)
                        };

                    Grid.SetColumn(cell, c);
                    rowGrid.Children.Add(cell);
                }

                TableRows.Items.Add(new ContentPresenter
                {
                    Content = new Border
                    {
                        Padding = new Thickness(0, 10, 0, 10),
                        BorderThickness = new Thickness(0, 0, 0, 1),
                        BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0x1E, 0x2D, 0x4A)),
                        Background = new SolidColorBrush(
                            alternate
                                ? Color.FromArgb(0x0A, 0x1E, 0x2D, 0x4A)
                                : Colors.Transparent),
                        Child = rowGrid
                    }
                });
                alternate = !alternate;
            }
        }

        private void UpdateTableTitle(string mode, bool isTable, int rowCount = 0)
        {
            TableTitleText.Text = mode switch
            {
                "accounts" => "MY ACCOUNTS",
                "transactions" => "TRANSACTION HISTORY",
                "open_account" => "NEW ACCOUNT CREATED",
                "balance" => "ACCOUNT BALANCE",
                "deposit" => "DEPOSIT CONFIRMED",
                "withdraw" => "WITHDRAWAL CONFIRMED",
                "transfer" => "TRANSFER CONFIRMED",
                "loan" => "LOAN HISTORY",
                _ => "OUTPUT"
            };

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
        //  PARSERS
        // ════════════════════════════════════════════════════════════════

        // ── ParseAccountLines ────────────────────────────────────────────
        // C++ format per line (displayStatement()):
        //   "ACC2000 | Savings | Balance: $1000.00 | Active | Opened: 2024-01-01 12:00:00"
        private static List<string[]> ParseAccountLines(string raw)
        {
            var result = new List<string[]>();
            foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;
                if (!Regex.IsMatch(trimmed, @"^ACC\d+")) continue;
                var parts = trimmed.Split('|');
                if (parts.Length < 4) continue;

                string accNum = parts[0].Trim();
                string type = parts[1].Trim();
                string balanceRaw = StripPrefix(parts[2].Trim(), "Balance:").TrimStart('$', ' ');
                string status = parts[3].Trim();
                string opened = parts.Length >= 5
                    ? ExtractDateOnly(StripPrefix(parts[4].Trim(), "Opened:").Trim())
                    : DateTime.Now.ToString("yyyy-MM-dd");

                // ✅ Format balance exactly
                result.Add(new[] { accNum, type, FormatCurrency(balanceRaw), status, opened });
            }
            return result;
        }

        // ── ParseBalanceBlock ────────────────────────────────────────────
        // C++ format (getAccountBalance()):
        //   "Account: ACC2000\nBalance: $1000.00\nStatus: Active\nType: Savings"
        private static List<string[]> ParseBalanceBlock(string raw)
        {
            string accNum = string.Empty, type = string.Empty,
                   balanceRaw = string.Empty, status = string.Empty;
            foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                int colonIdx = trimmed.IndexOf(':');
                if (colonIdx < 0) continue;
                string key = trimmed[..colonIdx].Trim().ToLowerInvariant();
                string value = trimmed[(colonIdx + 1)..].Trim();
                switch (key)
                {
                    case "account": accNum = value; break;
                    case "balance": balanceRaw = value.TrimStart('$', ' '); break;
                    case "status":
                        status = value.Equals("FROZEN", StringComparison.OrdinalIgnoreCase)
                            ? "Frozen" : value; break;
                    case "type": type = value; break;
                }
            }
            var result = new List<string[]>();
            if (!string.IsNullOrEmpty(accNum))
                // ✅ Format balance exactly
                result.Add(new[] { accNum, type, FormatCurrency(balanceRaw), status });
            return result;
        }

        // ── ParseTransactionLines ────────────────────────────────────────
        // C++ format per line (displayReceipt()):
        //   "TXN5000 | Deposit  | 500    | 2024-01-01 12:00:00 | ACC2000"
        //   "TXN5001 | Transfer | 200    | 2024-01-01 12:00:00 | ACC2000 -> ACC2001"
        // ── ParseTransactionLines ────────────────────────────────────────
        // C++ format per line (displayReceipt()):
        //   "TXN5000 | Deposit  | 500    | 2024-01-01 12:00:00 | ACC2000"
        //   "TXN5001 | Transfer | 200    | 2024-01-01 12:00:00 | ACC2000 -> ACC2001"
        // ── ParseTransactionLines ────────────────────────────────────────
        // C++ format per line (displayReceipt()):
        //   "TXN5000 | Deposit  | 500    | 2024-01-01 12:00:00 | ACC2000"
        //   "TXN5001 | Transfer | 200    | 2024-01-01 12:00:00 | ACC2000 -> ACC2001"
        // ── ParseTransactionLines ────────────────────────────────────────
        // C++ format per line (displayReceipt()):
        //   "TXN5000 | Deposit  | 500    | 2024-01-01 12:00:00 | ACC2000"
        //   "TXN5001 | Transfer | 200    | 2024-01-01 12:00:00 | ACC2000 -> ACC2001"
        // ── ParseTransactionLines ────────────────────────────────────────
        // C++ format per line (displayReceipt()):
        //   "TXN5000 | Deposit  | 500.00    | 2024-01-01 12:00:00 | ACC2000"
        //   "TXN5001 | Transfer | 200.00    | 2024-01-01 12:00:00 | ACC2000 -> ACC2001"
        private static List<string[]> ParseTransactionLines(string raw)
        {
            var result = new List<string[]>();
            foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;
                var parts = trimmed.Split('|');
                if (parts.Length < 5) continue;
                string txnId = parts[0].Trim();
                if (!txnId.StartsWith("TXN", StringComparison.OrdinalIgnoreCase)) continue;
                string txnType = parts[1].Trim();

                // ✅ FIX: Use FormatCurrency (same as deposit/withdraw/transfer)
                string amount = FormatCurrency(parts[2].Trim());

                string date = ExtractDateOnly(parts[3].Trim());
                // "ACC2000 -> ACC2001" → show primary account only
                string accRaw = parts[4].Trim();
                string account = accRaw.Contains("->")
                    ? accRaw[..accRaw.IndexOf("->", StringComparison.Ordinal)].Trim()
                    : accRaw;
                result.Add(new[] { txnId, txnType, amount, date, account });
            }
            return result;
        }

        // ── ParseDepositWithdrawResponse ─────────────────────────────────
        //
        // C++ exact output format (no variation possible):
        //
        //   Deposit:  "Success: Deposited: <amount> to <accNum> | New Balance: <balance>"
        //   Withdraw: "Success: Withdrawn: <amount> from <accNum> | New Balance: <balance>"
        //
        // Parsing strategy — NO regex on numbers, purely positional string ops:
        //
        //   Step 1.  Verify "Success:" prefix. Strip it.
        //   Step 2.  Find the LAST " | New Balance: " in the string and split there.
        //            "Last" makes this immune to any number containing unusual chars.
        //   Step 3.  Everything after the marker is the raw balance string → FormatNumber.
        //   Step 4.  The action clause (left side) has a known structure:
        //              "Deposited: <amount> to ACC<n>"
        //              "Withdrawn: <amount> from ACC<n>"
        //            Split on the fixed keyword " to " or " from " that precedes "ACC".
        //            The token immediately before that keyword (after the verb+colon) is
        //            the amount.  The token after is the account number.
        //   Step 5.  Amount = exact string between ": " and the keyword — no regex needed.
        //   Step 6.  Account = everything after the keyword until end of action clause.
        //   Step 7.  Fetch TXN ID (single history call).
        //
        // This approach is completely immune to large numbers, decimals, or any future
        // change in the numeric value because it never tries to pattern-match digits.
        //
        private List<string[]> ParseDepositWithdrawResponse(
    string raw, string txnType, string contextAccount)
        {
            if (!raw.StartsWith("Success:", StringComparison.OrdinalIgnoreCase))
                return new List<string[]>();

            string body = raw["Success:".Length..].Trim();
            const string balanceMarker = " | New Balance: ";
            int markerIdx = body.LastIndexOf(balanceMarker, StringComparison.OrdinalIgnoreCase);
            string actionClause = markerIdx >= 0 ? body[..markerIdx].Trim() : body;
            string balanceRaw = markerIdx >= 0 ? body[(markerIdx + balanceMarker.Length)..].Trim() : string.Empty;

            // ✅ Format balance exactly
            string newBalance = string.IsNullOrEmpty(balanceRaw) ? "0.00" : FormatCurrency(balanceRaw);
            string amount = "0.00";
            string account = contextAccount;
            string[] separators = new[] { " to ACC", " from ACC" };

            foreach (var sep in separators)
            {
                int sepIdx = actionClause.IndexOf(sep, StringComparison.OrdinalIgnoreCase);
                if (sepIdx < 0) continue;
                int verbColonIdx = actionClause.IndexOf(": ", StringComparison.Ordinal);
                if (verbColonIdx >= 0 && verbColonIdx < sepIdx)
                {
                    string amtRaw = actionClause[(verbColonIdx + 2)..sepIdx].Trim();
                    amount = FormatCurrency(amtRaw); // ✅ Format amount exactly
                }
                string accPart = actionClause[(sepIdx + sep.Length)..].Trim();
                account = "ACC" + accPart;
                break;
            }

            string txnId = FetchLatestTxnId(account);
            return new List<string[]> { new[] { txnId, txnType, amount, account, newBalance, "Success" } };
        }

        // ── ParseTransferResponse ────────────────────────────────────────
        //
        // C++ exact output format:
        //   "Success: Transfer completed. <amount> from <fromAcc> to <toAcc>"
        //
        // Parsing strategy — purely positional, no regex on numbers:
        //
        //   Step 1.  Verify "Success:" prefix. Strip it.
        //   Step 2.  Find " from ACC" — everything between "completed. " and that is amount.
        //   Step 3.  Find " to ACC"   — everything between "from ACC" and that is fromAcc digits.
        //   Step 4.  Everything after "to ACC" is toAcc digits.
        //   Step 5.  Fetch TXN ID from fromAcc history.
        //
        private List<string[]> ParseTransferResponse(string raw, string contextFromAccount)
        {
            if (!raw.StartsWith("Success:", StringComparison.OrdinalIgnoreCase))
                return new List<string[]>();
            string body = raw["Success:".Length..].Trim();
            string amount = "0.00";
            string fromAcc = contextFromAccount;
            string toAcc = "—";

            const string fromMarker = " from ACC";
            int fromIdx = body.IndexOf(fromMarker, StringComparison.OrdinalIgnoreCase);
            if (fromIdx >= 0)
            {
                const string completedMarker = "completed. ";
                int compIdx = body.IndexOf(completedMarker, StringComparison.OrdinalIgnoreCase);
                if (compIdx >= 0)
                {
                    string amtRaw = body[(compIdx + completedMarker.Length)..fromIdx].Trim();
                    amount = FormatCurrency(amtRaw); // ✅ Format amount exactly
                }
                const string toMarker = " to ACC";
                int toIdx = body.IndexOf(toMarker, fromIdx, StringComparison.OrdinalIgnoreCase);
                if (toIdx >= 0)
                {
                    string fromDigits = body[(fromIdx + fromMarker.Length)..toIdx].Trim();
                    fromAcc = "ACC" + fromDigits;
                    string toDigits = body[(toIdx + toMarker.Length)..].Trim();
                    toAcc = "ACC" + toDigits;
                }
                else
                {
                    fromAcc = "ACC" + body[(fromIdx + fromMarker.Length)..].Trim();
                }
            }

            string txnId = FetchLatestTxnId(fromAcc);
            return new List<string[]> { new[] { txnId, "Transfer", amount, fromAcc, toAcc, "Success" } };
        }

        // ── ParseMyLoanLines ─────────────────────────────────────────────
        // C++ getMyLoans() format per line:
        //   "LOAN1000 | ACC2000 | 50000 | 12.5 | 24 | 2256.12 | pending\n"
        private static List<string[]> ParseMyLoanLines(string raw)
        {
            var result = new List<string[]>();
            if (string.IsNullOrWhiteSpace(raw)) return result;
            if (raw.StartsWith("Error:", StringComparison.OrdinalIgnoreCase)) return result;
            if (raw.StartsWith("No loan", StringComparison.OrdinalIgnoreCase)) return result;

            foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;
                if (!trimmed.StartsWith("LOAN", StringComparison.OrdinalIgnoreCase)) continue;

                var parts = trimmed.Split('|');
                if (parts.Length < 7) continue;

                string loanId = parts[0].Trim();
                string account = parts[1].Trim();
                string principal = parts[2].Trim();
                string rate = parts[3].Trim();
                string tenure = parts[4].Trim();
                string emi = parts[5].Trim();
                string statusRaw = parts[6].Trim();

                // Capitalise: "pending" → "Pending"
                string status = statusRaw.Length > 0
                    ? char.ToUpper(statusRaw[0]) + statusRaw[1..].ToLower()
                    : statusRaw;

                result.Add(new[] { loanId, account, principal, rate, tenure, emi, status });
            }
            return result;
        }

        // ── BuildLoanRow ─────────────────────────────────────────────────
        // C++ apply_loan success format:
        //   "Success: Loan applied: LOAN1000 | Disbursed to: ACC2000 | EMI: 1234.567890"
        private string[]? BuildLoanRow(
    string raw, string accountNo,
    string principal, string rate, string tenure)
        {
            if (!raw.StartsWith("Success:", StringComparison.OrdinalIgnoreCase)) return null;
            string loanId = Regex.Match(raw, @"LOAN\d+").Value;
            if (string.IsNullOrEmpty(loanId)) loanId = "—";
            string account = Regex.Match(raw, @"ACC\d+").Value;
            if (string.IsNullOrEmpty(account)) account = accountNo;

            string emi = "0.00";
            var emiMatch = Regex.Match(raw, @"EMI:\s*([\d.]+)", RegexOptions.IgnoreCase);
            if (emiMatch.Success)
            {
                emi = FormatCurrency(emiMatch.Groups[1].Value); // ✅ Format EMI exactly
            }

            return new[] { loanId, account, FormatCurrency(principal), FormatCurrency(rate), tenure, emi, "Pending" };
        }

        // ════════════════════════════════════════════════════════════════
        //  Utility Helpers
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Fetches transaction history for an account and returns the TXN ID
        /// of the LAST transaction recorded — which is always the one just created.
        /// Returns "—" on any failure.
        /// </summary>
        private string FetchLatestTxnId(string accountNo)
        {
            if (string.IsNullOrWhiteSpace(accountNo)) return "—";
            try
            {
                string historyRaw = App.Bank.Execute("history", accountNo.Trim(), "", "", "");
                if (string.IsNullOrWhiteSpace(historyRaw)
                    || historyRaw.StartsWith("Error:", StringComparison.OrdinalIgnoreCase)
                    || historyRaw.StartsWith("No transactions", StringComparison.OrdinalIgnoreCase))
                    return "—";

                string lastTxnId = "—";
                foreach (var line in historyRaw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = line.Trim().Split('|');
                    if (parts.Length < 1) continue;
                    string candidate = parts[0].Trim();
                    if (candidate.StartsWith("TXN", StringComparison.OrdinalIgnoreCase))
                        lastTxnId = candidate;  // keep updating — last line wins
                }
                return lastTxnId;
            }
            catch { return "—"; }
        }

        /// <summary>
        /// Strips a leading label (e.g. "Balance:") and returns the remainder trimmed.
        /// Case-insensitive.
        /// </summary>
        private static string StripPrefix(string s, string prefix)
        {
            if (s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return s[prefix.Length..].Trim();
            return s;
        }

        /// <summary>
        /// Formats a raw numeric string for clean display.
        ///
        /// Rules:
        ///   • Adds thousand-separators to the whole part (e.g. 1000000 → 1,000,000)
        ///   • Keeps at most 2 decimal places, strips trailing zeros
        ///     (1000000.85     → 1,000,000.85)
        ///     (1000000.850000 → 1,000,000.85)
        ///     (1000000.00     → 1,000,000)
        ///     (1000000.1      → 1,000,000.1)
        ///   • NEVER touches floating-point arithmetic — works purely on the string,
        ///     so no precision corruption regardless of how many digits are present.
        ///   • Handles negative values and arbitrarily large whole parts.
        /// </summary>
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

        /// <summary>Returns only the date portion (yyyy-MM-dd) from a datetime string.</summary>
        private static string ExtractDateOnly(string s)
        {
            var m = Regex.Match(s, @"\d{4}-\d{2}-\d{2}");
            return m.Success ? m.Value : s;
        }

        // ════════════════════════════════════════════════════════════════
        //  Navigation
        // ════════════════════════════════════════════════════════════════
        protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is not NavigationArgs args) return;

            _customerId = args.CustomerId;
            string name = args.WelcomeMessage ?? string.Empty;
            if (name.StartsWith("Success: ", StringComparison.OrdinalIgnoreCase))
                name = name["Success: ".Length..];

            CustomerIdText.Text = $"ID: {args.CustomerId}";
            CustomerNameText.Text = name;

            string[] parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            AvatarInitials.Text = parts.Length >= 2
                ? $"{parts[0][0]}{parts[1][0]}".ToUpper()
                : (name.Length >= 2 ? name[..2].ToUpper() : name.ToUpper());

            int hour = DateTime.Now.Hour;
            string timeGreeting = hour < 12 ? "Good morning" : hour < 17 ? "Good afternoon" : "Good evening";
            BannerGreetingText.Text = $"{timeGreeting}, {name}!";

            ShowResult("", $"Welcome back, {name}.\nSelect an action from the sidebar to get started.");
        }

        private void SetPage(string subtitle) => PageSubtitleText.Text = subtitle;

        // ════════════════════════════════════════════════════════════════
        //  Action Handlers
        // ════════════════════════════════════════════════════════════════

        private async void OpenAccountBtn_Click(object sender, RoutedEventArgs e)
        {
            SetPage("Open Account");
            var input = await InputDialog.ShowDoubleAsync(
                this.XamlRoot, "Open Account",
                "Account type (savings / current)", "Account type (savings / current)",
                "Minimum balance (e.g. 1000)", "Minimum balance (e.g. 1000)",
                "Open");

            if (input is null) return;

            string accountType = string.IsNullOrWhiteSpace(input.Value.First)
                ? "savings"
                : input.Value.First.Trim().ToLower();
            if (accountType != "savings" && accountType != "current") accountType = "savings";

            string rawBal = string.IsNullOrWhiteSpace(input.Value.Second)
                ? "0"
                : input.Value.Second.Trim();

            if (!double.TryParse(rawBal,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double minBal))
            {
                await ShowInfoDialog("Invalid Minimum Balance",
                    "Please enter a valid number for the minimum balance (e.g. 1000).");
                return;
            }
            if (minBal < 0)
            {
                await ShowInfoDialog("Invalid Minimum Balance",
                    "Minimum balance cannot be negative.");
                return;
            }

            string openRaw = App.Bank.Execute("open_account", _customerId, accountType, "5.0", rawBal);
            if (!openRaw.StartsWith("Success:", StringComparison.OrdinalIgnoreCase))
            {
                ShowResult("open_account", openRaw);
                return;
            }

            // Show the live account list so the table reflects actual backend state
            string listRaw = App.Bank.Execute("list_accounts", _customerId, "", "", "");
            ShowResult("open_account", listRaw);
        }

        private async void ShowBalanceBtn_Click(object sender, RoutedEventArgs e)
        {
            SetPage("Show Balance");
            string? accountNo = await InputDialog.ShowAsync(
                this.XamlRoot, "Check Balance",
                "Enter account number (e.g. ACC2000)", "Check");
            if (string.IsNullOrWhiteSpace(accountNo)) return;

            ShowResult("balance", App.Bank.Execute("get_balance", accountNo.Trim(), "", "", ""));
        }

        private void TotalAccountsBtn_Click(object sender, RoutedEventArgs e)
        {
            SetPage("Total Accounts");
            ShowResult("accounts", App.Bank.Execute("list_accounts", _customerId, "", "", ""));
        }

        private async void DepositBtn_Click(object sender, RoutedEventArgs e)
        {
            SetPage("Deposit");
            var input = await InputDialog.ShowDoubleAsync(
                this.XamlRoot, "Deposit",
                "Account Number", "Account number (e.g. ACC2000)",
                "Amount", "Amount to deposit",
                "Deposit");
            if (input is null) return;

            string accNum = input.Value.First?.Trim() ?? string.Empty;
            string amount = input.Value.Second?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(accNum) || string.IsNullOrEmpty(amount))
            {
                ShowResult("deposit", "Error: Account number and amount are required.");
                return;
            }
            if (!TryParsePositiveAmount(amount, out string amtError))
            {
                ShowResult("deposit", $"Error: {amtError}");
                return;
            }

            string raw = App.Bank.Execute("deposit", accNum, amount, "", "");
            ShowResult("deposit", raw, accNum);
        }

        private async void WithdrawBtn_Click(object sender, RoutedEventArgs e)
        {
            SetPage("Withdraw");
            var input = await InputDialog.ShowDoubleAsync(
                this.XamlRoot, "Withdraw",
                "Account Number", "Account number (e.g. ACC2000)",
                "Amount", "Amount to withdraw",
                "Withdraw");
            if (input is null) return;

            string accNum = input.Value.First?.Trim() ?? string.Empty;
            string amount = input.Value.Second?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(accNum) || string.IsNullOrEmpty(amount))
            {
                ShowResult("withdraw", "Error: Account number and amount are required.");
                return;
            }
            if (!TryParsePositiveAmount(amount, out string amtError))
            {
                ShowResult("withdraw", $"Error: {amtError}");
                return;
            }

            string raw = App.Bank.Execute("withdraw", accNum, amount, "", "");
            ShowResult("withdraw", raw, accNum);
        }

        private async void TransferBtn_Click(object sender, RoutedEventArgs e)
        {
            SetPage("Transfer");
            var input = await InputDialog.ShowTripleAsync(
                this.XamlRoot, "Transfer",
                "From Account", "From account number",
                "To Account", "To account number",
                "Amount", "Amount to transfer",
                "Transfer");
            if (input is null) return;

            string fromAcc = input.Value.A?.Trim() ?? string.Empty;
            string toAcc = input.Value.B?.Trim() ?? string.Empty;
            string amount = input.Value.C?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(fromAcc) || string.IsNullOrEmpty(toAcc) || string.IsNullOrEmpty(amount))
            {
                ShowResult("transfer", "Error: Both accounts and amount are required.");
                return;
            }
            if (fromAcc.Equals(toAcc, StringComparison.OrdinalIgnoreCase))
            {
                ShowResult("transfer", "Error: Source and destination accounts must be different.");
                return;
            }
            if (!TryParsePositiveAmount(amount, out string amtError))
            {
                ShowResult("transfer", $"Error: {amtError}");
                return;
            }

            string raw = App.Bank.Execute("transfer", fromAcc, toAcc, amount, "");
            ShowResult("transfer", raw, fromAcc);
        }

        private async void HistoryBtn_Click(object sender, RoutedEventArgs e)
        {
            SetPage("Transactions");
            string? accountNo = await InputDialog.ShowAsync(
                this.XamlRoot, "Transaction History",
                "Account number (e.g. ACC2000)", "View");
            if (string.IsNullOrWhiteSpace(accountNo)) return;
            ShowResult("transactions", App.Bank.Execute("history", accountNo.Trim(), "", "", ""));
        }

        private async void ApplyLoanBtn_Click(object sender, RoutedEventArgs e)
        {
            SetPage("Apply for Loan");
            ShowLoanHistory();

            string? accountNo = await InputDialog.ShowAsync(
                this.XamlRoot, "Loan Disbursement",
                "Enter account number for loan disbursement (e.g. ACC2000)", "Next");
            if (string.IsNullOrWhiteSpace(accountNo)) return;

            var input = await InputDialog.ShowTripleAsync(
                this.XamlRoot, "Loan Details",
                "Principal", "Loan amount (e.g. 50000)",
                "Interest Rate", "Annual interest rate % (e.g. 12.5)",
                "Tenure", "Repayment period in months (e.g. 24)",
                "Apply");
            if (input is null) return;

            string principal = input.Value.A?.Trim() ?? string.Empty;
            string rate = input.Value.B?.Trim() ?? string.Empty;
            string tenure = input.Value.C?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(principal) || string.IsNullOrEmpty(rate) || string.IsNullOrEmpty(tenure))
            { ShowResult("loan", "Error: All loan fields are required."); return; }

            if (!double.TryParse(principal, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double p) || p <= 0)
            { ShowResult("loan", "Error: Principal must be a positive number."); return; }

            if (!double.TryParse(rate, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double r) || r <= 0)
            { ShowResult("loan", "Error: Interest rate must be a positive number."); return; }

            if (!int.TryParse(tenure, out int t) || t <= 0)
            { ShowResult("loan", "Error: Tenure must be a positive whole number of months."); return; }

            string combinedParam = _customerId + "|" + accountNo.Trim();
            string raw = App.Bank.Execute("apply_loan", combinedParam, principal, rate, tenure);

            var newRow = BuildLoanRow(raw, accountNo.Trim(), principal, rate, tenure);
            if (newRow is not null)
                _sessionLoanHistory.Add(newRow);

            if (!raw.StartsWith("Success:", StringComparison.OrdinalIgnoreCase))
            { ShowResult("loan", raw); return; }

            ShowLoanHistory();
        }

        private void LogoutBtn_Click(object sender, RoutedEventArgs e)
        {
            _customerId = string.Empty;
            _sessionLoanHistory.Clear();
            App.Bank.Execute("logout", "", "", "", "");
            Frame.Navigate(
                typeof(LoginPage), null,
                new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromLeft });
        }

        // ════════════════════════════════════════════════════════════════
        //  Validation Helpers
        // ════════════════════════════════════════════════════════════════

        private static bool TryParsePositiveAmount(string raw, out string error)
        {
            error = string.Empty;
            if (!double.TryParse(raw,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double value))
            {
                error = "Amount must be a valid number (e.g. 500 or 1500.50).";
                return false;
            }
            if (!double.IsFinite(value) || value <= 0)
            {
                error = "Amount must be a positive number greater than zero.";
                return false;
            }
            if (value > 1_000_000)
            {
                error = "Maximum transaction amount is 1,000,000 per operation.";
                return false;
            }
            return true;
        }

        private async System.Threading.Tasks.Task ShowInfoDialog(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = new TextBlock
                {
                    Text = title,
                    FontWeight = FontWeights.SemiBold,
                    FontFamily = new FontFamily("Segoe UI Variable Display"),
                    Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xF1, 0xF5, 0xFF))
                },
                Content = new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = new FontFamily("Segoe UI Variable Text"),
                    Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x7A, 0x8B, 0xAA)),
                    Margin = new Thickness(0, 8, 0, 0)
                },
                PrimaryButtonText = "Try Again",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot,
                RequestedTheme = ElementTheme.Dark
            };
            await dialog.ShowAsync();
        }
    }
}