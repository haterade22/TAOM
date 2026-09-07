using HarmonyLib;
using TaleWorlds.ObjectSystem;

namespace TAOM.Features.CharacterSkillsRepair.Hooks;

/// <summary>
/// Patch83 — repairs characters with no skill set before anything can read one.
///
/// <para><b>The seam is forced by ordering.</b> <c>Campaign.OnGameLoaded</c> (v1.4.8 :679-695) runs
/// <c>base.ObjectManager.AfterLoad()</c> at :687 and <c>CampaignObjectManager.AfterLoad()</c> at
/// :688, and the crash is inside the latter. <c>OnGameEarlyLoaded</c> and <c>OnGameLoaded</c> are
/// dispatched at :691 and :692, i.e. AFTER it — so no <c>CampaignBehaviorBase</c> load event can
/// possibly repair the data in time, and this cannot be a behavior. A postfix on the public
/// <c>MBObjectManager.AfterLoad</c> is the last point before the crashing call, and by then every
/// object's <c>AfterLoadInternal</c> has run, so any character vanilla could still fix itself has
/// been fixed.</para>
///
/// <para>It also fires on a new game and on the initial data load, where the sweep finds nothing
/// and returns silently. That is cheaper than trying to tell the two apart.</para>
///
/// <para>Thin per ADR-002: resolve, delegate, swallow. The service and adapter already swallow
/// their own faults; the catch here covers an IoC resolve failing, because a diagnostic repair must
/// never be the thing that stops a save from loading.</para>
/// </summary>
[HarmonyPatch(typeof(MBObjectManager), nameof(MBObjectManager.AfterLoad))]
[HarmonyPatchCategory("Patch83_CharacterSkillsRepair")]
public static class Patch83_CharacterSkillsRepair
{
    private static ICharacterSkillsRepairService _service;

    public static void Initialize(ICharacterSkillsRepairService service) => _service = service;

    [HarmonyPostfix]
    public static void Postfix()
    {
        try { (_service ?? TAOM.IoC.Resolve<ICharacterSkillsRepairService>())?.RepairMissingSkillSets(); }
        catch { /* never block a save load over a repair */ }
    }
}
