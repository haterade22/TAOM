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
    private ExecutionActionHook _sut;

    [TestInitialize]
    public void Setup()
    {
        _alignmentService = Substitute.For<IAlignmentService>();

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

        _sut = new ExecutionActionHook(_alignmentService);
    }

    private void Side(string kingdomId, string cultureId, FactionSide side)
        => _alignmentService.ResolveSide(kingdomId, cultureId).Returns(side);

    private static bool IsEnemy(FactionSide a, FactionSide b)
        => a == FactionSide.Neutral || b == FactionSide.Neutral || a != b;

    private static ExecutionParticipant P(string kingdomId, string cultureId = null)
        => new ExecutionParticipant(kingdomId, cultureId);

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
        // vanilla -1000 Honor XP because the empty kingdom id bailed out before the alignment check.
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
        _alignmentService.ResolveSide(null, EvilCulture).Returns(FactionSide.Evil);

        Assert.IsFalse(_sut.ShouldApplyHonorPenalty(P(null, EvilCulture), P(PlayerKingdom, FreeCulture)));
    }
}
