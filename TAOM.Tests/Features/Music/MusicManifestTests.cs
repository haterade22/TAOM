using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.Music;

[TestClass]
public class MusicManifestTests
{
    private static readonly string ModuleRootPath = MusicTestPaths.ModuleRootPath;
    private static readonly string ModuleDataPath = MusicTestPaths.ModuleDataPath;
    private static readonly string ModuleSoundsPath = MusicTestPaths.ModuleSoundsPath;

    private static readonly IReadOnlyDictionary<string, int> ExpectedBucketCounts =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["battle_music"] = 65,
            ["character_creation"] = 116,
            ["siege_music"] = 55,
            ["tavern_wander"] = 48,
            ["town_wander"] = 76,
            ["worldmap"] = 116
        };

    [TestMethod]
    public void TaomMusicEntries_AreRegisteredInModuleSoundsXml()
    {
        var entries = LoadTaomMusicEntries();
        var expectedCount = ExpectedBucketCounts.Values.Sum();

        Assert.AreEqual(expectedCount, entries.Count,
            $"Expected all {expectedCount} TAOM music registrations to be merged into Main/_Module/ModuleData/module_sounds.xml.");
    }

    [TestMethod]
    public void TaomMusicEntries_AreTwoDimensionalMusicEvents()
    {
        var invalid = LoadTaomMusicEntries()
            .Where(e => !string.Equals(e.Attribute("sound_category")?.Value, "music", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(e.Attribute("is_2d")?.Value, "true", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Attribute("name")?.Value ?? "(missing name)")
            .ToList();

        Assert.AreEqual(0, invalid.Count,
            $"TAOM music entries must be 2D music events: {string.Join(", ", invalid)}");
    }

    [TestMethod]
    public void TaomMusicBucketCounts_MatchReplacementManifest()
    {
        var actual = LoadTaomMusicEntries()
            .Select(e => GetBucket(e.Attribute("path")?.Value))
            .GroupBy(bucket => bucket, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        foreach (var expected in ExpectedBucketCounts)
        {
            actual.TryGetValue(expected.Key, out var count);
            Assert.AreEqual(expected.Value, count,
                $"Unexpected track count for music bucket '{expected.Key}'.");
        }

        var unexpected = actual.Keys
            .Where(bucket => !ExpectedBucketCounts.ContainsKey(bucket))
            .OrderBy(bucket => bucket)
            .ToList();

        Assert.AreEqual(0, unexpected.Count,
            $"Unexpected TAOM music buckets in module_sounds.xml: {string.Join(", ", unexpected)}");
    }

    [TestMethod]
    public void EveryTaomMusicXmlPath_HasAnOggAsset()
    {
        var missing = LoadTaomMusicEntries()
            .Select(e => e.Attribute("path")?.Value)
            .Where(path => !string.IsNullOrEmpty(path))
            .Where(path => !File.Exists(Path.Combine(ModuleSoundsPath, NormalizeRelativePath(path))))
            .OrderBy(path => path)
            .ToList();

        Assert.AreEqual(0, missing.Count,
            $"module_sounds.xml references missing TAOM music files: {string.Join(", ", missing)}");
    }

    [TestMethod]
    public void EveryTaomMusicOggAsset_HasAnXmlRegistration()
    {
        var taomRoot = Path.Combine(ModuleSoundsPath, "taom");
        Assert.IsTrue(Directory.Exists(taomRoot),
            $"TAOM music asset directory is missing: {taomRoot}");

        var registeredPaths = new HashSet<string>(
            LoadTaomMusicEntries()
                .Select(e => NormalizeRelativePath(e.Attribute("path")?.Value))
                .Where(path => !string.IsNullOrEmpty(path)),
            StringComparer.OrdinalIgnoreCase);

        var unregistered = Directory.GetFiles(taomRoot, "*.ogg", SearchOption.AllDirectories)
            .Select(path => NormalizeRelativePath(GetModuleSoundRelativePath(path)))
            .Where(path => !registeredPaths.Contains(path))
            .OrderBy(path => path)
            .ToList();

        Assert.AreEqual(0, unregistered.Count,
            $"TAOM music OGG files with no module_sounds.xml registration: {string.Join(", ", unregistered)}");
    }

    private static List<XElement> LoadTaomMusicEntries()
    {
        return LoadRegisteredModuleSoundXmlFiles()
            .SelectMany(path => XDocument.Load(path).Descendants("module_sound"))
            .Where(e => StartsWithTaom(e.Attribute("name")?.Value)
                || StartsWithTaom(e.Attribute("path")?.Value))
            .ToList();
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
            .Select(name => Path.Combine(ModuleRootPath, NormalizeRelativePath(name)))
            .ToList();
    }

    private static bool StartsWithTaom(string value)
    {
        return !string.IsNullOrEmpty(value)
            && value.StartsWith("taom/", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetBucket(string path)
    {
        var parts = NormalizeRelativePath(path)
            .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

        return parts.Length >= 2 && string.Equals(parts[0], "taom", StringComparison.OrdinalIgnoreCase)
            ? parts[1]
            : string.Empty;
    }

    private static string GetModuleSoundRelativePath(string fullPath)
    {
        var root = Path.GetFullPath(ModuleSoundsPath);
        var file = Path.GetFullPath(fullPath);
        return file.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string NormalizeRelativePath(string path)
    {
        return (path ?? string.Empty).Replace('\\', '/').TrimStart('/');
    }
}
