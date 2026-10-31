using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Core.Logging;

/// <summary>
/// Issue #1071: the facade and the summary do nothing unless the seams call them, and these seams are engine
/// lifecycle methods no unit test runs. Each test pins one call in the source, with comments stripped so a
/// commented-out call cannot satisfy it.
/// </summary>
[TestClass]
public class OutcomeSeamsTests
{
    [TestMethod]
    public void SubModule_InitialisesTheFacade_AfterConfiguringTheContainer_AndHooksProcessExit()
    {
        var source = ReadSource("Main/SubModule.cs", stripComments: true);

        var configure = IndexOf(source, "IoC.Configure();");
        var initialise = IndexOf(source, "Outcomes.Initialize(IoC.Resolve<Core.Logging.IOutcomeLog>())");
        Assert.IsTrue(initialise > configure, "Outcomes.Initialize resolves from the container, so it has to follow IoC.Configure().");
        IndexOf(source, "Outcomes.SummariseOnProcessExit()");
    }

    [TestMethod]
    public void SubModule_TicksTheHeartbeat_AndWritesTheExitSummaryBeforeTheShutdownLine()
    {
        var source = ReadSource("Main/SubModule.cs", stripComments: true);

        var tickStart = IndexOf(source, "protected override void OnApplicationTick(float dt)");
        var tick = IndexOf(source, "Outcomes.Tick();");
        Assert.IsTrue(tick > tickStart && tick < source.IndexOf("protected override", tickStart + 1, StringComparison.Ordinal),
            "the heartbeat is ticked from OnApplicationTick");

        var unload = IndexOf(source, "protected override void OnSubModuleUnloaded()");
        var exit = IndexOf(source, "Outcomes.WriteSummary(\"exit\")");
        var shutdown = IndexOf(source, "[Shutdown] clean module unload");
        Assert.IsTrue(unload < exit && exit < shutdown, "the exit summary is written in OnSubModuleUnloaded, before [Shutdown]");
    }

    [TestMethod]
    public void TheSavePrefix_WritesASaveSummary()
    {
        var source = ReadSource("Main/Features/SaveLoadDiagnostics/Hooks/SaveManager_Save_Patch.cs", stripComments: true);

        var start = IndexOf(source, "public static void Prefix(");
        var end = source.IndexOf("\n    }", start, StringComparison.Ordinal);
        Assert.IsTrue(end > start, "the end of the Prefix body was not found");
        var call = IndexOf(source, "Outcomes.WriteSummary(\"save\")");
        Assert.IsTrue(call > start && call < end, "the save summary is written inside Prefix, not in another method");
    }

    [TestMethod]
    public void TheCrashHandler_WritesACrashSummary_ForAnAdmittedCrash_BeforeTheLogTailIsCollected()
    {
        var source = ReadSource("Main/Features/CrashReport/CrashReportService.cs", stripComments: true);

        var admitted = IndexOf(source, "admission.Decision != CrashBundleDecision.WriteBundle");
        var summary = IndexOf(source, "_outcomes.WriteSummary(\"crash\")");
        var compose = IndexOf(source, "var context = ComposeContext(");
        var tail = IndexOf(source, "_logTail.Collect()");
        Assert.IsTrue(admitted < summary && summary < compose && compose < tail,
            "the summary follows the throttle (a crash every frame would write one per frame) and precedes the log tail, so report.txt's TAOM tail carries it");
    }

    [TestMethod]
    public void TheContainer_RegistersTheOutcomeLogOnce()
    {
        var source = ReadSource("Main/IoC.cs", stripComments: true);

        IndexOf(source, "container.Register<IOutcomeLog, OutcomeLog>(Reuse.Singleton);");
    }

    [TestMethod]
    public void ScreenFaultLog_ReportsEveryThrowToOutcomes()
    {
        var source = ReadSource("Main/Core/Logging/ScreenFaultLog.cs", stripComments: true);

        IndexOf(source, "Outcomes.Swallowed(_feature, site, ex);");
    }

    private static int IndexOf(string source, string text)
    {
        var at = source.IndexOf(text, StringComparison.Ordinal);
        Assert.IsTrue(at >= 0, $"'{text}' is missing from the source.");
        return at;
    }
}
