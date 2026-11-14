using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Execution;
using TAOM.Features.FireAndTribute;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.FireAndTribute;

/// <summary>
/// #870: the shipped host data against the shipped ModuleData (like HuntTroopListsDataTests). An unknown troop id would
/// make the top-up silently add nobody, and an unknown clan would skip the host at run time with one log line, so both
/// fail here instead. The anchor settlements live in the unversioned TAOM_Map install: they are checked against it when
/// it is present (TAOM_Map is in no repo), under the LiveInstall category.
/// </summary>
[TestClass]
public class FireAndTributeDataTests
{
    private static string GameDir()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        return string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env;
    }

    private static string LiveMapPath() => Path.Combine(GameDir(), "Modules", "TAOM_Map", "ModuleData", "settlements.xml");

    private static readonly Dictionary<string, string> AgreedNames = new(StringComparer.Ordinal)
    {
        ["gundabad"] = "The Host of Gundabad",
        ["rhun"] = "The Easterling Host of Rh\u00fbn",
        ["harad"] = "The Warhost of Harad",
        ["dunland"] = "The Dunlending Host",
        ["umbar"] = "The Corsair Host of Umbar",
        ["gondor"] = "The Free Company of An\u00f3rien",
        ["erebor"] = "The Blacklock Warband",
        ["mirkwood"] = "The Stalker Band of Mirkwood",
    };

    private static readonly Lazy<Dictionary<string, string>> ModuleStrings = new(LoadModuleStrings);

    private static Dictionary<string, string> LoadModuleStrings()
    {
        var doc = XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "taom_module_strings.xml"));
        return doc.Descendants("string")
            .Where(e => e.Attribute("id") != null)
            .GroupBy(e => (string)e.Attribute("id")!)
            .ToDictionary(g => g.Key, g => (string?)g.First().Attribute("text") ?? string.Empty, StringComparer.Ordinal);
    }

    private static readonly Lazy<FireAndTributeConfig> Config = new(LoadConfig);
    private static readonly Lazy<Dictionary<string, XElement>> Characters = new(LoadCharacters);
    private static readonly Lazy<Dictionary<string, XElement>> Clans = new(LoadClans);

    private static FireAndTributeConfig LoadConfig()
    {
        var path = Substitute.For<IPathService>();
        path.ModuleDataPath.Returns(RepoPaths.RepoPath("Main", "_Module", "ModuleData"));
        return new FireAndTributeConfigProvider(path, Substitute.For<IModLogger>()).GetConfig();
    }

    private static Dictionary<string, XElement> LoadCharacters()
    {
        var byId = new Dictionary<string, XElement>(StringComparer.Ordinal);
        // Languages holds string tables, never an NPCCharacter, so the scan skips it.
        var moduleData = RepoPaths.RepoPath("Main", "_Module", "ModuleData");
        var languages = Path.Combine(moduleData, "Languages") + Path.DirectorySeparatorChar;
        foreach (var file in Directory.EnumerateFiles(moduleData, "*.xml", SearchOption.AllDirectories)
                     .Where(f => !f.StartsWith(languages, StringComparison.OrdinalIgnoreCase)))
        {
            XDocument doc;
            try { doc = XDocument.Load(file); }
            catch (System.Xml.XmlException) { continue; }
            foreach (var el in doc.Descendants("NPCCharacter"))
            {
                var id = (string?)el.Attribute("id");
                if (id != null && (!byId.ContainsKey(id) || byId[id].Attribute("level") == null))
                    byId[id] = el;
            }
        }
        Assert.IsTrue(byId.Count > 1000, $"only {byId.Count} NPCCharacters parsed; the scan is broken");
        return byId;
    }

    private static Dictionary<string, XElement> LoadClans()
    {
        var doc = XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "characters", "clans.xml"));
        return doc.Descendants("Faction").Where(f => f.Attribute("id") != null).ToDictionary(f => (string)f.Attribute("id")!, f => f, StringComparer.Ordinal);
    }

    public static IEnumerable<object[]> HostIds() => Config.Value.Hosts.Select(h => new object[] { h.Id });

    public static IEnumerable<object[]> HostTroops() =>
        Config.Value.Hosts.SelectMany(h => h.Troops.Select(t => new object[] { h.Id, t.TroopId }));

    [TestMethod]
    public void TheShippedFile_HoldsEightHosts_OnePerOutlawClan_TwoFreeFiveEvilOneNeutral()
    {
        var hosts = Config.Value.Hosts;

        Assert.AreEqual(8, hosts.Count);
        Assert.AreEqual(8, hosts.Select(h => h.ClanId).Distinct().Count(), "one host per clan");
        CollectionAssert.AreEquivalent(FireAndTributeConfigProvider.AllowedClans, hosts.Select(h => h.ClanId).ToArray());
        Assert.AreEqual(2, hosts.Count(h => h.Side == FactionSide.Free));
        Assert.AreEqual(5, hosts.Count(h => h.Side == FactionSide.Evil));
        Assert.AreEqual(1, hosts.Count(h => h.Side == FactionSide.Neutral));
    }

    [TestMethod]
    public void EverySide_HasAHost()
    {
        foreach (var side in new[] { FactionSide.Free, FactionSide.Neutral, FactionSide.Evil })
            Assert.IsTrue(Config.Value.Hosts.Any(h => h.Side == side), side + " has no host");
    }

    [DataTestMethod]
    [DynamicData(nameof(HostIds), DynamicDataSourceType.Method)]
    public void EveryHost_NamesAClanThatExistsAsAnOutlawBanditFactionOfItsOwnCulture(string hostId)
    {
        var host = Config.Value.Hosts.Single(h => h.Id == hostId);

        Assert.IsTrue(Clans.Value.TryGetValue(host.ClanId, out var clan), $"{hostId}: clan {host.ClanId} is not in characters/clans.xml");
        Assert.AreEqual("true", (string?)clan!.Attribute("is_bandit"), $"{hostId}: {host.ClanId} is not a bandit faction");
        Assert.AreEqual("true", (string?)clan.Attribute("is_outlaw"), $"{hostId}: {host.ClanId} is not an outlaw faction");
        Assert.AreEqual("Culture." + host.ClanId, (string?)clan.Attribute("culture"), $"{hostId}: the clan's culture (the hideouts' culture) is its own id");
    }

    [DataTestMethod]
    [DynamicData(nameof(HostTroops), DynamicDataSourceType.Method)]
    public void EveryTroop_ExistsAsANonHeroSoldier(string hostId, string troopId)
    {
        Assert.IsTrue(Characters.Value.TryGetValue(troopId, out var el), $"{hostId}: {troopId} is not in ModuleData");
        Assert.AreEqual("Soldier", (string?)el!.Attribute("occupation"), $"{hostId}: {troopId} occupation");
        Assert.AreNotEqual("true", (string?)el.Attribute("is_hero"), $"{hostId}: {troopId} is a hero");
        Assert.IsTrue(int.TryParse((string?)el.Attribute("level"), out int level) && level >= 1, $"{hostId}: {troopId} has no level");
    }

    [DataTestMethod]
    [DynamicData(nameof(HostIds), DynamicDataSourceType.Method)]
    public void EveryHost_TakesItsTroopsFromItsClansDefaultRaiderTemplate(string hostId)
    {
        var host = Config.Value.Hosts.Single(h => h.Id == hostId);
        var templates = XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "taom_partyTemplates.xml"));
        var template = templates.Descendants("MBPartyTemplate").Single(t => (string?)t.Attribute("id") == host.ClanId + "_raider_party_template");
        var templateTroops = template.Descendants("PartyTemplateStack")
            .Select(s => ((string?)s.Attribute("troop") ?? string.Empty).Replace("NPCCharacter.", string.Empty))
            .ToHashSet();

        var outside = host.Troops.Select(t => t.TroopId).Where(id => !templateTroops.Contains(id)).ToList();

        Assert.AreEqual(0, outside.Count, $"{hostId}: troops not in {host.ClanId}'s raider template: {string.Join(", ", outside)}");
    }

    [DataTestMethod]
    [DynamicData(nameof(HostIds), DynamicDataSourceType.Method)]
    public void EveryHost_SharesSumToOne_AndNameCarriesItsOwnKey(string hostId)
    {
        var host = Config.Value.Hosts.Single(h => h.Id == hostId);

        Assert.AreEqual(1f, host.Troops.Sum(t => t.Share), 1e-4f);
        Assert.IsTrue(host.Name.StartsWith("{=taom_fat_host_" + host.Id + "}", StringComparison.Ordinal), host.Name);
    }

    [TestMethod]
    public void EverySide_TargetsOnlyTheOpposedSide_AndTheNeutralHostBothFreeAndEvil()
    {
        foreach (var host in Config.Value.Hosts)
        {
            Assert.IsFalse(host.TargetSides.Contains(FactionSide.Neutral), host.Id + " targets Neutral");
            Assert.IsFalse(host.TargetSides.Contains(host.Side), host.Id + " targets its own side");
        }
        CollectionAssert.AreEquivalent(new[] { FactionSide.Free, FactionSide.Evil }, Config.Value.Hosts.Single(h => h.Side == FactionSide.Neutral).TargetSides.ToArray());
    }

    [DataTestMethod]
    [DynamicData(nameof(HostIds), DynamicDataSourceType.Method)]
    public void EveryHost_HasItsAgreedSingularCollectiveName(string hostId)
    {
        var host = Config.Value.Hosts.Single(h => h.Id == hostId);

        Assert.AreEqual("{=taom_fat_host_" + hostId + "}" + AgreedNames[hostId], host.Name);
    }

    [DataTestMethod]
    [DynamicData(nameof(HostIds), DynamicDataSourceType.Method)]
    public void EveryHostName_IsTheEnglishOfItsRegisteredModuleStringsRow(string hostId)
    {
        var host = Config.Value.Hosts.Single(h => h.Id == hostId);
        string key = "taom_fat_host_" + hostId;

        Assert.IsTrue(ModuleStrings.Value.TryGetValue(key, out var row), $"{key} is not registered in taom_module_strings.xml");
        string english = host.Name.Substring(host.Name.IndexOf('}') + 1);
        Assert.AreEqual("{=" + key + "}" + english, row, "the JSON name and the registered row must carry the same English");
    }

    [TestMethod]
    public void TheFallbackHostName_HasItsOwnKey_NotAHostsKeyWithOtherEnglish()
    {
        Assert.IsTrue(ModuleStrings.Value.TryGetValue("taom_fat_host_unknown", out var row));
        Assert.AreEqual("{=taom_fat_host_unknown}A raiding host", row);
        Assert.AreEqual(RaidHostDef.FallbackName, row, "the code's fallback and the registered row carry the same text");
    }

    [TestMethod]
    public void EveryLanguage_CarriesARowForEachHostKeyAndTheFallback()
    {
        var keys = Config.Value.Hosts.Select(h => "taom_fat_host_" + h.Id).Concat(new[] { "taom_fat_host_unknown" }).ToList();
        var files = Directory.EnumerateFiles(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "Languages"), "std_taom_module_strings_*.xml", SearchOption.AllDirectories).ToList();
        Assert.AreEqual(12, files.Count, "twelve translated languages");

        foreach (var file in files)
        {
            var ids = XDocument.Load(file).Descendants("string").Select(e => (string?)e.Attribute("id")).ToHashSet();
            var missing = keys.Where(k => !ids.Contains(k)).ToList();
            Assert.AreEqual(0, missing.Count, $"{Path.GetFileName(file)} lacks: {string.Join(", ", missing)}");
        }
    }

    [TestMethod]
    public void TheShippedNames_KeepTheirAccents_ReadAsUtf8()
    {
        Assert.AreEqual("{=taom_fat_host_rhun}The Easterling Host of Rh\u00fbn", Config.Value.Hosts.Single(h => h.Id == "rhun").Name);
        Assert.AreEqual("{=taom_fat_host_gondor}The Free Company of An\u00f3rien", Config.Value.Hosts.Single(h => h.Id == "gondor").Name);
    }

    [TestMethod]
    public void TheShippedFile_HasNoEmOrEnDash()
    {
        var text = File.ReadAllText(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "fire_and_tribute", "fire_and_tribute.json"));

        Assert.IsFalse(text.Contains('\u2014') || text.Contains('\u2013'), "no em or en dash");
    }

    [DataTestMethod]
    [DynamicData(nameof(HostIds), DynamicDataSourceType.Method)]
    [TestCategory("LiveInstall")]
    public void EveryAnchor_IsATownOrCastleOnTheLiveMap_WhenTheMapIsInstalled(string hostId)
    {
        var settlements = LiveSettlements();
        var host = Config.Value.Hosts.Single(h => h.Id == hostId);

        var bad = host.Anchors
            .Where(r => !settlements.TryGetValue(r, out var el) || el.Descendants("Town").FirstOrDefault() == null)
            .ToList();

        Assert.AreEqual(0, bad.Count, $"{hostId}: anchors that are not a town or castle on the live map: {string.Join(", ", bad)}");
    }

    [DataTestMethod]
    [DynamicData(nameof(HostIds), DynamicDataSourceType.Method)]
    [TestCategory("LiveInstall")]
    public void EveryClanCulture_HasHideoutsInTheLiveMap_WhenTheMapIsInstalled(string hostId)
    {
        var host = Config.Value.Hosts.Single(h => h.Id == hostId);

        int hideouts = LiveSettlements().Values
            .Count(s => (string?)s.Attribute("type") == "Hideout" && (string?)s.Attribute("culture") == "Culture." + host.ClanId);

        Assert.IsTrue(hideouts > 0, $"{hostId}: the live map has no hideout of culture {host.ClanId}");
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void EveryFreeAndEvilHosts_AnchorsLieInRealmsOfItsOwnSide_SoItsSpawnPointIsFriendlyGround()
    {
        // The realm that owns an anchor is read by the settlement's authored culture, the way the shipped culture tables
        // are checked (ShippedMainCultureAlignmentCoverageTests), through the real alignment service.
        var settlements = LiveSettlements();
        var path = Substitute.For<IPathService>();
        path.ModuleDataPath.Returns(RepoPaths.RepoPath("Main", "_Module", "ModuleData"));
        var alignment = new AlignmentService(new AlignmentConfigProvider(path, Substitute.For<IModLogger>()), Substitute.For<IModLogger>());

        foreach (var host in Config.Value.Hosts.Where(h => h.Side != FactionSide.Neutral))
        {
            foreach (var anchor in host.Anchors)
            {
                string culture = ((string?)settlements[anchor].Attribute("culture") ?? string.Empty).Replace("Culture.", string.Empty);
                Assert.AreEqual(host.Side, alignment.GetCultureSide(culture),
                    $"{host.Id}: anchor {anchor} lies in culture '{culture}', which is not {host.Side}");
            }
        }
    }

    [TestMethod]
    public void TheShippedAnchors_AreTheOnesTheReviewPicked()
    {
        var byHost = Config.Value.Hosts.ToDictionary(h => h.Id, h => h.Anchors.ToArray());

        CollectionAssert.AreEqual(new[] { "town_RU4", "town_RU5", "town_RU7" }, byHost["erebor"], "the Blacklocks muster beside Rhun, not in the halls of Erebor");
        CollectionAssert.AreEqual(new[] { "town_EW1", "castle_EW4", "town_EW7" }, byHost["gondor"], "the Free Company musters on the Ithilien front");
    }

    private static Dictionary<string, XElement> LiveSettlements()
    {
        string path = LiveMapPath();
        if (!File.Exists(path))
            Assert.Inconclusive("The live TAOM_Map install is not on this machine.");
        return XDocument.Load(path).Descendants("Settlement")
            .Where(s => s.Attribute("id") != null)
            .GroupBy(s => (string)s.Attribute("id")!)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
    }
}
