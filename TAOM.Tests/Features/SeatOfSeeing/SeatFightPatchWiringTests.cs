using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Client;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;
using TAOM.Features.SeatOfSeeing;
using TAOM.Features.SeatOfSeeing.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>
/// Patch140's patches for an AI main agent and for the take of a soldier (build-spec 8.2 and section 9): the panel's check, the boundary
/// countdown, the siege weapon view and <c>Mission.CanTakeControlOfAgent</c>. Each needs its target and its category, Harmony injects by
/// NAME, and each is a no-op until its gate holds. The ladder queue is not patched, and a test says why.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class SeatFightPatchWiringTests
{
    private static readonly (Type Patch, string Type, string Method)[] Targets =
    {
        (typeof(Patch140_OrderPanelCheck), "TaleWorlds.MountAndBlade.ViewModelCollection.Order.MissionOrderVM", "CheckCanBeOpened"),
        (typeof(Patch140_BoundaryCountdown), "TaleWorlds.MountAndBlade.MissionBoundaryCrossingHandler", "HandleAgentStateChange"),
        (typeof(Patch140_RangedSiegeInput), "TaleWorlds.MountAndBlade.View.MissionViews.SiegeWeapon.RangedSiegeWeaponView", "HandleUserInput"),
        (typeof(Patch140_TakeControlOfAgent), "TaleWorlds.MountAndBlade.Mission", "CanTakeControlOfAgent"),
    };

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    [TestInitialize]
    public void Setup()
    {
        SeatPresence.Set(false);
        SeatPresence.SetHeroFightsOn(false);
    }

    [TestCleanup]
    public void Cleanup()
    {
        SeatPresence.Set(false);
        SeatPresence.SetHeroFightsOn(false);
    }

    private static Type Engine(string fullName)
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(fullName, throwOnError: false)).FirstOrDefault(t => t != null);
        Assert.IsNotNull(type, fullName + " is not in the installed engine");
        return type!;
    }

    private static MethodInfo Resolve((Type Patch, string Type, string Method) target)
    {
        var info = target.Patch.GetCustomAttributes<HarmonyPatch>(inherit: false).Single().info;
        Assert.AreEqual(Engine(target.Type), info.declaringType, target.Patch.Name);
        Assert.AreEqual(target.Method, info.methodName, target.Patch.Name);
        var method = AccessTools.DeclaredMethod(info.declaringType, info.methodName, info.argumentTypes);
        Assert.IsNotNull(method, target.Type + "." + target.Method + " did not resolve in the installed engine");
        return method!;
    }

    [TestMethod]
    public void EveryPatch_HasOneTargetAndTheCategory_AndLivesInTheClient()
    {
        foreach (var (patch, _, _) in Targets)
        {
            Assert.AreEqual(1, patch.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Length, patch.Name);
            var categories = patch.GetCustomAttributes(typeof(HarmonyPatchCategory), inherit: false).Cast<HarmonyPatchCategory>().Select(c => c.info.category);
            CollectionAssert.Contains(categories.ToList(), SeatOfSeeingModule.PatchCategory, patch.Name);
            Assert.AreEqual(typeof(TaomClientComposition).Assembly, patch.Assembly, patch.Name);
            Assert.AreEqual("TAOM.Features.SeatOfSeeing.Hooks", patch.Namespace);
        }
    }

    [TestMethod]
    public void EveryTarget_ResolvesInTheInstalledEngine()
    {
        foreach (var target in Targets)
            Resolve(target);
    }

    [TestMethod]
    public void TheTargets_HaveTheShapesTheContractReads()
    {
        var panel = Resolve(Targets[0]);
        Assert.IsTrue(panel.IsPrivate);
        Assert.AreEqual(typeof(bool), panel.ReturnType);

        var boundary = Resolve(Targets[1]);
        Assert.IsTrue(boundary.IsPrivate);
        CollectionAssert.AreEqual(new[] { "agent", "isAgentOutside", "isTimerActiveForAgent", "timerInstance" }, boundary.GetParameters().Select(p => p.Name).ToArray(),
            "Harmony binds the prefix's agent and isAgentOutside by name");
        Assert.AreEqual(typeof(bool), boundary.GetParameters()[1].ParameterType);

        var siege = Resolve(Targets[2]);
        Assert.IsTrue(siege.IsFamily && siege.IsVirtual, "protected virtual (RangedSiegeWeaponView.cs:79)");
        CollectionAssert.AreEqual(new[] { typeof(float) }, siege.GetParameters().Select(p => p.ParameterType).ToArray());

        var take = Resolve(Targets[3]);
        Assert.AreEqual(typeof(bool), take.ReturnType);
        Assert.AreEqual(1, take.GetParameters().Length);
    }

    [TestMethod]
    public void ThePrefixAndPostfixParameters_AreNamedAsTheEngineNamesThem()
    {
        CollectionAssert.AreEqual(new[] { "agent", "isAgentOutside" }, typeof(Patch140_BoundaryCountdown).GetMethod("Prefix")!.GetParameters().Select(p => p.Name).ToArray());
        Assert.IsTrue(typeof(Patch140_BoundaryCountdown).GetMethod("Prefix")!.GetParameters()[1].ParameterType.IsByRef, "isAgentOutside is written");
        var postfix = typeof(Patch140_TakeControlOfAgent).GetMethod("Postfix")!.GetParameters();
        CollectionAssert.AreEqual(new[] { "__result" }, postfix.Select(p => p.Name).ToArray());
        Assert.IsTrue(postfix[0].ParameterType.IsByRef);
    }

    [TestMethod]
    public void NoEngineSubclassOverridesTheSiegeWeaponViewsInput_SoOnePatchOnTheBaseReachesEveryCall()
    {
        var target = Resolve(Targets[2]);
        var overrides = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic)
            .SelectMany(a => { try { return a.GetTypes(); } catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null)!; } })
            .Where(t => t != target.DeclaringType && target.DeclaringType!.IsAssignableFrom(t))
            .Where(t => t.GetMethod("HandleUserInput", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly) != null)
            .ToList();

        Assert.AreEqual(0, overrides.Count, "a subclass that overrides it would bypass the patch: " + string.Join(", ", overrides.Select(t => t.FullName)));
    }

    // ---- Off, every one is a no-op ----

    [TestMethod]
    public void OffTheGate_ThePrefixesLetTheOriginalRun_AndThePostfixLeavesTheAnswer()
    {
        var outside = true;
        Patch140_BoundaryCountdown.Prefix(null!, ref outside);
        var answer = true;
        Patch140_TakeControlOfAgent.Postfix(ref answer);

        Assert.IsTrue(outside, "not seated");
        Assert.IsTrue(answer, "not seated");
        Assert.IsTrue(Patch140_RangedSiegeInput.Prefix(), "the hero is not handed over");
    }

    [TestMethod]
    public void SeatedWithoutTheHandOff_OnlyTheTakePostfixActs()
    {
        SeatPresence.Set(true);

        var answer = true;
        Patch140_TakeControlOfAgent.Postfix(ref answer);

        Assert.IsFalse(answer, "vanilla's held Kick key takes nothing from the sky");
        Assert.IsTrue(Patch140_RangedSiegeInput.Prefix(), "the siege view skips only for a hero who fights on");
    }

    [TestMethod]
    public void WhileTheHeroFightsOn_TheSiegeWeaponViewSkipsItsInput()
    {
        SeatPresence.SetHeroFightsOn(true);

        Assert.IsFalse(Patch140_RangedSiegeInput.Prefix());
    }

    [TestMethod]
    public void TheBoundaryPrefix_WithNoAgent_ChangesNothingEvenSeated()
    {
        SeatPresence.Set(true, holdsBoundary: true);
        var outside = true;

        Patch140_BoundaryCountdown.Prefix(null!, ref outside);

        Assert.IsTrue(outside);
    }

    [DataTestMethod]
    [DataRow(true, true, true, true)]
    [DataRow(true, true, false, false)]
    [DataRow(true, false, true, false)]
    [DataRow(true, false, false, false)]
    [DataRow(false, true, true, false)]
    [DataRow(false, true, false, false)]
    public void HoldsBoundaryCountdown_NeedsTheSeat_ThePolicysFightOnPermission_AndTheMainAgent(bool seated, bool permitted, bool main, bool holds)
    {
        SeatPresence.Set(seated, holdsBoundary: permitted);

        Assert.AreEqual(holds, SeatPresence.HoldsBoundaryCountdown(main));
    }

    [TestMethod]
    public void HoldsBoundaryCountdown_ReadsTheSeat_NotTheHandOff()
    {
        SeatPresence.SetHeroFightsOn(true);

        Assert.IsFalse(SeatPresence.HoldsBoundaryCountdown(true), "a stand-fast hero outside the line has the same countdown, and the hand-off alone is not the Seat");
        SeatPresence.Set(true, holdsBoundary: true);
        SeatPresence.SetHeroFightsOn(false);
        Assert.IsTrue(SeatPresence.HoldsBoundaryCountdown(true));
    }

    [TestMethod]
    public void HoldsBoundaryCountdown_WithAPolicyThatGivesNoHandOff_IsFalseEvenSeated()
    {
        SeatPresence.Set(true);

        Assert.IsFalse(SeatPresence.HoldsBoundaryCountdown(true), "a session that permits no hand-off keeps the engine's punishment for leaving the line");
    }

    [TestMethod]
    public void LeavingTheSeat_ReleasesTheHoldWithTheSeatedFlag()
    {
        SeatPresence.Set(true, holdsBoundary: true);

        SeatPresence.Set(false);
        SeatPresence.Set(true);

        Assert.IsFalse(SeatPresence.HoldsBoundaryCountdown(true), "a later entry without the permission does not inherit the earlier one's hold");
    }

    [DataTestMethod]
    [DataRow(true, false, false, true)]
    [DataRow(false, true, true, true)]
    [DataRow(false, false, true, false)]
    [DataRow(false, true, false, false)]
    public void CountsAsPlayerControlled_IsTheEnginesAnswer_OrYesForTheHandedMainAgent(bool engine, bool main, bool gate, bool answer)
    {
        SeatPresence.SetHeroFightsOn(gate);

        Assert.AreEqual(answer, SeatPresence.CountsAsPlayerControlled(engine, main));
    }

    // ---- The transpiler ----

    private static bool IsRead(CodeInstruction ci) =>
        (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt) && ci.operand is MethodInfo mi
        && mi == AccessTools.PropertyGetter(typeof(TaleWorlds.MountAndBlade.Agent), nameof(TaleWorlds.MountAndBlade.Agent.IsPlayerControlled));

    private static bool IsGate(CodeInstruction ci) =>
        ci.opcode == OpCodes.Call && ci.operand is MethodInfo mi && mi == AccessTools.Method(typeof(SeatOrderPanelGate), nameof(SeatOrderPanelGate.IsPlayerControlled));

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void TheTranspiler_SwapsTheOneRead_InTheInstalledEngine_AndNothingElse()
    {
        var il = PatchProcessor.GetOriginalInstructions(Resolve(Targets[0])).ToList();
        var logger = Substitute.For<IModLogger>();
        var labels = il.Single(IsRead).labels.ToList();

        var rewritten = SeatOrderPanelTranspiler.LetTheHandedHeroOpenIt(il, logger).ToList();

        logger.DidNotReceive().LogError(Arg.Any<string>());
        Assert.AreEqual(1, rewritten.Count(IsGate));
        Assert.AreEqual(0, rewritten.Count(IsRead), "no read of the engine's flag may remain");
        Assert.AreEqual(il.Count, rewritten.Count, "a call-for-call swap changes no instruction count");
        CollectionAssert.AreEqual(labels, rewritten.Single(IsGate).labels, "the gate call keeps the branch target");
        Assert.IsTrue(rewritten.Count(ci => ci.opcode == OpCodes.Ret) == il.Count(ci => ci.opcode == OpCodes.Ret), "the method's other exits are untouched");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void ThePatchedMethod_BuildsAReplacementTheJitAccepts()
    {
        var target = Resolve(Targets[0]);
        var harmony = new Harmony("taom.test.seat-order-panel-check");
        try
        {
            harmony.Patch(target, transpiler: new HarmonyMethod(typeof(Patch140_OrderPanelCheck).GetMethod(nameof(Patch140_OrderPanelCheck.Transpiler))));
            var patched = Harmony.GetPatchInfo(target);
            Assert.IsTrue(patched.Transpilers.Any(p => p.owner == harmony.Id), "the transpiler is attached");
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    [TestMethod]
    public void TheTranspiler_WithNoRead_ReturnsTheOriginal_AndFailsTheRunWithOneErrorLine()
    {
        var logger = Substitute.For<IModLogger>();
        var original = new List<CodeInstruction> { new(OpCodes.Ldarg_0), new(OpCodes.Ret) };

        var result = SeatOrderPanelTranspiler.LetTheHandedHeroOpenIt(original, logger).ToList();

        CollectionAssert.AreEqual(original.Select(c => c.opcode).ToList(), result.Select(c => c.opcode).ToList());
        logger.Received(1).LogError(Arg.Is<string>(m => m.StartsWith("[SeatOfSeeing]") && m.Contains("found 0")));
    }

    [TestMethod]
    public void TheTranspiler_WithNoRead_ReportsTheSeatOfSeeing()
    {
        var saved = SelfDisables.Ledger;
        SelfDisables.Ledger = new SelfDisableLedger();
        try
        {
            var original = new List<CodeInstruction> { new(OpCodes.Ldarg_0), new(OpCodes.Ret) };

            SeatOrderPanelTranspiler.LetTheHandedHeroOpenIt(original, Substitute.For<IModLogger>()).ToList();

            var taken = SelfDisables.Ledger.Take();
            Assert.AreEqual(1, taken.Count);
            Assert.AreEqual("Patch140_SeatOfSeeing", taken[0].Id);
            StringAssert.Contains(taken[0].Reason, "found 0");
        }
        finally
        {
            SelfDisables.Ledger = saved;
        }
    }

    [TestMethod]
    public void TheTranspiler_WithTwoReads_ReturnsTheOriginalUnchanged_AndFailsTheRunWithOneErrorLine()
    {
        var getter = AccessTools.PropertyGetter(typeof(TaleWorlds.MountAndBlade.Agent), nameof(TaleWorlds.MountAndBlade.Agent.IsPlayerControlled));
        var logger = Substitute.For<IModLogger>();
        var original = new List<CodeInstruction> { new(OpCodes.Ldarg_0), new(OpCodes.Callvirt, getter), new(OpCodes.Pop), new(OpCodes.Ldarg_0), new(OpCodes.Callvirt, getter), new(OpCodes.Ret) };

        var result = SeatOrderPanelTranspiler.LetTheHandedHeroOpenIt(original, logger).ToList();

        Assert.AreEqual(2, result.Count(IsRead));
        Assert.AreEqual(0, result.Count(IsGate));
        logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("found 2")));
    }

    // ---- The ladder queue is left alone (build-spec 8.2, decided by reading the engine) ----

    [TestMethod]
    public void TheLadderQueueManager_IsNotPatched_SoItsLockAndItsListsAreVanillas()
    {
        var patched = typeof(Patch140_OrderPanelCheck).Assembly.GetTypes()
            .Where(t => t.Namespace == "TAOM.Features.SeatOfSeeing.Hooks")
            .SelectMany(t => t.GetCustomAttributes<HarmonyPatch>(inherit: false))
            .Select(a => a.info.declaringType?.Name)
            .ToList();

        CollectionAssert.DoesNotContain(patched, "LadderQueueManager");
    }
}
