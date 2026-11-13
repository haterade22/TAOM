using System;
using HarmonyLib;

namespace TAOM.Features.FactionMap.Hooks;

/// <summary>
/// Finds the engine's character-creation culture stage view for the three CultureStageView patches. It
/// lives in SandBox.GauntletUI or SandBox.View, which TAOM cannot reference at compile time, so each name
/// is tried in order and the first that resolves wins. Not cached: every Prepare and TargetMethod call
/// probes again. It carries no client-only marker: ServerLoadGateTests allows that attribute only on
/// Harmony patch classes.
/// </summary>
internal static class CultureStageViewType
{
    internal static readonly string[] PossibleTypeNames =
    {
        "SandBox.GauntletUI.CharacterCreation.CharacterCreationCultureStageView",
        "SandBox.View.CharacterCreation.CharacterCreationCultureStageView",
    };

    internal static Type? Find()
    {
        foreach (var typeName in PossibleTypeNames)
        {
            var type = AccessTools.TypeByName(typeName);
            if (type != null) return type;
        }
        return null;
    }
}
