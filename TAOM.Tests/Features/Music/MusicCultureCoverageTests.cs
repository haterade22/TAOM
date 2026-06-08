using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace TAOM.Tests.Features.Music;

[TestClass]
public class MusicCultureCoverageTests
{
    private static readonly string ModuleDataPath = MusicTestPaths.ModuleDataPath;
    private static readonly string ModuleRootPath = MusicTestPaths.ModuleRootPath;

    private static readonly IReadOnlyCollection<string> RequiredBuckets = new[]
    {
        "battle_music",
        "character_creation",
        "siege_music",
        "tavern_wander",
        "town_wander",
        "worldmap"
    };

    private static readonly HashSet<string> DocumentedNeutralFallbackCultures =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "abanissa",
            "dunland_raiders",
            "erebor_warriors",
            "goblin",
            "gondor_soldiers",
            "gundabad_raiders",
            "harad_raiders",
            "mirkwood_stalkers",
            "mistymountainorcs",
            "rhun_raiders",
            "shaghana",
            "umbar_corsairs"
        };

    [TestMethod]
    public void NeutralCulture_HasEveryMusicBucket()
    {
        var neutralBuckets = new HashSet<string>(
            LoadMusicCulturesByBucket()
                .Where(pair => pair.Value.Contains("neutral_culture"))
                .Select(pair => pair.Key),
            StringComparer.OrdinalIgnoreCase);

        var missing = RequiredBuckets
            .Where(bucket => !neutralBuckets.Contains(bucket))
            .OrderBy(bucket => bucket)
            .ToList();

        Assert.AreEqual(0, missing.Count,
            $"neutral_culture is missing music buckets: {string.Join(", ", missing)}");
    }

    [TestMethod]
    public void EveryCultureDomainId_HasMusicPoolOrDocumentedNeutralFallback()
    {
        var musicCultures = new HashSet<string>(
            LoadMusicCulturesByBucket()
                .SelectMany(pair => pair.Value)
                .Where(culture => !string.Equals(culture, "neutral_culture", StringComparison.OrdinalIgnoreCase)),
            StringComparer.OrdinalIgnoreCase);

        var uncovered = LoadCultureDomain()
            .Where(culture => !musicCultures.Contains(culture))
            .Where(culture => !DocumentedNeutralFallbackCultures.Contains(culture))
            .OrderBy(culture => culture)
            .ToList();

        Assert.AreEqual(0, uncovered.Count,
            "Every TAOM culture domain id must have a music folder or an explicit neutral fallback. " +
            $"Missing coverage: {string.Join(", ", uncovered)}");
    }

    [TestMethod]
    public void DocumentedNeutralFallbackCultures_AreStillMissingDedicatedPools()
    {
        var musicCultures = new HashSet<string>(
            LoadMusicCulturesByBucket().SelectMany(pair => pair.Value),
            StringComparer.OrdinalIgnoreCase);

        var nowCovered = DocumentedNeutralFallbackCultures
            .Where(culture => musicCultures.Contains(culture))
            .OrderBy(culture => culture)
            .ToList();

        Assert.AreEqual(0, nowCovered.Count,
            "These cultures now have dedicated music pools and should be removed from the documented fallback list: " +
            string.Join(", ", nowCovered));
    }

    private static Dictionary<string, HashSet<string>> LoadMusicCulturesByBucket()
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in LoadRegisteredModuleSoundXmlFiles().SelectMany(path => XDocument.Load(path).Descendants("module_sound")))
        {
            var relativePath = NormalizePath(entry.Attribute("path")?.Value);
            if (!relativePath.StartsWith("taom/", StringComparison.OrdinalIgnoreCase))
                continue;

            var parts = relativePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4)
                continue;

            var bucket = parts[1];
            var culture = parts[2];
            if (!result.TryGetValue(bucket, out var cultures))
            {
                cultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                result[bucket] = cultures;
            }

            cultures.Add(culture);
        }

        return result;
    }

    private static IEnumerable<string> LoadRegisteredModuleSoundXmlFiles()
    {
        var projectPath = Path.Combine(ModuleDataPath, "project.mbproj");
        Assert.IsTrue(File.Exists(projectPath), $"project.mbproj not found at {projectPath}");

        return XDocument.Load(projectPath)
            .Descendants("file")
            .Where(e => string.Equals(e.Attribute("type")?.Value, "module_sound", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Attribute("name")?.Value)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => Path.Combine(ModuleRootPath, NormalizePath(name)))
            .ToList();
    }

    private static HashSet<string> LoadCultureDomain()
    {
        var cultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var culture in LoadCharacterCreationCultures())
            cultures.Add(culture);

        foreach (var culture in LoadTaomSpCultureIds())
            cultures.Add(culture);

        return cultures;
    }

    private static IEnumerable<string> LoadCharacterCreationCultures()
    {
        var path = Path.Combine(ModuleDataPath, "charactercreation", "cultures.json");
        Assert.IsTrue(File.Exists(path), $"cultures.json not found at {path}");

        return JArray.Parse(File.ReadAllText(path))
            .Select(c => c.Value<string>("culture_id"))
            .Where(id => !string.IsNullOrEmpty(id));
    }

    private static IEnumerable<string> LoadTaomSpCultureIds()
    {
        var path = Path.Combine(ModuleDataPath, "taom_spcultures.xml");
        Assert.IsTrue(File.Exists(path), $"taom_spcultures.xml not found at {path}");

        return XDocument.Load(path)
            .Descendants("Culture")
            .Select(c => c.Attribute("id")?.Value)
            .Where(id => !string.IsNullOrEmpty(id));
    }

    private static string NormalizePath(string path)
    {
        return (path ?? string.Empty).Replace('\\', '/').TrimStart('/');
    }
}
