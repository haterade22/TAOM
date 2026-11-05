using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Features.MissionStartGuard;
using TAOM.Features.MissionStartGuard.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.MissionStartGuard;

/// <summary>
/// Patch103's prefix and finalizer: thin forwarders that never throw into <c>Mission.AfterStart</c>. The class mentions
/// <c>Mission</c> in its attribute, so it needs the game assemblies; both methods are called directly with a plain object.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class MissionStartGuardHookTests
{
    private static bool _gameLoaded;
    private IMissionStartGuardService _service = null!;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestInitialize]
    public void Setup()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        _service = Substitute.For<IMissionStartGuardService>();
        MissionStartGuardCalls.Initialize(_service);
        MissionStartGuardSwaps.LastSwapped = 0;
    }

    [TestCleanup]
    public void Cleanup()
    {
        MissionStartGuardCalls.Initialize(null);
        MissionStartGuardSwaps.LastSwapped = 0;
    }

    [TestMethod]
    public void Prefix_PassesItsInstanceToBeginMission()
    {
        var mission = new object();

        Mission_AfterStart_MissionStartGuard_Patch.Prefix(mission, out _);

        _service.Received(1).BeginMission(Arg.Is<object?>(o => ReferenceEquals(o, mission)));
    }

    [TestMethod]
    public void Prefix_SetsTheStateTheFinalizerReadsAsPrefixRan()
    {
        Mission_AfterStart_MissionStartGuard_Patch.Prefix(new object(), out var state);

        Assert.IsTrue(state);
    }

    [TestMethod]
    public void Prefix_NoServiceOrAThrowingService_StillSetsTheState()
    {
        MissionStartGuardCalls.Initialize(null);
        Mission_AfterStart_MissionStartGuard_Patch.Prefix(new object(), out var withoutService);
        MissionStartGuardCalls.Initialize(_service);
        _service.When(s => s.BeginMission(Arg.Any<object?>())).Do(_ => throw new InvalidOperationException("boom"));

        Mission_AfterStart_MissionStartGuard_Patch.Prefix(new object(), out var withThrowingService);

        Assert.IsTrue(withoutService);
        Assert.IsTrue(withThrowingService);
    }

    [TestMethod]
    public void Prefix_NoService_DoesNothing()
    {
        MissionStartGuardCalls.Initialize(null);

        Mission_AfterStart_MissionStartGuard_Patch.Prefix(new object(), out _);
    }

    [TestMethod]
    public void Prefix_TheServiceThrows_DoesNotThrowIntoTheEngine()
    {
        _service.When(s => s.BeginMission(Arg.Any<object?>())).Do(_ => throw new InvalidOperationException("boom"));

        Mission_AfterStart_MissionStartGuard_Patch.Prefix(new object(), out _);
    }

    [TestMethod]
    public void Finalizer_NoException_CallsOnlyEndMissionStart()
    {
        Mission_AfterStart_MissionStartGuard_Patch.Finalizer(null, true);

        _service.Received(1).EndMissionStart(Arg.Any<bool>(), Arg.Any<int>());
        _service.DidNotReceive().ReportEscaped(Arg.Any<Exception>());
    }

    [TestMethod]
    public void Finalizer_PassesTheTranspilersLastSwapCountToEndMissionStart()
    {
        MissionStartGuardSwaps.LastSwapped = 5;

        Mission_AfterStart_MissionStartGuard_Patch.Finalizer(null, true);

        _service.Received(1).EndMissionStart(true, 5);
    }

    [TestMethod]
    public void Finalizer_PassesThisCallsPrefixFlagToEndMissionStart()
    {
        Mission_AfterStart_MissionStartGuard_Patch.Finalizer(null, false);
        Mission_AfterStart_MissionStartGuard_Patch.Finalizer(null, true);

        Received.InOrder(() =>
        {
            _service.EndMissionStart(false, Arg.Any<int>());
            _service.EndMissionStart(true, Arg.Any<int>());
        });
    }

    [TestMethod]
    public void Finalizer_AnException_ReportsItThenEndsTheMissionStart()
    {
        var ex = new InvalidOperationException("escaped");

        Mission_AfterStart_MissionStartGuard_Patch.Finalizer(ex, true);

        Received.InOrder(() =>
        {
            _service.ReportEscaped(ex);
            _service.EndMissionStart(Arg.Any<bool>(), Arg.Any<int>());
        });
    }

    [TestMethod]
    public void Finalizer_NoService_DoesNothing()
    {
        MissionStartGuardCalls.Initialize(null);

        Mission_AfterStart_MissionStartGuard_Patch.Finalizer(new InvalidOperationException("escaped"), true);
    }

    [TestMethod]
    public void Finalizer_ReportEscapedThrows_DoesNotThrowIntoTheEngine()
    {
        _service.When(s => s.ReportEscaped(Arg.Any<Exception>())).Do(_ => throw new InvalidOperationException("boom"));

        Mission_AfterStart_MissionStartGuard_Patch.Finalizer(new InvalidOperationException("escaped"), true);
    }

    [TestMethod]
    public void Finalizer_EndMissionStartThrows_DoesNotThrowIntoTheEngine()
    {
        _service.When(s => s.EndMissionStart(Arg.Any<bool>(), Arg.Any<int>())).Do(_ => throw new InvalidOperationException("boom"));

        Mission_AfterStart_MissionStartGuard_Patch.Finalizer(null, true);
    }

    // ---- Cleanup: Harmony calls it after the apply, with the exception when the apply failed ----

    [TestMethod]
    public void Cleanup_TheApplyFailed_ZeroesTheSwapCountTheTranspilerLeftBehind()
    {
        MissionStartGuardSwaps.LastSwapped = MissionStartGuardService.ExpectedSites;

        Mission_AfterStart_MissionStartGuard_Patch.Cleanup(new InvalidOperationException("the apply failed"));

        Assert.AreEqual(0, MissionStartGuardSwaps.LastSwapped);
    }

    [TestMethod]
    public void Cleanup_TheApplySucceeded_LeavesTheSwapCount()
    {
        MissionStartGuardSwaps.LastSwapped = MissionStartGuardService.ExpectedSites;

        Mission_AfterStart_MissionStartGuard_Patch.Cleanup(null);

        Assert.AreEqual(MissionStartGuardService.ExpectedSites, MissionStartGuardSwaps.LastSwapped);
    }
}
