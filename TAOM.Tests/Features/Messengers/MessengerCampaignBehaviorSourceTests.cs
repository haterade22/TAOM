using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Messengers;

/// <summary>
/// Source pins on the messenger boundary (#898): the recovery when a conversation fails to open, the one offer shared by the
/// dialog, the send and the button, the in-person dialog path, the quote reads and the mixin's wiring. They only read
/// files, so they run without the game, unlike the RequiresGame class that holds the wiring checks.
/// </summary>
[TestClass]
public class MessengerCampaignBehaviorSourceTests
{
    private const string Path = "Main/Features/Messengers/MessengerCampaignBehavior.cs";

    [TestMethod]
    public void TheConversationStart_RecoversAndRethrows_WhenAnythingAfterOpenThrows()
    {
        var src = RepoPaths.ReadSource(Path, stripComments: true);

        StringAssert.Matches(src, new Regex(@"_conversation\.Open\(\);\s*try\s*\{"));
        StringAssert.Matches(src, new Regex(@"catch\s*\{\s*AbortConversation\(\);\s*throw;\s*\}"));
    }

    [TestMethod]
    public void TheNullMissionBranch_UsesTheSameRecovery()
    {
        var src = RepoPaths.ReadSource(Path, stripComments: true);

        StringAssert.Matches(src, new Regex(@"else\s*\{\s*_logger\.LogWarning\([^;]*\);\s*AbortConversation\(\);\s*\}"));
    }

    [TestMethod]
    public void AbortConversation_SchedulesTheSettlementCleanup_SoAFailedOpenFinishesTheEncounter()
    {
        var src = RepoPaths.ReadSource(Path, stripComments: true);

        StringAssert.Matches(src, new Regex(
            @"void AbortConversation\(\)\s*\{[^}]*_conversation\.Close\(\);\s*CampaignEvents\.TickEvent\.AddNonSerializedListener\(this,\s*CleanUpSettlementEncounter\);"));
    }

    [TestMethod]
    public void AbortConversation_KeepsTheOriginalPosition_SoTheCleanupCanRestoreIt()
    {
        var src = RepoPaths.ReadSource(Path, stripComments: true);

        var body = Regex.Match(src, @"void AbortConversation\(\)\s*\{[^}]*\}").Value;

        Assert.IsTrue(body.Length > 0, "AbortConversation not found");
        Assert.IsFalse(body.Contains("_originalPosition"), "AbortConversation must leave _originalPosition for CleanUpSettlementEncounter to restore");
    }

    [TestMethod]
    public void TheButtonAndTheSend_ReadTheSameOffer_FromTheOneTargetReader()
    {
        var behavior = RepoPaths.ReadSource(Path, stripComments: true);
        var mixin = RepoPaths.ReadSource("Client/Features/Messengers/UI/MessengerEncyclopediaMixin.cs", stripComments: true);

        StringAssert.Contains(behavior, "MessengerTargetReader.ReadSnapshot(");
        StringAssert.Contains(behavior, "MessengerTargetReader.ReadQuoteInput(");
        Assert.IsFalse(behavior.Contains("new HeroSnapshot("), "the behavior must not build its own snapshot");
        Assert.IsFalse(mixin.Contains("HeroSnapshot"), "the mixin must not build its own snapshot");
        StringAssert.Contains(mixin, "behavior.GetOffer(hero, inPerson: false)");
        Assert.AreEqual(1, Regex.Matches(behavior, @"GiveGoldAction\.ApplyBetweenCharacters\(Hero\.MainHero, null, offer\.Cost, false\)").Count, "the send pays the quoted cost");
    }

    [TestMethod]
    public void TheInPersonLine_KeepsItsOldBehaviour_OnlyTheRemoteSendRefusesTogetherness()
    {
        var src = RepoPaths.ReadSource(Path, stripComments: true);

        StringAssert.Matches(src, new Regex(@"DialogCondition_CanSend\(\)[^}]*GetOffer\(hero, inPerson: true\)"));
        StringAssert.Matches(src, new Regex(@"DialogConsequence_DispatchMessenger\(\)[^}]*SendMessenger\(hero, inPerson: true\)"));
        StringAssert.Matches(src, new Regex(@"DialogCondition_CanSendAtQuote\(\)[\s\S]*?GetOffer\(hero, inPerson: true\)"));
    }

    [TestMethod]
    public void TheSameArmyRule_MeansBothPartiesAreInTheLeadersGroup()
    {
        var reader = RepoPaths.ReadSource("Main/Features/Messengers/MessengerTargetReader.cs", stripComments: true);

        Assert.AreEqual(2, Regex.Matches(reader, @"DoesLeaderPartyAndAttachedPartiesContain\(").Count);
    }

    [TestMethod]
    public void TheGoldLine_AsksTheOfferOnce_NoInlineGoldCompare()
    {
        var src = RepoPaths.ReadSource(Path, stripComments: true);
        var body = Regex.Match(src, @"bool DialogCondition_CanSendAtQuote\(\)[\s\S]*?\n    \}").Value;

        StringAssert.Contains(body, "offer.CanSend");
        StringAssert.Contains(body, "offer.Cost");
        Assert.IsFalse(body.Contains("Hero.MainHero?.Gold"), "the gold check lives in the service, behind CanSend");
    }

    [TestMethod]
    public void TheButton_BuildsItsReasonFromTheOfferItAlreadyHas_AndPluralisesTheDays()
    {
        var mixin = RepoPaths.ReadSource("Client/Features/Messengers/UI/MessengerEncyclopediaMixin.cs", stripComments: true);

        StringAssert.Contains(mixin, "behavior.GetReason(offer, hero)");
        StringAssert.Contains(mixin, "{=taom_messenger_trip_days}The messenger will take about {DAYS} {?IS_PLURAL}days{?}day");
        StringAssert.Matches(mixin, new Regex(@"SetTextVariable\(""IS_PLURAL"",\s*offer\.Days\s*>\s*1\s*\?\s*1\s*:\s*0\)"));
    }

    [TestMethod]
    public void TheQuoteInput_ReadsWarStrangerAndSiege()
    {
        var reader = RepoPaths.ReadSource("Main/Features/Messengers/MessengerTargetReader.cs", stripComments: true);

        StringAssert.Contains(reader, "FactionManager.IsAtWarAgainstFaction(hero?.MapFaction, Hero.MainHero?.MapFaction)");
        StringAssert.Contains(reader, "!hero.HasMet");
        StringAssert.Contains(reader, "IsUnderSiege");
    }

    [TestMethod]
    public void TheEncyclopediaSend_IsRemote()
    {
        var mixin = RepoPaths.ReadSource("Client/Features/Messengers/UI/MessengerEncyclopediaMixin.cs", stripComments: true);

        StringAssert.Contains(mixin, "SendMessenger(hero, inPerson: false)");
    }

    [TestMethod]
    public void TheTargetReader_ReadsFactsOnly_TheServiceDecidesWhatTogethernessMeans()
    {
        var reader = RepoPaths.ReadSource("Main/Features/Messengers/MessengerTargetReader.cs", stripComments: true);
        var behavior = RepoPaths.ReadSource(Path, stripComments: true);

        Assert.IsFalse(reader.Contains("inPerson"), "inPerson is a service rule");
        StringAssert.Contains(behavior, "_service.GetOffer(");
    }

    [TestMethod]
    public void TheSentNotice_StillPromisesTheTravelDaysCap_NotTheStraightLineEstimate()
    {
        var src = RepoPaths.ReadSource(Path, stripComments: true);

        StringAssert.Contains(src, "sentText.SetTextVariable(\"DAYS\", _settings.MessengerTravelDays);");
    }

    [TestMethod]
    public void TheArrival_AsksTheServiceForItsVerdict_WithTheSnapshotAndTheEngineState()
    {
        var behavior = RepoPaths.ReadSource(Path, stripComments: true);
        var reader = RepoPaths.ReadSource("Main/Features/Messengers/MessengerTargetReader.cs", stripComments: true);
        var body = Regex.Match(behavior, @"ArrivedHandleOutcome HandleArrivedMessenger\([\s\S]*?\n    \}").Value;

        Assert.IsTrue(body.Length > 0, "HandleArrivedMessenger not found");
        StringAssert.Contains(body, "_service.ClassifyArrival(");
        StringAssert.Contains(body, "MessengerTargetReader.ReadSnapshot(target)");
        StringAssert.Contains(body, "playerAvailable: IsPlayerAvailable()");
        StringAssert.Contains(body, "targetPartyInMapEvent: target.PartyBelongedTo?.MapEvent != null");
        // #1133: a raw party equality is true when both parties are null (a captive player and a target with no party),
        // which dropped the messenger as "in your own party". The snapshot's test needs both parties to exist.
        Assert.IsFalse(body.Contains("== Hero.MainHero?.PartyBelongedTo"), "the arrival must not compare the two parties itself");
        StringAssert.Contains(reader, "Hero.MainHero?.PartyBelongedTo != null");
        StringAssert.Contains(reader, "heroState: (MessengerHeroState)(int)hero.HeroState");
        StringAssert.Contains(reader, "isSpecial: hero.IsSpecial");
    }

    [TestMethod]
    public void TheArrival_TellsThePlayerOnce_WhenTheMessengerWaitsForTheHero()
    {
        var behavior = RepoPaths.ReadSource(Path, stripComments: true);
        var notices = RepoPaths.ReadSource("Main/Features/Messengers/MessengerArrivalNotices.cs", stripComments: true);
        var body = Regex.Match(behavior, @"ArrivedHandleOutcome HandleArrivedMessenger\([\s\S]*?\n    \}").Value;

        // The latch is on the messenger, so one notice per messenger and per load; the other wait (a busy player) is silent.
        StringAssert.Matches(body, new Regex(
            @"if \(verdict == MessengerArrivalVerdict\.WaitForTarget && messenger\.TryMarkWaitNoticeShown\(\)\)\s*MessengerArrivalNotices\.Waiting\(target\);"));
        // Both waits keep the messenger for the next hour: only Ready goes on to the inquiry.
        StringAssert.Matches(body, new Regex(
            @"if \(verdict != MessengerArrivalVerdict\.Ready\)\s*return ArrivedHandleOutcome\.WaitForNextTick;"));
        // A log line, not an inquiry: the hourly tick must not open a dialog for a wait.
        StringAssert.Contains(notices, "{=taom_messenger_waiting}");
        StringAssert.Contains(notices, "InformationManager.DisplayMessage(");
        Assert.IsFalse(notices.Contains("ShowInquiry"), "the wait notice must not pause the game or need an answer");
    }
}
