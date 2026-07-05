using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.WarOfTheRingMomentum;
using TAOM.Features.WarOfTheRingMomentum.Domain;

namespace TAOM.Tests.Features.WarOfTheRingMomentum;

[TestClass]
public class PlayerMomentumServiceTests
{
    private MomentumStateStore _stateStore = null!;
    private IMomentumSettingsProvider _settings = null!;
    private IPlayerContextAdapter _playerContext = null!;
    private IModLogger _logger = null!;
    private PlayerMomentumService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _stateStore = new MomentumStateStore(_logger);
        _settings = Substitute.For<IMomentumSettingsProvider>();
        _playerContext = Substitute.For<IPlayerContextAdapter>();

        _settings.MinimumPlayerEventsForVictory.Returns(5);
        _settings.ParticipationMultiplier.Returns(1.5f);
        _playerContext.GetPlayerKingdomId().Returns("");

        _sut = new PlayerMomentumService(_stateStore, _settings, _playerContext);
    }

    // ---- RecordPlayerEvent ----

    [TestMethod]
    public void RecordPlayerEvent_Adds()
    {
        _sut.RecordPlayerEvent(MomentumActionType.BattleWon);
        Assert.AreEqual(1, _stateStore.PlayerEvents.Count);
    }

    [TestMethod]
    public void RecordPlayerEvent_CapsAtTwiceMinimum_DropsOldest()
    {
        for (int i = 0; i < 12; i++)
            _sut.RecordPlayerEvent(i == 0 ? MomentumActionType.Sieges : MomentumActionType.BattleWon);

        Assert.AreEqual(10, _stateStore.PlayerEvents.Count);
        // The first (Sieges) entry was trimmed.
        Assert.AreEqual(MomentumActionType.BattleWon, _stateStore.PlayerEvents[0]);
    }

    // ---- Victory gate ----

    [TestMethod]
    public void HasPlayerMetVictoryRequirement_BelowMinimum_False()
    {
        for (int i = 0; i < 4; i++)
            _sut.RecordPlayerEvent(MomentumActionType.BattleWon);

        Assert.IsFalse(_sut.HasPlayerMetVictoryRequirement());
    }

    [TestMethod]
    public void HasPlayerMetVictoryRequirement_AtMinimum_True()
    {
        for (int i = 0; i < 5; i++)
            _sut.RecordPlayerEvent(MomentumActionType.BattleWon);

        Assert.IsTrue(_sut.HasPlayerMetVictoryRequirement());
    }

    [TestMethod]
    public void HasPlayerMetVictoryRequirement_ZeroMinimum_TrueWithNoEvents()
    {
        _settings.MinimumPlayerEventsForVictory.Returns(0);
        Assert.IsTrue(_sut.HasPlayerMetVictoryRequirement());
    }

    // ---- Multiplier ----

    [TestMethod]
    public void GetParticipationMultiplier_NotInvolved_ReturnsOne()
    {
        Assert.AreEqual(1.0f, _sut.GetParticipationMultiplier(false), 0.0001f);
    }

    [TestMethod]
    public void GetParticipationMultiplier_Involved_ReturnsConfiguredValue()
    {
        Assert.AreEqual(1.5f, _sut.GetParticipationMultiplier(true), 0.0001f);
    }

    // ---- IsPlayerOnStrongerSide ----

    private MomentumWarState StateWithSides()
    {
        var state = new MomentumWarState();
        state.Free.AddKingdom("empire_w");
        state.Evil.AddKingdom("empire_s");
        return state;
    }

    [TestMethod]
    public void IsPlayerOnStrongerSide_NoPlayerKingdom_False()
    {
        Assert.IsFalse(_sut.IsPlayerOnStrongerSide(StateWithSides(), 100f, 50f));
    }

    [TestMethod]
    public void IsPlayerOnStrongerSide_PlayerKingdomNotEnrolled_False()
    {
        _playerContext.GetPlayerKingdomId().Returns("umbar");
        Assert.IsFalse(_sut.IsPlayerOnStrongerSide(StateWithSides(), 100f, 50f));
    }

    [TestMethod]
    public void IsPlayerOnStrongerSide_FreePlayerFreeStronger_True()
    {
        _playerContext.GetPlayerKingdomId().Returns("empire_w");
        Assert.IsTrue(_sut.IsPlayerOnStrongerSide(StateWithSides(), 100f, 50f));
    }

    [TestMethod]
    public void IsPlayerOnStrongerSide_FreePlayerEvilStronger_False()
    {
        _playerContext.GetPlayerKingdomId().Returns("empire_w");
        Assert.IsFalse(_sut.IsPlayerOnStrongerSide(StateWithSides(), 50f, 100f));
    }

    [TestMethod]
    public void IsPlayerOnStrongerSide_EvilPlayerEvilStronger_True()
    {
        _playerContext.GetPlayerKingdomId().Returns("empire_s");
        Assert.IsTrue(_sut.IsPlayerOnStrongerSide(StateWithSides(), 50f, 100f));
    }

    [TestMethod]
    public void IsPlayerOnStrongerSide_ExactTie_CountsAsEvilStronger()
    {
        // LOTRAOM parity: strict `>` comparison — a tie is "not Free-stronger".
        _playerContext.GetPlayerKingdomId().Returns("empire_s");
        Assert.IsTrue(_sut.IsPlayerOnStrongerSide(StateWithSides(), 100f, 100f));

        _playerContext.GetPlayerKingdomId().Returns("empire_w");
        Assert.IsFalse(_sut.IsPlayerOnStrongerSide(StateWithSides(), 100f, 100f));
    }

    [TestMethod]
    public void IsPlayerOnStrongerSide_NullState_False()
    {
        _playerContext.GetPlayerKingdomId().Returns("empire_w");
        Assert.IsFalse(_sut.IsPlayerOnStrongerSide(null, 100f, 50f));
    }
}
