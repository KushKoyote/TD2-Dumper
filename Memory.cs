using System.ComponentModel;
using System.Runtime.InteropServices;

namespace TD2Dumper;

// ─── Windows API ─────────────────────────────────────────────────────────────

internal static class NativeMethods
{
    public const uint PROCESS_VM_READ           = 0x0010;
    public const uint PROCESS_QUERY_INFORMATION = 0x0400;

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(
        uint dwDesiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
        int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ReadProcessMemory(
        IntPtr hProcess,
        IntPtr lpBaseAddress,
        byte[] lpBuffer,
        nint nSize,
        out nint lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(IntPtr hObject);
}

// ─── Memory reader ────────────────────────────────────────────────────────────

/// <summary>
/// Wraps ReadProcessMemory with a clean interface.
/// Must be disposed to close the process handle.
/// </summary>
internal sealed class MemoryReader : IDisposable
{
    private IntPtr _hProcess;
    private bool   _disposed;

    public MemoryReader(int pid)
    {
        _hProcess = NativeMethods.OpenProcess(
            NativeMethods.PROCESS_VM_READ | NativeMethods.PROCESS_QUERY_INFORMATION,
            false,
            pid);

        if (_hProcess == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            throw new Win32Exception(err,
                $"OpenProcess failed (Win32 error {err}). " +
                "Run the dumper as Administrator.");
        }
    }

    /// <summary>
    /// Reads exactly <paramref name="size"/> bytes from <paramref name="address"/>.
    /// Returns null if the read fails or is short.
    /// </summary>
    public byte[]? ReadBytes(ulong address, int size)
    {
        if (size <= 0) return [];

        var buf = new byte[size];
        bool ok = NativeMethods.ReadProcessMemory(
            _hProcess, (IntPtr)(long)address, buf, size, out nint read);

        return (ok && read == size) ? buf : null;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_hProcess != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(_hProcess);
                _hProcess = IntPtr.Zero;
            }
            _disposed = true;
        }
    }
}
