using System;
using System.Runtime.InteropServices;
using TAOM.Core.Logging;

namespace TAOM.Features.NativeSkinFixes
{
    internal static class HairClothHookInterop
    {
        private const long ClothFactoryRva = 0x359C10;
        private const long AddToListRva = 0x0C4040;
        private const long GpuInitRva = 0x292570;
        private const long HasClothDataRva = 0x2C3420;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool InstallDelegate(
            IntPtr clothFactoryPtr,
            IntPtr addToListPtr,
            IntPtr gpuInitPtr,
            IntPtr hasClothDataPtr);

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
                _install = NativeHookLoader.GetExport<InstallDelegate>("HairClothHook_Install");
                _uninstall = NativeHookLoader.GetExport<VoidDelegate>("HairClothHook_Uninstall");

                if (_install == null || _uninstall == null)
                {
                    logger.LogError("[NativeSkinFixes] HairCloth: failed to resolve exports");
                    return;
                }

                IntPtr clothFactory = NativeHookLoader.ResolveRva(ClothFactoryRva);
                IntPtr addToList = NativeHookLoader.ResolveRva(AddToListRva);
                IntPtr gpuInit = NativeHookLoader.ResolveRva(GpuInitRva);
                IntPtr hasClothData = NativeHookLoader.ResolveRva(HasClothDataRva);

                bool result = _install(clothFactory, addToList, gpuInit, hasClothData);
                IsInstalled = result;
                if (result)
                    logger.LogInfo("[NativeSkinFixes] HairCloth: cloth factory detour installed — hair/beard physics active");
                else
                    logger.LogError("[NativeSkinFixes] HairCloth: cloth factory detour install failed");
            }
            catch (Exception ex)
            {
                logger.LogError($"[NativeSkinFixes] HairCloth: init error — {ex.Message}");
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
                _logger?.LogError($"[NativeSkinFixes] HairCloth: shutdown error — {ex.Message}");
            }
        }
    }
}
