using System;
using System.Linq;
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
/// The shipped data the feature reads, through the real providers: the Lord's Harness quest the quest
/// behavior starts by id, a config that loads without a single reversion (Mike's decisions pinned: lord kit
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
    public void CareerQuests_ShipTheHarnessQuest_WhichCannotCompleteInsideItsOwnStart()
    {
        var quest = new CareerQuestConfigProvider(_paths, _logger).LoadQuests().SingleOrDefault(q => q.Id == LordHarnessService.QuestId);

        Assert.IsNotNull(quest, $"{LordHarnessService.QuestId} is missing from taom_career_quests.xml; the quest cannot start");
        Assert.AreEqual(LordHarnessService.QuestId, quest!.CareerId, "its career_id must name no career, so the career offer loop never offers it");
        // CareerQuest.OnStartQuest seeds threshold objectives and completes a quest they already satisfy, inside
        // QuestBase.StartQuest (a finalized quest then sits in QuestManager). A counted deed starts at zero.
        var thresholds = new[] { CareerQuestObjectiveType.SkillThreshold, CareerQuestObjectiveType.RenownThreshold, CareerQuestObjectiveType.GoldAccumulated };
        Assert.IsTrue(quest.Objectives.Any(o => !thresholds.Contains(o.Type)), "the harness quest needs at least one counted deed");
    }

    [TestMethod]
    public void Config_NamesAllSeventeenHeroItems()
    {
        var named = new ArmourAcquisitionConfigProvider(_paths, _logger).GetConfig().NamedWeapons;

        Assert.AreEqual(17, named.Count, "Mike, 2026-09-27: all seventeen hero weapons and shields are never sold, looted or awarded");
        foreach (var id in new[] { "anduril", "glamdring_sword", "witchking_sword", "wm_boromir_shield", "wm_theoden_shield" })
            Assert.IsTrue(named.Contains(id), id);
        CollectionAssert.AreEquivalent(ArmourAcquisitionConfig.Default.NamedWeapons.ToList(), named.ToList(),
            "the compiled default must match the shipped list");
    }

    [TestMethod]
    public void Marketplace_CulturesWithoutArmourDrawOnTheCommissionMapping()
    {
        // Mike, 2026-09-27: the Armourer's Commission mapping fills both the markets and the lord kit.
        var marketplace = new TAOM.Features.CultureMarketplace.CultureMarketplaceConfigProvider(_paths, _logger);
        var expected = new (string Culture, string Donor)[]
        {
            ("lindon", "rivendell"), ("lothlorien", "rivendell"), ("abanissa", "aserai"), ("shaghana", "aserai"),
            ("battania", "khuzait"), ("goblin", "mordor"), ("mistymountainorcs", "mordor"), ("bluecraig", "mordor"),
            ("umbar", "mordor"),
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

    [TestMethod]
    public void Commissions_RewardOnlyHeavyOrEliteKit()
    {
        var table = new ArmourClassTableProvider(_paths, _logger).GetEntries();
        var commissions = System.Xml.Linq.XDocument
            .Load(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "lotr_issues", "taom_lotr_issues.xml"))
            .Root!.Elements("LotrIssue")
            .Where(e => ((string?)e.Attribute("id") ?? string.Empty).StartsWith("lotr_armourer_commission_", StringComparison.Ordinal))
            .ToList();

        Assert.AreEqual(18, commissions.Count);
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
