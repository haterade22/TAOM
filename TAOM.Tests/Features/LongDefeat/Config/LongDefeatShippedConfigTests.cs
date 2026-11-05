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
/// The REAL <c>long_defeat/*.json</c> files, read the way the game reads them (phase1-plan.md section 3): they load with no
/// warning, every troop exists in <c>fords_troops.xml</c> with the class and culture the data claims, and the keep numbers keep a
/// straggler group from taking the keep from the smallest wave.
/// </summary>
[TestClass]
public class LongDefeatShippedConfigTests
{
    private static string ModuleData => RepoPaths.RepoPath("Main", "_Module", "ModuleData");

    private static (LongDefeatConfigCatalog Catalog, IModLogger Logger) Load()
    {
        var logger = Substitute.For<IModLogger>();
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(ModuleData);
        return (new LongDefeatConfigCatalog(paths, logger), logger);
    }

    private static Dictionary<string, XElement> Troops() =>
        XDocument.Load(Path.Combine(ModuleData, "fords_of_war", "fords_troops.xml")).Descendants("NPCCharacter")
            .GroupBy(e => (string)e.Attribute("id")!)
            .ToDictionary(g => g.Key, g => g.First());

    private static string Culture(XElement npc) => ((string)npc.Attribute("culture")!).Replace("Culture.", "");

    private static IEnumerable<(string Troop, FormationSlot Slot, string Culture, string Where)> Rows(FortressEntry f)
    {
        foreach (var g in f.Garrison)
            yield return (g.TroopId, g.Slot, f.DefenderCulture, "garrison");
        foreach (var o in f.Shop.Offers)
            yield return (o.TroopId, o.Slot, f.DefenderCulture, "offer " + o.Id);
        for (var w = 1; w <= f.Waves.Authored.Count; w++)
            foreach (var m in f.Waves.Authored[w - 1].Mix)
                yield return (m.TroopId, m.Slot, f.AttackerCulture, "wave " + w);
    }

    [TestMethod]
    public void ShippedFiles_LoadHelmsWithNoWarning()
    {
        var (catalog, logger) = Load();

        Assert.IsTrue(catalog.TryGetFortress("helms", out var helms));
        Assert.AreEqual("taom_rohan_castle_helms_deep_forceatmo", helms.Scene);
        Assert.AreEqual(10, helms.Waves.Authored.Count);
        Assert.AreEqual(150, helms.Garrison.Sum(g => g.Count));
        Assert.AreEqual(600, helms.Shop.StartingGold);
        Assert.AreEqual(5, helms.Shop.Offers.Count);
        Assert.IsNotNull(helms.Muster?.Foot, "new foot must not fall back to the terrace");
        Assert.IsNotNull(helms.Muster?.Archers, "new archers must not fall back to the terrace");
        logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [TestMethod]
    public void ShippedFiles_EveryTroopExistsInTheShippedRoster()
    {
        var troops = Troops();
        var (catalog, _) = Load();

        var missing = catalog.Fortresses.SelectMany(Rows).Where(r => !troops.ContainsKey(r.Troop)).Select(r => r.Troop + " (" + r.Where + ")").ToList();

        CollectionAssert.AreEqual(Array.Empty<string>(), missing, string.Join(", ", missing));
    }

    [TestMethod]
    public void ShippedFiles_ClassMatchesTheXmlGroup_ExceptACavalryTroopOnFoot()
    {
        var troops = Troops();
        var (catalog, _) = Load();

        var wrong = new List<string>();
        foreach (var r in catalog.Fortresses.SelectMany(Rows))
        {
            var group = (string?)troops[r.Troop].Attribute("default_group");
            var expected = group == "Ranged" ? FormationSlot.Ranged : FormationSlot.Infantry;
            if (group != "Ranged" && group != "Infantry" && group != "Cavalry")
                wrong.Add($"{r.Troop} has group {group}");
            else if (r.Slot != expected)
                wrong.Add($"{r.Troop} ({r.Where}) is {r.Slot}, the XML says {group}");
        }

        CollectionAssert.AreEqual(Array.Empty<string>(), wrong, string.Join("; ", wrong));
    }

    [TestMethod]
    public void ShippedFiles_EveryTroopBelongsToTheSideThatFieldsIt()
    {
        var troops = Troops();
        var (catalog, _) = Load();

        var wrong = catalog.Fortresses.SelectMany(Rows).Where(r => Culture(troops[r.Troop]) != r.Culture)
            .Select(r => $"{r.Troop} ({r.Where}) is {Culture(troops[r.Troop])}, expected {r.Culture}").ToList();

        CollectionAssert.AreEqual(Array.Empty<string>(), wrong, string.Join("; ", wrong));
    }

    [TestMethod]
    public void ShippedFiles_SceneAndPlayerTroopExist()
    {
        var (catalog, _) = Load();
        var scenes = File.ReadAllText(Path.Combine(ModuleData, "custom_battle_scenes.xml"));
        var lords = File.ReadAllText(Path.Combine(ModuleData, "characters", "custom_battle_lords.xml"));

        foreach (var f in catalog.Fortresses)
        {
            StringAssert.Contains(scenes, f.Scene);
            StringAssert.Contains(lords, $"id=\"{f.PlayerTroop}\"");
        }
    }

    [TestMethod]
    public void ShippedFiles_KeepMinAttackers_ExceedsTheStragglerShareOfTheSmallestWave()
    {
        var (catalog, _) = Load();

        foreach (var f in catalog.Fortresses)
        {
            var rules = catalog.BuildPlan(f, RunOptions.Default).Rules;
            var smallest = f.Waves.Authored.Min(w => w.Count);
            Assert.IsTrue(rules.KeepMinAttackers > rules.StragglerShare * smallest,
                $"{f.Id}: keepMinAttackers {rules.KeepMinAttackers} must exceed {rules.StragglerShare} of {smallest}");
        }
    }

    [TestMethod]
    public void ShippedFiles_EveryWaveTroopHasABounty()
    {
        var (catalog, _) = Load();

        foreach (var f in catalog.Fortresses)
        {
            var bounties = catalog.BuildPlan(f, RunOptions.Default).Bounties;
            var unpaid = Rows(f).Where(r => r.Where.StartsWith("wave")).Select(r => r.Troop).Distinct().Where(t => !bounties.ContainsKey(t)).ToList();
            CollectionAssert.AreEqual(Array.Empty<string>(), unpaid, string.Join(", ", unpaid));
        }
    }

    private static IEnumerable<(FortressEntry Fortress, string From, string To)> Links() =>
        Load().Catalog.Fortresses.SelectMany(f => f.Waves.Endless.Tiers.Select(l => (f, l.Key, l.Value)));

    [TestMethod]
    public void ShippedFiles_EveryTierLink_GoesUpOneLevel_InsideOneClassAndTheAttackerCulture()
    {
        var troops = Troops();
        var links = Links().ToList();

        Assert.IsTrue(links.Count >= 10, "helms ships its tier chain");
        var wrong = new List<string>();
        foreach (var (fortress, from, to) in links)
        {
            if (!troops.TryGetValue(from, out var a) || !troops.TryGetValue(to, out var b))
            {
                wrong.Add($"{from} to {to}: a troop is not in fords_troops.xml");
                continue;
            }

            if (Culture(a) != fortress.AttackerCulture || Culture(b) != fortress.AttackerCulture)
                wrong.Add($"{from} to {to}: not the {fortress.AttackerCulture} culture");
            if ((string?)a.Attribute("default_group") != (string?)b.Attribute("default_group"))
                wrong.Add($"{from} to {to}: the class changes");
            if (!((int)b.Attribute("level")! > (int)a.Attribute("level")!))
                wrong.Add($"{from} to {to}: the level does not rise");
        }

        CollectionAssert.AreEqual(Array.Empty<string>(), wrong, string.Join("; ", wrong));
    }

    [TestMethod]
    public void ShippedFiles_EveryReachableAttackerTroop_HasABounty_AndATierTargetPaysAtLeastWhatItsSourcePays()
    {
        var (catalog, _) = Load();

        foreach (var f in catalog.Fortresses)
        {
            var bounties = catalog.BuildPlan(f, RunOptions.Default).Bounties;
            var reachable = f.Waves.Authored.SelectMany(w => w.Mix.Select(m => m.TroopId)).Concat(f.Waves.Endless.Targets).Distinct().ToList();
            var unpaid = reachable.Where(id => !bounties.ContainsKey(id)).ToList();
            CollectionAssert.AreEqual(Array.Empty<string>(), unpaid, string.Join(", ", unpaid));
            foreach (var link in f.Waves.Endless.Tiers)
                Assert.IsTrue(bounties[link.Value] >= bounties[link.Key], $"{link.Value} pays {bounties[link.Value]}, less than {link.Key} ({bounties[link.Key]})");
        }
    }

    [TestMethod]
    public void ShippedFiles_TheFourVeteranTroopsAStepOrASummonCanReach_PayABounty()
    {
        var (catalog, _) = Load();
        var bounties = catalog.BuildPlan(catalog.Fortresses.Single(f => f.Id == "helms"), RunOptions.Default).Bounties;

        foreach (var id in new[] { "fow_urukhai_veteranspearman", "fow_urukhai_veteranpikeman", "fow_urukhai_veteraninfantry", "fow_urukhai_veterancrossbowman" })
            Assert.IsTrue(bounties.TryGetValue(id, out var gold) && gold > 0, id + " has no bounty");
    }

    [TestMethod]
    public void ShippedFiles_AllTroopIds_HoldsEveryTierTarget_SoALaunchChecksThemBeforeTheRun()
    {
        var (catalog, _) = Load();

        foreach (var f in catalog.Fortresses)
        {
            var ids = RunPlanTroops.AllTroopIds(catalog.BuildPlan(f, RunOptions.Default));
            var missing = f.Waves.Endless.Targets.Where(id => !ids.Contains(id)).ToList();
            CollectionAssert.AreEqual(Array.Empty<string>(), missing, string.Join(", ", missing));
            foreach (var row in Rows(f))
                CollectionAssert.Contains(ids.ToList(), row.Troop);
        }
    }

    [TestMethod]
    public void ShippedFiles_TheEndlessGrowth_IsTheFirstGuessOfTheApprovedPlan()
    {
        var (catalog, _) = Load();
        var waves = catalog.Fortresses.Single(f => f.Id == "helms").Waves;

        var scale = waves.Endless;

        Assert.AreEqual((0.05f, 0.03f, 3.0f, 3), (scale.HealthPerWave, scale.DamagePerWave, scale.MaxFactor, scale.TierEveryWaves));
        Assert.AreEqual("fow_urukhai_warrior", scale.Tiers["fow_urukhai_fighter"]);
        Assert.AreEqual("fow_urukhai_veterancrossbowman", scale.Tiers["fow_urukhai_crossbowman"]);
        Assert.AreEqual(10, waves.Authored.Count);
    }
}
