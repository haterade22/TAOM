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

    [TestMethod]
    public void ShippedAffixes_LoadWithNoWarning_WithTheApprovedScheduleAndEveryNumberSet()
    {
        var (catalog, logger) = Load();
        var plan = catalog.BuildPlan(catalog.Fortresses.Single(f => f.Id == "helms"), RunOptions.Default).Affixes;

        Assert.AreEqual((0, 1, 1, 2, 2, 3, 3), (plan.CountFor(4), plan.CountFor(5), plan.CountFor(9), plan.CountFor(10), plan.CountFor(14), plan.CountFor(15), plan.CountFor(40)));
        foreach (var power in AffixPlan.Ids)
            foreach (var key in AffixPlan.Keys[power])
                Assert.AreNotEqual(0f, plan.Value(power, key), power + "." + key + " reads as 0: missing from the file or refused");
        Assert.AreEqual(-30f, plan.Value("eye", "aimErrorPercent"), "negative aim error is better aim");
        logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [TestMethod]
    public void ShippedAffixes_EveryFortressesAttackerCulture_HasAllEightNames()
    {
        var (catalog, _) = Load();

        foreach (var f in catalog.Fortresses)
        {
            var plan = catalog.BuildPlan(f, RunOptions.Default).Affixes;
            var unnamed = AffixPlan.Ids.Where(id => plan.NameOf(id) == null).ToList();
            CollectionAssert.AreEqual(Array.Empty<string>(), unnamed, f.Id + " (" + f.AttackerCulture + ") lacks names for " + string.Join(", ", unnamed));
        }
    }

    [TestMethod]
    public void ShippedAffixes_TheMordorTableIsComplete_SoPhaseFiveNeedsNoNewNames()
    {
        var root = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(ModuleData, "long_defeat", "affixes.json")));
        var mordor = (Newtonsoft.Json.Linq.JObject)root["names"]!["mordor"]!;

        CollectionAssert.AreEquivalent(AffixPlan.Ids.ToList(), mordor.Properties().Select(p => p.Name).ToList());
        Assert.AreEqual("{=TAOM_LD_Affix_Mordor_Terror}Winged Terror", (string?)mordor["terror"]);
    }

    [TestMethod]
    public void ShippedAffixes_EveryNameKeyIsRegisteredWithTheSameDefault()
    {
        var rows = XDocument.Load(Path.Combine(ModuleData, "taom_module_strings.xml")).Descendants("string")
            .ToDictionary(e => (string)e.Attribute("id")!, e => (string)e.Attribute("text")!);
        var root = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(ModuleData, "long_defeat", "affixes.json")));

        var names = root["names"]!.Children<Newtonsoft.Json.Linq.JProperty>().SelectMany(c => ((Newtonsoft.Json.Linq.JObject)c.Value).Properties()).Select(p => (string)p.Value!).ToList();

        Assert.AreEqual(16, names.Count);
        foreach (var raw in names)
        {
            var key = raw.Substring(2, raw.IndexOf('}') - 2);
            Assert.IsTrue(rows.TryGetValue(key, out var registered), key + " is not in taom_module_strings.xml");
            Assert.AreEqual(raw, registered, key);
        }
    }

    private static string BoonsText() => File.ReadAllText(Path.Combine(ModuleData, "long_defeat", "boons.json"));

    private static string Pascal(string snake) => string.Concat(snake.Split('_').Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1)));

    [TestMethod]
    public void ShippedBoons_LoadWithNoWarning_AndNoRowWasSkipped()
    {
        var (catalog, logger) = Load();
        var plan = catalog.BuildPlan(catalog.Fortresses.Single(f => f.Id == "helms"), RunOptions.Default).Boons;
        var inFile = Newtonsoft.Json.Linq.JObject.Parse(BoonsText())["boons"]!.Count();

        Assert.AreEqual(inFile, plan.Rows.Count, "a row the reader refused is missing from the pool");
        logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains("boons.json")));
    }

    [TestMethod]
    public void ShippedBoons_SoFarHoldEnoughRowsToShowThreeCards_PerPoolThisSliceBuilds()
    {
        var (catalog, _) = Load();
        var rows = catalog.BuildPlan(catalog.Fortresses.Single(f => f.Id == "helms"), RunOptions.Default).Boons.Rows;

        Assert.IsTrue(rows.Count(r => r.Category == BoonCategory.WarChest) >= 3, "war chest rows");
        Assert.IsTrue(rows.Count(r => r.Category == BoonCategory.Fortress) >= 3, "fortress rows");
        Assert.AreEqual(2, rows.Count(r => r.Category == BoonCategory.Legend), "Legend rows");
        Assert.IsTrue(rows.Where(r => r.Category == BoonCategory.Legend).All(r => r.Effects.Any(e => e.Key == BoonKeys.AffixCount)), "this slice's Legend rows cut affixes");
        Assert.IsTrue(rows.Any(r => r.HasDrawback), "some boons carry a real drawback");
    }

    [TestMethod]
    public void ShippedBoons_EveryNameTextAndDrawbackKey_IsTheRowsOwn_AndRegisteredWithTheSameDefault()
    {
        var strings = XDocument.Load(Path.Combine(ModuleData, "taom_module_strings.xml")).Descendants("string")
            .ToDictionary(e => (string)e.Attribute("id")!, e => (string)e.Attribute("text")!);
        var (catalog, _) = Load();
        var rows = catalog.BuildPlan(catalog.Fortresses.Single(f => f.Id == "helms"), RunOptions.Default).Boons.Rows;

        foreach (var row in rows)
        {
            var key = "TAOM_LD_Boon_" + Pascal(row.Id);
            Assert.IsTrue(row.Name.StartsWith("{=" + key + "}", StringComparison.Ordinal), row.Id + " name key");
            Assert.IsTrue(row.Text.StartsWith("{=" + key + "_Text}", StringComparison.Ordinal), row.Id + " text key");
            var raws = new List<(string Key, string Raw)> { (key, row.Name), (key + "_Text", row.Text) };
            if (row.HasDrawback)
            {
                Assert.IsTrue(row.DrawbackText.StartsWith("{=" + key + "_Drawback}", StringComparison.Ordinal), row.Id + " drawback key");
                raws.Add((key + "_Drawback", row.DrawbackText));
            }

            foreach (var (k, raw) in raws)
            {
                Assert.IsTrue(strings.TryGetValue(k, out var registered), k + " is not in taom_module_strings.xml");
                Assert.AreEqual(raw, registered, k);
            }
        }
    }

    [TestMethod]
    public void ShippedBoons_TheFirstOffersOfSeedOne_ArePinned_ToTheSameVectorsAsThePythonReplay()
    {
        var (catalog, _) = Load();
        var plan = catalog.BuildPlan(catalog.Fortresses.Single(f => f.Id == "helms"), RunOptions.Default).Boons;
        var deck = new BoonDeck(plan, 1UL, 2);
        for (var n = 1; n <= 3; n++)
            deck.Cleared(n, n);
        deck.TryReroll();
        deck.TryReroll();

        // pick 1: draw 0, then each reroll leaves out the three just shown. Over this file's twelve rows; the vectors move when a later slice adds rows (and tools/tests/test_run_long_defeat.py moves with them).
        CollectionAssert.AreEqual(new[] { "granary_stores", "glittering_caves_refuge", "weregild_of_the_mark" }, deck.Picks[0].Draws[0].Ids.ToArray());
        CollectionAssert.AreEqual(new[] { "stout_hearts_at_the_gate", "the_long_watch", "haggler_of_edoras" }, deck.Picks[0].Draws[1].Ids.ToArray());
        CollectionAssert.AreEqual(new[] { "granary_stores", "weregild_of_the_mark", "healers_of_the_hold" }, deck.Picks[0].Draws[2].Ids.ToArray());

        // Reroll twice again from a fresh deck, take the first card of draw 1, and the second pick is drawn over the eleven rows left.
        var second = new BoonDeck(plan, 1UL, 1);
        for (var n = 1; n <= 3; n++)
            second.Cleared(n, n);
        second.TryReroll();
        second.TryPick("stout_hearts_at_the_gate", 3, 4);
        for (var n = 4; n <= 6; n++)
            second.Cleared(n, n);
        CollectionAssert.AreEqual(new[] { "light_of_the_phial", "riddermark_levy", "haggler_of_edoras" }, second.Offer.Select(r => r.Id).ToArray());
    }

    [TestMethod]
    public void ShippedBoons_ClassAndTroopRows_NameARealClass_AndAimOnlyAtTheirOwnGroup()
    {
        var (catalog, _) = Load();
        var helms = catalog.Fortresses.Single(f => f.Id == "helms");
        var rows = catalog.BuildPlan(helms, RunOptions.Default).Boons.Rows;
        var classes = catalog.ClassesFor(helms).Select(c => c.Id).ToList();

        foreach (var id in classes)
            Assert.IsTrue(rows.Count(r => r.Category == BoonCategory.Class && r.ClassId == id) >= 3, id + " needs 3 rows to show a full offer");
        Assert.IsTrue(rows.Where(r => r.Category == BoonCategory.Class).All(r => classes.Contains(r.ClassId)), "a class row names a class the fortress offers");
        foreach (var category in new[] { BoonCategory.Infantry, BoonCategory.Archers, BoonCategory.Cavalry })
        {
            var own = rows.Where(r => r.Category == category).ToList();
            Assert.IsTrue(own.Count >= 3, category + " rows");
            var group = Enum.GetValues(typeof(BoonTarget)).Cast<BoonTarget>().Single(t => BoonTargets.CategoryOf(t) == category);
            Assert.IsTrue(own.All(r => r.Effects.Concat(r.Drawback).All(e => e.Target == group)), category + " rows change their own group only");
        }

        Assert.IsTrue(rows.All(r => r.Effects.Concat(r.Drawback).All(e => BoonKeys.IsTargeted(e.Key) == e.Target.HasValue)), "a targeted key names its target, and no other key does");
    }

    [TestMethod]
    public void ShippedHelms_HoldsATroopOfEachGroup_ReadFromTheXmlDefaultGroupTheWayTheEngineClassifiesIt()
    {
        var (catalog, _) = Load();
        var helms = catalog.Fortresses.Single(f => f.Id == "helms");
        var troops = Troops();
        BoonTarget GroupOf(string id)
        {
            var group = (string?)troops[id].Attribute("default_group");
            return BoonTargets.OfCharacter(group == "Cavalry" || group == "HorseArcher", group == "Ranged" || group == "HorseArcher");
        }

        var groups = helms.Garrison.Select(g => g.TroopId).Concat(helms.Shop.Offers.Select(o => o.TroopId)).Select(GroupOf).Distinct().OrderBy(g => g).ToList();

        CollectionAssert.AreEqual(new[] { BoonTarget.Infantry, BoonTarget.Archers, BoonTarget.Cavalry }, groups,
            "King's Knights are a Cavalry troop on the shop list, so the cavalry rows are eligible at Helm's Deep; tools/run_long_defeat.py troop_groups reads the same file");
    }

    [TestMethod]
    public void ShippedBoons_TheFirstOffersOfAnArcherAtHelms_ArePinned_ToTheSameVectorsAsThePythonReplay()
    {
        var (catalog, _) = Load();
        var plan = catalog.BuildPlan(catalog.Fortresses.Single(f => f.Id == "helms"), RunOptions.Default).Boons;
        var groups = new[] { BoonCategory.Infantry, BoonCategory.Archers, BoonCategory.Cavalry };
        var deck = new BoonDeck(plan, 1UL, 2, "archer", groups);
        for (var n = 1; n <= 3; n++)
            deck.Cleared(n, n);
        deck.TryReroll();
        deck.TryReroll();

        // Seed 1, pick 1, an archer with the garrison and shop of Helm's Deep: 24 rows are eligible (the war chest, fortress and Legend rows, the archer's class rows, and all three troop categories).
        CollectionAssert.AreEqual(new[] { "steady_aim", "stout_yeomanry", "fletchers_favour" }, deck.Picks[0].Draws[0].Ids.ToArray());
        CollectionAssert.AreEqual(new[] { "marksmans_call", "wains_of_the_westfold", "granary_stores" }, deck.Picks[0].Draws[1].Ids.ToArray());
        CollectionAssert.AreEqual(new[] { "stout_hearts_at_the_gate", "hornblowers_bargain", "fletchers_favour" }, deck.Picks[0].Draws[2].Ids.ToArray());
        Assert.AreEqual(24, deck.Picks[0].Draws[0].Eligible);
    }

    // The own-gear rule: a class kit gives the lord only what a troop of his people carries in fords_troops.xml. An item no troop carries sits here with its source, as a named exception.
    // Decision D5 (Mike's standing word, 2026-11-05): the two-handed class swings the Armory axe, because no Rohan troop carries a two-handed sword, axe or mace.
    private static readonly IReadOnlyDictionary<string, string> OwnGearExceptions = new Dictionary<string, string>
    {
        ["wm_rohan_ws_axe_2h_a01"] = "LOTRLOME_Armory ModuleData/LOTRLOME_items/LOTRAOM_weapons.xml, culture vlandia, carried by no troop (decision D5)",
    };

    private static (RunPlan Plan, IReadOnlyList<RunClass> Offer, IModLogger Logger) ShippedClasses()
    {
        var (catalog, logger) = Load();
        var helms = catalog.Fortresses.Single(f => f.Id == "helms");
        return (catalog.BuildPlan(helms, RunOptions.Default), catalog.ClassesFor(helms), logger);
    }

    [TestMethod]
    public void ShippedClasses_AreTheThreeFootClasses_InThePlansOrder_WithNoWarning()
    {
        var (_, offer, logger) = ShippedClasses();

        CollectionAssert.AreEqual(new[] { "sword_and_board", "two_handed", "archer" }, offer.Select(c => c.Id).ToArray());
        Assert.IsTrue(offer.All(c => !c.Mounted), "Cavalry waits for Slices 10 and 11");
        logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains("classes.json")));
    }

    [TestMethod]
    public void ShippedClasses_EveryKitItemIsCarriedByATroopOfThatCulture_OrIsANamedException()
    {
        var troops = Troops().Values.ToList();
        var (_, offer, _) = ShippedClasses();
        var strays = new List<string>();
        foreach (var cls in offer)
        {
            foreach (var pair in cls.Kits)
            {
                var culture = pair.Key;
                var kit = pair.Value;
                var carried = troops.Where(t => Culture(t).Equals(culture, StringComparison.OrdinalIgnoreCase))
                    .SelectMany(t => t.Descendants("equipment").Select(e => ((string)e.Attribute("id")!).Replace("Item.", "")))
                    .ToHashSet();
                foreach (var slot in kit.Slots.Where(s => s.ItemId.Length > 0))
                {
                    if (!carried.Contains(slot.ItemId) && !OwnGearExceptions.ContainsKey(slot.ItemId))
                        strays.Add($"{cls.Id}/{culture}/{slot.Slot}: {slot.ItemId}");
                }
            }
        }

        Assert.AreEqual(0, strays.Count, "a kit item no troop of that culture carries and no exception names:\n" + string.Join("\n", strays));
    }

    [TestMethod]
    public void ShippedClasses_EveryExceptionIsUsedByAKit_AndNoTroopCarriesIt()
    {
        var troops = Troops().Values.ToList();
        var (_, offer, _) = ShippedClasses();
        var used = offer.SelectMany(c => c.Kits.Values).SelectMany(k => k.Slots).Select(s => s.ItemId).ToHashSet();
        var carriedByAnyone = troops.SelectMany(t => t.Descendants("equipment").Select(e => ((string)e.Attribute("id")!).Replace("Item.", ""))).ToHashSet();

        foreach (var item in OwnGearExceptions.Keys)
        {
            Assert.IsTrue(used.Contains(item), item + " is listed as an exception but no kit uses it");
            Assert.IsFalse(carriedByAnyone.Contains(item), item + " is carried by a troop now, so it needs no exception");
        }
    }

    // The one kit item no fords troop carries has no other gate: validate_moduledata.py and audit_armory_refs.py read neither long_defeat/ nor a bare item id. An Armory sync that drops or
    // renames it leaves the Two Handed class with the lord's own Item0 and an empty hand, found only by a game run. Inconclusive where the Armory is not installed.
    [TestMethod]
    [TestCategory("LiveInstall")]
    public void TheArmory_DefinesEachNamedExceptionItem_OnceAsACraftedTwoHandedAxe()
    {
        var gameDir = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        var weapons = Path.Combine(string.IsNullOrWhiteSpace(gameDir) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : gameDir,
            "Modules", "LOTRLOME_Armory", "ModuleData", "LOTRLOME_items", "LOTRAOM_weapons.xml");
        if (!File.Exists(weapons))
            Assert.Inconclusive("LOTRLOME_Armory is not installed on this machine; the live axe cannot be checked here.");

        var doc = XDocument.Load(weapons);
        foreach (var id in OwnGearExceptions.Keys)
        {
            var items = doc.Descendants("CraftedItem").Where(i => (string?)i.Attribute("id") == id).ToList();
            Assert.AreEqual(1, items.Count, $"LOTRAOM_weapons.xml must define {id} exactly once");
            Assert.AreEqual("TwoHandedAxe", (string?)items[0].Attribute("crafting_template"), id);
        }
    }

    [TestMethod]
    public void EveryShippedFortress_OffersAtLeastOneClass_SinceEveryRunOwesAClassLine()
    {
        var (catalog, _) = Load();

        foreach (var fortress in catalog.Fortresses)
            Assert.IsTrue(catalog.ClassesFor(fortress).Count > 0,
                $"{fortress.Id} offers no class: its runs write no class line, and the wrapper's judge and 1079.toml both fail a run without one");
    }

    [TestMethod]
    public void ShippedClasses_TheTwoHandedKitSwingsTheArmoryAxe_InItemZero_WithNoShield()
    {
        var (_, offer, _) = ShippedClasses();

        var kit = offer.Single(c => c.Id == "two_handed").Kits["vlandia"].Slots.ToDictionary(s => s.Slot, s => s.ItemId);

        Assert.AreEqual("wm_rohan_ws_axe_2h_a01", kit["Item0"]);
        Assert.IsTrue(!kit.TryGetValue("Item1", out var offhand) || offhand.Length == 0, "a two-handed weapon leaves the off-hand slot empty");
    }

    [TestMethod]
    public void ShippedClasses_EveryNameAndText_IsRegisteredWithTheSameDefault()
    {
        var strings = XDocument.Load(Path.Combine(ModuleData, "taom_module_strings.xml")).Descendants("string")
            .ToDictionary(e => (string)e.Attribute("id")!, e => (string)e.Attribute("text")!);
        var (_, offer, _) = ShippedClasses();

        foreach (var cls in offer)
        {
            foreach (var raw in new[] { cls.Name, cls.Text })
            {
                var key = raw.Substring(2, raw.IndexOf('}') - 2);
                Assert.IsTrue(key.StartsWith("TAOM_LD_Class_", StringComparison.Ordinal), key);
                Assert.IsTrue(strings.TryGetValue(key, out var registered), key + " is not in taom_module_strings.xml");
                Assert.AreEqual(raw, registered, key);
            }
        }
    }

    [TestMethod]
    public void ShippedClasses_ThePromptKeys_AreRegisteredWithTheSameDefault()
    {
        var strings = XDocument.Load(Path.Combine(ModuleData, "taom_module_strings.xml")).Descendants("string")
            .ToDictionary(e => (string)e.Attribute("id")!, e => (string)e.Attribute("text")!);
        var source = File.ReadAllText(RepoPaths.RepoPath("Main", "Features", "LongDefeat", "LongDefeatSetupPrompt.cs"));

        foreach (var key in new[] { "TAOM_LD_Prompt_ClassTitle", "TAOM_LD_Prompt_ClassDesc", "TAOM_LD_Prompt_ChoiceClass" })
        {
            Assert.IsTrue(strings.TryGetValue(key, out var registered), key + " is not in taom_module_strings.xml");
            StringAssert.Contains(source, "\"" + registered + "\"", key + " differs from the prompt's constant");
        }
    }

    [TestMethod]
    public void ShippedClasses_EveryClassHasAKitForTheFortressesDefenderCulture()
    {
        var (_, offer, _) = ShippedClasses();
        var (catalog, _) = Load();
        var helms = catalog.Fortresses.Single(f => f.Id == "helms");

        Assert.AreEqual(3, offer.Count);
        Assert.IsTrue(offer.All(c => c.LoadoutFor(helms.DefenderCulture) != null));
    }
}
