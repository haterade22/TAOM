using HarmonyLib;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.HeroRace.Hooks;

[HarmonyPatch(typeof(ActionSetCode), "GenerateActionSetNameWithSuffix")]
[HarmonyPatchCategory("Late_ActionSetOverride")]
// Phase 9b #151 — Harmony 2 attribute-based patches require `public static class`. A non-static
// patch class causes unpredictable application behavior (Harmony tries to instantiate it under
// some conditions). All other TAOM patches are static; this one was the outlier.
public static class ActionSetCode_GenerateActionSetNameWithSuffix_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(ref string __result, Monster monster, bool isFemale, string suffix)
    {
        try
        {
            if (monster == null)
            {
                __result = "as_human" + (isFemale ? "_female" : "") + suffix;

                // This fallback silently turns a non-human into a human. It is the only way a
                // dwarf/elf/orc agent can end up running `as_human_warrior`, which is exactly
                // what a player's mission census showed in the Erebor arena (crash bundle
                // d7d9f7d3, 2026-08-06) with no way to tell why. Unlike the success path below
                // this is rare, so logging it costs nothing and names the caller.
                LogNullMonsterOnce(isFemale, suffix);
                return false;
            }

            // Match vanilla: prefer BaseMonster when present, otherwise use full StringId
            var monsterId = !string.IsNullOrEmpty(monster.BaseMonster)
                ? monster.BaseMonster
                : monster.StringId;

            __result = "as_" + monsterId + (isFemale ? "_female" : "") + suffix;

            // Deliberately NOT logged per call. This runs for every suffixed lookup in the game —
            // map, villager, tavern, portrait — and produced ~200 lines a session while adding
            // nothing: each consumer (CC screen, tableau, spawner) already reports the name it
            // asked for alongside whether that name actually resolved. Only the failure below is
            // worth a line.
            return false;
        }
        catch (Exception e)
        {
            // Falling through to vanilla. Diagnostics 2026-07-31: this catch was silent.
            Diagnostics.TableauDiagnostics.LogError($"GenerateActionSetName THREW, deferring to vanilla: {e}");
            return true;
        }
    }

    /// <summary>
    /// Deduped per (sex, suffix): the null-monster fallback repeats for every agent on the same
    /// broken spawn path, and an unthrottled line here would flood the log of exactly the player
    /// whose log we need to read.
    /// </summary>
    private static void LogNullMonsterOnce(bool isFemale, string suffix)
    {
        var key = $"nullmonster.{isFemale}.{suffix}";
        Diagnostics.TableauDiagnostics.LogDeduped(
            key,
            $"GenerateActionSetName: monster was NULL for suffix='{suffix}' isFemale={isFemale} " +
            $"-> falling back to 'as_human{(isFemale ? "_female" : "")}{suffix}'. " +
            "Any non-human agent on this path renders and animates as a human.");
    }
}
