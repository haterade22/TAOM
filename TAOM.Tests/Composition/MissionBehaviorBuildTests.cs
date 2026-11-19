using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Composition;

namespace TAOM.Tests.Composition;

/// <summary>
/// #785: the isolation SubModule.AddTaomBehavior uses for each hand-wired mission behavior. A throw while building
/// one is logged with the behavior's type name, reported to the mission-start guard with the type's full name and
/// assembly, and yields null, so the caller adds nothing and goes on to the next.
/// </summary>
[TestClass]
public class MissionBehaviorBuildTests
{
    private sealed class Probe
    {
    }

    private readonly List<string> _log = new();
    private readonly List<(string OwnerType, string OwnerAssembly, Exception Thrown)> _reports = new();

    private void Report(string ownerType, string ownerAssembly, Exception thrown) =>
        _reports.Add((ownerType, ownerAssembly, thrown));

    [TestMethod]
    public void Isolated_FactoryReturnsAnInstance_ReturnsItAndLogsAndReportsNothing()
    {
        var probe = new Probe();

        var built = MissionBehaviorBuild.Isolated(() => probe, _log.Add, Report);

        Assert.AreSame(probe, built);
        Assert.AreEqual(0, _log.Count);
        Assert.AreEqual(0, _reports.Count);
    }

    [TestMethod]
    public void Isolated_FactoryThrows_ReturnsNullLogsOnceAndReportsTheTypeAndTheException()
    {
        var thrown = new InvalidOperationException("no IDeadMountDespawnService registered");

        var built = MissionBehaviorBuild.Isolated<Probe>(() => throw thrown, _log.Add, Report);

        Assert.IsNull(built);
        Assert.AreEqual(1, _log.Count);
        StringAssert.Contains(_log[0], "[MissionWiring] Probe was not added");
        StringAssert.Contains(_log[0], "no IDeadMountDespawnService registered");
        Assert.AreEqual(1, _reports.Count);
        Assert.AreEqual(typeof(Probe).FullName, _reports[0].OwnerType);
        Assert.AreEqual(typeof(Probe).Assembly.GetName().Name, _reports[0].OwnerAssembly);
        Assert.AreSame(thrown, _reports[0].Thrown);
    }

    [TestMethod]
    public void Isolated_FactoryReturnsNull_ReturnsNullAndLogsAndReportsNothing()
    {
        var built = MissionBehaviorBuild.Isolated<Probe>(() => null, _log.Add, Report);

        Assert.IsNull(built);
        Assert.AreEqual(0, _log.Count);
        Assert.AreEqual(0, _reports.Count);
    }

    [TestMethod]
    public void Isolated_LogThrows_StillReportsToTheGuard()
    {
        var built = MissionBehaviorBuild.Isolated<Probe>(
            () => throw new InvalidOperationException("build"), _ => throw new InvalidOperationException("log"), Report);

        Assert.IsNull(built);
        Assert.AreEqual(1, _reports.Count);
    }

    [TestMethod]
    public void Isolated_FactoryLogAndReportAllThrow_ReturnsNullWithoutThrowing()
    {
        var built = MissionBehaviorBuild.Isolated<Probe>(
            () => throw new InvalidOperationException("build"),
            _ => throw new InvalidOperationException("log"),
            (_, _, _) => throw new InvalidOperationException("report"));

        Assert.IsNull(built);
    }
}
