using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Helpers;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.CheatScreenGuard.Hooks;
using TAOM.Features.MissionPerf;
using TAOM.Tests.Migration;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace TAOM.Tests.Features.CheatScreenGuard;

/// <summary>
/// Pins what Patch_CheatScreenGuard depends on in the installed engine (v1.4.8 at the time of writing): both opener
/// shapes, the one Game.get_CheatMode read in each, the swap applied to the real IL and through real Harmony. The openers
/// are never run: neither can run in a test host.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class CheatScreenGuardBindingTests
{
    private static readonly Type InventoryPatch = typeof(InventoryScreenHelper_OpenInventoryPresentation_CheatScreenGuard_Patch);
    private static readonly Type PartyPatch = typeof(PartyScreenHelper_OpenScreenAsNormal_CheatScreenGuard_Patch);

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static MethodInfo InventoryTarget() => AccessTools.Method(typeof(InventoryScreenHelper), "OpenInventoryPresentation");

    private static MethodInfo PartyTarget() => AccessTools.Method(typeof(PartyScreenHelper), nameof(PartyScreenHelper.OpenScreenAsNormal));

    private static MethodInfo Getter() => AccessTools.PropertyGetter(typeof(Game), nameof(Game.CheatMode));

    private static void AssertRewriteSwapsOneSite(MethodInfo target, IReadOnlyList<CallSwap> swaps, string label)
    {
        var logger = Substitute.For<IModLogger>();
        var original = PatchProcessor.GetOriginalInstructions(target).ToList();

        var rewritten = TickProfilerTranspiler.Rewrite(original, swaps, label, logger, out var swapped);

        logger.DidNotReceive().LogWarning(Arg.Any<string>());
        Assert.AreEqual(1, swapped);
        Assert.AreEqual(original.Count, rewritten.Count, "a swap inserts and removes nothing");
        Assert.AreEqual(0, rewritten.Count(ci => ci.Calls(Getter())), "the original read is gone");
        Assert.AreEqual(1, rewritten.Count(ci => ci.opcode == OpCodes.Call
                                                 && ci.operand is MethodInfo m && m.DeclaringType == typeof(CheatScreenGuardCalls)));
    }

    private static void AssertOneTranspilerOneCleanup(Type patch)
    {
        Assert.AreEqual("Patch_CheatScreenGuard", patch.GetCustomAttribute<HarmonyPatchCategory>()!.info.category);
        var methods = patch.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly);
        Assert.AreEqual(1, methods.Count(m => m.GetCustomAttribute<HarmonyTranspiler>() != null), patch.Name);
        Assert.AreEqual(1, methods.Count(m => m.GetCustomAttribute<HarmonyCleanup>() != null), patch.Name);
        foreach (var m in methods)
        {
            Assert.IsNull(m.GetCustomAttribute<HarmonyPrefix>(), m.Name);
            Assert.IsNull(m.GetCustomAttribute<HarmonyPostfix>(), m.Name);
            Assert.IsNull(m.GetCustomAttribute<HarmonyFinalizer>(), m.Name);
            Assert.IsFalse(m.Name is "Prefix" or "Postfix" or "Finalizer", m.Name);
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Targets_HaveTheirV148Shapes()
    {
        RequireGame();

        var inventory = InventoryTarget();
        Assert.IsNotNull(inventory, "InventoryScreenHelper.OpenInventoryPresentation did not resolve.");
        Assert.IsTrue(inventory.IsPrivate && inventory.IsStatic);
        Assert.AreEqual(typeof(void), inventory.ReturnType);
        CollectionAssert.AreEqual(new[] { typeof(TextObject), typeof(Action) },
            inventory.GetParameters().Select(p => p.ParameterType).ToArray());

        var party = PartyTarget();
        Assert.IsNotNull(party, "PartyScreenHelper.OpenScreenAsNormal did not resolve.");
        Assert.IsTrue(party.IsPublic && party.IsStatic);
        Assert.AreEqual(typeof(void), party.ReturnType);
        Assert.AreEqual(0, party.GetParameters().Length);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void GameCheatMode_IsAnInstanceBoolGetterOnTheSealedGame()
    {
        RequireGame();

        var getter = Getter();
        Assert.IsNotNull(getter, "Game.get_CheatMode did not resolve.");
        Assert.IsFalse(getter.IsStatic);
        Assert.AreEqual(typeof(bool), getter.ReturnType);
        Assert.AreEqual(typeof(Game), getter.DeclaringType);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void EachTarget_ReadsCheatModeExactlyOnce_InTheInstalledEngine()
    {
        RequireGame();

        Assert.AreEqual(1, PatchProcessor.GetOriginalInstructions(InventoryTarget()).Count(ci => ci.Calls(Getter())), "inventory opener");
        Assert.AreEqual(1, PatchProcessor.GetOriginalInstructions(PartyTarget()).Count(ci => ci.Calls(Getter())), "party opener");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void Rewrite_OnTheInventoryOpener_SwapsOneSiteWithNoWarning()
    {
        RequireGame();

        AssertRewriteSwapsOneSite(InventoryTarget(), CheatScreenGuardSwaps.BuildInventory(), "InventoryScreenHelper.OpenInventoryPresentation");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void Rewrite_OnThePartyOpener_SwapsOneSiteWithNoWarning()
    {
        RequireGame();

        AssertRewriteSwapsOneSite(PartyTarget(), CheatScreenGuardSwaps.BuildParty(), "PartyScreenHelper.OpenScreenAsNormal");
    }

    // Harmony builds each replacement and the JIT compiles it (an InvalidProgramException from a mis-shaped swap would
    // surface here), then both come off again.
    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void PatchClasses_AppliedToTheInstalledEngine_BuildReplacementsTheJitAccepts()
    {
        RequireGame();
        const string id = "taom.tests.cheatscreenguard.jit";
        var harmony = new Harmony(id);
        try
        {
            harmony.CreateClassProcessor(InventoryPatch).Patch();
            harmony.CreateClassProcessor(PartyPatch).Patch();

            Assert.AreEqual(1, CheatScreenGuardSwaps.LastInventorySwapped);
            Assert.AreEqual(1, CheatScreenGuardSwaps.LastPartySwapped);
            System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(InventoryTarget().MethodHandle);
            System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(PartyTarget().MethodHandle);
        }
        finally
        {
            harmony.UnpatchAll(id);
            CheatScreenGuardSwaps.LastInventorySwapped = -1;
            CheatScreenGuardSwaps.LastPartySwapped = -1;
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PatchClasses_AreOneTranspilerAndOneCleanupEach_InTheLiteralCategory()
    {
        RequireGame();

        AssertOneTranspilerOneCleanup(InventoryPatch);
        AssertOneTranspilerOneCleanup(PartyPatch);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Setting_CheatModeListsAllItemsAndTroops_IsAMapPerformanceBoolOffByDefault()
    {
        RequireGame();

        var property = typeof(BattleLoadDiagnosticsSettings).GetProperty("CheatModeListsAllItemsAndTroops");
        Assert.IsNotNull(property, "BattleLoadDiagnosticsSettings.CheatModeListsAllItemsAndTroops is missing.");
        Assert.AreEqual(typeof(bool), property.PropertyType);
        var attribute = property.GetCustomAttribute<SettingPropertyBoolAttribute>();
        Assert.IsNotNull(attribute);
        Assert.AreEqual("Cheat Mode Lists All Items And Troops", attribute.DisplayName);
        Assert.IsFalse(attribute.RequireRestart, "read at each screen open");
        Assert.IsFalse(string.IsNullOrWhiteSpace(attribute.HintText));
        Assert.AreEqual("Map Performance", property.GetCustomAttribute<SettingPropertyGroupAttribute>()!.GroupName);
        Assert.IsFalse(new BattleLoadDiagnosticsSettings().CheatModeListsAllItemsAndTroops);
    }
}
