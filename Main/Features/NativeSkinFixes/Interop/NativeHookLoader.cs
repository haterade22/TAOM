using System;
using System.IO;
using System.Runtime.InteropServices;
using TaleWorlds.ModuleManager;

namespace TAOM.Features.NativeSkinFixes.Interop;

/// <summary>
/// Loads <c>TAOM.NativeSkinFixes.dll</c> + its MinHook dependency from the
/// TAOM module's <c>bin\Win64_Shipping_Client</c> directory and exposes
/// <c>GetExport&lt;T&gt;</c> for the three hook installers to resolve their
/// extern "C" entry points.
/// </summary>
/// <remarks>
/// <para>Unlike the upstream NativeSkinFixes mod, this loader does NOT resolve
/// per-function RVAs in <c>TaleWorlds.Native.dll</c> — the C++ side scans for
/// byte patterns at install time (see <c>Signatures.h</c>). The interop layer
/// is therefore version-independent: only the C++ patterns change between
/// Bannerlord releases.</para>
/// <para>Idempotent: <see cref="EnsureLoaded"/> is safe to call multiple times.
/// First call resolves and caches; subsequent calls return the cached state.</para>
/// </remarks>
internal static class NativeHookLoader
{
    private const string DllName = "TAOM.NativeSkinFixes";

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SetDllDirectory(string? lpPathName);

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

    private static IntPtr _hooksModule;
    private static bool _loadAttempted;
    private static string? _lastLoadError;

    /// <summary>Handle to the loaded <c>TAOM.NativeSkinFixes.dll</c>, or <see cref="IntPtr.Zero"/>.</summary>
    public static IntPtr HooksModule => _hooksModule;

    /// <summary>The last LoadLibrary error message, or null on success / not-yet-attempted.</summary>
    public static string? LastLoadError => _lastLoadError;

    /// <summary>
    /// Loads <c>TAOM.NativeSkinFixes.dll</c> from <c>Modules/TAOM/bin/Win64_Shipping_Client</c>.
    /// Returns true on success. Subsequent calls return the cached result.
    /// </summary>
    public static bool EnsureLoaded()
    {
        if (_hooksModule != IntPtr.Zero) return true;
        if (_loadAttempted) return false;
        _loadAttempted = true;

        string modulePath;
        try
        {
            modulePath = ModuleHelper.GetModuleFullPath("TAOM");
        }
        catch (Exception ex)
        {
            _lastLoadError = $"ModuleHelper.GetModuleFullPath threw: {ex.GetType().Name}: {ex.Message}";
            return false;
        }

        string binDir = Path.GetFullPath(Path.Combine(modulePath, "bin", "Win64_Shipping_Client"));

        // Setting the DLL directory so MinHook.x64.dll resolves alongside the
        // native plugin even if the working directory differs.
        SetDllDirectory(binDir);
        try
        {
            _hooksModule = LoadLibrary(DllName + ".dll");
        }
        finally
        {
            SetDllDirectory(null);
        }

        if (_hooksModule == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            _lastLoadError = $"LoadLibrary failed (Win32 error {err}). Expected DLL at: {Path.Combine(binDir, DllName + ".dll")}";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Resolves a DLL export to a typed delegate. Returns null if the export
    /// is missing — caller is responsible for the fallback (log + skip-install).
    /// </summary>
    public static T? GetExport<T>(string name)
        where T : Delegate
    {
        if (_hooksModule == IntPtr.Zero) return null;
        IntPtr proc = GetProcAddress(_hooksModule, name);
        if (proc == IntPtr.Zero) return null;
        return Marshal.GetDelegateForFunctionPointer<T>(proc);
    }
}
