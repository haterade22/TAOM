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
            heroState: MessengerHeroState.Active,isWanderer: false, isHumanPlayerCharacter: false,
            isInPlayerParty: false);

    // --- Validation: skip-guard exhaustion ---

    [TestMethod]
    public void CanSendMessenger_NullTarget_ReturnsNullTarget()
    {
        var result = _sut.CanSendMessenger(null, playerGold: 1000, quotedCost: 50, inPerson: false);
        Assert.AreEqual(MessengerValidationResult.NullTarget, result);
    }

    [TestMethod]
    public void CanSendMessenger_PlayerCharacter_ReturnsHumanPlayerCharacter()
    {
        var hero = new HeroSnapshot("player",
            isAlive: true, isPrisoner: false, isChild: false, isFugitive: false,
            heroState: MessengerHeroState.Active,isWanderer: false, isHumanPlayerCharacter: true,
            isInPlayerParty: true);

        var result = _sut.CanSendMessenger(hero, playerGold: 1000, quotedCost: 50, inPerson: false);

        Assert.AreEqual(MessengerValidationResult.HumanPlayerCharacter, result);
    }

    [TestMethod]
    public void CanSendMessenger_DeadHero_ReturnsHeroDead()
    {
        var hero = new HeroSnapshot("lord_dead",
            isAlive: false, isPrisoner: false, isChild: false, isFugitive: false,
            heroState: MessengerHeroState.Dead,isWanderer: false, isHumanPlayerCharacter: false,
            isInPlayerParty: false);

        var result = _sut.CanSendMessenger(hero, playerGold: 1000, quotedCost: 50, inPerson: false);

        Assert.AreEqual(MessengerValidationResult.HeroDead, result);
    }

    [TestMethod]
    public void CanSendMessenger_PrisonerHero_ReturnsHeroPrisoner()
    {
        var hero = new HeroSnapshot("lord_prisoner",
            isAlive: true, isPrisoner: true, isChild: false, isFugitive: false,
            heroState: MessengerHeroState.Prisoner,isWanderer: false, isHumanPlayerCharacter: false,
            isInPlayerParty: false);

        var result = _sut.CanSendMessenger(hero, playerGold: 1000, quotedCost: 50, inPerson: false);

        Assert.AreEqual(MessengerValidationResult.HeroPrisoner, result);
    }

    [TestMethod]
    public void CanSendMessenger_ChildHero_ReturnsHeroChild()
    {
        var hero = new HeroSnapshot("lord_child",
            isAlive: true, isPrisoner: false, isChild: true, isFugitive: false,
            heroState: MessengerHeroState.Active,isWanderer: false, isHumanPlayerCharacter: false,
            isInPlayerParty: false);

        var result = _sut.CanSendMessenger(hero, playerGold: 1000, quotedCost: 50, inPerson: false);

        Assert.AreEqual(MessengerValidationResult.HeroChild, result);
    }

    [TestMethod]
    public void CanSendMessenger_FugitiveHero_ReturnsHeroFugitive()
    {
        var hero = new HeroSnapshot("lord_fugitive",
            isAlive: true, isPrisoner: false, isChild: false, isFugitive: true,
            heroState: MessengerHeroState.Fugitive,isWanderer: false, isHumanPlayerCharacter: false,
            isInPlayerParty: false);

        var result = _sut.CanSendMessenger(hero, playerGold: 1000, quotedCost: 50, inPerson: false);

        Assert.AreEqual(MessengerValidationResult.HeroFugitive, result);
    }

    [TestMethod]
    public void CanSendMessenger_HeroNotActiveAndNotWanderer_ReturnsTargetUnavailable()
    {
        var hero = new HeroSnapshot("lord_idle",
            isAlive: true, isPrisoner: false, isChild: false, isFugitive: false,
            heroState: MessengerHeroState.NotSpawned,isWanderer: false, isHumanPlayerCharacter: false,
            isInPlayerParty: false);

        var result = _sut.CanSendMessenger(hero, playerGold: 1000, quotedCost: 50, inPerson: false);

        Assert.AreEqual(MessengerValidationResult.TargetUnavailable, result);
    }

    [TestMethod]
    public void CanSendMessenger_TargetInPlayerParty_ReturnsTargetInPlayerParty()
    {
        var hero = new HeroSnapshot("companion",
            isAlive: true, isPrisoner: false, isChild: false, isFugitive: false,
            heroState: MessengerHeroState.Active,isWanderer: true, isHumanPlayerCharacter: false,
            isInPlayerParty: true);

        var result = _sut.CanSendMessenger(hero, playerGold: 1000, quotedCost: 50, inPerson: false);

        Assert.AreEqual(MessengerValidationResult.TargetInPlayerParty, result);
    }

    [TestMethod]
    public void CanSendMessenger_InsufficientGold_ReturnsInsufficientGold()
    {
        var result = _sut.CanSendMessenger(ValidActiveHero(), playerGold: 124, quotedCost: 125, inPerson: false);

        Assert.AreEqual(MessengerValidationResult.InsufficientGold, result);
    }

    [TestMethod]
    public void CanSendMessenger_SameSettlement_ReturnsSameSettlement()
    {
        var hero = new HeroSnapshot("lord_1",
            isAlive: true, isPrisoner: false, isChild: false, isFugitive: false,
            heroState: MessengerHeroState.Active,isWanderer: false, isHumanPlayerCharacter: false,
            isInPlayerParty: false, sameSettlement: true);

        Assert.AreEqual(MessengerValidationResult.SameSettlement, _sut.CanSendMessenger(hero, playerGold: 1000, quotedCost: 50, inPerson: false));
    }

    [TestMethod]
    public void CanSendMessenger_SameArmy_ReturnsSameArmy()
    {
        var hero = new HeroSnapshot("lord_1",
            isAlive: true, isPrisoner: false, isChild: false, isFugitive: false,
            heroState: MessengerHeroState.Active,isWanderer: false, isHumanPlayerCharacter: false,
            isInPlayerParty: false, sameArmy: true);

        Assert.AreEqual(MessengerValidationResult.SameArmy, _sut.CanSendMessenger(hero, playerGold: 1000, quotedCost: 50, inPerson: false));
    }

    [TestMethod]
    public void CanSendMessenger_InPerson_IgnoresTogethernessButStillChecksGold()
    {
        var hero = new HeroSnapshot("lord_1",
            isAlive: true, isPrisoner: false, isChild: false, isFugitive: false,
            heroState: MessengerHeroState.Active,isWanderer: false, isHumanPlayerCharacter: false,
            isInPlayerParty: false, sameSettlement: true, sameArmy: true);

        Assert.AreEqual(MessengerValidationResult.Ok, _sut.CanSendMessenger(hero, playerGold: 1000, quotedCost: 50, inPerson: true));
        Assert.AreEqual(MessengerValidationResult.InsufficientGold, _sut.CanSendMessenger(hero, playerGold: 0, quotedCost: 50, inPerson: true));
    }

    [TestMethod]
    public void GetOffer_ChecksGoldAgainstThisTripsQuote()
    {
        var offer = _sut.GetOffer(ValidActiveHero(), Trip(1000f), playerGold: 124, inPerson: false);

        Assert.AreEqual(MessengerValidationResult.InsufficientGold, offer.Validation);
        Assert.AreEqual(125, offer.Cost);
        Assert.AreEqual(3, offer.Days);
        Assert.IsFalse(offer.CanSend);
    }

    [TestMethod]
    public void CanSendMessenger_SameSettlementAndPoor_ReportsTheSettlementFirst()
    {
        var hero = new HeroSnapshot("lord_1",
            isAlive: true, isPrisoner: false, isChild: false, isFugitive: false,
            heroState: MessengerHeroState.Active,isWanderer: false, isHumanPlayerCharacter: false,
            isInPlayerParty: false, sameSettlement: true);

        Assert.AreEqual(MessengerValidationResult.SameSettlement, _sut.CanSendMessenger(hero, playerGold: 0, quotedCost: 50, inPerson: false));
    }

    [TestMethod]
    public void CanSendMessenger_AlreadyPending_ReturnsAlreadyPending()
    {
        _store.Contains("lord_1").Returns(true);

        var result = _sut.CanSendMessenger(ValidActiveHero(), playerGold: 1000, quotedCost: 50, inPerson: false);

        Assert.AreEqual(MessengerValidationResult.AlreadyPending, result);
    }

    [TestMethod]
    public void CanSendMessenger_NotSpawnedWanderer_AllowedAsTarget()
    {
        var wanderer = new HeroSnapshot("wanderer_1",
            isAlive: true, isPrisoner: false, isChild: false, isFugitive: false,
            heroState: MessengerHeroState.NotSpawned,isWanderer: true, isHumanPlayerCharacter: false,
            isInPlayerParty: false);

        var result = _sut.CanSendMessenger(wanderer, playerGold: 1000, quotedCost: 50, inPerson: false);

        Assert.AreEqual(MessengerValidationResult.Ok, result);
    }

    // One state at a time, with the facts the engine derives from it (v1.5.7 Hero.cs:303-319).
    private static HeroSnapshot InState(MessengerHeroState state, bool wanderer, bool special = false) =>
        new HeroSnapshot("hero_1",
            isAlive: state != MessengerHeroState.Dead,
            isPrisoner: state == MessengerHeroState.Prisoner,
            isChild: false,
            isFugitive: state == MessengerHeroState.Fugitive,
            heroState: state,
            isWanderer: wanderer,
            isHumanPlayerCharacter: false,
            isInPlayerParty: false,
            isSpecial: special);

    [DataTestMethod]
    [DataRow(MessengerHeroState.Released, false, MessengerValidationResult.Ok)]
    [DataRow(MessengerHeroState.Traveling, false, MessengerValidationResult.Ok)]
    [DataRow(MessengerHeroState.Released, true, MessengerValidationResult.Ok)]
    [DataRow(MessengerHeroState.Traveling, true, MessengerValidationResult.Ok)]
    [DataRow(MessengerHeroState.NotSpawned, false, MessengerValidationResult.TargetUnavailable)]
    [DataRow(MessengerHeroState.Disabled, false, MessengerValidationResult.TargetUnavailable)]
    [DataRow(MessengerHeroState.Disabled, true, MessengerValidationResult.TargetUnavailable)]
    public void CanSendMessenger_HeroNotActive_FollowsTheArrivalRule(MessengerHeroState state, bool wanderer, MessengerValidationResult expected)
        => Assert.AreEqual(expected, _sut.CanSendMessenger(InState(state, wanderer), playerGold: 1000, quotedCost: 50, inPerson: false));

    // The engine's daily settle skips a released hero of the Special occupation, so the send refuses one; a Special hero on a
    // journey or Active can be sent to like any other.
    [DataTestMethod]
    [DataRow(MessengerHeroState.Released, MessengerValidationResult.TargetUnavailable)]
    [DataRow(MessengerHeroState.Fugitive, MessengerValidationResult.HeroFugitive)]
    [DataRow(MessengerHeroState.Traveling, MessengerValidationResult.Ok)]
    [DataRow(MessengerHeroState.Active, MessengerValidationResult.Ok)]
    public void CanSendMessenger_SpecialHero_IsRefusedOnlyWhereTheEngineNeverSettlesIt(MessengerHeroState state, MessengerValidationResult expected)
        => Assert.AreEqual(expected, _sut.CanSendMessenger(InState(state, wanderer: false, special: true), playerGold: 1000, quotedCost: 50, inPerson: false));

    [TestMethod]
    public void CanSendMessenger_NeverAcceptsATargetTheArrivalWouldLose()
    {
        // #1133: the player must not be able to pay for a messenger that is always lost on arrival.
        foreach (MessengerHeroState state in System.Enum.GetValues(typeof(MessengerHeroState)))
        foreach (var wanderer in new[] { false, true })
        foreach (var special in new[] { false, true })
        {
            var target = InState(state, wanderer, special);
            if (_sut.CanSendMessenger(target, playerGold: 1000, quotedCost: 50, inPerson: false) != MessengerValidationResult.Ok)
                continue;

            Assert.AreNotEqual(MessengerArrivalVerdict.Lost,
                _sut.ClassifyArrival(target, playerAvailable: true, targetPartyInMapEvent: false),
                $"{state} wanderer={wanderer} special={special} can be sent to but is lost on arrival");
        }
    }

    [TestMethod]
    public void CanSendMessenger_ValidActiveHero_ReturnsOk()
    {
        var result = _sut.CanSendMessenger(ValidActiveHero(), playerGold: 1000, quotedCost: 50, inPerson: false);

        Assert.AreEqual(MessengerValidationResult.Ok, result);
    }

    [TestMethod]
    public void CanSendMessenger_ExactlyEnoughGold_ReturnsOk()
    {
        var result = _sut.CanSendMessenger(ValidActiveHero(), playerGold: 125, quotedCost: 125, inPerson: false);

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

    // --- QuoteCost / QuoteTravelDays (#898 part 2) ---

    private static MessengerQuoteInput Trip(float distance, bool atWar = false, bool neverMet = false, bool besieged = false,
        float diagonal = 1000f, int travelDays = 3) =>
        new(distance, diagonal, travelDays, atWar, neverMet, besieged);

    [TestMethod]
    public void QuoteCost_NextDoor_IsTheBaseCost() => Assert.AreEqual(50, _sut.QuoteCost(Trip(0f)));

    [TestMethod]
    public void QuoteCost_AcrossTheMap_IsBasePlusAFullTrip() => Assert.AreEqual(125, _sut.QuoteCost(Trip(1000f)));

    [TestMethod]
    public void QuoteCost_HalfWay_RoundsTheFractionUp() => Assert.AreEqual(88, _sut.QuoteCost(Trip(500f)));

    [TestMethod]
    public void QuoteCost_BeyondTheDiagonal_StopsAtAFullTrip() => Assert.AreEqual(125, _sut.QuoteCost(Trip(5000f)));

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(-1f)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(float.PositiveInfinity)]
    public void QuoteCost_ABadDistance_CountsAsAFullTrip(float distance) => Assert.AreEqual(125, _sut.QuoteCost(Trip(distance)));

    [DataTestMethod]
    [DataRow(0f)]
    [DataRow(-5f)]
    [DataRow(float.NaN)]
    public void QuoteCost_ABadMapDiagonal_CountsAsAFullTrip(float diagonal) => Assert.AreEqual(125, _sut.QuoteCost(Trip(10f, diagonal: diagonal)));

    [TestMethod]
    public void QuoteCost_AtWar_DoublesIt() => Assert.AreEqual(250, _sut.QuoteCost(Trip(1000f, atWar: true)));

    [TestMethod]
    public void QuoteCost_NeverMet_DoublesIt() => Assert.AreEqual(250, _sut.QuoteCost(Trip(1000f, neverMet: true)));

    [TestMethod]
    public void QuoteCost_Besieged_IsFiveTimes() => Assert.AreEqual(625, _sut.QuoteCost(Trip(1000f, besieged: true)));

    [TestMethod]
    public void QuoteCost_EveryMultiplier_Stacks() => Assert.AreEqual(2500, _sut.QuoteCost(Trip(1000f, true, true, true)));

    [TestMethod]
    public void QuoteCost_TravelSpeedMultiplier_ShortensTheTrip()
    {
        _config.GetConfig().Returns(new MessengerConfig { TravelSpeedMultiplier = 2f });

        Assert.AreEqual(50 + 19, _sut.QuoteCost(Trip(500f)), "3 days * 0.25 = 0.75 day, 18.75 gold, rounded up");
    }

    [TestMethod]
    public void QuoteCost_ASpeedMultiplierOfZero_CountsAsAFullTrip()
    {
        // The provider keeps this at 0.1 or more; the service still must not divide into a non-finite fraction.
        _config.GetConfig().Returns(new MessengerConfig { TravelSpeedMultiplier = 0f });

        Assert.AreEqual(125, _sut.QuoteCost(Trip(500f)));
        Assert.AreEqual(3, _sut.QuoteTravelDays(Trip(500f)));
    }

    [TestMethod]
    public void QuoteCost_ZeroTravelDays_IsTreatedAsOne()
    {
        Assert.AreEqual(75, _sut.QuoteCost(Trip(1000f, travelDays: 0)));
        Assert.AreEqual(1, _sut.QuoteTravelDays(Trip(1000f, travelDays: 0)));
    }

    [TestMethod]
    public void QuoteCost_NeverExceedsTheCeiling()
    {
        _settings.MessengerGoldCost.Returns(500);
        _config.GetConfig().Returns(new MessengerConfig { GoldPerTravelDay = 1000f, AtWarMultiplier = 10f, NeverMetMultiplier = 10f, BesiegedMultiplier = 10f });

        Assert.AreEqual(100000, _sut.QuoteCost(Trip(1000f, true, true, true, travelDays: 10)));
    }

    [TestMethod]
    public void QuoteCost_WithNoPerDayRate_IsTheBaseCostAnywhere()
    {
        _config.GetConfig().Returns(new MessengerConfig { GoldPerTravelDay = 0f });

        Assert.AreEqual(50, _sut.QuoteCost(Trip(1000f)));
    }

    [TestMethod]
    public void QuoteCost_ANonFiniteResult_IsTheBaseCost()
    {
        // The provider validates the config; if a poisoned value ever got through, the NaN exit gate keeps the
        // price finite instead of letting it reach the gold check.
        _config.GetConfig().Returns(new MessengerConfig { GoldPerTravelDay = float.NaN });

        Assert.AreEqual(50, _sut.QuoteCost(Trip(1000f)));
    }

    [DataTestMethod]
    [DataRow(0f, 1)]
    [DataRow(500f, 2)]
    [DataRow(1000f, 3)]
    [DataRow(float.NaN, 3)]
    public void QuoteTravelDays_IsTheWholeDaysTheTripTakes_AtLeastOne(float distance, int expected)
        => Assert.AreEqual(expected, _sut.QuoteTravelDays(Trip(distance)));
}
