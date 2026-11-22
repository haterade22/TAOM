using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Features.WarChronicle.Events;

namespace TAOM.Tests.Features.WarChronicle.Events;

/// <summary>
/// A Narrative event with a story about a hero (#1105 slice 3: the Breaking of the Fellowship and Boromir). When a story mission can run, the event opens a
/// window and its result is the hero's life: Averted (he lives) or Occurred (he dies in battle). With no answer by the close day, and when the player lets
/// fate decide, he dies. Where no story mission can run (co-op, the offers off, a hero who is dead or is the player) the event occurs at once as text, as before.
/// </summary>
[TestClass]
public class ChronicleHeroStoryTests
{
    private const string Hero = "lord_1_75";

    private ChronicleFixture _f = null!;
    private ChronicleService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _f = new ChronicleFixture();
        _f.World.AliveHeroes.Add(Hero);
        _sut = _f.CreateService();
    }

    private static ChronicleEventDefinition Breaking()
    {
        var definition = ChronicleFixture.Narrative("breaking", ChronicleAnchor.Phase1, 0);
        definition.WindowDays = 10;
        definition.Defender = "empire_w";
        definition.Attackers = new[] { "isengard" };
        definition.Story = new ChronicleStoryDefinition { MissionId = "parth_galen_save", AttackMissionId = "parth_galen_kill", HeroId = Hero };
        definition.Averted = new ChronicleOutcomeDefinition
        {
            Text = "{=taom_wc_breaking_averted}he lives",
            Effects = new[] { new ChronicleEffectDefinition { Target = "empire_w", Kind = WarEffectKind.VolunteerRate, Magnitude = 0.1f, DurationDays = 15 } },
        };
        return definition;
    }

    // The chronicle counts a Phase1 event from the day it sees the Isengard War as the current phase, and the fixture's effective day for that phase is 30.
    private void ReachTheDay(ChronicleEventDefinition definition)
    {
        _f.Phase = TAOM.Features.Diplomacy.Models.WarPhase.IsengardWar;
        _f.Events.Add(definition);
        _f.World.Day = 0;
        _sut.LaunchChronicle();
        _f.World.Day = 30;
        _sut.RunChronicleDay();
    }

    private List<string?> DrainTexts()
    {
        var texts = new List<string?>();
        _f.Presenter.When(p => p.Show(Arg.Any<ChronicleAnnouncement>())).Do(c => texts.Add(c.Arg<ChronicleAnnouncement>().Text));
        while (_f.Queue.Count > 0)
            _f.Queue.TryShowNext();
        return texts;
    }

    [TestMethod]
    public void OnItsDay_TheEventOpensAWindow_WithNoAnnouncement_AndOffersBothMissions()
    {
        ReachTheDay(Breaking());

        Assert.AreEqual(ChronicleEventState.Active, _f.StateOf("breaking"), string.Join(" | ", _f.Logged.Concat(_f.Warnings)));
        Assert.AreEqual(40d, _f.Store.Get("breaking")!.CloseDay);
        Assert.AreEqual(0, _f.Queue.Count, "the offer itself tells the player; an opening line would hold the offer back");
        var offer = _sut.GetStoryOffers().Single();
        Assert.AreEqual("breaking", offer.EventId);
        Assert.AreEqual(Hero, offer.HeroId);
        Assert.AreEqual("parth_galen_save", offer.MissionId);
        Assert.AreEqual("parth_galen_kill", offer.AttackMissionId);
        Assert.AreEqual("empire_w", offer.Defender);
        CollectionAssert.AreEqual(new[] { "isengard" }, offer.Attackers.ToArray());
        Assert.IsNull(offer.TargetSettlementId);
        Assert.AreEqual(0, _f.World.KillsAsked.Count);
    }

    [TestMethod]
    public void InACoopSession_TheEventOccursAtOnceAsText_AndNobodyIsKilled()
    {
        _f.Coop.IsSessionActive.Returns(true);

        ReachTheDay(Breaking());

        Assert.AreEqual(ChronicleEventState.Occurred, _f.StateOf("breaking"));
        Assert.AreEqual(0, _f.World.KillsAsked.Count);
    }

    [TestMethod]
    public void WithTheOffersOff_TheEventOccursAtOnceAsText()
    {
        _f.Settings.StoryOffersEnabled.Returns(false);

        ReachTheDay(Breaking());

        Assert.AreEqual(ChronicleEventState.Occurred, _f.StateOf("breaking"));
        Assert.AreEqual(0, _f.World.KillsAsked.Count);
    }

    [TestMethod]
    public void AHeroWhoIsDead_OrIsThePlayer_GetsNoWindow_AndNoKill()
    {
        _f.World.AliveHeroes.Clear();
        ReachTheDay(Breaking());
        Assert.AreEqual(ChronicleEventState.Occurred, _f.StateOf("breaking"));

        var second = new ChronicleFixture();
        second.World.AliveHeroes.Add(Hero);
        second.World.PlayerHero = Hero;
        var sut = second.CreateService();
        second.Phase = TAOM.Features.Diplomacy.Models.WarPhase.IsengardWar;
        second.Events.Add(Breaking());
        second.World.Day = 0;
        sut.LaunchChronicle();
        second.World.Day = 30;
        sut.RunChronicleDay();

        Assert.AreEqual(ChronicleEventState.Occurred, second.StateOf("breaking"));
        Assert.AreEqual(0, _f.World.KillsAsked.Count + second.World.KillsAsked.Count);
    }

    [TestMethod]
    public void ANarrativeWithNoStory_OccursAtOnce_AsBefore()
    {
        var plain = ChronicleFixture.Narrative("breaking", ChronicleAnchor.Phase1, 0);

        ReachTheDay(plain);

        Assert.AreEqual(ChronicleEventState.Occurred, _f.StateOf("breaking"));
    }

    [TestMethod]
    public void TheHeroLives_TheEventIsAverted_WithItsOwnLineAndEffects_AndNoKill()
    {
        ReachTheDay(Breaking());

        Assert.AreEqual(ChronicleHeroStoryResult.Averted, _sut.ResolveHeroStory("breaking", heroLives: true, "the player won"));

        Assert.AreEqual(ChronicleEventState.Averted, _f.StateOf("breaking"));
        Assert.AreEqual(0, _f.World.KillsAsked.Count);
        StringAssert.Contains(DrainTexts().Single(), "taom_wc_breaking_averted");
        Assert.IsTrue(_f.Effects.GetMultiplier("empire_w", WarEffectKind.VolunteerRate) > 1f, "the averted outcome pays its own effects");
        Assert.AreEqual(0, _sut.GetStoryOffers().Count);
    }

    [TestMethod]
    public void TheHeroDies_TheEventOccurs_AndHeIsKilledOnce()
    {
        ReachTheDay(Breaking());

        Assert.AreEqual(ChronicleHeroStoryResult.Occurred, _sut.ResolveHeroStory("breaking", heroLives: false, "the player lost"));

        Assert.AreEqual(ChronicleEventState.Occurred, _f.StateOf("breaking"));
        CollectionAssert.AreEqual(new[] { Hero }, _f.World.KillsAsked);
        StringAssert.Contains(DrainTexts().Single(), "taom_wc_breaking_occurred");
        Assert.AreEqual(ChronicleHeroStoryResult.NoWindow, _sut.ResolveHeroStory("breaking", heroLives: false, "again"), "a resolved window takes no second result");
        Assert.AreEqual(1, _f.World.KillsAsked.Count);
    }

    [TestMethod]
    public void LetFateDecide_ResolvesAtOnce_AndTheHeroDies()
    {
        ReachTheDay(Breaking());

        Assert.IsTrue(_sut.DeclineStory("breaking"));

        Assert.AreEqual(ChronicleEventState.Occurred, _f.StateOf("breaking"));
        CollectionAssert.AreEqual(new[] { Hero }, _f.World.KillsAsked);
    }

    [TestMethod]
    public void WithNoAnswerByTheCloseDay_TheHeroDies()
    {
        ReachTheDay(Breaking());

        _f.World.Day = 39;
        _sut.RunChronicleDay();
        Assert.AreEqual(ChronicleEventState.Active, _f.StateOf("breaking"), "the window is still open the day before it closes");
        _f.World.Day = 40;
        _sut.RunChronicleDay();

        Assert.AreEqual(ChronicleEventState.Occurred, _f.StateOf("breaking"));
        CollectionAssert.AreEqual(new[] { Hero }, _f.World.KillsAsked);
    }

    [TestMethod]
    public void AHeroWhoDiesInTheCampaignDuringTheWindow_ClosesItAsOccurred_WithNoKill()
    {
        ReachTheDay(Breaking());
        _f.World.AliveHeroes.Remove(Hero);
        Assert.AreEqual(0, _sut.GetStoryOffers().Count, "no offer for a hero who is dead");

        _f.World.Day = 31;
        _sut.RunChronicleDay();

        Assert.AreEqual(ChronicleEventState.Occurred, _f.StateOf("breaking"));
        Assert.AreEqual(0, _f.World.KillsAsked.Count);
    }

    [TestMethod]
    public void ACoopSessionThatStartsDuringTheWindow_ClosesItAsText_WithNoKill()
    {
        ReachTheDay(Breaking());
        _f.Coop.IsSessionActive.Returns(true);

        _f.World.Day = 40;
        _sut.RunChronicleDay();

        Assert.AreEqual(ChronicleEventState.Occurred, _f.StateOf("breaking"));
        Assert.AreEqual(0, _f.World.KillsAsked.Count);
    }

    // Deferred: his party is in a battle or besieges, so the engine only marks him (KillCharacterAction.cs:51). Refused: a quest or an issue
    // vetoes the death (:34, Hero.CanDie). Neither is a death, so the event must not say that he died.
    [DataTestMethod]
    [DataRow(ChronicleHeroDeath.Deferred)]
    [DataRow(ChronicleHeroDeath.Refused)]
    public void AKillTheEngineDoesNotFinish_KeepsTheEventOpen_AnnouncesNothing_AndOffersNoMission(ChronicleHeroDeath answer)
    {
        _f.World.DeathAnswer = answer;
        ReachTheDay(Breaking());

        Assert.AreEqual(ChronicleHeroStoryResult.DeathOwed, _sut.ResolveHeroStory("breaking", heroLives: false, "the player lost"));

        Assert.AreEqual(ChronicleEventState.Active, _f.StateOf("breaking"), "the event does not occur while he lives");
        Assert.AreEqual(30d, _f.Store.Get("breaking")!.CloseDay, "the window closes on the day his death was decided");
        Assert.AreEqual(0, DrainTexts().Count, "no death is announced while he lives");
        Assert.AreEqual(0, _sut.GetStoryOffers().Count, "a decided story offers no second mission");
        Assert.IsTrue(_f.Logged.Any(l => l.Contains("breaking") && l.Contains(answer.ToString())), "the log says the death is owed");
    }

    [TestMethod]
    public void AnOwedDeath_IsAskedAgainEachDay_AndTheEventOccursWhenHeDies()
    {
        _f.World.DeathAnswer = ChronicleHeroDeath.Refused;
        ReachTheDay(Breaking());
        _sut.ResolveHeroStory("breaking", heroLives: false, "the player lost");

        _f.World.Day = 31;
        _sut.RunChronicleDay();
        Assert.AreEqual(ChronicleEventState.Active, _f.StateOf("breaking"), "still refused");
        Assert.AreEqual(2, _f.World.KillsAsked.Count);

        _f.World.DeathAnswer = ChronicleHeroDeath.Killed;
        _f.World.Day = 32;
        _sut.RunChronicleDay();

        Assert.AreEqual(ChronicleEventState.Occurred, _f.StateOf("breaking"));
        Assert.AreEqual(3, _f.World.KillsAsked.Count);
        StringAssert.Contains(DrainTexts().Single(), "taom_wc_breaking_occurred");
    }

    [TestMethod]
    public void AnOwedDeathTheEngineFinishesItself_ClosesTheEventAsOccurred_WithNoSecondKill()
    {
        // The end of his battle completes the engine's own mark (MapEventSide.cs:431-437).
        _f.World.DeathAnswer = ChronicleHeroDeath.Deferred;
        ReachTheDay(Breaking());
        _sut.ResolveHeroStory("breaking", heroLives: false, "the player lost");
        _f.World.AliveHeroes.Remove(Hero);

        _f.World.Day = 31;
        _sut.RunChronicleDay();

        Assert.AreEqual(ChronicleEventState.Occurred, _f.StateOf("breaking"));
        Assert.AreEqual(1, _f.World.KillsAsked.Count);
    }

    [TestMethod]
    public void LetFateDecide_WithAKillTheEngineDefers_WaitsToo()
    {
        _f.World.DeathAnswer = ChronicleHeroDeath.Deferred;
        ReachTheDay(Breaking());

        Assert.IsTrue(_sut.DeclineStory("breaking"), "the choice was taken");

        Assert.AreEqual(ChronicleEventState.Active, _f.StateOf("breaking"));
        Assert.AreEqual(0, _sut.GetStoryOffers().Count);
    }

    [TestMethod]
    public void TheCloseDay_WithAKillTheEngineDefers_WaitsToo_AndOffersNoMissionAfterIt()
    {
        _f.World.DeathAnswer = ChronicleHeroDeath.Deferred;
        ReachTheDay(Breaking());

        _f.World.Day = 40;
        _sut.RunChronicleDay();

        Assert.AreEqual(ChronicleEventState.Active, _f.StateOf("breaking"));
        Assert.AreEqual(40d, _f.Store.Get("breaking")!.CloseDay, "a close day already reached is kept");
        Assert.AreEqual(0, _sut.GetStoryOffers().Count);
    }

    [TestMethod]
    public void AnOwedDeath_WhereTheStoryNoLongerMatters_EndsAsText_WithNoSecondKill()
    {
        _f.World.DeathAnswer = ChronicleHeroDeath.Refused;
        ReachTheDay(Breaking());
        _sut.ResolveHeroStory("breaking", heroLives: false, "the player lost");
        _f.Coop.IsSessionActive.Returns(true);

        _f.World.Day = 31;
        _sut.RunChronicleDay();

        Assert.AreEqual(ChronicleEventState.Occurred, _f.StateOf("breaking"));
        Assert.AreEqual(1, _f.World.KillsAsked.Count);
    }

    [TestMethod]
    public void TheOffersTurnedOffDuringTheWindow_EndItAsText_WithNoOfferAndNoKill()
    {
        ReachTheDay(Breaking());
        _f.Settings.StoryOffersEnabled.Returns(false);
        Assert.AreEqual(0, _sut.GetStoryOffers().Count, "no offer while the offers are off");

        _f.World.Day = 40;
        _sut.RunChronicleDay();

        Assert.AreEqual(ChronicleEventState.Occurred, _f.StateOf("breaking"));
        Assert.AreEqual(0, _f.World.KillsAsked.Count, "a campaign with the offers off keeps the event as text, whenever they were turned off");
    }

    [TestMethod]
    public void APlayerWhoBecomesTheHeroDuringTheWindow_GetsNoOffer_AndIsNotKilled()
    {
        ReachTheDay(Breaking());
        _f.World.PlayerHero = Hero;
        Assert.AreEqual(0, _sut.GetStoryOffers().Count, "nobody is offered a fight against himself");

        _f.World.Day = 40;
        _sut.RunChronicleDay();

        Assert.AreEqual(ChronicleEventState.Occurred, _f.StateOf("breaking"));
        Assert.AreEqual(0, _f.World.KillsAsked.Count);
    }

    [TestMethod]
    public void TheDryRunOfTheConsoleOpen_SaysTheHeroStoryOpensAWindow()
    {
        _f.Phase = TAOM.Features.Diplomacy.Models.WarPhase.IsengardWar;
        _f.Events.Add(Breaking());
        _f.World.Day = 0;
        _sut.LaunchChronicle();

        Assert.IsTrue(_sut.OpenChronicleEventNow("breaking", dryRun: true, out var reason), reason);

        StringAssert.Contains(reason, "would open its window until day 10");
        Assert.AreEqual(ChronicleEventState.Pending, _f.StateOf("breaking"));
    }

    [TestMethod]
    public void TheDryRunOfTheConsoleOpen_SaysAHeroStoryThatCannotOpen_OccursNow()
    {
        _f.Settings.StoryOffersEnabled.Returns(false);
        _f.Phase = TAOM.Features.Diplomacy.Models.WarPhase.IsengardWar;
        _f.Events.Add(Breaking());
        _f.World.Day = 0;
        _sut.LaunchChronicle();

        Assert.IsTrue(_sut.OpenChronicleEventNow("breaking", dryRun: true, out var reason), reason);

        StringAssert.Contains(reason, "would occur now");
    }

    [TestMethod]
    public void TheFortressCallsOfThePort_RefuseAHeroWindow()
    {
        ReachTheDay(Breaking());

        Assert.IsFalse(_sut.ResolveStoryHeld("breaking", "wrong call"));
        Assert.IsFalse(_sut.ConfirmStoryWindow("breaking"));
        Assert.AreEqual(ChronicleEventState.Active, _f.StateOf("breaking"));
    }

    [TestMethod]
    public void AHeroResult_OnAFortressWindow_IsRefused()
    {
        var siege = ChronicleFixture.SiegeDef();
        siege.Story = new ChronicleStoryDefinition { MissionId = "helms_deep_defend" };
        _f.Phase = TAOM.Features.Diplomacy.Models.WarPhase.FullWar;
        _f.Events.Add(siege);
        _f.World.Day = 0;
        _sut.LaunchChronicle();
        _f.World.Day = 49;
        _sut.RunChronicleDay();

        Assert.AreEqual(ChronicleHeroStoryResult.NoWindow, _sut.ResolveHeroStory("hornburg", heroLives: true, "wrong call"));
        Assert.AreEqual(ChronicleEventState.Active, _f.StateOf("hornburg"));
    }

    [TestMethod]
    public void TheWarsEnd_SkipsAnOpenHeroWindow_WithNoKill()
    {
        ReachTheDay(Breaking());
        _f.Phase = TAOM.Features.Diplomacy.Models.WarPhase.WarEnded;

        _f.World.Day = 31;
        _sut.RunChronicleDay();

        Assert.AreEqual(ChronicleEventState.Skipped, _f.StateOf("breaking"));
        Assert.AreEqual(0, _f.World.KillsAsked.Count);
    }
}
