using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace TAOM.Features.BattleLoadDiagnostics;

/// <summary>
/// Writes a thread-stacks-only minidump of this process (thread info and unloaded modules, a few MB),
/// the shape the shipped Bannerlord.ButterLib.dll uses for its own dumps. Any failure returns false with
/// the error text.
/// </summary>
internal static class FreezeDumpWriter
{
    private const uint MiniDumpWithUnloadedModules = 0x20;
    private const uint MiniDumpWithThreadInfo = 0x1000;

    [DllImport("dbghelp.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MiniDumpWriteDump(
        IntPtr hProcess,
        uint processId,
        SafeFileHandle hFile,
        uint dumpType,
        IntPtr exceptionParam,
        IntPtr userStreamParam,
        IntPtr callbackParam);

    public static string FileName(DateTime localTime, int pid)
        => "taom_freeze_" + localTime.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture) + "_pid" + pid.ToString(CultureInfo.InvariantCulture) + ".dmp";

    public static bool TryWrite(string directory, out string pathOrError)
    {
        try
        {
            Directory.CreateDirectory(directory);
            using var process = Process.GetCurrentProcess();
            string path = Path.Combine(directory, FileName(DateTime.Now, process.Id));
            using var file = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            bool written = MiniDumpWriteDump(process.Handle, (uint)process.Id, file.SafeFileHandle,
                MiniDumpWithThreadInfo | MiniDumpWithUnloadedModules, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (!written)
            {
                pathOrError = "MiniDumpWriteDump failed, error 0x" + Marshal.GetLastWin32Error().ToString("X", CultureInfo.InvariantCulture);
                return false;
            }

            pathOrError = path;
            return true;
        }
        catch (Exception ex)
        {
            pathOrError = ex.GetType().Name + ": " + ex.Message;
            return false;
        }
    }
}
