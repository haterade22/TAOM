using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;

namespace TAOM.Features.CharacterCreation.Hooks;

/// <summary>
/// Guards against NullReferenceException in GetYouthMenuNarrativeMenuCharacterArgs when
/// a culture's character creation roster has no horse in its battle equipment set.
/// Cultures like Erebor (dwarves) intentionally omit mounts from CC equipment.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
public static class CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch
{
    static MethodBase TargetMethod() =>
        AccessTools.DeclaredMethod(
            typeof(CharacterCreationCampaignBehavior),
            "GetYouthMenuNarrativeMenuCharacterArgs");

    [HarmonyPrefix]
    static bool Prefix(
        CultureObject culture,
        string occupationType,
        CharacterCreationManager characterCreationManager,
        ref List<NarrativeMenuCharacterArgs> __result)
    {
        if (Game.Current == null)
            return true;

        var service = IoC.Resolve<INarrativeHorseGuardService>();
        var titleType = NarrativeHorseGuardPatchHelper.ResolveTitle(characterCreationManager, occupationType);
        bool isFemale = Hero.MainHero?.IsFemale ?? false;

        var args = service.TryBuildNoHorseArgs(culture.StringId, titleType, isFemale,
            characterId: "player_youth_character", age: 17);

        if (args == null)
            return true;

        __result = NarrativeMenuCharacterArgsList.FromGuardArgs(args);
        return false;
    }
}

/// <summary>
/// Guards against NullReferenceException in GetAdultMenuNarrativeMenuCharacterArgs when
/// a culture's character creation roster has no horse in its battle equipment set.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
public static class CharacterCreationCampaignBehavior_GetAdultMenuArgs_Patch
{
    static MethodBase TargetMethod() =>
        AccessTools.DeclaredMethod(
            typeof(CharacterCreationCampaignBehavior),
            "GetAdultMenuNarrativeMenuCharacterArgs");

    [HarmonyPrefix]
    static bool Prefix(
        CultureObject culture,
        string occupationType,
        CharacterCreationManager characterCreationManager,
        ref List<NarrativeMenuCharacterArgs> __result)
    {
        if (Game.Current == null)
            return true;

        var service = IoC.Resolve<INarrativeHorseGuardService>();
        var titleType = NarrativeHorseGuardPatchHelper.ResolveTitle(characterCreationManager, occupationType);
        bool isFemale = Hero.MainHero?.IsFemale ?? false;

        var args = service.TryBuildNoHorseArgs(culture.StringId, titleType, isFemale,
            characterId: "player_adulthood_character", age: 20);

        if (args == null)
            return true;

        __result = NarrativeMenuCharacterArgsList.FromGuardArgs(args);
        return false;
    }
}

/// <summary>
/// Guards against NullReferenceException in GetAgeSelectionMenuNarrativeMenuCharacterArgs when
/// a culture's character creation roster has no horse in its battle equipment set.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
public static class CharacterCreationCampaignBehavior_GetAgeSelectionMenuArgs_Patch
{
    static MethodBase TargetMethod() =>
        AccessTools.DeclaredMethod(
            typeof(CharacterCreationCampaignBehavior),
            "GetAgeSelectionMenuNarrativeMenuCharacterArgs");

    [HarmonyPrefix]
    static bool Prefix(
        CultureObject culture,
        string occupationType,
        CharacterCreationManager characterCreationManager,
        ref List<NarrativeMenuCharacterArgs> __result)
    {
        if (Game.Current == null)
            return true;

        var service = IoC.Resolve<INarrativeHorseGuardService>();
        var titleType = NarrativeHorseGuardPatchHelper.ResolveTitle(characterCreationManager, occupationType);
        bool isFemale = Hero.MainHero?.IsFemale ?? false;
        int startingAge = characterCreationManager.CharacterCreationContent.StartingAge;

        var args = service.TryBuildNoHorseArgs(culture.StringId, titleType, isFemale,
            characterId: "player_age_selection_character", age: startingAge);

        if (args == null)
            return true;

        __result = NarrativeMenuCharacterArgsList.FromGuardArgs(args);
        return false;
    }
}

/// <summary>
/// Guards against ArgumentNullException in SpawnNonHumanNarrativeMenuCharacter when
/// the youth narrative scene horse character has a null item ID. This happens when a
/// culture's CC roster omits horse slots — ModifyMenuCharacters skips the horse entry,
/// leaving the scene horse character with uninitialized IDs.
/// The Finalizer suppresses the null-key crash so the horse is simply not spawned.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
public static class CharacterCreationNarrativeStageView_SpawnNonHuman_Patch
{
    static MethodBase TargetMethod()
    {
        var type = AccessTools.TypeByName(
            "SandBox.GauntletUI.CharacterCreation.CharacterCreationNarrativeStageView");
        return type == null ? null : AccessTools.DeclaredMethod(type, "SpawnNonHumanNarrativeMenuCharacter");
    }

    [HarmonyFinalizer]
    static Exception Finalizer(Exception __exception)
    {
        if (__exception is ArgumentNullException ane && ane.ParamName == "key")
            return null;
        return __exception;
    }
}

internal static class NarrativeMenuCharacterArgsList
{
    internal static List<NarrativeMenuCharacterArgs> FromGuardArgs(Models.NarrativeHorseGuardArgs args) =>
        new List<NarrativeMenuCharacterArgs>
        {
            new NarrativeMenuCharacterArgs(
                characterId: args.CharacterId,
                age: args.Age,
                equipmentId: args.EquipmentId,
                animationId: args.AnimationId,
                spawnPointEntityId: args.SpawnPointEntityId,
                isHuman: args.IsHuman,
                isFemale: args.IsFemale),
            // Include an empty horse entry so ModifyMenuCharacters clears any stale
            // horse placeholder from a previously-selected mounted culture.
            // SpawnNonHumanNarrativeMenuCharacter will attempt to spawn this entry,
            // hit ArgumentNullException("key") on the empty equipment, and the
            // existing Finalizer suppresses that crash gracefully.
            new NarrativeMenuCharacterArgs(
                characterId: "narrative_character_horse",
                age: 0,
                equipmentId: "",
                animationId: "",
                spawnPointEntityId: "",
                isHuman: false,
                isFemale: false)
        };
}

internal static class NarrativeHorseGuardPatchHelper
{
    internal static string ResolveTitle(CharacterCreationManager manager, string occupationType)
    {
        var selected = manager.CharacterCreationContent.SelectedTitleType;
        return string.IsNullOrEmpty(selected) ? occupationType : selected;
    }
}
