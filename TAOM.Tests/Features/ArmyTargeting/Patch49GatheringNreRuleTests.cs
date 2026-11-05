using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ArmyTargeting.Hooks;

namespace TAOM.Tests.Features.ArmyTargeting;

/// <summary>
/// Patch49 swallows a <see cref="NullReferenceException"/> from vanilla
/// <c>Army.FindBestGatheringSettlementAndMoveTheLeader</c> only in its two proven dead ends (v1.4.8
/// Army.cs:652-728): a kingdomless army with an active leader (<c>Kingdom.Settlements</c>, :659), and no
/// gathering settlement with a null nearest-fortification fallback (<c>settlement.GatePosition</c>, :726,
/// after vanilla set <c>AiBehaviorObject</c> to that null at :725). Any other NRE is another fault and
/// propagates (NRE guard audit 2026-10-27).
/// </summary>
[TestClass]
public class Patch49GatheringNreRuleTests
{
    [TestMethod]
    public void IsKnownDeadEnd_KingdomlessArmyWithActiveLeader_IsTrue() =>
        Assert.IsTrue(Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.IsKnownDeadEnd(
            kingdomMissing: true, leaderActive: true, objectiveMissing: false, fallbackMissing: () => false));

    [TestMethod]
    public void IsKnownDeadEnd_KingdomlessArmyWithInactiveLeaderAndAFallback_IsFalse() =>
        Assert.IsFalse(Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.IsKnownDeadEnd(
            kingdomMissing: true, leaderActive: false, objectiveMissing: true, fallbackMissing: () => false));

    [TestMethod]
    public void IsKnownDeadEnd_NoObjectiveAndNoFallback_IsTrue() =>
        Assert.IsTrue(Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.IsKnownDeadEnd(
            kingdomMissing: false, leaderActive: true, objectiveMissing: true, fallbackMissing: () => true));

    [TestMethod]
    public void IsKnownDeadEnd_NoObjectiveButAFallbackExists_IsFalse() =>
        Assert.IsFalse(Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.IsKnownDeadEnd(
            kingdomMissing: false, leaderActive: true, objectiveMissing: true, fallbackMissing: () => false));

    [TestMethod]
    public void IsKnownDeadEnd_ObjectiveSet_IsFalse_AndSkipsTheFallbackSearch()
    {
        var searched = false;

        var result = Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.IsKnownDeadEnd(
            kingdomMissing: false, leaderActive: true, objectiveMissing: false,
            fallbackMissing: () => { searched = true; return true; });

        Assert.IsFalse(result);
        Assert.IsFalse(searched, "The fallback search runs only when the objective is missing.");
    }

    [TestMethod]
    public void Finalizer_NreWhoseArmyCannotBeRead_PropagatesSameInstance()
    {
        var nre = new NullReferenceException("not a proven dead end");

        var returned = Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.Finalizer(nre, null!, null!, true);

        Assert.AreSame(nre, returned, "On any doubt the NRE goes back; it is no longer swallowed by type alone.");
    }

    // --- provenance: the body must have been entered (Codex 2026-10-27 HIGH: a foreign prefix's NRE
    // in a dead-end state was contained) ---

    [TestMethod]
    public void ShouldContain_NreFromTheBodyInADeadEnd_IsTrue() =>
        Assert.IsTrue(Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.ShouldContain(
            new NullReferenceException(), bodyEntered: true, deadEnd: () => true));

    [TestMethod]
    public void ShouldContain_BodyNeverEntered_IsFalse_AndSkipsTheStateRead()
    {
        var read = false;

        var result = Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.ShouldContain(
            new NullReferenceException(), bodyEntered: false, deadEnd: () => { read = true; return true; });

        Assert.IsFalse(result, "A prefix that threw before vanilla's body is not this defect.");
        Assert.IsFalse(read);
    }

    [TestMethod]
    public void ShouldContain_NotADeadEnd_IsFalse() =>
        Assert.IsFalse(Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.ShouldContain(
            new NullReferenceException(), bodyEntered: true, deadEnd: () => false));

    [TestMethod]
    public void ShouldContain_OtherException_IsFalse() =>
        Assert.IsFalse(Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.ShouldContain(
            new InvalidOperationException(), bodyEntered: true, deadEnd: () => true));

    [TestMethod]
    public void Finalizer_BodyNeverEntered_PropagatesSameInstance()
    {
        var nre = new NullReferenceException("a foreign prefix");

        var returned = Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.Finalizer(nre, null!, null!, false);

        Assert.AreSame(nre, returned);
    }

    [TestMethod]
    public void ThePatch_PairsALastPrefixWithTheFinalizerThroughState()
    {
        var type = typeof(Army_FindBestGatheringSettlementAndMoveTheLeader_Patch);
        var prefix = type.GetMethod("Prefix", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
        var finalizer = type.GetMethod("Finalizer")!;

        Assert.AreEqual(Priority.Last, prefix.GetCustomAttribute<HarmonyPriority>()!.info.priority);
        CollectionAssert.AreEqual(new[] { "__state" }, prefix.GetParameters().Select(p => p.Name).ToArray());
        Assert.IsTrue(prefix.GetParameters()[0].IsOut);
        CollectionAssert.Contains(finalizer.GetParameters().Select(p => p.Name).ToArray(), "__state");
    }

    // The Harmony 2.4.2 behaviour the pair relies on, through real Harmony: a prefix that throws before
    // the Last prefix leaves __state false; a throw from the body sees it true.

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void HarmonyState_ThrowFromTheBody_SeesTheLastPrefixRun() =>
        Assert.IsTrue(RunStatePair(foreignPrefix: false));

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void HarmonyState_ThrowFromAnEarlierPrefix_SeesItNotRun() =>
        Assert.IsFalse(RunStatePair(foreignPrefix: true));

    private static bool RunStatePair(bool foreignPrefix)
    {
        const string id = "taom.tests.patch49.state";
        var harmony = new Harmony(id);
        StatePairProbe.Seen = null;
        try
        {
            var target = AccessTools.Method(typeof(StatePairTarget), nameof(StatePairTarget.Run));
            if (foreignPrefix)
                harmony.Patch(target, prefix: new HarmonyMethod(AccessTools.Method(typeof(ForeignThrowingPrefix), nameof(ForeignThrowingPrefix.Prefix)), priority: Priority.First));
            harmony.Patch(target,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(StatePairProbe), nameof(StatePairProbe.Prefix)), priority: Priority.Last),
                finalizer: new HarmonyMethod(AccessTools.Method(typeof(StatePairProbe), nameof(StatePairProbe.Finalizer))));
            StatePairTarget.Run();
            Assert.IsNotNull(StatePairProbe.Seen, "the finalizer did not run");
            return StatePairProbe.Seen!.Value;
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }

    public static class StatePairTarget
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Run()
        {
            object? missing = null;
            _ = missing!.ToString();
        }
    }

    public static class ForeignThrowingPrefix
    {
        public static void Prefix() => throw new NullReferenceException("foreign prefix");
    }

    public static class StatePairProbe
    {
        public static bool? Seen;

        public static void Prefix(out bool __state) => __state = true;

        public static Exception? Finalizer(Exception? __exception, bool __state)
        {
            Seen = __state;
            return null;
        }
    }
}
