using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CreatureSiegeRole.Domain;
using TAOM.Features.CreatureSiegeRole.Hooks;
using TAOM.Tests.Migration;
using TaleWorlds.MountAndBlade;

namespace TAOM.Tests.Features.CreatureSiegeRole;

/// <summary>
/// The static facade the four game models call, from the engine's AI thread (the detachment cost) and from whichever thread
/// native picks for a hit (the gate damage). An <c>Agent</c> cannot be built outside the game, so the facade reduces its engine
/// arguments to primitives in a one-line public method and the decisions live in an internal overload the tests drive. The cost
/// hook's whole contract is cheapness: one volatile read, a reference compare and an array index, nothing allocated.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class CreatureSiegeHooksTests
{
    private static readonly object Mission = new();
    private IModLogger _logger = null!;

    [ClassInitialize]
    public static void Init(TestContext _) => GameAssemblies.EnsureLoaded();

    [TestInitialize]
    public void Setup()
    {
        CreatureSiegeSnapshot.Clear();
        _logger = Substitute.For<IModLogger>();
        CreatureSiegeHooks.Logger = _logger;
        CreatureSiegeHooks.BlowLogThrottle = new SiegeLogThrottle(4, 1.0);
    }

    [TestCleanup]
    public void Cleanup()
    {
        CreatureSiegeSnapshot.Clear();
        CreatureSiegeHooks.Logger = null;
    }

    private static CreatureSiegeSnapshot Snapshot(float multiplier = 2f) =>
        new(Mission, new[] { false, true, true }, multiplier, new object[] { new object() });

    // --- the detachment cost (AI thread) -------------------------------------------------------------------------------

    [TestMethod]
    public void DetachmentCost_NoSnapshot_IsTheBase()
    {
        Assert.AreEqual(1f, CreatureSiegeHooks.DetachmentCost(null, Mission, 1, 1f));
    }

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(2)]
    public void DetachmentCost_ACreatureOfThisMission_IsPositiveInfinity(int race)
    {
        Assert.AreEqual(float.PositiveInfinity, CreatureSiegeHooks.DetachmentCost(Snapshot(), Mission, race, 1f));
        Assert.AreEqual(float.PositiveInfinity, CreatureSiegeHooks.DetachmentCost(Snapshot(), Mission, race, 10f), "a banner bearer's 10");
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(3)]
    [DataRow(-1)]
    [DataRow(int.MaxValue)]
    public void DetachmentCost_AnyOtherRace_IsTheBase(int race)
    {
        Assert.AreEqual(1f, CreatureSiegeHooks.DetachmentCost(Snapshot(), Mission, race, 1f));
    }

    [TestMethod]
    public void DetachmentCost_ACreatureOfAnotherMission_IsTheBase_SoAStaleSnapshotNeverAppliesToTheNextBattle()
    {
        Assert.AreEqual(1f, CreatureSiegeHooks.DetachmentCost(Snapshot(), new object(), 1, 1f));
        Assert.AreEqual(1f, CreatureSiegeHooks.DetachmentCost(Snapshot(), null, 1, 1f));
    }

    [TestMethod]
    public void DetachmentCost_ANaNBaseOfANonCreature_PassesThrough()
    {
        Assert.IsTrue(float.IsNaN(CreatureSiegeHooks.DetachmentCost(Snapshot(), Mission, 0, float.NaN)));
    }

    [TestMethod]
    public void DetachmentCost_TheAgentOverload_WithNoSnapshotOrNoAgent_IsTheBase()
    {
        Assert.AreEqual(1f, CreatureSiegeHooks.DetachmentCost((Agent)null!, 1f), "no snapshot");

        CreatureSiegeSnapshot.Publish(Snapshot());

        Assert.AreEqual(1f, CreatureSiegeHooks.DetachmentCost((Agent)null!, 1f), "a snapshot but no agent");
    }

    // --- the allocation contract, read from the IL ----------------------------------------------------------------------------
    //
    // The cost hook runs on the engine's asynchronous AI thread for every agent on every detachment tick, so its contract is that it
    // allocates nothing. A process-wide allocation counter is flaky, and GC.GetAllocatedBytesForCurrentThread does not exist on
    // net472, so the contract is read from the IL: no newobj, newarr or box in either overload or in the TAOM members they call, and
    // no call to anything outside a short list of plain reads (a log call, a LINQ call or a closure would show up as a new callee
    // and fail the second test).

    // Properties, not static fields: `typeof(Agent)` loads the engine assembly, which must wait for ClassInitialize.
    private static MethodInfo CostOfAgent => typeof(CreatureSiegeHooks).GetMethod(nameof(CreatureSiegeHooks.DetachmentCost),
        new[] { typeof(Agent), typeof(float) })!;

    private static MethodInfo CostOfPrimitives => typeof(CreatureSiegeHooks).GetMethod(nameof(CreatureSiegeHooks.DetachmentCost),
        BindingFlags.NonPublic | BindingFlags.Static, null,
        new[] { typeof(CreatureSiegeSnapshot), typeof(object), typeof(int), typeof(float) }, null)!;

    // The TAOM members the cost path reads through, which the allocation scan covers along with the two overloads.
    private static MethodBase[] CostPath => new MethodBase[]
    {
        CostOfAgent,
        CostOfPrimitives,
        typeof(CreatureSiegeSnapshot).GetProperty(nameof(CreatureSiegeSnapshot.Current))!.GetGetMethod()!,
        typeof(CreatureSiegeSnapshot).GetProperty(nameof(CreatureSiegeSnapshot.MissionToken))!.GetGetMethod()!,
        typeof(CreatureSiegeSnapshot).GetMethod(nameof(CreatureSiegeSnapshot.IsCreatureRace))!,
        typeof(CreatureSiegeRules).GetMethod(nameof(CreatureSiegeRules.DetachmentCost))!,
    };

    private static readonly HashSet<string> PlainReads = new(StringComparer.Ordinal)
    {
        "CreatureSiegeSnapshot.get_Current",
        "CreatureSiegeSnapshot.get_MissionToken",
        "CreatureSiegeSnapshot.IsCreatureRace",
        "CreatureSiegeRules.DetachmentCost",
        "CreatureSiegeHooks.DetachmentCost",
        "Object.ReferenceEquals",
        "Agent.get_Mission",
        "Agent.get_Character",
        "BasicCharacterObject.get_Race",
    };

    private static IReadOnlyList<OpCode> AllocatingOpCodes(MethodBase method) =>
        IlCallScanner.ExtractOpCodes(method.GetMethodBody()!.GetILAsByteArray()!)
            .Where(op => op == OpCodes.Newobj || op == OpCodes.Newarr || op == OpCodes.Box)
            .ToList();

    // A method that allocates on purpose: the positive control that proves the scan can fail.
    private static object Allocator() => new object();

    [TestMethod]
    public void DetachmentCost_TheCostPath_ContainsNoAllocatingInstruction()
    {
        foreach (var method in CostPath)
        {
            var found = AllocatingOpCodes(method);
            Assert.AreEqual(0, found.Count, $"{method.DeclaringType!.Name}.{method.Name} contains {string.Join(", ", found.Select(o => o.Name))}");
        }
    }

    [TestMethod]
    public void DetachmentCost_TheTwoOverloads_CallOnlyPlainReads()
    {
        foreach (var method in new MethodBase[] { CostOfAgent, CostOfPrimitives })
        {
            var callees = IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody()!.GetILAsByteArray()!)
                .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
                .Distinct()
                .ToList();

            var unexpected = callees.Where(c => !PlainReads.Contains(c)).ToList();
            Assert.AreEqual(0, unexpected.Count, $"{method.Name} calls {string.Join(", ", unexpected)}, which is not on the list of plain reads");
            Assert.IsTrue(callees.Count > 0, $"{method.Name} read nothing: the scan walked an empty body");
        }
    }

    [TestMethod]
    public void DetachmentCost_TheAllocationScan_SeesAnAllocation()
    {
        var control = typeof(CreatureSiegeHooksTests).GetMethod(nameof(Allocator), BindingFlags.NonPublic | BindingFlags.Static)!;

        CollectionAssert.AreEqual(new[] { OpCodes.Newobj }, AllocatingOpCodes(control).ToArray(), "the scan must flag a plain new object()");
    }

    // --- the gate damage ---------------------------------------------------------------------------------------------------

    [TestMethod]
    public void ScaleGateBlow_ACreatureMeleeBlow_IsMultiplied_AndLoggedOnce()
    {
        var scaled = CreatureSiegeHooks.ScaleGateBlow(Snapshot(), 15000f, false, false, false, 1, 180f);

        Assert.AreEqual(360f, scaled);
        _logger.Received(1).LogDebug(Arg.Is<string>(s => s.Contains("180.0") && s.Contains("360.0") && s.Contains("15000.0") && !s.Contains("14640.0")));
    }

    [DataTestMethod]
    [DataRow(true, false, false, 1)]
    [DataRow(false, true, false, 1)]
    [DataRow(false, false, true, 1)]
    [DataRow(false, false, false, 0)]
    [DataRow(false, false, false, -1)]
    [DataRow(false, false, false, 99)]
    public void ScaleGateBlow_AnyConditionFailing_LeavesTheBlowAlone_AndLogsNothing(bool friendly, bool mount, bool missile, int race)
    {
        var scaled = CreatureSiegeHooks.ScaleGateBlow(Snapshot(), 15000f, friendly, mount, missile, race, 180f);

        Assert.AreEqual(180f, scaled);
        _logger.DidNotReceive().LogDebug(Arg.Any<string>());
    }

    [TestMethod]
    public void ScaleGateBlow_ANaNOrNonPositiveDamage_IsHandedBack_AndLogsNothing()
    {
        Assert.IsTrue(float.IsNaN(CreatureSiegeHooks.ScaleGateBlow(Snapshot(), 15000f, false, false, false, 1, float.NaN)));
        Assert.AreEqual(0f, CreatureSiegeHooks.ScaleGateBlow(Snapshot(), 15000f, false, false, false, 1, 0f));
        Assert.AreEqual(float.PositiveInfinity, CreatureSiegeHooks.ScaleGateBlow(Snapshot(), 15000f, false, false, false, 1, float.PositiveInfinity));

        _logger.DidNotReceive().LogDebug(Arg.Any<string>());
    }

    [TestMethod]
    public void ScaleGateBlow_AMultiplierOfOne_ChangesNothing_AndLogsNothing()
    {
        Assert.AreEqual(180f, CreatureSiegeHooks.ScaleGateBlow(Snapshot(1f), 15000f, false, false, false, 1, 180f));

        _logger.DidNotReceive().LogDebug(Arg.Any<string>());
    }

    [TestMethod]
    public void ScaleGateBlow_TheBlowLines_AreThrottled_AndTheNextOneCountsWhatWasSwallowed()
    {
        var snapshot = Snapshot();

        for (var i = 0; i < 50; i++)
            CreatureSiegeHooks.ScaleGateBlow(snapshot, 15000f, false, false, false, 1, 100f);

        Assert.AreEqual(4, _logger.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IModLogger.LogDebug)), "the burst, then nothing");
    }

    [TestMethod]
    public void ScaleGateBlow_AGateWhoseHitPointsAreNotFinite_StillScales_AndNamesThem()
    {
        var scaled = CreatureSiegeHooks.ScaleGateBlow(Snapshot(), float.NaN, false, false, false, 1, 180f);

        Assert.AreEqual(360f, scaled);
        _logger.Received(1).LogDebug(Arg.Is<string>(s => s.Contains("NaN")));
    }

    [TestMethod]
    public void ScaleGateBlow_WithNoLogger_StillScales()
    {
        CreatureSiegeHooks.Logger = null;

        Assert.AreEqual(360f, CreatureSiegeHooks.ScaleGateBlow(Snapshot(), 15000f, false, false, false, 1, 180f));
    }

    [TestMethod]
    public void ScaleGateBlow_ALoggerThatThrows_NeverBreaksTheBlow()
    {
        _logger.When(l => l.LogDebug(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("log failed"));

        Assert.AreEqual(360f, CreatureSiegeHooks.ScaleGateBlow(Snapshot(), 15000f, false, false, false, 1, 180f));
    }

    [TestMethod]
    public void ScaleGateDamage_NoSnapshot_IsTheBlowUnchanged()
    {
        var attack = default(AttackInformation);
        var collision = default(AttackCollisionData);

        Assert.AreEqual(180f, CreatureSiegeHooks.ScaleGateDamage(in attack, in collision, 180f));
    }

    [TestMethod]
    public void ScaleGateDamage_ASnapshotButNoGateInTheHit_IsTheBlowUnchanged()
    {
        // An ordinary agent-to-agent blow: the hit object's destructible component is null.
        CreatureSiegeSnapshot.Publish(Snapshot());
        var attack = default(AttackInformation);
        var collision = default(AttackCollisionData);

        Assert.AreEqual(180f, CreatureSiegeHooks.ScaleGateDamage(in attack, in collision, 180f));
        _logger.DidNotReceive().LogDebug(Arg.Any<string>());
    }
}
