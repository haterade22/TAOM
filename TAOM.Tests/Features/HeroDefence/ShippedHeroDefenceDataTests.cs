using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.HeroDefence;
using TAOM.Features.HeroDefence.Domain;
using TAOM.Tests.Features.HeroDefence.Domain;

namespace TAOM.Tests.Features.HeroDefence;

/// <summary>The shipped hero_defence files against the troop trees and against Mike's wave rules. Reads the real files, with no game.</summary>
[TestClass]
public class ShippedHeroDefenceDataTests
{
    // The generator's pattern: settlement militia ids end in _militia_spearman, _militia_archer or their _veteran_ forms.
    // Line units with "militia" elsewhere in the id, such as rohan_westfold_militiaman, stay.
    private static readonly Regex Militia = new("_militia_(veteran_)?(spearman|archer)$");
    private static readonly string ModuleData = Path.Combine(Locate(), "ModuleData");

    private static string Locate()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "Main", "_Module");
            if (File.Exists(Path.Combine(candidate, "SubModule.xml")))
                return candidate;
        }

        throw new InvalidOperationException("Main/_Module not found above " + AppContext.BaseDirectory);
    }

    private static string Data(string name) => File.ReadAllText(Path.Combine(ModuleData, "hero_defence", name));

    private static HeroDefenceConfigCatalog Shipped(IModLogger? logger = null) =>
        HeroDefenceConfigCatalog.FromTexts(Data("waves.json"), Data("cultures.json"), Data("rosters.json"), logger ?? Substitute.For<IModLogger>());

    private static WaveCatalog Waves => Shipped().Waves;

    private static Dictionary<string, List<(string Id, int Level, string Class)>> TreesFromXml()
    {
        var files = Directory.GetFiles(Path.Combine(ModuleData, "troops"), "troops_*.xml")
            .Where(path => Path.GetFileName(path) != "troops_feral_orcs.xml")
            .Select(path => XDocument.Load(path).Root!.Elements("NPCCharacter").ToList())
            .ToList();
        var upgradedInto = new HashSet<string>(files.SelectMany(f => f).SelectMany(Targets));
        var trees = new Dictionary<string, List<(string, int, string)>>();
        foreach (var file in files)
        {
            var own = file.GroupBy(CultureOf).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).First().Key;
            foreach (var npc in file)
            {
                var id = (string)npc.Attribute("id")!;
                var linked = Targets(npc).Any() || upgradedInto.Contains(id);
                if (CultureOf(npc) != own || (string?)npc.Attribute("occupation") != "Soldier" || Militia.IsMatch(id) || !linked)
                    continue;

                if (!trees.TryGetValue(own, out var rows))
                    trees[own] = rows = new List<(string, int, string)>();
                rows.Add((id, (int)npc.Attribute("level")!, (string)npc.Attribute("default_group")!));
            }
        }

        return trees;
    }

    private static string CultureOf(XElement npc)
    {
        var culture = ((string?)npc.Attribute("culture") ?? "").Replace("Culture.", "");
        return culture == "shaghana" ? "aserai" : culture;
    }

    private static IEnumerable<string> Targets(XElement npc) =>
        npc.Element("upgrade_targets")?.Elements("upgrade_target").Select(t => ((string)t.Attribute("id")!).Replace("NPCCharacter.", "")) ?? Enumerable.Empty<string>();

    [TestMethod]
    public void ShippedFiles_LoadWithNoWarning()
    {
        var logger = Substitute.For<IModLogger>();

        var catalog = Shipped(logger);
        Assert.AreEqual(25, catalog.Waves.Cultures.Count);

        logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void RosterFile_MatchesTheTroopTreesExactly()
    {
        var trees = TreesFromXml();
        var shipped = JObject.Parse(Data("rosters.json"));

        CollectionAssert.AreEquivalent(trees.Keys.ToList(), shipped.Properties().Select(p => p.Name).ToList(), "the cultures differ");
        foreach (var tree in trees)
        {
            var expected = tree.Value.OrderBy(r => r.Level).ThenBy(r => r.Id, StringComparer.Ordinal).Select(r => $"{r.Id}|{r.Level}|{r.Class}").ToList();
            var actual = shipped[tree.Key]!.Select(r => $"{(string)r["id"]!}|{(int)r["level"]!}|{(string)r["class"]!}").ToList();
            CollectionAssert.AreEqual(expected, actual, $"{tree.Key} has drifted; run tools/generate_hero_defence_rosters.py");
        }
    }

    [TestMethod]
    public void WaveOne_IsMordorAtLevelsOneAndSix()
    {
        var brief = new CreepWavePlan(Waves, 1UL).Brief(1);

        Assert.AreEqual("mordor", brief.CultureId);
        CollectionAssert.AreEquivalent(new[] { 1, 6 }, brief.Packs.SelectMany(p => p.Creeps).Select(c => c.Level).Distinct().ToArray());
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("{}")]
    [DataRow(@"{ ""opening"": 3 }")]
    public void OpeningNotWritten_StillPinsMordorThenErebor(string? waves)
    {
        var catalog = HeroDefenceConfigCatalog.FromTexts(waves, Data("cultures.json"), Data("rosters.json"), Substitute.For<IModLogger>());
        var plan = new CreepWavePlan(catalog.Waves, 1UL);

        Assert.AreEqual("mordor", plan.Brief(1).CultureId);
        Assert.AreEqual("erebor", plan.Brief(2).CultureId);
    }

    [TestMethod]
    public void WaveTwo_IsEreborAtLevelEleven()
    {
        var brief = new CreepWavePlan(Waves, 1UL).Brief(2);
        var creeps = brief.Packs.SelectMany(p => p.Creeps).ToList();

        Assert.AreEqual("erebor", brief.CultureId);
        Assert.AreEqual(36, creeps.Count, "30 + 6, and wall waves have the count factor 1");
        Assert.IsTrue(creeps.All(c => c.Level == 11));
    }

    [DataTestMethod]
    [DataRow(1UL)]
    [DataRow(2UL)]
    [DataRow(12345UL)]
    public void ThirtyWaves_NeverRepeatACultureAndGrowInEveryWay(ulong seed)
    {
        var plan = new CreepWavePlan(Waves, seed);
        var briefs = Enumerable.Range(1, 30).Select(plan.Brief).ToList();

        for (var i = 1; i < briefs.Count; i++)
        {
            Assert.AreNotEqual(briefs[i - 1].CultureId, briefs[i].CultureId, $"wave {i + 1}");
            Assert.IsTrue(briefs[i].HealthFactor >= briefs[i - 1].HealthFactor, $"wave {i + 1} health");
            Assert.IsTrue(briefs[i].DamageFactor >= briefs[i - 1].DamageFactor, $"wave {i + 1} damage");
            Assert.IsTrue(briefs[i].ArmourPercent >= briefs[i - 1].ArmourPercent, $"wave {i + 1} armour");
            Assert.IsTrue(briefs[i].SwingPercent >= briefs[i - 1].SwingPercent, $"wave {i + 1} swing");
            Assert.IsTrue(briefs[i].PowerIds.Count >= briefs[i - 1].PowerIds.Count, $"wave {i + 1} powers");
        }

        Assert.IsTrue(briefs[29].Packs.SelectMany(p => p.Creeps).Count() > briefs[0].Packs.SelectMany(p => p.Creeps).Count());
    }

    [TestMethod]
    public void SeededRun_ClearsThirtyWavesAndAFallenHeroReturns_ThenLeaksEndIt()
    {
        var catalog = Shipped();
        var plan = new CreepWavePlan(catalog.Waves, 77UL);
        const string HeroId = DefenceDriver.Melee;
        var run = new HeroDefenceRun(new DefencePlan(catalog.Rules, new[] { new HeroDef(HeroId, HeroClass.Melee, 500) }, DefenceDriver.Map, plan.Brief));
        Assert.AreEqual(BuyResult.Bought, run.TryBuyHero(HeroId));
        Assert.AreEqual(PlaceResult.Placed, run.TryPlaceHero(HeroId, DefenceDriver.OnRoad(100f)));

        var nextKey = 1;
        string? lastCulture = null;
        for (var wave = 1; wave <= 30; wave++)
        {
            Assert.AreEqual(StartResult.Started, run.TryStartWave(), $"wave {wave}");
            if (wave == 3)
                run.HeroDown(HeroId);

            DefenceEffects last = new();
            for (var guard = 0; run.Phase == DefencePhase.Wave && guard < 100; guard++)
            {
                last = run.Tick(new DefenceSnapshot(7f, null));
                if (last.WaveBegan != null)
                {
                    Assert.AreNotEqual(lastCulture, last.WaveBegan.CultureId, $"wave {wave}");
                    lastCulture = last.WaveBegan.CultureId;
                }

                foreach (var creep in last.CreepBatches.SelectMany(batch => batch.Pack.Creeps.Select(spec => (spec, batch.Slot))))
                {
                    var key = new AgentKey(nextKey++);
                    run.CreepSpawned(key, creep.spec, creep.Slot);
                    run.CreepGone(key, CreepEnd.Killed);
                }
            }

            Assert.AreEqual(DefencePhase.Pause, run.Phase, $"wave {wave} did not clear");
            if (wave == 3)
            {
                Assert.AreEqual(DefenceHeroState.Placed, run.StateOf(HeroId));
                Assert.AreEqual(HeroId, last.HeroSpawns.Single().HeroId);
            }
        }

        Assert.AreEqual(30, run.WavesCleared);
        var kills = run.Kills;

        var live = new List<AgentKey>();
        for (var guard = 0; run.Phase != DefencePhase.Ended && guard < 1000; guard++)
        {
            if (run.Phase == DefencePhase.Pause)
                run.TryStartWave();

            var fx = run.Tick(new DefenceSnapshot(7f, new FieldReading(live.Select(key => new UnitSample(key, DefenceDriver.Exit)).ToList())));
            live.RemoveAll(fx.Removals.Contains);
            foreach (var creep in fx.CreepBatches.SelectMany(batch => batch.Pack.Creeps.Select(spec => (spec, batch.Slot))))
            {
                var key = new AgentKey(nextKey++);
                run.CreepSpawned(key, creep.spec, creep.Slot);
                live.Add(key);
            }
        }

        Assert.AreEqual(DefencePhase.Ended, run.Phase);
        Assert.AreEqual(0, run.Lives);
        Assert.AreEqual(new DefenceResult(30, kills), run.Result);
    }

    [TestMethod]
    public void ThreeHundredWaves_FieldOnlyRealTroopsInEveryPack()
    {
        var known = new HashSet<string>(TreesFromXml().Values.SelectMany(rows => rows.Select(r => r.Id)));
        var plan = new CreepWavePlan(Waves, 9UL);

        for (var wave = 1; wave <= 300; wave++)
        {
            var brief = plan.Brief(wave);
            Assert.IsTrue(brief.Packs.Count > 0, $"wave {wave} is empty");
            foreach (var creep in brief.Packs.SelectMany(p => p.Creeps).Where(c => c.Rank == CreepRank.Soldier))
                Assert.IsTrue(known.Contains(creep.TroopId), $"wave {wave}: {creep.TroopId}");
        }
    }

    [TestMethod]
    public void EveryCulture_AppearsWithinSixtyWaves()
    {
        var plan = new CreepWavePlan(Waves, 4UL);

        var seen = Enumerable.Range(1, 60).Select(w => plan.Brief(w).CultureId).Distinct().ToList();

        Assert.AreEqual(25, seen.Count);
    }

    [DataTestMethod]
    [DataRow("rivendell", 5)]
    [DataRow("lindon", 5)]
    [DataRow("lothlorien", 7)]
    [DataRow("mirkwood", 13)]
    public void LateCultures_NeverAppearBeforeTheirEntryWave(string culture, int entry)
    {
        foreach (var seed in Enumerable.Range(1, 8))
        {
            var plan = new CreepWavePlan(Waves, (ulong)seed);
            for (var wave = 1; wave < entry; wave++)
                Assert.AreNotEqual(culture, plan.Brief(wave).CultureId, $"seed {seed} wave {wave}");
        }
    }

    [TestMethod]
    public void EveryLeader_LoadsInCustomGameWithABattleKit()
    {
        var subModule = XDocument.Load(Path.Combine(Locate(), "SubModule.xml"));
        var lordsNode = subModule.Descendants("XmlNode").Single(node => (string?)node.Element("XmlName")?.Attribute("path") == "characters/lords");
        Assert.IsTrue(lordsNode.Descendants("GameType").Any(type => (string?)type.Attribute("value") == "CustomGame"), "characters/lords must load in Custom Game");

        var folder = Path.Combine(ModuleData, "characters");
        // lords.xslt rebuilds these stubs into full characters, and CustomBattleLordStubsTests guards that.
        var stubs = new HashSet<string>(XDocument.Load(Path.Combine(folder, "custom_battle_lords.xml")).Root!.Elements("NPCCharacter").Select(npc => (string)npc.Attribute("id")!));
        var lords = XDocument.Load(Path.Combine(folder, "lords.xml")).Root!.Elements("NPCCharacter").ToDictionary(npc => (string)npc.Attribute("id")!);

        var problems = new List<string>();
        foreach (var id in Waves.Cultures.SelectMany(c => c.Leaders).Where(id => !stubs.Contains(id)))
        {
            if (!lords.TryGetValue(id, out var npc))
                problems.Add(id + ": not in characters/lords.xml");
            else if (!HasBattleKit(npc))
                problems.Add(id + ": no battle equipment");
        }

        CollectionAssert.AreEqual(new List<string>(), problems);
    }

    private static bool HasBattleKit(XElement npc)
    {
        var entries = npc.Element("Equipments")?.Elements().Where(e => e.Name == "EquipmentRoster" || e.Name == "EquipmentSet") ?? Enumerable.Empty<XElement>();
        return npc.Attribute("battleTemplate") != null || entries.Any(e => (string?)e.Attribute("civilian") != "true");
    }

    [TestMethod]
    public void EveryCulture_ListsThreeDifferentLeaders()
    {
        foreach (var culture in Waves.Cultures)
            Assert.AreEqual(3, culture.Leaders.Distinct().Count(), culture.Id);
    }

    [DataTestMethod]
    [DataRow("lord_1_75")]
    [DataRow("lord_1_34")]
    [DataRow("lord_M1_11")]
    [DataRow("lord_M3_3")]
    [DataRow("lord_R1_1")]
    [DataRow("lord_R1_5")]
    public void ABuyableHero_NeverLeadsAWave(string heroId)
    {
        CollectionAssert.DoesNotContain(Waves.Cultures.SelectMany(c => c.Leaders).ToList(), heroId);
    }

    /// <summary>The plan: famous villains appear only at general rank, the third leader of a culture.</summary>
    [DataTestMethod]
    [DataRow("lord_1_15")]
    [DataRow("lord_1_48_1")]
    [DataRow("lord_I1_1")]
    [DataRow("lord_I2_1")]
    [DataRow("lord_I3_1")]
    [DataRow("lord_I4_1")]
    [DataRow("lord_GB1_2")]
    [DataRow("lord_GB1_4")]
    [DataRow("lord_GB1_5")]
    public void AFamousVillain_LeadsOnlyAsAGeneral(string lordId)
    {
        Assert.AreEqual(25, Waves.Cultures.Count);
        foreach (var culture in Waves.Cultures)
            CollectionAssert.DoesNotContain(culture.Leaders.Take(2).ToList(), lordId, culture.Id);
    }
}
