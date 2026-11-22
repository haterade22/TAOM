using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat.Config;

/// <summary>
/// The REAL commanders of <c>long_defeat/waves.json</c> (phase2-plan.md section 3, "Shipped-data tests"): they load with no warning, each rank troop is a lord of the roster's culture (in
/// <c>characters/lords.xml</c>, or a stub in <c>custom_battle_lords.xml</c> that <c>lords.xslt</c> rebuilds), each summon troop is one of the roster's own troops and pays a bounty, and a launch
/// finds every one of them before the run.
/// </summary>
[TestClass]
public class LongDefeatShippedCommandersTests
{
    private static string ModuleData => RepoPaths.RepoPath("Main", "_Module", "ModuleData");

    private static (LongDefeatConfigCatalog Catalog, IModLogger Logger) Load()
    {
        var logger = Substitute.For<IModLogger>();
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(ModuleData);
        return (new LongDefeatConfigCatalog(paths, logger), logger);
    }

    private static string Culture(XElement npc) => ((string?)npc.Attribute("culture") ?? "").Replace("Culture.", "");

    private static Dictionary<string, string> Troops() =>
        XDocument.Load(Path.Combine(ModuleData, "fords_of_war", "fords_troops.xml")).Descendants("NPCCharacter")
            .GroupBy(e => (string)e.Attribute("id")!)
            .ToDictionary(g => g.Key, g => Culture(g.First()));

    /// <summary>Every lord by id with his culture: the full definitions of lords.xml, then the stubs of custom_battle_lords.xml that lords.xslt rebuilds.</summary>
    private static Dictionary<string, string> Lords()
    {
        var lords = new Dictionary<string, string>();
        foreach (var file in new[] { "lords.xml", "custom_battle_lords.xml" })
        {
            foreach (var npc in XDocument.Load(Path.Combine(ModuleData, "characters", file)).Descendants("NPCCharacter"))
            {
                var id = (string)npc.Attribute("id")!;
                if (!lords.ContainsKey(id))
                    lords[id] = Culture(npc);
            }
        }

        return lords;
    }

    /// <summary>The own-troops rule: a rank troop is a lord of the roster's culture, a summon troop a troop of it. Every breach, named.</summary>
    private static List<string> CultureProblems(CommanderPlan plan, string attackerCulture)
    {
        var lords = Lords();
        var troops = Troops();
        var problems = new List<string>();
        foreach (var rank in plan.Ranks.Where(r => !lords.TryGetValue(r.TroopId, out var c) || c != attackerCulture))
            problems.Add($"rank {rank.Id}: {rank.TroopId} is not a {attackerCulture} lord");
        foreach (var id in plan.SummonTroopIds.Where(id => !troops.TryGetValue(id, out var c) || c != attackerCulture))
            problems.Add($"summon troop {id} is not a {attackerCulture} troop");
        return problems;
    }

    private static CommanderPlan SyntheticMordor(string lord, string summonTroop)
    {
        var fortress = "{'fortresses':[{'id':'minas','name':'{=K}Minas','scene':'s','defenderCulture':'gondor','attackerCulture':'mordor','playerTroop':'lord_4_1','waves':'r','shop':'sh'," +
                       "'garrison':[{'troop':'g','class':'Infantry','count':10}],'keep':null}]}";
        var waves = "{'rules':{},'bounties':{'" + summonTroop + "':20},'rosters':{'r':{'scaling':{'countPerWave':10,'maxCount':300}," +
                    "'commanders':{'every':4,'ranks':[{'rank':'captain','name':'{=R}Captain','troop':'" + lord + "','bounty':100,'health':2,'abilities':['summon']}]," +
                    "'abilities':{'summon':{'kind':'Summon','name':'{=A}Summon','firstSeconds':30,'cooldownSeconds':120,'count':20,'mix':[{'troop':'" + summonTroop + "','class':'Infantry','weight':1}]}}}," +
                    "'waves':[{'count':110,'mix':[{'troop':'" + summonTroop + "','class':'Infantry','weight':1}]}]}}}";
        var shop = "{'shops':{'sh':{'startingGold':600,'offers':[{'id':'m','name':'M','troop':'g','class':'Infantry','packSize':12,'price':180}]}}}";
        var catalog = LongDefeatConfigCatalog.FromTexts(fortress, waves, shop, Substitute.For<IModLogger>(), "{}", "{'boons':[]}", "{'classes':[]}");
        return catalog.Fortresses.Single().Waves.Commanders;
    }

    [TestMethod]
    public void ShippedCommanders_LoadWithNoWarning_ThreeRanksTwoAbilitiesAndTheApprovedStepNumbers()
    {
        var (catalog, logger) = Load();

        var plan = catalog.Fortresses.Single(f => f.Id == "helms").Waves.Commanders;

        Assert.AreEqual(4, plan.Every);
        CollectionAssert.AreEqual(new[] { "lord_I3_1", "lord_I1_1", "lord_I4_1" }, plan.Ranks.Select(r => r.TroopId).ToArray(), "Lugdush, Ugluk, Lurtz (assumption A5)");
        CollectionAssert.AreEqual(new[] { 2f, 3f, 4f }, plan.Ranks.Select(r => r.Health).ToArray(), "D11");
        CollectionAssert.AreEqual(new[] { 150, 250, 400 }, plan.Ranks.Select(r => r.Bounty).ToArray());
        CollectionAssert.AreEquivalent(new[] { "war_cry", "summon_uruks" }, plan.Abilities.Keys.ToArray());
        Assert.AreEqual(new CommanderBeyond(0.25f, 0.9f, 5, 10), plan.Beyond);
        logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [TestMethod]
    public void ShippedCommanders_EveryRankTroopIsALordOfTheRostersCulture_AndEverySummonTroopOneOfItsTroops()
    {
        var (catalog, _) = Load();

        foreach (var f in catalog.Fortresses)
        {
            var problems = CultureProblems(f.Waves.Commanders, f.AttackerCulture);
            CollectionAssert.AreEqual(Array.Empty<string>(), problems, string.Join("; ", problems));
        }
    }

    [TestMethod]
    public void ShippedCommanders_EverySummonTroopPaysABounty_AndAllTroopIdsHoldsEveryCommanderAndSummonTroop()
    {
        var (catalog, _) = Load();

        foreach (var f in catalog.Fortresses)
        {
            var plan = catalog.BuildPlan(f, RunOptions.Default);
            var unpaid = f.Waves.Commanders.SummonTroopIds.Where(id => !plan.Bounties.TryGetValue(id, out var gold) || gold <= 0).ToList();
            CollectionAssert.AreEqual(Array.Empty<string>(), unpaid, string.Join(", ", unpaid));
            var ids = RunPlanTroops.AllTroopIds(plan);
            var missing = f.Waves.Commanders.TroopIds.Where(id => !ids.Contains(id)).ToList();
            CollectionAssert.AreEqual(Array.Empty<string>(), missing, string.Join(", ", missing));
        }
    }

    /// <summary>The ids of the Custom Battle stubs that lords.xslt rebuilds with <c>is_hero</c> true: the stub is bare, so the template is what makes the lord a hero.</summary>
    private static HashSet<string> StubHeroes()
    {
        XNamespace xsl = "http://www.w3.org/1999/XSL/Transform";
        var stubs = XDocument.Load(Path.Combine(ModuleData, "characters", "custom_battle_lords.xml")).Descendants("NPCCharacter").Select(e => (string)e.Attribute("id")!).ToHashSet();
        return XDocument.Load(Path.Combine(ModuleData, "lords.xslt")).Descendants(xsl + "template")
            .Where(t => t.Descendants(xsl + "attribute").Any(a => (string?)a.Attribute("name") == "is_hero" && a.Value.Trim() == "true"))
            .Select(t => ((string?)t.Attribute("match") ?? "").Replace("NPCCharacter[@id='", "").Replace("']", ""))
            .Where(stubs.Contains).ToHashSet();
    }

    [TestMethod]
    public void ShippedCommanders_EveryRankTroopIsAHero_InLordsXml_OrAStubThatLordsXsltRebuildsAsOne()
    {
        var (catalog, _) = Load();
        var heroes = XDocument.Load(Path.Combine(ModuleData, "characters", "lords.xml")).Descendants("NPCCharacter")
            .Where(e => (string?)e.Attribute("is_hero") == "true").Select(e => (string)e.Attribute("id")!).ToHashSet();
        var stubHeroes = StubHeroes();

        Assert.IsTrue(stubHeroes.Contains("lord_4_1") && !stubHeroes.Contains("lord_I3_1"), "the stub read finds Theoden, and Lugdush is a lords.xml row, not a stub");
        foreach (var rank in catalog.Fortresses.SelectMany(f => f.Waves.Commanders.Ranks))
            Assert.IsTrue(heroes.Contains(rank.TroopId) || stubHeroes.Contains(rank.TroopId), rank.TroopId + " is no hero of lords.xml and no hero stub");
    }

    // D54: Gorbag, Gothmog, then the Witch-king, with the numbers of Helm's Deep. The names are the lords' own name keys, so they are translated already.
    [TestMethod]
    public void ShippedMordorCommanders_AreGorbagGothmogAndTheWitchKing_WithTheHelmsDeepNumbers_AndMordorsOwnAbilityNames()
    {
        var (catalog, logger) = Load();

        var plan = catalog.Fortresses.Single(f => f.Id == "minas_tirith").Waves.Commanders;
        var helms = catalog.Fortresses.Single(f => f.Id == "helms").Waves.Commanders;

        Assert.AreEqual(4, plan.Every);
        CollectionAssert.AreEqual(new[] { "lord_1_63", "lord_1_30", "lord_1_15" }, plan.Ranks.Select(r => r.TroopId).ToArray());
        CollectionAssert.AreEqual(new[] { "{=aom_lord_1_63_name}Gorbag, Captain of Minas Morgul", "{=aom_lord_1_30_name}Gothmog, Lieutenant of Morgul", "{=aom_lord_1_15_name}Witch-King of Angmar" },
            plan.Ranks.Select(r => r.Name).ToArray());
        CollectionAssert.AreEqual(helms.Ranks.Select(r => r.Health).ToArray(), plan.Ranks.Select(r => r.Health).ToArray());
        CollectionAssert.AreEqual(helms.Ranks.Select(r => r.Bounty).ToArray(), plan.Ranks.Select(r => r.Bounty).ToArray());
        Assert.AreEqual(helms.Beyond, plan.Beyond);
        CollectionAssert.AreEquivalent(new[] { "war_cry", "summon_uruks" }, plan.Abilities.Keys.ToArray());
        foreach (var ability in plan.Abilities.Values)
            Assert.IsFalse(ability.Name.Contains("Isengard") || ability.Name.Contains("White Hand"), ability.Name + " is an Isengard name (D55)");
        CollectionAssert.AreEquivalent(new[] { "fow_mordor_uruk_baraddurguard", "fow_mordor_uruk_crossbow" }, plan.SummonTroopIds.ToArray(), "the Barad-dur guard, so a summon is stronger than the wave it joins");
        logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    // The rank names above are the lords' own: each must equal the name its lords.xslt template writes, or the HUD shows one name and the body another.
    [TestMethod]
    public void ShippedMordorCommanders_RankNamesEqualTheNamesLordsXsltGivesThoseLords()
    {
        XNamespace xsl = "http://www.w3.org/1999/XSL/Transform";
        var (catalog, _) = Load();
        var templates = XDocument.Load(Path.Combine(ModuleData, "lords.xslt")).Descendants(xsl + "template").ToList();

        foreach (var rank in catalog.Fortresses.Single(f => f.Id == "minas_tirith").Waves.Commanders.Ranks)
        {
            var template = templates.Single(t => (string?)t.Attribute("match") == "NPCCharacter[@id='" + rank.TroopId + "']");
            Assert.AreEqual(template.Descendants(xsl + "attribute").First(a => (string?)a.Attribute("name") == "name").Value, rank.Name, rank.TroopId);
        }
    }

    [TestMethod]
    public void AMordorRosterWithGorbag_SummoningMordorUruks_PassesTheOwnTroopsRule_ThroughTheCustomBattleStub()
    {
        var plan = SyntheticMordor("lord_1_63", "fow_mordor_uruk_warrior");

        Assert.AreEqual(1, plan.Ranks.Count);
        CollectionAssert.AreEqual(Array.Empty<string>(), CultureProblems(plan, "mordor"));
    }

    [TestMethod]
    public void TheSameMordorRoster_SummoningUrukHai_IsRefused()
    {
        var plan = SyntheticMordor("lord_1_63", "fow_urukhai_warrior");

        var problems = CultureProblems(plan, "mordor");

        Assert.AreEqual(1, problems.Count);
        StringAssert.Contains(problems[0], "fow_urukhai_warrior");
    }

    [TestMethod]
    public void AMordorRoster_WithAnIsengardLord_IsRefused()
    {
        var plan = SyntheticMordor("lord_I3_1", "fow_mordor_uruk_warrior");

        var problems = CultureProblems(plan, "mordor");

        Assert.AreEqual(1, problems.Count);
        StringAssert.Contains(problems[0], "lord_I3_1");
    }
}
