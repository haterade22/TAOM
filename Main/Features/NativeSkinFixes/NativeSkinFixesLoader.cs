using System;
using TAOM.Core.Logging;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TAOM.Features.NativeSkinFixes
{
    internal static class NativeSkinFixesLoader
    {
        private static bool _initialized;

        public static void Install(IModLogger logger)
        {
            if (_initialized) return;

            bool isEditor = System.Diagnostics.Process
                .GetCurrentProcess()
                .MainModule?.FileName.Contains("wEditor") == true;

            if (isEditor)
            {
                logger.LogInfo("[NativeSkinFixes] Skipping hook install in editor");
                return;
            }

            try
            {
                if (!NativeHookLoader.EnsureLoaded(logger))
                {
                    logger.LogWarning("[NativeSkinFixes] Native DLL not loaded — skin fixes disabled");
                    return;
                }

                // Install order matters: FaceMeshObserve depends on CoversHead and HairCloth state
                CoversHeadHookInterop.InstallHook(logger);
                HairClothHookInterop.InstallHook(logger);
                FaceMeshObserveInterop.InstallHook(logger);

                int installed = (CoversHeadHookInterop.IsInstalled ? 1 : 0)
                              + (HairClothHookInterop.IsInstalled ? 1 : 0)
                              + (FaceMeshObserveInterop.IsInstalled ? 1 : 0);

                if (installed == 3)
                {
                    logger.LogInfo("[NativeSkinFixes] All 3 hooks installed successfully");
                    InformationManager.DisplayMessage(new InformationMessage(
                        "NativeSkinFixes loaded — covers_head morph fix + hair/beard cloth simulation",
                        new Color(0.6f, 0.8f, 1f)));
                }
                else if (installed > 0)
                {
                    logger.LogWarning($"[NativeSkinFixes] Partial install: {installed}/3 hooks active");
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"NativeSkinFixes partially loaded ({installed}/3 hooks)",
                        new Color(1f, 0.8f, 0.2f)));
                }
                else
                {
                    logger.LogError("[NativeSkinFixes] All hooks failed to install");
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"[NativeSkinFixes] FATAL: {ex.GetType().Name}: {ex.Message}");
            }

            _initialized = true;
        }

        public static void Uninstall()
        {
            if (!_initialized) return;

            FaceMeshObserveInterop.RemoveHook();
            HairClothHookInterop.RemoveHook();
            CoversHeadHookInterop.RemoveHook();

            _initialized = false;
        }
    }
}
