using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;

namespace TAOM.Features.MountReins.Hooks;

/// <summary>
/// IL surgery for Patch153: the one <c>MBAgentVisuals.AddHorseReinsClothMesh(MetaMesh, MetaMesh)</c> call in
/// <c>MountVisualCreator.AddMountMesh</c> becomes <see cref="MountReinsHook.AddReinsCloth"/>, with <c>ldarg.1</c> (the
/// mount item) pushed right before it. Labels on the call move to the inserted load, so a branch to the call still
/// loads all four arguments. Any count other than one returns the original stream untouched with one warning.
/// </summary>
public static class MountReinsTranspiler
{
    internal static readonly MethodInfo? VanillaCall =
        AccessTools.Method(typeof(MBAgentVisuals), nameof(MBAgentVisuals.AddHorseReinsClothMesh), new[] { typeof(MetaMesh), typeof(MetaMesh) });

    internal static readonly MethodInfo HookCall =
        AccessTools.Method(typeof(MountReinsHook), nameof(MountReinsHook.AddReinsCloth));

    public static IEnumerable<CodeInstruction> SwapReinsCloth(IEnumerable<CodeInstruction> instructions, IModLogger? logger)
    {
        var list = instructions.ToList();
        var sites = list.Where(IsVanillaCall).ToList();
        if (sites.Count != 1)
        {
            logger?.LogWarning($"[MountReins] expected exactly one AddHorseReinsClothMesh call in MountVisualCreator.AddMountMesh, found {sites.Count}: the reins guard is not applied");
            SelfDisables.Report(MountReinsModule.PatchCategory, $"found {sites.Count} AddHorseReinsClothMesh calls, expected 1");
            return list;
        }

        var call = sites[0];
        var loadMount = new CodeInstruction(OpCodes.Ldarg_1);
        loadMount.labels.AddRange(call.labels);
        call.labels.Clear();
        call.opcode = OpCodes.Call;
        call.operand = HookCall;
        list.Insert(list.IndexOf(call), loadMount);
        return list;
    }

    private static bool IsVanillaCall(CodeInstruction ci) =>
        (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt)
        && ci.operand is MethodInfo mi
        && VanillaCall != null
        && mi == VanillaCall;
}
