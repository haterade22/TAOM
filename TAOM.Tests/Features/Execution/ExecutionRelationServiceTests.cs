using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.Execution;

namespace TAOM.Tests.Features.Execution;

[TestClass]
public class ExecutionRelationServiceTests
{
    private const string PlayerKingdom = "empire_w";   // Gondor
    private const string AllyKingdom = "vlandia";      // Rohan
    private const string VictimKingdom = "empire_s";   // Mordor
    private const string VictimAllyKingdom = "isengard";
    private const string NeutralKingdom = "umbar";
    private const string FreeCulture = "gondor";
    private const string EvilCulture = "mordor";

    private IAlignmentService _alignmentService;
    private ExecutionRelationService _sut;

    [TestInitialize]
    public void Setup()
    {
        _alignmentService = Substitute.For<IAlignmentService>();

        // Side resolution mirrors the shipped alignment.json: by kingdom id, by culture id, and for
        // the kingdom-less combinations the fix has to survive.
        Side(PlayerKingdom, null, FactionSide.Free);
        Side(AllyKingdom, null, FactionSide.Free);
        Side("erebor", null, FactionSide.Free);
        Side(VictimKingdom, null, FactionSide.Evil);
        Side(VictimAllyKingdom, null, FactionSide.Evil);
        Side(NeutralKingdom, null, FactionSide.Neutral);
        Side(null, FreeCulture, FactionSide.Free);
        Side("", FreeCulture, FactionSide.Free);
        Side(null, EvilCulture, FactionSide.Evil);
        Side(PlayerKingdom, FreeCulture, FactionSide.Free);
        Side(AllyKingdom, FreeCulture, FactionSide.Free);
        Side("erebor", FreeCulture, FactionSide.Free);
        Side(VictimKingdom, EvilCulture, FactionSide.Evil);
        Side(VictimAllyKingdom, EvilCulture, FactionSide.Evil);

        // The two side predicates are pure, so use the real semantics rather than stubbing every pair.
        _alignmentService
            .AreEnemyAlignments(Arg.Any<FactionSide>(), Arg.Any<FactionSide>())
            .Returns(ci => IsEnemy(ci.ArgAt<FactionSide>(0), ci.ArgAt<FactionSide>(1)));
        _alignmentService
            .AreSameAlignment(Arg.Any<FactionSide>(), Arg.Any<FactionSide>())
            .Returns(ci => IsSame(ci.ArgAt<FactionSide>(0), ci.ArgAt<FactionSide>(1)));

        _sut = new ExecutionRelationService(_alignmentService);
    }

    private void Side(string kingdomId, string cultureId, FactionSide side)
        => _alignmentService.ResolveSide(kingdomId, cultureId).Returns(side);

    private static bool IsEnemy(FactionSide a, FactionSide b)
        => a == FactionSide.Neutral || b == FactionSide.Neutral || a != b;

    private static bool IsSame(FactionSide a, FactionSide b)
        => a != FactionSide.Neutral && b != FactionSide.Neutral && a == b;

    private static ExecutionParticipant P(string kingdomId, string cultureId = null)
        => new ExecutionParticipant(kingdomId, cultureId);

    // ----- The player report: a kingdom-less executor must not cost his allies anything -----

    [TestMethod]
    public void GetRelationModifier_EmptyExecutorKingdomButFreeCulture_AllyEvaluatorTakesNoPenalty()
    {
        // GetPlayerKingdomId() returns "" for an independent, mercenary or enlisted player. Before
        // the culture fallback this fell through to the full vanilla penalty for every clan leader
        // in the world, which is what bottomed allied Free Peoples lords out at the -100 clamp.
        var result = _sut.GetRelationModifier(
            P("", FreeCulture), P(VictimKingdom, EvilCulture), P(AllyKingdom, FreeCulture), -60, true);

        Assert.AreEqual(0, result.RelationDelta);
        Assert.IsFalse(result.ShowNotification);
    }

    [TestMethod]
    public void GetRelationModifier_NullExecutorKingdomButFreeCulture_AllyEvaluatorTakesNoPenalty()
    {
        var result = _sut.GetRelationModifier(
            P(null, FreeCulture), P(VictimKingdom, EvilCulture), P(AllyKingdom, FreeCulture), -60, true);

        Assert.AreEqual(0, result.RelationDelta);
    }

    [TestMethod]
    public void GetRelationModifier_EmptyExecutorKingdom_VictimsOwnSideStillTakesVanillaPenalty()
    {
        var result = _sut.GetRelationModifier(
            P("", FreeCulture), P(VictimKingdom, EvilCulture), P(VictimAllyKingdom, EvilCulture), -60, true);

        Assert.AreEqual(-60, result.RelationDelta);
        Assert.IsTrue(result.ShowNotification);
    }

    // ----- The victim's clan is destroyed by the kill, nulling Clan.Kingdom before the relation pass -----

    [TestMethod]
    public void GetRelationModifier_VictimKingdomNulledByClanDestruction_AllyEvaluatorTakesNoPenalty()
    {
        var result = _sut.GetRelationModifier(
            P(PlayerKingdom, FreeCulture), P(null, EvilCulture), P(AllyKingdom, FreeCulture), -60, true);

        Assert.AreEqual(0, result.RelationDelta);
    }

    [TestMethod]
    public void GetRelationModifier_VictimKingdomNulledByClanDestruction_VictimsOwnSideStillPenalised()
    {
        var result = _sut.GetRelationModifier(
            P(PlayerKingdom, FreeCulture), P(null, EvilCulture), P(VictimAllyKingdom, EvilCulture), -60, true);

        Assert.AreEqual(-60, result.RelationDelta);
    }

    // ----- A minor or mercenary clan leader has no kingdom of his own -----

    [TestMethod]
    public void GetRelationModifier_EvaluatorHasNoKingdomButFreeCulture_TakesNoPenalty()
    {
        var result = _sut.GetRelationModifier(
            P(PlayerKingdom, FreeCulture), P(VictimKingdom, EvilCulture), P(null, FreeCulture), -60, true);

        Assert.AreEqual(0, result.RelationDelta);
    }

    [TestMethod]
    public void GetRelationModifier_EvaluatorHasNoKingdomButEvilCulture_TakesVanillaPenalty()
    {
        var result = _sut.GetRelationModifier(
            P(PlayerKingdom, FreeCulture), P(VictimKingdom, EvilCulture), P(null, EvilCulture), -60, true);

        Assert.AreEqual(-60, result.RelationDelta);
    }

    // ----- Unclassified on both ids stays Neutral, preserving the pre-fix meaning -----

    [TestMethod]
    public void GetRelationModifier_ParticipantsUnclassifiedOnBothIds_ResolveNeutralAndCostNothing()
    {
        _alignmentService.ResolveSide("new_kingdom", "made_up").Returns(FactionSide.Neutral);

        var result = _sut.GetRelationModifier(
            P("new_kingdom", "made_up"), P(VictimKingdom, EvilCulture), P(AllyKingdom, FreeCulture), -60, true);

        // Neutral executor against an Evil victim is cross-alignment, and the evaluator sides with
        // neither of them, so nothing is applied.
        Assert.AreEqual(0, result.RelationDelta);
    }

    // ----- Cross-alignment: established behaviour, unchanged -----

    [TestMethod]
    public void GetRelationModifier_CrossAlignmentEvaluatorSameAsExecutor_ZerosNotification()
    {
        var result = _sut.GetRelationModifier(
            P(PlayerKingdom), P(VictimKingdom), P(AllyKingdom), -60, true);

        Assert.AreEqual(0, result.RelationDelta);
        Assert.IsFalse(result.ShowNotification, "Notification must be suppressed when modifier zeroes the delta");
    }

    [TestMethod]
    public void GetRelationModifier_CrossAlignmentEvaluatorNeutral_ZerosNotification()
    {
        var result = _sut.GetRelationModifier(
            P(PlayerKingdom), P(VictimKingdom), P(NeutralKingdom), -60, true);

        Assert.AreEqual(0, result.RelationDelta);
        Assert.IsFalse(result.ShowNotification);
    }

    [TestMethod]
    public void GetRelationModifier_CrossAlignmentEvaluatorSameAsVictim_PreservesBaseNotificationTrue()
    {
        var result = _sut.GetRelationModifier(
            P(PlayerKingdom), P(VictimKingdom), P(VictimAllyKingdom), -60, true);

        Assert.AreEqual(-60, result.RelationDelta);
        Assert.IsTrue(result.ShowNotification);
    }

    [TestMethod]
    public void GetRelationModifier_NeutralVictim_CostsTheExecutorsAlliesNothing()
    {
        var result = _sut.GetRelationModifier(
            P(PlayerKingdom), P(NeutralKingdom), P(AllyKingdom), -60, true);

        Assert.AreEqual(0, result.RelationDelta);
    }

    // ----- Kinslaying: same side, 1.5x -----

    [TestMethod]
    public void GetRelationModifier_KinslayingApplies150PercentMultiplier()
    {
        var result = _sut.GetRelationModifier(
            P(PlayerKingdom), P(AllyKingdom), P("erebor"), -60, true);

        Assert.AreEqual(-90, result.RelationDelta);
        Assert.IsTrue(result.ShowNotification);
    }

    [TestMethod]
    public void GetRelationModifier_KinslayingFriendPenalty()
    {
        var result = _sut.GetRelationModifier(
            P(PlayerKingdom), P(AllyKingdom), P("erebor"), -30, true);

        Assert.AreEqual(-45, result.RelationDelta);
    }

    [TestMethod]
    public void GetRelationModifier_KinslayingSmallerPenalty()
    {
        var result = _sut.GetRelationModifier(
            P(PlayerKingdom), P(AllyKingdom), P("erebor"), -10, true);

        Assert.AreEqual(-15, result.RelationDelta);
    }

    [TestMethod]
    public void GetRelationModifier_KinslayingByAKingdomlessPlayerStillBites()
    {
        // The culture fallback must not become a loophole: an independent Gondor-cultured player
        // executing a Rohan lord is still kinslaying.
        var result = _sut.GetRelationModifier(
            P("", FreeCulture), P(AllyKingdom, FreeCulture), P("erebor", FreeCulture), -60, true);

        Assert.AreEqual(-90, result.RelationDelta);
    }

    // ----- Notification flag -----

    [TestMethod]
    public void GetRelationModifier_ZeroBaseRelation_SuppressesNotificationEvenIfBaseTrue()
    {
        var result = _sut.GetRelationModifier(
            P(PlayerKingdom), P(AllyKingdom), P("erebor"), 0, true);

        Assert.AreEqual(0, result.RelationDelta);
        Assert.IsFalse(result.ShowNotification);
    }

    [TestMethod]
    public void GetRelationModifier_BaseNotificationFalse_StaysFalse()
    {
        var result = _sut.GetRelationModifier(
            P(PlayerKingdom), P(AllyKingdom), P("erebor"), -60, false);

        Assert.AreEqual(-90, result.RelationDelta);
        Assert.IsFalse(result.ShowNotification);
    }
}
