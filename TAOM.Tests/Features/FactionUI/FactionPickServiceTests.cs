using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.FactionUI.Presets;
using TAOM.Features.PlayerSwitcher;
using TAOM.Features.PlayerSwitcher.Domain;
using static TAOM.Adapters.CharacterCreationStageKind;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// Issue #704, Mike 2026-10-01: "When I pick an existing character like Thranduil from the UI, it should
/// skip the character creation all of the way until the career picker, user picks the career and it
/// starts the game." A living hero with a clan whom Player Switcher would hand over is taken over through
/// Player Switcher's own selection and handover, and the stages around the career choice are taken out;
/// every other pick is copied as before (Option A).
/// </summary>
[TestClass]
public class FactionPickServiceTests
{
    private const string FactionCulture = "mirkwood";
    private const string HeroId = "lord_M1_1";

    private static readonly CharacterCreationStageKind?[] VanillaStages =
        { Culture, FaceGenerator, Narrative, BannerEditor, ClanNaming, Review, Options };

    /// <summary>The engine's stage list as the adapter exposes it: removing keeps the stage, appending
    /// adds a kept one at the end, and a stage TAOM has no kind for is a null entry.</summary>
    private sealed class FakeStageList : ICharacterCreationStagesAdapter
    {
        private readonly List<CharacterCreationStageKind?> _stages;
        private readonly HashSet<CharacterCreationStageKind> _kept = new();

        public FakeStageList(params CharacterCreationStageKind?[] stages) => _stages = stages.ToList();

        public CharacterCreationStageKind?[] Stages => _stages.ToArray();

        /// <summary>A kind whose kept stage the engine fails to take back.</summary>
        public CharacterCreationStageKind? RefuseAppend { get; set; }

        public int Count => _stages.Count;

        public int CurrentIndex { get; set; } = 1;

        public bool HoldsRemoved => _kept.Count > 0;

        public bool Has(CharacterCreationStageKind kind) => _stages.Contains(kind);

        public bool Remove(CharacterCreationStageKind kind)
        {
            if (!_stages.Remove(kind))
                return false;
            _kept.Add(kind);
            return true;
        }

        public bool Append(CharacterCreationStageKind kind)
        {
            if (kind == RefuseAppend || !_kept.Remove(kind))
                return false;
            _stages.Add(kind);
            return true;
        }
    }

    private IPresetAppearanceAdapter _appearance = null!;
    private IHeroPickerService _heroes = null!;
    private IPlayerSwitchPolicyProvider _policy = null!;
    private PlayerSwitchSessionStore _session = null!;
    private IPlayerIdentityAdapter _identity = null!;
    private FakeStageList _stages = null!;
    private IModLogger _logger = null!;
    private FactionPresetService _presets = null!;
    private FactionPickService _sut = null!;

    private readonly object _heroSource = new();
    private readonly object _resolvedHero = new();
    private readonly object _templateSource = new();
    private readonly object _resolvedTemplate = new();
    private readonly HeroPickRow _row = new("lord_M1_1", "Thranduil", HeroPickerGroup.RulingHouse, race: 2, isFemale: false, isLeader: true, hasClan: true);

    [TestInitialize]
    public void Setup()
    {
        _appearance = Substitute.For<IPresetAppearanceAdapter>();
        _appearance.Resolve(_heroSource, Arg.Any<bool>()).Returns(_resolvedHero);
        _appearance.Resolve(_templateSource, false).Returns(_resolvedTemplate);
        _appearance.CaptureLook().Returns(new object());
        _presets = new FactionPresetService(_appearance);

        _heroes = Substitute.For<IHeroPickerService>();
        _heroes.FindTakeover(HeroId, FactionCulture, Arg.Any<PlayerSwitchPolicy>()).Returns(_row);
        _policy = Substitute.For<IPlayerSwitchPolicyProvider>();
        _policy.Current.Returns(PlayerSwitchPolicy.Default);
        _session = new PlayerSwitchSessionStore();
        _identity = Substitute.For<IPlayerIdentityAdapter>();
        _identity.CanReassignPlayerClan.Returns(true);
        _identity.StartupClanIsDisposable.Returns(true);
        _identity.IsSwitchable(HeroId).Returns(true);
        _stages = new FakeStageList(VanillaStages);
        _logger = Substitute.For<IModLogger>();

        _sut = new FactionPickService(_presets, new Lazy<IHeroPickerService>(() => _heroes), _policy, _session, _identity, _logger);
    }

    // A named card's character (Thranduil's own CharacterObject), resolved to the hero behind it.
    private RosterEntry Card(string? heroId = HeroId) => new(_heroSource, "lord_M1_1", "Thranduil", isHero: false, heroId: heroId);

    // A lord from the browse lists: the hero itself.
    private RosterEntry Lord() => new(_heroSource, HeroId, "Thranduil", isHero: true, heroId: HeroId);

    private RosterEntry Legend() => new(_templateSource, "taom_fui_tauriel", "Tauriel", isHero: false);

    // ---------- Which picks are taken over ----------

    [TestMethod]
    public void Pick_ALivingHeroPlayerSwitcherWouldHandOver_SelectsHimForTheHandover()
    {
        _sut.Pick(Card(), FactionCulture);

        Assert.AreEqual(HeroId, _session.SelectedHeroId, "Player Switcher's handover reads this at 1100");
        Assert.IsTrue(_sut.IsTakeover);
    }

    [TestMethod]
    public void Pick_ALordFromTheBrowseLists_IsTakenOverToo()
    {
        _sut.Pick(Lord(), FactionCulture);

        Assert.AreEqual(HeroId, _session.SelectedHeroId);
    }

    [TestMethod]
    public void Pick_AsksWithTheFactionsCultureAndTheLivePolicy()
    {
        var policy = new PlayerSwitchPolicy(true, true, allowLoreLockedHeroes: true, false);
        _policy.Current.Returns(policy);

        _sut.Pick(Card(), FactionCulture);

        _heroes.Received(1).FindTakeover(HeroId, FactionCulture, policy);
    }

    [TestMethod]
    public void OnCharacterCreationFinalize_ATakeover_CopiesNothingAndForgetsThePick()
    {
        // Player Switcher's handover at 1100 makes the player that hero; copying onto the character it
        // then removes would be wasted, and skills copied onto it could raise notifications.
        _sut.Pick(Card(), FactionCulture);
        _sut.OnCultureStageCompleted(_stages);

        _sut.OnCharacterCreationFinalize();

        _appearance.DidNotReceiveWithAnyArgs().ApplyIdentity(default!);
        _appearance.Received(1).ApplyLook(_resolvedHero);
        Assert.IsFalse(_presets.HasPick);
    }

    [TestMethod]
    public void OnCharacterCreationFinalize_ATakeover_LeavesPlayerSwitchersSelectionForTheHandover()
    {
        // 1060 runs before 1100: clearing the selection here would cancel the handover.
        _sut.Pick(Card(), FactionCulture);

        _sut.OnCharacterCreationFinalize();

        Assert.AreEqual(HeroId, _session.SelectedHeroId);
    }

    [TestMethod]
    public void OnCharacterCreationFinalize_ACopiedLord_GetsHisNameAndSkills()
    {
        _heroes.FindTakeover(HeroId, FactionCulture, Arg.Any<PlayerSwitchPolicy>()).Returns(default(HeroPickRow));
        _sut.Pick(Lord(), FactionCulture);

        _sut.OnCharacterCreationFinalize();

        _appearance.Received(1).ApplyLook(_resolvedHero);
        _appearance.Received(1).ApplyIdentity(_resolvedHero);
    }

    [TestMethod]
    public void Pick_ACharacterWithNoHeroBehindIt_IsCopiedAsBefore()
    {
        _sut.Pick(Legend(), FactionCulture);

        Assert.IsFalse(_sut.IsTakeover);
        Assert.IsFalse(_session.HasSelection);
        _heroes.DidNotReceiveWithAnyArgs().FindTakeover(default!, default!, default);
        _sut.OnCharacterCreationFinalize();
        _appearance.Received(1).ApplyIdentity(_resolvedTemplate);
    }

    [TestMethod]
    public void Pick_AHeroPlayerSwitchersRulesRefuse_IsCopiedWithHisName()
    {
        // Sauron with the opt-in off, a hero of another culture, a clanless companion, Player Switcher off.
        _heroes.FindTakeover(HeroId, FactionCulture, Arg.Any<PlayerSwitchPolicy>()).Returns(default(HeroPickRow));

        _sut.Pick(Lord(), FactionCulture);

        Assert.IsFalse(_sut.IsTakeover);
        Assert.IsFalse(_session.HasSelection);
        _sut.OnCharacterCreationFinalize();
        _appearance.Received(1).Resolve(_heroSource, true);
        _appearance.Received(1).ApplyIdentity(_resolvedHero);
    }

    [TestMethod]
    public void Pick_ARefusedCard_IsCopiedWithoutAName()
    {
        _heroes.FindTakeover(HeroId, FactionCulture, Arg.Any<PlayerSwitchPolicy>()).Returns(default(HeroPickRow));

        _sut.Pick(Card(), FactionCulture);

        _appearance.Received(1).Resolve(_heroSource, false);
    }

    [TestMethod]
    public void Pick_WhenThePlayerClanCannotBeMoved_IsCopiedInstead()
    {
        // The handover would refuse at the end, after the stages it needs were skipped.
        _identity.CanReassignPlayerClan.Returns(false);

        _sut.Pick(Card(), FactionCulture);

        Assert.IsFalse(_sut.IsTakeover);
        Assert.IsFalse(_session.HasSelection);
    }

    [TestMethod]
    public void Pick_WhenTheCreationClanHoldsAnotherLord_IsCopiedInstead()
    {
        // StoryMode seeds an elder brother; the handover refuses rather than strand the clan.
        _identity.StartupClanIsDisposable.Returns(false);

        _sut.Pick(Card(), FactionCulture);

        Assert.IsFalse(_sut.IsTakeover);
        Assert.IsFalse(_session.HasSelection);
    }

    [TestMethod]
    public void Pick_WhenTheHeroIsNotSwitchable_IsCopiedInstead()
    {
        _identity.IsSwitchable(HeroId).Returns(false);

        _sut.Pick(Card(), FactionCulture);

        Assert.IsFalse(_sut.IsTakeover);
        Assert.IsFalse(_session.HasSelection);
    }

    [TestMethod]
    public void Pick_WithNoCulture_IsCopiedWithoutAskingPlayerSwitcher()
    {
        _sut.Pick(Card(), null);

        Assert.IsFalse(_sut.IsTakeover);
        _heroes.DidNotReceiveWithAnyArgs().FindTakeover(default!, default!, default);
    }

    [TestMethod]
    public void Pick_WhenTheTakeoverCheckThrows_IsCopiedAndTheErrorLogged()
    {
        // A click must never throw out of the faction screen: the check walks the whole campaign.
        _heroes.FindTakeover(HeroId, FactionCulture, Arg.Any<PlayerSwitchPolicy>()).Throws(new InvalidOperationException("boom"));

        _sut.Pick(Card(), FactionCulture);

        Assert.IsFalse(_sut.IsTakeover);
        Assert.IsFalse(_session.HasSelection);
        Assert.IsTrue(_presets.HasPick, "the pick falls back to a copy");
        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("boom")));
    }

    [TestMethod]
    public void Pick_WhenResolvingThePickThrows_LeavesNoPickAndNoSelection()
    {
        _appearance.Resolve(_heroSource, Arg.Any<bool>()).Throws(new InvalidOperationException("broken hero"));

        _sut.Pick(Card(), FactionCulture);

        Assert.IsFalse(_sut.IsTakeover);
        Assert.IsFalse(_session.HasSelection, "a half-made takeover must not reach the handover");
        Assert.IsFalse(_presets.HasPick);
        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("broken hero")));
    }

    [TestMethod]
    public void Pick_WhenPuttingBackTheEarlierLookThrows_StillDoesNotThrow()
    {
        // The pick's own Clear restores the look from before an earlier pick; if that throws, the catch's
        // Clear must not throw it again out of the click.
        _sut.Pick(Card(), FactionCulture);
        _sut.OnCultureStageCompleted(_stages);
        _appearance.When(a => a.RestoreLook(Arg.Any<object>())).Do(_ => throw new InvalidOperationException("restore failed"));

        _sut.Pick(Legend(), FactionCulture);

        Assert.IsFalse(_sut.IsTakeover);
        Assert.IsFalse(_session.HasSelection);
        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("restore failed")));
    }

    [TestMethod]
    public void Pick_ACopyAfterATakeover_DropsPlayerSwitchersSelection()
    {
        _sut.Pick(Card(), FactionCulture);

        _sut.Pick(Legend(), FactionCulture);

        Assert.IsFalse(_session.HasSelection);
        Assert.IsFalse(_sut.IsTakeover);
    }

    // ---------- Dropping a pick ----------

    [TestMethod]
    public void Clear_AfterATakeover_DropsPlayerSwitchersSelection()
    {
        _sut.Pick(Card(), FactionCulture);

        _sut.Clear();

        Assert.IsFalse(_session.HasSelection);
        Assert.IsFalse(_sut.IsTakeover);
    }

    [TestMethod]
    public void Clear_DropsASelectionThisServiceHasNoRecordOf()
    {
        // While the faction screen is the picker nothing else selects, so whatever Player Switcher holds
        // is a faction-screen pick, however the record of it was lost.
        _session.Select(_row);

        _sut.Clear();

        Assert.IsFalse(_session.HasSelection);
    }

    [TestMethod]
    public void Clear_AfterTheTakeoversLookWasShown_PutsTheLookBack()
    {
        var before = new object();
        _appearance.CaptureLook().Returns(before);
        _sut.Pick(Card(), FactionCulture);
        _sut.OnCultureStageCompleted(_stages);

        _sut.Clear();

        _appearance.Received(1).RestoreLook(before);
    }

    [TestMethod]
    public void Clear_KeepsTheStagesSkipped_UntilTheNextConfirmDecides()
    {
        // Back on the faction screen the list is still short; the next culture-stage completion restores it.
        _sut.Pick(Card(), FactionCulture);
        _sut.OnCultureStageCompleted(_stages);

        _sut.Clear();
        _sut.OnCultureStageCompleted(_stages);

        CollectionAssert.AreEqual(VanillaStages, _stages.Stages);
    }

    [TestMethod]
    public void ResetForNewCharacterCreation_ForgetsTheTakeoverAndThePick()
    {
        _sut.Pick(Card(), FactionCulture);

        _sut.ResetForNewCharacterCreation();

        Assert.IsFalse(_sut.IsTakeover);
        Assert.IsFalse(_session.HasSelection);
        Assert.IsFalse(_presets.HasPick);
        _appearance.DidNotReceiveWithAnyArgs().RestoreLook(default!);
    }

    [TestMethod]
    public void ResetForNewCharacterCreation_ForgetsThatAnotherCreationsStagesWereSkipped()
    {
        // Quitting from the career menu leaves that creation's list short; the next creation's list is
        // whole and must not be "put back".
        _sut.Pick(Card(), FactionCulture);
        _sut.OnCultureStageCompleted(_stages);

        _sut.ResetForNewCharacterCreation();
        var next = new FakeStageList(VanillaStages);
        _sut.OnCultureStageCompleted(next);

        CollectionAssert.AreEqual(VanillaStages, next.Stages);
    }

    // ---------- The culture stage completes ----------

    [TestMethod]
    public void OnCultureStageCompleted_ATakeover_LeavesOnlyTheCultureAndNarrativeStages()
    {
        _sut.Pick(Card(), FactionCulture);

        _sut.OnCultureStageCompleted(_stages);

        CollectionAssert.AreEqual(new CharacterCreationStageKind?[] { Culture, Narrative }, _stages.Stages,
            "after the faction screen only the backstory and career choice stage, and after it the campaign");
    }

    [TestMethod]
    public void OnCultureStageCompleted_ATakeover_ShowsTheHerosLookOnTheCareerMenu()
    {
        _sut.Pick(Card(), FactionCulture);

        _sut.OnCultureStageCompleted(_stages);

        _appearance.Received(1).ApplyLook(_resolvedHero);
        _logger.Received().LogInfo(Arg.Is<string>(m => m.Contains("Thranduil") && m.Contains("career")));
    }

    [TestMethod]
    public void OnCultureStageCompleted_ACopyAfterATakeover_PutsEveryStageBackInTheEnginesOrder()
    {
        // The player went back from the career menu and chose a legend (or Custom Character).
        _sut.Pick(Card(), FactionCulture);
        _sut.OnCultureStageCompleted(_stages);
        _sut.Pick(Legend(), FactionCulture);

        _sut.OnCultureStageCompleted(_stages);

        CollectionAssert.AreEqual(VanillaStages, _stages.Stages);
        Assert.IsTrue(_presets.HasPick, "the legend is copied when the face generator opens");
    }

    [TestMethod]
    public void OnCultureStageCompleted_ATakeoverConfirmedAgain_StaysShort()
    {
        _sut.Pick(Card(), FactionCulture);
        _sut.OnCultureStageCompleted(_stages);
        _sut.Pick(Lord(), FactionCulture);

        _sut.OnCultureStageCompleted(_stages);

        CollectionAssert.AreEqual(new CharacterCreationStageKind?[] { Culture, Narrative }, _stages.Stages);
    }

    [TestMethod]
    public void OnCultureStageCompleted_ACopyWithNothingSkipped_LeavesTheListAlone()
    {
        _sut.Pick(Legend(), FactionCulture);

        _sut.OnCultureStageCompleted(_stages);

        CollectionAssert.AreEqual(VanillaStages, _stages.Stages);
        _appearance.DidNotReceiveWithAnyArgs().ApplyLook(default!);
    }

    [TestMethod]
    public void OnCultureStageCompleted_AStageTaomDoesNotKnow_LeavesTheListAloneAndKeepsTheTakeover()
    {
        // Another mod's stage: every stage stays, the face generator shows the hero and the handover still
        // runs at the end.
        var withAModStage = new FakeStageList(Culture, FaceGenerator, Narrative, null, BannerEditor, ClanNaming, Review, Options);
        _sut.Pick(Card(), FactionCulture);

        _sut.OnCultureStageCompleted(withAModStage);

        Assert.AreEqual(8, withAModStage.Count);
        Assert.IsTrue(_sut.IsTakeover);
        Assert.IsTrue(_session.HasSelection);
        _appearance.DidNotReceiveWithAnyArgs().ApplyLook(default!);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("Thranduil")));
    }

    [TestMethod]
    public void OnCultureStageCompleted_SevenStagesWithAModsInPlaceOfOne_AreLeftAlone()
    {
        // Seven stages with the culture stage first, but not vanilla's seven: appending could not rebuild it.
        var replaced = new FakeStageList(Culture, FaceGenerator, Narrative, null, ClanNaming, Review, Options);
        _sut.Pick(Card(), FactionCulture);

        _sut.OnCultureStageCompleted(replaced);

        CollectionAssert.AreEqual(new CharacterCreationStageKind?[] { Culture, FaceGenerator, Narrative, null, ClanNaming, Review, Options }, replaced.Stages);
        Assert.IsTrue(_sut.IsTakeover);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("Thranduil")));
    }

    [TestMethod]
    public void OnCultureStageCompleted_AStageTheEngineDoesNotTakeBack_IsReported()
    {
        _sut.Pick(Card(), FactionCulture);
        _sut.OnCultureStageCompleted(_stages);
        _stages.RefuseAppend = Review;
        _sut.Pick(Legend(), FactionCulture);

        _sut.OnCultureStageCompleted(_stages);

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("Review")));
    }

    [TestMethod]
    public void OnCultureStageCompleted_StoryModesFiveStages_AreLeftAlone()
    {
        var storyMode = new FakeStageList(Culture, FaceGenerator, Narrative, Review, Options);
        _sut.Pick(Card(), FactionCulture);

        _sut.OnCultureStageCompleted(storyMode);

        CollectionAssert.AreEqual(new CharacterCreationStageKind?[] { Culture, FaceGenerator, Narrative, Review, Options }, storyMode.Stages);
    }

    [TestMethod]
    public void OnCultureStageCompleted_WhenTheCultureStageIsNotFirst_LeavesTheListAlone()
    {
        // The engine opens the stage at its index next; past index 1 a short list would end creation at once.
        _stages.CurrentIndex = 2;
        _sut.Pick(Card(), FactionCulture);

        _sut.OnCultureStageCompleted(_stages);

        CollectionAssert.AreEqual(VanillaStages, _stages.Stages);
    }
}
