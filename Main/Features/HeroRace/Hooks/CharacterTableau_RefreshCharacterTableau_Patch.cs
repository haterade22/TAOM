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
    [HarmonyPostfix]
    public static void Postfix()
    {
        // Earliest tableau-lifecycle hook, and it runs for every race including human — so it is the
        // first opportunity after game-init to repair the statics vanilla is about to read.
        ActionIndexCacheRepair.TryEnsureRepaired("tableau-first-time-init");

        // This used to also load CharacterAvatarPatch.json into a public static that nothing read,
        // re-reading the file from disk on every FirstTimeInit and discarding the validator warnings.
        // Removed 2026-08-21 with the Patch72 work: RacePositionStore is the single owner of both
        // framing configs, and a second snapshot here could only ever disagree with it once the
        // in-game tuner started editing rows.
        TableauDiagnostics.Log("p1.firsttimeinit", "Patch1 CharacterTableau.FirstTimeInit ran.");
    }
}

[HarmonyPatch(typeof(CharacterTableau), "RefreshCharacterTableau")]
[HarmonyPatchCategory("Patch2_RefreshTableau")]
public class CharacterTableau_RefreshCharacterTableau_Patch
{
    [HarmonyPrefix]
    public static void Prefix(ref AgentVisuals ____oldAgentVisuals, int ____race, bool ____isFemale)
    {
        // Repair FIRST, unconditionally, before any early-out. This is the load-bearing call site:
        // vanilla's RefreshCharacterTableau body calls GetIdleAction() — which returns the static
        // ActionIndexCache.act_inventory_idle_start — so repairing here means the SAME refresh
        // consumes the corrected value, and an already-constructed tableau self-corrects on its next
        // refresh. CharacterSpawnerService cannot serve as the backstop: it resolves its actions with
        // live Create() calls (so it never reads a poisoned static) and it is skipped entirely for
        // race 0, which is exactly the human case players reported.
        ActionIndexCacheRepair.TryEnsureRepaired("tableau-refresh");

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
                // act_inventory_idle_start. A valid action set is NOT sufficient — the set must also
                // bind a clip to that action. Resolved via Create() (a live native lookup) rather
                // than the static field: reading the static is what can poison ActionIndexCache if
                // it happens before action types load, and a diagnostic must never be able to cause
                // the fault it reports. The static-vs-live comparison itself lives in
                // TableauDiagnostics.ProbeActionIndexHealth, called from the spawner.
                var idle = ActionIndexCache.Create("act_inventory_idle_start");
                string line = $"Patch2 race={____race} female={____isFemale} monster='{monster.StringId}' '{setName}' -> " +
                              $"{TableauDiagnostics.Describe(actionSet)} " +
                              $"idleStart-anim={TableauDiagnostics.DescribeAction(actionSet, idle)}";

                // Positive predicate, not equality against one of DescribeAction's four markers —
                // matching only "<NONE>" let "<action-index-(-1)>" (the poisoned-index case) log as
                // INFO, which is precisely the state this patch exists to surface.
                if (!TableauDiagnostics.HasAnimation(actionSet, idle))
                    TableauDiagnostics.LogError(line + "  <-- NO USABLE IDLE CLIP (bind-pose condition)");
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
