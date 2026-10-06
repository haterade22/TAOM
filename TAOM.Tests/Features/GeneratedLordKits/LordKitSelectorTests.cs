using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.GeneratedLordKits;

namespace TAOM.Tests.Features.GeneratedLordKits;

[TestClass]
public class LordKitSelectorTests
{
    private const int Human = 0;
    private const int Uruk = 7;

    private static LordKitRequest Request(string culture = "empire", int race = Human, bool female = false,
        bool civilian = false, bool isLord = true, bool isAdult = true, bool minorFaction = false) =>
        new(culture, race, female, civilian, isLord, isAdult, minorFaction);

    private static readonly LordKitRequest DunlandMaleBattle = Request();

    private static LordKitCandidate Kit(string donor, string signature, string culture = "empire", int race = Human,
        bool female = false, bool civilian = false, bool alive = true) =>
        new(donor, culture, race, female, civilian, alive, signature, donor + ":" + signature);

    private static LordKitCandidate Pick(LordKitRequest request, IEnumerable<LordKitCandidate> candidates, int roll = 0) =>
        LordKitSelector.Pick(request, new List<LordKitCandidate>(candidates), _ => roll);

    [TestMethod]
    public void Pick_KitSharedByTwoLordsOfSameCultureRaceAndSex_ReturnsIt()
    {
        var result = Pick(DunlandMaleBattle, new[] { Kit("lord_a", "dunland_kit"), Kit("lord_b", "dunland_kit") });

        Assert.IsNotNull(result);
        Assert.AreEqual("dunland_kit", result.Signature);
    }

    [TestMethod]
    public void Pick_KitWornByOneLordOnly_IsUniqueGearAndReturnsNull()
    {
        var result = Pick(DunlandMaleBattle, new[] { Kit("king", "royal_kit") });

        Assert.IsNull(result, "A kit only one lord wears is that lord's own gear, never a generated lord's.");
    }

    [TestMethod]
    public void Pick_OneLordWearingTheKitInTwoSets_CountsAsOneDonor()
    {
        var result = Pick(DunlandMaleBattle, new[] { Kit("king", "royal_kit"), Kit("king", "royal_kit") });

        Assert.IsNull(result);
    }

    [TestMethod]
    public void Pick_KitWornOnlyByDeadLords_IsSkipped()
    {
        // The four kept vanilla ancestors (dead_lord_6_*) share a Calradic kit no living lord wears.
        var result = Pick(DunlandMaleBattle, new[]
        {
            Kit("dead_lord_6_1", "calradic_kit", alive: false), Kit("dead_lord_6_2", "calradic_kit", alive: false),
        });

        Assert.IsNull(result, "A kit nobody alive wears is history, not what the culture's lords wear now.");
    }

    [TestMethod]
    public void Pick_KitSharedByOneLivingAndOneDeadLord_ReturnsIt()
    {
        var result = Pick(DunlandMaleBattle, new[] { Kit("lord_a", "dunland_kit"), Kit("lord_b", "dunland_kit", alive: false) });

        Assert.IsNotNull(result, "A kit keeps counting after one of its wearers dies, so the pool does not shrink as lords fall.");
    }

    [TestMethod]
    public void Pick_SharedKitOfAnotherRace_IsSkipped()
    {
        var result = Pick(DunlandMaleBattle, new[] { Kit("uruk_a", "uruk_kit", race: Uruk), Kit("uruk_b", "uruk_kit", race: Uruk) });

        Assert.IsNull(result, "An uruk lord's kit must never reach a human lord of the same culture.");
    }

    [TestMethod]
    public void Pick_SharedKitOfAnotherCulture_IsSkipped()
    {
        var result = Pick(DunlandMaleBattle, new[] { Kit("rohan_a", "rohan_kit", "vlandia"), Kit("rohan_b", "rohan_kit", "vlandia") });

        Assert.IsNull(result);
    }

    [TestMethod]
    public void Pick_SharedKitOfTheOtherSex_IsSkipped()
    {
        var result = Pick(DunlandMaleBattle, new[] { Kit("lady_a", "lady_kit", female: true), Kit("lady_b", "lady_kit", female: true) });

        Assert.IsNull(result);
    }

    [TestMethod]
    public void Pick_CivilianKitForABattleRequest_IsSkipped()
    {
        var result = Pick(DunlandMaleBattle, new[] { Kit("lord_a", "civ_kit", civilian: true), Kit("lord_b", "civ_kit", civilian: true) });

        Assert.IsNull(result);
    }

    [TestMethod]
    public void Pick_CivilianRequest_ReturnsSharedCivilianKit()
    {
        var result = Pick(Request(civilian: true), new[]
        {
            Kit("lord_a", "bat_kit"), Kit("lord_b", "bat_kit"),
            Kit("lord_a", "civ_kit", civilian: true), Kit("lord_b", "civ_kit", civilian: true),
        });

        Assert.IsNotNull(result);
        Assert.AreEqual("civ_kit", result.Signature);
    }

    [TestMethod]
    public void Pick_SeveralSharedKits_RollChoosesAmongKitsNotDonors()
    {
        var candidates = new[]
        {
            Kit("a", "kit_one"), Kit("b", "kit_one"), Kit("c", "kit_one"), Kit("d", "kit_one"),
            Kit("e", "kit_two"), Kit("f", "kit_two"),
        };
        var seenBound = -1;

        var first = LordKitSelector.Pick(DunlandMaleBattle, candidates, bound => { seenBound = bound; return 0; });
        var second = LordKitSelector.Pick(DunlandMaleBattle, candidates, _ => 1);

        Assert.AreEqual(2, seenBound, "The roll ranges over the two distinct kits, so a popular kit does not crowd out the rest.");
        Assert.AreNotEqual(first.Signature, second.Signature);
    }

    [TestMethod]
    public void Pick_NoCandidatesOrNullRequest_ReturnsNull()
    {
        Assert.IsNull(LordKitSelector.Pick(DunlandMaleBattle, new List<LordKitCandidate>(), _ => 0));
        Assert.IsNull(LordKitSelector.Pick(DunlandMaleBattle, null, _ => 0));
        Assert.IsNull(LordKitSelector.Pick(null, new[] { Kit("a", "k"), Kit("b", "k") }, _ => 0));
    }

    [TestMethod]
    public void Pick_NullCandidateInList_IsSkipped()
    {
        var result = Pick(DunlandMaleBattle, new[] { null, Kit("a", "k"), Kit("b", "k") });

        Assert.IsNotNull(result);
    }

    // The gate: who gets a peer kit at all. Every engine path asks, so it lives here, tested, not in the models.

    [TestMethod]
    public void Wants_AdultLordOfALordClan_IsTrue() =>
        Assert.IsTrue(LordKitSelector.Wants(DunlandMaleBattle));

    [TestMethod]
    public void Wants_HeroWhoIsNotALord_IsFalse() =>
        Assert.IsFalse(LordKitSelector.Wants(Request(isLord: false)), "Wanderers, notables and troops keep vanilla's handling.");

    [TestMethod]
    public void Wants_Child_IsFalse() =>
        Assert.IsFalse(LordKitSelector.Wants(Request(isAdult: false)), "Children keep the engine's child and teen templates.");

    [TestMethod]
    public void Wants_MinorFactionClanHero_IsFalse() =>
        Assert.IsFalse(LordKitSelector.Wants(Request(minorFaction: true)),
            "A minor faction (the Corsair Blades run on a vanilla Rohan-culture template) keeps its own template gear.");

    [TestMethod]
    public void Wants_NullRequest_IsFalse() =>
        Assert.IsFalse(LordKitSelector.Wants(null));

    [TestMethod]
    public void Pick_RequestTheGateRefuses_ReturnsNull()
    {
        var candidates = new[] { Kit("lord_a", "dunland_kit"), Kit("lord_b", "dunland_kit") };

        Assert.IsNull(Pick(Request(isLord: false), candidates));
        Assert.IsNull(Pick(Request(isAdult: false), candidates));
        Assert.IsNull(Pick(Request(minorFaction: true), candidates));
    }

    [TestMethod]
    public void Pick_CandidatesOutsideTheRequest_DoNotChangeThePick()
    {
        // The adapter prefilters to the request's culture, race and sex; the pick must not depend on that.
        var matching = new[] { Kit("a", "kit_one"), Kit("b", "kit_one"), Kit("c", "kit_two"), Kit("d", "kit_two") };
        var mixed = new[]
        {
            Kit("x", "rohan_kit", "vlandia"), matching[0], Kit("y", "uruk_kit", race: Uruk), matching[1],
            Kit("z", "lady_kit", female: true), matching[2], Kit("w", "civ_kit", civilian: true), matching[3],
        };
        int boundA = -1, boundB = -1;

        var fromMatching = LordKitSelector.Pick(DunlandMaleBattle, matching, n => { boundA = n; return 1; });
        var fromMixed = LordKitSelector.Pick(DunlandMaleBattle, mixed, n => { boundB = n; return 1; });

        Assert.AreEqual(boundA, boundB);
        Assert.AreEqual(fromMatching.Token, fromMixed.Token);
    }
}
