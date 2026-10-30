using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Core;
using TAOM.Features.ShadowfaxScreens.Inventory;

namespace TAOM.Tests.Features.ShadowfaxScreens.Inventory;

/// <summary>The name-sort rewrite on synthetic IL shaped like InitializeInventory's two sort statements.</summary>
[TestClass]
public class NameSortRewriteTests
{
    private static readonly MethodInfo OrderBy = typeof(Enumerable).GetMethods()
        .Single(m => m.Name == "OrderBy" && m.GetParameters().Length == 2)
        .MakeGenericMethod(typeof(ItemRosterElement), typeof(string));

    private static readonly MethodInfo ToArray = typeof(Enumerable).GetMethod("ToArray")!
        .MakeGenericMethod(typeof(ItemRosterElement));

    private static readonly MethodInfo Helper =
        typeof(ShadowfaxInventoryCalls).GetMethod(nameof(ShadowfaxInventoryCalls.SortByNameOrPassThrough))!;

    private static List<CodeInstruction> Statement(Label? label = null)
    {
        var first = new CodeInstruction(OpCodes.Ldarg_0);
        if (label.HasValue) first.labels.Add(label.Value);
        return new List<CodeInstruction>
        {
            first,
            new(OpCodes.Ldnull),
            new(OpCodes.Call, OrderBy),
            new(OpCodes.Call, ToArray),
            new(OpCodes.Pop),
        };
    }

    private static List<CodeInstruction> Method(int sites)
    {
        var list = new List<CodeInstruction>();
        for (var i = 0; i < sites; i++) list.AddRange(Statement());
        list.Add(new CodeInstruction(OpCodes.Ret));
        return list;
    }

    [TestMethod]
    public void Rewrite_TwoSites_SwapsBothInPlace_WithNoWarning()
    {
        var list = Method(2);

        var result = NameSortRewrite.Rewrite(list, out var swapped, out var problem);

        Assert.AreEqual(2, swapped);
        Assert.AreEqual(list.Count, result.Count);
        Assert.AreEqual(2, result.Count(c => c.operand is MethodInfo m && m == Helper));
        Assert.AreEqual(0, result.Count(c => c.operand is MethodInfo m && m == OrderBy));
        Assert.AreEqual(2, result.Count(c => c.operand is MethodInfo m && m == ToArray));
        Assert.IsNull(problem);
    }

    [TestMethod]
    public void Rewrite_KeepsLabelsOnTheSwappedInstructions()
    {
        var generator = new DynamicMethod("t", typeof(void), Type.EmptyTypes).GetILGenerator();
        var label = generator.DefineLabel();
        var list = Statement(label);
        list.AddRange(Statement());
        list.Add(new CodeInstruction(OpCodes.Ret));
        list[2].labels.Add(generator.DefineLabel());

        var result = NameSortRewrite.Rewrite(list, out var swapped, out var problem);

        Assert.AreEqual(2, swapped);
        CollectionAssert.AreEqual(new[] { label }, result[0].labels);
        Assert.AreEqual(1, result[2].labels.Count);
        Assert.AreEqual(OpCodes.Call, result[2].opcode);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(3)]
    public void Rewrite_WrongSiteCount_ReturnsTheIlUnmodified_WithOneProblem(int sites)
    {
        var list = Method(sites);

        var result = NameSortRewrite.Rewrite(list, out var swapped, out var problem);

        Assert.AreEqual(0, swapped);
        Assert.AreEqual(sites, result.Count(c => c.operand is MethodInfo m && m == OrderBy));
        Assert.AreEqual(0, result.Count(c => c.operand is MethodInfo m && m == Helper));
        Assert.IsNotNull(problem);
    }

    [TestMethod]
    public void Rewrite_AnOrderByNotFollowedByToArray_Bails()
    {
        var list = Method(2);
        list[3] = new CodeInstruction(OpCodes.Pop);

        var result = NameSortRewrite.Rewrite(list, out var swapped, out var problem);

        Assert.AreEqual(0, swapped);
        Assert.AreEqual(2, result.Count(c => c.operand is MethodInfo m && m == OrderBy));
        Assert.IsNotNull(problem);
    }

    [TestMethod]
    public void Rewrite_SecondPass_BailsWithoutThrowing_AndChangesNothing()
    {
        var first = NameSortRewrite.Rewrite(Method(2), out _, out _);

        var second = NameSortRewrite.Rewrite(first, out var swapped, out var problem);

        Assert.AreEqual(0, swapped);
        Assert.AreEqual(2, second.Count(c => c.operand is MethodInfo m && m == Helper));
        Assert.IsNotNull(problem);
    }
}
