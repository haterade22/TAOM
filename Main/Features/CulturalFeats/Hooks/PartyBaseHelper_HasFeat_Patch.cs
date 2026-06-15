using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.CulturalFeats.Hooks;

/// <summary>
/// Null-safe replacement for vanilla <see cref="PartyBaseHelper.HasFeat"/>. Vanilla's body
/// evaluates <c>if (party.Culture != null)</c>, and <c>PartyBase.Culture =&gt; MapFaction.Culture</c>
/// has NO null guard (PartyBase.cs:255) — so the comparison itself NREs for a faction-less party
/// (<c>LeaderHero == null</c> AND <c>MapFaction == null</c>), e.g. an empty lord party during
/// <c>Army.OnSiegeStarted</c> strength calc.
///
/// TAOM's own feat checks already route through the null-safe <see cref="CultureFeatAdapter.ResolvePartyCulture"/>
/// chokepoint, but every <c>TaomXxxModel</c> override calls <c>base.XxxMethod()</c> FIRST, and several
/// vanilla base methods call <c>HasFeat</c> internally (verified v1.4.6: <c>DailyBeingAtArmyInfluenceAward</c>,
/// <c>CalculateRenownGain</c>, <c>CalculateFinalSpeed</c>, <c>GetGoldCostForUpgrade</c>) — so the crash can
/// still fire INSIDE the base call, which TAOM-side null-safety cannot reach. This Prefix replaces the
/// whole vanilla body with the SAME precedence (LeaderHero -&gt; party.Culture[=MapFaction] -&gt; Owner -&gt;
/// Settlement) resolved null-safely, returning <c>false</c> instead of throwing when no culture resolves.
/// Behaviorally identical to vanilla for every non-crashing input.
///
/// Fixes the residual base-call exposure Codex review caught. See
/// <c>docs/reviews/rca-culturefeat-partyculture-nre-2026-06-15.md</c>. <c>HasFeat</c> is a campaign-tick
/// cultural-feat check (not per-frame), so the one extra resolve per call is negligible. No recursion:
/// the Prefix calls <c>CultureObject.HasFeat</c> on the resolved culture, a different method than the
/// patched static <c>PartyBaseHelper.HasFeat</c>.
/// </summary>
[HarmonyPatch(typeof(PartyBaseHelper), nameof(PartyBaseHelper.HasFeat), new[] { typeof(PartyBase), typeof(FeatObject) })]
[HarmonyPatchCategory("Patch18_CulturalFeats")]
public static class PartyBaseHelper_HasFeat_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(PartyBase party, FeatObject feat, ref bool __result)
    {
        __result = CultureFeatAdapter.ResolvePartyCulture(party)?.HasFeat(feat) ?? false;
        return false; // skip the throwing vanilla body
    }
}
