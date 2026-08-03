using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.PlayerPossession;

namespace TAOM.Tests.Features.PlayerPossession;

[TestClass]
public class PlayerPossessionServiceTests
{
    private ICoopPresenceProvider _coopPresence;
    private IModLogger _logger;
    private PlayerPossessionService _sut;

    [TestInitialize]
    public void Setup()
    {
        _coopPresence = Substitute.For<ICoopPresenceProvider>();
        _logger = Substitute.For<IModLogger>();
        _coopPresence.IsCoopActive.Returns(true);
        _sut = new PlayerPossessionService(_coopPresence, _logger);
    }

    private static PlayerCharacterCreationChoices Choices(string heroId = "cc_hero") =>
        new(heroId, "mirkwood", 3, "ranger");

    // --- The case the feature exists for ---

    [TestMethod]
    public void TryConsumePossession_HeroSwitchedAfterCharacterCreation_ReturnsChoices()
    {
        _sut.RecordBaselineHero("cc_hero");
        _sut.CaptureCharacterCreationChoices(Choices());

        var detected = _sut.TryConsumePossession("host_authored_hero", out var choices);

        Assert.IsTrue(detected);
        Assert.AreEqual("mirkwood", choices.CultureId);
        Assert.AreEqual(3, choices.RaceId);
        Assert.AreEqual("ranger", choices.CareerId);
        Assert.AreEqual("host_authored_hero", _sut.PossessedHeroId);
    }

    [TestMethod]
    public void TryConsumePossession_CalledRepeatedly_ReturnsTrueOnlyOnce()
    {
        // The hourly tick keeps calling this forever; a second true would re-grant the package.
        _sut.RecordBaselineHero("cc_hero");
        _sut.CaptureCharacterCreationChoices(Choices());

        Assert.IsTrue(_sut.TryConsumePossession("host_hero", out _));
        Assert.IsFalse(_sut.TryConsumePossession("host_hero", out _));
        Assert.IsFalse(_sut.TryConsumePossession("host_hero", out _));
    }

    // --- Single-player inertness: the guards that keep this away from heir succession ---

    [TestMethod]
    public void TryConsumePossession_NoCoopModule_NeverFires()
    {
        // Hero.MainHero ALSO changes in single-player when the player dies and continues as an
        // heir. Without the co-op gate this feature would hand that heir a fresh starting package.
        _coopPresence.IsCoopActive.Returns(false);
        _sut.RecordBaselineHero("player_hero");
        _sut.CaptureCharacterCreationChoices(Choices("player_hero"));

        var detected = _sut.TryConsumePossession("heir_hero", out var choices);

        Assert.IsFalse(detected);
        Assert.IsNull(choices);
        Assert.IsNull(_sut.PossessedHeroId);
    }

    [TestMethod]
    public void TryConsumePossession_CoopActiveButHeirSucceedsAfterJoin_DoesNotReFire()
    {
        // Co-op player joins (consumes the package), then dies hours later and inherits. The heir
        // is a hero-id change exactly like the join was — single-consumption is what separates them.
        _sut.RecordBaselineHero("cc_hero");
        _sut.CaptureCharacterCreationChoices(Choices());
        Assert.IsTrue(_sut.TryConsumePossession("joined_hero", out _), "precondition: join consumed");

        var heirDetected = _sut.TryConsumePossession("heir_hero", out var heirChoices);

        Assert.IsFalse(heirDetected);
        Assert.IsNull(heirChoices);
    }

    [TestMethod]
    public void TryConsumePossession_NoCharacterCreationCaptured_NeverFires()
    {
        // Rejoining an existing campaign in a fresh process: no CC happened, so there is no package
        // to restore and nothing may be granted.
        _sut.RecordBaselineHero("some_hero");

        Assert.IsFalse(_sut.TryConsumePossession("another_hero", out _));
    }

    [TestMethod]
    public void TryConsumePossession_HeroUnchanged_DoesNotFire()
    {
        _sut.RecordBaselineHero("cc_hero");
        _sut.CaptureCharacterCreationChoices(Choices());

        Assert.IsFalse(_sut.TryConsumePossession("cc_hero", out _));
    }

    [TestMethod]
    public void TryConsumePossession_CurrentHeroIsTheCharacterCreationHero_DoesNotFire()
    {
        // Baseline recorded late (a client whose OnGameLoaded landed after the hand-off) would leave
        // the baseline pointing at some other hero while we are still on the CC hero. Comparing
        // against BOTH ids is what stops that from reading as a switch.
        _sut.RecordBaselineHero("world_gen_hero");
        _sut.CaptureCharacterCreationChoices(Choices("cc_hero"));

        Assert.IsFalse(_sut.TryConsumePossession("cc_hero", out _));
    }

    [TestMethod]
    public void TryConsumePossession_NoBaselineRecorded_DoesNotFire()
    {
        _sut.CaptureCharacterCreationChoices(Choices());

        Assert.IsFalse(_sut.TryConsumePossession("any_hero", out _));
    }

    // --- Baseline bookkeeping ---

    [TestMethod]
    public void RecordBaselineHero_CalledTwice_KeepsTheFirstValue()
    {
        // OnGameLoaded and OnSessionLaunched both call this, and on a client the hand-off can land
        // between them. Overwriting would adopt the POST-switch hero and hide the switch forever.
        _sut.RecordBaselineHero("original_hero");
        _sut.RecordBaselineHero("post_switch_hero");
        _sut.CaptureCharacterCreationChoices(Choices("original_hero"));

        Assert.IsTrue(_sut.TryConsumePossession("post_switch_hero", out _),
            "the second RecordBaselineHero must not have become the baseline");
    }

    [TestMethod]
    public void RecordBaselineHero_NullOrEmpty_IsIgnored()
    {
        _sut.RecordBaselineHero(null);
        _sut.RecordBaselineHero(string.Empty);
        _sut.RecordBaselineHero("real_hero");
        _sut.CaptureCharacterCreationChoices(Choices("real_hero"));

        Assert.IsTrue(_sut.TryConsumePossession("other_hero", out _));
    }

    [TestMethod]
    public void ResetForNewCampaign_KeepsCharacterCreationChoices()
    {
        // Character creation completes and raises its event BEFORE the joining client's campaign is
        // replaced. Clearing the choices on new-campaign would discard the very data being carried.
        _sut.CaptureCharacterCreationChoices(Choices());
        _sut.ResetForNewCampaign();
        _sut.RecordBaselineHero("world_gen_hero");

        Assert.IsTrue(_sut.TryConsumePossession("joined_hero", out var choices));
        Assert.AreEqual("mirkwood", choices.CultureId);
    }

    [TestMethod]
    public void ResetForNewCampaign_ClearsBaselineSoTheNextCampaignRecordsItsOwn()
    {
        _sut.RecordBaselineHero("first_campaign_hero");
        _sut.ResetForNewCampaign();
        _sut.CaptureCharacterCreationChoices(Choices("second_campaign_hero"));

        // With the baseline cleared and no new one recorded, nothing may fire.
        Assert.IsFalse(_sut.TryConsumePossession("third_hero", out _));
    }
}
