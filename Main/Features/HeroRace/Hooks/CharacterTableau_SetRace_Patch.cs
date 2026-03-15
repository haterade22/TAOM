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
    private static Func<int, int> _raceIndexMapper;

    public static void InitializeRaceMapper(Func<int, int> mapper) => _raceIndexMapper = mapper;

    [HarmonyPrefix]
    static void Prefix(ref int race)
    {
        if (_raceIndexMapper == null) return;

        try
        {
            race = _raceIndexMapper(race);
        }
        catch (Exception)
        {
            // Fall through with original race value
        }
    }

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
