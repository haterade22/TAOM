using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.ArmourAcquisition;
using TAOM.Features.ArmourAcquisition.Domain;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.ArmourAcquisition;

/// <summary>
/// The shipped data the feature reads, through the real providers: the ladder's rung quests the armoury
/// starts by id, a config that loads without a single reversion (Mike's decisions pinned: lord kit
/// costs the best metal plus the special resource; all seventeen hero items are named), the culture map
/// the markets and the lord kit share, and a generated class table whose upgrade links always climb, with
/// lord kit reachable only from elite.
/// </summary>
[TestClass]
public class ArmourAcquisitionShippedDataTests
{
    private IPathService _paths = null!;
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _paths = Substitute.For<IPathService>();
        _paths.ModuleDataPath.Returns(RepoPaths.RepoPath("Main", "_Module", "ModuleData"));
        _logger = Substitute.For<IModLogger>();
    }

    [TestMethod]
    public void CareerQuests_ShipEveryLadderRungsQuest_WhichCannotCompleteInsideItsOwnStart()
    {
        var quests = new CareerQuestConfigProvider(_paths, _logger).LoadQuests();
        var steps = new ArmourAcquisitionConfigProvider(_paths, _logger).GetConfig().Ladder.Steps;

        Assert.AreEqual(6, steps.Count, "one rung per slot: hands, legs, shoulders, head, body, weapon");
        foreach (var step in steps)
        {
            var quest = quests.SingleOrDefault(q => q.Id == step.QuestId);
            Assert.IsNotNull(quest, $"{step.QuestId} is missing from taom_career_quests.xml; the {step.Slot} rung cannot start");
            Assert.AreEqual(step.QuestId, quest!.CareerId, "its career_id must name no career, so the career offer loop never offers it");
            Assert.AreEqual(0, quest.Rewards.Count, "the rung's piece is claimed at an armoury; the quest itself pays nothing");
            // CareerQuest.OnStartQuest seeds threshold objectives and completes a quest they already satisfy, inside
            // QuestBase.StartQuest (a finalized quest then sits in QuestManager). The hero's kills start at zero.
            Assert.IsTrue(quest.Objectives.Any(o => o.Type == CareerQuestObjectiveType.HeroKills), $"{step.QuestId} counts the hero's kills");
        }
        // Only the ladder's kill counter feeds HeroKills, and only to the current rung's quest: a career quest that
        // listed it would never progress.
        var rungQuests = steps.Select(s => s.QuestId).ToList();
        foreach (var quest in quests.Where(q => q.Objectives.Any(o => o.Type == CareerQuestObjectiveType.HeroKills)))
            Assert.IsTrue(rungQuests.Contains(quest.Id), $"{quest.Id} lists HeroKills but is no ladder rung");
    }

    [TestMethod]
    public void Config_EveryCultureWithArmourHasALordsMaterialAndWeaponPicks()
    {
        var ladder = new ArmourAcquisitionConfigProvider(_paths, _logger).GetConfig().Ladder;
        var cultures = new[] { "gondor", "vlandia", "erebor", "sturgia", "rivendell", "mirkwood", "mordor", "isengard",
            "dolguldur", "gundabad", "khuzait", "aserai", "empire" };

        CollectionAssert.AreEquivalent(cultures, ladder.Materials.Keys.ToArray(), "one lord's material per culture that owns armour");
        // Lórien owns no armour but has a named weapon of its own, Galadriel's sword (Mike, 2026-09-28).
        CollectionAssert.AreEquivalent(cultures.Append("lothlorien").ToArray(), ladder.Weapons.Keys.ToArray(),
            "every culture's weapon rung has a pick");
        CollectionAssert.AreEquivalent(ArmourAcquisitionConfig.Default.Ladder.Materials.ToArray(), ladder.Materials.ToArray(),
            "the compiled default mirrors the shipped file");
    }

    [TestMethod]
    public void Config_TheCompiledLadderMirrorsTheShippedFile()
    {
        var shipped = new ArmourAcquisitionConfigProvider(_paths, _logger).GetConfig().Ladder;
        var d = ArmourAcquisitionConfig.Default.Ladder;

        CollectionAssert.AreEqual(d.Steps.Select(s => $"{s.Slot}:{s.QuestId}:{s.Materials}").ToArray(),
            shipped.Steps.Select(s => $"{s.Slot}:{s.QuestId}:{s.Materials}").ToArray(), "the rungs");
        Assert.AreEqual(d.CountsKnockouts, shipped.CountsKnockouts, "count_knockouts");
        Assert.AreEqual((d.Drop.BaseChance, d.Drop.ChancePerTenKills, d.Drop.MaxChance, d.Drop.MinUnits, d.Drop.MaxUnits),
            (shipped.Drop.BaseChance, shipped.Drop.ChancePerTenKills, shipped.Drop.MaxChance, shipped.Drop.MinUnits, shipped.Drop.MaxUnits),
            "the drop curve");
        CollectionAssert.AreEquivalent(d.Weapons.Keys.ToArray(), shipped.Weapons.Keys.ToArray(), "the weapon rung's cultures");
        foreach (var culture in d.Weapons.Keys)
            CollectionAssert.AreEqual(d.Weapons[culture].ToArray(), shipped.Weapons[culture].ToArray(), $"{culture}'s weapon picks");
    }

    [TestMethod]
    public void Ladder_ArthedainsHeadRungOffersTheKingsCrown_AndNoOtherCultureDoes()
    {
        // Mike, 2026-10-08: the king's crown is never sold; it is earned through the head rung.
        var ladder = new ArmourAcquisitionConfigProvider(_paths, _logger).GetConfig().Ladder;

        CollectionAssert.AreEqual(new[] { "sk_ar_art_crown_king_a" },
            ladder.Pieces[LordsLadderConfig.PieceKey("arthedain", LadderSlot.Head)].ToArray());
        Assert.AreEqual(1, ladder.Pieces.Count, "no other culture or slot has a configured piece");
    }

    [TestMethod]
    public void LordsMaterials_TheItemsFileKeepsThemOutOfEveryEconomy()
    {
        // RCA 2026-09-28 rows 16 and 20: what keeps a material out of workshops, caravans, loot and the hideout pool.
        var doc = XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "armour_acquisition", "taom_lords_materials.xml"));
        var items = doc.Root!.Elements("Item").ToList();
        var materials = new ArmourAcquisitionConfigProvider(_paths, _logger).GetConfig().Ladder.Materials;

        CollectionAssert.AreEquivalent(materials.Values.ToArray(), items.Select(i => (string)i.Attribute("id")!).ToArray(),
            "one item per configured lord's material, and no other");
        foreach (var item in items)
        {
            var id = (string)item.Attribute("id")!;
            Assert.AreEqual("Goods", (string?)item.Attribute("Type"), id);
            Assert.AreEqual("unassigned", (string?)item.Attribute("item_category"), $"{id}: no workshop, caravan or town demand");
            Assert.AreEqual("false", (string?)item.Attribute("is_merchandise"), $"{id}: no battle loot or plunder");
            Assert.IsNull(item.Attribute("culture"), $"{id}: CultureMarketplace pools only items with a culture");
            // The hideout night pool takes Goods up to a theoretical value of 4750, ten times the value.
            Assert.IsTrue(int.Parse((string)item.Attribute("value")!, CultureInfo.InvariantCulture) >= 475, $"{id}: in the hideout pool");
            Assert.IsNull(item.Element("ItemComponent"), $"{id}: vanilla XML non-food goods carry no component");
            Assert.AreEqual("true", (string?)item.Element("Flags")?.Attribute("Civilian"), $"{id}: vanilla goods are civilian");
        }
    }

    [TestMethod]
    public void DeepSeam_EveryRowPaysTheMaterialOfEveryCultureItServes_OnlyToAPlayerOfThem()
    {
        // RCA 2026-09-28 row 4 and the XML lens: the rows copy the culture-to-material map a third time.
        var ladder = new ArmourAcquisitionConfigProvider(_paths, _logger).GetConfig().Ladder;
        var marketplace = new TAOM.Features.CultureMarketplace.CultureMarketplaceConfigProvider(_paths, _logger);
        var rows = XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "lotr_issues", "taom_lotr_issues.xml"))
            .Root!.Elements("LotrIssue").Where(r => ((string)r.Attribute("id")!).StartsWith("lotr_deep_seam_", StringComparison.Ordinal)).ToList();

        Assert.AreEqual(ladder.Materials.Count, rows.Count, "one Deep Seam row per lord's material");
        var served = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var id = (string)row.Attribute("id")!;
            Assert.AreEqual("true", (string?)row.Attribute("for_player_culture"), $"{id}: offered only to a player who can spend it");
            foreach (var culture in ((string)row.Attribute("cultures")!).Split(','))
            {
                var c = culture.Trim();
                Assert.IsTrue(served.Add(c), $"{c} is served by two Deep Seam rows");
                var own = ladder.Materials.TryGetValue(c, out var m) ? m : null;
                var donor = marketplace.GetArmourDonor(c);
                var expected = own ?? (donor != null && ladder.Materials.TryGetValue(donor, out var dm) ? dm : null);
                Assert.AreEqual(expected, (string?)row.Attribute("reward_item"), $"{id} pays {c} the wrong material");
            }
        }
    }

    [TestMethod]
    public void Config_EveryCultureThatDrawsOnADonor_HasTheDonorsMaterialAndWeapons()
    {
        var ladder = new ArmourAcquisitionConfigProvider(_paths, _logger).GetConfig().Ladder;
        var marketplace = new TAOM.Features.CultureMarketplace.CultureMarketplaceConfigProvider(_paths, _logger);

        foreach (var culture in new[] { "lindon", "lothlorien", "abanissa", "shaghana", "battania", "goblin", "mistymountainorcs", "bluecraig", "umbar", "arthedain" })
        {
            var donor = marketplace.GetArmourDonor(culture);
            Assert.IsNotNull(donor, culture);
            Assert.IsTrue(ladder.Materials.ContainsKey(donor!), $"{culture}'s donor {donor} has no lord's material");
            Assert.IsTrue(ladder.Weapons.ContainsKey(donor!), $"{culture}'s donor {donor} has no weapon picks");
        }
    }

    [TestMethod]
    public void Config_NamesEveryHeroWeaponAndShield()
    {
        var named = new ArmourAcquisitionConfigProvider(_paths, _logger).GetConfig().NamedWeapons;

        // Mike: the seventeen (2026-09-27), then Tuor's two heirloom axes, Galadriel's sword and the seven Noldor
        // swords (2026-09-28). Never sold, never looted.
        Assert.AreEqual(27, named.Count);
        foreach (var id in new[]
                 {
                     "anduril", "glamdring_sword", "witchking_sword", "wm_boromir_shield", "wm_theoden_shield",
                     "wm_tuors_axe_1h", "wm_tuors_axe", "wm_galadriel_sword", "wm_fingon_sword", "wm_finarin_sword",
                     "wm_finwe_sword", "wm_ingwe_sword", "wm_turin_sword", "wm_voronwe_sword", "wm_celegorm_sword",
                 })
            Assert.IsTrue(named.Contains(id), id);
        CollectionAssert.AreEquivalent(ArmourAcquisitionConfig.Default.NamedWeapons.ToList(), named.ToList(),
            "the compiled default must match the shipped list");
    }

    [TestMethod]
    public void Ladder_EveryNamedWeaponIsAWeaponRungChoice()
    {
        // Mike, 2026-09-28: the weapon rung is the named weapons' route. Only the two shields have none yet, and
        // Tuor's axes, which carry no culture, sit on Rivendell's rung.
        var config = new ArmourAcquisitionConfigProvider(_paths, _logger).GetConfig();
        var offered = config.Ladder.Weapons.Values.SelectMany(w => w).ToHashSet();

        CollectionAssert.AreEquivalent(new[] { "wm_boromir_shield", "wm_theoden_shield" },
            config.NamedWeapons.Where(id => !offered.Contains(id)).ToArray(), "named weapons no rung offers");
        CollectionAssert.IsSubsetOf(new[] { "wm_tuors_axe_1h", "wm_tuors_axe" }, config.Ladder.Weapons["rivendell"].ToList());
    }

    [TestMethod]
    public void Marketplace_CulturesWithoutArmourDrawOnTheCommissionMapping()
    {
        // Mike, 2026-09-27: the Armourer's Commission mapping fills the lord kit, and the markets of a culture
        // without <Stock> rows (Lindon and Arthedain stock theirs through Stock rows, #755).
        var marketplace = new TAOM.Features.CultureMarketplace.CultureMarketplaceConfigProvider(_paths, _logger);
        var expected = new (string Culture, string Donor)[]
        {
            ("lindon", "rivendell"), ("lothlorien", "rivendell"), ("abanissa", "aserai"), ("shaghana", "aserai"),
            ("battania", "khuzait"), ("goblin", "mordor"), ("mistymountainorcs", "mordor"), ("bluecraig", "mordor"),
            ("umbar", "mordor"), ("arthedain", "gondor"),
        };

        foreach (var (culture, donor) in expected)
            Assert.AreEqual(donor, marketplace.GetArmourDonor(culture), culture);
    }

    [TestMethod]
    public void Config_LoadsWithoutAnyReversion()
    {
        var config = new ArmourAcquisitionConfigProvider(_paths, _logger).GetConfig();

        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        Assert.AreNotSame(ArmourAcquisitionConfig.Default, config, "the shipped file must be read, not the compiled default");
        foreach (var target in new[] { ArmourClass.Medium, ArmourClass.Heavy, ArmourClass.Elite, ArmourClass.Lord })
            Assert.IsTrue(config.Recipes.ContainsKey(target), $"no {target} recipe");
    }

    [TestMethod]
    public void Config_LordKitCostsTheBestMetalAndTheSpecialResource()
    {
        var lord = new ArmourAcquisitionConfigProvider(_paths, _logger).GetConfig().Recipes[ArmourClass.Lord];

        Assert.IsTrue(lord.SpecialResource > 0f, "Mike, 2026-09-27: lord kit costs the kingdom special resource");
        Assert.IsTrue(lord.Materials.Any(m => m.ItemId == "ironIngot6"), "Mike, 2026-09-27: and the best metal (thamaskene steel)");
    }

    [TestMethod]
    public void Config_GateLevelsAreMikesBarracksLadder()
    {
        // Mike, 2026-09-27: heavy 1, elite and heavy 2, lord and all 3.
        var config = new ArmourAcquisitionConfigProvider(_paths, _logger).GetConfig();

        Assert.AreEqual((1, 2, 3), (config.HeavyLevel, config.EliteLevel, config.LordLevel));
    }

    [TestMethod]
    public void ClassTable_LoadsCleanly()
    {
        var entries = new ArmourClassTableProvider(_paths, _logger).GetEntries();

        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        Assert.IsTrue(entries.Count > 2000, $"only {entries.Count} rows: regenerate with python tools/generate_armour_classes.py --apply");
    }

    private static System.Collections.Generic.List<string> CharacterCreationCultureIds() =>
        Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText(
                RepoPaths.RepoPath("Main", "_Module", "ModuleData", "charactercreation", "cultures.json")))
            .Select(c => (string?)c["culture_id"])
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .ToList();

    private static System.Collections.Generic.HashSet<string> CulturesServedBy(string idPrefix) =>
        new(XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "lotr_issues", "taom_lotr_issues.xml"))
            .Root!.Elements("LotrIssue")
            .Where(r => ((string?)r.Attribute("id") ?? string.Empty).StartsWith(idPrefix, StringComparison.Ordinal))
            .SelectMany(r => ((string?)r.Attribute("cultures") ?? string.Empty).Split(','))
            .Select(c => c.Trim()), StringComparer.Ordinal);

    [TestMethod]
    public void Commissions_EveryCharacterCreationCultureHasARow()
    {
        // 2026-10-07 review: Arthedain shipped as the one playable culture no artisan would ever commission.
        var served = CulturesServedBy("lotr_armourer_commission_");

        var missing = CharacterCreationCultureIds().Where(c => !served.Contains(c)).ToList();

        Assert.AreEqual(0, missing.Count, $"no Armourer's Commission row for: {string.Join(", ", missing)}");
    }

    [TestMethod]
    public void DeepSeam_EveryCharacterCreationCultureWithALordsMaterialIsServed()
    {
        // A culture whose ladder spends a material (its own, or its armour donor's) gets that material's row.
        var ladder = new ArmourAcquisitionConfigProvider(_paths, _logger).GetConfig().Ladder;
        var marketplace = new TAOM.Features.CultureMarketplace.CultureMarketplaceConfigProvider(_paths, _logger);
        var served = CulturesServedBy("lotr_deep_seam_");

        var missing = CharacterCreationCultureIds()
            .Where(c => ladder.Materials.ContainsKey(c)
                        || (marketplace.GetArmourDonor(c) is { } donor && ladder.Materials.ContainsKey(donor)))
            .Where(c => !served.Contains(c))
            .ToList();

        Assert.AreEqual(0, missing.Count, $"no Deep Seam row serves: {string.Join(", ", missing)}");
    }

    [TestMethod]
    public void Commissions_RewardOnlyHeavyOrEliteKit()
    {
        var table = new ArmourClassTableProvider(_paths, _logger).GetEntries();
        var commissions = System.Xml.Linq.XDocument
            .Load(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "lotr_issues", "taom_lotr_issues.xml"))
            .Root!.Elements("LotrIssue")
            .Where(e => ((string?)e.Attribute("id") ?? string.Empty).StartsWith("lotr_armourer_commission_", StringComparison.Ordinal))
            .ToList();

        Assert.AreEqual(19, commissions.Count);
        foreach (var issue in commissions)
        {
            var id = (string?)issue.Attribute("id");
            var reward = (string?)issue.Attribute("reward_item") ?? string.Empty;
            Assert.IsTrue(table.TryGetValue(reward, out var entry), $"{id}: reward '{reward}' is not in the class table");
            Assert.IsTrue(entry!.Class == ArmourClass.Heavy || entry.Class == ArmourClass.Elite,
                $"{id}: reward '{reward}' is {entry.Class}; a commission hands out heavy or elite kit, never lord or named");
        }
    }

    [TestMethod]
    public void ClassTable_EveryUpgradeLinkClimbs_AndLordKitComesOnlyFromElite()
    {
        var entries = new ArmourClassTableProvider(_paths, _logger).GetEntries();

        var broken = entries.Values
            .Where(e => e.NextItemId != null)
            .Where(e => !entries.TryGetValue(e.NextItemId!, out var next)
                        || ArmourClassRules.Rank(next.Class) <= ArmourClassRules.Rank(e.Class)
                        || (next.Class == ArmourClass.Lord && e.Class != ArmourClass.Elite)
                        || !ArmourClassRules.IsUpgradeSource(e.Class))
            .Select(e => $"{e.ItemId} ({e.Class}) -> {e.NextItemId}")
            .ToList();

        Assert.AreEqual(0, broken.Count, "bad upgrade links:\n" + string.Join("\n", broken.Take(20)));
    }
}
