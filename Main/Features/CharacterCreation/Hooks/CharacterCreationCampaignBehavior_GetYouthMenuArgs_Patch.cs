using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace TAOM.Features.CharacterCreation.Hooks;

/// <summary>
/// Guards against NullReferenceException in GetYouthMenuNarrativeMenuCharacterArgs when
/// a culture's character creation roster has no horse in its battle equipment set.
/// Cultures like Erebor (dwarves) intentionally omit mounts from CC equipment.
/// When no horse is present, the narrative scene shows only the player character (no mount actor).
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

        string selectedTitleType = characterCreationManager.CharacterCreationContent.SelectedTitleType;
        if (string.IsNullOrEmpty(selectedTitleType))
            selectedTitleType = occupationType;

        bool isFemale = Hero.MainHero?.IsFemale ?? false;
        string playerEquipmentId =
            $"player_char_creation_{culture.StringId}_{selectedTitleType}_{(isFemale ? "f" : "m")}";

        var roster = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(playerEquipmentId);
        if (roster == null)
            return true; // Roster not found — let vanilla run

        if (roster.DefaultEquipment[EquipmentIndex.Horse].Item != null)
            return true; // Horse present — let vanilla run normally

        // No horse in roster: return only the player character entry, skip the mount actor.
        // ModifyMenuCharacters handles an empty or horse-absent list safely.
        __result = new List<NarrativeMenuCharacterArgs>
        {
            new NarrativeMenuCharacterArgs(
                characterId: "player_youth_character",
                age: 17,
                equipmentId: playerEquipmentId,
                animationId: "act_childhood_schooled",
                spawnPointEntityId: "spawnpoint_player_1",
                isHuman: true,
                isFemale: isFemale)
        };
        return false;
    }
}

/// <summary>
/// Guards against NullReferenceException in GetAdultMenuNarrativeMenuCharacterArgs when
/// a culture's character creation roster has no horse in its battle equipment set.
/// Same root cause as the youth stage patch — both methods unconditionally read
/// DefaultEquipment[Horse].Item.StringId to build the horse NarrativeMenuCharacterArgs.
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

        string selectedTitleType = characterCreationManager.CharacterCreationContent.SelectedTitleType;
        if (string.IsNullOrEmpty(selectedTitleType))
            selectedTitleType = occupationType;

        bool isFemale = Hero.MainHero?.IsFemale ?? false;
        string playerEquipmentId =
            $"player_char_creation_{culture.StringId}_{selectedTitleType}_{(isFemale ? "f" : "m")}";

        var roster = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(playerEquipmentId);
        if (roster == null)
            return true; // Roster not found — let vanilla run

        if (roster.DefaultEquipment[EquipmentIndex.Horse].Item != null)
            return true; // Horse present — let vanilla run normally

        // No horse in roster: return only the player character entry, skip the mount actor.
        __result = new List<NarrativeMenuCharacterArgs>
        {
            new NarrativeMenuCharacterArgs(
                characterId: "player_adulthood_character",
                age: 20,
                equipmentId: playerEquipmentId,
                animationId: "act_childhood_schooled",
                spawnPointEntityId: "spawnpoint_player_1",
                isHuman: true,
                isFemale: isFemale)
        };
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
        // Suppress the null-key crash from MBObjectManager.GetObject when horse item ID
        // is null (no horse in the culture's CC equipment roster).
        if (__exception is ArgumentNullException ane && ane.ParamName == "key")
            return null;
        return __exception;
    }
}
