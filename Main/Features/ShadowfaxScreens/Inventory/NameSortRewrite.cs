using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.Core;

namespace TAOM.Features.ShadowfaxScreens.Inventory;

/// <summary>
/// The pure IL rewrite behind the name-sort skip. <c>SPInventoryVM.InitializeInventory</c> sorts each side with
/// <c>OrderBy&lt;ItemRosterElement,string&gt;</c> straight into <c>ToArray&lt;ItemRosterElement&gt;</c>; both calls
/// become <see cref="ShadowfaxInventoryCalls.SortByNameOrPassThrough"/> (same stack, the instruction mutated in place
/// so labels stay). It swaps only when there are exactly two such sites, each followed by that <c>ToArray</c>;
/// anything else returns the IL unmodified with the reason in <c>problem</c>, which also makes a second pass over
/// its own output a no-op.
/// </summary>
internal static class NameSortRewrite
{
    internal const int ExpectedSites = 2;

    internal static readonly MethodInfo OrderByTarget = typeof(Enumerable).GetMethods()
        .Single(m => m.Name == nameof(Enumerable.OrderBy) && m.GetParameters().Length == 2)
        .MakeGenericMethod(typeof(ItemRosterElement), typeof(string));

    internal static readonly MethodInfo ToArrayTarget = typeof(Enumerable).GetMethod(nameof(Enumerable.ToArray))!
        .MakeGenericMethod(typeof(ItemRosterElement));

    internal static List<CodeInstruction> Rewrite(List<CodeInstruction> instructions, out int swapped, out string? problem)
    {
        swapped = 0;
        problem = null;
        var sites = new List<int>();
        for (var i = 0; i < instructions.Count; i++)
        {
            if (!instructions[i].Calls(OrderByTarget)) continue;
            if (i + 1 >= instructions.Count || !instructions[i + 1].Calls(ToArrayTarget))
            {
                problem = "an OrderBy call is not followed by ToArray";
                return instructions;
            }
            sites.Add(i);
        }

        if (sites.Count != ExpectedSites)
        {
            problem = $"found {sites.Count} OrderBy call sites, expected {ExpectedSites}";
            return instructions;
        }

        var helper = ShadowfaxInventoryCalls.SortByNameMethod;
        foreach (var site in sites)
        {
            instructions[site].opcode = OpCodes.Call;
            instructions[site].operand = helper;
        }
        swapped = sites.Count;
        return instructions;
    }
}
