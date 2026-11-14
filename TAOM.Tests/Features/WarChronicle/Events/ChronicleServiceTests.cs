using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.ArmyTargeting;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.WarChronicle;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Features.WarChronicle.Events;
using TAOM.Features.WarOfTheRingMomentum.Domain;
using static TAOM.Tests.Features.WarChronicle.Events.ChronicleFixture;

namespace TAOM.Tests.Features.WarChronicle.Events;

/// <summary>
/// The chronicle state machine (docs/features/war-chronicle.md "Events"): Pending opens on its day after
/// its preconditions; a narrative occurs; a siege goes Active with an army focus and resolves Held, Fell
/// or Lapsed from the map. Every owner-changed handler is idempotent: the event fires twice per conquest.
/// </summary>
[TestClass]
public class ChronicleServiceTests
{
    private ChronicleFixture _f = null!;
    private ChronicleService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _f = new ChronicleFixture();
        _sut = _f.CreateService();
    }

    private void Launch(double day = 0)
    {
        _f.World.Day = day;
        _sut.LaunchChronicle();
    }

    private void Day(double day)
    {
        _f.World.Day = day;
        _sut.RunChronicleDay();
    }

    /// <summary>Shows every queued announcement and returns their texts in order.</summary>
    private System.Collections.Generic.List<string?> DrainTexts()
    {
        var texts = new System.Collections.Generic.List<string?>();
        _f.Presenter.When(p => p.Show(Arg.Any<ChronicleAnnouncement>())).Do(c => texts.Add(c.Arg<ChronicleAnnouncement>().Text));
        while (_f.Queue.Count > 0)
            _f.Queue.TryShowNext();
        return texts;
    }

    // ---- Session launch: adoption and world checks -------------------------------------------------

    [TestMethod]
    public void LaunchChronicle_NewCampaign_CreatesAPendingRowPerEvent()
    {
        _f.Events.Add(Narrative("fellowship"));
        _f.Events.Add(SiegeDef());

        Launch(0);

        Assert.AreEqual(ChronicleEventState.Pending, _f.StateOf("fellowship"));
        Assert.AreEqual(ChronicleEventState.Pending, _f.StateOf("hornburg"));
    }

    [TestMethod]
    public void LaunchChronicle_AnOldSavePastAnEventsDay_SkipsItWithNoRetroactiveEffect()
    {
        _f.Events.Add(Narrative("fellowship", effects: Effect("rivendell")));

        Launch(60);

        Assert.AreEqual(ChronicleEventState.Skipped, _f.StateOf("fellowship"));
        Assert.AreEqual(1f, _f.Volunteers("rivendell"));
        Assert.AreEqual(0, _f.Queue.Count, "a skip is silent");
        Assert.AreEqual(1, _f.LedgerLines.Count(l => l.Contains("outcome=Skipped")));
    }

    [TestMethod]
    public void LaunchChronicle_AnOldSaveBeforeAPhaseIsReached_KeepsThePhaseEventPending()
    {
        _f.Phase = WarPhase.Peace;
        _f.Events.Add(SiegeDef());

        Launch(100);

        Assert.AreEqual(ChronicleEventState.Pending, _f.StateOf("hornburg"));
    }

    [TestMethod]
    public void LaunchChronicle_OnAnEventsOwnDay_KeepsItPending()
    {
        _f.Events.Add(Narrative("fellowship", offset: 20));

        Launch(20.4);

        Assert.AreEqual(ChronicleEventState.Pending, _f.StateOf("fellowship"), "day 20.4 is still day 20");
    }

    [TestMethod]
    public void LaunchChronicle_ASavedRow_IsNotReAdopted()
    {
        _f.Events.Add(Narrative("fellowship"));
        _f.Store.Set(new ChronicleEventRecord("fellowship", ChronicleEventState.Pending));

        Launch(60);

        Assert.AreEqual(ChronicleEventState.Pending, _f.StateOf("fellowship"), "only a missing row is adopted; a saved Pending one opens on the next day");
    }

    [TestMethod]
    public void LaunchChronicle_UnknownSettlement_SkipsTheSiege()
    {
        _f.Events.Add(SiegeDef(target: "town_nowhere"));

        Launch(0);

        Assert.AreEqual(ChronicleEventState.Skipped, _f.StateOf("hornburg"));
        Assert.IsTrue(_f.Warnings.Any(w => w.Contains("town_nowhere")));
    }

    [DataTestMethod]
    [DataRow("attacker")]
    [DataRow("defender")]
    [DataRow("effect")]
    public void LaunchChronicle_UnknownKingdom_SkipsTheEvent(string where)
    {
        var siege = SiegeDef(attackers: where == "attacker" ? new[] { "mordor_typo" } : null, defender: where == "defender" ? "rohan_typo" : "vlandia");
        if (where == "effect")
            siege.Held = new ChronicleOutcomeDefinition { Effects = new[] { Effect("gondor_typo") } };
        _f.Events.Add(siege);

        Launch(0);

        Assert.AreEqual(ChronicleEventState.Skipped, _f.StateOf("hornburg"));
    }

    [TestMethod]
    public void LaunchChronicle_UnknownIdsOnAResolvedRow_LeaveItAlone()
    {
        _f.Events.Add(SiegeDef(target: "town_nowhere"));
        _f.Store.Set(new ChronicleEventRecord("hornburg", ChronicleEventState.Held) { ResolvedDay = 70 });

        Launch(80);

        Assert.AreEqual(ChronicleEventState.Held, _f.StateOf("hornburg"));
    }

    [TestMethod]
    public void LaunchChronicle_AnActiveSiegeFromTheSave_PushesTheFocusAgain()
    {
        _f.Events.Add(SiegeDef());
        _f.Store.Set(new ChronicleEventRecord("hornburg", ChronicleEventState.Active) { OpenDay = 49, CloseDay = 69 });

        Launch(55);

        Assert.AreEqual(1.4f, _f.Focus.GetMultiplier(ArmyTargetingMission.Besieger, "town_V2", "isengard"), 0.0001f);
        Assert.AreEqual(1.4f, _f.Focus.GetMultiplier(ArmyTargetingMission.Defender, "town_V2", "vlandia"), 0.0001f);
    }

    [TestMethod]
    public void LaunchChronicle_WarAlreadyEnded_SkipsEveryOpenEventAndClearsTheFocus()
    {
        _f.Phase = WarPhase.WarEnded;
        _f.Events.Add(SiegeDef());
        _f.Events.Add(Narrative("later", ChronicleAnchor.Phase2, 30));
        _f.Store.Set(new ChronicleEventRecord("hornburg", ChronicleEventState.Active) { OpenDay = 49, CloseDay = 69 });

        Launch(55);

        Assert.AreEqual(ChronicleEventState.Skipped, _f.StateOf("hornburg"));
        Assert.AreEqual(ChronicleEventState.Skipped, _f.StateOf("later"));
        Assert.IsTrue(_f.Focus.IsEmpty);
    }

    // ---- Narrative events ----------------------------------------------------------------------------

    [TestMethod]
    public void RunChronicleDay_BeforeANarrativesDay_KeepsItPending()
    {
        _f.Events.Add(Narrative("fellowship", offset: 20));
        Launch(0);

        Day(19.9);

        Assert.AreEqual(ChronicleEventState.Pending, _f.StateOf("fellowship"));
    }

    [TestMethod]
    public void RunChronicleDay_OnANarrativesDay_OccursAppliesItsEffectsAnnouncesAndLedgers()
    {
        _f.Events.Add(Narrative("fellowship", offset: 20, effects: Effect("rivendell", WarEffectKind.VolunteerRate, 0.10f, 20)));
        Launch(0);

        Day(20);

        Assert.AreEqual(ChronicleEventState.Occurred, _f.StateOf("fellowship"));
        Assert.AreEqual(1.1f, _f.Volunteers("rivendell"), 0.0001f);
        var effect = _f.Effects.Snapshot().Single();
        Assert.AreEqual(_f.World.GetNowHours() + 20 * 24d, effect.EndTimeHours, 0.0001);
        Assert.AreEqual(1, _f.Queue.Count);
        Assert.AreEqual(1, _f.LedgerLines.Count(l => l.Contains("id=fellowship outcome=Occurred")));
    }

    [TestMethod]
    public void RunChronicleDay_SideTargets_ReachEveryLiveKingdomOfThatSide()
    {
        _f.Events.Add(Narrative("bridge", offset: 1, effects: new[] { Effect("free", WarEffectKind.VolunteerRate, -0.05f, 15), Effect("isengard", WarEffectKind.VolunteerRate, 0.10f, 15) }));
        Launch(0);

        Day(1);

        foreach (var free in new[] { "vlandia", "empire_w", "rivendell", "sturgia" })
            Assert.AreEqual(0.95f, _f.Volunteers(free), 0.0001f, free);
        Assert.AreEqual(1.1f, _f.Volunteers("isengard"), 0.0001f);
        Assert.AreEqual(1f, _f.Volunteers("empire_s"));
    }

    [TestMethod]
    public void RunChronicleDay_AnEffectOnAnEliminatedKingdom_IsNotWritten()
    {
        _f.Events.Add(Narrative("fellowship", offset: 1, effects: Effect("rivendell")));
        Launch(0);
        _f.World.Kingdoms.Remove("rivendell");

        Day(1);

        Assert.AreEqual(ChronicleEventState.Occurred, _f.StateOf("fellowship"));
        Assert.AreEqual(0, _f.Effects.Snapshot().Count);
    }

    [TestMethod]
    public void RunChronicleDay_Phase1Anchor_WaitsForThePhaseThenCountsFromItsEffectiveDay()
    {
        _f.Phase = WarPhase.Peace;
        _f.Events.Add(Narrative("breaking", ChronicleAnchor.Phase1, 0));
        Launch(0);

        Day(35);
        Assert.AreEqual(ChronicleEventState.Pending, _f.StateOf("breaking"), "the phase has not been reached");

        _f.Phase = WarPhase.IsengardWar;
        Day(36);
        Assert.AreEqual(ChronicleEventState.Occurred, _f.StateOf("breaking"));
    }

    [TestMethod]
    public void RunChronicleDay_EventAnchor_OpensOffsetDaysAfterTheAnchorResolved()
    {
        _f.Events.Add(Narrative("fellowship", offset: 20));
        _f.Events.Add(Narrative("bridge", ChronicleAnchor.Event, 6, anchorEvent: "fellowship"));
        Launch(0);
        Day(20);

        Day(25);
        Assert.AreEqual(ChronicleEventState.Pending, _f.StateOf("bridge"));

        Day(26);
        Assert.AreEqual(ChronicleEventState.Occurred, _f.StateOf("bridge"));
    }

    [TestMethod]
    public void RunChronicleDay_EventAnchorWithOffsetZero_OpensOnTheSameDay()
    {
        _f.Events.Add(Narrative("fellowship", offset: 20));
        _f.Events.Add(Narrative("bridge", ChronicleAnchor.Event, 0, anchorEvent: "fellowship"));
        Launch(0);

        Day(20);

        Assert.AreEqual(ChronicleEventState.Occurred, _f.StateOf("bridge"));
    }

    [TestMethod]
    public void RunChronicleDay_AnchorSkipped_SkipsTheChild()
    {
        _f.Events.Add(Narrative("fellowship", offset: 20));
        _f.Events.Add(Narrative("bridge", ChronicleAnchor.Event, 6, anchorEvent: "fellowship"));
        Launch(40);

        Day(41);

        Assert.AreEqual(ChronicleEventState.Skipped, _f.StateOf("fellowship"));
        Assert.AreEqual(ChronicleEventState.Skipped, _f.StateOf("bridge"));
    }

    [TestMethod]
    public void RunChronicleDay_RequiredOutcomeNotMet_SkipsTheChild()
    {
        _f.Events.Add(SiegeDef());
        _f.Events.Add(Narrative("ents", ChronicleAnchor.Event, 2, anchorEvent: "hornburg", requires: ChronicleEventState.Held));
        Launch(0);
        Day(49);
        _f.World.Owners["town_V2"] = "isengard";
        _sut.OnTargetOwnerChanged("town_V2");

        Day(50);

        Assert.AreEqual(ChronicleEventState.Fell, _f.StateOf("hornburg"));
        Assert.AreEqual(ChronicleEventState.Skipped, _f.StateOf("ents"));
    }

    [TestMethod]
    public void RunChronicleDay_RequiredOutcomeMet_OpensTheChild()
    {
        _f.Events.Add(SiegeDef());
        _f.Events.Add(Narrative("ents", ChronicleAnchor.Event, 2, anchorEvent: "hornburg", requires: ChronicleEventState.Held));
        Launch(0);
        Day(49);
        _f.World.Besiegers["town_V2"] = "isengard";
        Day(50);
        _f.World.Besiegers.Remove("town_V2");

        Day(69);
        Day(71);

        Assert.AreEqual(ChronicleEventState.Held, _f.StateOf("hornburg"));
        Assert.AreEqual(ChronicleEventState.Occurred, _f.StateOf("ents"));
    }

    // ---- Sieges: opening -----------------------------------------------------------------------------

    [TestMethod]
    public void RunChronicleDay_BeforeTheFullWar_KeepsAPhase2SiegePending()
    {
        _f.Phase = WarPhase.IsengardWar;
        _f.Events.Add(SiegeDef());
        Launch(0);

        Day(60);

        Assert.AreEqual(ChronicleEventState.Pending, _f.StateOf("hornburg"));
    }

    [TestMethod]
    public void RunChronicleDay_OnASiegesDay_OpensTheWindowWithFocusAndAnnouncement()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);

        Day(49);

        var record = _f.Store.Get("hornburg")!;
        Assert.AreEqual(ChronicleEventState.Active, record.State);
        Assert.AreEqual(49d, record.OpenDay);
        Assert.AreEqual(69d, record.CloseDay);
        Assert.IsFalse(record.Assaulted);
        Assert.AreEqual(1.4f, _f.Focus.GetMultiplier(ArmyTargetingMission.Besieger, "town_V2", "empire"), 0.0001f);
        Assert.AreEqual(1, _f.Queue.Count);
        Assert.AreEqual(0, _f.Effects.Snapshot().Count, "opening a window pays nothing");
    }

    [TestMethod]
    public void RunChronicleDay_OpeningAnnouncement_NamesTheTargetAndTheDays()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        ChronicleAnnouncement? shown = null;
        _f.Presenter.When(p => p.Show(Arg.Any<ChronicleAnnouncement>())).Do(c => shown = c.Arg<ChronicleAnnouncement>());

        Day(49);
        _f.Queue.TryShowNext();

        Assert.IsNotNull(shown);
        Assert.AreEqual("town_V2", shown!.TargetSettlementId);
        Assert.AreEqual(20, shown.Days);
        StringAssert.Contains(shown.Text, "taom_wc_hornburg_open");
    }

    [TestMethod]
    public void RunChronicleDay_TheAnnouncements_CarryTheEventsImage()
    {
        var narrative = Narrative("khazaddum", offset: 20);
        narrative.Image = "taom_event_khazaddum";
        var siege = SiegeDef();
        siege.Image = "taom_event_hornburg";
        _f.Events.Add(narrative);
        _f.Events.Add(siege);
        Launch(0);
        ChronicleAnnouncement? shown = null;
        _f.Presenter.When(p => p.Show(Arg.Any<ChronicleAnnouncement>())).Do(c => shown = c.Arg<ChronicleAnnouncement>());

        Day(20);
        _f.Queue.TryShowNext();
        Assert.AreEqual("khazaddum", shown?.EventId);
        Assert.AreEqual("taom_event_khazaddum", shown!.Image, "a narrative's announcement");

        Day(49);
        _f.Queue.TryShowNext();
        Assert.AreEqual("hornburg", shown?.EventId);
        Assert.AreEqual("taom_event_hornburg", shown!.Image, "a siege's opening");
    }

    [DataTestMethod]
    [DataRow("rivendell")]
    [DataRow(null)]
    public void RunChronicleDay_TargetHeldByAThirdKingdomOrNone_SkipsTheSiege(string? owner)
    {
        _f.Events.Add(SiegeDef());
        _f.Events.Add(Narrative("after", ChronicleAnchor.Event, 0, anchorEvent: "hornburg"));
        Launch(0);
        _f.World.Owners["town_V2"] = owner;

        Day(49);

        Assert.AreEqual(ChronicleEventState.Skipped, _f.StateOf("hornburg"));
        Assert.AreEqual(ChronicleEventState.Skipped, _f.StateOf("after"), "a skipped siege still skips its children");
        Assert.IsTrue(_f.Focus.IsEmpty);
        Assert.AreEqual(0, _f.Effects.Snapshot().Count);
        Assert.AreEqual(0, _f.Queue.Count, "a skip is silent");
    }

    // Decided by the maintainer 2026-10-14: a target an attacker already owns when its siege opens has fallen.
    [DataTestMethod]
    [DataRow("isengard")]
    [DataRow("empire")]
    public void RunChronicleDay_TargetAlreadyOwnedByAnAttacker_FallsAndPaysItsFellOutcome(string attacker)
    {
        _f.Events.Add(SiegeDef());
        _f.Events[0].Fell!.Momentum = new ChronicleMomentumAward { Side = MomentumSide.Evil, Units = 40 };
        Launch(0);
        _f.World.Owners["town_V2"] = attacker;

        Day(49);

        var record = _f.Store.Get("hornburg")!;
        Assert.AreEqual(ChronicleEventState.Fell, record.State);
        Assert.AreEqual(49d, record.OpenDay);
        Assert.AreEqual(49d, record.ResolvedDay);
        Assert.IsNull(record.CloseDay, "no window was opened");
        Assert.AreEqual(1.15f, _f.Volunteers("isengard"), 0.0001f, "Fell pays the attackers");
        Assert.AreEqual(1.15f, _f.Volunteers("empire"), 0.0001f);
        Assert.AreEqual(1.2f, _f.Escape("vlandia"), 0.0001f, "and the defender's lords escape sooner");
        _f.Momentum.Received(1).TryAwardScripted(MomentumSide.Evil, 40, Arg.Any<string>(), Arg.Any<double>());
        Assert.IsTrue(_f.Focus.IsEmpty, "no army focus for a town already taken");
        Assert.AreEqual(1, _f.LedgerLines.Count(l => l.Contains("id=hornburg outcome=Fell")));
        CollectionAssert.AreEqual(new[] { "{=taom_wc_hornburg_fell}fell" }, DrainTexts(), "the normal fell line, no opening line");
    }

    [TestMethod]
    public void RunChronicleDay_TargetAlreadyOwnedByAnAttacker_OpensTheEventAnchoredChild()
    {
        _f.Events.Add(SiegeDef("osgiliath", "town_EW3", "empire_w", new[] { "empire_s" }, offset: 10));
        _f.Events.Add(SiegeDef("gondor", "town_EW1", "empire_w", new[] { "empire_s" }, ChronicleAnchor.Event, 3, anchorEvent: "osgiliath"));
        _f.Events.Add(Narrative("blackgate", ChronicleAnchor.Event, 5, anchorEvent: "gondor", requires: ChronicleEventState.Held));
        Launch(0);
        _f.World.Owners["town_EW3"] = "empire_s";

        Day(54);
        Assert.AreEqual(ChronicleEventState.Fell, _f.StateOf("osgiliath"));
        Assert.AreEqual(ChronicleEventState.Pending, _f.StateOf("gondor"));

        Day(57);
        Assert.AreEqual(ChronicleEventState.Active, _f.StateOf("gondor"), "the child opens instead of being skipped");
        Assert.AreEqual(ChronicleEventState.Pending, _f.StateOf("blackgate"));
    }

    [TestMethod]
    public void RunChronicleDay_TargetOwnedByAnAttackerWhoseDefenderIsGone_StillFalls()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        _f.World.Owners["town_V2"] = "isengard";
        _f.World.Kingdoms.Remove("vlandia");

        Day(49);

        Assert.AreEqual(ChronicleEventState.Fell, _f.StateOf("hornburg"));
        Assert.AreEqual(1f, _f.Escape("vlandia"), "no effect is written for a dead kingdom");
    }

    [TestMethod]
    public void RunChronicleDay_TargetOwnedByAnAttackerBeforeTheFullWar_IsSkippedAsBefore()
    {
        _f.Phase = WarPhase.IsengardWar;
        _f.Events.Add(SiegeDef(anchor: ChronicleAnchor.CampaignStart, offset: 10));
        Launch(0);
        _f.World.Owners["town_V2"] = "isengard";

        Day(10);

        Assert.AreEqual(ChronicleEventState.Skipped, _f.StateOf("hornburg"), "a siege opens only in the Full War, whoever owns it");
        Assert.AreEqual(0, _f.Effects.Snapshot().Count);
    }

    [TestMethod]
    public void RunChronicleDay_DefenderAtWarWithNoAttacker_SkipsTheSiege()
    {
        _f.AtWar.Clear();
        _f.Events.Add(SiegeDef());
        Launch(0);

        Day(49);

        Assert.AreEqual(ChronicleEventState.Skipped, _f.StateOf("hornburg"));
    }

    [TestMethod]
    public void RunChronicleDay_EveryAttackerEliminated_SkipsTheSiege()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        _f.World.Kingdoms.Remove("isengard");
        _f.World.Kingdoms.Remove("empire");

        Day(49);

        Assert.AreEqual(ChronicleEventState.Skipped, _f.StateOf("hornburg"));
    }

    [TestMethod]
    public void RunChronicleDay_DefenderEliminated_SkipsTheSiege()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        _f.World.Kingdoms.Remove("vlandia");

        Day(49);

        Assert.AreEqual(ChronicleEventState.Skipped, _f.StateOf("hornburg"));
    }

    [TestMethod]
    public void RunChronicleDay_OneAttackerAtWar_IsEnough()
    {
        _f.AtWar.Remove(Pair("empire", "vlandia"));
        _f.Events.Add(SiegeDef());
        Launch(0);

        Day(49);

        Assert.AreEqual(ChronicleEventState.Active, _f.StateOf("hornburg"));
    }

    [TestMethod]
    public void RunChronicleDay_TargetAlreadyBesiegedByAnAttacker_CountsAsAssaulted()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        _f.World.Besiegers["town_V2"] = "empire";

        Day(49);

        Assert.IsTrue(_f.Store.Get("hornburg")!.Assaulted);
    }

    [TestMethod]
    public void RunChronicleDay_TargetBesiegedByAThirdParty_IsNotAnAssault()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        _f.World.Besiegers["town_V2"] = "empire_s";

        Day(49);
        Day(50);

        Assert.IsFalse(_f.Store.Get("hornburg")!.Assaulted);
    }

    // ---- Sieges: resolution --------------------------------------------------------------------------

    [TestMethod]
    public void WindowClose_AssaultedAndStillTheDefenders_IsHeldAndPaysBothSides()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        Day(49);
        _f.World.Besiegers["town_V2"] = "isengard";
        _sut.OnTargetBesieged("town_V2");
        _f.World.Besiegers.Remove("town_V2");

        Day(69);

        Assert.AreEqual(ChronicleEventState.Held, _f.StateOf("hornburg"));
        Assert.AreEqual(1.15f, _f.Volunteers("vlandia"), 0.0001f, "the winner recruits faster");
        Assert.AreEqual(1.2f, _f.Escape("isengard"), 0.0001f, "the loser's lords escape sooner");
        Assert.AreEqual(1.2f, _f.Escape("empire"), 0.0001f);
        Assert.AreEqual(1f, _f.Volunteers("isengard"));
        Assert.IsTrue(_f.Focus.IsEmpty, "the focus clears on resolution");
        Assert.AreEqual(1, _f.LedgerLines.Count(l => l.Contains("outcome=Held")));
    }

    [TestMethod]
    public void WindowClose_NeverAssaulted_LapsesWithNoEffectButAToast()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        Day(49);
        _f.Queue.ResetForNewSession();

        Day(69);

        Assert.AreEqual(ChronicleEventState.Lapsed, _f.StateOf("hornburg"));
        Assert.AreEqual(0, _f.Effects.Snapshot().Count);
        Assert.AreEqual(1, _f.Queue.Count, "a lapse is announced");
        Assert.IsTrue(_f.Focus.IsEmpty);
        CollectionAssert.AreEqual(new[] { "{=taom_wc_hornburg_lapsed}lapsed" }, DrainTexts());
    }

    [TestMethod]
    public void WindowClose_AttackerStillBesieging_DefersTheClose()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        Day(49);
        _f.World.Besiegers["town_V2"] = "isengard";

        Day(69);

        Assert.AreEqual(ChronicleEventState.Active, _f.StateOf("hornburg"), "a siege still being fought decides the window");
        Assert.AreEqual(0, _f.Effects.Snapshot().Count);
        Assert.AreEqual(1.4f, _f.Focus.GetMultiplier(ArmyTargetingMission.Besieger, "town_V2", "isengard"), 0.0001f);
    }

    [TestMethod]
    public void WindowClose_DeferredThenTaken_FallsAndSkipsTheHeldChild()
    {
        _f.Events.Add(SiegeDef());
        _f.Events.Add(Narrative("ents", ChronicleAnchor.Event, 2, anchorEvent: "hornburg", requires: ChronicleEventState.Held));
        Launch(0);
        Day(49);
        _f.World.Besiegers["town_V2"] = "isengard";
        Day(69);

        _f.World.Owners["town_V2"] = "isengard";
        _f.World.Day = 71;
        _sut.OnTargetOwnerChanged("town_V2");
        Day(72);

        Assert.AreEqual(ChronicleEventState.Fell, _f.StateOf("hornburg"));
        Assert.AreEqual(1.15f, _f.Volunteers("isengard"), 0.0001f);
        Assert.AreEqual(1.2f, _f.Escape("vlandia"), 0.0001f);
        Assert.AreEqual(1f, _f.Volunteers("vlandia"), "Held never paid");
        Assert.AreEqual(ChronicleEventState.Skipped, _f.StateOf("ents"));
    }

    [TestMethod]
    public void WindowClose_DeferredThenLifted_IsHeldOnTheNextDay()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        Day(49);
        _f.World.Besiegers["town_V2"] = "isengard";
        Day(69);
        Day(70);

        _f.World.Besiegers.Remove("town_V2");
        Day(71);

        Assert.AreEqual(ChronicleEventState.Held, _f.StateOf("hornburg"));
        Assert.AreEqual(71d, _f.Store.Get("hornburg")!.ResolvedDay);
    }

    [TestMethod]
    public void WindowClose_ThirdPartyBesieging_DoesNotDefer()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        Day(49);
        _f.World.Besiegers["town_V2"] = "empire_s";

        Day(69);

        Assert.AreEqual(ChronicleEventState.Lapsed, _f.StateOf("hornburg"));
    }

    [TestMethod]
    public void WindowClose_StillBesiegedAtTheCap_Closes()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        Day(49);
        _f.World.Besiegers["town_V2"] = "isengard";
        Day(69);
        Day(88);
        Assert.AreEqual(ChronicleEventState.Active, _f.StateOf("hornburg"), "one extra window of overtime at most");

        Day(89);

        Assert.AreEqual(ChronicleEventState.Held, _f.StateOf("hornburg"));
    }

    [TestMethod]
    public void WindowClose_DayBeforeClose_StaysActive()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        Day(49);

        Day(68.9);

        Assert.AreEqual(ChronicleEventState.Active, _f.StateOf("hornburg"));
    }

    // An owner change can raise no OnSettlementOwnerChangedEvent: a clan that defects or leaves with
    // rebellion takes its fiefs along (ChangeKingdomAction), and a change while the chronicle is off is
    // never seen. Setting the owner without calling OnTargetOwnerChanged simulates both.

    [TestMethod]
    public void RunChronicleDay_AnAttackerOwnsTheTargetUnseen_FallsOnTheNextStep()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        Day(49);
        _f.World.Owners["town_V2"] = "isengard";

        Day(50);

        Assert.AreEqual(ChronicleEventState.Fell, _f.StateOf("hornburg"));
        Assert.AreEqual(1.15f, _f.Volunteers("isengard"), 0.0001f);
        Assert.AreEqual(1.15f, _f.Volunteers("empire"), 0.0001f);
        Assert.AreEqual(1.2f, _f.Escape("vlandia"), 0.0001f);
        Assert.IsTrue(_f.Focus.IsEmpty);
    }

    [DataTestMethod]
    [DataRow("empire_s")]
    [DataRow(null)]
    public void RunChronicleDay_AnotherOwnsTheTargetUnseen_LapsesOnTheNextStep(string? owner)
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        Day(49);
        _f.World.Owners["town_V2"] = owner;

        Day(50);
        _f.World.Owners["town_V2"] = "isengard";
        _sut.OnTargetOwnerChanged("town_V2");

        Assert.AreEqual(ChronicleEventState.Lapsed, _f.StateOf("hornburg"));
        Assert.AreEqual(0, _f.Effects.Snapshot().Count, "a later capture of a lapsed window changes nothing");
    }

    [TestMethod]
    public void RunChronicleDay_CapturedWhileTheChronicleWasOff_FallsOnceWhenBackOn()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        Day(49);
        _f.Settings.ChronicleEnabled.Returns(false);
        _f.World.Owners["town_V2"] = "isengard";
        _sut.OnTargetOwnerChanged("town_V2");
        _f.Settings.ChronicleEnabled.Returns(true);

        Day(70);

        Assert.AreEqual(ChronicleEventState.Fell, _f.StateOf("hornburg"));
        Assert.AreEqual(1, _f.LedgerLines.Count(l => l.Contains("outcome=")));
        Assert.AreEqual(1f, _f.Volunteers("vlandia"), "no Held pay");
    }

    [TestMethod]
    public void RunChronicleDay_UnseenChangeOnTheCloseDay_ResolvesOnce()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        Day(49);
        _f.World.Owners["town_V2"] = "isengard";

        Day(69);

        Assert.AreEqual(ChronicleEventState.Fell, _f.StateOf("hornburg"));
        Assert.AreEqual(1, _f.LedgerLines.Count(l => l.Contains("outcome=")));
        Assert.AreEqual(3, _f.Effects.Snapshot().Count);
    }

    [DataTestMethod]
    [DataRow("empire_s")]
    [DataRow(null)]
    public void WindowClose_AssaultedButAnotherHoldsIt_LapsesWithNoEffect(string? owner)
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        Day(49);
        _f.World.Besiegers["town_V2"] = "isengard";
        _sut.OnTargetBesieged("town_V2");
        _f.World.Besiegers.Remove("town_V2");
        _f.World.Owners["town_V2"] = owner;
        _f.Queue.ResetForNewSession();

        Day(69);

        Assert.IsTrue(_f.Store.Get("hornburg")!.Assaulted);
        Assert.AreEqual(ChronicleEventState.Lapsed, _f.StateOf("hornburg"));
        Assert.AreEqual(0, _f.Effects.Snapshot().Count, "Held must not pay for a town another holds");
        CollectionAssert.AreEqual(new[] { ChronicleService.LostText }, DrainTexts());
    }

    [TestMethod]
    public void RunChronicleDay_FractionalClock_ClosesAfterExactlyTheWindow()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        Day(49.003);

        Day(69.001);

        var record = _f.Store.Get("hornburg")!;
        Assert.AreEqual(ChronicleEventState.Lapsed, record.State);
        Assert.AreEqual(49d, record.OpenDay);
        Assert.AreEqual(69d, record.CloseDay);
    }

    [TestMethod]
    public void OnTargetOwnerChanged_MidDay_ChildCountsFromTheResolutionDay()
    {
        _f.Events.Add(SiegeDef());
        _f.Events.Add(Narrative("after", ChronicleAnchor.Event, 2, anchorEvent: "hornburg"));
        Launch(0);
        Day(49.003);
        _f.World.Day = 60.5;
        _f.World.Owners["town_V2"] = "isengard";
        _sut.OnTargetOwnerChanged("town_V2");

        Day(62.001);

        Assert.AreEqual(ChronicleEventState.Occurred, _f.StateOf("after"));
    }

    [TestMethod]
    public void OnTargetOwnerChanged_ToAnAttacker_FallsAtOnceAndPaysBothSides()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        Day(49);
        _f.World.Owners["town_V2"] = "isengard";

        _sut.OnTargetOwnerChanged("town_V2");

        Assert.AreEqual(ChronicleEventState.Fell, _f.StateOf("hornburg"));
        Assert.AreEqual(1.15f, _f.Volunteers("isengard"), 0.0001f);
        Assert.AreEqual(1.15f, _f.Volunteers("empire"), 0.0001f);
        Assert.AreEqual(1.2f, _f.Escape("vlandia"), 0.0001f);
        Assert.IsTrue(_f.Focus.IsEmpty);
    }

    [TestMethod]
    public void OnTargetOwnerChanged_FiredTwiceForOneConquest_ResolvesAndPaysOnce()
    {
        _f.Events.Add(SiegeDef());
        _f.Events[0].Fell!.Momentum = new ChronicleMomentumAward { Side = MomentumSide.Evil, Units = 40 };
        Launch(0);
        Day(49);
        _f.Queue.ResetForNewSession();
        _f.World.Owners["town_V2"] = "isengard";

        _sut.OnTargetOwnerChanged("town_V2");
        _sut.OnTargetOwnerChanged("town_V2");

        Assert.AreEqual(ChronicleEventState.Fell, _f.StateOf("hornburg"));
        Assert.AreEqual(3, _f.Effects.Snapshot().Count, "two attackers' volunteers and the defender's escape: written once");
        Assert.AreEqual(1, _f.LedgerLines.Count());
        _f.Momentum.Received(1).TryAwardScripted(MomentumSide.Evil, 40, Arg.Any<string>(), Arg.Any<double>());
        Assert.AreEqual(1, _f.Queue.Count);
    }

    [TestMethod]
    public void OnTargetOwnerChanged_CaptureThenGrantWithinTheAttacker_StaysFell()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        Day(49);
        _f.World.Owners["town_V2"] = "isengard";
        _sut.OnTargetOwnerChanged("town_V2");

        _f.World.Owners["town_V2"] = "empire";
        _sut.OnTargetOwnerChanged("town_V2");

        Assert.AreEqual(ChronicleEventState.Fell, _f.StateOf("hornburg"));
    }

    [TestMethod]
    public void OnTargetOwnerChanged_ToAThirdParty_Lapses()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        Day(49);
        _f.World.Owners["town_V2"] = "empire_s";
        _f.Queue.ResetForNewSession();

        _sut.OnTargetOwnerChanged("town_V2");

        Assert.AreEqual(ChronicleEventState.Lapsed, _f.StateOf("hornburg"));
        Assert.AreEqual(0, _f.Effects.Snapshot().Count);
        CollectionAssert.AreEqual(new[] { ChronicleService.LostText }, DrainTexts(), "not the event's no-assault line");
    }

    [TestMethod]
    public void OnTargetOwnerChanged_ToAKingdomlessClan_Lapses()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        Day(49);
        _f.World.Owners["town_V2"] = null;
        _f.Queue.ResetForNewSession();

        _sut.OnTargetOwnerChanged("town_V2");

        Assert.AreEqual(ChronicleEventState.Lapsed, _f.StateOf("hornburg"));
        CollectionAssert.AreEqual(new[] { ChronicleService.LostText }, DrainTexts());
    }

    [TestMethod]
    public void OnTargetOwnerChanged_RegrantedWithinTheDefender_ChangesNothing()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        Day(49);

        _sut.OnTargetOwnerChanged("town_V2");
        _sut.OnTargetOwnerChanged("town_V2");

        Assert.AreEqual(ChronicleEventState.Active, _f.StateOf("hornburg"));
    }

    [TestMethod]
    public void OnTargetOwnerChanged_AnotherSettlementOrNull_ChangesNothing()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        Day(49);
        _f.World.Owners["town_EW1"] = "empire_s";

        _sut.OnTargetOwnerChanged("town_EW1");
        _sut.OnTargetOwnerChanged(null);

        Assert.AreEqual(ChronicleEventState.Active, _f.StateOf("hornburg"));
    }

    [TestMethod]
    public void OnTargetOwnerChanged_PendingSiege_IsNotResolved()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        _f.World.Owners["town_V2"] = "isengard";

        _sut.OnTargetOwnerChanged("town_V2");

        Assert.AreEqual(ChronicleEventState.Pending, _f.StateOf("hornburg"));
    }

    [TestMethod]
    public void OnTargetBesieged_ByAnAttacker_MarksTheWindowAssaulted()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        Day(49);
        _f.World.Besiegers["town_V2"] = "empire";

        _sut.OnTargetBesieged("town_V2");

        Assert.IsTrue(_f.Store.Get("hornburg")!.Assaulted);
    }

    [TestMethod]
    public void OnTargetBesieged_ByAThirdParty_DoesNotCount()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        Day(49);
        _f.World.Besiegers["town_V2"] = "khuzait";

        _sut.OnTargetBesieged("town_V2");

        Assert.IsFalse(_f.Store.Get("hornburg")!.Assaulted);
    }

    [TestMethod]
    public void Resolution_WithAMomentumAward_AwardsItsSideWithTheResolvedTitle()
    {
        _f.Events.Add(SiegeDef());
        _f.Events[0].Held!.Momentum = new ChronicleMomentumAward { Side = MomentumSide.Free, Units = 50 };
        Launch(0);
        Day(49);
        _f.World.Besiegers["town_V2"] = "isengard";
        Day(50);
        _f.World.Besiegers.Remove("town_V2");

        Day(69);

        _f.Momentum.Received(1).TryAwardScripted(MomentumSide.Free, 50, "resolved:{=taom_wc_hornburg_title}hornburg", _f.World.GetNowHours());
    }

    // ---- War end, switches and guards ----------------------------------------------------------------

    [TestMethod]
    public void RunChronicleDay_WarEnded_SkipsEveryOpenEventAndClearsTheFocus()
    {
        _f.Events.Add(SiegeDef());
        _f.Events.Add(SiegeDef("gondor", "town_EW1", "empire_w", new[] { "empire_s" }, offset: 30));
        Launch(0);
        Day(49);

        _f.Phase = WarPhase.WarEnded;
        Day(50);

        Assert.AreEqual(ChronicleEventState.Skipped, _f.StateOf("hornburg"));
        Assert.AreEqual(ChronicleEventState.Skipped, _f.StateOf("gondor"));
        Assert.IsTrue(_f.Focus.IsEmpty);
        Assert.AreEqual(0, _f.Effects.Snapshot().Count);
    }

    [TestMethod]
    public void OnTargetOwnerChanged_AfterWarEnded_SkipsTheWindowAndPaysNothing()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        Day(49);
        DrainTexts();
        _f.Phase = WarPhase.WarEnded;
        _f.World.Owners["town_V2"] = "isengard";

        _sut.OnTargetOwnerChanged("town_V2");
        _sut.OnTargetOwnerChanged("town_V2");

        Assert.AreEqual(ChronicleEventState.Skipped, _f.StateOf("hornburg"));
        Assert.AreEqual(0, _f.Effects.Snapshot().Count);
        Assert.AreEqual(0, DrainTexts().Count, "no resolution announcement");
        Assert.IsTrue(_f.Focus.IsEmpty);
    }

    [TestMethod]
    public void OnTargetBesieged_AfterWarEnded_SkipsTheWindowAndPaysNothing()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        Day(49);
        DrainTexts();
        _f.Phase = WarPhase.WarEnded;
        _f.World.Besiegers["town_V2"] = "empire";

        _sut.OnTargetBesieged("town_V2");

        Assert.AreEqual(ChronicleEventState.Skipped, _f.StateOf("hornburg"));
        Assert.IsFalse(_f.Store.Get("hornburg")!.Assaulted);
        Assert.AreEqual(0, _f.Effects.Snapshot().Count);
        Assert.AreEqual(0, DrainTexts().Count, "no resolution announcement");
        Assert.IsTrue(_f.Focus.IsEmpty);
    }

    [TestMethod]
    public void RunChronicleDay_ChronicleOff_OpensNothingAndClearsTheFocus()
    {
        _f.Events.Add(SiegeDef());
        _f.Events.Add(Narrative("fellowship", offset: 20));
        Launch(0);
        Day(49);
        Assert.AreEqual(ChronicleEventState.Active, _f.StateOf("hornburg"));

        _f.Settings.ChronicleEnabled.Returns(false);
        _f.World.Owners["town_V2"] = "isengard";
        _sut.OnTargetOwnerChanged("town_V2");
        Day(70);

        Assert.AreEqual(ChronicleEventState.Active, _f.StateOf("hornburg"), "off freezes the chronicle; the state is kept");
        Assert.IsTrue(_f.Focus.IsEmpty);
    }

    [TestMethod]
    public void RunChronicleDay_FocusMultiplierChanged_IsPushedOnTheNextDay()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        Day(49);

        _f.Settings.ArmyFocusMultiplier.Returns(2f);
        Day(50);

        Assert.AreEqual(2f, _f.Focus.GetMultiplier(ArmyTargetingMission.Besieger, "town_V2", "isengard"), 0.0001f);
    }

    [DataTestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    public void RunChronicleDay_NonFiniteClock_DoesNothing(double day)
    {
        _f.Events.Add(Narrative("fellowship", offset: 0));
        _f.Events.Add(SiegeDef());
        _f.Store.Set(new ChronicleEventRecord("hornburg", ChronicleEventState.Active) { OpenDay = 49, CloseDay = 69 });
        Launch(0);

        Day(day);

        Assert.AreEqual(ChronicleEventState.Pending, _f.StateOf("fellowship"));
        Assert.AreEqual(ChronicleEventState.Active, _f.StateOf("hornburg"));
    }

    [TestMethod]
    public void RunChronicleDay_AThrowingWorldRead_IsLoggedAndTheDayIsSkipped()
    {
        var world = Substitute.For<TAOM.Adapters.IChronicleWorldAdapter>();
        world.GetElapsedDays().Returns(_ => throw new System.NullReferenceException("engine"));
        var sut = new ChronicleService(_f.Config, _f.Store, world, _f.Alliance, _f.Wotr,
            new ChronicleOutcomeService(_f.Effects, _f.Alliance, _f.Alignment, _f.Momentum, _f.Ledger, _f.Text, _f.Logger),
            _f.Queue, _f.Focus, _f.Settings, _f.Logger, _f.Daily);

        sut.RunChronicleDay();

        Assert.IsTrue(_f.Warnings.Any(w => w.Contains("engine")));
    }

    [TestMethod]
    public void ResetTransientState_ClearsTheFocusLeftByAnEarlierCampaign()
    {
        _f.Focus.Replace(new[] { new ArmyTargetFocus("town_V2", new[] { "isengard" }, "vlandia", 1.4f) });

        _sut.ResetTransientState();

        Assert.IsTrue(_f.Focus.IsEmpty);
    }

    // ---- Once per campaign day -----------------------------------------------------------------------
    // A save made in the first half of an elapsed campaign day ticks that day again after a load (v1.5.5
    // Campaign.cs:1024-1030). The elapsed day starts at CampaignStartTime (09:00 on vanilla's calendar clock).

    [TestMethod]
    public void RunChronicleDay_TwiceOnTheSameDay_StepsOnce()
    {
        _f.Events.Add(Narrative("fellowship"));
        Day(5);
        _f.Config.ClearReceivedCalls();

        Day(5.6);

        _f.Config.DidNotReceive().GetEvents();
    }

    [TestMethod]
    public void RunChronicleDay_TheNextDay_Steps()
    {
        _f.Events.Add(Narrative("fellowship"));
        Day(5);
        _f.Config.ClearReceivedCalls();

        Day(6);

        _f.Config.Received().GetEvents();
    }

    [TestMethod]
    public void RunChronicleDay_TheTickAlreadyRanToday_StillSteps()
    {
        _f.Daily.TryClaim(WarChronicleDailyGate.TickLane, 5d);

        Day(5);

        _f.Config.Received().GetEvents();
    }

    [DataTestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    [DataRow(-1d)]
    [DataRow(-0.5d)]
    public void RunChronicleDay_AnInvalidClock_ClaimsNoDay(double day)
    {
        // ChronicleWorldAdapter passes the engine value through unmasked (no day-0 fallback), so the
        // finite check and the gate's range check see the bad clock and skip the day.
        _f.Events.Add(Narrative("fellowship"));

        Day(day);

        Assert.AreEqual(0, _f.Daily.Snapshot().Count);
        _f.Config.DidNotReceive().GetEvents();
    }

    // ---- Console: open now ---------------------------------------------------------------------------

    [TestMethod]
    public void OpenChronicleEventNow_APendingSiege_OpensIgnoringTheTime()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        _f.World.Day = 3;

        Assert.IsTrue(_sut.OpenChronicleEventNow("hornburg", dryRun: false, out var reason), reason);

        Assert.AreEqual(ChronicleEventState.Active, _f.StateOf("hornburg"));
        Assert.AreEqual(23d, _f.Store.Get("hornburg")!.CloseDay);
    }

    [TestMethod]
    public void OpenChronicleEventNow_APreconditionFails_StaysPendingAndSaysWhy()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        _f.World.Owners["town_V2"] = "rivendell";

        Assert.IsFalse(_sut.OpenChronicleEventNow("hornburg", dryRun: false, out var reason));

        Assert.AreEqual(ChronicleEventState.Pending, _f.StateOf("hornburg"));
        StringAssert.Contains(reason, "vlandia");
    }

    [TestMethod]
    public void OpenChronicleEventNow_TargetAlreadyOwnedByAnAttacker_RecordsItFell()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        _f.World.Owners["town_V2"] = "isengard";
        _f.World.Day = 3;

        Assert.IsTrue(_sut.OpenChronicleEventNow("hornburg", dryRun: false, out var reason), reason);

        Assert.AreEqual(ChronicleEventState.Fell, _f.StateOf("hornburg"));
        StringAssert.Contains(reason, "Fell");
    }

    [TestMethod]
    public void OpenChronicleEventNow_AResolvedEvent_IsRefused()
    {
        _f.Events.Add(Narrative("fellowship"));
        Launch(0);
        Day(20);

        Assert.IsFalse(_sut.OpenChronicleEventNow("fellowship", dryRun: false, out var reason));
        StringAssert.Contains(reason, "Occurred");
    }

    [DataTestMethod]
    [DataRow("nope")]
    [DataRow(null)]
    public void OpenChronicleEventNow_AnUnknownId_IsRefused(string id)
    {
        _f.Events.Add(Narrative("fellowship"));
        Launch(0);

        Assert.IsFalse(_sut.OpenChronicleEventNow(id, dryRun: false, out var reason));
        Assert.IsFalse(string.IsNullOrEmpty(reason));
    }

    [TestMethod]
    public void OpenChronicleEventNow_ChronicleOff_IsRefused()
    {
        _f.Events.Add(Narrative("fellowship"));
        Launch(0);
        _f.Settings.ChronicleEnabled.Returns(false);

        Assert.IsFalse(_sut.OpenChronicleEventNow("fellowship", dryRun: false, out _));
        Assert.AreEqual(ChronicleEventState.Pending, _f.StateOf("fellowship"));
    }

    // ---- Console: the dry run (Tier C: nothing changes without 'confirm') ----------------------------

    [TestMethod]
    public void OpenChronicleEventNow_DryRunOnAPendingSiege_ReportsTheWindowAndChangesNothing()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        _f.World.Day = 3;

        Assert.IsTrue(DryRun("hornburg", out var reason), reason);

        StringAssert.Contains(reason, "would open its window until day 23");
        var record = _f.Store.Get("hornburg")!;
        Assert.AreEqual(ChronicleEventState.Pending, record.State);
        Assert.IsNull(record.OpenDay);
        Assert.IsNull(record.CloseDay);
        Assert.AreEqual(0, _f.Queue.Count);
        Assert.IsTrue(_f.Focus.IsEmpty);
    }

    [TestMethod]
    public void OpenChronicleEventNow_DryRunOnATargetAnAttackerHolds_ReportsFellAndPaysNothing()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        _f.World.Owners["town_V2"] = "isengard";
        _f.World.Day = 3;

        Assert.IsTrue(DryRun("hornburg", out var reason), reason);

        StringAssert.Contains(reason, "would resolve Fell");
        StringAssert.Contains(reason, "apply its Fell outcome, if it defines one");
        StringAssert.Contains(reason, "isengard");
        Assert.AreEqual(ChronicleEventState.Pending, _f.StateOf("hornburg"));
        _f.Momentum.DidNotReceiveWithAnyArgs().TryAwardScripted(default, default, default!, default);
        Assert.AreEqual(0, _f.Queue.Count);
    }

    [TestMethod]
    public void OpenChronicleEventNow_DryRunOnANarrative_ReportsItWouldOccurAndAppliesNoEffect()
    {
        _f.Events.Add(Narrative("fellowship", offset: 20, effects: Effect("rivendell", WarEffectKind.VolunteerRate, 0.10f, 20)));
        Launch(0);

        Assert.IsTrue(DryRun("fellowship", out var reason), reason);

        StringAssert.Contains(reason, "would occur");
        Assert.AreEqual(ChronicleEventState.Pending, _f.StateOf("fellowship"));
        Assert.AreEqual(0, _f.Effects.Snapshot().Count);
        Assert.AreEqual(0, _f.Queue.Count);
        Assert.AreEqual(0, _f.LedgerLines.Count());
    }

    [TestMethod]
    public void OpenChronicleEventNow_DryRunWithAFailedPrecondition_SaysItStaysPendingAndWhy()
    {
        _f.Events.Add(SiegeDef());
        Launch(0);
        _f.World.Owners["town_V2"] = "rivendell";

        Assert.IsFalse(DryRun("hornburg", out var reason));

        StringAssert.Contains(reason, "would stay Pending");
        StringAssert.Contains(reason, "vlandia");
        Assert.AreEqual(ChronicleEventState.Pending, _f.StateOf("hornburg"));
    }

    [TestMethod]
    public void OpenChronicleEventNow_DryRunOnAResolvedEvent_IsRefusedAsBefore()
    {
        _f.Events.Add(Narrative("fellowship"));
        Launch(0);
        Day(20);

        Assert.IsFalse(DryRun("fellowship", out var reason));
        StringAssert.Contains(reason, "only a Pending event can be opened");
    }

    [TestMethod]
    public void OpenChronicleEventNow_DryRunWithTheChronicleOff_IsRefusedAsBefore()
    {
        _f.Events.Add(Narrative("fellowship"));
        Launch(0);
        _f.Settings.ChronicleEnabled.Returns(false);

        Assert.IsFalse(DryRun("fellowship", out var reason));
        StringAssert.Contains(reason, "The War Chronicle is off");
    }

    [TestMethod]
    public void OpenChronicleEventNow_DryRunAfterTheWarEnded_IsRefusedAsBefore()
    {
        _f.Events.Add(Narrative("fellowship"));
        Launch(0);
        _f.Phase = WarPhase.WarEnded;

        Assert.IsFalse(DryRun("fellowship", out var reason));
        StringAssert.Contains(reason, "the chronicle is closed");
    }

    [TestMethod]
    public void OpenChronicleEventNow_DryRunBeforeAnyLaunch_CreatesNoRow()
    {
        _f.Events.Add(Narrative("fellowship"));
        _f.World.Day = 3;

        Assert.IsFalse(DryRun("fellowship", out var reason));

        StringAssert.Contains(reason, "no saved row");
        StringAssert.Contains(reason, "its anchor fails", "adoption also skips an event whose anchor failed");
        Assert.IsFalse(reason.Contains("then opens"), "the dry run cannot promise what a confirmed open does: " + reason);
        Assert.IsNull(_f.Store.Get("fellowship"), "a dry run must not adopt the row");
    }

    [TestMethod]
    public void OpenChronicleEventNow_DryRunAfterFullWarBegins_RecordsNoPhaseDay()
    {
        _f.Phase = WarPhase.IsengardWar;
        _f.Events.Add(Narrative("fellowship"));
        Launch(0);
        _f.Phase = WarPhase.FullWar;
        _f.World.Day = 10;

        DryRun("fellowship", out _);

        Assert.IsNull(_f.Store.GetPhaseSeenDay(WarPhase.FullWar), "a dry run must not note the first Full War day");
    }

    /// <summary>
    /// Parity: the dry run and the confirmed open agree on whether the open acts, and the dry run's predicted
    /// outcome is the state the confirmed open leaves. The confirmed open runs on its own, equivalent fixture,
    /// so a preview that changed saved state cannot be hidden by a confirmation that overwrites the same field
    /// (a siege's open resets <c>Assaulted</c>).
    /// </summary>
    [DataTestMethod]
    [DataRow("narrative", "would occur", ChronicleEventState.Occurred, DisplayName = "narrative occurs")]
    [DataRow("siege-open", "would open its window", ChronicleEventState.Active, DisplayName = "siege opens")]
    [DataRow("siege-fell", "would resolve Fell", ChronicleEventState.Fell, DisplayName = "siege Fell at open")]
    [DataRow("precondition", "would stay Pending", ChronicleEventState.Pending, DisplayName = "precondition fails")]
    public void OpenChronicleEventNow_DryRunThenConfirm_AgreeOnTheOutcome(string shape, string predicted, ChronicleEventState endState)
    {
        var preview = new ChronicleFixture();
        var previewSut = preview.CreateService();
        var id = ArrangeParityShape(preview, previewSut, shape);
        ArrangeParityShape(_f, _sut, shape);
        Assert.AreEqual(DeepState(_f.Store), DeepState(preview.Store), "the two fixtures must start equal");

        var dryActed = DryRun(preview, previewSut, id, out var dryReason);
        var acted = _sut.OpenChronicleEventNow(id, dryRun: false, out var reason);

        Assert.AreEqual(acted, dryActed, $"dry run: {dryReason} / confirmed: {reason}");
        StringAssert.Contains(dryReason, predicted);
        Assert.AreEqual(endState, _f.StateOf(id), $"dry run predicted '{dryReason}'");
    }

    private static string ArrangeParityShape(ChronicleFixture f, ChronicleService sut, string shape)
    {
        var id = shape == "narrative" ? "fellowship" : "hornburg";
        f.Events.Add(shape == "narrative" ? Narrative(id, effects: Effect("rivendell")) : SiegeDef());
        f.World.Day = 0;
        sut.LaunchChronicle();
        if (shape == "siege-fell")
            f.World.Owners["town_V2"] = "isengard";
        else if (shape == "precondition")
            f.World.Owners["town_V2"] = "rivendell";
        f.World.Day = 3;
        return id;
    }

    // ---- Dry-run guard: a deep value snapshot of the saved chronicle state ----------------------------

    /// <summary>What the save codec writes for a row. <see cref="DeepState"/> reads every public property, so
    /// this only proves the reflection still sees them all.</summary>
    private static readonly string[] SavedRecordFields = { "Id", "State", "OpenDay", "CloseDay", "Assaulted", "ResolvedDay" };

    private bool DryRun(string id, out string reason) => DryRun(_f, _sut, id, out reason);

    /// <summary>Runs the dry run and fails unless every saved record field and phase-seen day is unchanged.</summary>
    private static bool DryRun(ChronicleFixture f, ChronicleService sut, string id, out string reason)
    {
        var before = DeepState(f.Store);
        var acted = sut.OpenChronicleEventNow(id, dryRun: true, out reason);
        Assert.AreEqual(before, DeepState(f.Store), "a dry run must change no saved chronicle field or phase day: " + reason);
        return acted;
    }

    /// <summary>
    /// Every public property of every record by value, in id order, then every phase-seen day.
    /// <see cref="ChronicleStateStore.Snapshot"/> is no baseline on its own: it copies the list but shares the
    /// mutable records, so a field changed in place would read the same before and after.
    /// </summary>
    private static string DeepState(ChronicleStateStore store)
    {
        var properties = typeof(ChronicleEventRecord).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToList();
        CollectionAssert.IsSubsetOf(SavedRecordFields, properties.Select(p => p.Name).ToList(),
            "the snapshot no longer reads every saved record field");

        var text = new StringBuilder();
        foreach (var record in store.Records.Values.OrderBy(r => r.Id, StringComparer.Ordinal))
        {
            foreach (var property in properties)
                text.Append(property.Name).Append('=').Append(Format(property.GetValue(record))).Append(';');
            text.Append('\n');
        }
        foreach (var pair in store.PhaseSeenSnapshot().OrderBy(p => p.Key))
            text.Append("phase ").Append(pair.Key).Append('=').Append(Format(pair.Value)).Append('\n');
        return text.ToString();
    }

    private static string Format(object? value) => value switch
    {
        null => "null",
        double d => d.ToString("R", CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
    };

    [TestMethod]
    public void Status_ListsEveryEventWithItsStateAndDays()
    {
        _f.Events.Add(Narrative("fellowship"));
        _f.Events.Add(SiegeDef());
        Launch(0);
        Day(49);

        var rows = _sut.Status();

        Assert.AreEqual(2, rows.Count);
        Assert.AreEqual(ChronicleEventState.Occurred, rows[0].State);
        Assert.AreEqual(ChronicleEventState.Active, rows[1].State);
        Assert.AreEqual(69d, rows[1].CloseDay);
        Assert.AreEqual(49d, rows[1].OpenDay);
    }
}
