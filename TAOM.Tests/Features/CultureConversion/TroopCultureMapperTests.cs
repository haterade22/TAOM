using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CultureConversion.GarrisonSwap;
using TAOM.Features.CultureConversion.GarrisonSwap.Domain;

namespace TAOM.Tests.Features.CultureConversion;

/// <summary>
/// The matching ladder, rung by rung. Every scenario below is modelled on a real hole measured in
/// TAOM's troop data on 2026-09-20, not invented: Mirkwood's empty tiers 4-6, Goblin's total
/// absence of cavalry, Dunland and Dale stopping at tier 6.
/// </summary>
[TestClass]
public class TroopCultureMapperTests
{
    private const string Town = "town_ES1";
    private const string Target = "mordor";

    private TroopCultureMapper _sut = null!;

    [TestInitialize]
    public void Setup() => _sut = new TroopCultureMapper();

    private static CultureTroopIndex Index(string culture, params (string Id, int Tier, TroopRole Role)[] troops)
        => new(culture, troops.Select(t => new CultureTroopCandidate(t.Id, t.Tier, t.Role)));

    private static GarrisonTroopInfo Row(
        string id, int tier, TroopRole role, string? culture = "gondor",
        int count = 10, int wounded = 0, bool isHero = false)
        => new(id, culture, tier, role, count, wounded, isHero);

    private static List<GarrisonTroopInfo> Roster(params GarrisonTroopInfo[] rows) => rows.ToList();

    private TroopSwapPlan Map(CultureTroopIndex index, params GarrisonTroopInfo[] rows)
        => _sut.MapGarrison(Town, Target, Roster(rows), index);

    // --- Rung 1: exact cell ---

    [TestMethod]
    public void MapGarrison_ExactTierAndRole_PicksFromThatCell()
    {
        var index = Index(Target,
            ("orc_archer_t4", 4, TroopRole.Ranged),
            ("orc_spear_t4", 4, TroopRole.Infantry));

        var plan = Map(index, Row("gondor_archer", 4, TroopRole.Ranged));

        Assert.AreEqual(1, plan.Swaps.Count);
        Assert.AreEqual("gondor_archer", plan.Swaps[0].OldTroopId);
        Assert.AreEqual("orc_archer_t4", plan.Swaps[0].NewTroopId, "An exact tier+role cell must win outright.");
        Assert.AreEqual(0, plan.UnmappedTroopIds.Count);
    }

    // --- Rung 2: same role, nearest tier within the window ---

    [TestMethod]
    public void MapGarrison_MissingTier_PicksNearestSameRoleWithinWindow()
    {
        var index = Index(Target, ("orc_archer_t3", 3, TroopRole.Ranged));

        var plan = Map(index, Row("gondor_archer", 4, TroopRole.Ranged));

        Assert.AreEqual("orc_archer_t3", plan.Swaps.Single().NewTroopId);
    }

    [TestMethod]
    public void MapGarrison_EquallyDistantTiers_PrefersTheLowerOne()
    {
        // Tier 2 and tier 6 are both two away from tier 4. Preferring the lower one is what stops a
        // conversion handing out a free upgrade on every ragged cell.
        var index = Index(Target,
            ("orc_archer_t2", 2, TroopRole.Ranged),
            ("orc_archer_t6", 6, TroopRole.Ranged));

        var plan = Map(index, Row("gondor_archer", 4, TroopRole.Ranged));

        Assert.AreEqual("orc_archer_t2", plan.Swaps.Single().NewTroopId);
    }

    [TestMethod]
    public void MapGarrison_TierAboveEverythingTheCultureFields_DropsToItsCeiling()
    {
        // Dunland, Dale and Umbar all stop at tier 6, so a captured tier-8 Gondor stack has no
        // same-tier target anywhere in their rosters.
        var index = Index(Target,
            ("dun_inf_t5", 5, TroopRole.Infantry),
            ("dun_inf_t6", 6, TroopRole.Infantry));

        var plan = Map(index, Row("gondor_guard", 8, TroopRole.Infantry));

        Assert.AreEqual("dun_inf_t6", plan.Swaps.Single().NewTroopId);
    }

    // --- Rung 3: same tier, role fallback chain ---

    [TestMethod]
    public void MapGarrison_RoleAbsentEntirely_KeepsTheTierAndChangesRole()
    {
        // Goblin fields no cavalry at any tier. Keeping tier 5 and becoming infantry preserves far
        // more of the garrison's strength than dropping to a tier-1 rider would.
        var index = Index(Target,
            ("goblin_inf_t5", 5, TroopRole.Infantry),
            ("goblin_bow_t5", 5, TroopRole.Ranged));

        var plan = Map(index, Row("gondor_knight", 5, TroopRole.Cavalry));

        Assert.AreEqual("goblin_inf_t5", plan.Swaps.Single().NewTroopId,
            "Cavalry's fallback chain puts Infantry ahead of Ranged.");
    }

    [TestMethod]
    public void MapGarrison_SameRoleOnlyFarAway_PrefersSameTierDifferentRole()
    {
        // The window exists for exactly this: a tier-5 rider becoming a tier-1 rider is a bigger
        // loss than becoming tier-5 infantry, so rung 2 must decline and rung 3 must take it.
        var index = Index(Target,
            ("orc_rider_t1", 1, TroopRole.Cavalry),
            ("orc_inf_t5", 5, TroopRole.Infantry));

        var plan = Map(index, Row("gondor_knight", 5, TroopRole.Cavalry));

        Assert.AreEqual("orc_inf_t5", plan.Swaps.Single().NewTroopId);
    }

    [TestMethod]
    public void MapGarrison_HorseArcherFallsBackToCavalryBeforeFoot()
    {
        var index = Index(Target,
            ("orc_rider_t4", 4, TroopRole.Cavalry),
            ("orc_inf_t4", 4, TroopRole.Infantry),
            ("orc_bow_t4", 4, TroopRole.Ranged));

        var plan = Map(index, Row("rhun_horsearcher", 4, TroopRole.HorseArcher));

        Assert.AreEqual("orc_rider_t4", plan.Swaps.Single().NewTroopId, "Mounted should degrade to mounted first.");
    }

    [TestMethod]
    public void MapGarrison_UnknownRole_IsTreatedAsInfantry()
    {
        var index = Index(Target,
            ("orc_inf_t3", 3, TroopRole.Infantry),
            ("orc_bow_t3", 3, TroopRole.Ranged));

        var plan = Map(index, Row("odd_troop", 3, TroopRole.Unknown));

        Assert.AreEqual("orc_inf_t3", plan.Swaps.Single().NewTroopId);
    }

    // --- Rungs 4 and 5: last resorts ---

    [TestMethod]
    public void MapGarrison_NothingNearTheTier_TakesTheNearestPopulatedTier()
    {
        // Mirkwood's real shape: nothing at all between tier 3 and tier 7.
        var index = Index(Target,
            ("mk_inf_t3", 3, TroopRole.Infantry),
            ("mk_inf_t7", 7, TroopRole.Infantry));

        var plan = Map(index, Row("gondor_guard", 6, TroopRole.Infantry));

        Assert.AreEqual("mk_inf_t7", plan.Swaps.Single().NewTroopId, "Tier 7 is one away from 6; tier 3 is three away.");
    }

    [TestMethod]
    public void MapGarrison_RoleMissingAndTierMissing_StillResolvesFromWhatExists()
    {
        var index = Index(Target, ("mk_inf_t7", 7, TroopRole.Infantry));

        var plan = Map(index, Row("gondor_knight", 2, TroopRole.Cavalry));

        Assert.AreEqual("mk_inf_t7", plan.Swaps.Single().NewTroopId);
        Assert.AreEqual(0, plan.UnmappedTroopIds.Count);
    }

    [TestMethod]
    public void MapGarrison_TierOnlyReachableOutsideTheFallbackChain_StillResolves()
    {
        // A ranged troop whose chain is {Ranged, Infantry}, against a culture that fields only
        // cavalry. Rung 5's second sweep over the tier's actual roles is what catches this.
        var index = Index(Target, ("orc_rider_t4", 4, TroopRole.Cavalry));

        var plan = Map(index, Row("gondor_archer", 4, TroopRole.Ranged));

        Assert.AreEqual("orc_rider_t4", plan.Swaps.Single().NewTroopId);
    }

    // --- Fail-safe ---

    [TestMethod]
    public void MapGarrison_CultureWithNoTroops_LeavesTheStackAloneAndReportsIt()
    {
        var plan = Map(Index(Target), Row("gondor_archer", 4, TroopRole.Ranged));

        Assert.AreEqual(0, plan.Swaps.Count, "A culture with no troops must never cost a garrison its men.");
        CollectionAssert.AreEqual(new[] { "gondor_archer" }, plan.UnmappedTroopIds.ToArray());
    }

    // --- Rows that must never be touched ---

    [TestMethod]
    public void MapGarrison_SkipsHeroesEmptyStacksAndTroopsAlreadyOfTheTargetCulture()
    {
        var index = Index(Target, ("orc_inf_t3", 3, TroopRole.Infantry));

        var plan = Map(index,
            Row("some_lord", 3, TroopRole.Infantry, isHero: true),
            Row("gondor_ghost", 3, TroopRole.Infantry, count: 0),
            Row("orc_already_here", 3, TroopRole.Infantry, culture: Target),
            Row("no_culture_troop", 3, TroopRole.Infantry, culture: null));

        Assert.AreEqual(0, plan.Swaps.Count);
        Assert.AreEqual(0, plan.UnmappedTroopIds.Count, "Skipped rows are deliberate, not failures to map.");
    }

    [TestMethod]
    public void MapMilitia_SlotResolvingToTheSameTroop_ProducesNoSwap()
    {
        // The self-swap guard now only fires on the militia path: on the garrison path the
        // index-membership skip catches an identical troop first. Two cultures sharing a militia
        // troop is real (Lothlorien reuses the Rivendell militia), so this is the live case.
        var shared = new CultureMilitiaTroops("lothlorien", "rivendell_militia_spearman", "me", "r", "re");
        var from = new CultureMilitiaTroops("rivendell", "rivendell_militia_spearman", "me2", "r2", "re2");

        var plan = _sut.MapMilitia(Town, "lothlorien",
            Roster(Row("rivendell_militia_spearman", 2, TroopRole.Infantry, culture: "rivendell")),
            from, shared, targetIndex: null);

        Assert.AreEqual(0, plan.Swaps.Count);
        Assert.AreEqual(0, plan.UnmappedTroopIds.Count);
    }

    [TestMethod]
    public void MapGarrison_TroopTheTargetCultureAlreadyFields_IsLeftAlone()
    {
        // Lothlorien recruits the Rivendell line, so a Rivendell troop in a Lothlorien fief is
        // already correct even though its culture tag says rivendell. Comparing culture ids alone
        // would churn a perfectly good garrison every conversion.
        var index = Index("lothlorien",
            ("imladris_infantry", 4, TroopRole.Infantry),
            ("imladris_bowman", 4, TroopRole.Ranged));

        var plan = _sut.MapGarrison(Town, "lothlorien",
            Roster(Row("imladris_infantry", 4, TroopRole.Infantry, culture: "rivendell")), index);

        Assert.AreEqual(0, plan.Swaps.Count);
        Assert.AreEqual(0, plan.UnmappedTroopIds.Count);
    }

    [TestMethod]
    public void MapGarrison_TroopOfAnotherCultureTheTargetDoesNotField_IsStillSwapped()
    {
        // The guard above must not become "never swap anything from another culture".
        var index = Index("lothlorien", ("imladris_infantry", 4, TroopRole.Infantry));

        var plan = _sut.MapGarrison(Town, "lothlorien",
            Roster(Row("mordor_orc_warrior", 4, TroopRole.Infantry, culture: "mordor")), index);

        Assert.AreEqual("imladris_infantry", plan.Swaps.Single().NewTroopId);
    }

    // --- Head count and wounded ---

    [TestMethod]
    public void MapGarrison_PreservesHeadCountAndWounded()
    {
        var index = Index(Target, ("orc_inf_t3", 3, TroopRole.Infantry));

        var swap = Map(index, Row("gondor_inf", 3, TroopRole.Infantry, count: 37, wounded: 9)).Swaps.Single();

        Assert.AreEqual(37, swap.Count, "A swap is strength-neutral by construction: one count, used for both halves.");
        Assert.AreEqual(9, swap.WoundedCount);
    }

    [TestMethod]
    public void MapGarrison_WoundedAboveTheHeadCount_IsClamped()
    {
        var index = Index(Target, ("orc_inf_t3", 3, TroopRole.Infantry));

        var swap = Map(index, Row("gondor_inf", 3, TroopRole.Infantry, count: 5, wounded: 9)).Swaps.Single();

        Assert.AreEqual(5, swap.Count);
        Assert.AreEqual(5, swap.WoundedCount, "More wounded than bodies would make the engine's AddToCounts assert.");
    }

    [TestMethod]
    public void MapGarrison_NegativeWounded_IsClampedToZero()
    {
        var index = Index(Target, ("orc_inf_t3", 3, TroopRole.Infantry));

        var swap = Map(index, Row("gondor_inf", 3, TroopRole.Infantry, count: 5, wounded: -2)).Swaps.Single();

        Assert.AreEqual(0, swap.WoundedCount);
    }

    // --- Determinism ---

    [TestMethod]
    public void MapGarrison_SameSettlementAndCell_AlwaysPicksTheSameCandidate()
    {
        var index = Index(Target,
            ("orc_inf_a", 3, TroopRole.Infantry),
            ("orc_inf_b", 3, TroopRole.Infantry),
            ("orc_inf_c", 3, TroopRole.Infantry));

        var first = Map(index, Row("gondor_inf", 3, TroopRole.Infantry)).Swaps.Single().NewTroopId;
        for (var i = 0; i < 5; i++)
            Assert.AreEqual(first, Map(index, Row("gondor_inf", 3, TroopRole.Infantry)).Swaps.Single().NewTroopId,
                "A re-run must not re-roll, or a save reload would look like a second swap.");
    }

    [TestMethod]
    public void MapGarrison_DifferentSettlements_SpreadAcrossTheCandidates()
    {
        var index = Index(Target,
            ("orc_inf_a", 3, TroopRole.Infantry),
            ("orc_inf_b", 3, TroopRole.Infantry),
            ("orc_inf_c", 3, TroopRole.Infantry));

        var picked = new HashSet<string>();
        for (var i = 0; i < 40; i++)
        {
            var plan = _sut.MapGarrison($"town_{i}", Target, Roster(Row("gondor_inf", 3, TroopRole.Infantry)), index);
            picked.Add(plan.Swaps.Single().NewTroopId);
        }

        Assert.AreEqual(3, picked.Count, "Every candidate in a cell should be reachable, or towns all look identical.");
    }

    [TestMethod]
    public void MapGarrison_CandidateOrderInTheIndexDoesNotChangeThePick()
    {
        var ascending = Index(Target,
            ("orc_inf_a", 3, TroopRole.Infantry),
            ("orc_inf_b", 3, TroopRole.Infantry));
        var descending = Index(Target,
            ("orc_inf_b", 3, TroopRole.Infantry),
            ("orc_inf_a", 3, TroopRole.Infantry));

        Assert.AreEqual(
            Map(ascending, Row("gondor_inf", 3, TroopRole.Infantry)).Swaps.Single().NewTroopId,
            Map(descending, Row("gondor_inf", 3, TroopRole.Infantry)).Swaps.Single().NewTroopId,
            "The index sorts its cells ordinally so XML ordering can never move a town's garrison.");
    }

    // --- Militia: slot for slot ---

    private static CultureMilitiaTroops Militia(string culture, string prefix)
        => new(culture, $"{prefix}_melee", $"{prefix}_melee_vet", $"{prefix}_ranged", $"{prefix}_ranged_vet");

    [TestMethod]
    public void MapMilitia_MapsEverySlotOntoItsCounterpart()
    {
        var from = Militia("gondor", "gondor");
        var to = Militia(Target, "mordor");

        var plan = _sut.MapMilitia(Town, Target, Roster(
            Row("gondor_melee", 2, TroopRole.Infantry),
            Row("gondor_melee_vet", 3, TroopRole.Infantry),
            Row("gondor_ranged", 2, TroopRole.Ranged),
            Row("gondor_ranged_vet", 3, TroopRole.Ranged)), from, to, null);

        var byOld = plan.Swaps.ToDictionary(s => s.OldTroopId, s => s.NewTroopId);
        Assert.AreEqual("mordor_melee", byOld["gondor_melee"]);
        Assert.AreEqual("mordor_melee_vet", byOld["gondor_melee_vet"]);
        Assert.AreEqual("mordor_ranged", byOld["gondor_ranged"]);
        Assert.AreEqual("mordor_ranged_vet", byOld["gondor_ranged_vet"]);
    }

    [TestMethod]
    public void MapMilitia_NeedsNoTroopIndexAtAll()
    {
        var plan = _sut.MapMilitia(Town, Target,
            Roster(Row("gondor_melee", 2, TroopRole.Infantry)),
            Militia("gondor", "gondor"), Militia(Target, "mordor"), targetIndex: null);

        Assert.AreEqual("mordor_melee", plan.Swaps.Single().NewTroopId,
            "Slot mapping is why militia is reliable where the garrison ladder is not.");
        Assert.AreEqual(0, plan.UnmappedTroopIds.Count);
    }

    [TestMethod]
    public void MapMilitia_TroopThatIsNotAMilitiaSlot_FallsBackToTheGarrisonLadder()
    {
        // A regular line troop that found its way into the militia party.
        var index = Index(Target, ("orc_inf_t4", 4, TroopRole.Infantry));

        var plan = _sut.MapMilitia(Town, Target,
            Roster(Row("gondor_line_infantry", 4, TroopRole.Infantry)),
            Militia("gondor", "gondor"), Militia(Target, "mordor"), index);

        Assert.AreEqual("orc_inf_t4", plan.Swaps.Single().NewTroopId);
    }

    [TestMethod]
    public void MapMilitia_TargetCultureMissingThatSlot_FallsBackToTheGarrisonLadder()
    {
        var index = Index(Target, ("orc_bow_t3", 3, TroopRole.Ranged));
        var to = new CultureMilitiaTroops(Target, "mordor_melee", "mordor_melee_vet", rangedBasicTroopId: null, rangedEliteTroopId: null);

        var plan = _sut.MapMilitia(Town, Target,
            Roster(Row("gondor_ranged", 3, TroopRole.Ranged)),
            Militia("gondor", "gondor"), to, index);

        Assert.AreEqual("orc_bow_t3", plan.Swaps.Single().NewTroopId);
    }

    [TestMethod]
    public void MapMilitia_WithNoMilitiaDataAndNoIndex_LeavesTheStackAlone()
    {
        var plan = _sut.MapMilitia(Town, Target,
            Roster(Row("gondor_melee", 2, TroopRole.Infantry)), null, null, null);

        Assert.AreEqual(0, plan.Swaps.Count);
        CollectionAssert.AreEqual(new[] { "gondor_melee" }, plan.UnmappedTroopIds.ToArray());
    }

    // --- Degenerate inputs ---

    [TestMethod]
    public void MapGarrison_EmptyRoster_ReturnsAnEmptyPlan()
    {
        var plan = _sut.MapGarrison(Town, Target, new List<GarrisonTroopInfo>(), Index(Target, ("orc_inf_t3", 3, TroopRole.Infantry)));

        Assert.AreEqual(0, plan.Swaps.Count);
        Assert.AreEqual(0, plan.UnmappedTroopIds.Count);
    }

    [TestMethod]
    public void MapGarrison_NullRoster_ReturnsAnEmptyPlan()
    {
        var plan = _sut.MapGarrison(Town, Target, null!, Index(Target, ("orc_inf_t3", 3, TroopRole.Infantry)));

        Assert.AreEqual(0, plan.Swaps.Count);
    }
}
