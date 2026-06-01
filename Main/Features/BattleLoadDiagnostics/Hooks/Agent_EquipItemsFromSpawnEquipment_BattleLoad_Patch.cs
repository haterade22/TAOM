using HarmonyLib;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;

namespace TAOM.Features.BattleLoadDiagnostics.Hooks;

// Phase 5 — THE money hook. Begin (prefix) logs the agent + full loadout incl. collision
// mesh names BEFORE the engine equips it; Ok (postfix) only after it returns. A begin with
// no matching Ok = the freeze, and the dumped slots name the suspect item (look for
// bo=<null> / shieldBo=<null>). Double-gated on IsEnabled + the loading window so it is a
// two-bool no-op outside the initial-load window (reinforcement waves are not logged).
// Coexists with Patch23_BannerColorPersistence's prefix on the same method.
[HarmonyPatch(typeof(Agent), nameof(Agent.EquipItemsFromSpawnEquipment))]
[HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
public static class Agent_EquipItemsFromSpawnEquipment_BattleLoad_Patch
{
    private static IBattleLoadDiagnosticsService? _service;
    private static IEquipmentSnapshotAdapter? _adapter;

    public static void Initialize(IBattleLoadDiagnosticsService service, IEquipmentSnapshotAdapter adapter)
    {
        _service = service;
        _adapter = adapter;
    }

    [HarmonyPrefix]
    public static void Prefix(Agent __instance)
    {
        var svc = _service;
        if (svc == null || !svc.IsEnabled) return;
        if (!BattleLoadLoadingWindow.IsOpen) return;
        try
        {
            var snapshot = _adapter?.Capture(__instance);
            if (snapshot != null) svc.LogAgentEquipBegin(snapshot);
        }
        catch { /* diagnostic only — never break agent spawn */ }
    }

    [HarmonyPostfix]
    public static void Postfix(Agent __instance)
    {
        var svc = _service;
        if (svc == null || !svc.IsEnabled) return;
        if (!BattleLoadLoadingWindow.IsOpen) return;
        try { svc.LogAgentEquipOk(__instance?.Index ?? -1, __instance?.Name ?? "<unnamed>"); }
        catch { /* diagnostic only */ }
    }
}
