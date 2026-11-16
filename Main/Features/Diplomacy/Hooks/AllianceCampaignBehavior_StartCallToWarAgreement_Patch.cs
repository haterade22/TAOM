using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace TAOM.Features.Diplomacy.Hooks;

// A call-to-war agreement moves gold (the caller's or the player's wallet to the called kingdom's) and
// only then declares the war through DeclareWarAction.ApplyByCallToWarAgreement, whose prefix would
// refuse a war the phase forbids after the gold had already moved. Skipping the agreement here keeps
// both the gold and the agreement out of a war that cannot start. The veto is ShouldPreventCallToWar: the
// DeclareWarAction prefix's own war veto, or a callToWarRefusals row (#1283), and it defers to the host under
// co-op. Initialize is wired in DiplomacyIoC.InitializeHooks.
// Priority.High, as on the DeclareWarAction and MakePeaceAction prefixes: Coop's own prefix on this
// method does the agreement on the server and returns false, so a veto that ran after it would never
// run (#1343).
[HarmonyPatch(typeof(AllianceCampaignBehavior), nameof(AllianceCampaignBehavior.StartCallToWarAgreement))]
[HarmonyPatchCategory("Patch11_Diplomacy")]
public static class AllianceCampaignBehavior_StartCallToWarAgreement_Patch
{
    private static IOnAllianceAction _hook;

    public static void Initialize(IOnAllianceAction hook)
    {
        _hook = hook;
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.High)]
    public static bool Prefix(Kingdom calledKingdom, Kingdom kingdomToCallToWarAgainst)
    {
        if (_hook == null || calledKingdom == null || kingdomToCallToWarAgainst == null)
            return true;

        return !_hook.ShouldPreventCallToWar(calledKingdom.StringId, kingdomToCallToWarAgainst.StringId);
    }
}
