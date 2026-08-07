using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Arena;

namespace TAOM.Tests.Features.Arena;

/// <summary>
/// Patch69 — guards the two unguarded dereferences in vanilla
/// <c>TournamentVM.OnTournamentEnd</c> (v1.4.7): <c>hero.MapFaction.Color</c> on the hero branch
/// and <c>character.Culture.Color</c> on the troop branch. Reported by a player as a tournament
/// crash at Erebor after "Skip All Rounds".
///
/// The service is pure over <see cref="TournamentEntrant"/>, so no Campaign.Current and no
/// engine types are needed here. Harmony invocation itself is not unit-testable.
/// </summary>
[TestClass]
public class TournamentRosterGuardServiceTests
{
    private TournamentRosterGuardService _sut;

    [TestInitialize]
    public void Setup() => _sut = new TournamentRosterGuardService();

    private static TournamentEntrant Hero(bool hasMapFaction, bool isFemale = false, string clanId = "clan_erebor_6") =>
        new TournamentEntrant("lord_E6_3", "Flási", isHero: true, isFemale: isFemale,
            raceName: "dwarf", clanId: hasMapFaction ? clanId : null,
            hasMapFaction: hasMapFaction, hasCulture: true);

    private static TournamentEntrant Troop(bool hasCulture) =>
        new TournamentEntrant("erebor_reg_miner", "Miner", isHero: false, isFemale: false,
            raceName: "dwarf", clanId: null, hasMapFaction: false, hasCulture: hasCulture);

    [TestMethod]
    public void Classify_HeroWithMapFaction_IsSafe()
    {
        Assert.AreEqual(TournamentEntrantVerdict.Safe, _sut.Classify(Hero(hasMapFaction: true)));
    }

    [TestMethod]
    public void Classify_HeroWithoutMapFaction_IsUnsafe()
    {
        // A clanless hero with neither a home settlement nor a party — Hero.MapFaction returns null.
        Assert.AreEqual(TournamentEntrantVerdict.UnsafeNullMapFaction, _sut.Classify(Hero(hasMapFaction: false)));
    }

    [TestMethod]
    public void Classify_FemaleHeroWithMapFaction_IsSafe()
    {
        // Regression pin for the reporter's theory: gender is NOT a crash discriminator.
        // All 13 TAOM female dwarf lords carry faction="Faction.clan_erebor_N".
        Assert.AreEqual(TournamentEntrantVerdict.Safe, _sut.Classify(Hero(hasMapFaction: true, isFemale: true)));
    }

    [TestMethod]
    public void Classify_TroopWithCulture_IsSafe()
    {
        Assert.AreEqual(TournamentEntrantVerdict.Safe, _sut.Classify(Troop(hasCulture: true)));
    }

    [TestMethod]
    public void Classify_TroopWithoutCulture_IsUnsafe()
    {
        Assert.AreEqual(TournamentEntrantVerdict.UnsafeNullCulture, _sut.Classify(Troop(hasCulture: false)));
    }

    [TestMethod]
    public void Classify_HeroWithoutCulture_IsSafe_BecauseHeroBranchNeverReadsCulture()
    {
        var hero = new TournamentEntrant("lord_E1_3", "Dís", isHero: true, isFemale: true,
            raceName: "dwarf", clanId: "clan_erebor_1", hasMapFaction: true, hasCulture: false);

        Assert.AreEqual(TournamentEntrantVerdict.Safe, _sut.Classify(hero));
    }

    [TestMethod]
    public void Classify_TroopWithoutMapFaction_IsSafe_BecauseTroopBranchNeverReadsMapFaction()
    {
        Assert.AreEqual(TournamentEntrantVerdict.Safe, _sut.Classify(Troop(hasCulture: true)));
    }

    [TestMethod]
    public void Classify_NullCharacter_IsUnsafe()
    {
        Assert.AreEqual(TournamentEntrantVerdict.UnsafeNullCharacter, _sut.Classify(default));
    }

    [TestMethod]
    public void FindUnsafeIndices_AllSafe_ReturnsEmpty()
    {
        var roster = new List<TournamentEntrant> { Hero(true), Troop(true), Hero(true, isFemale: true) };

        Assert.AreEqual(0, _sut.FindUnsafeIndices(roster).Count);
    }

    [TestMethod]
    public void FindUnsafeIndices_ReportsEveryOffender_InAscendingOrder()
    {
        var roster = new List<TournamentEntrant>
        {
            Hero(true),           // 0 safe
            Hero(false),          // 1 unsafe — null MapFaction
            Troop(true),          // 2 safe
            Troop(false),         // 3 unsafe — null Culture
            default               // 4 unsafe — null character
        };

        CollectionAssert.AreEqual(new[] { 1, 3, 4 }, ToArray(_sut.FindUnsafeIndices(roster)));
    }

    [TestMethod]
    public void FindUnsafeIndices_NullRoster_ReturnsEmpty_DoesNotThrow()
    {
        Assert.AreEqual(0, _sut.FindUnsafeIndices(null).Count);
    }

    [TestMethod]
    public void Describe_NamesTheOffendingFieldAndTheEntrant()
    {
        var text = _sut.Describe(Hero(hasMapFaction: false, isFemale: true),
            TournamentEntrantVerdict.UnsafeNullMapFaction);

        StringAssert.Contains(text, "lord_E6_3");
        StringAssert.Contains(text, "female");
        StringAssert.Contains(text, "race=dwarf");
        StringAssert.Contains(text, "mapFaction=NULL");
        StringAssert.Contains(text, "UnsafeNullMapFaction");
    }

    [TestMethod]
    public void Describe_NullEntrant_DoesNotThrow()
    {
        var text = _sut.Describe(default, TournamentEntrantVerdict.UnsafeNullCharacter);

        StringAssert.Contains(text, "<null>");
    }

    private static int[] ToArray(IReadOnlyList<int> source)
    {
        var result = new int[source.Count];
        for (var i = 0; i < source.Count; i++) result[i] = source[i];
        return result;
    }
}
