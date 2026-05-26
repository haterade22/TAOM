using System;
using System.Runtime.InteropServices;
using TAOM.Core.Logging;

namespace TAOM.Features.NativeSkinFixes.Interop;

/// <summary>
/// Wraps <c>FaceMeshObserveHook_Install</c> / <c>FaceMeshObserveHook_Uninstall</c>
/// in the native plugin. The hook detours the Face_mesh render list builder so
/// static hair / beard / face slots can be suppressed during a list rebuild
/// (and restored after) without disturbing refcount or mesh allocation.
/// </summary>
internal static class FaceMeshObserveHookInterop
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool InstallDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void UninstallDelegate();

    private static InstallDelegate? _install;
    private static UninstallDelegate? _uninstall;

    public static bool IsInitialized { get; private set; }

    public static bool TryInstall(IModLogger logger)
    {
        if (IsInitialized) return true;

        _install   = NativeHookLoader.GetExport<InstallDelegate>("FaceMeshObserveHook_Install");
        _uninstall = NativeHookLoader.GetExport<UninstallDelegate>("FaceMeshObserveHook_Uninstall");

        if (_install == null || _uninstall == null)
        {
            logger.LogWarning(
                "[NativeSkinFixes][FaceMesh] required exports not found in TAOM.NativeSkinFixes.dll " +
                "(rebuild C++ project from Dependencies/NativeSkinFixes.NativeHooks/Build.ps1)");
            return false;
        }

        try
        {
            IsInitialized = _install();
        }
        catch (Exception ex)
        {
            logger.LogError($"[NativeSkinFixes][FaceMesh] Install threw {ex.GetType().Name}: {ex.Message}");
            IsInitialized = false;
        }

        if (IsInitialized) logger.LogInfo("[NativeSkinFixes][FaceMesh] hook installed");
        else                logger.LogWarning("[NativeSkinFixes][FaceMesh] hook NOT installed (see NativeSkinFixes.log)");
        return IsInitialized;
    }

    public static void Uninstall()
    {
        if (!IsInitialized || _uninstall == null) return;
        try { _uninstall(); } catch { /* shutdown — swallow */ }
        IsInitialized = false;
    }
}
