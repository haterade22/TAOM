using HarmonyLib;
using SandBox;
using TaleWorlds.SaveSystem;

namespace TAOM.Features.BattleLoadDiagnostics.Hooks;

// #425 follow-up (PR #429 review, HIGH): the quit-to-load disarm must run BEFORE the next
// campaign's load work, and SubModule.OnGameEnd cannot guarantee that. Verified against the
// installed 1.4.7 DLLs: Game.Destroy has exactly two callers —
// Game::IGameStateManagerOwner.OnStateStackEmpty and MBInitialScreenBase.OnInitialize. The
// second proves OnGameEnd fires on quit-to-MENU (the initial screen destroys the old Game);
// neither sits on the early in-campaign load path, so on quit-to-LOAD the exit window could
// stay armed into MBObjectManager.LoadXML — the suspend-mid-allocation hazard the captured
// stack in #425 shows.
//
// SandBoxSaveHelper.TryLoadSave is the Load Game click itself — the same origin
// SaveLoadDiagnostics stamps as LoadRequested — which precedes any teardown of the old Game
// by construction. A prefix here closes the window earlier than either Destroy caller can.
//
// ResetLifecycle, not a bare disarm: its window close is deliberately unconditional (same
// toggle-off rule as CloseExitWindow), and a load discards the mission the loadout cache
// described, so the full lifecycle reset is the correct scope. Idempotent when no window is
// open. SubModule.OnGameEnd keeps its ResetLifecycle call for the quit-to-menu path this
// patch never sees.
[HarmonyPatch(typeof(SandBoxSaveHelper), nameof(SandBoxSaveHelper.TryLoadSave))]
[HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
public static class SandBoxSaveHelper_TryLoadSave_DisarmPatch
{
    private static IBattleLoadDiagnosticsService? _service;

    public static void Initialize(IBattleLoadDiagnosticsService service) => _service = service;

    [HarmonyPrefix]
    public static void Prefix(SaveGameFileInfo saveInfo)
    {
        var svc = _service;
        if (svc == null) return;
        try { svc.ResetLifecycle(); }
        catch { /* diagnostic only — never break a save load */ }

        _ = saveInfo; // signature match with the engine method; identity is logged by SaveLoadDiagnostics' own patch
    }
}
