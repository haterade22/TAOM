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

    private static HashSet<string> RealLordIds(string moduleData)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // TAOM-native lords (NPCCharacter id="lord_...") + XSLT-transformed vanilla lords (template @id='lord_...').
        var xml = File.ReadAllText(Path.Combine(moduleData, "characters", "lords.xml"));
        foreach (Match m in Regex.Matches(xml, "\\bid=\"(lord_[A-Za-z0-9_]+)\""))
            ids.Add(m.Groups[1].Value);

        var xslt = File.ReadAllText(Path.Combine(moduleData, "lords.xslt"));
        foreach (Match m in Regex.Matches(xslt, "NPCCharacter\\[@id='(lord_[A-Za-z0-9_]+)'\\]"))
            ids.Add(m.Groups[1].Value);

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
    public void ShippedConfig_EveryCuratedLordId_ExistsInLordData()
    {
        var md = FindModuleDataPath();
        if (md == null) { Assert.Inconclusive("ModuleData path not found — run from repo root"); return; }

        var provider = RealProvider(md);
        var realIds = RealLordIds(md);
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
            $"Curated commander ids not found as NPCCharacter defs in lords.xml/lords.xslt: {string.Join(", ", missing)}");
    }
}
