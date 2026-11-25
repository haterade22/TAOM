using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.CoopInterop;
using TAOM.Features.CoopInterop.CareerSync;

namespace TAOM.Tests.Features.CoopInterop.CareerSync;

// The co-op server's side of career sync: it decides and stores, and says what goes to the sender and to the others.
[TestClass]
public class CareerSyncHostTests
{
    private const string Hero = "Player1";
    private CareerDataService _data = null!;
    private ICareerRegistry _registry = null!;
    private ICareerPassiveService _passives = null!;
    private ICareerSyncHeroAdapter _heroes = null!;
    private TaomSettings _settings = null!;
    private IModLogger _logger = null!;
    private CareerSyncHost _host = null!;
    private readonly List<CareerSyncAnswer> _raised = new();

    private static readonly CareerDefinition Ranger = new(
        id: "ranger", displayName: "Ranger", description: "", portraitSprite: "", abilityTemplateId: "volley",
        minClanTier: 0, rootChoiceId: "ranger_root", eligibleCultureIds: new List<string>(), choiceGroupIds: new List<string>());

    [TestInitialize]
    public void Setup()
    {
        _data = new CareerDataService();
        _registry = Substitute.For<ICareerRegistry>();
        _registry.GetCareer("ranger").Returns(Ranger);
        _registry.GetMaxChoicesForHero(Ranger, Arg.Any<int>()).Returns(5);
        _registry.IsEligible("ranger", Arg.Any<ICareerHeroAdapter>()).Returns(true);
        _passives = Substitute.For<ICareerPassiveService>();
        _heroes = Substitute.For<ICareerSyncHeroAdapter>();
        var player = Substitute.For<ICareerHeroAdapter>();
        player.Level.Returns(10);
        _heroes.Find(Hero).Returns(player);
        _settings = new TaomSettings();
        _logger = Substitute.For<IModLogger>();
        var quests = Substitute.For<ICareerQuestConfigProvider>();
        quests.LoadQuests().Returns(new List<CareerQuestDefinition>());
        _host = new CareerSyncHost(_data, _registry, _passives, quests, _heroes, new CoopSettingsProvider(_settings), _logger);
        _raised.Clear();
        _host.StoredCareerChanged += _raised.Add;
    }

    private static CareerSyncSnapshot Ranger1(string hero = Hero) =>
        new(hero, "ranger", new[] { "ranger_root" }, Array.Empty<int>(), Array.Empty<string>());

    private static CareerSyncSnapshot Unknown() =>
        new(Hero, "no_such_career", new[] { "x" }, Array.Empty<int>(), Array.Empty<string>());

    private void StoreOnServer(string career)
    {
        _data.SetCareer(Hero, career);
        _data.TryAddChoice(Hero, career + "_root", 5);
        _raised.Clear();
    }

    [TestMethod]
    public void Handle_NoSenderHero_IsRefused_AndNothingIsStoredOrSent()
    {
        var outcome = _host.Handle("", Ranger1(), 1, joinReport: false);

        Assert.AreEqual(CareerSyncRefusal.NotYourHero, outcome.Refusal);
        Assert.IsNull(outcome.ToSender);
        Assert.IsNull(outcome.ToOthers);
        Assert.IsFalse(_data.HasCareer(Hero));
    }

    [TestMethod]
    public void Handle_AnotherHerosCareer_IsRefused_AndNotStored()
    {
        // The hero comes from the sender's connection; the id in the report is only compared with it.
        var outcome = _host.Handle("Player2", Ranger1(), 1, joinReport: false);

        Assert.AreEqual(CareerSyncRefusal.NotYourHero, outcome.Refusal);
        Assert.IsNull(outcome.ToSender);
        Assert.IsNull(outcome.ToOthers);
        Assert.IsFalse(_data.HasCareer(Hero));
        Assert.IsFalse(_data.HasCareer("Player2"));
    }

    [TestMethod]
    public void Handle_AnAcceptedChange_IsStored_RefreshesPassives_AndGoesToTheOthersOnly()
    {
        var outcome = _host.Handle(Hero, Ranger1(), 7, joinReport: false);

        Assert.AreEqual(CareerSyncRefusal.None, outcome.Refusal);
        Assert.AreEqual("ranger", _data.GetCareerStringId(Hero));
        CollectionAssert.AreEqual(new[] { "ranger_root" }, new List<string>(_data.GetChoiceIds(Hero)));
        _passives.Received(1).RefreshCache(_data, _registry);
        Assert.IsNull(outcome.ToSender, "the sender already holds what was stored");
        Assert.IsTrue(outcome.ToOthers!.Career.SameAs(Ranger1()));
        Assert.AreEqual(7, outcome.ToOthers.Sequence);
        Assert.IsTrue(outcome.ToOthers.Known);
        Assert.IsFalse(outcome.ToOthers.Refused);
    }

    [TestMethod]
    public void Handle_StoringAReport_RaisesNoStoredCareerChanged()
    {
        _host.Handle(Hero, Ranger1(), 1, joinReport: false);

        Assert.AreEqual(0, _raised.Count, "the relay sends the outcome; a second broadcast from the event would double it");
    }

    [TestMethod]
    public void Handle_ARefusedChange_EchoesTheServersCopy_ToTheSenderOnly()
    {
        StoreOnServer("ranger");

        var outcome = _host.Handle(Hero, Unknown(), 4, joinReport: false);

        Assert.AreEqual(CareerSyncRefusal.UnknownCareer, outcome.Refusal);
        Assert.IsNull(outcome.ToOthers);
        Assert.IsTrue(outcome.ToSender!.Refused);
        Assert.IsTrue(outcome.ToSender.Known);
        Assert.AreEqual(4, outcome.ToSender.Sequence, "the answer carries the report's sequence so the sender can match it");
        Assert.IsTrue(outcome.ToSender.Career.SameAs(Ranger1()));
        Assert.AreEqual("ranger", _data.GetCareerStringId(Hero), "the server's copy is unchanged");
    }

    [TestMethod]
    public void Handle_ARefusedChange_WithNothingStored_TellsTheSender_WithoutACopyToApply()
    {
        var outcome = _host.Handle(Hero, Unknown(), 2, joinReport: false);

        Assert.IsTrue(outcome.ToSender!.Refused);
        Assert.IsFalse(outcome.ToSender.Known, "the server holds nothing, so the sender keeps its own copy");
        Assert.AreEqual(Hero, outcome.ToSender.Career.HeroId);
        Assert.IsNull(outcome.ToOthers);
    }

    [TestMethod]
    public void Handle_ARefusedChange_WithAnEmptyEntryStored_TellsTheSender_ItHoldsNoCareer()
    {
        // A player who joined with no career, or whose career a death cleared, has an entry with no career.
        _data.GetOrCreateData(Hero);

        var outcome = _host.Handle(Hero, Unknown(), 2, joinReport: false);

        Assert.IsTrue(outcome.ToSender!.Refused);
        Assert.IsFalse(outcome.ToSender.Known, "no career is saved, so the sender is not told the server keeps one");
    }

    [TestMethod]
    public void Handle_AJoinReport_WhenTheServerHoldsTheSameCareer_SendsNothing()
    {
        StoreOnServer("ranger");

        var outcome = _host.Handle(Hero, Ranger1(), 1, joinReport: true);

        Assert.IsNull(outcome.ToSender);
        Assert.IsNull(outcome.ToOthers);
    }

    [TestMethod]
    public void Handle_AJoinReport_WhenTheServerHoldsAnother_KeepsTheServers_AndSendsItToTheSender()
    {
        var other = new CareerDefinition("warden", "", "", "", "", 0, "warden_root", new List<string>(), new List<string>());
        _registry.GetCareer("warden").Returns(other);
        StoreOnServer("warden");

        var outcome = _host.Handle(Hero, Ranger1(), 3, joinReport: true);

        Assert.AreEqual("warden", _data.GetCareerStringId(Hero));
        Assert.AreEqual("warden", outcome.ToSender!.Career.CareerId);
        Assert.AreEqual(3, outcome.ToSender.Sequence);
        Assert.IsFalse(outcome.ToSender.Refused);
        Assert.IsTrue(outcome.ToSender.Known);
        Assert.IsNull(outcome.ToOthers);
    }

    [TestMethod]
    public void Handle_AJoinReport_WhenTheServerHoldsNone_IsCheckedAndStored()
    {
        var outcome = _host.Handle(Hero, Ranger1(), 1, joinReport: true);

        Assert.AreEqual("ranger", _data.GetCareerStringId(Hero));
        Assert.IsNotNull(outcome.ToOthers);
        Assert.IsNull(outcome.ToSender);
    }

    [TestMethod]
    public void Handle_AHeroTheServerCannotFind_IsRefused()
    {
        _heroes.Find(Hero).Returns((ICareerHeroAdapter?)null);

        var outcome = _host.Handle(Hero, Ranger1(), 1, joinReport: false);

        Assert.AreEqual(CareerSyncRefusal.UnknownHero, outcome.Refusal);
        Assert.IsFalse(_data.HasCareer(Hero));
    }

    [TestMethod]
    public void Handle_AnIneligibleCareer_IsStored_AndLogged()
    {
        // Hub decision: eligibility is logged only. A possessed hero can carry another culture than the one picked.
        _registry.IsEligible("ranger", Arg.Any<ICareerHeroAdapter>()).Returns(false);

        var outcome = _host.Handle(Hero, Ranger1(), 1, joinReport: false);

        Assert.AreEqual(CareerSyncRefusal.None, outcome.Refusal);
        Assert.AreEqual("ranger", _data.GetCareerStringId(Hero));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not eligible")));
    }

    [TestMethod]
    public void Handle_SwitchOff_IgnoresReports_AndLogsOnce()
    {
        _settings.CoopCareerSync = false;

        var first = _host.Handle(Hero, Ranger1(), 1, joinReport: false);
        _host.Handle(Hero, Ranger1(), 2, joinReport: false);

        Assert.AreEqual(CareerSyncRefusal.SwitchedOff, first.Refusal);
        Assert.IsNull(first.ToSender);
        Assert.IsNull(first.ToOthers);
        Assert.IsFalse(_data.HasCareer(Hero));
        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("switched off")));
    }

    [TestMethod]
    public void AChangeOnTheServer_RaisesStoredCareerChanged_WithTheHerosLastSequence()
    {
        _host.Handle(Hero, Ranger1(), 9, joinReport: false);

        _data.ClearCareer(Hero);

        Assert.AreEqual(1, _raised.Count);
        Assert.AreEqual(Hero, _raised[0].Career.HeroId);
        Assert.IsNull(_raised[0].Career.CareerId);
        Assert.AreEqual(9, _raised[0].Sequence, "the player's client accepts it: it is not older than its last report");
        Assert.IsTrue(_raised[0].Known);
    }

    [TestMethod]
    public void AChangeOnTheServer_WithTheSwitchOff_RaisesNothing()
    {
        _settings.CoopCareerSync = false;

        _data.SetCareer(Hero, "ranger");

        Assert.AreEqual(0, _raised.Count);
    }

    // --- Baseline for a player who just said hello: a change made while that player loaded reached nobody ---

    [TestMethod]
    public void Baseline_IsTheStoredCareerOfEveryHeroSyncedInThisProcess_AtSequenceZero()
    {
        var second = Substitute.For<ICareerHeroAdapter>();
        second.Level.Returns(10);
        _heroes.Find("Player2").Returns(second);
        _host.Handle(Hero, Ranger1(), 3, joinReport: false);
        _host.Handle("Player2", Ranger1("Player2"), 7, joinReport: false);
        _data.SetCareer("changed_on_server", "ranger");
        _data.ApplyHeroCareer(Ranger1("only_in_the_save").ToData());

        var baseline = _host.Baseline();

        CollectionAssert.AreEquivalent(new[] { Hero, "Player2", "changed_on_server" }, baseline.Select(a => a.Career.HeroId).ToList(),
            "a career only in the save is in the joining player's save too");
        Assert.IsTrue(baseline.All(a => a.Sequence == 0), "a client restart starts its counter again: 0 never beats the join report");
        Assert.IsTrue(baseline.All(a => a.Known && !a.Refused));
    }

    [TestMethod]
    public void Baseline_LeavesOutAReportedHeroWithNoStoredCareer_AndIsEmptyWithTheSwitchOff()
    {
        _host.Handle(Hero, Unknown(), 2, joinReport: false);
        Assert.AreEqual(0, _host.Baseline().Count);

        _host.Handle(Hero, Ranger1(), 3, joinReport: false);
        _settings.CoopCareerSync = false;
        Assert.AreEqual(0, _host.Baseline().Count);
    }
}
