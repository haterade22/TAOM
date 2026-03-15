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
        }
        catch (Exception)
        {
            // Prevent game crash if ReflectionHelper or IoC is not ready
        }
    }
}
