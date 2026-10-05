using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Domain;
using TAOM.Features.SiegeForces;
using TAOM.Features.SiegeForces.Domain;
using static TAOM.Tests.Features.SiegeForces.SiegeForcesFixtures;

namespace TAOM.Tests.Features.SiegeForces;

/// <summary>
/// The siege troop picker's pure rules: who is offered, what starts ticked, how a picked roster maps back to
/// parties, which window entries a plan drops, and how the spawn totals shrink to match. The expected values are
/// worked by hand from the design's example (the player leads an army with one vassal attached), never derived
/// from the code under test.
/// </summary>
[TestClass]
public class SiegeForcesRulesTests
{
    private static string[] Ids(IEnumerable<SiegeParty> parties) => parties.Select(p => p.Id).ToArray();

    private static string[] Rows(PickerRequest request) =>
        request.Rows.Select(r => $"{r.CharacterId}:{r.Number}:{r.Wounded}:{r.Initial}").ToArray();

    private static IReadOnlyDictionary<string, int> KeepOf(SiegeForcesPlan plan, string partyId)
    {
        Assert.IsTrue(plan.TryGetKeep(partyId, out var keep), $"party '{partyId}' is not in the plan");
        return keep;
    }

    // --- PartiesInScope --------------------------------------------------------------------------------------

    [TestMethod]
    public void PartiesInScope_ThePlayerLeadsTheArmy_IsTheMainPartyThenEveryArmyParty()
    {
        var snapshot = Snapshot(new[]
        {
            MainParty(),
            Party("vassalA", new[] { Troop(Uruk) }, inArmy: true),
            Party("vassalB", new[] { Troop(Uruk) }, inArmy: true),
        }, leadsArmy: true);

        CollectionAssert.AreEqual(new[] { "main", "vassalA", "vassalB" }, Ids(SiegeForcesRules.PartiesInScope(snapshot)));
    }

    [TestMethod]
    public void PartiesInScope_ThePlayerIsInAnotherLordsArmy_IsOnlyTheMainParty()
    {
        // The army's other parties carry the in-army flag, but the player does not lead it: they are the lord's.
        var snapshot = Snapshot(new[]
        {
            MainParty(),
            Party("lordA", new[] { Troop(Uruk) }, inArmy: true),
            Party("lordB", new[] { Troop(Uruk) }, inArmy: true),
        }, leadsArmy: false);

        CollectionAssert.AreEqual(new[] { "main" }, Ids(SiegeForcesRules.PartiesInScope(snapshot)));
    }

    [TestMethod]
    public void PartiesInScope_AnAlliedLordOutsideTheArmy_IsExcluded()
    {
        var snapshot = Snapshot(new[]
        {
            MainParty(),
            Party("ally", new[] { Troop(Uruk) }),
        }, leadsArmy: true);

        CollectionAssert.AreEqual(new[] { "main" }, Ids(SiegeForcesRules.PartiesInScope(snapshot)));
    }

    [TestMethod]
    public void PartiesInScope_DefendingTheOwnFief_AddsTheGarrison()
    {
        var snapshot = Snapshot(new[]
        {
            MainParty(),
            Party("garrison", new[] { Troop(Uruk, 40) }, garrison: true),
        }, playerIsAttacker: false, ownFief: true);

        CollectionAssert.AreEqual(new[] { "main", "garrison" }, Ids(SiegeForcesRules.PartiesInScope(snapshot)));
    }

    [TestMethod]
    public void PartiesInScope_AGarrisonOfAFiefThatIsNotTheirs_IsExcluded()
    {
        // The player defends a lord's castle: the garrison is the lord's, however it is flagged.
        var snapshot = Snapshot(new[]
        {
            MainParty(),
            Party("garrison", new[] { Troop(Uruk, 40) }, garrison: true),
        }, playerIsAttacker: false, ownFief: false);

        CollectionAssert.AreEqual(new[] { "main" }, Ids(SiegeForcesRules.PartiesInScope(snapshot)));
    }

    [TestMethod]
    public void PartiesInScope_AnOwnFiefDefenceWithAnArmy_IsMainThenArmyThenGarrison_WhateverTheSnapshotOrder()
    {
        var snapshot = Snapshot(new[]
        {
            Party("garrison", new[] { Troop(Uruk) }, garrison: true),
            Party("vassalA", new[] { Troop(Uruk) }, inArmy: true),
            MainParty(),
            Party("vassalB", new[] { Troop(Uruk) }, inArmy: true),
        }, playerIsAttacker: false, leadsArmy: true, ownFief: true);

        CollectionAssert.AreEqual(new[] { "main", "vassalA", "vassalB", "garrison" },
            Ids(SiegeForcesRules.PartiesInScope(snapshot)));
    }

    [TestMethod]
    public void PartiesInScope_TheMainPartyIsInTheArmyToo_ButIsListedOnce()
    {
        var snapshot = Snapshot(new[] { MainParty() }, leadsArmy: true);

        CollectionAssert.AreEqual(new[] { "main" }, Ids(SiegeForcesRules.PartiesInScope(snapshot)));
    }

    [TestMethod]
    public void PartiesInScope_ASnapshotWithNoParties_IsEmpty()
    {
        Assert.AreEqual(0, SiegeForcesRules.PartiesInScope(Snapshot(Array.Empty<SiegeParty>())).Count);
    }

    // --- BuildRequest ----------------------------------------------------------------------------------------

    [TestMethod]
    public void BuildRequest_TheWorkedExample_ListsOneAggregatedRowPerCharacter_WithTheTrollsUnticked()
    {
        var request = SiegeForcesRules.BuildRequest(WorkedExample(), startOversizedUnticked: true, FakeRaceManager.WithTrolls());

        Assert.IsNotNull(request);
        CollectionAssert.AreEqual(
            new[] { "main_hero:1:0:1", "uruk_hai:30:0:30", "hill_troll:2:0:0", "cave_troll:3:0:0" },
            Rows(request!));
        Assert.AreEqual(36, request!.Max, "the healthy total: 1 + 30 + 2 + 3");
        Assert.AreEqual(1, request.Min);
    }

    [TestMethod]
    public void BuildRequest_OversizedTroopsStartTicked_WhenTheConfigSaysSo()
    {
        var request = SiegeForcesRules.BuildRequest(WorkedExample(), startOversizedUnticked: false, FakeRaceManager.WithTrolls());

        CollectionAssert.AreEqual(
            new[] { "main_hero:1:0:1", "uruk_hai:30:0:30", "hill_troll:2:0:2", "cave_troll:3:0:3" },
            Rows(request!));
    }

    [TestMethod]
    public void BuildRequest_ATrollRacePlayer_StaysTicked()
    {
        var snapshot = Snapshot(new[]
        {
            Party("main", new[]
            {
                Troop(Player, 1, race: FakeRaceManager.HillTroll, isPlayer: true),
                Troop(Uruk, 5),
            }, main: true),
        });

        var request = SiegeForcesRules.BuildRequest(snapshot, true, FakeRaceManager.WithTrolls());

        CollectionAssert.AreEqual(new[] { "main_hero:1:0:1", "uruk_hai:5:0:5" }, Rows(request!));
    }

    [TestMethod]
    public void BuildRequest_ATrollRaceHero_StartsUnticked()
    {
        var snapshot = Snapshot(new[]
        {
            Party("main", new[]
            {
                Troop(Player, 1, isPlayer: true),
                Troop("lord_troll_captain", 1, race: FakeRaceManager.HillTroll),
                Troop(Uruk, 5),
            }, main: true),
        });

        var request = SiegeForcesRules.BuildRequest(snapshot, true, FakeRaceManager.WithTrolls());

        CollectionAssert.AreEqual(new[] { "main_hero:1:0:1", "lord_troll_captain:1:0:0", "uruk_hai:5:0:5" }, Rows(request!));
    }

    [TestMethod]
    public void BuildRequest_AnOversizedRaceTheTableDoesNotHold_NeverMakesAHumanOversized()
    {
        var snapshot = Snapshot(new[]
        {
            Party("main", new[] { Troop(Player, 1, isPlayer: true), Troop(Uruk, 5, race: FakeRaceManager.Human) }, main: true),
        });
        // The worst lookup: a name the table does not hold answers the human id (0). Validity is checked first.
        var races = Substitute.For<IRaceManager>();
        races.IsValidRaceName(Arg.Any<string>()).Returns(false);
        races.GetRaceIdFromName(Arg.Any<string>()).Returns(FakeRaceManager.Human);

        var request = SiegeForcesRules.BuildRequest(snapshot, true, races);

        CollectionAssert.AreEqual(new[] { "main_hero:1:0:1", "uruk_hai:5:0:5" }, Rows(request!));
    }

    [TestMethod]
    public void BuildRequest_ResolvesTheOversizedRacesOnce_NotPerTroop()
    {
        var snapshot = Snapshot(new[]
        {
            Party("main", new[]
            {
                Troop(Player, 1, isPlayer: true), Troop(Uruk, 5), Troop("warg_rider", 2, race: FakeRaceManager.Warg),
                Troop(HillTrollTroop, 1, race: FakeRaceManager.HillTroll), Troop(CaveTrollTroop, 1, race: FakeRaceManager.CaveTroll),
            }, main: true),
        });
        var races = FakeRaceManager.WithTrolls();

        SiegeForcesRules.BuildRequest(snapshot, true, races);

        Assert.AreEqual(2, races.IdLookups, "one lookup per oversized race name, however many troops there are");
        Assert.AreEqual(0, races.NameLookups);
    }

    [TestMethod]
    public void BuildRequest_TheSwitchIsOff_ResolvesNoRace()
    {
        var snapshot = Snapshot(new[]
        {
            Party("main", new[] { Troop(Player, 1, isPlayer: true), Troop(HillTrollTroop, 1, race: FakeRaceManager.HillTroll) }, main: true),
        });
        var races = FakeRaceManager.WithTrolls();

        var request = SiegeForcesRules.BuildRequest(snapshot, false, races);

        Assert.AreEqual(0, races.IdLookups);
        CollectionAssert.AreEqual(new[] { "main_hero:1:0:1", "hill_troll:1:0:1" }, Rows(request!));
    }

    [TestMethod]
    public void BuildRequest_WoundedTroops_AreShown_ButNotTicked_AndAreNotInTheMax()
    {
        var snapshot = Snapshot(new[]
        {
            Party("main", new[] { Troop(Player, 1, isPlayer: true), Troop(Uruk, 10, wounded: 4) }, main: true),
        });

        var request = SiegeForcesRules.BuildRequest(snapshot, true, FakeRaceManager.WithTrolls());

        CollectionAssert.AreEqual(new[] { "main_hero:1:0:1", "uruk_hai:10:4:6" }, Rows(request!));
        Assert.AreEqual(7, request!.Max, "1 player + 6 healthy; the 4 wounded can never fight");
    }

    [TestMethod]
    public void BuildRequest_AWoundedHero_IsShown_ButUnticked()
    {
        var snapshot = Snapshot(new[]
        {
            Party("main", new[]
            {
                Troop(Player, 1, isPlayer: true),
                Troop("lord_hurt", 1, wounded: 1),
                Troop(Uruk, 3),
            }, main: true),
        });

        var request = SiegeForcesRules.BuildRequest(snapshot, true, FakeRaceManager.WithTrolls());

        CollectionAssert.AreEqual(new[] { "main_hero:1:0:1", "lord_hurt:1:1:0", "uruk_hai:3:0:3" }, Rows(request!));
    }

    [TestMethod]
    public void BuildRequest_MoreWoundedThanMen_CountsNoHealthyTroops()
    {
        var snapshot = Snapshot(new[]
        {
            Party("main", new[] { Troop(Player, 1, isPlayer: true), Troop(Uruk, 3, wounded: 5), Troop("other", 2) }, main: true),
        });

        var request = SiegeForcesRules.BuildRequest(snapshot, true, FakeRaceManager.WithTrolls());

        CollectionAssert.AreEqual(new[] { "main_hero:1:0:1", "uruk_hai:3:5:0", "other:2:0:2" }, Rows(request!));
        Assert.AreEqual(3, request!.Max);
    }

    [TestMethod]
    public void BuildRequest_OnlyThePlayer_HasNothingToChoose()
    {
        var snapshot = Snapshot(new[] { Party("main", new[] { Troop(Player, 1, isPlayer: true) }, main: true) });

        Assert.IsNull(SiegeForcesRules.BuildRequest(snapshot, true, FakeRaceManager.WithTrolls()));
    }

    [TestMethod]
    public void BuildRequest_EveryoneElseIsWounded_HasNothingToChoose()
    {
        var snapshot = Snapshot(new[]
        {
            Party("main", new[] { Troop(Player, 1, isPlayer: true), Troop(Uruk, 5, wounded: 5) }, main: true),
        });

        Assert.IsNull(SiegeForcesRules.BuildRequest(snapshot, true, FakeRaceManager.WithTrolls()));
    }

    [TestMethod]
    public void BuildRequest_OnlyTrollsBesideThePlayer_StillOffersThePickerSoTheyCanBeTicked()
    {
        var snapshot = Snapshot(new[]
        {
            Party("main", new[]
            {
                Troop(Player, 1, isPlayer: true),
                Troop(HillTrollTroop, 4, race: FakeRaceManager.HillTroll),
            }, main: true),
        });

        var request = SiegeForcesRules.BuildRequest(snapshot, true, FakeRaceManager.WithTrolls());

        Assert.IsNotNull(request);
        CollectionAssert.AreEqual(new[] { "main_hero:1:0:1", "hill_troll:4:0:0" }, Rows(request!));
    }

    [TestMethod]
    public void BuildRequest_ASnapshotWithNoParties_HasNothingToChoose()
    {
        Assert.IsNull(SiegeForcesRules.BuildRequest(Snapshot(Array.Empty<SiegeParty>()), true, FakeRaceManager.WithTrolls()));
    }

    [TestMethod]
    public void BuildRequest_LeavesOutThePartiesTheyDoNotCommand()
    {
        var snapshot = Snapshot(new[]
        {
            MainParty(),
            Party("ally", new[] { Troop("ally_knight", 50) }),
        }, leadsArmy: true);

        var request = SiegeForcesRules.BuildRequest(snapshot, true, FakeRaceManager.WithTrolls());

        CollectionAssert.DoesNotContain(request!.Rows.Select(r => r.CharacterId).ToList(), "ally_knight");
        Assert.AreEqual(13, request.Max, "1 + 10 + 2 of the main party alone");
    }

    [TestMethod]
    public void BuildRequest_RowsFollowTheFillOrder_TheMainPartyFirst()
    {
        var snapshot = Snapshot(new[]
        {
            Party("vassalA", new[] { Troop("vassal_pikeman", 5) }, inArmy: true),
            Party("main", new[] { Troop(Player, 1, isPlayer: true), Troop(Uruk, 2) }, main: true),
        }, leadsArmy: true);

        var request = SiegeForcesRules.BuildRequest(snapshot, true, FakeRaceManager.WithTrolls());

        CollectionAssert.AreEqual(new[] { "main_hero", "uruk_hai", "vassal_pikeman" },
            request!.Rows.Select(r => r.CharacterId).ToArray());
    }

    [TestMethod]
    public void BuildRequest_ARowCarriesTheSourceTokenOfItsFirstSighting()
    {
        var first = new object();
        var second = new object();
        var snapshot = Snapshot(new[]
        {
            Party("main", new[] { Troop(Player, 1, isPlayer: true), Troop(Uruk, 2, source: first) }, main: true),
            Party("vassalA", new[] { Troop(Uruk, 3, source: second) }, inArmy: true),
        }, leadsArmy: true);

        var request = SiegeForcesRules.BuildRequest(snapshot, true, FakeRaceManager.WithTrolls());

        Assert.AreSame(first, request!.Rows.Single(r => r.CharacterId == Uruk).Source);
    }

    // --- BuildPlan -------------------------------------------------------------------------------------------

    [TestMethod]
    public void BuildPlan_TheWorkedExample_KeepsWhatTheDesignTableSays()
    {
        var plan = SiegeForcesRules.BuildPlan(WorkedExample(), WorkedSelection());

        var main = KeepOf(plan, "main");
        Assert.AreEqual(1, main[Player]);
        Assert.AreEqual(10, main[Uruk]);
        Assert.AreEqual(1, main[HillTrollTroop], "one of the two hill trolls was picked; the main party fills first");

        var vassal = KeepOf(plan, "vassalA");
        Assert.AreEqual(15, vassal[Uruk], "25 picked: 10 from the main party, 15 from the vassal; 5 are left out");
        Assert.AreEqual(0, vassal[CaveTrollTroop], "no cave troll was picked: all 3 are left out");
    }

    [TestMethod]
    public void BuildPlan_TheWorkedExample_LeavesNineOfThirtySixOut()
    {
        // Left out: 1 hill troll (main) + 5 Uruk-hai and 3 cave trolls (vassal) = 9 of 36 healthy.
        var snapshot = WorkedExample();
        var plan = SiegeForcesRules.BuildPlan(snapshot, WorkedSelection());

        var healthy = snapshot.Parties.Sum(p => p.Troops.Sum(t => t.Healthy));
        var kept = snapshot.Parties.Sum(p => KeepOf(plan, p.Id).Values.Sum());

        Assert.AreEqual(36, healthy);
        Assert.AreEqual(27, kept);
    }

    [TestMethod]
    public void BuildPlan_EveryoneSelected_KeepsEveryone()
    {
        var plan = SiegeForcesRules.BuildPlan(WorkedExample(), EveryoneSelected());

        Assert.AreEqual(10, KeepOf(plan, "main")[Uruk]);
        Assert.AreEqual(2, KeepOf(plan, "main")[HillTrollTroop]);
        Assert.AreEqual(20, KeepOf(plan, "vassalA")[Uruk]);
        Assert.AreEqual(3, KeepOf(plan, "vassalA")[CaveTrollTroop]);
    }

    [TestMethod]
    public void BuildPlan_ASelectionAboveTheHealthyCount_ClampsToIt()
    {
        var selected = EveryoneSelected();
        selected[Uruk] = 999;

        var plan = SiegeForcesRules.BuildPlan(WorkedExample(), selected);

        Assert.AreEqual(10, KeepOf(plan, "main")[Uruk]);
        Assert.AreEqual(20, KeepOf(plan, "vassalA")[Uruk]);
    }

    [TestMethod]
    public void BuildPlan_AnIdTheSnapshotDoesNotHold_IsIgnored()
    {
        var selected = WorkedSelection();
        selected["a_ghost_troop"] = 40;

        var plan = SiegeForcesRules.BuildPlan(WorkedExample(), selected);

        Assert.IsFalse(KeepOf(plan, "main").ContainsKey("a_ghost_troop"));
        Assert.IsFalse(KeepOf(plan, "vassalA").ContainsKey("a_ghost_troop"));
    }

    [TestMethod]
    public void BuildPlan_ThePlayerIsKept_EvenWhenTheSelectionOmitsHim()
    {
        var selected = WorkedSelection();
        selected.Remove(Player);

        var plan = SiegeForcesRules.BuildPlan(WorkedExample(), selected);

        Assert.AreEqual(1, KeepOf(plan, "main")[Player]);
    }

    [TestMethod]
    public void BuildPlan_ThePlayerIsKept_EvenWhenTheSelectionSaysZero()
    {
        var selected = WorkedSelection();
        selected[Player] = 0;

        var plan = SiegeForcesRules.BuildPlan(WorkedExample(), selected);

        Assert.AreEqual(1, KeepOf(plan, "main")[Player]);
    }

    [TestMethod]
    public void BuildPlan_AnotherPartyIsFilledOnlyAfterTheMainParty()
    {
        var selected = EveryoneSelected();
        selected[Uruk] = 12;

        var plan = SiegeForcesRules.BuildPlan(WorkedExample(), selected);

        Assert.AreEqual(10, KeepOf(plan, "main")[Uruk], "the main party's own troops fight first");
        Assert.AreEqual(2, KeepOf(plan, "vassalA")[Uruk]);
    }

    [TestMethod]
    public void BuildPlan_TheGarrisonIsFilledLast()
    {
        var snapshot = Snapshot(new[]
        {
            Party("garrison", new[] { Troop(Uruk, 40) }, garrison: true),
            Party("vassalA", new[] { Troop(Uruk, 20) }, inArmy: true),
            Party("main", new[] { Troop(Player, 1, isPlayer: true), Troop(Uruk, 10) }, main: true),
        }, playerIsAttacker: false, leadsArmy: true, ownFief: true);

        var plan = SiegeForcesRules.BuildPlan(snapshot, new Dictionary<string, int> { [Player] = 1, [Uruk] = 35 });

        Assert.AreEqual(10, KeepOf(plan, "main")[Uruk]);
        Assert.AreEqual(20, KeepOf(plan, "vassalA")[Uruk]);
        Assert.AreEqual(5, KeepOf(plan, "garrison")[Uruk], "35 picked: the garrison gets what the main party and the army left");
    }

    [TestMethod]
    public void BuildPlan_APartyOutOfScope_IsNotInThePlan()
    {
        var snapshot = Snapshot(new[]
        {
            MainParty(),
            Party("ally", new[] { Troop(Uruk, 8) }),
        }, leadsArmy: true);

        var plan = SiegeForcesRules.BuildPlan(snapshot, EveryoneSelected());

        Assert.IsTrue(plan.TryGetKeep("main", out _));
        Assert.IsFalse(plan.TryGetKeep("ally", out _), "a party the player does not command keeps everything");
    }

    [TestMethod]
    public void BuildPlan_ACharacterPickedZeroTimes_IsKeptZeroTimesEverywhere()
    {
        var selected = EveryoneSelected();
        selected[Uruk] = 0;

        var plan = SiegeForcesRules.BuildPlan(WorkedExample(), selected);

        Assert.AreEqual(0, KeepOf(plan, "main")[Uruk]);
        Assert.AreEqual(0, KeepOf(plan, "vassalA")[Uruk]);
    }

    [TestMethod]
    public void BuildPlan_ANegativeSelection_IsTreatedAsZero()
    {
        var selected = EveryoneSelected();
        selected[Uruk] = -5;

        var plan = SiegeForcesRules.BuildPlan(WorkedExample(), selected);

        Assert.AreEqual(0, KeepOf(plan, "main")[Uruk]);
    }

    [TestMethod]
    public void BuildPlan_WoundedTroops_AreNotKept_AndNeverCountAsHealthy()
    {
        var snapshot = Snapshot(new[]
        {
            Party("main", new[] { Troop(Player, 1, isPlayer: true), Troop(Uruk, 10, wounded: 4) }, main: true),
        });

        var plan = SiegeForcesRules.BuildPlan(snapshot, new Dictionary<string, int> { [Player] = 1, [Uruk] = 10 });

        Assert.AreEqual(6, KeepOf(plan, "main")[Uruk], "only the 6 healthy can be kept");
    }

    [TestMethod]
    public void BuildPlan_ASelectionIsNotChanged()
    {
        var selected = WorkedSelection();
        var before = selected.OrderBy(p => p.Key).Select(p => p.Key + "=" + p.Value).ToArray();

        SiegeForcesRules.BuildPlan(WorkedExample(), selected);

        CollectionAssert.AreEqual(before, selected.OrderBy(p => p.Key).Select(p => p.Key + "=" + p.Value).ToArray());
    }

    [TestMethod]
    public void BuildPlan_NullSelection_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(() => SiegeForcesRules.BuildPlan(WorkedExample(), null!));
    }

    [TestMethod]
    public void BuildPlan_NullSnapshot_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(() => SiegeForcesRules.BuildPlan(null!, WorkedSelection()));
    }

    // --- Keeps -----------------------------------------------------------------------------------------------

    private static SiegeForcesPlan PlanKeeping(string partyId, string characterId, int keep) =>
        new(new Dictionary<string, IReadOnlyDictionary<string, int>>
        {
            [partyId] = new Dictionary<string, int> { [characterId] = keep },
        });

    [TestMethod]
    public void Keeps_KeepsTheFirstKOfACharacter_ThenDropsTheRest()
    {
        var plan = PlanKeeping("main", Uruk, keep: 3);

        Assert.IsTrue(SiegeForcesRules.Keeps(plan, "main", Uruk, false, keptSoFar: 0));
        Assert.IsTrue(SiegeForcesRules.Keeps(plan, "main", Uruk, false, keptSoFar: 1));
        Assert.IsTrue(SiegeForcesRules.Keeps(plan, "main", Uruk, false, keptSoFar: 2));
        Assert.IsFalse(SiegeForcesRules.Keeps(plan, "main", Uruk, false, keptSoFar: 3));
        Assert.IsFalse(SiegeForcesRules.Keeps(plan, "main", Uruk, false, keptSoFar: 4));
    }

    [TestMethod]
    public void Keeps_AZeroLimit_DropsEveryInstance()
    {
        var plan = PlanKeeping("main", Uruk, keep: 0);

        Assert.IsFalse(SiegeForcesRules.Keeps(plan, "main", Uruk, false, keptSoFar: 0));
    }

    [TestMethod]
    public void Keeps_NeverDropsThePlayer_WhateverThePlanSays()
    {
        var plan = PlanKeeping("main", Player, keep: 0);

        Assert.IsTrue(SiegeForcesRules.Keeps(plan, "main", Player, isPlayerCharacter: true, keptSoFar: 5));
    }

    [TestMethod]
    public void Keeps_APartyNotInThePlan_KeepsEverything()
    {
        var plan = PlanKeeping("main", Uruk, keep: 0);

        Assert.IsTrue(SiegeForcesRules.Keeps(plan, "ally", Uruk, false, keptSoFar: 99));
    }

    [TestMethod]
    public void Keeps_ACharacterThePlanRowDoesNotName_IsKept()
    {
        // An entry the plan never saw (a roster that changed under it) is left to vanilla, never dropped.
        var plan = PlanKeeping("main", Uruk, keep: 0);

        Assert.IsTrue(SiegeForcesRules.Keeps(plan, "main", "a_new_recruit", false, keptSoFar: 0));
    }

    [TestMethod]
    public void Keeps_ATroopWithNoId_IsKept()
    {
        var plan = PlanKeeping("main", Uruk, keep: 0);

        Assert.IsTrue(SiegeForcesRules.Keeps(plan, "main", null, false, keptSoFar: 0));
    }

    // --- FitTotals -------------------------------------------------------------------------------------------

    [TestMethod]
    public void FitTotals_SubtractsTheDroppedTroops_AndClampsTheInitialSpawnToTheNewTotal()
    {
        var (total, initial) = SiegeForcesRules.FitTotals(total: 412, initial: 412, dropped: 9, readyCount: null);

        Assert.AreEqual(403, total);
        Assert.AreEqual(403, initial);
    }

    [TestMethod]
    public void FitTotals_AnInitialSpawnBelowTheTotal_IsKept()
    {
        var (total, initial) = SiegeForcesRules.FitTotals(total: 100, initial: 50, dropped: 20, readyCount: null);

        Assert.AreEqual(80, total);
        Assert.AreEqual(50, initial);
    }

    [TestMethod]
    public void FitTotals_MoreDroppedThanTheTotal_NeverGoesBelowZero()
    {
        var (total, initial) = SiegeForcesRules.FitTotals(total: 5, initial: 5, dropped: 50, readyCount: null);

        Assert.AreEqual(0, total);
        Assert.AreEqual(0, initial);
    }

    [TestMethod]
    public void FitTotals_TheReadyListIsShorterThanTheArithmetic_ClampsToIt()
    {
        // The hero-drift case: a hero healed inside one continued map event is counted in the involved men but
        // missing from the ready list. 100 - 20 = 80, but only 75 can ever be supplied, so the initial spawn must
        // not ask for more or CheckDeployment waits for troops that never come.
        var (total, initial) = SiegeForcesRules.FitTotals(total: 100, initial: 100, dropped: 20, readyCount: 75);

        Assert.AreEqual(75, total);
        Assert.AreEqual(75, initial);
    }

    [TestMethod]
    public void FitTotals_TheReadyListIsLongerThanTheArithmetic_DoesNotRaiseTheTotal()
    {
        var (total, initial) = SiegeForcesRules.FitTotals(total: 100, initial: 100, dropped: 20, readyCount: 90);

        Assert.AreEqual(80, total);
        Assert.AreEqual(80, initial);
    }

    [TestMethod]
    public void FitTotals_AZeroReadyCount_IsAnEmptyList_SoTheSideIsFittedToNobody()
    {
        // The side's list really is empty: the player is off it (a hero still in _woundedInBattle is left off the list)
        // and everyone else was left out. 11 counted - 10 dropped is 1, but the supplier has nobody to give, and an
        // initial spawn of 1 would wait for that troop forever (v1.5.3 DefaultBattleMissionAgentSpawnLogic.CheckDeployment
        // :539-547). A side whose initial spawn is 0 is skipped (:535-538).
        var (total, initial) = SiegeForcesRules.FitTotals(total: 11, initial: 11, dropped: 10, readyCount: 0);

        Assert.AreEqual(0, total);
        Assert.AreEqual(0, initial);
    }

    [DataTestMethod]
    [DataRow(-1)]
    [DataRow(int.MinValue)]
    public void FitTotals_ANegativeReadyCount_IsIgnored_ALengthIsNeverNegative(int readyCount)
    {
        // A list cannot have a negative length, so the number is no size at all and is read like a count nobody
        // recorded (null): the arithmetic stands, and a corrupt value cannot spawn nobody.
        var (total, initial) = SiegeForcesRules.FitTotals(total: 100, initial: 100, dropped: 20, readyCount: readyCount);

        Assert.AreEqual(80, total);
        Assert.AreEqual(80, initial);
    }

    [TestMethod]
    public void FitTotals_NothingDropped_ChangesNothing()
    {
        var (total, initial) = SiegeForcesRules.FitTotals(total: 100, initial: 60, dropped: 0, readyCount: 100);

        Assert.AreEqual(100, total);
        Assert.AreEqual(60, initial);
    }

    [TestMethod]
    public void FitTotals_ANegativeDropped_IsTreatedAsZero_NeverRaisesTheTotal()
    {
        var (total, initial) = SiegeForcesRules.FitTotals(total: 100, initial: 100, dropped: -30, readyCount: null);

        Assert.AreEqual(100, total);
        Assert.AreEqual(100, initial);
    }

    [TestMethod]
    public void FitTotals_AnInitialSpawnAboveTheTotal_IsClampedDown()
    {
        var (total, initial) = SiegeForcesRules.FitTotals(total: 100, initial: 150, dropped: 10, readyCount: null);

        Assert.AreEqual(90, total);
        Assert.AreEqual(90, initial);
    }
}
