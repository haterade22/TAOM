using System;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using System.Xml;

namespace TAOM.Features.CreatureSaddles.Hooks;

/// <summary>
/// Patch154: after the engine reads a Monster from XML, a creature rig gets its own harness family
/// (<see cref="CreatureSaddleFamilies"/>). Runs for every Monster read, in every game; the base_monster copy runs inside
/// Deserialize, so a child is read after its base and keeps its own id. Registry: "Patch154_CreatureSaddleFamilies".
/// </summary>
[HarmonyPatch(typeof(Monster), nameof(Monster.Deserialize))]
[HarmonyPatchCategory(CreatureSaddlesModule.PatchCategory)]
public static class Patch154_MonsterFamily
{
    [HarmonyPostfix]
    public static void Postfix(Monster __instance)
    {
        try
        {
            CreatureSaddleFamilyWriter.ApplyToMonster(__instance);
        }
        catch (Exception ex)
        {
            CreatureSaddlesModule.Logger?.LogError($"[CreatureSaddles] Monster family write failed ({ex.GetType().Name}): the data's family stays");
        }
    }
}

/// <summary>
/// Patch154: after the engine reads an item from XML (every module's copy of an id is merged into one node first), a creature saddle
/// gets its mount's family (<see cref="CreatureSaddleFamilies"/>). Registry: "Patch154_CreatureSaddleFamilies".
/// </summary>
[HarmonyPatch(typeof(ItemObject), nameof(ItemObject.Deserialize), new[] { typeof(MBObjectManager), typeof(XmlNode) })]
[HarmonyPatchCategory(CreatureSaddlesModule.PatchCategory)]
public static class Patch154_HarnessFamily
{
    [HarmonyPostfix]
    public static void Postfix(ItemObject __instance)
    {
        try
        {
            CreatureSaddleFamilyWriter.ApplyToItem(__instance);
        }
        catch (Exception ex)
        {
            CreatureSaddlesModule.Logger?.LogError($"[CreatureSaddles] harness family write failed ({ex.GetType().Name}): the data's family stays");
        }
    }
}
