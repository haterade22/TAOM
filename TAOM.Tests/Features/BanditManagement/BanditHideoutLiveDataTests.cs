using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Core;

namespace TAOM.Tests.Features.BanditManagement;

/// <summary>
/// Bandit clans against the hideouts in the live, unversioned TAOM_Map. A clan whose culture owns no hideout throws in
/// GetInfestedHideoutCount on new game, and a reinstall of TAOM_Map that drops the Feral Orc dens leaves the repo
/// untouched, so only a live read catches it before the crash. Inconclusive on a machine without the module.
/// </summary>
[TestClass]
[TestCategory("LiveInstall")]
public class BanditHideoutLiveDataTests
{
    private static readonly string[] Languages = { "BR", "CNs", "CNt", "DE", "FR", "IT", "JP", "KO", "PL", "RU", "SP", "TR" };

    private static string ModulesDir()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        string game = string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env;
        return Path.Combine(game, "Modules");
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
        var orphans = BanditClans()
            .Where(f => CultureDataFixture.HideoutOnlyCultures.Contains(((string?)f.Attribute("culture") ?? "").Replace("Culture.", "")))
            .Where(f => !hideoutCultures.Contains((string?)f.Attribute("culture") ?? ""))
            .Select(f => (string)f.Attribute("id")!).ToList();
        Assert.AreEqual(0, orphans.Count,
            $"bandit clans with no live hideout of their culture crash a new game: {string.Join(", ", orphans)}");
    }

    [TestMethod]
    public void FeralOrcsClan_IsHomedOnALiveFeralDen()
    {
        var clan = BanditClans().SingleOrDefault(f => (string?)f.Attribute("id") == "feral_orcs");
        Assert.IsNotNull(clan, "characters/clans.xml has no feral_orcs bandit clan");
        string home = ((string?)clan!.Attribute("initial_home_settlement") ?? "").Replace("Settlement.", "");
        var den = LiveHideouts().SingleOrDefault(h => (string?)h.Attribute("id") == home);
        Assert.IsNotNull(den, $"feral_orcs is homed on {home}, which is not a live hideout");
        Assert.AreEqual("Culture.feral_orcs", (string?)den!.Attribute("culture"));
    }

    [TestMethod]
    public void FeralDens_LoadAStockSceneAndHaveANameInEveryLanguage()
    {
        var dens = LiveHideouts().Where(h => ((string)h.Attribute("id")!).StartsWith("hideout_feral_", StringComparison.Ordinal)).ToList();
        Assert.IsTrue(dens.Count > 0, "the live map has no hideout_feral_ dens");

        var missingScenes = dens
            .SelectMany(h => h.Descendants("Location").Select(l => (string?)l.Attribute("scene_name") ?? ""))
            .Distinct()
            .Where(s => !Directory.EnumerateDirectories(ModulesDir())
                .Any(m => File.Exists(Path.Combine(m, "SceneObj", s, "scene.xscene"))))
            .ToList();
        Assert.AreEqual(0, missingScenes.Count, $"no module has these den scenes, so raiding crashes: {string.Join(", ", missingScenes)}");

        foreach (var lang in Languages)
        {
            string loc = Path.Combine(ModulesDir(), "TAOM_Map", "ModuleData", "Languages", lang, "loc_settlements.xml");
            var ids = XDocument.Load(loc).Descendants("string").Select(s => (string?)s.Attribute("id")).ToHashSet();
            var unnamed = dens.Select(h => (string)h.Attribute("id")!)
                .Where(id => !ids.Contains("Settlements.Settlement.name." + id)).ToList();
            Assert.AreEqual(0, unnamed.Count, $"{lang} loc_settlements.xml lacks names for {string.Join(", ", unnamed)}");
        }
    }
}
