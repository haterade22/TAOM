using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace TAOM.Features.LordPartyTemplates.Hooks;

/// <summary>
/// Scope half of Patch88 for the new-game top-up (#580). <c>HeroSpawnCampaignBehavior.SpawnLordParty</c>
/// (installed v1.4.8, :246-278, private, reached from <c>TrySpawnHeroesAndParties</c> on both the
/// new-game follow-up and the daily clan tick) reads <c>Owner.Clan.DefaultPartyTemplate</c> at :265
/// to weight the troops it adds toward the party size limit. The prefix marks <c>hero</c> as the
/// ambient owner for the whole call; the finalizer restores whatever was there before.
///
/// Restore, not clear: the initial roster is built inside this call by
/// <see cref="Patch88_InitializeLordPartyPropertiesScope"/>, whose own restore must hand the
/// ambient owner back to this scope rather than null it mid-flight. Harmony's <c>__state</c> is
/// per invocation, which is exactly the ownership a nested scope needs.
///
/// The finalizer is <c>void</c> (#1155). Patch65 also finalizes this method and swallows
/// <c>InvalidOperationException</c> on purpose; a finalizer that returns a value writes the exception
/// slot every finalizer shares, and a void one cannot, so this scope never decides what Patch65
/// decided and never touches the trace.
/// </summary>
[HarmonyPatch(typeof(HeroSpawnCampaignBehavior), "SpawnLordParty")]
[HarmonyPatchCategory(Patch88_LordPartyTemplate.Category)]
public static class Patch88_SpawnLordPartyScope
{
    [HarmonyPrefix]
    public static void Prefix(Hero hero, out Hero? __state)
    {
        __state = Patch88_LordPartyTemplate.AmbientOwner;
        Patch88_LordPartyTemplate.AmbientOwner = hero;
    }

    [HarmonyFinalizer]
    public static void Finalizer(Hero? __state)
    {
        Patch88_LordPartyTemplate.AmbientOwner = __state;
    }
}
