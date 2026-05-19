using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.Execution;

namespace TAOM.Tests.Features.Diplomacy;

[TestClass]
public class DiplomacyServiceTests
{
    private IDiplomacyConfigProvider _configProvider;
    private IAllianceAdapter _allianceAdapter;
    private IAlignmentService _alignmentService;
    private IModLogger _logger;
    private DiplomacyService _sut;

    [TestInitialize]
    public void Setup()
    {
        _configProvider = Substitute.For<IDiplomacyConfigProvider>();
        _allianceAdapter = Substitute.For<IAllianceAdapter>();
        _alignmentService = Substitute.For<IAlignmentService>();
        _logger = Substitute.For<IModLogger>();

        var config = new DiplomacyConfig
        {
            Relationships = new List<KingdomRelationship>
            {
                new() { KingdomA = "empire_w", KingdomB = "vlandia", Tier = AllianceTier.Permanent },
                new() { KingdomA = "erebor", KingdomB = "sturgia", Tier = AllianceTier.Permanent },
                new() { KingdomA = "rivendell", KingdomB = "lothlorien", Tier = AllianceTier.Permanent },
                new() { KingdomA = "erebor", KingdomB = "mirkwood", Tier = AllianceTier.Natural },
                new() { KingdomA = "empire_w", KingdomB = "empire_s", Tier = AllianceTier.Hostile },
                new() { KingdomA = "umbar", KingdomB = "empire_w", Tier = AllianceTier.Hostile },
            }
        };
        _configProvider.LoadConfig().Returns(config);

        _sut = new DiplomacyService(_configProvider, _allianceAdapter, _alignmentService, _logger);
    }

    [TestMethod]
    public void GetRelationshipTier_PermanentAllies_ReturnsPermanent()
    {
        var tier = _sut.GetRelationshipTier("empire_w", "vlandia");

        Assert.AreEqual(AllianceTier.Permanent, tier);
    }

    [TestMethod]
    public void GetRelationshipTier_HostileKingdoms_ReturnsHostile()
    {
        var tier = _sut.GetRelationshipTier("empire_w", "empire_s");

        Assert.AreEqual(AllianceTier.Hostile, tier);
    }

    [TestMethod]
    public void GetRelationshipTier_UnknownPair_ReturnsNeutral()
    {
        var tier = _sut.GetRelationshipTier("empire_w", "battania");

        Assert.AreEqual(AllianceTier.Neutral, tier);
    }

    [TestMethod]
    public void GetRelationshipTier_IsSymmetric()
    {
        var tierAB = _sut.GetRelationshipTier("empire_w", "vlandia");
        var tierBA = _sut.GetRelationshipTier("vlandia", "empire_w");

        Assert.AreEqual(tierAB, tierBA);
    }

    [TestMethod]
    public void GetAllianceScoreModifier_Permanent_ReturnsHighPositive()
    {
        var score = _sut.GetAllianceScoreModifier("empire_w", "vlandia");

        Assert.IsTrue(score > 500f);
    }

    [TestMethod]
    public void GetAllianceScoreModifier_Hostile_ReturnsHighNegative()
    {
        var score = _sut.GetAllianceScoreModifier("empire_w", "empire_s");

        Assert.IsTrue(score < -500f);
    }

    [TestMethod]
    public void GetAllianceScoreModifier_Neutral_ReturnsZero()
    {
        var score = _sut.GetAllianceScoreModifier("empire_w", "battania");

        Assert.AreEqual(0f, score);
    }

    [TestMethod]
    public void GetAllianceScoreModifier_Natural_ReturnsModeratePositive()
    {
        var score = _sut.GetAllianceScoreModifier("erebor", "mirkwood");

        Assert.IsTrue(score > 0f);
        Assert.IsTrue(score < 1000f);
    }

    [TestMethod]
    public void IsAllianceAllowed_Hostile_ReturnsFalse()
    {
        var allowed = _sut.IsAllianceAllowed("empire_w", "empire_s");

        Assert.IsFalse(allowed);
    }

    [TestMethod]
    public void IsAllianceAllowed_NonHostile_ReturnsTrue()
    {
        Assert.IsTrue(_sut.IsAllianceAllowed("empire_w", "vlandia"));
        Assert.IsTrue(_sut.IsAllianceAllowed("erebor", "mirkwood"));
        Assert.IsTrue(_sut.IsAllianceAllowed("empire_w", "battania"));
    }

    [TestMethod]
    public void EstablishInitialAlliances_CreatesAllPermanentAlliances()
    {
        _allianceAdapter.AreAllied(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        _sut.EstablishInitialAlliances();

        _allianceAdapter.Received(1).StartAlliance("empire_w", "vlandia");
        _allianceAdapter.Received(1).StartAlliance("erebor", "sturgia");
        _allianceAdapter.Received(1).StartAlliance("rivendell", "lothlorien");
        _allianceAdapter.DidNotReceive().StartAlliance("erebor", "mirkwood");
    }

    [TestMethod]
    public void IsWarAllowed_PermanentTier_ReturnsFalse()
    {
        // Permanent tier blocks war regardless of alignment-service answer.
        _alignmentService.AreSameAlignment(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        Assert.IsFalse(_sut.IsWarAllowed("empire_w", "vlandia"));
    }

    [TestMethod]
    public void IsWarAllowed_SameAlignmentFree_ReturnsFalse()
    {
        // The bug case: mirkwood vs sturgia (Dale) — no tier in config, same "free" alignment.
        _alignmentService.AreSameAlignment("mirkwood", "sturgia").Returns(true);

        Assert.IsFalse(_sut.IsWarAllowed("mirkwood", "sturgia"));
    }

    [TestMethod]
    public void IsWarAllowed_SameAlignmentEvil_ReturnsFalse()
    {
        // Evil-vs-evil: both gates compose (Permanent OR same alignment); covers OR semantics.
        _alignmentService.AreSameAlignment("isengard", "dolguldur").Returns(true);

        Assert.IsFalse(_sut.IsWarAllowed("isengard", "dolguldur"));
    }

    [TestMethod]
    public void IsWarAllowed_DifferentAlignment_ReturnsTrue()
    {
        // Gondor vs Mordor — Hostile tier, different alignment. War must be allowed.
        _alignmentService.AreSameAlignment("empire_w", "empire_s").Returns(false);

        Assert.IsTrue(_sut.IsWarAllowed("empire_w", "empire_s"));
    }

    [TestMethod]
    public void IsWarAllowed_NeutralAlignment_ReturnsTrue()
    {
        // Neutrals (Umbar, Khand-equivalent) can war each other. AreSameAlignment returns false for neutrals.
        _alignmentService.AreSameAlignment("umbar", "shaghana").Returns(false);

        Assert.IsTrue(_sut.IsWarAllowed("umbar", "shaghana"));
    }

    [TestMethod]
    public void IsWarAllowed_OneNeutralOneFree_ReturnsTrue()
    {
        // Mixed neutral-vs-free: neutrals do not gain alignment protection.
        _alignmentService.AreSameAlignment("umbar", "empire_w").Returns(false);

        Assert.IsTrue(_sut.IsWarAllowed("umbar", "empire_w"));
    }

    [TestMethod]
    public void IsWarAllowed_UnknownIds_ReturnsTrue()
    {
        // Vanilla third-party kingdoms not in alignment.json fall through to allowed.
        _alignmentService.AreSameAlignment("unknown_a", "unknown_b").Returns(false);

        Assert.IsTrue(_sut.IsWarAllowed("unknown_a", "unknown_b"));
    }

    [TestMethod]
    public void EnforcePermanentAlliances_SkipsAlreadyAllied()
    {
        _allianceAdapter.AreAllied("empire_w", "vlandia").Returns(true);
        _allianceAdapter.AreAllied("erebor", "sturgia").Returns(false);
        _allianceAdapter.AreAllied("rivendell", "lothlorien").Returns(true);

        _sut.EnforcePermanentAlliances();

        _allianceAdapter.DidNotReceive().StartAlliance("empire_w", "vlandia");
        _allianceAdapter.Received(1).StartAlliance("erebor", "sturgia");
        _allianceAdapter.DidNotReceive().StartAlliance("rivendell", "lothlorien");
    }

}
