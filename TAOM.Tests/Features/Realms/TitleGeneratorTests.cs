using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Realms.Domain.Config;
using TAOM.Features.Realms.Domain.Generation;
using TAOM.Features.Realms.Domain.Laws;
using TAOM.Features.Realms.Domain.Titles;
using TAOM.Features.Realms.Domain.World;

namespace TAOM.Tests.Features.Realms;

[TestClass]
public class TitleGeneratorTests
{
    private static GenerationResult Generate(RealmsConfig? config = null, WorldSnapshot? world = null) =>
        TitleGenerator.Generate(world ?? TestWorld.Build(), config ?? RealmsConfig.Defaults());

    [TestMethod]
    public void Creates_one_title_per_kingdom_and_fief_held_by_the_current_owner()
    {
        var result = Generate();

        Assert.AreEqual(TitleRank.Kingdom, result.Catalog.Get("k_north")!.Rank);
        Assert.AreEqual("king_n", result.Ledger.HolderOf("k_north"));
        Assert.AreEqual(TitleRank.County, result.Catalog.ForSettlement("town_b")!.Rank);
        Assert.AreEqual("lord_b", result.Ledger.HolderOf("c_town_b"));
        Assert.AreEqual(TitleRank.Barony, result.Catalog.ForSettlement("castle_a")!.Rank);
        Assert.AreEqual(TitleRank.Lordship, result.Catalog.ForSettlement("village_a1")!.Rank);
        Assert.AreEqual(0, result.Warnings.Count, string.Join("; ", result.Warnings));
    }

    [TestMethod]
    public void Places_a_castle_under_the_nearest_town_of_its_kingdom_and_villages_under_their_fief()
    {
        var result = Generate();

        Assert.AreEqual("c_town_b", result.Catalog.Get("b_castle_a")!.ParentId);
        Assert.AreEqual("c_town_a", result.Catalog.Get("l_village_a1")!.ParentId);
        Assert.AreEqual("b_castle_a", result.Catalog.Get("l_village_ca")!.ParentId);
    }

    [TestMethod]
    public void Groups_nearby_towns_into_a_duchy_and_leaves_a_lone_town_under_its_kingdom()
    {
        var config = RealmsConfig.Defaults();
        config.CountiesPerDuchy = 2;
        var result = Generate(config);

        // town_a seeds (westmost) and takes town_b; town_c is left alone and joins the nearest duchy.
        var duchy = result.Catalog.Get("d_town_a")!;
        Assert.AreEqual(TitleRank.Duchy, duchy.Rank);
        Assert.AreEqual("k_north", duchy.ParentId);
        CollectionAssert.AreEquivalent(new[] { "c_town_a", "c_town_b", "c_town_c" },
            result.Catalog.ChildrenOf("d_town_a").Select(t => t.Id).ToArray());
        Assert.AreEqual("k_south", result.Catalog.Get("c_town_s")!.ParentId);
    }

    [TestMethod]
    public void Duke_is_the_owner_of_most_of_the_duchys_towns()
    {
        var world = new WorldSnapshot(
            new List<KingdomInfo> { new("north", "North", null, "king") },
            new List<FiefInfo>
            {
                TestWorld.Town("t1", "north", "king", 0),
                TestWorld.Town("t2", "north", "duke", 1),
                TestWorld.Town("t3", "north", "duke", 2),
            });
        var result = Generate(world: world);

        Assert.AreEqual("duke", result.Ledger.HolderOf("d_t1"));
    }

    [TestMethod]
    public void Configured_duchy_takes_its_towns_before_automatic_grouping()
    {
        var config = RealmsConfig.Defaults();
        config.Duchies.Add(new DuchyConfig("eastmarch", "{=x}Eastmarch", "north", new[] { "town_c", "town_s" }));
        var result = Generate(config);

        // town_s belongs to another kingdom and is not taken.
        CollectionAssert.AreEqual(new[] { "c_town_c" }, result.Catalog.ChildrenOf("d_eastmarch").Select(t => t.Id).ToArray());
        Assert.AreEqual("d_town_a", result.Catalog.Get("c_town_b")!.ParentId);
    }

    [TestMethod]
    public void A_castle_listed_in_a_duchy_joins_the_nearest_county_of_that_duchy()
    {
        // castle_a (x 12) is nearest town_b (x 10), but its duchy only has town_c (x 100).
        var config = RealmsConfig.Defaults();
        config.Duchies.Add(new DuchyConfig("eastmarch", "{=x}Eastmarch", "north", new[] { "town_c" }, new[] { "castle_a" }));
        var result = Generate(config);

        Assert.AreEqual("c_town_c", result.Catalog.Get("b_castle_a")!.ParentId);
        Assert.AreEqual("d_eastmarch", result.Catalog.Get("c_town_c")!.ParentId);
        Assert.AreEqual(0, result.Warnings.Count, string.Join("; ", result.Warnings));
    }

    [TestMethod]
    public void A_duchy_of_castles_only_holds_its_baronies_and_goes_to_the_main_castle_owner()
    {
        var world = new WorldSnapshot(
            new List<KingdomInfo> { new("north", "North", null, "king") },
            new List<FiefInfo>
            {
                TestWorld.Town("t1", "north", "king", 0),
                TestWorld.Castle("c1", "north", "warden", 1),
                TestWorld.Castle("c2", "north", "warden", 2),
                TestWorld.Castle("c3", "north", "other", 3),
                TestWorld.Castle("c4", "north", "free", 4),
            });
        var config = RealmsConfig.Defaults();
        config.Duchies.Add(new DuchyConfig("fells", "{=x}Fells", "north", new string[0], new[] { "c3", "c1", "c2", "c2" }));
        var result = Generate(config, world);

        CollectionAssert.AreEquivalent(new[] { "b_c1", "b_c2", "b_c3" },
            result.Catalog.ChildrenOf("d_fells").Select(t => t.Id).ToArray());
        Assert.AreEqual("k_north", result.Catalog.Get("d_fells")!.ParentId);
        Assert.AreEqual("warden", result.Ledger.HolderOf("d_fells"));
        Assert.AreEqual("c_t1", result.Catalog.Get("b_c4")!.ParentId);
        Assert.AreEqual(0, result.Warnings.Count, string.Join("; ", result.Warnings));
    }

    [TestMethod]
    public void A_duchy_leaves_out_and_reports_settlements_of_another_kingdom()
    {
        var config = RealmsConfig.Defaults();
        config.Duchies.Add(new DuchyConfig("eastmarch", "{=x}Eastmarch", "north", new[] { "town_c", "town_s" }, new[] { "castle_missing" }));
        config.Duchies.Add(new DuchyConfig("empty", "{=x}Empty", "north", new[] { "town_s" }));
        var result = Generate(config);

        Assert.IsNotNull(result.Catalog.Get("d_eastmarch"));
        Assert.IsNull(result.Catalog.Get("d_empty"));
        Assert.AreEqual(2, result.Warnings.Count, string.Join("; ", result.Warnings));
    }

    [TestMethod]
    public void High_title_sits_above_member_kingdoms_and_is_vacant_when_their_rulers_differ()
    {
        var config = RealmsConfig.Defaults();
        var laws = new TitleLaws(SuccessionLaw.Primogeniture, GenderLaw.Agnatic);
        config.HighTitles.Add(new HighTitleConfig("arnor", "{=x}Arnor", new[] { "north", "south", "missing" }, laws));
        var result = Generate(config);

        Assert.AreEqual("e_arnor", result.Catalog.Get("k_north")!.ParentId);
        Assert.AreEqual("e_arnor", result.Catalog.Get("k_south")!.ParentId);
        Assert.IsNull(result.Ledger.HolderOf("e_arnor"));
        Assert.AreEqual(laws, result.Ledger.LawsOn("e_arnor"));
    }

    [TestMethod]
    public void Villages_can_be_left_out()
    {
        var config = RealmsConfig.Defaults();
        config.IncludeVillages = false;

        Assert.IsFalse(Generate(config).Catalog.All.Any(t => t.Rank == TitleRank.Lordship));
    }

    [TestMethod]
    public void Applies_kingdom_laws_and_campaign_default()
    {
        var config = RealmsConfig.Defaults();
        config.DefaultLaws = new TitleLaws(SuccessionLaw.Seniority, GenderLaw.Absolute);
        config.KingdomLaws["south"] = new TitleLaws(SuccessionLaw.Elective, GenderLaw.Absolute);
        var result = Generate(config);

        Assert.AreEqual(config.DefaultLaws, result.Ledger.DefaultLaws);
        Assert.AreEqual(SuccessionLaw.Elective, result.Ledger.LawsOn("k_south")!.Succession);
        Assert.IsNull(result.Ledger.LawsOn("k_north"));
    }
}
