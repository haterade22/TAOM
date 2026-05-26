using System;
using System.Runtime.InteropServices;
using TAOM.Core.Logging;

namespace TAOM.Features.NativeSkinFixes.Interop;

/// <summary>
/// Wraps <c>CoversHeadHook_Install</c> / <c>CoversHeadHook_Uninstall</c> in
/// the native plugin. The hook forces Face_mesh creation even when a covered
/// head ("covers_head=true") helmet is equipped, preserving the GPU morph
/// pipeline so hand grips animate correctly.
/// </summary>
internal static class CoversHeadHookInterop
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

        _install   = NativeHookLoader.GetExport<InstallDelegate>("CoversHeadHook_Install");
        _uninstall = NativeHookLoader.GetExport<UninstallDelegate>("CoversHeadHook_Uninstall");

        if (_install == null || _uninstall == null)
        {
            logger.LogWarning(
                "[NativeSkinFixes][CoversHead] required exports not found in TAOM.NativeSkinFixes.dll " +
                "(rebuild C++ project from Dependencies/NativeSkinFixes.NativeHooks/Build.ps1)");
            return false;
        }

        try
        {
            IsInitialized = _install();
        }
        catch (Exception ex)
        {
            logger.LogError($"[NativeSkinFixes][CoversHead] Install threw {ex.GetType().Name}: {ex.Message}");
            IsInitialized = false;
        }

        if (IsInitialized) logger.LogInfo("[NativeSkinFixes][CoversHead] hook installed");
        else                logger.LogWarning("[NativeSkinFixes][CoversHead] hook NOT installed (see NativeSkinFixes.log)");
        return IsInitialized;
    }

    public static void Uninstall()
    {
        if (!IsInitialized || _uninstall == null) return;
        try { _uninstall(); } catch { /* shutdown — swallow */ }
        IsInitialized = false;
    }
}
