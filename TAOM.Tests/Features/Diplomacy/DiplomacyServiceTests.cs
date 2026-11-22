using System.Collections.Generic;
using System.Linq;
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

    [TestMethod]
    public void EnforcePermanentAlliances_AlliedButAlsoAtWar_MakesPeaceWithoutRestartingAlliance()
    {
        // Bug observed 2026-05-22 (Mordor↔Harad in-game): vanilla initial-state setup
        // declared war on a Permanent pair AFTER EstablishInitialAlliances created the
        // alliance, leaving them simultaneously allied AND at war. Earlier impl
        // short-circuited on AreAllied=true and never called MakePeace. Fix verifies
        // the two invariants (peace, alliance) are checked independently.
        // Uses empire_w↔vlandia which is the test fixture's Permanent pair.
        _allianceAdapter.AreAllied("empire_w", "vlandia").Returns(true);
        _allianceAdapter.AreAtWar("empire_w", "vlandia").Returns(true);

        _sut.EnforcePermanentAlliances();

        _allianceAdapter.Received(1).MakePeace("empire_w", "vlandia");
        _allianceAdapter.DidNotReceive().StartAlliance("empire_w", "vlandia");
    }

    [TestMethod]
    public void EnforcePermanentAlliances_NotAlliedAndAtWar_MakesPeaceThenStartsAlliance()
    {
        _allianceAdapter.AreAtWar("empire_w", "vlandia").Returns(true);
        _allianceAdapter.AreAllied("empire_w", "vlandia").Returns(false, true);

        _sut.EnforcePermanentAlliances();

        Received.InOrder(() =>
        {
            _allianceAdapter.MakePeace("empire_w", "vlandia");
            _allianceAdapter.StartAlliance("empire_w", "vlandia");
        });
    }

    // --- Player alliance freedom (issue: player kingdoms could not form alliances) ---
    // Full freedom: any check that involves the player's kingdom bypasses the lore
    // Hostile gate, so AI will offer to the player and the player can accept.

    [TestMethod]
    public void GetAllianceScoreModifier_InvolvesPlayer_OverridesHostileToClearThreshold()
    {
        // empire_w↔empire_s is Hostile in the fixture. Vanilla CanMakeAlliance needs the
        // total score >= 50f; the player branch must contribute enough to clear it and
        // never apply the -10000 Hostile penalty.
        var score = _sut.GetAllianceScoreModifier("empire_w", "empire_s", involvesPlayer: true);

        Assert.IsTrue(score >= 50f);
    }

    [TestMethod]
    public void GetAllianceScoreModifier_NotInvolvesPlayer_PreservesTierBehavior()
    {
        Assert.IsTrue(_sut.GetAllianceScoreModifier("empire_w", "empire_s", involvesPlayer: false) < -500f);
        Assert.AreEqual(0f, _sut.GetAllianceScoreModifier("empire_w", "battania", involvesPlayer: false));
    }

    [TestMethod]
    public void GetAllianceScoreModifier_TwoArgOverload_MatchesNonPlayerBehavior()
    {
        // Regression: the legacy 2-arg form must equal the involvesPlayer:false path.
        Assert.AreEqual(
            _sut.GetAllianceScoreModifier("empire_w", "empire_s", involvesPlayer: false),
            _sut.GetAllianceScoreModifier("empire_w", "empire_s"));
    }

    [TestMethod]
    public void IsAllianceDecisionAllowed_InvolvesPlayer_AllowsEvenHostile()
    {
        Assert.IsTrue(_sut.IsAllianceDecisionAllowed("empire_w", "empire_s", involvesPlayer: true));
    }

    [TestMethod]
    public void IsAllianceDecisionAllowed_NotInvolvesPlayer_PreservesHostileBlock()
    {
        Assert.IsFalse(_sut.IsAllianceDecisionAllowed("empire_w", "empire_s", involvesPlayer: false));
        Assert.IsTrue(_sut.IsAllianceDecisionAllowed("empire_w", "vlandia", involvesPlayer: false));
    }

    [TestMethod]
    public void CanPlayerProposeAlliance_SameKingdom_ReturnsFalse()
    {
        Assert.IsFalse(_sut.CanPlayerProposeAlliance("player_kingdom", "player_kingdom"));
    }

    [TestMethod]
    public void CanPlayerProposeAlliance_EmptyId_ReturnsFalse()
    {
        Assert.IsFalse(_sut.CanPlayerProposeAlliance("", "empire_w"));
        Assert.IsFalse(_sut.CanPlayerProposeAlliance("player_kingdom", null));
    }

    [TestMethod]
    public void CanPlayerProposeAlliance_AtWar_ReturnsFalse()
    {
        // Alliances require peace first (vanilla CanMakeAlliance rejects at-war pairs).
        _allianceAdapter.AreAtWar("player_kingdom", "empire_w").Returns(true);

        Assert.IsFalse(_sut.CanPlayerProposeAlliance("player_kingdom", "empire_w"));
    }

    [TestMethod]
    public void CanPlayerProposeAlliance_AlreadyAllied_ReturnsFalse()
    {
        _allianceAdapter.AreAllied("player_kingdom", "empire_w").Returns(true);

        Assert.IsFalse(_sut.CanPlayerProposeAlliance("player_kingdom", "empire_w"));
    }

    [TestMethod]
    public void CanPlayerProposeAlliance_PeaceAndNotAllied_ReturnsTrue()
    {
        // Defaults: AreAtWar=false, AreAllied=false.
        Assert.IsTrue(_sut.CanPlayerProposeAlliance("player_kingdom", "empire_w"));
    }

    [TestMethod]
    public void CanPlayerProposeAlliance_HostileTier_ReturnsTrue()
    {
        // Full freedom: lore Hostile (empire_w↔empire_s) does not block the player.
        Assert.IsTrue(_sut.CanPlayerProposeAlliance("empire_w", "empire_s"));
    }

    [TestMethod]
    public void FormPlayerAlliance_ValidPairAndEngineForms_CallsStartAllianceAndReturnsTrue()
    {
        // AreAllied: first call is the can-propose precondition (false → may propose);
        // second call is the post-StartAlliance confirmation (true → it formed).
        _allianceAdapter.AreAllied("player_kingdom", "empire_w").Returns(false, true);

        var result = _sut.FormPlayerAlliance("player_kingdom", "empire_w");

        _allianceAdapter.Received(1).StartAlliance("player_kingdom", "empire_w");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void FormPlayerAlliance_ValidPairButEngineNoOps_ReturnsFalse()
    {
        // AreAllied stays false even after StartAlliance — e.g. a missing/eliminated kingdom
        // made the adapter no-op. The method must report failure, not success.
        _allianceAdapter.AreAllied("player_kingdom", "empire_w").Returns(false);

        var result = _sut.FormPlayerAlliance("player_kingdom", "empire_w");

        _allianceAdapter.Received(1).StartAlliance("player_kingdom", "empire_w");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void FormPlayerAlliance_AlreadyAllied_DoesNotCallStartAllianceAndReturnsFalse()
    {
        _allianceAdapter.AreAllied("player_kingdom", "empire_w").Returns(true);

        var result = _sut.FormPlayerAlliance("player_kingdom", "empire_w");

        _allianceAdapter.DidNotReceive().StartAlliance("player_kingdom", "empire_w");
        Assert.IsFalse(result);
    }

    // #1283: Dunland went to war with Gondor in every run because an evil ally called it in. A refusal row has a
    // direction: the called realm refuses a call against the target, not the other way round.
    private DiplomacyService ServiceWithRefusal(string called, string target)
    {
        var provider = Substitute.For<IDiplomacyConfigProvider>();
        provider.LoadConfig().Returns(new DiplomacyConfig
        {
            CallToWarRefusals = new List<CallToWarRefusal> { new() { Called = called, Target = target } }
        });
        return new DiplomacyService(provider, _allianceAdapter, _alignmentService, _logger);
    }

    [TestMethod]
    public void RefusesCallToWar_ListedPair_ReturnsTrue()
    {
        Assert.IsTrue(ServiceWithRefusal("empire", "empire_w").RefusesCallToWar("empire", "empire_w"));
    }

    [TestMethod]
    public void RefusesCallToWar_ReverseDirection_ReturnsFalse()
    {
        Assert.IsFalse(ServiceWithRefusal("empire", "empire_w").RefusesCallToWar("empire_w", "empire"));
    }

    [TestMethod]
    public void RefusesCallToWar_UnlistedPair_ReturnsFalse()
    {
        Assert.IsFalse(_sut.RefusesCallToWar("empire", "empire_w"));
    }

    // #1105 slice 5: a permanent alliance the Hornburg's repulsed block ends. The pair is recorded first, so the EndAlliance prefix
    // (AllianceActionHook, which reads the tier) lets the engine call through.
    private void Allied(bool allied) => _allianceAdapter.AreAllied(Arg.Any<string>(), Arg.Any<string>()).Returns(allied);

    [TestMethod]
    public void EndPermanentAlliance_AlliedPermanentPair_EndsItAndReadsNeutralWhileTheEngineIsAsked()
    {
        Allied(true);
        AllianceTier? tierDuringCall = null;
        _allianceAdapter.When(a => a.EndAlliance("vlandia", "empire_w")).Do(_ =>
        {
            tierDuringCall = _sut.GetRelationshipTier("empire_w", "vlandia");
            Allied(false);
        });

        var result = _sut.EndPermanentAlliance("vlandia", "empire_w", "test");

        Assert.AreEqual(PermanentAllianceEnd.Ended, result);
        Assert.AreEqual(AllianceTier.Neutral, tierDuringCall, "the pair is recorded before the engine runs its EndAlliance prefix");
        Assert.IsTrue(_sut.IsBrokenAlliance("empire_w", "vlandia"));
        Assert.AreEqual(AllianceTier.Neutral, _sut.GetRelationshipTier("empire_w", "vlandia"));
    }

    [TestMethod]
    public void EndPermanentAlliance_ASecondCall_IsAlreadyEndedAndAsksTheEngineNoMore()
    {
        Allied(true);
        _allianceAdapter.When(a => a.EndAlliance(Arg.Any<string>(), Arg.Any<string>())).Do(_ => Allied(false));
        _sut.EndPermanentAlliance("empire_w", "vlandia", "test");
        _allianceAdapter.ClearReceivedCalls();

        Assert.AreEqual(PermanentAllianceEnd.AlreadyEnded, _sut.EndPermanentAlliance("empire_w", "vlandia", "test"));
        _allianceAdapter.DidNotReceive().EndAlliance(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void EndPermanentAlliance_APairWithNoPermanentRow_IsNotPermanentAndRecordsNothing()
    {
        var result = _sut.EndPermanentAlliance("erebor", "mirkwood", "test");

        Assert.AreEqual(PermanentAllianceEnd.NotPermanent, result);
        Assert.IsFalse(_sut.IsBrokenAlliance("erebor", "mirkwood"));
        _allianceAdapter.DidNotReceive().EndAlliance(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void EndPermanentAlliance_NotAllied_RecordsThePairWithoutAskingTheEngine()
    {
        Allied(false);

        Assert.AreEqual(PermanentAllianceEnd.Ended, _sut.EndPermanentAlliance("empire_w", "vlandia", "test"));
        Assert.IsTrue(_sut.IsBrokenAlliance("empire_w", "vlandia"));
        _allianceAdapter.DidNotReceive().EndAlliance(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void EndPermanentAlliance_TheEngineLeavesThemAllied_IsStillAllied()
    {
        Allied(true);

        Assert.AreEqual(PermanentAllianceEnd.StillAllied, _sut.EndPermanentAlliance("empire_w", "vlandia", "test"));
    }

    [TestMethod]
    public void BrokenPair_NeverBlocksTheRestOfTheTable()
    {
        Allied(false);
        _sut.EndPermanentAlliance("empire_w", "vlandia", "test");

        Assert.AreEqual(AllianceTier.Permanent, _sut.GetRelationshipTier("erebor", "sturgia"));
        Assert.AreEqual(AllianceTier.Hostile, _sut.GetRelationshipTier("empire_w", "empire_s"));
    }

    [TestMethod]
    public void BrokenPair_IsNotAllowedAnAllianceAndScoresNegative()
    {
        Allied(false);
        _sut.EndPermanentAlliance("empire_w", "vlandia", "test");

        Assert.IsFalse(_sut.IsAllianceAllowed("empire_w", "vlandia"));
        Assert.AreEqual(-10000f, _sut.GetAllianceScoreModifier("vlandia", "empire_w"));
    }

    [TestMethod]
    public void BrokenPair_StaysBroken_AlsoWhenAPlayerRulesOneOfTheTwo()
    {
        Allied(false);
        _sut.EndPermanentAlliance("empire_w", "vlandia", "test");

        Assert.AreEqual(-10000f, _sut.GetAllianceScoreModifier("vlandia", "empire_w", involvesPlayer: true), "the player's freedom does not undo a broken alliance");
        Assert.IsFalse(_sut.IsAllianceDecisionAllowed("empire_w", "vlandia", involvesPlayer: true));
        Assert.IsFalse(_sut.CanPlayerProposeAlliance("empire_w", "vlandia"));
        Assert.IsFalse(_sut.FormPlayerAlliance("vlandia", "empire_w"));
    }

    [TestMethod]
    public void BrokenPair_IsNotAllowedToGoToWar()
    {
        _alignmentService.AreSameAlignment("empire_w", "vlandia").Returns(false);
        Allied(false);
        _sut.EndPermanentAlliance("empire_w", "vlandia", "test");

        Assert.IsFalse(_sut.IsWarAllowed("empire_w", "vlandia"), "a broken pair keeps its war veto whatever its alignment");
    }

    [TestMethod]
    public void EnforcePermanentAlliances_SkipsABrokenPair()
    {
        Allied(false);
        _sut.EndPermanentAlliance("empire_w", "vlandia", "test");
        _allianceAdapter.ClearReceivedCalls();

        _sut.EnforcePermanentAlliances();

        _allianceAdapter.DidNotReceive().StartAlliance("empire_w", "vlandia");
        _allianceAdapter.DidNotReceive().MakePeace("empire_w", "vlandia");
        _allianceAdapter.Received().StartAlliance("erebor", "sturgia");
    }

    [TestMethod]
    public void RestoreBrokenAlliances_KeepsOnlyPermanentRowsAndRoundTrips()
    {
        _sut.RestoreBrokenAlliances(new[] { "vlandia|empire_w", "erebor|mirkwood", "garbage", "", null! });

        Assert.IsTrue(_sut.IsBrokenAlliance("empire_w", "vlandia"));
        Assert.IsFalse(_sut.IsBrokenAlliance("erebor", "mirkwood"), "a pair with no Permanent row is dropped");
        CollectionAssert.AreEqual(new[] { "empire_w|vlandia" }, _sut.BrokenAlliancesForSave().ToArray());
    }

    [TestMethod]
    public void RestoreBrokenAlliances_Null_LeavesTheSetEmpty()
    {
        _sut.RestoreBrokenAlliances(null);

        Assert.AreEqual(0, _sut.BrokenAlliancesForSave().Count);
    }

    [TestMethod]
    public void ResetBrokenAlliances_EmptiesTheSetAndTheTierReturns()
    {
        _sut.RestoreBrokenAlliances(new[] { "empire_w|vlandia" });

        _sut.ResetBrokenAlliances();

        Assert.IsFalse(_sut.IsBrokenAlliance("empire_w", "vlandia"));
        Assert.AreEqual(AllianceTier.Permanent, _sut.GetRelationshipTier("empire_w", "vlandia"));
    }
}
