using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.FordsOfWar;

/// <summary>The <c>tactics</c> block of a faction in <c>factions.json</c>, and the tactics the module ships.</summary>
[TestClass]
public class FordsTacticsConfigTests
{
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup() => _logger = Substitute.For<IModLogger>();

    private FactionCatalog WithTactics(string tactics)
    {
        var text = "{ 'factions': { 'gondor': { 'name': 'Gondor', 'culture': 'gondor', 'units': [{ 'id': 'm', 'troop': 'a', 'role': 'Infantry', 'packSize': 5, 'price': 50 }],"
            + " 'heroes': [], 'freeWaves': [{ 'fromMinute': 0, 'units': [{ 'troop': 'a', 'count': 1 }] }], 'tactics': " + tactics + " } } }";
        Assert.IsTrue(FordsConfigCatalog.FromTexts("{}", text, FordsConfigFixtures.Maps, _logger).TryGetFaction("gondor", out var gondor));
        return gondor;
    }

    private void AssertOneWarningNaming(params string[] parts) =>
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("factions.json") && parts.All(p => m.Contains(p))));

    [TestMethod]
    public void Tactics_AreReadWithTheirArrangementsAndRanges()
    {
        var gondor = WithTactics("{ 'march': 'Line', 'close': 'ShieldWall', 'closeRange': 40, 'chargeRange': 8 }");

        Assert.AreEqual(new LaneTactic(LaneArrangement.Line, LaneArrangement.ShieldWall, 40f, 8f), gondor.Tactic);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Tactics_LeaveOutWhatTheyDoNotName()
    {
        Assert.AreEqual(new LaneTactic(ChargeRange: 35f), WithTactics("{ 'chargeRange': 35 }").Tactic);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Tactics_WithAnUnknownArrangement_AreDroppedWithOneWarning()
    {
        Assert.IsNull(WithTactics("{ 'close': 'Phalanx' }").Tactic);
        AssertOneWarningNaming("gondor", "tactics", "Phalanx");
    }

    /// <summary>Codex, 2026-11-07: Enum.TryParse read "2" as ShieldWall and "Loose, ShieldWall" as 1 | 2, which is Square.</summary>
    [TestMethod]
    public void Tactics_WithANumberOrAListForAnArrangement_AreDroppedWithOneWarning()
    {
        Assert.IsNull(WithTactics("{ 'close': '2' }").Tactic);
        AssertOneWarningNaming("gondor", "tactics");
        _logger.ClearReceivedCalls();

        Assert.IsNull(WithTactics("{ 'close': 'Loose, ShieldWall' }").Tactic);
        AssertOneWarningNaming("gondor", "tactics");
    }

    [TestMethod]
    public void Tactics_WithARangeOutOfBounds_AreDroppedWithOneWarning()
    {
        Assert.IsNull(WithTactics("{ 'chargeRange': 0 }").Tactic);
        AssertOneWarningNaming("gondor", "tactics", "chargeRange");
    }

    [TestMethod]
    public void Tactics_ThatAreNotAnObject_AreDroppedWithOneWarning()
    {
        Assert.IsNull(WithTactics("'wall'").Tactic);
        AssertOneWarningNaming("gondor", "tactics");
    }

    [TestMethod]
    public void NoTactics_IsNull()
    {
        Assert.IsTrue(FordsConfigCatalog.FromTexts("{}", "{ 'factions': { 'gondor': { 'name': 'Gondor', 'culture': 'gondor', 'units': [{ 'id': 'm', 'troop': 'a', 'role': 'Infantry', 'packSize': 5, 'price': 50 }],"
            + " 'heroes': [], 'freeWaves': [{ 'fromMinute': 0, 'units': [{ 'troop': 'a', 'count': 1 }] }] } } }", FordsConfigFixtures.Maps, _logger)
            .TryGetFaction("gondor", out var gondor));
        Assert.IsNull(gondor.Tactic);
    }

    /// <summary>
    /// Mike, 2026-11-07: Isengard and the dwarves fight in formation and attack at close quarters. The wall forms only inside 20 m:
    /// a shield wall's behaviour set gives ranged a weight of 0 at every distance, and inside 20 m the engine's normal move set
    /// already prefers melee (HumanAIComponent, DefensiveArrangementMove against DefaultMove). Lens 2, 2026-11-07: DefaultMove's
    /// ranged weight is only 0.02 to 0.04 at every distance, so the arrows are not the reason; the reason is that a wall walks at
    /// about 0.3 of its units' top speed, so a longer approach crawls under fire.
    /// </summary>
    [TestMethod]
    public void Shipped_IsengardAndErebor_CloseInAShieldWall_AndChargeOnlyAtCloseQuarters()
    {
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(RepoPaths.RepoPath("Main", "_Module", "ModuleData"));
        var catalog = new FordsConfigCatalog(paths, _logger);
        var engage = catalog.Match.Lanes.EngageRange;

        foreach (var id in new[] { "isengard", "erebor" })
        {
            Assert.IsTrue(catalog.TryGetFaction(id, out var faction), id);
            var tactic = faction.Tactic;
            Assert.IsNotNull(tactic, id);
            Assert.AreEqual(LaneArrangement.ShieldWall, tactic!.Close, id);
            Assert.IsTrue(tactic.ChargeAt(engage) < engage, id + ": charges later than an untrained lane");
            Assert.IsTrue(tactic.CloseAt(engage) > tactic.ChargeAt(engage), id + ": forms the wall before it charges");
        }

        foreach (var faction in catalog.Factions.Where(f => f.Tactic?.Close is LaneArrangement.ShieldWall or LaneArrangement.Square))
            Assert.IsTrue(faction.Tactic!.CloseAt(engage) <= 20f, faction.FactionId + ": a wall walks at 0.3 of top speed, so it forms only for the last 20 m");
    }
}
