using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;
using TAOM.Features.HiddenHosts;

namespace TAOM.Tests.Features.HiddenHosts;

/// <summary>
/// #846: a hook that faults shows vanilla's text for that frame. A fault that repeats every frame would cost a throw per
/// plate and flood the log, so one line per hook and exception type is logged and the 32nd fault stops the hooks for the
/// rest of the process. No game type is touched here, so none of this needs the engine.
/// </summary>
[TestClass]
public class HiddenHostsFaultBudgetTests
{
    private IModLogger _logger = null!;
    private SelfDisableLedger _savedLedger = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        HiddenHostsCalls.Initialize(null, null, _logger);
        _savedLedger = SelfDisables.Ledger;
        SelfDisables.Ledger = new SelfDisableLedger();
    }

    [TestCleanup]
    public void Cleanup()
    {
        HiddenHostsCalls.Initialize(null, null);
        SelfDisables.Ledger = _savedLedger;
    }

    [TestMethod]
    public void ReportFault_LogsOncePerHookAndExceptionType()
    {
        HiddenHostsCalls.ReportFault("hook", new InvalidOperationException("a"));
        HiddenHostsCalls.ReportFault("hook", new InvalidOperationException("b"));
        HiddenHostsCalls.ReportFault("hook", new ArgumentException("c"));
        HiddenHostsCalls.ReportFault("other", new ArgumentException("d"));

        _logger.Received(3).LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void ReportFault_WithinTheBudget_LetsTheHooksGoOn()
    {
        for (int i = 0; i < HiddenHostsCalls.FaultBudget - 1; i++)
        {
            HiddenHostsCalls.ReportFault("hook", new InvalidOperationException());
            Assert.IsFalse(HiddenHostsCalls.Stopped, "fault " + (i + 1));
        }

        Assert.IsFalse(HiddenHostsCalls.Stopped);
    }

    [TestMethod]
    public void ReportFault_PastTheBudget_TellsTheHooksToStop()
    {
        for (int i = 0; i < HiddenHostsCalls.FaultBudget - 1; i++)
            HiddenHostsCalls.ReportFault("hook", new InvalidOperationException());

        HiddenHostsCalls.ReportFault("hook", new InvalidOperationException());
        Assert.IsTrue(HiddenHostsCalls.Stopped, "the fault that spends the budget");
        HiddenHostsCalls.ReportFault("hook", new InvalidOperationException());
        Assert.IsTrue(HiddenHostsCalls.Stopped, "and every one after");
    }

    [TestMethod]
    public void ReportFault_TheBudgetIsReached_LogsOneFromNowOnLine()
    {
        for (int i = 0; i < HiddenHostsCalls.FaultBudget + 5; i++)
            HiddenHostsCalls.ReportFault("hook", new InvalidOperationException());

        // One line for the first fault of the hook, one when the budget is reached; nothing per fault after that.
        _logger.Received(2).LogError(Arg.Any<string>());
        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("from now on")));
    }

    [TestMethod]
    public void ReportFault_WithinTheBudget_ReportsNoSelfDisable()
    {
        for (int i = 0; i < HiddenHostsCalls.FaultBudget - 1; i++)
            HiddenHostsCalls.ReportFault("hook", new InvalidOperationException());

        Assert.IsFalse(SelfDisables.Ledger.HasPending);
    }

    [TestMethod]
    public void ReportFault_TheBudgetIsReached_ReportsTheSelfDisableOnce()
    {
        // #1185: the hooks are off from here, so the player is told. The faults after the budget add nothing.
        for (int i = 0; i < HiddenHostsCalls.FaultBudget + 5; i++)
            HiddenHostsCalls.ReportFault("hook", new InvalidOperationException());

        var taken = SelfDisables.Ledger.Take();
        Assert.AreEqual(1, taken.Count);
        Assert.AreEqual("Patch105_HiddenHosts", taken[0].Id);
        Assert.AreEqual("32 hook faults", taken[0].Reason);
    }

    [TestMethod]
    public void ReportFault_TheBudgetIsReachedAndTheLoggerThrows_StillReportsTheSelfDisable()
    {
        _logger.When(l => l.LogError(Arg.Any<string>())).Throw(new InvalidOperationException("log down"));

        for (int i = 0; i < HiddenHostsCalls.FaultBudget; i++)
            HiddenHostsCalls.ReportFault("hook", new InvalidOperationException());

        Assert.AreEqual("Patch105_HiddenHosts", SelfDisables.Ledger.Take().Single().Id);
    }

    [TestMethod]
    public void Initialize_ResetsTheBudgetAndTheReportedSet()
    {
        for (int i = 0; i < HiddenHostsCalls.FaultBudget; i++)
            HiddenHostsCalls.ReportFault("hook", new InvalidOperationException());
        Assert.IsTrue(HiddenHostsCalls.Stopped);

        HiddenHostsCalls.Initialize(null, null, _logger);

        Assert.IsFalse(HiddenHostsCalls.Stopped);
        _logger.ClearReceivedCalls();
        HiddenHostsCalls.ReportFault("hook", new InvalidOperationException());
        _logger.Received(1).LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void ReportFault_TheLoggerThrows_IsSwallowed()
    {
        _logger.When(l => l.LogError(Arg.Any<string>())).Throw(new InvalidOperationException("log down"));

        HiddenHostsCalls.ReportFault("hook", new InvalidOperationException("a"));

        Assert.IsFalse(HiddenHostsCalls.Stopped);
    }

    [TestMethod]
    public void ReportFault_NoLogger_StillCounts()
    {
        HiddenHostsCalls.Initialize(null, null);

        for (int i = 0; i < HiddenHostsCalls.FaultBudget; i++)
            HiddenHostsCalls.ReportFault("hook", new InvalidOperationException());

        Assert.IsTrue(HiddenHostsCalls.Stopped);
    }
}

/// <summary>
/// #1185: a hook whose engine field does not bind does nothing for the rest of the process, yet it counts one fault,
/// so the fault budget alone never reports it. Each bind miss reports Patch105 itself, once.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class HiddenHostsBindMissSelfDisableTests
{
    private SelfDisableLedger _savedLedger = null!;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => TAOM.Tests.Migration.GameAssemblies.EnsureLoaded();

    [TestInitialize]
    public void Setup()
    {
        HiddenHostsCalls.Initialize(null, null, Substitute.For<IModLogger>());
        _savedLedger = SelfDisables.Ledger;
        SelfDisables.Ledger = new SelfDisableLedger();
    }

    [TestCleanup]
    public void Cleanup()
    {
        HiddenHostsCalls.Initialize(null, null);
        SelfDisables.Ledger = _savedLedger;
    }

    [DataTestMethod]
    [DataRow(typeof(TAOM.Features.HiddenHosts.Hooks.PartyNameplateVM_RefreshBinding_HiddenHosts_Patch))]
    [DataRow(typeof(TAOM.Features.HiddenHosts.Hooks.PropertyBasedTooltipVM_OnPeriodicRefresh_HiddenHosts_Patch))]
    public void ReportMissingField_ReportsPatch105Once(Type hook)
    {
        var flag = hook.GetField("_bindReported", BindingFlags.NonPublic | BindingFlags.Static)!;
        var report = hook.GetMethod("ReportMissingField", BindingFlags.NonPublic | BindingFlags.Static)!;
        var saved = flag.GetValue(null);
        flag.SetValue(null, false);
        try
        {
            report.Invoke(null, null);
            report.Invoke(null, null);

            var taken = SelfDisables.Ledger.Take();
            Assert.AreEqual(1, taken.Count);
            Assert.AreEqual(HiddenHostsModule.PatchCategory, taken[0].Id);
            StringAssert.Contains(taken[0].Reason, "lacks");
            Assert.IsFalse(HiddenHostsCalls.Stopped, "one fault does not spend the budget: the report is the bind miss's own");
        }
        finally
        {
            flag.SetValue(null, saved);
        }
    }
}
