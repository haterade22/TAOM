using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TAOM.Features.Messengers;
using TAOM.Features.Messengers.Domain;

namespace TAOM.Tests.Features.Messengers;

/// <summary>
/// The arrival verdict, MessengerService.ClassifyArrival, as the maintainer decided it for #1133: a messenger to a
/// prisoner is lost; a hero the engine will make Active again (Fugitive, Released, Traveling) is waited for; a target
/// with no party while the player has none (a captive player) is not "in the player's party". A fugitive or released
/// hero of the Special occupation is lost, because the engine's daily settle skips one.
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

    // Facts consistent with the engine for one state: IsDead, IsFugitive and IsPrisoner read HeroState, and IsAlive is
    // !IsDead (v1.5.7 Hero.cs:303-319).
    private static HeroSnapshot Snap(MessengerHeroState state, bool wanderer = false, bool child = false, bool inPlayerParty = false, bool special = false) =>
        new HeroSnapshot("hero_1",
            isAlive: state != MessengerHeroState.Dead,
            isPrisoner: state == MessengerHeroState.Prisoner,
            isChild: child,
            isFugitive: state == MessengerHeroState.Fugitive,
            heroState: state,
            isWanderer: wanderer,
            isHumanPlayerCharacter: false,
            isInPlayerParty: inPlayerParty,
            isSpecial: special);

    private MessengerArrivalVerdict Classify(HeroSnapshot target, bool playerAvailable = true, bool inMapEvent = false) =>
        _sut.ClassifyArrival(target, playerAvailable: playerAvailable, targetPartyInMapEvent: inMapEvent);

    [DataTestMethod]
    [DataRow(MessengerHeroState.NotSpawned, MessengerArrivalVerdict.Lost)]
    [DataRow(MessengerHeroState.Active, MessengerArrivalVerdict.Ready)]
    [DataRow(MessengerHeroState.Fugitive, MessengerArrivalVerdict.WaitForTarget)]
    [DataRow(MessengerHeroState.Prisoner, MessengerArrivalVerdict.Lost)]
    [DataRow(MessengerHeroState.Released, MessengerArrivalVerdict.WaitForTarget)]
    [DataRow(MessengerHeroState.Dead, MessengerArrivalVerdict.Lost)]
    [DataRow(MessengerHeroState.Disabled, MessengerArrivalVerdict.Lost)]
    [DataRow(MessengerHeroState.Traveling, MessengerArrivalVerdict.WaitForTarget)]
    public void ClassifyArrival_NonWandererInEachState(MessengerHeroState state, MessengerArrivalVerdict expected)
        => Assert.AreEqual(expected, Classify(Snap(state)));

    [DataTestMethod]
    [DataRow(MessengerHeroState.NotSpawned, MessengerArrivalVerdict.Ready)]
    [DataRow(MessengerHeroState.Active, MessengerArrivalVerdict.Ready)]
    [DataRow(MessengerHeroState.Fugitive, MessengerArrivalVerdict.WaitForTarget)]
    [DataRow(MessengerHeroState.Prisoner, MessengerArrivalVerdict.Lost)]
    [DataRow(MessengerHeroState.Released, MessengerArrivalVerdict.WaitForTarget)]
    [DataRow(MessengerHeroState.Dead, MessengerArrivalVerdict.Lost)]
    [DataRow(MessengerHeroState.Disabled, MessengerArrivalVerdict.Lost)]
    [DataRow(MessengerHeroState.Traveling, MessengerArrivalVerdict.WaitForTarget)]
    public void ClassifyArrival_WandererInEachState(MessengerHeroState state, MessengerArrivalVerdict expected)
        => Assert.AreEqual(expected, Classify(Snap(state, wanderer: true)));

    [TestMethod]
    public void ClassifyArrival_Prisoner_IsLostNotDeferred()
    {
        Assert.AreEqual(MessengerArrivalVerdict.Lost, Classify(Snap(MessengerHeroState.Prisoner)));
        Assert.AreEqual(MessengerArrivalVerdict.Lost, Classify(Snap(MessengerHeroState.Prisoner, wanderer: true)));
        Assert.AreEqual(MessengerArrivalVerdict.Lost, Classify(Snap(MessengerHeroState.Prisoner), playerAvailable: false, inMapEvent: true));
    }

    // Fugitive belongs here: a lord who escapes a lost battle is a fugitive (v1.5.7 MapEvent.cs:1846-1850), a journey the
    // engine cancels makes the traveller one (TeleportationCampaignBehavior.cs:371-374), and the engine settles a
    // fugitive on the same line as a released hero (HeroSpawnCampaignBehavior.cs:123).
    [DataTestMethod]
    [DataRow(MessengerHeroState.Fugitive)]
    [DataRow(MessengerHeroState.Released)]
    [DataRow(MessengerHeroState.Traveling)]
    public void ClassifyArrival_HeroTheEngineWillMakeActiveAgain_WaitsThoughThePlayerIsFree(MessengerHeroState state)
    {
        Assert.AreEqual(MessengerArrivalVerdict.WaitForTarget, Classify(Snap(state), playerAvailable: true, inMapEvent: false));
        Assert.AreEqual(MessengerArrivalVerdict.WaitForTarget, Classify(Snap(state, wanderer: true), playerAvailable: true, inMapEvent: false));
    }

    // The two waits are different verdicts because only this one is told to the player: it can last days, and the cause
    // is the hero, whatever the player does meanwhile.
    [DataTestMethod]
    [DataRow(MessengerHeroState.Fugitive)]
    [DataRow(MessengerHeroState.Released)]
    [DataRow(MessengerHeroState.Traveling)]
    public void ClassifyArrival_HeroNotOnTheMapYet_IsWaitForTargetWhateverElseHolds(MessengerHeroState state)
        => Assert.AreEqual(MessengerArrivalVerdict.WaitForTarget,
            Classify(Snap(state, child: true), playerAvailable: false, inMapEvent: true));

    // HeroSpawnCampaignBehavior.OnHeroDailyTick (v1.5.7, line 125), the daily settle for a fugitive or released hero,
    // skips a hero with IsSpecial. The other paths to Active need a hero who comes of age or a companion who leaves a
    // clan, so a wait for a Special adult would not end.
    [DataTestMethod]
    [DataRow(MessengerHeroState.Fugitive)]
    [DataRow(MessengerHeroState.Released)]
    public void ClassifyArrival_SpecialHeroTheEngineNeverSettles_IsLost(MessengerHeroState state)
    {
        Assert.AreEqual(MessengerArrivalVerdict.Lost, Classify(Snap(state, special: true)));
        Assert.AreEqual(MessengerArrivalVerdict.Lost, Classify(Snap(state, wanderer: true, special: true)));
    }

    // The journey of a Special hero ends like any other (TeleportationCampaignBehavior does not read the occupation).
    [DataTestMethod]
    [DataRow(MessengerHeroState.Active, MessengerArrivalVerdict.Ready)]
    [DataRow(MessengerHeroState.Traveling, MessengerArrivalVerdict.WaitForTarget)]
    [DataRow(MessengerHeroState.Prisoner, MessengerArrivalVerdict.Lost)]
    public void ClassifyArrival_SpecialHeroInAStateThatEnds_FollowsTheCommonRule(MessengerHeroState state, MessengerArrivalVerdict expected)
        => Assert.AreEqual(expected, Classify(Snap(state, special: true)));

    [TestMethod]
    public void ClassifyArrival_DeadTargetInThePlayersParty_IsLostBeforeConfused()
        => Assert.AreEqual(MessengerArrivalVerdict.Lost, Classify(Snap(MessengerHeroState.Dead, inPlayerParty: true)));

    [TestMethod]
    public void ClassifyArrival_ReachableTargetInThePlayersParty_IsConfused()
        => Assert.AreEqual(MessengerArrivalVerdict.Confused, Classify(Snap(MessengerHeroState.Active, inPlayerParty: true)));

    [TestMethod]
    public void ClassifyArrival_InThePlayersPartyWhileAChildThePlayerBusyAndABattle_IsConfusedBeforeWait()
        => Assert.AreEqual(MessengerArrivalVerdict.Confused,
            Classify(Snap(MessengerHeroState.Active, child: true, inPlayerParty: true), playerAvailable: false, inMapEvent: true));

    [TestMethod]
    public void ClassifyArrival_ReachableChild_Waits()
    {
        Assert.AreEqual(MessengerArrivalVerdict.Wait, Classify(Snap(MessengerHeroState.Active, child: true)));
        Assert.AreEqual(MessengerArrivalVerdict.Wait, Classify(Snap(MessengerHeroState.NotSpawned, wanderer: true, child: true)));
    }

    // The captive player is this case: the captivity menu makes the player unavailable, and the snapshot's
    // IsInPlayerParty is false because neither hero has a party (the reader's test is pinned in
    // MessengerCampaignBehaviorSourceTests).
    [TestMethod]
    public void ClassifyArrival_PlayerNotAvailable_Waits()
        => Assert.AreEqual(MessengerArrivalVerdict.Wait, Classify(Snap(MessengerHeroState.Active), playerAvailable: false));

    [TestMethod]
    public void ClassifyArrival_TargetPartyInAMapEvent_Waits()
        => Assert.AreEqual(MessengerArrivalVerdict.Wait, Classify(Snap(MessengerHeroState.Active), inMapEvent: true));

    [TestMethod]
    public void ClassifyArrival_ActiveAdultPlayerFreeNoBattleNotInTheParty_IsReady()
        => Assert.AreEqual(MessengerArrivalVerdict.Ready, Classify(Snap(MessengerHeroState.Active)));

    [TestMethod]
    public void ClassifyArrival_EveryInputCombination_LostDependsOnTheStateAloneAndReadyNeedsEveryCondition()
    {
        Assert.AreEqual(8, AllStates.Length, "MessengerHeroState must carry all eight engine states");
        var problems = new List<string>();
        var cells = 0;
        foreach (var state in AllStates)
        foreach (var wanderer in Both)
        foreach (var special in Both)
        foreach (var child in Both)
        foreach (var inParty in Both)
        foreach (var playerAvailable in Both)
        foreach (var inMapEvent in Both)
        {
            cells++;
            var verdict = Classify(Snap(state, wanderer, child, inParty, special), playerAvailable, inMapEvent);
            var cell = $"{state} wanderer={wanderer} special={special} child={child} inParty={inParty} player={playerAvailable} battle={inMapEvent}";

            // The other inputs never rescue a hero that cannot receive, and never lose one that can.
            var lostAlone = Classify(Snap(state, wanderer, special: special)) == MessengerArrivalVerdict.Lost;
            if ((verdict == MessengerArrivalVerdict.Lost) != lostAlone)
                problems.Add($"{cell}: Lost must depend on the state, the wanderer flag and the Special flag alone, got {verdict}");

            var deliverableNow = state == MessengerHeroState.Active || (wanderer && state == MessengerHeroState.NotSpawned);
            if (verdict == MessengerArrivalVerdict.Ready && !(deliverableNow && !child && !inParty && playerAvailable && !inMapEvent))
                problems.Add($"{cell}: Ready with a condition unmet");

            // A wait the engine never ends is the defect the Special rule closes.
            var engineEndsTheState = state == MessengerHeroState.Traveling
                                     || (!special && (state == MessengerHeroState.Fugitive || state == MessengerHeroState.Released));
            if ((verdict == MessengerArrivalVerdict.WaitForTarget) != (engineEndsTheState && !inParty))
                problems.Add($"{cell}: WaitForTarget must mean a state the engine ends, got {verdict}");
        }

        Assert.AreEqual(512, cells);
        Assert.AreEqual(0, problems.Count, string.Join(Environment.NewLine, problems));
    }

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
