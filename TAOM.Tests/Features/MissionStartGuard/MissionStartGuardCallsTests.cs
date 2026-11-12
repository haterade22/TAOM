using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.MountAndBlade;
using TAOM.Features.MissionStartGuard;
using TAOM.Features.MissionStartGuard.Hooks;
using TAOM.Features.MissionStartGuard.Models;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.MissionStartGuard;

/// <summary>
/// The six static helpers Patch103 swaps in for the engine's start calls. Each one is "the engine's call inside a
/// try, with an exception filter that asks the service": a throwing subject is caught and reported when the guard
/// says survive, a normal call runs exactly once, and a guard that says no leaves the exception untouched (the
/// filter does not unwind, so the original stack stays). The subjects are real engine subclasses, so the class
/// needs the game assemblies.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class MissionStartGuardCallsTests
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
        _service.ShouldSurvive(Arg.Any<Exception>()).Returns(true);
        MissionStartGuardCalls.Initialize(_service);
    }

    [TestCleanup]
    public void Cleanup()
    {
        MissionStartGuardCalls.Initialize(null);
        MissionBehaviorDeferral.Reset();
        RestoreMission();
    }

    private static readonly InvalidOperationException Thrown = new InvalidOperationException("boom");

    private sealed class Behavior : MissionLogic
    {
        public int Calls;
        public bool Throw;
        public Exception? ToThrow;
        public Action? Mutate;
        public override void OnBehaviorInitialize() { Calls++; Mutate?.Invoke(); if (Throw) throw ToThrow ?? Thrown; }
        public override void EarlyStart() { Calls++; Mutate?.Invoke(); if (Throw) throw ToThrow ?? Thrown; }
        public override void AfterStart() { Calls++; Mutate?.Invoke(); if (Throw) throw ToThrow ?? Thrown; }
    }

    private sealed class SubModule : MBSubModuleBase
    {
        public int Calls;
        public bool Throw;
        public Action? During;
        public override void OnBeforeMissionBehaviorInitialize(Mission mission) { Calls++; During?.Invoke(); if (Throw) throw Thrown; }
        public override void OnMissionBehaviorInitialize(Mission mission) { Calls++; During?.Invoke(); if (Throw) throw Thrown; }
    }

    private static void RunBehavior(StartCall call, Behavior b)
    {
        switch (call)
        {
            case StartCall.OnBehaviorInitialize: MissionStartGuardCalls.OnBehaviorInitialize(b); break;
            case StartCall.EarlyStart: MissionStartGuardCalls.EarlyStart(b); break;
            default: MissionStartGuardCalls.AfterStart(b); break;
        }
    }

    private static void RunSubModule(StartCall call, SubModule s)
    {
        if (call == StartCall.OnBeforeMissionBehaviorInitialize) MissionStartGuardCalls.OnBeforeMissionBehaviorInitialize(s, null!);
        else MissionStartGuardCalls.OnMissionBehaviorInitialize(s, null!);
    }

    private static readonly StartCall[] BehaviorCalls =
        { StartCall.OnBehaviorInitialize, StartCall.EarlyStart, StartCall.AfterStart };

    private static readonly StartCall[] SubModuleCalls =
        { StartCall.OnBeforeMissionBehaviorInitialize, StartCall.OnMissionBehaviorInitialize };

    [TestMethod]
    public void BehaviorHelpers_ANormalCall_RunsTheEngineCallOnceAndReportsNothing()
    {
        foreach (var call in BehaviorCalls)
        {
            var b = new Behavior();

            RunBehavior(call, b);

            Assert.AreEqual(1, b.Calls, call.ToString());
        }
        _service.DidNotReceiveWithAnyArgs().Report(default, null!, null!, null!);
    }

    [TestMethod]
    public void BehaviorHelpers_AThrowingCall_IsCaughtAndReportedWithTheCallTypeAndAssembly()
    {
        foreach (var call in BehaviorCalls)
        {
            _service.ClearReceivedCalls();
            var b = new Behavior { Throw = true };

            RunBehavior(call, b);

            Assert.AreEqual(1, b.Calls, call.ToString());
            _service.Received(1).Report(call, typeof(Behavior).FullName!, typeof(Behavior).Assembly.GetName().Name!, Thrown);
        }
    }

    [TestMethod]
    public void SubModuleHelpers_ANormalCall_RunsTheEngineCallOnceAndReportsNothing()
    {
        foreach (var call in SubModuleCalls)
        {
            var s = new SubModule();

            RunSubModule(call, s);

            Assert.AreEqual(1, s.Calls, call.ToString());
        }
        _service.DidNotReceiveWithAnyArgs().Report(default, null!, null!, null!);
    }

    [TestMethod]
    public void SubModuleHelpers_AThrowingCall_IsCaughtAndReported()
    {
        foreach (var call in SubModuleCalls)
        {
            _service.ClearReceivedCalls();
            var s = new SubModule { Throw = true };

            RunSubModule(call, s);

            _service.Received(1).Report(call, typeof(SubModule).FullName!, typeof(SubModule).Assembly.GetName().Name!, Thrown);
        }
    }

    [TestMethod]
    public void Helpers_ANullOwner_TheNullReferenceIsReportedOnceAsNull()
    {
        // A null behavior or module another mod added to a list: the engine call throws a NullReferenceException.
        var calls = new (StartCall Call, Action Run)[]
        {
            (StartCall.OnBeforeMissionBehaviorInitialize, () => MissionStartGuardCalls.OnBeforeMissionBehaviorInitialize(null!, null!)),
            (StartCall.OnBehaviorInitialize, () => MissionStartGuardCalls.OnBehaviorInitialize(null!)),
            (StartCall.OnMissionBehaviorInitialize, () => MissionStartGuardCalls.OnMissionBehaviorInitialize(null!, null!)),
            (StartCall.EarlyStart, () => MissionStartGuardCalls.EarlyStart(null!)),
            (StartCall.AfterStart, () => MissionStartGuardCalls.AfterStart(null!)),
            (StartCall.AfterMissionStart, () => MissionStartGuardCalls.AfterMissionStart(null!)),
        };
        foreach (var (call, run) in calls)
        {
            _service.ClearReceivedCalls();

            run();

            _service.Received(1).Report(call, "<null>", "unknown", Arg.Is<Exception>(e => e is NullReferenceException));
        }
    }

    // MissionObject cannot be allocated in a test host (ScriptComponentBehavior's static constructor needs the native
    // engine), so the sixth helper has no behavioural subject. All six are pinned in the IL instead: the engine call
    // sits in a try, and the only handler is an exception filter.
    [TestMethod]
    public void EachHelper_CallsItsEngineMethodInsideATryWhoseOnlyHandlerIsAnExceptionFilter()
    {
        var swaps = MissionStartGuardSwaps.Build();
        Assert.AreEqual(6, swaps.Count);

        foreach (var swap in swaps)
        {
            var helper = swap.Helpers.Single();
            var body = helper.GetMethodBody()!;
            var clauses = body.ExceptionHandlingClauses;

            Assert.AreEqual(1, clauses.Count, helper.Name);
            Assert.AreEqual(System.Reflection.ExceptionHandlingClauseOptions.Filter, clauses[0].Flags, helper.Name + ": a catch-when filter, so a refusal does not unwind");
            var calls = IlCallScanner.ExtractCalledMethods(helper, body.GetILAsByteArray()!).ToList();
            Assert.IsTrue(calls.Any(c => c == swap.Target), helper.Name + " no longer calls " + swap.Target.DeclaringType!.Name + "." + swap.Target.Name);
        }
    }

    [TestMethod]
    public void Helpers_TheGuardSaysNo_RethrowTheSameExceptionObjectWithItsOriginalStackAndReportNothing()
    {
        _service.ShouldSurvive(Arg.Any<Exception>()).Returns(false);
        var b = new Behavior { Throw = true };

        var caught = Assert.ThrowsException<InvalidOperationException>(() => MissionStartGuardCalls.EarlyStart(b));

        Assert.AreSame(Thrown, caught);
        StringAssert.Contains(caught.StackTrace, nameof(Behavior.EarlyStart), "the filter did not unwind: the thrower's frame is still in the trace");
        _service.DidNotReceiveWithAnyArgs().Report(default, null!, null!, null!);
    }

    [TestMethod]
    public void Helpers_NoServiceInitialized_RethrowTheException()
    {
        MissionStartGuardCalls.Initialize(null);
        var s = new SubModule { Throw = true };

        var caught = Assert.ThrowsException<InvalidOperationException>(() => MissionStartGuardCalls.OnBeforeMissionBehaviorInitialize(s, null!));

        Assert.AreSame(Thrown, caught);
    }

    [TestMethod]
    public void Helpers_TheServiceThrowsInTheFilter_RethrowTheEnginesException()
    {
        _service.ShouldSurvive(Arg.Any<Exception>()).Returns(_ => throw new InvalidOperationException("settings broke"));
        var b = new Behavior { Throw = true };

        var caught = Assert.ThrowsException<InvalidOperationException>(() => MissionStartGuardCalls.AfterStart(b));

        Assert.AreSame(Thrown, caught);
    }

    [TestMethod]
    public void Helpers_TheReportThrows_StillSurvive()
    {
        _service.When(s => s.Report(Arg.Any<StartCall>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Exception>()))
            .Do(_ => throw new InvalidOperationException("log closed"));
        var b = new Behavior { Throw = true };

        MissionStartGuardCalls.AfterStart(b);

        Assert.AreEqual(1, b.Calls);
    }

    [TestMethod]
    public void Helpers_WithTheRealService_OutOfMemoryIsNeverSwallowed()
    {
        var settings = Substitute.For<IMissionStartGuardSettingsProvider>();
        settings.SurviveMissionStartFailures.Returns(true);
        var real = new MissionStartGuardService(
            settings,
            Substitute.For<TAOM.Adapters.IMissionStartGuardAdapter>(),
            Substitute.For<TAOM.Features.CoopInterop.IDedicatedServerProvider>(),
            Substitute.For<TAOM.Core.Logging.IModLogger>());
        MissionStartGuardCalls.Initialize(real);
        var oom = new OutOfMemoryException();
        var behavior = new Behavior { Throw = true, ToThrow = oom };

        var caught = Assert.ThrowsException<OutOfMemoryException>(() => MissionStartGuardCalls.AfterStart(behavior));

        Assert.AreSame(oom, caught);
    }

    // ---- the list-size check (#1028) ----
    // A Mission cannot be built in a test host, so these tests allocate one without its constructor and give it the two lists
    // the helpers read. The static field and the two list fields are engine internals: a rename makes the tests Inconclusive.

    private static FieldInfo? _currentField;
    private object? _previousMission;
    private bool _installed;

    private Mission InstallMission(int behaviors)
    {
        _currentField = typeof(Mission).GetField("_current", BindingFlags.NonPublic | BindingFlags.Static);
        var behaviorsField = typeof(Mission).GetField("<MissionBehaviors>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
        var objectsField = typeof(Mission).GetField("_missionObjects", BindingFlags.NonPublic | BindingFlags.Instance);
        if (_currentField == null || behaviorsField == null || objectsField == null)
            Assert.Inconclusive("Mission's internals moved: _current, <MissionBehaviors>k__BackingField or _missionObjects");

        Mission mission;
        try { mission = (Mission)FormatterServices.GetUninitializedObject(typeof(Mission)); }
        catch (Exception ex) { Assert.Inconclusive("Mission cannot be allocated in this host: " + ex.GetType().Name); return null!; }

        var list = new List<MissionBehavior>();
        for (var i = 0; i < behaviors; i++) list.Add(new Behavior());
        behaviorsField!.SetValue(mission, list);
        objectsField!.SetValue(mission, new TaleWorlds.Library.MBList<MissionObject>());
        SetCurrent(mission);
        return mission;
    }

    // The previous value is saved on the first call of a test only: a second install must not make the first fake the "previous" one.
    private void SetCurrent(Mission? mission)
    {
        _currentField ??= typeof(Mission).GetField("_current", BindingFlags.NonPublic | BindingFlags.Static);
        if (_currentField == null) Assert.Inconclusive("Mission._current moved");
        if (!_installed)
        {
            _previousMission = _currentField!.GetValue(null);
            _installed = true;
        }
        _currentField!.SetValue(null, mission);
    }

    private void RestoreMission()
    {
        if (_installed) _currentField!.SetValue(null, _previousMission);
        _installed = false;
        _previousMission = null;
    }

    [TestMethod]
    public void BehaviorHelpers_ACallThatAddsABehavior_ReportsTheChangeWithBothSizesAndTheNewestItem()
    {
        foreach (var call in new[] { StartCall.EarlyStart, StartCall.AfterStart })
        {
            _service.ClearReceivedCalls();
            var mission = InstallMission(2);
            var added = new AddedBehavior();
            var b = new Behavior { Mutate = () => mission.MissionBehaviors.Add(added) };

            RunBehavior(call, b);

            _service.Received(1).ReportListChange(call, typeof(Behavior).FullName!, typeof(Behavior).Assembly.GetName().Name!,
                "MissionBehaviors", 2, 3, typeof(AddedBehavior).FullName!);
        }
    }

    [TestMethod]
    public void BehaviorHelpers_WhileTheCallRuns_AnAddIsDeferredThroughTheGateAndTheListIsLeftAlone()
    {
        _service.ShouldDeferBehaviorAdds().Returns(true);
        foreach (var call in new[] { StartCall.EarlyStart, StartCall.AfterStart })
        {
            _service.ClearReceivedCalls();
            var mission = InstallMission(2);
            var deferred = false;
            var b = new Behavior
            {
                Mutate = () =>
                {
                    var added = new AddedBehavior();
                    deferred = MissionStartGuardCalls.TryDeferAdd(mission, added);
                    if (!deferred) mission.MissionBehaviors.Add(added);   // what the engine's add would have done
                },
            };

            RunBehavior(call, b);

            Assert.IsTrue(deferred, $"{call}: the window was closed during the call");
            Assert.AreEqual(2, mission.MissionBehaviors.Count);
            Assert.AreEqual(1, MissionBehaviorDeferral.Pending);
            _service.DidNotReceiveWithAnyArgs().ReportListChange(default, default!, default!, default!, default, default, default!);
            Assert.IsFalse(MissionStartGuardCalls.TryDeferAdd(mission, new AddedBehavior()), $"{call}: the window stayed open after the call");
            MissionBehaviorDeferral.Reset();
        }
    }

    [TestMethod]
    public void BehaviorHelpers_ToggleOff_AnAddReachesTheListAndIsReported()
    {
        _service.ShouldDeferBehaviorAdds().Returns(false);
        var mission = InstallMission(2);
        var deferred = true;
        var b = new Behavior
        {
            Mutate = () =>
            {
                var added = new AddedBehavior();
                deferred = MissionStartGuardCalls.TryDeferAdd(mission, added);
                if (!deferred) mission.MissionBehaviors.Add(added);
            },
        };

        RunBehavior(StartCall.AfterStart, b);

        Assert.IsFalse(deferred);
        Assert.AreEqual(3, mission.MissionBehaviors.Count);
        _service.Received(1).ReportListChange(StartCall.AfterStart, typeof(Behavior).FullName!, typeof(Behavior).Assembly.GetName().Name!,
            "MissionBehaviors", 2, 3, typeof(AddedBehavior).FullName!);
    }

    private Mission InstallMissionWithAddLists()
    {
        var mission = InstallMission(2);
        typeof(Mission).GetField("<MissionLogics>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(mission, new List<MissionLogic>());
        typeof(Mission).GetField("_otherMissionBehaviors", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(mission, new List<MissionBehavior>());
        return mission;
    }

    [TestMethod]
    public void AfterStartFinalizer_AfterAThrow_StillAppliesTheQueuedAddsAndClosesTheWindow()
    {
        // The engine loads the mission again after an unsurvived throw. A behaviour left in the queue would be lost for
        // good: DualWield's IsActive is already true, so it would never add its behaviour again.
        var mission = InstallMissionWithAddLists();
        var added = new AddedBehavior();
        MissionBehaviorDeferral.Open(mission, StartCall.AfterStart, new Behavior());
        MissionBehaviorDeferral.TryDefer(mission, added);

        Mission_AfterStart_MissionStartGuard_Patch.Finalizer(new InvalidOperationException("boom"), true);

        Assert.AreSame(added, mission.MissionBehaviors[2]);
        Assert.IsNull(MissionBehaviorDeferral.WindowMission);
        Assert.AreEqual(0, MissionBehaviorDeferral.Pending);
        _service.Received(1).ReportEscaped(Arg.Any<Exception>());
    }

    [TestMethod]
    public void SubModuleHelpers_NeverOpenTheDeferralWindow()
    {
        // Every AddTaomBehavior add and DualWield's own OnMissionBehaviorInitialize add happen here: queueing them would
        // hand them no start calls.
        foreach (var call in SubModuleCalls)
        {
            var mission = InstallMission(1);
            var open = true;
            var s = new SubModule { During = () => open = MissionBehaviorDeferral.IsOpenFor(mission) };

            RunSubModule(call, s);

            Assert.AreEqual(1, s.Calls, $"{call}: the call did not run");
            Assert.IsFalse(open, $"{call}: the window was open during the call");
        }
    }

    [TestMethod]
    public void AfterStartFinalizer_AppliesTheQueuedAddsAfterTheWalk_LikeAddMissionBehavior()
    {
        var mission = InstallMissionWithAddLists();
        var added = new AddedBehavior();
        MissionBehaviorDeferral.Open(mission, StartCall.AfterStart, new Behavior());
        MissionBehaviorDeferral.TryDefer(mission, added);

        Mission_AfterStart_MissionStartGuard_Patch.Finalizer(null, true);

        Assert.AreEqual(3, mission.MissionBehaviors.Count);
        Assert.AreSame(added, mission.MissionBehaviors[2]);
        CollectionAssert.Contains(mission.MissionLogics, added);
        Assert.AreSame(mission, added.Mission);
        Assert.AreEqual(0, MissionBehaviorDeferral.Pending);
    }

    [TestMethod]
    public void AfterStartPrefix_DropsAQueueLeftByAnEarlierStart()
    {
        MissionBehaviorDeferral.Open(new object(), StartCall.AfterStart, new Behavior());
        MissionBehaviorDeferral.TryDefer(MissionBehaviorDeferral.WindowMission!, new AddedBehavior());

        Mission_AfterStart_MissionStartGuard_Patch.Prefix(new object(), out _);

        Assert.AreEqual(0, MissionBehaviorDeferral.Pending);
    }

    [TestMethod]
    public void OnBehaviorInitializeHelper_NeverOpensTheDeferralWindow()
    {
        var mission = InstallMission(2);
        var deferred = false;
        var b = new Behavior { Mutate = () => deferred = MissionBehaviorDeferral.TryDefer(mission, new AddedBehavior()) };

        RunBehavior(StartCall.OnBehaviorInitialize, b);

        Assert.IsFalse(deferred);
    }

    [TestMethod]
    public void BehaviorHelpers_ACallThatRemovesABehavior_ReportsTheSmallerSize()
    {
        var mission = InstallMission(2);
        var b = new Behavior { Mutate = () => mission.MissionBehaviors.RemoveAt(0) };

        MissionStartGuardCalls.AfterStart(b);

        _service.Received(1).ReportListChange(StartCall.AfterStart, Arg.Any<string>(), Arg.Any<string>(),
            "MissionBehaviors", 2, 1, Arg.Any<string>());
    }

    [TestMethod]
    public void BehaviorHelpers_ACallThatOverwritesAnItem_ReportsItAlthoughTheSizeIsTheSame()
    {
        // The engine's enumerator throws on any version change, not only on a size change.
        foreach (var call in new[] { StartCall.EarlyStart, StartCall.AfterStart })
        {
            _service.ClearReceivedCalls();
            var mission = InstallMission(2);
            var b = new Behavior { Mutate = () => mission.MissionBehaviors[0] = new AddedBehavior() };

            RunBehavior(call, b);

            _service.Received(1).ReportListChange(call, typeof(Behavior).FullName!, typeof(Behavior).Assembly.GetName().Name!,
                "MissionBehaviors", 2, 2, "n/a");
        }
    }

    [TestMethod]
    public void BehaviorHelpers_ACallThatAddsAndRemovesAnItem_ReportsItAlthoughTheSizeIsTheSame()
    {
        var mission = InstallMission(2);
        var b = new Behavior { Mutate = () => { mission.MissionBehaviors.RemoveAt(0); mission.MissionBehaviors.Add(new AddedBehavior()); } };

        MissionStartGuardCalls.AfterStart(b);

        _service.Received(1).ReportListChange(StartCall.AfterStart, Arg.Any<string>(), Arg.Any<string>(), "MissionBehaviors", 2, 2, "n/a");
    }

    [TestMethod]
    public void Watches_ReadTheMissionsOwnLists()
    {
        var mission = InstallMission(1);

        Assert.AreSame(mission.MissionBehaviors, MissionStartGuardCalls.WatchBehaviors().Items);
        Assert.AreSame(mission.MissionObjects, MissionStartGuardCalls.WatchObjects().Items);
    }

    [TestMethod]
    public void NoteListChange_TheObjectsList_IsReportedAsMissionObjects()
    {
        // AfterMissionStart has no behavioural subject (a MissionObject cannot be allocated in a test host), so its mapping is pinned here.
        var mission = InstallMission(0);
        var watch = MissionStartGuardCalls.WatchObjects();
        ((List<MissionObject>)mission.MissionObjects).Add(null!);

        MissionStartGuardCalls.NoteListChange(StartCall.AfterMissionStart, new object(), watch);

        _service.Received(1).ReportListChange(StartCall.AfterMissionStart, typeof(object).FullName!, typeof(object).Assembly.GetName().Name!,
            "MissionObjects", 0, 1, "n/a");
    }

    [TestMethod]
    public void BehaviorHelpers_ACallThatChangesNothing_ReportsNoListChange()
    {
        InstallMission(2);
        var b = new Behavior();

        MissionStartGuardCalls.EarlyStart(b);
        MissionStartGuardCalls.AfterStart(b);

        _service.DidNotReceiveWithAnyArgs().ReportListChange(default, null!, null!, null!, 0, 0, null!);
    }

    [TestMethod]
    public void BehaviorHelpers_ACallOnAnEmptyUnchangedList_ReportsNoListChange()
    {
        // An empty list takes the enumerator's MoveNextRare path with equal versions: it must not throw.
        InstallMission(0);
        var b = new Behavior();

        MissionStartGuardCalls.EarlyStart(b);
        MissionStartGuardCalls.AfterStart(b);

        _service.DidNotReceiveWithAnyArgs().ReportListChange(default, null!, null!, null!, 0, 0, null!);
    }

    [TestMethod]
    public void BehaviorHelpers_NoCurrentMission_ReportsNoListChangeAndRunsTheCall()
    {
        SetCurrent(null);
        var b = new Behavior();

        MissionStartGuardCalls.AfterStart(b);

        Assert.AreEqual(1, b.Calls);
        _service.DidNotReceiveWithAnyArgs().ReportListChange(default, null!, null!, null!, 0, 0, null!);
    }

    [TestMethod]
    public void BehaviorHelpers_TheListChangeReportThrows_StillSurvive()
    {
        var mission = InstallMission(0);
        _service.When(s => s.ReportListChange(Arg.Any<StartCall>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>()))
            .Do(_ => throw new InvalidOperationException("log closed"));
        var b = new Behavior { Mutate = () => mission.MissionBehaviors.Add(new Behavior()) };

        MissionStartGuardCalls.AfterStart(b);

        Assert.AreEqual(1, b.Calls);
    }

    private sealed class AddedBehavior : MissionLogic { }
}
