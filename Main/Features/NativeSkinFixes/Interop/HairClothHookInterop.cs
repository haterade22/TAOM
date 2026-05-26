using System;
using System.Runtime.InteropServices;
using TAOM.Core.Logging;

namespace TAOM.Features.NativeSkinFixes.Interop;

/// <summary>
/// Wraps <c>HairClothHook_Install</c> / <c>HairClothHook_Uninstall</c> in the
/// native plugin. The hook detours the cloth factory and rescues orphan cloth
/// components from <c>Face_mesh+0x1A0</c> (hair) and re-enters the factory for
/// beard cloth at <c>Face_mesh+0x108</c>, registering both in the entity +
/// simulation lists so they render and animate.
/// </summary>
internal static class HairClothHookInterop
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

        _install   = NativeHookLoader.GetExport<InstallDelegate>("HairClothHook_Install");
        _uninstall = NativeHookLoader.GetExport<UninstallDelegate>("HairClothHook_Uninstall");

        if (_install == null || _uninstall == null)
        {
            logger.LogWarning(
                "[NativeSkinFixes][HairCloth] required exports not found in TAOM.NativeSkinFixes.dll " +
                "(rebuild C++ project from Dependencies/NativeSkinFixes.NativeHooks/Build.ps1)");
            return false;
        }

        try
        {
            IsInitialized = _install();
        }
        catch (Exception ex)
        {
            logger.LogError($"[NativeSkinFixes][HairCloth] Install threw {ex.GetType().Name}: {ex.Message}");
            IsInitialized = false;
        }

        if (IsInitialized) logger.LogInfo("[NativeSkinFixes][HairCloth] hook installed");
        else                logger.LogWarning("[NativeSkinFixes][HairCloth] hook NOT installed (see NativeSkinFixes.log)");
        return IsInitialized;
    }

    public static void Uninstall()
    {
        if (!IsInitialized || _uninstall == null) return;
        try { _uninstall(); } catch { /* shutdown — swallow */ }
        IsInitialized = false;
    }
}
