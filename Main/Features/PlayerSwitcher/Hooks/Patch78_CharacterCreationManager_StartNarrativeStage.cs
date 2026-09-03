using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.PlayerSwitcher.Hooks;

/// <summary>
/// Sends a player who has already picked a lord straight to the career choice, past the six
/// backstory menus whose answers that player's hero never keeps.
/// </summary>
/// <remarks>
/// StartNarrativeStage has exactly one caller in the shipped game (v1.4.8):
/// SandBox.GauntletUI.CharacterCreation.CharacterCreationNarrativeStageView's constructor, which
/// calls it BEFORE building CharacterCreationNarrativeStageVM and before loading the movie. So the
/// walk finishes while no UI exists to render the menus it passes through, and the first screen the
/// player sees is the career menu. Re-entering the stage rebuilds the view, so this fires again on
/// back-navigation and re-reads the selection each time.
///
/// A postfix, not a prefix: vanilla has to set CurrentMenu to the head of the chain first, because
/// the walk moves forward from wherever vanilla left it.
/// </remarks>
[HarmonyPatch(typeof(CharacterCreationManager), nameof(CharacterCreationManager.StartNarrativeStage))]
[HarmonyPatchCategory("Patch78_PlayerSwitcher_CareerFastPath")]
public static class Patch78_CharacterCreationManager_StartNarrativeStage
{
    private static IModLogger? _logger;

    public static void Initialize(IModLogger logger) => _logger = logger;

    [HarmonyPostfix]
    public static void Postfix(CharacterCreationManager __instance)
    {
        try
        {
            IoC.Resolve<INarrativeCareerFastPathService>()
                .SkipToCareerMenu(new NarrativeStageAdapter(__instance));
        }
        catch (Exception ex)
        {
            // Falling through leaves the player in the ordinary backstory flow, which is a working
            // character creation, so there is nothing here worth risking a throw for.
            _logger?.LogError($"Player Switcher: the career fast path could not run: {ex}");
        }
    }
}
