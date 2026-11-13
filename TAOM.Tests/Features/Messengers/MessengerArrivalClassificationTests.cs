using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TAOM.Features.Messengers;
using TAOM.Features.Messengers.Domain;

namespace TAOM.Tests.Features.Messengers;

/// <summary>
/// The arrival verdict, MessengerService.ClassifyArrival, held to the rule MessengerCampaignBehavior.HandleArrivedMessenger
/// executed before the move (7087fa633). That rule is the executed one, not its old comments: a prisoner is lost, not
/// deferred, and a wanderer that is Released, Traveling or Disabled is lost although the send check accepts it.
/// </summary>
[TestClass]
public class MessengerArrivalClassificationTests
{
    private static readonly MessengerHeroState[] AllStates = (MessengerHeroState[])Enum.GetValues(typeof(MessengerHeroState));
    private static readonly bool[] Both = { false, true };

    private MessengerService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _sut = new MessengerService(
            Substitute.For<IMessengerSettingsProvider>(),
            Substitute.For<IMessengerConfigProvider>(),
            Substitute.For<IMessengerStateStore>(),
            Substitute.For<IMessengerRandomSource>());
    }

    // Facts consistent with the engine for one state: IsDead, IsFugitive, IsPrisoner and IsActive read HeroState, and
    // IsAlive is !IsDead (v1.5.7 Hero.cs:303-319).
    private static HeroSnapshot Snap(MessengerHeroState state, bool wanderer = false, bool child = false, bool inPlayerParty = false) =>
        new HeroSnapshot("hero_1",
            isAlive: state != MessengerHeroState.Dead,
            isPrisoner: state == MessengerHeroState.Prisoner,
            isChild: child,
            isFugitive: state == MessengerHeroState.Fugitive,
            isActive: state == MessengerHeroState.Active,
            isWanderer: wanderer,
            isHumanPlayerCharacter: false,
            isInPlayerParty: inPlayerParty,
            heroState: state);

    private MessengerArrivalVerdict Classify(HeroSnapshot target, bool shares = false, bool playerAvailable = true, bool inMapEvent = false) =>
        _sut.ClassifyArrival(target, sharesMainHeroParty: shares, playerAvailable: playerAvailable, targetPartyInMapEvent: inMapEvent);

    // MessengerCampaignBehavior.cs:283-307 at 7087fa633, transcribed over the same engine identities.
    private static MessengerArrivalVerdict OldArrivalRule(
        MessengerHeroState state, bool wanderer, bool child, bool shares, bool playerAvailable, bool inMapEvent)
    {
        var isAlive = state != MessengerHeroState.Dead;
        var isActive = state == MessengerHeroState.Active;
        var isFugitive = state == MessengerHeroState.Fugitive;
        var isPrisoner = state == MessengerHeroState.Prisoner;
        if (!isAlive
            || state == MessengerHeroState.Disabled
            || isFugitive
            || (!isActive && !wanderer)
            || (!isActive && wanderer && state != MessengerHeroState.NotSpawned))
            return MessengerArrivalVerdict.Lost;
        if (shares)
            return MessengerArrivalVerdict.Confused;
        var targetAvailableNow = isAlive && !isPrisoner && !child && !inMapEvent;
        if (!playerAvailable || !targetAvailableNow)
            return MessengerArrivalVerdict.Wait;
        return MessengerArrivalVerdict.Ready;
    }

    [TestMethod]
    public void ClassifyArrival_EveryInputCombination_MatchesTheRuleTheBehaviorExecuted()
    {
        Assert.AreEqual(8, AllStates.Length, "MessengerHeroState must carry all eight engine states");
        var mismatches = new List<string>();
        var cells = 0;
        foreach (var state in AllStates)
        foreach (var wanderer in Both)
        foreach (var child in Both)
        foreach (var shares in Both)
        foreach (var playerAvailable in Both)
        foreach (var inMapEvent in Both)
        {
            cells++;
            var expected = OldArrivalRule(state, wanderer, child, shares, playerAvailable, inMapEvent);
            var actual = Classify(Snap(state, wanderer, child), shares, playerAvailable, inMapEvent);
            if (expected != actual)
                mismatches.Add($"{state} wanderer={wanderer} child={child} shares={shares} player={playerAvailable} battle={inMapEvent}: expected {expected}, got {actual}");
        }

        Assert.AreEqual(256, cells);
        Assert.AreEqual(0, mismatches.Count, string.Join(Environment.NewLine, mismatches));
    }

    [DataTestMethod]
    [DataRow(MessengerHeroState.NotSpawned, MessengerArrivalVerdict.Lost)]
    [DataRow(MessengerHeroState.Active, MessengerArrivalVerdict.Ready)]
    [DataRow(MessengerHeroState.Fugitive, MessengerArrivalVerdict.Lost)]
    [DataRow(MessengerHeroState.Prisoner, MessengerArrivalVerdict.Lost)]
    [DataRow(MessengerHeroState.Released, MessengerArrivalVerdict.Lost)]
    [DataRow(MessengerHeroState.Dead, MessengerArrivalVerdict.Lost)]
    [DataRow(MessengerHeroState.Disabled, MessengerArrivalVerdict.Lost)]
    [DataRow(MessengerHeroState.Traveling, MessengerArrivalVerdict.Lost)]
    public void ClassifyArrival_NonWandererInEachState_OnlyActiveIsReachable(MessengerHeroState state, MessengerArrivalVerdict expected)
        => Assert.AreEqual(expected, Classify(Snap(state)));

    [DataTestMethod]
    [DataRow(MessengerHeroState.NotSpawned, MessengerArrivalVerdict.Ready)]
    [DataRow(MessengerHeroState.Active, MessengerArrivalVerdict.Ready)]
    [DataRow(MessengerHeroState.Fugitive, MessengerArrivalVerdict.Lost)]
    [DataRow(MessengerHeroState.Prisoner, MessengerArrivalVerdict.Lost)]
    [DataRow(MessengerHeroState.Released, MessengerArrivalVerdict.Lost)]
    [DataRow(MessengerHeroState.Dead, MessengerArrivalVerdict.Lost)]
    [DataRow(MessengerHeroState.Disabled, MessengerArrivalVerdict.Lost)]
    [DataRow(MessengerHeroState.Traveling, MessengerArrivalVerdict.Lost)]
    public void ClassifyArrival_WandererInEachState_ActiveOrUnspawnedIsReachable(MessengerHeroState state, MessengerArrivalVerdict expected)
        => Assert.AreEqual(expected, Classify(Snap(state, wanderer: true)));

    [TestMethod]
    public void ClassifyArrival_Prisoner_IsLostNotDeferred()
    {
        Assert.AreEqual(MessengerArrivalVerdict.Lost, Classify(Snap(MessengerHeroState.Prisoner)));
        Assert.AreEqual(MessengerArrivalVerdict.Lost, Classify(Snap(MessengerHeroState.Prisoner, wanderer: true)));
        Assert.AreEqual(MessengerArrivalVerdict.Lost, Classify(Snap(MessengerHeroState.Prisoner), playerAvailable: false, inMapEvent: true));
    }

    [TestMethod]
    public void ClassifyArrival_DeadTargetSharingThePlayersParty_IsLostBeforeConfused()
        => Assert.AreEqual(MessengerArrivalVerdict.Lost, Classify(Snap(MessengerHeroState.Dead), shares: true));

    [TestMethod]
    public void ClassifyArrival_ReachableTargetSharingTheMainHerosParty_IsConfused()
        => Assert.AreEqual(MessengerArrivalVerdict.Confused, Classify(Snap(MessengerHeroState.Active), shares: true));

    [TestMethod]
    public void ClassifyArrival_SharedPartyWhileAChildThePlayerBusyAndABattle_IsConfusedBeforeWait()
        => Assert.AreEqual(MessengerArrivalVerdict.Confused,
            Classify(Snap(MessengerHeroState.Active, child: true), shares: true, playerAvailable: false, inMapEvent: true));

    [TestMethod]
    public void ClassifyArrival_UsesTheRawPartyEquality_NotTheSnapshotsIsInPlayerParty()
    {
        // Arrival's party test is true when both parties are null; IsInPlayerParty is false then, so it must not decide.
        Assert.AreEqual(MessengerArrivalVerdict.Ready, Classify(Snap(MessengerHeroState.Active, inPlayerParty: true), shares: false));
        Assert.AreEqual(MessengerArrivalVerdict.Confused, Classify(Snap(MessengerHeroState.Active, inPlayerParty: false), shares: true));
    }

    [TestMethod]
    public void ClassifyArrival_ReachableChild_Waits()
    {
        Assert.AreEqual(MessengerArrivalVerdict.Wait, Classify(Snap(MessengerHeroState.Active, child: true)));
        Assert.AreEqual(MessengerArrivalVerdict.Wait, Classify(Snap(MessengerHeroState.NotSpawned, wanderer: true, child: true)));
    }

    [TestMethod]
    public void ClassifyArrival_PlayerNotAvailable_Waits()
        => Assert.AreEqual(MessengerArrivalVerdict.Wait, Classify(Snap(MessengerHeroState.Active), playerAvailable: false));

    [TestMethod]
    public void ClassifyArrival_TargetPartyInAMapEvent_Waits()
        => Assert.AreEqual(MessengerArrivalVerdict.Wait, Classify(Snap(MessengerHeroState.Active), inMapEvent: true));

    [TestMethod]
    public void ClassifyArrival_ActiveAdultPlayerFreeNoBattleNoSharedParty_IsReady()
        => Assert.AreEqual(MessengerArrivalVerdict.Ready, Classify(Snap(MessengerHeroState.Active)));

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void MessengerHeroState_MatchesTheEngineCharacterStates_ByNameAndValue()
    {
        // The reader casts Hero.CharacterStates to MessengerHeroState by value, so the two must match one to one.
        CollectionAssert.AreEqual(Enum.GetNames(typeof(Hero.CharacterStates)), Enum.GetNames(typeof(MessengerHeroState)));
        foreach (Hero.CharacterStates s in Enum.GetValues(typeof(Hero.CharacterStates)))
            Assert.AreEqual(s.ToString(), ((MessengerHeroState)(int)s).ToString());
    }
}
