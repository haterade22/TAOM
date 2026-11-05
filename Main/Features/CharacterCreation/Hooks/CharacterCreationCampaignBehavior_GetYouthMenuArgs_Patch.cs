using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Engine;

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
/// Backstop for SpawnNonHumanNarrativeMenuCharacter; the primary defense is RemoveHorseCharacters in
/// the prefix guards above. Vanilla reads the spawn-point entity, both item ids, the horse's
/// <c>HorseComponent.Monster</c>, that monster's <c>ActionSetCode</c> (a dictionary key in
/// <c>MBGlobals.GetActionSet</c>) and <c>MountCreationKey</c> with no null check (v1.4.8 :204-215),
/// and creates the mount entity (:208) before the spawn-point read throws. This prefix skips the
/// spawn when any of those is missing, so the menu loop moves on to the next character. It replaced
/// a finalizer that swallowed every NRE in the method, a foreign one included, and left the
/// half-built entity in the scene (NRE guard audit 2026-10-27). Skipping also skips vanilla's
/// <c>ClearMountEntities()</c> (:203), which matters only with two non-human characters in one menu.
/// Applied at Patch20 time. A null TargetMethod (SandBox.GauntletUI not loaded) fails loudly: Harmony 2.4.2
/// throws, and PatchCategoryIndex leaves the rest of Patch20 unpatched; TryApply logs it.
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

    [HarmonyPrefix]
    static bool Prefix(NarrativeMenuCharacter character, Scene ____characterScene)
    {
        try
        {
            var item = character?.Item1Id == null ? null : Game.Current?.ObjectManager?.GetObject<ItemObject>(character.Item1Id);
            var canSpawn = NarrativeHorseGuardPatchHelper.CanSpawnMount(
                spawnPointFound: character != null && ____characterScene?.FindEntityWithTag(character.SpawnPointEntityId) != null,
                item1Id: character?.Item1Id,
                item2Id: character?.Item2Id,
                actionSetCode: item?.HorseComponent?.Monster?.ActionSetCode,
                mountKey: character?.MountCreationKey);
            if (!canSpawn)
            {
                try
                {
                    IoC.Resolve<TAOM.Core.Logging.IModLogger>()?.LogWarning(
                        $"[CharacterCreation] skipped spawning narrative mount '{character?.StringId}': its spawn point, " +
                        $"item ids, horse action set or mount key is missing (item1='{character?.Item1Id}').");
                }
                catch { /* logger unavailable: the skip stands */ }
            }
            return canSpawn;
        }
        catch
        {
            return true; // could not read the state: vanilla decides, as without this patch
        }
    }
}

/// <summary>
/// Syncs each NarrativeMenuCharacter's Race with the current PlayerCharacter.Race before each
/// agent-visual refresh. Vanilla AddParentsMenu captures Race once at construction time (when
/// PlayerCharacter.Race is still 0=human), but CreateAgentVisual uses character.Race for the
/// action_set lookup ("as_<race>_facegen") while applying PlayerCharacter.Race to the agent's
/// body skeleton. For TAOM custom races (dwarf/elf/orc), this mismatch produced broken poses
/// (dwarf skeleton playing human-rigged anim_father_0). Updating Race here makes the lookup
/// resolve to "as_dwarf_facegen" so the dwarf-skeleton-compatible animations play.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
public static class CharacterCreationNarrativeStageView_RefreshAgentVisuals_Patch
{
    static MethodBase TargetMethod()
    {
        var type = AccessTools.TypeByName(
            "SandBox.GauntletUI.CharacterCreation.CharacterCreationNarrativeStageView");
        return type == null ? null : AccessTools.DeclaredMethod(type, "RefreshAgentVisuals");
    }

    [HarmonyPrefix]
    static void Prefix(object __instance)
    {
        try
        {
            var managerField = __instance?.GetType().GetField("_characterCreationManager",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var manager = managerField?.GetValue(__instance) as CharacterCreationManager;
            var menu = manager?.CurrentMenu;
            if (menu?.Characters == null) return;

            int playerRace = CharacterObject.PlayerCharacter?.Race
                ?? Hero.MainHero?.CharacterObject?.Race
                ?? 0;
            foreach (var character in menu.Characters)
            {
                if (character != null && character.IsHuman && character.Race != playerRace)
                {
                    character.UpdateBodyProperties(character.BodyProperties, playerRace, character.IsFemale);
                }
            }
        }
        catch (Exception ex)
        {
            try { IoC.Resolve<TAOM.Core.Logging.IModLogger>()?.LogWarning($"[Patch20-RaceSync] failed: {ex.Message}"); } catch { }
        }
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

    /// <summary>
    /// The values vanilla SpawnNonHumanNarrativeMenuCharacter uses without a null check. A null id or
    /// action-set code throws <c>ArgumentNullException("key")</c> from a dictionary lookup; the action-set
    /// code is null when the horse item, its HorseComponent or its Monster is missing too.
    /// </summary>
    internal static bool CanSpawnMount(bool spawnPointFound, string item1Id, string item2Id, string actionSetCode, object mountKey) =>
        spawnPointFound && item1Id != null && item2Id != null && actionSetCode != null && mountKey != null;

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

/// <summary>
/// Overrides the vanilla age-30 animation in the Starting Age menu.
/// Vanilla <c>CharacterCreationCampaignBehavior.AgeSelectionAdultOptionOnSelect</c> calls
/// <c>SetAnimationId("act_childhood_athlete")</c>, which renders the player as a
/// horizontally-stretched / lying-down mesh on every TAOM race (all use the human_skeleton
/// chain). Ages 20/40/50 use other anims and render correctly. Replace with
/// <c>act_childhood_focus</c> (the age-20 anim) post-vanilla so the agent stands upright.
/// All other vanilla effects (ChangeAge(30), SetEquipment, SetBirthDay, StartingAge=30,
/// focus/attribute bonuses) are preserved.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
public static class CharacterCreationCampaignBehavior_AgeSelectionAdultOptionOnSelect_Patch
{
    static MethodBase TargetMethod() =>
        AccessTools.DeclaredMethod(
            typeof(CharacterCreationCampaignBehavior),
            "AgeSelectionAdultOptionOnSelect");

    [HarmonyPostfix]
    static void Postfix(CharacterCreationManager characterCreationManager)
    {
        var characters = characterCreationManager?.CurrentMenu?.Characters;
        if (characters == null)
            return;

        foreach (var character in characters)
        {
            if (character.StringId == "player_age_selection_character")
            {
                character.SetAnimationId("act_childhood_focus");
                break;
            }
        }
    }
}
