using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TAOM.Core.Logging;

namespace TAOM.Features.CharacterCreation.Hooks;

/// <summary>
/// Per-frame sync of the career-menu player character's BodyProperties from
/// <c>Hero.MainHero.BodyProperties</c>. The career menu's <see cref="NarrativeMenuCharacter"/>
/// is constructed at CC initialization (before any culture is selected), so its captured body
/// is stale once Patch29 applies a culture body via <c>SetSelectedCulture</c>. Existing
/// Patch20 syncs <c>Race</c> only on these characters; this patch additionally syncs body for
/// the TAOM-defined "player_career_character" StringId.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory("Patch29_CCBodyProperties")]
public static class CharacterCreationNarrativeStageView_RefreshAgentVisuals_BodySync_Patch
{
    private const string PlayerCareerCharacterId = "player_career_character";

    private static FieldInfo _managerField;

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
            _managerField ??= __instance?.GetType().GetField("_characterCreationManager",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var manager = _managerField?.GetValue(__instance) as CharacterCreationManager;
            var menu = manager?.CurrentMenu;
            if (menu?.Characters == null) return;

            var hero = Hero.MainHero;
            if (hero == null) return;

            var heroBody = hero.BodyProperties;
            var heroIsFemale = hero.IsFemale;
            var playerRace = CharacterObject.PlayerCharacter?.Race ?? hero.CharacterObject?.Race ?? 0;

            foreach (var character in menu.Characters)
            {
                if (character == null) continue;
                if (character.StringId != PlayerCareerCharacterId) continue;
                if (character.BodyProperties == heroBody) continue;

                character.UpdateBodyProperties(heroBody, playerRace, heroIsFemale);
            }
        }
        catch (Exception ex)
        {
            try { IoC.Resolve<IModLogger>()?.LogWarning($"[Patch29-BodySync] failed: {ex.Message}"); } catch { }
        }
    }
}
