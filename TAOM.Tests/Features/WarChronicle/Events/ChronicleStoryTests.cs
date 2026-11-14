using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.WarChronicle.Events;
using static TAOM.Tests.Features.WarChronicle.Events.ChronicleFixture;

namespace TAOM.Tests.Features.WarChronicle.Events;

/// <summary>
/// A siege event that offers a story mission (#1105 slice 4, docs/features/war-chronicle.md "Story offers"): the window lists an offer
/// while it is Active and not declined; a won mission resolves it Held at once; a window that closes with no assault resolves by its fate
/// outcome instead of Lapsed. The map still decides first: a real siege or capture resolves the window as it always did.
/// </summary>
[TestClass]
public class ChronicleStoryTests
{
    private ChronicleFixture _f = null!;
    private ChronicleService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _f = new ChronicleFixture();
        _sut = _f.CreateService();
    }

    private static ChronicleEventDefinition StorySiege(ChronicleEventState fate = ChronicleEventState.Held)
    {
        var definition = SiegeDef();
        definition.Story = new ChronicleStoryDefinition
        {
            MissionId = "helms_deep_defend",
            Fate = fate,
            FateText = "{=taom_wc_hornburg_fate}fate",
        };
        return definition;
    }

    // Shows the queued announcements unless told not to: an offer waits until the opening line that explains it has shown.
    private void OpenWindow(ChronicleEventDefinition definition, bool showAnnouncements = true)
    {
        _f.Events.Add(definition);
        _f.World.Day = 0;
        _sut.LaunchChronicle();
        _f.World.Day = 49;
        _sut.RunChronicleDay();
        Assert.AreEqual(ChronicleEventState.Active, _f.StateOf(definition.Id), "the window must be open for the test");
        if (showAnnouncements)
            DrainTexts();
    }

    private void CloseDay()
    {
        _f.World.Day = 69;
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
    public void GetStoryOffers_AnActiveStoryWindow_ListsItsMission()
    {
        OpenWindow(StorySiege());

        var offer = _sut.GetStoryOffers().Single();

        Assert.AreEqual("hornburg", offer.EventId);
        Assert.AreEqual("helms_deep_defend", offer.MissionId);
        Assert.AreEqual("town_V2", offer.TargetSettlementId);
        CollectionAssert.AreEqual(new[] { "isengard", "empire" }, offer.Attackers.ToArray());
    }

    [TestMethod]
    public void GetStoryOffers_TheMcmSwitchOff_ListsNothingAndTheFateStillDecides()
    {
        OpenWindow(StorySiege());
        _f.Settings.StoryOffersEnabled.Returns(false);

        Assert.AreEqual(0, _sut.GetStoryOffers().Count);
        CloseDay();
        Assert.AreEqual(ChronicleEventState.Held, _f.StateOf("hornburg"));
    }

    [TestMethod]
    public void GetStoryOffers_WhileAnAnnouncementWaits_ListsNothing()
    {
        OpenWindow(StorySiege(), showAnnouncements: false);

        Assert.IsTrue(_f.Queue.Count > 0, "the opening line is queued");
        Assert.AreEqual(0, _sut.GetStoryOffers().Count, "the offer must not show before the line that explains it");
        DrainTexts();
        Assert.AreEqual(1, _sut.GetStoryOffers().Count);
    }

    [TestMethod]
    public void GetStoryOffers_AWindowAlreadyAssaulted_ListsNothing()
    {
        // The map decided: an attacker's siege makes the window resolve Held at its close, or Fell when the fortress falls.
        OpenWindow(StorySiege());
        _f.World.Besiegers["town_V2"] = "isengard";
        _sut.OnTargetBesieged("town_V2");
        _f.World.Besiegers["town_V2"] = null;

        Assert.AreEqual(0, _sut.GetStoryOffers().Count);
    }

    [TestMethod]
    public void GetStoryOffers_WhileAnAttackerBesiegesWithNoEventSeen_ListsNothing()
    {
        OpenWindow(StorySiege());
        _f.World.Besiegers["town_V2"] = "isengard";

        Assert.AreEqual(0, _sut.GetStoryOffers().Count);
    }

    [TestMethod]
    public void GetStoryOffers_TheDefenderNoLongerHoldsTheTarget_ListsNothing()
    {
        OpenWindow(StorySiege());
        _f.World.Owners["town_V2"] = "isengard";

        Assert.AreEqual(0, _sut.GetStoryOffers().Count);
    }

    [TestMethod]
    public void ConfirmStoryWindow_AnOpenWindowTheDefenderHolds_IsTrue()
    {
        OpenWindow(StorySiege());

        Assert.IsTrue(_sut.ConfirmStoryWindow("hornburg"));
        Assert.AreEqual(ChronicleEventState.Active, _f.StateOf("hornburg"));
    }

    [TestMethod]
    public void ConfirmStoryWindow_AnOwnerChangeWithNoEvent_ResolvesTheWindowAndIsFalse()
    {
        // A clan that leaves its kingdom keeps its fiefs and raises no owner-change event; the transfer must not overwrite that owner.
        OpenWindow(StorySiege());
        _f.World.Owners["town_V2"] = "gondor";

        Assert.IsFalse(_sut.ConfirmStoryWindow("hornburg"));
        Assert.AreEqual(ChronicleEventState.Lapsed, _f.StateOf("hornburg"));
    }

    [TestMethod]
    public void ConfirmStoryWindow_TheWarHasEnded_IsFalse()
    {
        OpenWindow(StorySiege());
        _f.Phase = WarPhase.WarEnded;

        Assert.IsFalse(_sut.ConfirmStoryWindow("hornburg"));
    }

    [TestMethod]
    public void ConfirmStoryWindow_AWindowWithNoStory_IsFalse()
    {
        OpenWindow(SiegeDef());

        Assert.IsFalse(_sut.ConfirmStoryWindow("hornburg"));
    }

    [TestMethod]
    public void ResolveStoryWon_AnAttackerTookTheTargetWithNoEvent_ResolvesFellInstead()
    {
        OpenWindow(StorySiege());
        _f.World.Owners["town_V2"] = "isengard";

        Assert.IsFalse(_sut.ResolveStoryWon("hornburg"));
        Assert.AreEqual(ChronicleEventState.Fell, _f.StateOf("hornburg"));
    }

    [TestMethod]
    public void GetStoryOffers_TheWarHasEndedBeforeTheDailyStep_ListsNothing()
    {
        OpenWindow(StorySiege());
        _f.Phase = WarPhase.WarEnded;

        Assert.AreEqual(0, _sut.GetStoryOffers().Count, "no story battle after the war: the window is still Active in the store until the next guarded step");
    }

    [TestMethod]
    public void ResolveStoryWon_TheWarHasEnded_ChangesNothingAndSkipsTheWindow()
    {
        OpenWindow(StorySiege());
        _f.Phase = WarPhase.WarEnded;

        Assert.IsFalse(_sut.ResolveStoryWon("hornburg"));
        Assert.AreEqual(ChronicleEventState.Skipped, _f.StateOf("hornburg"));
    }

    [TestMethod]
    public void GetStoryOffers_AWindowWithNoStory_ListsNothing()
    {
        OpenWindow(SiegeDef());

        Assert.AreEqual(0, _sut.GetStoryOffers().Count);
    }

    [TestMethod]
    public void GetStoryOffers_APendingStoryEvent_ListsNothing()
    {
        _f.Events.Add(StorySiege());
        _f.World.Day = 0;
        _sut.LaunchChronicle();

        Assert.AreEqual(0, _sut.GetStoryOffers().Count);
    }

    [TestMethod]
    public void DeclineStory_AnOfferedWindow_DropsTheOfferAndKeepsTheWindowOpen()
    {
        OpenWindow(StorySiege());

        Assert.IsTrue(_sut.DeclineStory("hornburg"));

        Assert.AreEqual(0, _sut.GetStoryOffers().Count);
        Assert.IsTrue(_f.Store.Get("hornburg")!.StoryDeclined);
        Assert.AreEqual(ChronicleEventState.Active, _f.StateOf("hornburg"));
    }

    [TestMethod]
    public void DeclineStory_AnUnknownEvent_ReturnsFalse()
    {
        OpenWindow(StorySiege());

        Assert.IsFalse(_sut.DeclineStory("nope"));
    }

    [TestMethod]
    public void ResolveStoryWon_AnActiveWindow_ResolvesHeldAtOnceAndPaysHeld()
    {
        OpenWindow(StorySiege());

        Assert.IsTrue(_sut.ResolveStoryWon("hornburg"));

        Assert.AreEqual(ChronicleEventState.Held, _f.StateOf("hornburg"));
        Assert.IsTrue(_f.Volunteers("vlandia") > 1f, "the Held outcome pays the defender");
        Assert.AreEqual(0, _f.Focus.Snapshot().Count, "a resolved window drops its army focus");
    }

    [TestMethod]
    public void ResolveStoryWon_AResolvedWindow_ChangesNothing()
    {
        OpenWindow(StorySiege());
        _f.World.Owners["town_V2"] = "isengard";
        _sut.OnTargetOwnerChanged("town_V2");

        Assert.IsFalse(_sut.ResolveStoryWon("hornburg"));
        Assert.AreEqual(ChronicleEventState.Fell, _f.StateOf("hornburg"));
    }

    [TestMethod]
    public void ResolveStoryWon_AWindowWithNoStory_ChangesNothing()
    {
        OpenWindow(SiegeDef());

        Assert.IsFalse(_sut.ResolveStoryWon("hornburg"));
        Assert.AreEqual(ChronicleEventState.Active, _f.StateOf("hornburg"));
    }

    [TestMethod]
    public void Close_NoAssaultAndFateHeld_ResolvesHeldWithTheFateText()
    {
        OpenWindow(StorySiege());
        DrainTexts();

        CloseDay();

        Assert.AreEqual(ChronicleEventState.Held, _f.StateOf("hornburg"));
        CollectionAssert.Contains(DrainTexts(), "{=taom_wc_hornburg_fate}fate");
        Assert.IsTrue(_f.Volunteers("vlandia") > 1f, "fate pays the Held outcome");
    }

    [TestMethod]
    public void Close_NoAssaultAndADeclinedOffer_StillResolvesByFate()
    {
        OpenWindow(StorySiege());
        _sut.DeclineStory("hornburg");

        CloseDay();

        Assert.AreEqual(ChronicleEventState.Held, _f.StateOf("hornburg"));
    }

    [TestMethod]
    public void Close_NoAssaultAndFateLapsed_ResolvesLapsedWithTheLapsedText()
    {
        OpenWindow(StorySiege(ChronicleEventState.Lapsed));
        DrainTexts();

        CloseDay();

        Assert.AreEqual(ChronicleEventState.Lapsed, _f.StateOf("hornburg"));
        CollectionAssert.Contains(DrainTexts(), "{=taom_wc_hornburg_lapsed}lapsed");
    }

    [TestMethod]
    public void Close_AssaultedStoryWindow_ResolvesHeldWithTheHeldText()
    {
        OpenWindow(StorySiege());
        _f.World.Besiegers["town_V2"] = "isengard";
        _sut.OnTargetBesieged("town_V2");
        _f.World.Besiegers["town_V2"] = null;
        DrainTexts();

        CloseDay();

        Assert.AreEqual(ChronicleEventState.Held, _f.StateOf("hornburg"));
        CollectionAssert.Contains(DrainTexts(), "{=taom_wc_hornburg_held}held");
    }

    [TestMethod]
    public void Close_NoStoryAndNoAssault_StillLapses()
    {
        OpenWindow(SiegeDef());

        CloseDay();

        Assert.AreEqual(ChronicleEventState.Lapsed, _f.StateOf("hornburg"));
    }
}
