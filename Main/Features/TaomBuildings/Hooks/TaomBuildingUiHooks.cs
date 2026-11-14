using System;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;

namespace TAOM.Features.TaomBuildings.Hooks;

/// <summary>
/// The one handle both Patch108 postfixes read, set at static initialisation. The factory stays lazy so a config read or a
/// duplicate-id throw does not escape the fail-closed OnSubModuleLoad step (InitializeStatics runs inside IoC.Configure).
/// A factory that throws is not retried: the postfixes run for every building widget and level line shown, so the
/// failure is logged once and every later call returns null.
/// </summary>
internal static class TaomBuildingUiHooks
{
    private static Func<ITaomBuildingUiHook?>? _factory;
    private static ITaomBuildingUiHook? _hook;
    private static bool _failed;

    internal static void Initialize(Func<ITaomBuildingUiHook?> factory)
    {
        _factory = factory;
        _hook = null;
        _failed = false;
    }

    internal static ITaomBuildingUiHook? Hook
    {
        get
        {
            if (_hook != null || _failed)
                return _hook;

            try
            {
                return _hook = _factory?.Invoke();
            }
            catch (Exception ex)
            {
                // The icon and the effect line fall back to the engine's. The likely fault is a failed IoC resolve,
                // which can fail the logger's resolve as well, so the log line has a catch of its own.
                _failed = true;
                SelfDisables.Report(TaomBuildingsModule.PatchCategory + ".UiHook", ex.GetType().Name + ": " + ex.Message);
                try { TAOM.IoC.Resolve<IModLogger>()?.LogWarning($"[TaomBuildings] UI hook could not be built ({ex.GetType().Name}: {ex.Message}); the default icon and effect line stand"); } catch { }
                return null;
            }
        }
    }
}
