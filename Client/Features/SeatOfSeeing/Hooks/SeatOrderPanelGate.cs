using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;

namespace TAOM.Features.SeatOfSeeing.Hooks;

/// <summary>What <see cref="Patch140_OrderPanelCheck"/> asks in place of <c>Agent.IsPlayerControlled</c>: the engine's answer, or yes for the main agent while the hero fights on.</summary>
public static class SeatOrderPanelGate
{
    public static bool IsPlayerControlled(Agent agent) => SeatPresence.CountsAsPlayerControlled(agent.IsPlayerControlled, agent.IsMainAgent);
}

/// <summary>The IL change of <see cref="Patch140_OrderPanelCheck"/>: its one read of <c>Agent.IsPlayerControlled</c> becomes a call of <see cref="SeatOrderPanelGate.IsPlayerControlled"/>, or the original stream comes back with one error line.</summary>
public static class SeatOrderPanelTranspiler
{
    private static readonly MethodInfo? PlayerControlled = AccessTools.PropertyGetter(typeof(Agent), nameof(Agent.IsPlayerControlled));

    public static IEnumerable<CodeInstruction> LetTheHandedHeroOpenIt(IEnumerable<CodeInstruction> instructions, IModLogger? logger)
    {
        var list = instructions.ToList();
        var sites = list.Where(IsPlayerControlledRead).ToList();
        if (sites.Count != 1)
        {
            logger?.LogError($"[SeatOfSeeing] expected exactly one Agent.IsPlayerControlled read in MissionOrderVM.CheckCanBeOpened, found {sites.Count}: a hero who fights on cannot open the order panel");
            SelfDisables.Report(SeatOfSeeingModule.PatchCategory, $"found {sites.Count} Agent.IsPlayerControlled reads in MissionOrderVM.CheckCanBeOpened, expected 1");
            return list;
        }

        sites[0].opcode = OpCodes.Call;
        sites[0].operand = AccessTools.Method(typeof(SeatOrderPanelGate), nameof(SeatOrderPanelGate.IsPlayerControlled));
        return list;
    }

    private static bool IsPlayerControlledRead(CodeInstruction ci) =>
        (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt) && ci.operand is MethodInfo mi && mi == PlayerControlled;
}
