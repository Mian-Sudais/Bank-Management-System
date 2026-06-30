using System;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace ModernUI
{
    /// <summary>
    /// Low-level P/Invoke bindings for CoreLogicFixed.dll.
    /// All public access goes through <see cref="BankHandle"/>.
    /// </summary>
    internal static class NativeInterop
    {
        private const string DllName = "CoreLogicFixed.dll";

        [DllImport(DllName,
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Ansi,
            ExactSpelling = true,
            SetLastError = true)]
        internal static extern IntPtr CreateBank();

        [DllImport(DllName,
            CallingConvention = CallingConvention.StdCall,
            ExactSpelling = true)]
        internal static extern void DeleteBank(IntPtr bank);

        [DllImport(DllName,
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Ansi,
            ExactSpelling = true,
            EntryPoint = "ExecuteCommand",
            SetLastError = true)]
        internal static extern IntPtr ExecuteCommand(
            IntPtr bank,
            string cmd,
            string p1,
            string p2,
            string p3,
            string p4);
    }

    /// <summary>
    /// Thread-safe, disposable wrapper around the native Bank object.
    /// All commands are serialised through a single lock so the
    /// thread_local static result buffer in the DLL is never raced.
    /// </summary>
    public sealed class BankHandle : IDisposable
    {
        private readonly object _lock = new object();
        private IntPtr _ptr;
        private volatile bool _disposed;

        public bool IsDisposed => _disposed;

        // ── Construction ─────────────────────────────────────────────

        public BankHandle()
        {
            try
            {
                Debug.WriteLine("[BankHandle] Creating native Bank instance…");
                IntPtr ptr = NativeInterop.CreateBank();
                if (ptr == IntPtr.Zero)
                    throw new InvalidOperationException(
                        "CreateBank() returned a null pointer. " +
                        "Ensure CoreLogicFixed.dll is present and matches the process architecture.");
                _ptr = ptr;
                Debug.WriteLine("[BankHandle] Native Bank created successfully.");
            }
            catch (DllNotFoundException ex)
            {
                throw new ApplicationException(
                    "CoreLogicFixed.dll was not found. Copy it to the application output directory.", ex);
            }
            catch (BadImageFormatException ex)
            {
                throw new ApplicationException(
                    "CoreLogicFixed.dll architecture mismatch (x86 vs x64 vs ARM64).", ex);
            }
        }

        // ── Execute ──────────────────────────────────────────────────

        /// <summary>
        /// Sends a command to the native Bank and returns the result string.
        /// Never throws — errors are returned as "Error: …" strings so UI code
        /// can handle them uniformly.
        /// </summary>
        public string Execute(
            string cmd,
            string p1 = "",
            string p2 = "",
            string p3 = "",
            string p4 = "")
        {
            if (_disposed)
                return "Error: Bank handle has been disposed.";

            if (string.IsNullOrWhiteSpace(cmd))
                return "Error: Command cannot be empty.";

            lock (_lock)
            {
                if (_disposed)
                    return "Error: Bank handle has been disposed.";

                try
                {
                    IntPtr resultPtr = NativeInterop.ExecuteCommand(
                        _ptr,
                        cmd,
                        p1 ?? string.Empty,
                        p2 ?? string.Empty,
                        p3 ?? string.Empty,
                        p4 ?? string.Empty);

                    if (resultPtr == IntPtr.Zero)
                        return "Warning: Command returned no response.";

                    return Marshal.PtrToStringAnsi(resultPtr)
                        ?? "Warning: Failed to marshal response string.";
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[BankHandle] Interop error: {ex}");
                    return $"Error: Interop operation failed ({ex.GetType().Name}). " +
                            "See debug output for details.";
                }
            }
        }

        // ── Disposal ─────────────────────────────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;
                IntPtr ptr = _ptr;
                _ptr = IntPtr.Zero;
                if (ptr != IntPtr.Zero)
                {
                    try { NativeInterop.DeleteBank(ptr); }
                    catch (Exception ex)
                    { Debug.WriteLine($"[BankHandle] DeleteBank error: {ex.Message}"); }
                }
            }
            GC.SuppressFinalize(this);
            Debug.WriteLine("[BankHandle] Disposed.");
        }

        ~BankHandle()
        {
            if (_disposed) return;
            _disposed = true;
            IntPtr ptr = _ptr;
            _ptr = IntPtr.Zero;
            if (ptr != IntPtr.Zero)
            {
                try { NativeInterop.DeleteBank(ptr); } catch { }
            }
        }
    }
}