using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CustomBattles.Config;

namespace TAOM.Tests.Features.CustomBattles;

/// <summary>
/// Shipped-data regression: loads the REAL custom_battle_commanders.json through the real provider and
/// cross-checks every curated faction key + lord id against the real culture set and lords.xml/lords.xslt.
/// Catches a typo'd / renamed / removed id in the shipped config that the synthetic provider tests can't
/// (Codex review 2026-06-27 "things the implementer may have missed").
/// </summary>
[TestClass]
public class CustomBattleCommandersShippedDataTests
{
    // The 8 factions the shipped config curates. Pins coverage so a future edit can't silently drop one.
    private static readonly string[] ExpectedFactions =
        { "mordor", "gondor", "vlandia", "mirkwood", "rivendell", "lothlorien", "isengard", "erebor" };

    // Mirrors ConfigIdValidationTests.ValidCultureIds (custom + XSLT/vanilla culture StringIds).
    private static readonly HashSet<string> KnownCultureIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "gondor", "mordor", "erebor", "rivendell", "lothlorien", "mirkwood", "isengard",
        "gundabad", "dolguldur", "umbar", "goblin", "mistymountainorcs",
        "vlandia", "empire", "aserai", "khuzait", "sturgia", "battania"
    };

    private static string FindModuleDataPath()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "Main", "_Module", "ModuleData");
            if (Directory.Exists(candidate))
                return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }

        return null;
    }

    // Lords that exist in a Custom Battle (CustomGame). A lords.xslt template alone does not count: it rebuilds
    // a vanilla SandBox lord, and SandBox registers lords.xml for Campaign only, so in Custom Battle the template
    // has nothing to match unless characters/custom_battle_lords.xml supplies a stub for it.
    private static HashSet<string> CustomGameLordIds(string moduleData)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in new[] { "lords.xml", "custom_battle_lords.xml" })
        {
            var xml = File.ReadAllText(Path.Combine(moduleData, "characters", file));
            foreach (Match m in Regex.Matches(xml, "<NPCCharacter\\b[^>]*\\bid=\"(lord_[A-Za-z0-9_]+)\""))
                ids.Add(m.Groups[1].Value);
        }

        return ids;
    }

    private static CustomBattleCommandersProvider RealProvider(string moduleData)
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(moduleData);
        return new CustomBattleCommandersProvider(pathService, Substitute.For<IModLogger>());
    }

    [TestMethod]
    public void ShippedConfig_EveryFactionKey_IsKnownCultureAndCurated()
    {
        var md = FindModuleDataPath();
        if (md == null) { Assert.Inconclusive("ModuleData path not found — run from repo root"); return; }

        var provider = RealProvider(md);
        foreach (var faction in ExpectedFactions)
        {
            Assert.IsTrue(provider.HasCuratedEntry(faction), $"Shipped config is missing curated faction '{faction}'.");
            Assert.IsTrue(KnownCultureIds.Contains(faction),
                $"Faction key '{faction}' is not a known culture id (typo? use 'vlandia' for Rohan, 'dolguldur' not 'dol_guldur').");
        }
    }

    [TestMethod]
    public void ShippedConfig_EveryCuratedLordId_ExistsInCustomBattle()
    {
        var md = FindModuleDataPath();
        if (md == null) { Assert.Inconclusive("ModuleData path not found — run from repo root"); return; }

        var provider = RealProvider(md);
        var realIds = CustomGameLordIds(md);
        var missing = new List<string>();

        foreach (var faction in ExpectedFactions)
        {
            var ids = provider.GetCuratedCommanderIds(faction);
            Assert.IsTrue(ids.Count > 0, $"Shipped config has no curated commanders for faction '{faction}'.");
            foreach (var id in ids)
                if (!realIds.Contains(id))
                    missing.Add($"{faction}:{id}");
        }

        Assert.AreEqual(0, missing.Count,
            "Curated commander ids that do not exist in a Custom Battle (not in characters/lords.xml or " +
            $"characters/custom_battle_lords.xml): {string.Join(", ", missing)}");
    }
}
