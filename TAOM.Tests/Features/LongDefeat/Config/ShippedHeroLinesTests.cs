using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Tests.Features.LoadTimeStamps;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat.Config;

/// <summary>The REAL <c>long_defeat/hero_lines.json</c> (#1569), read through the reader over the repo's own ModuleSounds folder.</summary>
[TestClass]
public class ShippedHeroLinesTests
{
    private static string ModuleData => RepoPaths.RepoPath("Main", "_Module", "ModuleData");

    private static string Sounds => RepoPaths.RepoPath("Main", "_Module", "ModuleSounds");

    private static (HeroLinePlan Plan, LongDefeatConfigScope Scope, RecordingLogger Log) Load()
    {
        var log = new RecordingLogger();
        var scope = new LongDefeatConfigScope(log, "hero_lines.json");
        var text = File.ReadAllText(Path.Combine(ModuleData, "long_defeat", "hero_lines.json"));
        var plan = HeroLinesReader.Read(text, scope, rel =>
        {
            var full = Path.Combine(Sounds, rel);
            return File.Exists(full) ? full : null;
        });
        return (plan, scope, log);
    }

    [TestMethod]
    public void ShippedFile_ReadsWithNoWarning()
    {
        var (_, scope, log) = Load();

        Assert.AreEqual(0, scope.Warnings, string.Join("\n", log.Lines));
    }

    [TestMethod]
    public void ShippedFile_LegolasHasFourLinesOfThreeTakes()
    {
        var (plan, _, _) = Load();

        var lines = plan.Lines["legolas"];
        Assert.AreEqual(4, lines.Count);
        CollectionAssert.AreEqual(new[] { 3, 3, 3, 3 }, lines.Select(l => l.Count).ToArray());
    }

    [TestMethod]
    public void ShippedFile_EveryPathIsAFile()
    {
        var (plan, _, _) = Load();

        var paths = plan.Lines.Values.SelectMany(l => l).SelectMany(t => t).ToList();
        Assert.IsTrue(paths.Count > 0);
        foreach (var path in paths)
            Assert.IsTrue(File.Exists(path), path);
    }

    [TestMethod]
    public void ShippedFile_EveryHeroKeyIsAHeroOfTheShopOrAStoryMission()
    {
        var (plan, _, _) = Load();
        var known = new HashSet<string>(HeroIds("long_defeat/shop.json", "shops").Concat(HeroIds("story_missions/story_missions.json", "missions")));

        foreach (var hero in plan.Lines.Keys)
            Assert.IsTrue(known.Contains(hero), $"'{hero}' is no hero id in shop.json or story_missions.json");
    }

    private static IEnumerable<string> HeroIds(string file, string group)
    {
        var root = JObject.Parse(File.ReadAllText(Path.Combine(ModuleData, file)));
        var container = root[group]!;
        var owners = container is JObject o ? o.Properties().Select(p => p.Value) : container.Children();
        return owners.SelectMany(owner => owner["heroes"]?.Children() ?? Enumerable.Empty<JToken>()).Select(h => (string?)h["id"]).Where(id => id != null).Select(id => id!);
    }
}
