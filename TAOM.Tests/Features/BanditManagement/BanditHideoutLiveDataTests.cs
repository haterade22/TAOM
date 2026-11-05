using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Core;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.BanditManagement;

/// <summary>
/// Bandit clans against the hideouts in the live, unversioned TAOM_Map. A clan with no den of its culture throws on a
/// new game, and the map can change with the repo untouched, so only a live read catches it before the crash. This is
/// the release check for each TAOM_Map package (LOADING-HANG-08). Inconclusive on a machine without the module.
/// </summary>
[TestClass]
public class BanditHideoutLiveDataTests
{
    private static string ModulesDir()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game install not resolved (BANNERLORD_GAME_DIR): " + string.Join("; ", GameAssemblies.Diagnostics));
        return Path.Combine(GameAssemblies.GameDir, "Modules");
    }

    private static List<XElement> LiveHideouts()
    {
        string path = Path.Combine(ModulesDir(), "TAOM_Map", "ModuleData", "settlements.xml");
        if (!File.Exists(path))
            Assert.Inconclusive("TAOM_Map not installed on this machine; the live hideouts cannot be checked here.");
        return XDocument.Load(path).Root!.Elements("Settlement")
            .Where(s => (string?)s.Attribute("type") == "Hideout").ToList();
    }

    private static IEnumerable<XElement> BanditClans() =>
        XDocument.Load(Path.Combine(CultureDataFixture.ModuleDataPath(), "characters", "clans.xml"))
            .Descendants("Faction").Where(f => (string?)f.Attribute("is_bandit") == "true");

    [TestMethod]
    public void EveryHideoutCultureBanditClan_OwnsALiveHideout()
    {
        var hideoutCultures = new HashSet<string>(
            LiveHideouts().Select(h => (string?)h.Attribute("culture") ?? ""), StringComparer.OrdinalIgnoreCase);
        var clans = BanditClans()
            .Where(f => CultureDataFixture.HideoutOnlyCultures.Contains(((string?)f.Attribute("culture") ?? "").Replace("Culture.", "")))
            .ToList();
        Assert.IsTrue(clans.Count > 0, "no hideout-culture bandit clan in characters/clans.xml: the scan reads nothing");
        var orphans = clans
            .Where(f => !hideoutCultures.Contains((string?)f.Attribute("culture") ?? ""))
            .Select(f => (string)f.Attribute("id")!).ToList();
        Assert.AreEqual(0, orphans.Count,
            $"bandit clans with no live hideout of their culture crash a new game: {string.Join(", ", orphans)}");
    }
}
