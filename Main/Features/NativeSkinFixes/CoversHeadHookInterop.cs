using System;
using System.Runtime.InteropServices;
using TAOM.Core.Logging;

namespace TAOM.Features.NativeSkinFixes
{
    internal static class CoversHeadHookInterop
    {
        private const long AddSkinMeshesRva = 0x617B50;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool InstallDelegate(IntPtr targetFnPtr);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void VoidDelegate();

        private static InstallDelegate? _install;
        private static VoidDelegate? _uninstall;
        private static IModLogger? _logger;

        public static bool IsInstalled { get; private set; }

        public static void InstallHook(IModLogger logger)
        {
            _logger = logger;
            try
            {
                _install = NativeHookLoader.GetExport<InstallDelegate>("CoversHeadHook_Install");
                _uninstall = NativeHookLoader.GetExport<VoidDelegate>("CoversHeadHook_Uninstall");

                if (_install == null || _uninstall == null)
                {
                    logger.LogError("[NativeSkinFixes] CoversHead: failed to resolve exports");
                    return;
                }

                IntPtr targetFnPtr = NativeHookLoader.ResolveRva(AddSkinMeshesRva);
                bool result = _install(targetFnPtr);
                IsInstalled = result;
                if (result)
                    logger.LogInfo("[NativeSkinFixes] CoversHead: hook installed — covers_head morph fix active");
                else
                    logger.LogError("[NativeSkinFixes] CoversHead: hook install failed");
            }
            catch (Exception ex)
            {
                logger.LogError($"[NativeSkinFixes] CoversHead: init error — {ex.Message}");
            }
        }

        public static void RemoveHook()
        {
            if (!IsInstalled) return;
            try
            {
                _uninstall?.Invoke();
                IsInstalled = false;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"[NativeSkinFixes] CoversHead: shutdown error — {ex.Message}");
            }
        }
    }
}
