using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.Messengers;
using TAOM.Features.Messengers.Domain;

namespace TAOM.Tests.Features.Messengers;

[TestClass]
public class MessengerServiceTests
{
    private IMessengerSettingsProvider _settings = null!;
    private IMessengerConfigProvider _config = null!;
    private IMessengerStateStore _store = null!;
    private IMessengerRandomSource _random = null!;
    private MessengerService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IMessengerSettingsProvider>();
        _settings.EnableMessengers.Returns(true);
        _settings.MessengerGoldCost.Returns(50);
        _settings.MessengerTravelDays.Returns(3);
        _settings.MessengerAccidents.Returns(true);

        _config = Substitute.For<IMessengerConfigProvider>();
        _config.GetConfig().Returns(new MessengerConfig());

        _store = Substitute.For<IMessengerStateStore>();
        _store.Contains(Arg.Any<string>()).Returns(false);

        _random = Substitute.For<IMessengerRandomSource>();
        _random.NextFloat().Returns(0.5f);

        _sut = new MessengerService(_settings, _config, _store, _random);
    }

    private static HeroSnapshot ValidActiveHero(string id = "lord_1") =>
        new HeroSnapshot(id,
            isAlive: true, isPrisoner: false, isChild: false, isFugitive: false,
            isActive: true, isWanderer: false, isHumanPlayerCharacter: false,
            isInPlayerParty: false, isInMapEvent: false);

    // --- Validation: skip-guard exhaustion ---

    [TestMethod]
    public void CanSendMessenger_NullTarget_ReturnsNullTarget()
    {
        var result = _sut.CanSendMessenger(null, playerGold: 1000);
        Assert.AreEqual(MessengerValidationResult.NullTarget, result);
    }

    [TestMethod]
    public void CanSendMessenger_PlayerCharacter_ReturnsHumanPlayerCharacter()
    {
        var hero = new HeroSnapshot("player",
            isAlive: true, isPrisoner: false, isChild: false, isFugitive: false,
            isActive: true, isWanderer: false, isHumanPlayerCharacter: true,
            isInPlayerParty: true, isInMapEvent: false);

        var result = _sut.CanSendMessenger(hero, playerGold: 1000);

        Assert.AreEqual(MessengerValidationResult.HumanPlayerCharacter, result);
    }

    [TestMethod]
    public void CanSendMessenger_DeadHero_ReturnsHeroDead()
    {
        var hero = new HeroSnapshot("lord_dead",
            isAlive: false, isPrisoner: false, isChild: false, isFugitive: false,
            isActive: false, isWanderer: false, isHumanPlayerCharacter: false,
            isInPlayerParty: false, isInMapEvent: false);

        var result = _sut.CanSendMessenger(hero, playerGold: 1000);

        Assert.AreEqual(MessengerValidationResult.HeroDead, result);
    }

    [TestMethod]
    public void CanSendMessenger_PrisonerHero_ReturnsHeroPrisoner()
    {
        var hero = new HeroSnapshot("lord_prisoner",
            isAlive: true, isPrisoner: true, isChild: false, isFugitive: false,
            isActive: false, isWanderer: false, isHumanPlayerCharacter: false,
            isInPlayerParty: false, isInMapEvent: false);

        var result = _sut.CanSendMessenger(hero, playerGold: 1000);

        Assert.AreEqual(MessengerValidationResult.HeroPrisoner, result);
    }

    [TestMethod]
    public void CanSendMessenger_ChildHero_ReturnsHeroChild()
    {
        var hero = new HeroSnapshot("lord_child",
            isAlive: true, isPrisoner: false, isChild: true, isFugitive: false,
            isActive: true, isWanderer: false, isHumanPlayerCharacter: false,
            isInPlayerParty: false, isInMapEvent: false);

        var result = _sut.CanSendMessenger(hero, playerGold: 1000);

        Assert.AreEqual(MessengerValidationResult.HeroChild, result);
    }

    [TestMethod]
    public void CanSendMessenger_FugitiveHero_ReturnsHeroFugitive()
    {
        var hero = new HeroSnapshot("lord_fugitive",
            isAlive: true, isPrisoner: false, isChild: false, isFugitive: true,
            isActive: false, isWanderer: false, isHumanPlayerCharacter: false,
            isInPlayerParty: false, isInMapEvent: false);

        var result = _sut.CanSendMessenger(hero, playerGold: 1000);

        Assert.AreEqual(MessengerValidationResult.HeroFugitive, result);
    }

    [TestMethod]
    public void CanSendMessenger_HeroNotActiveAndNotWanderer_ReturnsTargetUnavailable()
    {
        var hero = new HeroSnapshot("lord_idle",
            isAlive: true, isPrisoner: false, isChild: false, isFugitive: false,
            isActive: false, isWanderer: false, isHumanPlayerCharacter: false,
            isInPlayerParty: false, isInMapEvent: false);

        var result = _sut.CanSendMessenger(hero, playerGold: 1000);

        Assert.AreEqual(MessengerValidationResult.TargetUnavailable, result);
    }

    [TestMethod]
    public void CanSendMessenger_TargetInPlayerParty_ReturnsTargetInPlayerParty()
    {
        var hero = new HeroSnapshot("companion",
            isAlive: true, isPrisoner: false, isChild: false, isFugitive: false,
            isActive: true, isWanderer: true, isHumanPlayerCharacter: false,
            isInPlayerParty: true, isInMapEvent: false);

        var result = _sut.CanSendMessenger(hero, playerGold: 1000);

        Assert.AreEqual(MessengerValidationResult.TargetInPlayerParty, result);
    }

    [TestMethod]
    public void CanSendMessenger_InsufficientGold_ReturnsInsufficientGold()
    {
        _settings.MessengerGoldCost.Returns(50);

        var result = _sut.CanSendMessenger(ValidActiveHero(), playerGold: 49);

        Assert.AreEqual(MessengerValidationResult.InsufficientGold, result);
    }

    [TestMethod]
    public void CanSendMessenger_AlreadyPending_ReturnsAlreadyPending()
    {
        _store.Contains("lord_1").Returns(true);

        var result = _sut.CanSendMessenger(ValidActiveHero(), playerGold: 1000);

        Assert.AreEqual(MessengerValidationResult.AlreadyPending, result);
    }

    [TestMethod]
    public void CanSendMessenger_NotSpawnedWanderer_AllowedAsTarget()
    {
        var wanderer = new HeroSnapshot("wanderer_1",
            isAlive: true, isPrisoner: false, isChild: false, isFugitive: false,
            isActive: false, isWanderer: true, isHumanPlayerCharacter: false,
            isInPlayerParty: false, isInMapEvent: false);

        var result = _sut.CanSendMessenger(wanderer, playerGold: 1000);

        Assert.AreEqual(MessengerValidationResult.Ok, result);
    }

    [TestMethod]
    public void CanSendMessenger_ValidActiveHero_ReturnsOk()
    {
        var result = _sut.CanSendMessenger(ValidActiveHero(), playerGold: 1000);

        Assert.AreEqual(MessengerValidationResult.Ok, result);
    }

    [TestMethod]
    public void CanSendMessenger_ExactlyEnoughGold_ReturnsOk()
    {
        _settings.MessengerGoldCost.Returns(50);

        var result = _sut.CanSendMessenger(ValidActiveHero(), playerGold: 50);

        Assert.AreEqual(MessengerValidationResult.Ok, result);
    }

    // --- RollAccident ---

    [TestMethod]
    public void RollAccident_AccidentsDisabledInSettings_ReturnsFalse()
    {
        _settings.MessengerAccidents.Returns(false);
        _random.NextFloat().Returns(0.0f);

        Assert.IsFalse(_sut.RollAccident());
    }

    [TestMethod]
    public void RollAccident_ChanceZero_ReturnsFalse()
    {
        _config.GetConfig().Returns(new MessengerConfig { AccidentChancePerHour = 0f });
        _random.NextFloat().Returns(0f);

        Assert.IsFalse(_sut.RollAccident());
    }

    [TestMethod]
    public void RollAccident_ChanceOne_ReturnsTrue()
    {
        _config.GetConfig().Returns(new MessengerConfig { AccidentChancePerHour = 1f });
        _random.NextFloat().Returns(0.999f);

        Assert.IsTrue(_sut.RollAccident());
    }

    [TestMethod]
    public void RollAccident_RandomBelowChance_ReturnsTrue()
    {
        _config.GetConfig().Returns(new MessengerConfig { AccidentChancePerHour = 0.1f });
        _random.NextFloat().Returns(0.05f);

        Assert.IsTrue(_sut.RollAccident());
    }

    [TestMethod]
    public void RollAccident_RandomAboveChance_ReturnsFalse()
    {
        _config.GetConfig().Returns(new MessengerConfig { AccidentChancePerHour = 0.1f });
        _random.NextFloat().Returns(0.5f);

        Assert.IsFalse(_sut.RollAccident());
    }

    // --- AdvancePosition ---

    [TestMethod]
    public void AdvancePosition_DistanceLessThanSpeed_SnapsAndArrives()
    {
        var current = new MapCoord(0f, 0f);
        var target = new MapCoord(3f, 4f);   // distance = 5
        var speed = 10f;

        var update = _sut.AdvancePosition(current, target, speed);

        Assert.IsTrue(update.Arrived);
        Assert.AreEqual(target.X, update.NewPosition.X, 0.0001f);
        Assert.AreEqual(target.Y, update.NewPosition.Y, 0.0001f);
    }

    [TestMethod]
    public void AdvancePosition_DistanceEqualToSpeed_SnapsAndArrives()
    {
        var current = new MapCoord(0f, 0f);
        var target = new MapCoord(3f, 4f);   // distance = 5
        var speed = 5f;

        var update = _sut.AdvancePosition(current, target, speed);

        Assert.IsTrue(update.Arrived);
    }

    [TestMethod]
    public void AdvancePosition_DistanceGreaterThanSpeed_StepsTowardTarget()
    {
        var current = new MapCoord(0f, 0f);
        var target = new MapCoord(10f, 0f);
        var speed = 2f;

        var update = _sut.AdvancePosition(current, target, speed);

        Assert.IsFalse(update.Arrived);
        Assert.AreEqual(2f, update.NewPosition.X, 0.0001f);
        Assert.AreEqual(0f, update.NewPosition.Y, 0.0001f);
    }

    [TestMethod]
    public void AdvancePosition_InvalidTarget_ReturnsCurrentNotArrived()
    {
        var current = new MapCoord(5f, 5f);
        var update = _sut.AdvancePosition(current, MapCoord.Invalid, speed: 10f);

        Assert.IsFalse(update.Arrived);
        Assert.AreEqual(current.X, update.NewPosition.X);
        Assert.AreEqual(current.Y, update.NewPosition.Y);
    }

    // --- CalculateMessengerSpeed ---

    [TestMethod]
    public void CalculateMessengerSpeed_StandardInputs_DivBy24TimesDays()
    {
        var speed = _sut.CalculateMessengerSpeed(mapDiagonal: 240f, travelDays: 5);

        // 240 / (24 * 5) = 2.0; multiplier = 1.0
        Assert.AreEqual(2.0f, speed, 0.0001f);
    }

    [TestMethod]
    public void CalculateMessengerSpeed_ZeroDays_ClampsToOne()
    {
        var speed = _sut.CalculateMessengerSpeed(mapDiagonal: 240f, travelDays: 0);

        // 240 / (24 * 1) = 10.0
        Assert.AreEqual(10.0f, speed, 0.0001f);
    }

    [TestMethod]
    public void CalculateMessengerSpeed_NegativeDays_ClampsToOne()
    {
        var speed = _sut.CalculateMessengerSpeed(mapDiagonal: 240f, travelDays: -3);

        Assert.AreEqual(10.0f, speed, 0.0001f);
    }

    [TestMethod]
    public void CalculateMessengerSpeed_DoubleMultiplier_DoublesSpeed()
    {
        _config.GetConfig().Returns(new MessengerConfig { TravelSpeedMultiplier = 2.0f });

        var speed = _sut.CalculateMessengerSpeed(mapDiagonal: 240f, travelDays: 5);

        Assert.AreEqual(4.0f, speed, 0.0001f);
    }
}
