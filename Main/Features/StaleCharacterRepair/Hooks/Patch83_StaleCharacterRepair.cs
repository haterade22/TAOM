using HarmonyLib;
using TaleWorlds.ObjectSystem;

namespace TAOM.Features.StaleCharacterRepair.Hooks;

/// <summary>
/// Patch83 — makes save-restored characters with no ModuleData definition inert, before anything
/// can dereference one of their null fields.
///
/// <para><b>The seam is forced by ordering.</b> <c>Campaign.OnGameLoaded</c> (v1.4.8:679-695) runs
/// <c>base.ObjectManager.PreAfterLoad()</c> at :683, <c>base.ObjectManager.AfterLoad()</c> at :687,
/// the crashing <c>CampaignObjectManager.AfterLoad()</c> at :688, and only then dispatches
/// <c>OnGameEarlyLoaded</c> / <c>OnGameLoaded</c> at :691-692. Both load events are AFTER the crash,
/// so no <c>CampaignBehaviorBase</c> can repair the data in time and this cannot be a behavior.</para>
///
/// <para><b>Why <c>PreAfterLoad</c> and not <c>AfterLoad</c>.</b> Both have exactly one call site in
/// the whole engine and both run before the crash, so either works. <c>PreAfterLoad</c> is strictly
/// earlier and therefore also covers <c>CampaignObjectManager</c>/<c>IssueManager</c>/
/// <c>QuestManager.PreAfterLoad</c> at :684-686. The original choice of <c>AfterLoad</c> was
/// justified in review by "vanilla has already fixed what it can by then", which is not true:
/// <c>CharacterObject</c> overrides neither hook, so nothing repairs these fields in between.</para>
///
/// <para><b>It fires once, on a saved-game load only.</b> <c>Campaign.OnGameLoaded</c> is reached
/// from <c>DoLoadingForGameType</c> only inside the <c>GameLoadingType.SavedCampaign</c> branch
/// (:1663-1670); the new-game branch calls <c>OnNewGameCreated</c> instead. A whole-install
/// member-reference scan finds no other caller. A new game therefore never runs this sweep, which
/// is correct: a fresh campaign has no save-restored stubs.</para>
///
/// <para>Thin per ADR-002: resolve, delegate, swallow. The service and adapter swallow their own
/// faults; the catch here covers the resolve itself, because a repair must never be the thing that
/// stops a save from loading.</para>
/// </summary>
[HarmonyPatch(typeof(MBObjectManager), nameof(MBObjectManager.PreAfterLoad))]
[HarmonyPatchCategory("Patch83_StaleCharacterRepair")]
public static class Patch83_StaleCharacterRepair
{
    private static IStaleCharacterRepairService _service;

    public static void Initialize(IStaleCharacterRepairService service) => _service = service;

    // Cleared on module unload like every other patch holding a static injected reference, so a
    // reloaded module never runs against a service from a dead container.
    public static void ResetForUnload() => _service = null;

    [HarmonyPostfix]
    public static void Postfix()
    {
        // No IoC fallback: SubModule.Initialize runs on the statement before the category is
        // applied, so a null here means the wiring itself regressed. A lazy resolve would mask
        // that as a working save load instead of a silent no-op anyone can find.
        try { _service?.RepairStaleCharacters(); }
        catch { /* never block a save load over a repair */ }
    }
}
