using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.EncounterLines;
using TAOM.Features.Execution;

namespace TAOM.Tests.Features.EncounterLines;

/// <summary>The pure bucket rules of Encounter Lines (Patch149), one test per branch and per order between branches.</summary>
[TestClass]
public class EncounterBucketsTests
{
    private static string Lord(
        bool liege = false,
        bool atWar = false,
        FactionSide speaker = FactionSide.Free,
        FactionSide? player = FactionSide.Free,
        string? speakerFamily = "man",
        string? playerFamily = "man",
        string? speakerGroup = "west",
        string? playerGroup = "rohirrim") =>
        EncounterBuckets.Lord(liege, atWar, speaker, player, speakerFamily, playerFamily, speakerGroup, playerGroup);

    [TestMethod]
    public void Lord_PlayerRulesTheRealm_IsLiege_EvenWhenKinOrOpposed()
    {
        Assert.AreEqual("liege", Lord(liege: true, playerGroup: "west"));
        Assert.AreEqual("liege", Lord(liege: true, speaker: FactionSide.Evil, atWar: true));
    }

    [TestMethod]
    public void Lord_AtWarOnDifferentSides_IsFoe()
        => Assert.AreEqual("foe", Lord(atWar: true, speaker: FactionSide.Free, player: FactionSide.Neutral));

    [TestMethod]
    public void Lord_FreeAgainstEvilAtPeace_IsFoe_EvenWhenKin()
    {
        Assert.AreEqual("foe", Lord(speaker: FactionSide.Free, player: FactionSide.Evil));
        Assert.AreEqual("foe", Lord(speaker: FactionSide.Evil, player: FactionSide.Free, playerGroup: "west"));
    }

    [TestMethod]
    public void Lord_NeutralAtPeace_IsAlly()
        => Assert.AreEqual("ally", Lord(speaker: FactionSide.Free, player: FactionSide.Neutral));

    [TestMethod]
    public void Lord_OrcPlayerToOrcLord_SameSideAtPeace_IsKin()
        => Assert.AreEqual("kin", Lord(speaker: FactionSide.Evil, player: FactionSide.Evil, speakerFamily: "orc", playerFamily: "orc", speakerGroup: "orcs", playerGroup: "west"));

    [TestMethod]
    public void Lord_GondorPlayerToGondorLord_IsKin()
        => Assert.AreEqual("kin", Lord(playerGroup: "west"));

    [TestMethod]
    public void Lord_RohanPlayerToGondorLord_IsAlly()
        => Assert.AreEqual("ally", Lord(playerGroup: "rohirrim"));

    [TestMethod]
    public void Lord_AtWarOnTheSameSide_Kin_IsKin()
        => Assert.AreEqual("kin", Lord(atWar: true, playerGroup: "west"));

    [TestMethod]
    public void Lord_AtWarOnTheSameSide_NotKin_IsRival()
        => Assert.AreEqual("rival", Lord(atWar: true));

    [TestMethod]
    public void Lord_UnknownPlayerSideAtPeace_IsRival()
        => Assert.AreEqual("rival", Lord(player: null));

    [TestMethod]
    public void Lord_UnknownPlayerSideAtWar_IsFoe()
        => Assert.AreEqual("foe", Lord(player: null, atWar: true));

    [TestMethod]
    public void Lord_FamilyNone_IsNeverKin()
    {
        Assert.AreEqual("ally", Lord(speakerFamily: "none", playerFamily: "none", speakerGroup: "west", playerGroup: "west"));
        Assert.AreEqual("ally", Lord(speakerFamily: null, playerFamily: null, playerGroup: "west"));
    }

    [TestMethod]
    public void Lord_DifferentFamilies_SameGroup_IsNotKin()
        => Assert.AreEqual("ally", Lord(speakerFamily: "man", playerFamily: "dwarf", playerGroup: "west"));

    [TestMethod]
    public void Lord_ElfToElf_InDifferentGroups_IsKin()
        => Assert.AreEqual("kin", Lord(speakerFamily: "elf", playerFamily: "elf", speakerGroup: "elves", playerGroup: "west"));

    [TestMethod]
    public void Trade_AtWar_IsFoe_EvenForKin()
        => Assert.AreEqual("foe", EncounterBuckets.Trade(true, "west", "west"));

    [TestMethod]
    public void Trade_SameGroup_IsKin()
        => Assert.AreEqual("kin", EncounterBuckets.Trade(false, "west", "west"));

    [TestMethod]
    public void Trade_OtherGroup_IsFriend()
        => Assert.AreEqual("friend", EncounterBuckets.Trade(false, "west", "rohirrim"));

    [TestMethod]
    public void Trade_NullPlayerGroup_IsFriend()
    {
        Assert.AreEqual("friend", EncounterBuckets.Trade(false, "west", null));
        Assert.AreEqual("friend", EncounterBuckets.Trade(false, null, null));
    }

    private static readonly BanditClan Orcs = new(FactionSide.Evil, "orc", null);
    private static readonly BanditClan Harad = new(FactionSide.Evil, "man", "southrons");
    private static readonly BanditClan Looters = new(FactionSide.Neutral, "man", null);
    private static readonly BanditClan Stalkers = new(FactionSide.Neutral, "elf", null);

    [TestMethod]
    public void Bandit_SameFamily_WithNoHomeGroup_IsKin()
        => Assert.AreEqual("kin", EncounterBuckets.Bandit(Orcs, "west", "orc", FactionSide.Free));

    [TestMethod]
    public void Bandit_GondorPlayerAgainstHaradRaiders_IsNotKin()
        => Assert.AreEqual("foe", EncounterBuckets.Bandit(Harad, "west", "man", FactionSide.Free));

    [TestMethod]
    public void Bandit_HaradPlayerAgainstHaradRaiders_IsKin()
        => Assert.AreEqual("kin", EncounterBuckets.Bandit(Harad, "southrons", "man", FactionSide.Evil));

    [TestMethod]
    public void Bandit_AnyHumanAgainstLooters_IsKin()
    {
        foreach (var group in new[] { "west", "rohirrim", "southrons", null })
            Assert.AreEqual("kin", EncounterBuckets.Bandit(Looters, group, "man", FactionSide.Free), group ?? "no group");
    }

    [TestMethod]
    public void Bandit_NeutralPlayerAgainstNeutralClan_IsSide()
        => Assert.AreEqual("side", EncounterBuckets.Bandit(Stalkers, "west", "man", FactionSide.Neutral));

    [TestMethod]
    public void Bandit_NeutralPlayerAgainstEvilClan_IsFoe()
        => Assert.AreEqual("foe", EncounterBuckets.Bandit(Orcs, "west", "man", FactionSide.Neutral));

    [TestMethod]
    public void Bandit_UnknownPlayerSide_IsFoe()
        => Assert.AreEqual("foe", EncounterBuckets.Bandit(Stalkers, "west", "man", null));

    [TestMethod]
    public void Bandit_UnknownClan_IsNull()
        => Assert.IsNull(EncounterBuckets.Bandit(null, "west", "man", FactionSide.Free));

    [TestMethod]
    public void Bandit_FamilyNoneClan_IsNeverKin()
        => Assert.AreEqual("foe", EncounterBuckets.Bandit(new BanditClan(FactionSide.Evil, "none", null), null, "none", FactionSide.Free));

    [TestMethod]
    public void StableHash_IsTheSameEveryCall_AndKnownForAFixedInput()
    {
        Assert.AreEqual(EncounterBuckets.StableHash("lord_1_1"), EncounterBuckets.StableHash("lord_1_1"));
        Assert.AreEqual(2166136261u, EncounterBuckets.StableHash(""), "FNV-1a offset basis for the empty string");
        Assert.AreEqual(EncounterBuckets.StableHash(""), EncounterBuckets.StableHash(null));
    }

    [TestMethod]
    public void IsAltLine_AlternatesFromDayToDay()
    {
        foreach (var hero in new[] { "lord_1_1", "lord_2_7", "lord_I1_4" })
        {
            for (var day = 0; day < 10; day++)
                Assert.AreNotEqual(EncounterBuckets.IsAltLine(hero, day), EncounterBuckets.IsAltLine(hero, day + 1), hero + " day " + day);
        }
    }

    [TestMethod]
    public void IsAltLine_IsStableForOneHeroAndDay()
    {
        for (var i = 0; i < 5; i++)
            Assert.AreEqual(EncounterBuckets.IsAltLine("lord_1_1", 40), EncounterBuckets.IsAltLine("lord_1_1", 40));
    }

    [TestMethod]
    public void IsAltLine_SplitsLordsOnOneDay()
    {
        var heroes = Enumerable.Range(0, 40).Select(i => "lord_" + i).ToList();
        var alt = heroes.Count(h => EncounterBuckets.IsAltLine(h, 12));

        Assert.IsTrue(alt > 0 && alt < heroes.Count, "every lord gave the same answer on one day: " + alt);
    }
}
