using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.TroopWeight.Hooks;

/// <summary>
/// Transfer All in the weighted frame (#1430, from Silas He's v1.5.6 fix package). v1.5.7
/// <c>PartyScreenLogic.TransferAllTroops</c> (:997-1045) budgets <c>num</c> as a headcount against the limit frozen at
/// screen open, then <c>if (num &lt;= 0)</c> moves everything, then clamps row by row. Moving heavy troops therefore
/// overfills the weighted cap. This inserts <c>num = Budget(num, this, command)</c> right before that test; every branch
/// that computed <c>num</c> jumps to it, so the test's labels move to the inserted load. Without its anchors the method
/// stays vanilla and one warning is logged per process (Harmony runs the transpiler again on each re-patch).
/// </summary>
[HarmonyPatch(typeof(PartyScreenLogic), "TransferAllTroops")]
[HarmonyPatchCategory("Patch17_TroopWeight")]
public static class PartyScreenLogic_TransferAllTroops_Patch
{
    private static IOnPartyScreenLogicTransfer? _hook;
    private static IModLogger? _logger;
    private static bool _warned;

    public static void Initialize(IOnPartyScreenLogicTransfer hook, IModLogger? logger)
    {
        _hook = hook;
        _logger = logger;
        _warned = false;
    }

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = Rewrite(instructions.ToList(), out bool applied);
        if (!applied && !_warned)
        {
            _warned = true;
            _logger?.LogWarning("[TroopWeight] Transfer All: the budget anchors are gone from PartyScreenLogic.TransferAllTroops; it keeps vanilla's headcount budget (#1430)");
        }
        return code;
    }

    /// <summary>The rewritten method, or an unchanged copy with <paramref name="applied"/> false when an anchor is
    /// missing.</summary>
    internal static List<CodeInstruction> Rewrite(List<CodeInstruction> original, out bool applied)
    {
        applied = false;
        var code = new List<CodeInstruction>(original);

        var limitGetter = AccessTools.PropertyGetter(typeof(PartyScreenLogic), nameof(PartyScreenLogic.RightPartyMembersSizeLimit));
        if (limitGetter == null)
            return code;
        int getter = code.FindIndex(c => c.Calls(limitGetter));
        int store = getter < 0 ? -1 : code.FindIndex(getter, c => c.IsStloc());
        if (store < 0)
            return code;

        // The test must follow the store at once (v1.5.7 IL_00af, IL_00b0); a wider search could land on the loop's own
        // test and run Budget on every pass.
        int budgetLocal = code[store].LocalIndex();
        int test = store + 1;
        if (test + 2 >= code.Count || !code[test].IsLdloc() || code[test].LocalIndex() != budgetLocal
            || code[test + 1].opcode != OpCodes.Ldc_I4_0
            || (code[test + 2].opcode != OpCodes.Bgt && code[test + 2].opcode != OpCodes.Bgt_S))
            return code;

        // A fresh instruction for the test so the caller's list keeps its labels.
        var testLoad = new CodeInstruction(code[test]);
        var labels = testLoad.ExtractLabels();
        code[test] = testLoad;
        code.InsertRange(test, new[]
        {
            CodeInstruction.LoadLocal(budgetLocal).WithLabels(labels),
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Ldarg_1),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PartyScreenLogic_TransferAllTroops_Patch), nameof(Budget))),
            CodeInstruction.StoreLocal(budgetLocal),
        });
        applied = true;
        return code;
    }

    public static int Budget(int vanillaBudget, PartyScreenLogic logic, PartyScreenLogic.PartyCommand command)
    {
        if (!(TaomSettings.Instance?.EnableTroopWeight ?? true))
            return vanillaBudget;
        return _hook?.OnTransferAllBudget(vanillaBudget, logic, command) ?? vanillaBudget;
    }
}
