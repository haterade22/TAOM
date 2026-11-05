using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TAOM.Features.ShadowfaxScreens.Inventory;
using TAOM.Features.ShadowfaxScreens.Inventory.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.ShadowfaxScreens.Inventory;

/// <summary>
/// Drift guard for the Inventory fast open. The transpiler soft-fails (a changed shape leaves vanilla with one
/// warning), so an engine bump would silently drop the speed-up; these feed the REAL installed IL through the
/// production rewrite, resolve every target and engine member the patches touch, and pin the facts the name-sort
/// skip relies on: the constructor and AfterReset sort the rows after InitializeInventory, with ItemDescription as
/// the tie-break.
/// </summary>
[TestClass]
public class InventoryBindingTests
{
    private const BindingFlags AllMembers = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static MethodBase InitializeInventory() =>
        AccessTools.Method(typeof(SPInventoryVM), "InitializeInventory", Type.EmptyTypes)
        ?? throw new AssertFailedException("SPInventoryVM.InitializeInventory() did not resolve.");

    private static ConstructorInfo InventoryCtor() =>
        AccessTools.Constructor(typeof(SPInventoryVM), new[]
        {
            typeof(InventoryLogic), typeof(bool), typeof(Func<WeaponComponentData, ItemObject.ItemUsageSetFlags>),
        }) ?? throw new AssertFailedException("The 3-argument SPInventoryVM constructor did not resolve.");

    private static List<CodeInstruction> Il(MethodBase method) => PatchProcessor.GetOriginalInstructions(method).ToList();

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void NameSortRewrite_SwapsBothSortSites_InTheInstalledEngine()
    {
        RequireGame();

        var rewritten = NameSortRewrite.Rewrite(Il(InitializeInventory()), out var swapped, out var problem);

        Assert.IsNull(problem);
        Assert.AreEqual(2, swapped);
        Assert.AreEqual(2, rewritten.Count(c => c.operand is MethodInfo m && m == ShadowfaxInventoryCalls.SortByNameMethod));
        Assert.AreEqual(0, rewritten.Count(c => c.Calls(NameSortRewrite.OrderByTarget)));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void SecondPassOverTheRealIl_BailsWithoutThrowing()
    {
        RequireGame();
        var sort = NameSortRewrite.Rewrite(Il(InitializeInventory()), out _, out _);

        NameSortRewrite.Rewrite(sort, out var sortAgain, out var problem);

        Assert.AreEqual(0, sortAgain);
        Assert.IsNotNull(problem);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void EveryRealSite_HasAHelperWithTheSameStackShape()
    {
        RequireGame();
        var orderBy = NameSortRewrite.OrderByTarget;
        var sort = ShadowfaxInventoryCalls.SortByNameMethod;
        CollectionAssert.AreEqual(orderBy.GetParameters().Select(p => p.ParameterType).ToList(),
            sort.GetParameters().Select(p => p.ParameterType).ToList());
        var toArrayParameter = NameSortRewrite.ToArrayTarget.GetParameters().Single().ParameterType;
        Assert.IsTrue(toArrayParameter.IsAssignableFrom(sort.ReturnType),
            "The sort helper's result must be accepted where ToArray<ItemRosterElement> reads its source.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void InventoryVm_HasExactlyOneConstructor_TheOneTheHookPatches()
    {
        RequireGame();
        Assert.AreEqual(1, typeof(SPInventoryVM).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Length);
        Assert.IsNotNull(InventoryCtor());
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void NameSortTranspiler_ProducesIlTheJitAccepts_OnTheRealInitializeInventory()
    {
        RequireGame();
        const string id = "taom.tests.inventory.jit";
        var harmony = new Harmony(id);
        try
        {
            harmony.CreateClassProcessor(typeof(SPInventoryVM_InitializeInventory_ShadowfaxPatch)).Patch();
            RuntimeHelpers.PrepareMethod(((MethodInfo)InitializeInventory()).MethodHandle);
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PatchTargets_ResolveWithTheParameterNamesTheHooksInject()
    {
        RequireGame();
        var prefix = typeof(SPInventoryVM_Ctor_ShadowfaxPatch).GetMethod("Prefix")!;
        CollectionAssert.AreEqual(new[] { "__state" }, prefix.GetParameters().Select(p => p.Name).ToList());
        var finalizer = typeof(SPInventoryVM_Ctor_ShadowfaxPatch).GetMethod("Finalizer")!;
        CollectionAssert.AreEqual(new[] { "inventoryLogic", "____usageType", "__exception", "__state" },
            finalizer.GetParameters().Select(p => p.Name).ToList());
        var usageType = AccessTools.Field(typeof(SPInventoryVM), "_usageType");
        Assert.IsNotNull(usageType, "SPInventoryVM._usageType did not resolve.");
        Assert.AreEqual(typeof(InventoryScreenHelper.InventoryMode), usageType.FieldType);
        var logicParameter = InventoryCtor().GetParameters()[0];
        Assert.AreEqual("inventoryLogic", logicParameter.Name);
        Assert.AreEqual(typeof(InventoryLogic), logicParameter.ParameterType);

        var rowsPrefix = typeof(SPInventoryVM_InitializeInventory_ShadowfaxPatch).GetMethod("Prefix")!;
        CollectionAssert.AreEqual(new[] { "__state" }, rowsPrefix.GetParameters().Select(p => p.Name).ToList());
        var rowsFinalizer = typeof(SPInventoryVM_InitializeInventory_ShadowfaxPatch).GetMethod("Finalizer")!;
        CollectionAssert.AreEqual(new[] { "__state" }, rowsFinalizer.GetParameters().Select(p => p.Name).ToList());

        Assert.IsNotNull(InitializeInventory());
        Assert.IsNotNull(InventoryCtor());
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void InitializeInventory_IsCalledOnlyByTheConstructorAndAfterReset()
    {
        RequireGame();
        var target = InitializeInventory();
        var callers = new List<string>();
        var bodies = typeof(SPInventoryVM).GetMethods(AllMembers).Cast<MethodBase>()
            .Concat(typeof(SPInventoryVM).GetConstructors(AllMembers));

        foreach (var method in bodies.Where(m => m.DeclaringType == typeof(SPInventoryVM) && !m.IsAbstract))
        {
            List<CodeInstruction> il;
            try { il = Il(method); }
            catch { continue; }
            if (il.Any(c => c.Calls((MethodInfo)target))) callers.Add(method.IsConstructor ? ".ctor" : method.Name);
        }

        CollectionAssert.AreEquivalent(new[] { ".ctor", "AfterReset" }, callers.Distinct().ToList(),
            "A new caller of InitializeInventory must also sort its rows, or the name-sort skip shows an unsorted list.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void ConstructorAndAfterReset_SortTheRowsAfterInitializeInventory()
    {
        RequireGame();
        var initialize = (MethodInfo)InitializeInventory();
        var sortByDefault = AccessTools.Method(typeof(SPInventorySortControllerVM), "SortByDefaultState");
        Assert.IsNotNull(sortByDefault, "SPInventorySortControllerVM.SortByDefaultState did not resolve.");
        var afterReset = AccessTools.Method(typeof(SPInventoryVM), "AfterReset");
        Assert.IsNotNull(afterReset, "SPInventoryVM.AfterReset did not resolve.");

        foreach (var method in new MethodBase[] { InventoryCtor(), afterReset })
        {
            var il = Il(method);
            var init = il.FindIndex(c => c.Calls(initialize));
            var sort = il.FindLastIndex(c => c.Calls(sortByDefault));
            Assert.IsTrue(init >= 0, method.Name + " no longer calls InitializeInventory.");
            Assert.IsTrue(sort > init, method.Name + " no longer sorts the rows after InitializeInventory.");
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void ItemComparerTieBreak_StillReadsItemDescription()
    {
        RequireGame();
        var comparer = AccessTools.Inner(typeof(SPInventorySortControllerVM), "ItemComparer");
        Assert.IsNotNull(comparer, "SPInventorySortControllerVM.ItemComparer did not resolve.");
        var resolve = comparer.GetMethod("ResolveEquality", AllMembers);
        Assert.IsNotNull(resolve, "ItemComparer.ResolveEquality did not resolve.");

        var getter = AccessTools.PropertyGetter(typeof(SPItemVM), "ItemDescription");
        Assert.IsNotNull(getter, "SPItemVM.ItemDescription did not resolve.");
        Assert.IsTrue(Il(resolve).Count(c => c.operand is MethodInfo m && m.MetadataToken == getter.MetadataToken && m.Module == getter.Module) >= 2,
            "ResolveEquality no longer compares both rows' ItemDescription.");
    }
}
