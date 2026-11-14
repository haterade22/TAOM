using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CustomBattles;
using TAOM.Features.CustomBattles.Config;
using TAOM.Features.CustomBattles.Hooks;

namespace TAOM.Tests.Features.CustomBattles;

/// <summary>
/// Shipped-data regression: loads the REAL custom_battle_commanders.json through the real provider and
/// cross-checks every curated faction key against the real culture set, and every lord id against the lords a
/// Custom Battle actually loads (characters/lords.xml plus the custom_battle_lords.xml stubs).
/// Catches a typo'd / renamed / removed id in the shipped config that the synthetic provider tests can't
/// (Codex review 2026-06-27 "things the implementer may have missed").
/// </summary>
[TestClass]
public class CustomBattleCommandersShippedDataTests
{
    // The 9 factions the shipped config curates. Pins coverage so a future edit can't silently drop one.
    private static readonly string[] ExpectedFactions =
        { "mordor", "gondor", "vlandia", "mirkwood", "rivendell", "lothlorien", "isengard", "erebor", "sturgia" };

    // The playable cultures, read from the data (custom is_main_culture ids plus the six XSLT ones).
    private static readonly HashSet<string> KnownCultureIds = TAOM.Tests.Core.CultureDataFixture.MainCultureIds();

    [TestMethod]
    public void ProviderWarningSet_IsThePlayableCultureSet()
    {
        // The provider warns on a faction key outside its own hand-kept set; a culture missing from it
        // draws a spurious "is not a known culture id" warning (2026-10-07 review: four copies, three contents).
        CollectionAssert.AreEquivalent(KnownCultureIds.ToList(), CustomBattleCommandersProvider.KnownCultureIds.ToList());
    }

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
        // Parsed, not regexed: a commented-out stub must not count as present.
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in new[] { "lords.xml", "custom_battle_lords.xml" })
            ids.UnionWith(XDocument.Load(Path.Combine(moduleData, "characters", file))
                .Descendants("NPCCharacter")
                .Select(c => (string)c.Attribute("id"))
                .Where(id => !string.IsNullOrEmpty(id)));

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

    [TestMethod]
    public void ShippedData_EveryOfferedCulture_ResolvesAnOwnCultureCommander()
    {
        // With no commander of its own a culture's dropdown is left unfiltered (CustomBattleSideVM_OnCultureSelection_Patch)
        // and the muster fields a Calradian commander. Dunland and Khand lost all of theirs once in #648.
        var md = FindModuleDataPath();
        if (md == null) { Assert.Inconclusive("ModuleData path not found: run from the repo root"); return; }

        var characters = CustomGameCharacters(md);
        var objectManager = Substitute.For<IObjectManagerAdapter>();
        objectManager.GetAllCultureInfos().Returns(OfferableCultures(md));
        objectManager.GetAllCharacterInfos().Returns(characters.Values.ToList());
        var service = new CustomBattleService(objectManager, Substitute.For<IModLogger>(), RealProvider(md));

        var offered = service.GetFactionIds();
        CollectionAssert.AreEquivalent(KnownCultureIds.ToList(), offered.ToList(),
            "The cultures Custom Battle offers are no longer the playable set; check OfferableCultures against the data.");

        var without = new List<string>();
        foreach (var culture in offered)
        {
            var ids = service.GetCommanderIdsForFaction(culture, SideCommanderFilter.MaxCommandersPerCulture);
            if (!ids.Any(id => characters.TryGetValue(id, out var c)
                               && string.Equals(c.CultureId, culture, StringComparison.OrdinalIgnoreCase)))
                without.Add($"{culture} [{string.Join(", ", ids)}]");
        }

        Assert.AreEqual(0, without.Count,
            "Cultures Custom Battle offers that resolve no commander of their own culture (add a curated entry, a " +
            "characters/lords.xml lord or a custom_battle_lords.xml stub): " + string.Join("; ", without));
    }

    // Every culture the faction list can show, with the fields CustomBattleService.GetFactionIds filters on. The six
    // vanilla ids come from the install's SandBoxCore spcultures.xml (can_have_settlement true, a faction_banner_key,
    // no is_bandit); spcultures.xslt renames them and keeps those three attributes.
    private static List<CultureInfo> OfferableCultures(string moduleData)
    {
        var cultures = new[] { "vlandia", "empire", "aserai", "khuzait", "sturgia", "battania" }
            .Select(id => new CultureInfo { Id = id, CanHaveSettlement = true, HasFactionBanner = true })
            .ToList();
        foreach (var c in XDocument.Load(Path.Combine(moduleData, "taom_spcultures.xml")).Descendants("Culture"))
            cultures.Add(new CultureInfo
            {
                Id = (string)c.Attribute("id"),
                CanHaveSettlement = (string)c.Attribute("can_have_settlement") == "true",
                IsBandit = (string)c.Attribute("is_bandit") == "true",
                HasFactionBanner = !string.IsNullOrEmpty((string)c.Attribute("faction_banner_key")),
            });
        return cultures;
    }

    // The TAOM characters a Custom Battle loads: every NPCCharacters node SubModule.xml registers for CustomGame, in
    // order, merged by id (a later file's attribute wins). The stub file goes through lords.xslt first, as the engine
    // applies the lords node's stylesheet to it (CustomBattleLordStubsTests). Vanilla's own characters are left out:
    // none has a lord_X_Y id.
    private static Dictionary<string, CharacterInfo> CustomGameCharacters(string moduleData)
    {
        var result = new Dictionary<string, CharacterInfo>(StringComparer.OrdinalIgnoreCase);
        var nodes = XDocument.Load(Path.Combine(moduleData, "..", "SubModule.xml")).Descendants("XmlNode")
            .Where(n => (string)n.Element("XmlName")?.Attribute("id") == "NPCCharacters"
                        && n.Descendants("GameType").Any(g => (string)g.Attribute("value") == "CustomGame"));
        foreach (var node in nodes)
        {
            var rel = (string)node.Element("XmlName").Attribute("path");
            var file = Path.Combine(moduleData, rel + ".xml");
            if (!File.Exists(file)) continue;
            var doc = XDocument.Load(file);
            if (rel == "characters/custom_battle_lords")
            {
                var transform = TAOM.Tests.Core.CompiledXslt.Get(
                    Path.Combine(moduleData, "lords.xslt"), TAOM.Tests.Core.XsltLoadStyle.FrameworkDefaults);
                var output = new XDocument();
                using (var writer = output.CreateWriter())
                    transform.Transform(doc.CreateReader(), null, writer);
                doc = output;
            }
            foreach (var c in doc.Root.Elements("NPCCharacter"))
            {
                var id = (string)c.Attribute("id");
                if (string.IsNullOrEmpty(id)) continue;
                if (!result.TryGetValue(id, out var info))
                    result[id] = info = new CharacterInfo { Id = id };
                if (c.Attribute("is_hero") != null) info.IsHero = (string)c.Attribute("is_hero") == "true";
                if (c.Attribute("culture") != null) info.CultureId = TAOM.Tests.Core.CultureDataFixture.StripPrefix((string)c.Attribute("culture"));
            }
        }
        return result;
    }
}
