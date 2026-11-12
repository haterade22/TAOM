using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.SiegeRetreatLoot;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SiegeRetreatLoot;

/// <summary>#1121: a defender whose assault ends by attacker retreat still receives the gear of the attackers who fell.</summary>
[TestClass]
public class SiegeRetreatLootServiceTests
{
    private ISiegeRetreatLootAdapter _loot = null!;
    private ICoopSessionProvider _coop = null!;
    private IOutcomeLog _outcomes = null!;
    private IModLogger _logger = null!;
    private SiegeRetreatLootService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _loot = Substitute.For<ISiegeRetreatLootAdapter>();
        _loot.CanLootCasualties().Returns(true);
        _loot.RunCasualtyLoot().Returns(true);
        _loot.OpenLootScreen().Returns(4);
        _coop = Substitute.For<ICoopSessionProvider>();
        _coop.IsCoopClient.Returns(false);
        _outcomes = Substitute.For<IOutcomeLog>();
        _logger = Substitute.For<IModLogger>();
        _service = new SiegeRetreatLootService(_loot, _coop, _outcomes, _logger);
    }

    [TestMethod]
    public void CollectLoot_EnemyDidNotRetreat_ReturnsFalseAndReadsNothing()
    {
        Assert.IsFalse(_service.CollectLoot(enemyRetreated: false));

        _loot.DidNotReceive().CanLootCasualties();
        _loot.DidNotReceive().RunCasualtyLoot();
    }

    [TestMethod]
    public void CollectLoot_NotADefenderVictoryInASiege_ReturnsFalseAndLootsNothing()
    {
        _loot.CanLootCasualties().Returns(false);

        Assert.IsFalse(_service.CollectLoot(enemyRetreated: true));

        _loot.DidNotReceive().RunCasualtyLoot();
        _outcomes.DidNotReceive().Skip(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void CollectLoot_SinglePlayerOrHost_RunsTheCasualtyLootAndReturnsTrue()
    {
        Assert.IsTrue(_service.CollectLoot(enemyRetreated: true));

        _loot.Received(1).RunCasualtyLoot();
    }

    [TestMethod]
    public void CollectLoot_CoopClient_SkipsWithoutRunningTheLoot()
    {
        _coop.IsCoopClient.Returns(true);

        Assert.IsFalse(_service.CollectLoot(enemyRetreated: true));

        _loot.DidNotReceive().RunCasualtyLoot();
        _outcomes.Received(1).Skip(SiegeRetreatLootService.CheckId, "", "reason=coop-client");
    }

    [TestMethod]
    public void CollectLoot_StepMissingOnThisEngine_ReportsTheFailureAndReturnsFalse()
    {
        _loot.RunCasualtyLoot().Returns(false);

        Assert.IsFalse(_service.CollectLoot(enemyRetreated: true));

        _outcomes.Received(1).Fail(SiegeRetreatLootService.CheckId, "", "reason=loot-step-missing");
        var manifest = RepoPaths.ReadSource("tools/ingame_checks/1121.toml");
        string pattern = Regex.Match(manifest, @"^fail = \['(.+)'\]", RegexOptions.Multiline).Groups[1].Value;
        Assert.AreNotEqual(string.Empty, pattern, "1121.toml has no fail pattern.");
        var failPattern = new Regex(pattern);
        _logger.Received(1).LogError(Arg.Is<string>(m => failPattern.IsMatch(m)));
    }

    [TestMethod]
    public void OpenLootScreen_ItemsShown_ReportsThePassWithTheCount()
    {
        _service.OpenLootScreen();

        _outcomes.Received(1).Pass(SiegeRetreatLootService.CheckId, "", "items=4");
    }

    [TestMethod]
    public void OpenLootScreen_NothingPending_ReportsASkipAndNoPass()
    {
        _loot.OpenLootScreen().Returns(0);

        _service.OpenLootScreen();

        _outcomes.Received(1).Skip(SiegeRetreatLootService.CheckId, "", "reason=nothing-to-loot");
        _outcomes.DidNotReceive().Pass(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void OpenLootScreen_ScreenCouldNotOpen_ReportsTheFailureAndNoPass()
    {
        _loot.OpenLootScreen().Returns(-1);

        _service.OpenLootScreen();

        _outcomes.Received(1).Fail(SiegeRetreatLootService.CheckId, "", "reason=screen-failed");
        _outcomes.DidNotReceive().Pass(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void Manifest_PassPatternMatchesThePassLine_AndFailPatternMatchesEveryTaggedErrorText()
    {
        var manifest = RepoPaths.ReadSource("tools/ingame_checks/1121.toml");
        var pass = new Regex(Regex.Match(manifest, @"^pass = \['(.+)'\]", RegexOptions.Multiline).Groups[1].Value);
        var fail = new Regex(Regex.Match(manifest, @"^fail = \['(.+)'\]", RegexOptions.Multiline).Groups[1].Value);

        Assert.IsTrue(pass.IsMatch("[Check] id=taom.siege-retreat-loot.collected result=pass items=4"));
        Assert.IsFalse(fail.IsMatch("[Check] id=taom.siege-retreat-loot.collected result=pass items=4"));

        // Every error text the feature writes starts with the tag, so the one pattern catches them all.
        foreach (var file in new[]
        {
            "Main/Adapters/SiegeRetreatLootAdapter.cs",
            "Main/Features/SiegeRetreatLoot/SiegeRetreatLootService.cs",
            "Main/Features/SiegeRetreatLoot/Hooks/Patch146_SiegeRetreatLoot.cs",
        })
        {
            foreach (Match m in Regex.Matches(RepoPaths.ReadSource(file), "LogError\\(\\$?\"([^\"]+)\""))
                Assert.IsTrue(fail.IsMatch(m.Groups[1].Value), file + ": error text not caught by the manifest: " + m.Groups[1].Value);
        }
    }
}
