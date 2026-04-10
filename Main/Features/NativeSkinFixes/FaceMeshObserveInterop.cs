using System;
using System.Runtime.InteropServices;
using TAOM.Core.Logging;

namespace TAOM.Features.NativeSkinFixes
{
    internal static class FaceMeshObserveInterop
    {
        private const long RenderListBuildRva = 0x61FE20;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool InstallDelegate(IntPtr targetFnPtr);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void VoidDelegate();

        private static InstallDelegate? _install;
        private static VoidDelegate? _uninstall;

        public static bool IsInstalled { get; private set; }

        public static void InstallHook(IModLogger logger)
        {
            try
            {
                _install = NativeHookLoader.GetExport<InstallDelegate>("FaceMeshObserveHook_Install");
                _uninstall = NativeHookLoader.GetExport<VoidDelegate>("FaceMeshObserveHook_Uninstall");

                if (_install == null || _uninstall == null)
                {
                    logger.LogError("[NativeSkinFixes] FaceMeshRenderList: failed to resolve exports");
                    return;
                }

                IntPtr targetFnPtr = NativeHookLoader.ResolveRva(RenderListBuildRva);
                bool result = _install(targetFnPtr);
                IsInstalled = result;
                if (result)
                    logger.LogInfo("[NativeSkinFixes] FaceMeshRenderList: render list hook installed");
                else
                    logger.LogError("[NativeSkinFixes] FaceMeshRenderList: hook install failed");
            }
            catch (Exception ex)
            {
                logger.LogError($"[NativeSkinFixes] FaceMeshRenderList: init error — {ex.Message}");
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
                System.Diagnostics.Debug.Print($"[NativeSkinFixes] FaceMeshRenderList: shutdown error — {ex.Message}");
            }
        }
    }
}
