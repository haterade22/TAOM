using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.Execution;
using TAOM.Features.Execution.Hooks;

namespace TAOM.Tests.Features.Execution;

[TestClass]
public class ExecutionActionHookTests
{
    private const string PlayerKingdom = "empire_w";   // Gondor
    private const string AllyKingdom = "vlandia";      // Rohan
    private const string VictimKingdom = "empire_s";   // Mordor
    private const string FreeCulture = "gondor";
    private const string EvilCulture = "mordor";

    private IAlignmentService _alignmentService;
    private IExecutionRelationService _relationService;
    private ExecutionActionHook _sut;

    [TestInitialize]
    public void Setup()
    {
        _alignmentService = Substitute.For<IAlignmentService>();
        _relationService = Substitute.For<IExecutionRelationService>();

        Side(PlayerKingdom, null, FactionSide.Free);
        Side(AllyKingdom, null, FactionSide.Free);
        Side(VictimKingdom, null, FactionSide.Evil);
        Side("", FreeCulture, FactionSide.Free);
        Side(null, FreeCulture, FactionSide.Free);
        Side(VictimKingdom, EvilCulture, FactionSide.Evil);
        Side(AllyKingdom, FreeCulture, FactionSide.Free);

        _alignmentService
            .AreEnemyAlignments(Arg.Any<FactionSide>(), Arg.Any<FactionSide>())
            .Returns(ci => IsEnemy(ci.ArgAt<FactionSide>(0), ci.ArgAt<FactionSide>(1)));

        _sut = new ExecutionActionHook(_alignmentService, _relationService);
    }

    private void Side(string kingdomId, string cultureId, FactionSide side)
        => _alignmentService.ResolveSide(kingdomId, cultureId).Returns(side);

    private static bool IsEnemy(FactionSide a, FactionSide b)
        => a == FactionSide.Neutral || b == FactionSide.Neutral || a != b;

    private static ExecutionParticipant P(string kingdomId, string cultureId = null)
        => new ExecutionParticipant(kingdomId, cultureId);

    // ---- honor half (unchanged contract from v1.4.8) ----

    [TestMethod]
    public void ShouldApplyHonorPenalty_CrossAlignment_ReturnsFalse()
    {
        Assert.IsFalse(_sut.ShouldApplyHonorPenalty(P(VictimKingdom), P(PlayerKingdom)));
    }

    [TestMethod]
    public void ShouldApplyHonorPenalty_SameAlignment_ReturnsTrue()
    {
        Assert.IsTrue(_sut.ShouldApplyHonorPenalty(P(AllyKingdom), P(PlayerKingdom)));
    }

    [TestMethod]
    public void ShouldApplyHonorPenalty_KingdomlessExecutorWithFreeCulture_ReturnsFalse()
    {
        // An independent, mercenary or enlisted player executing a Mordor lord used to eat the full
        // vanilla Honor hit because the empty kingdom id bailed out before the alignment check.
        Assert.IsFalse(_sut.ShouldApplyHonorPenalty(P(VictimKingdom, EvilCulture), P("", FreeCulture)));
    }

    [TestMethod]
    public void ShouldApplyHonorPenalty_KingdomlessExecutorKinslaying_StillReturnsTrue()
    {
        Assert.IsTrue(_sut.ShouldApplyHonorPenalty(P(AllyKingdom, FreeCulture), P(null, FreeCulture)));
    }

    [TestMethod]
    public void ShouldApplyHonorPenalty_VictimKingdomNulledByClanDestruction_ReturnsFalse()
    {
        // KillCharacterAction.ApplyInternal destroys the victim's clan before OnHeroKilled, so
        // OnBloodFeudStarted can see a null kingdom. The culture fallback places the victim anyway.
        _alignmentService.ResolveSide(null, EvilCulture).Returns(FactionSide.Evil);
        Assert.IsFalse(_sut.ShouldApplyHonorPenalty(P(null, EvilCulture), P(PlayerKingdom, FreeCulture)));
    }

    // ---- relation half (v1.5.x Blood Feud seam): the hook delegates, the service decides ----

    [TestMethod]
    public void GetRelationModifier_ReturnsTheServiceDelta()
    {
        _relationService
            .GetRelationModifier(Arg.Any<ExecutionParticipant>(), Arg.Any<ExecutionParticipant>(),
                                 Arg.Any<ExecutionParticipant>(), -30, false)
            .Returns(new ExecutionRelationResult(-45, false));

        Assert.AreEqual(-45, _sut.GetRelationModifier(P(PlayerKingdom), P(AllyKingdom), P(AllyKingdom), -30));
    }

    [TestMethod]
    public void GetRelationModifier_ZeroFromTheService_IsReturnedAsZero()
    {
        // Zero is load-bearing: the engine's own != 0 guard turns it into "skip this clan", so the
        // hook must not round, clamp or substitute it.
        _relationService
            .GetRelationModifier(Arg.Any<ExecutionParticipant>(), Arg.Any<ExecutionParticipant>(),
                                 Arg.Any<ExecutionParticipant>(), Arg.Any<int>(), false)
            .Returns(new ExecutionRelationResult(0, false));

        Assert.AreEqual(0, _sut.GetRelationModifier(P(PlayerKingdom), P(VictimKingdom), P(AllyKingdom), -30));
    }

    [TestMethod]
    public void GetRelationModifier_PassesEveryParticipantThroughUnchanged_IncludingKingdomlessOnes()
    {
        // The v1.5.0 port returned early when a kingdom id was empty and handed the whole calculation
        // back to vanilla. That escape is gone: the service owns the fallback, so a kingdom-less
        // participant must reach it intact.
        _relationService
            .GetRelationModifier(Arg.Any<ExecutionParticipant>(), Arg.Any<ExecutionParticipant>(),
                                 Arg.Any<ExecutionParticipant>(), Arg.Any<int>(), false)
            .Returns(new ExecutionRelationResult(-10, false));

        _sut.GetRelationModifier(P("", FreeCulture), P(null, EvilCulture), P(AllyKingdom, FreeCulture), -30);

        _relationService.Received(1).GetRelationModifier(
            Arg.Is<ExecutionParticipant>(p => p.KingdomId == "" && p.CultureId == FreeCulture),
            Arg.Is<ExecutionParticipant>(p => p.KingdomId == null && p.CultureId == EvilCulture),
            Arg.Is<ExecutionParticipant>(p => p.KingdomId == AllyKingdom && p.CultureId == FreeCulture),
            -30,
            false);
    }

    [TestMethod]
    public void GetRelationModifier_AsksTheServiceWithNotificationsOff()
    {
        // The v1.5.2 seam applies each per-clan change with quick notifications off and shows one
        // summary itself, so the hook has no notification to forward and must not ask for one.
        _relationService
            .GetRelationModifier(Arg.Any<ExecutionParticipant>(), Arg.Any<ExecutionParticipant>(),
                                 Arg.Any<ExecutionParticipant>(), Arg.Any<int>(), Arg.Any<bool>())
            .Returns(new ExecutionRelationResult(-30, false));

        _sut.GetRelationModifier(P(PlayerKingdom), P(AllyKingdom), P(AllyKingdom), -30);

        _relationService.Received(1).GetRelationModifier(
            Arg.Any<ExecutionParticipant>(), Arg.Any<ExecutionParticipant>(),
            Arg.Any<ExecutionParticipant>(), -30, false);
    }

    // ---- the bereaved path: an AI clan executed the player's kin, vanilla's number stands ----

    [TestMethod]
    public void IsPlayerTheBereaved_VictimInThePlayersClan_ReturnsTrue()
    {
        Assert.IsTrue(_sut.IsPlayerTheBereaved("clan_player", "clan_player"));
    }

    [TestMethod]
    public void IsPlayerTheBereaved_VictimInAnotherClan_ReturnsFalse()
    {
        Assert.IsFalse(_sut.IsPlayerTheBereaved("clan_mordor_1", "clan_player"));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void IsPlayerTheBereaved_VictimClanUnknown_ReturnsFalse(string victimClanId)
    {
        // A destroyed or missing clan is not evidence the player is the bereaved; the rule stays on.
        Assert.IsFalse(_sut.IsPlayerTheBereaved(victimClanId, "clan_player"));
        Assert.IsFalse(_sut.IsPlayerTheBereaved(victimClanId, ""));
    }
}
