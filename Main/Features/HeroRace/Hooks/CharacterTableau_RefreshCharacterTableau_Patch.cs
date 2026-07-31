using HarmonyLib;
using System;
using TAOM.Core.Infrastructure;
using TAOM.Features.HeroRace.Configuration;
using TAOM.Features.HeroRace.Diagnostics;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.Tableaus;
using FaceGen = TaleWorlds.Core.FaceGen;

namespace TAOM.Features.HeroRace.Hooks;

[HarmonyPatch(typeof(CharacterTableau), "FirstTimeInit")]
[HarmonyPatchCategory("Patch1_FirstTimeInit")]
public class CharacterTableau_FirstTimeInit_Patch
{
    public static RacePositionConfig Config;

    [HarmonyPostfix]
    public static void Postfix()
    {
        try
        {
            Config = RacePositionConfig.LoadConfig("CharacterAvatarPatch");
            TableauDiagnostics.Log("p1.firsttimeinit",
                $"Patch1 CharacterTableau.FirstTimeInit ran; CharacterAvatarPatch config loaded (null={Config == null}).");
        }
        catch (Exception e)
        {
            // Prevent game crash during mod initialization. Diagnostics 2026-07-31: this catch was
            // silent, so a config-load failure here was indistinguishable from success.
            TableauDiagnostics.LogError($"Patch1 FirstTimeInit config load threw: {e}");
        }
    }
}

[HarmonyPatch(typeof(CharacterTableau), "RefreshCharacterTableau")]
[HarmonyPatchCategory("Patch2_RefreshTableau")]
public class CharacterTableau_RefreshCharacterTableau_Patch
{
    [HarmonyPrefix]
    public static void Prefix(ref AgentVisuals ____oldAgentVisuals, int ____race, bool ____isFemale)
    {
        try
        {
            if (____oldAgentVisuals == null || ____race < 0)
            {
                TableauDiagnostics.Log($"p2.skip.{____race}",
                    $"Patch2 SKIPPED: oldAgentVisuals null={____oldAgentVisuals == null}, race={____race}. " +
                    "Vanilla resolution is left in place for this refresh.");
                return;
            }

            var monster = FaceGen.GetBaseMonsterFromRace(____race);
            if (monster == null)
            {
                TableauDiagnostics.LogError(
                    $"Patch2 ABORT: GetBaseMonsterFromRace({____race}) returned NULL — tableau keeps vanilla (human) resolution.");
                return;
            }

            string prefix = ____isFemale ? $"as_{monster.StringId}_female" : $"as_{monster.StringId}";
            string setName = $"{prefix}_warrior";
            var actionSet = MBGlobals.GetActionSet(setName);

            // The load-bearing line: an invalid action set here means the visual is refreshed with
            // no usable animation set, which the engine renders as the skeleton's bind pose — the
            // reported "lying down / bendy man".
            string key = $"p2.{____race}.{____isFemale}";
            if (!actionSet.IsValid)
            {
                TableauDiagnostics.LogError(
                    $"Patch2 race={____race} monster='{monster.StringId}' requested '{setName}' -> INVALID action set. " +
                    "This is the bind-pose condition.");
            }
            else
            {
                // Vanilla CharacterTableau.GetIdleAction() poses this visual with
                // act_inventory_idle_start. If the set we just injected has no clip bound to that
                // action, SetAction is a no-op and the character stays in bind pose — a valid
                // action set is NOT sufficient, so resolve the clip itself.
                string idleAnim = TableauDiagnostics.DescribeAction(actionSet, ActionIndexCache.act_inventory_idle_start);
                string line = $"Patch2 race={____race} female={____isFemale} monster='{monster.StringId}' '{setName}' -> " +
                              $"{TableauDiagnostics.Describe(actionSet)} idleStart-anim={idleAnim}";

                if (idleAnim == "<NONE>") TableauDiagnostics.LogError(line + "  <-- NO IDLE CLIP (bind-pose condition)");
                else TableauDiagnostics.Log(key, line);
            }

            var newData = ____oldAgentVisuals.GetCopyAgentVisualsData();
            newData
                .ActionSet(actionSet)
                .Race(____race)
                .Monster(monster);
            ____oldAgentVisuals.Refresh(false, newData, false);
        }
        catch (Exception e)
        {
            // Fall through — vanilla will handle refresh. Diagnostics 2026-07-31: this catch was
            // silent, so an exception here looked identical to the patch never running.
            TableauDiagnostics.LogError($"Patch2 THREW (tableau falls back to vanilla): {e}");
        }
    }
}
