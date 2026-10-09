using System;
using System.Linq;
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
    public void Cleanup() => MissionStartGuardCalls.Initialize(null);

    private static readonly InvalidOperationException Thrown = new InvalidOperationException("boom");

    private sealed class Behavior : MissionLogic
    {
        public int Calls;
        public bool Throw;
        public Exception? ToThrow;
        public override void OnBehaviorInitialize() { Calls++; if (Throw) throw ToThrow ?? Thrown; }
        public override void EarlyStart() { Calls++; if (Throw) throw ToThrow ?? Thrown; }
        public override void AfterStart() { Calls++; if (Throw) throw ToThrow ?? Thrown; }
    }

    private sealed class SubModule : MBSubModuleBase
    {
        public int Calls;
        public bool Throw;
        public override void OnBeforeMissionBehaviorInitialize(Mission mission) { Calls++; if (Throw) throw Thrown; }
        public override void OnMissionBehaviorInitialize(Mission mission) { Calls++; if (Throw) throw Thrown; }
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
}
