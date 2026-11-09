using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.MapEventGuard;
using TAOM.Tests.Features.LoadTimeStamps;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.MapEventGuard;

/// <summary>#1099: when a sally-out check must stop, read on plain values.</summary>
[TestClass]
public class SallyOutGuardServiceTests
{
    private IModLogger _logger = null!;
    private IOutcomeLog _outcomes = null!;
    private SallyOutGuardService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _outcomes = Substitute.For<IOutcomeLog>();
        _service = new SallyOutGuardService(_outcomes, _logger);
    }

    private static ISallySiegeAdapter Siege(
        bool leaderInPlayerBattle = true,
        bool joinsLiveBattle = false,
        (int Number, bool MarkedToDie)[]? roster = null,
        string settlementId = "town_A",
        string leaderId = "party_B")
    {
        var siege = Substitute.For<ISallySiegeAdapter>();
        siege.Read().Returns(new SallySiegeSnapshot(
            leaderInPlayerBattle, joinsLiveBattle, settlementId, leaderId, roster ?? Array.Empty<(int, bool)>()));
        return siege;
    }

    [TestMethod]
    public void ShouldStopSally_EmptyLeaderInPlayerBattle_ReturnsTrue() =>
        Assert.IsTrue(_service.ShouldStopSally(Siege()));

    [TestMethod]
    public void ShouldStopSally_MannedLeaderInPlayerBattle_ReturnsFalse() =>
        // Vanilla's Finish on this path is what closes the player's battle, so a healthy party must keep it.
        Assert.IsFalse(_service.ShouldStopSally(Siege(roster: new[] { (12, false) })));

    [TestMethod]
    public void ShouldStopSally_EmptyLeaderOutsidePlayerBattle_ReturnsFalse() =>
        Assert.IsFalse(_service.ShouldStopSally(Siege(leaderInPlayerBattle: false)));

    [TestMethod]
    public void ShouldStopSally_JoiningLiveBattleWithEmptyLeader_ReturnsFalse() =>
        // This vanilla branch only adds parties to a running battle and never opens an encounter.
        Assert.IsFalse(_service.ShouldStopSally(Siege(joinsLiveBattle: true)));

    [TestMethod]
    public void ShouldStopSally_NoSnapshot_ReturnsFalse()
    {
        var siege = Substitute.For<ISallySiegeAdapter>();
        siege.Read().Returns((SallySiegeSnapshot?)null);

        Assert.IsFalse(_service.ShouldStopSally(siege));
    }

    [TestMethod]
    public void ShouldStopSally_OnlyDepletedEntry_ReturnsTrue() =>
        Assert.IsTrue(_service.ShouldStopSally(Siege(roster: new[] { (0, false) })));

    [TestMethod]
    public void ShouldStopSally_OnlyHeroMarkedToDie_ReturnsTrue() =>
        Assert.IsTrue(_service.ShouldStopSally(Siege(roster: new[] { (1, true) })));

    [TestMethod]
    public void ShouldStopSally_MarkedHeroAndATroop_ReturnsFalse() =>
        Assert.IsFalse(_service.ShouldStopSally(Siege(roster: new[] { (1, true), (12, false) })));

    [TestMethod]
    public void ShouldStopSally_UnmarkedHero_ReturnsFalse() =>
        Assert.IsFalse(_service.ShouldStopSally(Siege(roster: new[] { (1, false) })));

    [TestMethod]
    public void ShouldStopSally_NotStopped_ReportsNothing()
    {
        _service.ShouldStopSally(Siege(roster: new[] { (12, false) }));

        _outcomes.DidNotReceive().Pass(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void ShouldStopSally_RepeatedStopOfOneParty_WarnsOnce()
    {
        _service.ShouldStopSally(Siege());
        _service.ShouldStopSally(Siege());
        _logger.Received(1).LogWarning(Arg.Any<string>());

        _service.ShouldStopSally(Siege(settlementId: "town_C", leaderId: "party_D"));
        _logger.Received(2).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void ShouldStopSally_Stopped_WritesTheCheckLineTheManifestReads()
    {
        var logger = Substitute.For<IModLogger>();
        var service = new SallyOutGuardService(new OutcomeLog(logger, new FakeStampClock(), () => 0), logger);

        service.ShouldStopSally(Siege());

        string line = logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IModLogger.LogInfo))
            .Select(c => (string)c.GetArguments()[0]!)
            .Single(l => l.StartsWith("[Check] ", StringComparison.Ordinal));
        var manifest = RepoPaths.ReadSource("tools/ingame_checks/1099.toml");
        string pattern = Regex.Match(manifest, @"^pass = \['(.+)'\]", RegexOptions.Multiline).Groups[1].Value;
        Assert.AreNotEqual(string.Empty, pattern, "1099.toml has no pass pattern.");
        StringAssert.Matches(line, new Regex(pattern), "the manifest would report this check as not reached.");
    }
}
