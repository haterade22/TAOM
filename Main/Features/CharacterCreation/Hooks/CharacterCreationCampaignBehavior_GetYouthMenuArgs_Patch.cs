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

        NarrativeHorseGuardPatchHelper.RemoveHorseCharacters(characterCreationManager);
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

        NarrativeHorseGuardPatchHelper.RemoveHorseCharacters(characterCreationManager);
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

        NarrativeHorseGuardPatchHelper.RemoveHorseCharacters(characterCreationManager);
        __result = NarrativeMenuCharacterArgsList.FromGuardArgs(args);
        return false;
    }
}

/// <summary>
/// Fallback Finalizer for SpawnNonHumanNarrativeMenuCharacter. Suppresses NRE/ANE
/// when horse data is invalid. Applied at Patch20 time — may silently fail if
/// SandBox.GauntletUI isn't loaded yet. The primary defense is RemoveHorseCharacters
/// in the prefix guards above; this is belt-and-suspenders.
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
        if (__exception is NullReferenceException)
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
                isFemale: args.IsFemale)
        };
}

internal static class NarrativeHorseGuardPatchHelper
{
    private const string HorseCharacterId = "narrative_character_horse";

    internal static string ResolveTitle(CharacterCreationManager manager, string occupationType)
    {
        var selected = manager.CharacterCreationContent.SelectedTitleType;
        if (!string.IsNullOrEmpty(selected))
            return selected;
        if (!string.IsNullOrEmpty(occupationType))
            return occupationType;
        return "guard";
    }

    internal static void RemoveHorseCharacters(CharacterCreationManager manager)
    {
        var characters = manager.CurrentMenu?.Characters;
        if (characters == null)
            return;

        for (int i = characters.Count - 1; i >= 0; i--)
        {
            if (!characters[i].IsHuman)
                characters.RemoveAt(i);
        }
    }
}
