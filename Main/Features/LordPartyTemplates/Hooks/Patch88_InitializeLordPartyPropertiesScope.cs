using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace TAOM.Features.LordPartyTemplates.Hooks;

/// <summary>
/// Scope half of Patch88 for the initial roster (#580).
/// <c>LordPartyComponent.InitializationArgs.InitializeLordPartyProperties(MobileParty, Hero)</c>
/// (installed v1.4.8, <c>LordPartyComponent.cs:29-47</c>) is the one place every lord party's first
/// roster is drawn: <c>LordPartyComponent.CreateLordParty</c> stores the args and
/// <c>OnMobilePartySetOnCreation</c> (:127-131) runs them. Callers of <c>CreateLordParty</c> in the
/// installed engine: <c>MobilePartyHelper.SpawnLordParty</c> (both overloads, so
/// <c>HeroSpawnCampaignBehavior</c>, <c>RebellionsCampaignBehavior</c> and StoryMode's
/// <c>DefeatTheConspiracyQuestBehavior</c>) and <c>CompanionRolesCampaignBehavior</c>. Marking
/// <c>owner</c> here rather than at any one of those covers all of them.
///
/// Same save-and-restore shape as <see cref="Patch88_SpawnLordPartyScope"/>, and for the same
/// reason: this runs nested inside that scope on the daily-tick and new-game paths.
/// </summary>
// BUTR.Harmony.Analyzer 1.0.1.50 does not resolve a nested target type: it reports this attribute as
// BHA0001 against "TaleWorlds.CampaignSystem.Party.PartyComponents.InitializationArgs", a name with the
// containing LordPartyComponent dropped. The target exists on v1.4.8 (LordPartyComponent.cs:14, :29)
// and Patch88LordPartyTemplateTests.InitializeLordPartyProperties_StillResolves_OnTheNestedArgs_MobilePartyAndOwner pins it.
#pragma warning disable BHA0001
[HarmonyPatch(typeof(LordPartyComponent.InitializationArgs), nameof(LordPartyComponent.InitializationArgs.InitializeLordPartyProperties))]
#pragma warning restore BHA0001
[HarmonyPatchCategory(Patch88_LordPartyTemplate.Category)]
public static class Patch88_InitializeLordPartyPropertiesScope
{
    [HarmonyPrefix]
    public static void Prefix(Hero owner, out Hero? __state)
    {
        __state = Patch88_LordPartyTemplate.AmbientOwner;
        Patch88_LordPartyTemplate.AmbientOwner = owner;
    }

    [HarmonyFinalizer]
    public static Exception? Finalizer(Exception? __exception, Hero? __state)
    {
        Patch88_LordPartyTemplate.AmbientOwner = __state;
        return __exception;
    }
}
