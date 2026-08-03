using System.Collections.Generic;
using System.Diagnostics;
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

    // Capturing a managed stack costs real time, so it is bounded to the first few agents of a
    // load by engine index — no counter, no reset, and the answer is only ever interesting for
    // the agents at the head of the spawn sequence. Agent.Index is mission-assigned from 0.
    private const int MaxAgentIndexForSpawnOrigin = 2;

    // Harmony inserts a generated wrapper per patched method in the chain, so the useful callers
    // sit deeper than the raw frame count suggests.
    private const int MaxSpawnOriginFrames = 6;

    [HarmonyPrefix]
    public static void Prefix(Agent __instance)
    {
        var svc = _service;
        if (svc == null || !svc.IsEnabled) return;
        if (!BattleLoadLoadingWindow.IsOpen) return;
        try
        {
            var snapshot = _adapter?.Capture(__instance, CaptureSpawnOrigin(__instance));
            if (snapshot != null) svc.LogAgentEquipBegin(snapshot);
        }
        catch { /* diagnostic only — never break agent spawn */ }
    }

    // Names the engine method that built this agent. A `musician_dunland` agent turned up inside a
    // TournamentFight mission whose 13 behaviors contain no MissionAgentHandler and whose roster
    // (FightTournamentGame.GetParticipantCharacters) cannot select a musician — the loadout dump
    // could not say who spawned it, and the caller's name can.
    private static string? CaptureSpawnOrigin(Agent? agent)
    {
        try
        {
            if ((agent?.Index ?? int.MaxValue) > MaxAgentIndexForSpawnOrigin) return null;

            var trace = new StackTrace(fNeedFileInfo: false);
            var names = new List<string?>(trace.FrameCount);
            for (int i = 0; i < trace.FrameCount; i++)
            {
                var method = trace.GetFrame(i)?.GetMethod();
                if (method == null) continue;

                // FULL name — the formatter filters on namespaces, and passing Type.Name instead
                // silently disabled every one of its filters on the first cut. Harmony's generated
                // wrappers have no declaring type and carry the whole path in the method name.
                var declaring = method.DeclaringType?.FullName;
                names.Add(string.IsNullOrEmpty(declaring) ? method.Name : $"{declaring}.{method.Name}");
            }

            var formatted = SpawnOriginFormatter.Format(names, MaxSpawnOriginFrames);
            return string.IsNullOrEmpty(formatted) ? null : formatted;
        }
        catch { return null; }
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
