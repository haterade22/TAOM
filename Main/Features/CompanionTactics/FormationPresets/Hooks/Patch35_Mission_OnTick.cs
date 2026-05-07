using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CompanionTactics.FormationPresets.Hooks;

/// <summary>
/// Postfix on <c>Mission.OnTick</c>. HOT PATH (~60Hz): only runs the cheapest possible
/// toggle check before exiting. The original developer's mod read keyboard hotkeys here
/// to drive Save/Load/AutoAssign — TAOM moves that input into the OOBButtonsVM commands
/// (button-only entry points), so the hot-path body shrinks to a near-no-op.
///
/// Hot-path discipline: zero allocations. Toggle check + early return only — no LINQ,
/// no closures, no foreach over enumerables.
/// </summary>
[HarmonyPatch(typeof(Mission), nameof(Mission.OnTick), new[] { typeof(float), typeof(float), typeof(bool), typeof(bool) })]
[HarmonyPatchCategory("Patch35_CompanionTactics")]
public static class Patch35_Mission_OnTick
{
    private static ICompanionTacticsSettingsProvider _settings;

    [HarmonyPostfix]
    public static void Postfix(float dt, float realDt, bool updateCamera, bool doAsyncAITick)
    {
        // First-call settings cache; per-frame thereafter is a single field read + bool check.
        if (_settings == null)
        {
            try { _settings = IoC.Resolve<ICompanionTacticsSettingsProvider>(); }
            catch { return; }
        }
        if (_settings == null) return;
        if (!_settings.EnableFormationPresets) return;

        // Body intentionally empty for now: hotkey-driven preset I/O moved to UI buttons.
        // Future per-tick work (e.g., a stance-decay timer) lives in a service method
        // resolved + cached above. Keep this body allocation-free.
    }
}
