using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.FactionMap;
using TAOM.Features.FactionUI;
using TAOM.Features.FactionUI.FactionScreen;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// Shipped-data regression (lens7-xml #6, #704): loads the REAL FactionUI configs through the real
/// <see cref="FactionScreenConfigProvider"/> and factions.json through the real
/// <see cref="FactionConfigProvider"/>, then cross-checks every faction key, character id and kingdom
/// id against the real data - the repo's <c>characters/*.xml</c> and <c>named_companions.xml</c>,
/// <see cref="FactionScreenArt.SpecialCharacters"/>, and (for the ids TAOM's <c>lords.xslt</c> retags,
/// and for TAOM's own kingdoms) the vanilla install. Reading the markup cannot catch a typo'd id;
/// this loads what the game loads. Skips (not fails) when the install is absent, matching
/// LordFamilyTransformTests / CustomBattleCommandersShippedDataTests.
/// </summary>
[TestClass]
[TestCategory("LiveInstall")]
public class FactionUIConfigIdsTests
{
    private const string DefaultGameDir = @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord";

    private static string? FindRepoModuleData()
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

    private static string? FindSandBoxModuleData()
    {
        var game = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        foreach (var root in new[] { game, DefaultGameDir })
        {
            if (string.IsNullOrEmpty(root)) continue;
            var path = Path.Combine(root!, "Modules", "SandBox", "ModuleData");
            if (File.Exists(Path.Combine(path, "lords.xml")) && File.Exists(Path.Combine(path, "spkingdoms.xml")))
                return path;
        }

        return null;
    }

    /// <summary>The real FactionScreenConfigProvider, reading the real repo ModuleData/FactionUI files
    /// (IFrontEndResourceAdapter.ReadAllText is a plain File.ReadAllText wrapper, so a substitute that
    /// forwards to the real file is a faithful stand-in - no engine type is touched).</summary>
    private static FactionScreenConfig RealFactionScreenConfig(string moduleData)
    {
        var adapter = Substitute.For<IFrontEndResourceAdapter>();
        adapter.ReadAllText(Arg.Any<string>()).Returns(ci =>
        {
            var path = ci.Arg<string>();
            return File.Exists(path) ? File.ReadAllText(path) : null;
        });
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(moduleData);
        pathService.ModuleRootPath.Returns(Path.GetDirectoryName(moduleData));
        var provider = new FactionScreenConfigProvider(adapter, new FactionUIPaths(pathService), Substitute.For<IModLogger>());
        return provider.Config;
    }

    private static HashSet<string> PlayableFactionKeys(string moduleData)
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(moduleData);
        var provider = new FactionConfigProvider(pathService, Substitute.For<IModLogger>());
        var factions = provider.LoadFactions();
        return new HashSet<string>(factions.Where(kv => kv.Value.Playable).Select(kv => kv.Key), StringComparer.Ordinal);
    }

    private static HashSet<string> NpcCharacterIds(string xmlText)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(xmlText, "<NPCCharacter\\s[^>]*\\bid=\"([^\"]+)\""))
            ids.Add(m.Groups[1].Value);
        return ids;
    }

    private static HashSet<string> RepoNpcCharacterIds(string moduleData)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(Path.Combine(moduleData, "characters"), "*.xml"))
            ids.UnionWith(NpcCharacterIds(File.ReadAllText(file)));

        var namedCompanions = Path.Combine(moduleData, "named_companions", "named_companions.xml");
        if (File.Exists(namedCompanions))
            ids.UnionWith(NpcCharacterIds(File.ReadAllText(namedCompanions)));

        return ids;
    }

    /// <summary>Vanilla lord ids lords.xslt retags (its xsl:template match="NPCCharacter[@id='X']"
    /// predicates), mirroring CustomBattleCommandersShippedDataTests.RealLordIds.</summary>
    private static HashSet<string> XsltRetaggedLordIds(string moduleData)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var xslt = Path.Combine(moduleData, "lords.xslt");
        if (File.Exists(xslt))
            foreach (Match m in Regex.Matches(File.ReadAllText(xslt), "NPCCharacter\\[@id='([^']+)'\\]"))
                ids.Add(m.Groups[1].Value);
        return ids;
    }

    private static HashSet<string> VanillaLordIds(string sandBoxModuleData) =>
        NpcCharacterIds(File.ReadAllText(Path.Combine(sandBoxModuleData, "lords.xml")));

    private static HashSet<string> KingdomIds(string xmlText)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(xmlText, "<Kingdom\\s[^>]*\\bid=\"([^\"]+)\""))
            ids.Add(m.Groups[1].Value);
        return ids;
    }

    private static HashSet<string> RepoKingdomIds(string moduleData)
    {
        var path = Path.Combine(moduleData, "taom_spkingdoms.xml");
        return File.Exists(path) ? KingdomIds(File.ReadAllText(path)) : new HashSet<string>(StringComparer.Ordinal);
    }

    private static HashSet<string> VanillaKingdomIds(string sandBoxModuleData) =>
        KingdomIds(File.ReadAllText(Path.Combine(sandBoxModuleData, "spkingdoms.xml")));

    private static bool ResolvesToCharacter(string characterId, HashSet<string> repoIds, HashSet<string> retagged, HashSet<string> vanillaIds) =>
        repoIds.Contains(characterId) || (retagged.Contains(characterId) && vanillaIds.Contains(characterId));

    [TestMethod]
    public void FactionCharactersJson_EveryFactionKey_IsAPlayableFaction()
    {
        var md = FindRepoModuleData();
        if (md == null) { Assert.Inconclusive("Repo ModuleData path not found - run from repo root"); return; }

        var playable = PlayableFactionKeys(md);
        var config = RealFactionScreenConfig(md);
        var unknown = config.ViewportCharacters.Keys.Where(k => !playable.Contains(k)).ToList();

        Assert.AreEqual(0, unknown.Count,
            $"faction_characters.json keys not a playable faction in factions.json: {string.Join(", ", unknown)}");
    }

    [TestMethod]
    public void FactionKingdomsJson_EveryFactionKey_IsAPlayableFaction()
    {
        var md = FindRepoModuleData();
        if (md == null) { Assert.Inconclusive("Repo ModuleData path not found - run from repo root"); return; }

        var playable = PlayableFactionKeys(md);
        var config = RealFactionScreenConfig(md);
        var unknown = config.Kingdoms.Keys.Where(k => !playable.Contains(k)).ToList();

        Assert.AreEqual(0, unknown.Count,
            $"faction_kingdoms.json keys not a playable faction in factions.json: {string.Join(", ", unknown)}");
    }

    [TestMethod]
    public void FactionMapPosJson_EveryFactionKey_IsAPlayableFaction()
    {
        var md = FindRepoModuleData();
        if (md == null) { Assert.Inconclusive("Repo ModuleData path not found - run from repo root"); return; }

        var playable = PlayableFactionKeys(md);
        var config = RealFactionScreenConfig(md);
        var unknown = config.MapPositions.Keys.Where(k => !playable.Contains(k)).ToList();

        Assert.AreEqual(0, unknown.Count,
            $"faction_map_pos.json keys not a playable faction in factions.json: {string.Join(", ", unknown)}");
    }

    [TestMethod]
    public void FactionViewportJson_EveryFactionKey_IsAPlayableFaction()
    {
        var md = FindRepoModuleData();
        if (md == null) { Assert.Inconclusive("Repo ModuleData path not found - run from repo root"); return; }

        var playable = PlayableFactionKeys(md);
        var config = RealFactionScreenConfig(md);
        var unknown = config.ViewportTweaks.Keys.Where(k => !playable.Contains(k)).ToList();

        Assert.AreEqual(0, unknown.Count,
            $"faction_viewport.json keys not a playable faction in factions.json: {string.Join(", ", unknown)}");
    }

    [TestMethod]
    public void FactionCharactersJson_EveryCharacterId_ResolvesToAnNpcCharacter()
    {
        var md = FindRepoModuleData();
        if (md == null) { Assert.Inconclusive("Repo ModuleData path not found - run from repo root"); return; }
        var sandbox = FindSandBoxModuleData();
        if (sandbox == null) { Assert.Inconclusive($"Bannerlord install not found (BANNERLORD_GAME_DIR or {DefaultGameDir})"); return; }

        var repoIds = RepoNpcCharacterIds(md);
        var retagged = XsltRetaggedLordIds(md);
        var vanillaIds = VanillaLordIds(sandbox);
        var config = RealFactionScreenConfig(md);

        var missing = config.ViewportCharacters
            .Where(kv => !ResolvesToCharacter(kv.Value, repoIds, retagged, vanillaIds))
            .Select(kv => $"{kv.Key}:{kv.Value}").ToList();

        Assert.AreEqual(0, missing.Count,
            $"faction_characters.json character ids that resolve to no NPCCharacter: {string.Join(", ", missing)}");
    }

    [TestMethod]
    public void FactionScreenArt_EverySpecialCharacterId_ResolvesToAnNpcCharacter()
    {
        var md = FindRepoModuleData();
        if (md == null) { Assert.Inconclusive("Repo ModuleData path not found - run from repo root"); return; }
        var sandbox = FindSandBoxModuleData();
        if (sandbox == null) { Assert.Inconclusive($"Bannerlord install not found (BANNERLORD_GAME_DIR or {DefaultGameDir})"); return; }

        var repoIds = RepoNpcCharacterIds(md);
        var retagged = XsltRetaggedLordIds(md);
        var vanillaIds = VanillaLordIds(sandbox);

        var missing = new List<string>();
        foreach (var entry in FactionScreenArt.SpecialCharacters)
            foreach (var character in entry.Value)
                if (!ResolvesToCharacter(character.CharacterId, repoIds, retagged, vanillaIds))
                    missing.Add($"{entry.Key}:{character.CharacterId}");

        Assert.AreEqual(0, missing.Count,
            $"FactionScreenArt.SpecialCharacters ids that resolve to no NPCCharacter: {string.Join(", ", missing)}");
    }

    [TestMethod]
    public void FactionKingdomsJson_EveryKingdomId_ResolvesToAKingdom()
    {
        var md = FindRepoModuleData();
        if (md == null) { Assert.Inconclusive("Repo ModuleData path not found - run from repo root"); return; }
        var sandbox = FindSandBoxModuleData();
        if (sandbox == null) { Assert.Inconclusive($"Bannerlord install not found (BANNERLORD_GAME_DIR or {DefaultGameDir})"); return; }

        var repoKingdoms = RepoKingdomIds(md);
        var vanillaKingdoms = VanillaKingdomIds(sandbox);
        var config = RealFactionScreenConfig(md);

        var missing = config.Kingdoms
            .Where(kv => !repoKingdoms.Contains(kv.Value) && !vanillaKingdoms.Contains(kv.Value))
            .Select(kv => $"{kv.Key}:{kv.Value}").ToList();

        Assert.AreEqual(0, missing.Count,
            $"faction_kingdoms.json kingdom ids that resolve to no Kingdom: {string.Join(", ", missing)}");
    }
}
