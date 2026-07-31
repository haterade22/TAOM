using HarmonyLib;
using System;
using TAOM.Core.Infrastructure;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.Tableaus;

namespace TAOM.Features.HeroRace.Hooks;

[HarmonyPatch(typeof(CharacterTableau), nameof(CharacterTableau.SetRace))]
[HarmonyPatchCategory("Patch3_SetRace")]
public class CharacterTableau_SetRace_Patch
{
    [HarmonyPostfix]
    static void Postfix(CharacterTableau __instance)
    {
        try
        {
            var agentVisuals = ReflectionHelper.GetFieldValue<CharacterTableau, AgentVisuals>(__instance, "_agentVisuals");
            agentVisuals?.Reset();
            var oldAgentVisuals = ReflectionHelper.GetFieldValue<CharacterTableau, AgentVisuals>(__instance, "_oldAgentVisuals");
            oldAgentVisuals?.Reset();
            ReflectionHelper.CallPrivateMethod(__instance, "InitializeAgentVisuals", new object[] { });

            // Diagnostics 2026-07-31: this postfix drives a FULL visual re-init on every race change.
            // A reflection miss on either private field (renamed by an engine build) would silently
            // skip the reset and leave the previous race's visuals in place.
            Diagnostics.TableauDiagnostics.Log("p3.setrace",
                $"Patch3 SetRace: re-initialised agent visuals (agentVisualsNull={agentVisuals == null}, oldAgentVisualsNull={oldAgentVisuals == null}).");
        }
        catch (Exception e)
        {
            // Prevent game crash if ReflectionHelper or IoC is not ready. Diagnostics 2026-07-31:
            // this catch was silent, hiding reflection failures against a drifted engine build.
            Diagnostics.TableauDiagnostics.LogError($"Patch3 SetRace THREW (visuals not re-initialised): {e}");
        }
    }
}
