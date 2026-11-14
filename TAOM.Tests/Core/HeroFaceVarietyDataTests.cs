using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.LordFaceVariety;
using TAOM.Tests.Features.LordFaceVariety;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Core;

/// <summary>
/// Lord Face Variety (#952) gate over the repo's own ModuleData. It builds the hero list the campaign will
/// start with from heroes.xml and the NPCCharacter files, runs the real <see cref="LordFaceVarietyService"/>
/// over a fake adapter whose rolls always give a unique face, and asserts which groups can still share one.
/// Vanilla lords are not in this repo, so they are outside the gate; the pass handles them at runtime.
/// A template that is not in TAOM_bodyproperties.xml is treated as a range with spread.
/// </summary>
[TestClass]
public class HeroFaceVarietyDataTests
{
    /// <summary>
    /// Rule B count: 216 on the data of 2026-11-14, when rule B started to ignore the colour fields and took
    /// in the six colour-only twins of #1221 (210 before).
    /// </summary>
    private const int MaxRolledAroundOwnFace = 216;

    private sealed record Npc(string Id, string Culture, string Race, bool IsFemale, string Key, bool Spread, bool Explicit);

    private static string ModuleData => RepoPath("Main", "_Module", "ModuleData");

    private static IEnumerable<XDocument> LoadDocs()
    {
        foreach (var file in Directory.EnumerateFiles(ModuleData, "*.xml", SearchOption.AllDirectories))
        {
            if (file.Contains(Path.DirectorySeparatorChar + "Languages" + Path.DirectorySeparatorChar)) continue;
            if (Path.GetFileName(file) == "custom_battle_lords.xml") continue;
            XDocument doc;
            try { doc = XDocument.Load(file); }
            catch (Exception) { continue; }
            yield return doc;
        }
    }

    private static Dictionary<string, (string Min, string Max)> LoadTemplates()
    {
        var doc = XDocument.Load(Path.Combine(ModuleData, "TAOM_bodyproperties.xml"));
        return doc.Descendants("BodyProperty").ToDictionary(
            e => "BodyProperty." + (string)e.Attribute("id"),
            e => (((string)e.Element("BodyPropertiesMin")?.Attribute("key")) ?? "",
                  ((string)e.Element("BodyPropertiesMax")?.Attribute("key")) ?? ""),
            StringComparer.Ordinal);
    }

    private static Npc ToNpc(XElement e, Dictionary<string, (string Min, string Max)> templates)
    {
        var face = e.Element("face");
        var template = (string)face?.Element("face_key_template")?.Attribute("value");
        var explicitKey = (string)face?.Element("BodyProperties")?.Attribute("key");
        string key;
        bool spread;
        if (template != null)
        {
            // The hero starts on the template minimum.
            key = templates.TryGetValue(template, out var range) ? "min:" + template + ":" + range.Min : "min:" + template;
            spread = !templates.TryGetValue(template, out var r) || !string.Equals(r.Min, r.Max, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            key = "key:" + (explicitKey ?? "none").ToUpperInvariant();
            spread = false;
        }

        return new Npc((string)e.Attribute("id"),
            ((string)e.Attribute("culture") ?? "").Replace("Culture.", ""),
            (string)e.Attribute("race") ?? "human",
            string.Equals((string)e.Attribute("is_female"), "true", StringComparison.OrdinalIgnoreCase),
            key, spread, template == null);
    }

    private static (List<Npc> Heroes, HashSet<string> Leaders) LoadCampaignHeroes()
    {
        var docs = LoadDocs().ToList();
        var templates = LoadTemplates();

        var npcs = new Dictionary<string, Npc>(StringComparer.Ordinal);
        foreach (var e in docs.SelectMany(d => d.Descendants("NPCCharacter")))
        {
            var id = (string)e.Attribute("id");
            if (id != null && !npcs.ContainsKey(id)) npcs[id] = ToNpc(e, templates);
        }

        var heroIds = docs.SelectMany(d => d.Descendants("Hero")).Select(e => (string)e.Attribute("id"))
            .Where(id => id != null).Distinct(StringComparer.Ordinal);
        var heroes = heroIds.Where(npcs.ContainsKey).Select(id => npcs[id]).ToList();

        var leaders = new HashSet<string>(
            docs.SelectMany(d => d.Descendants("Faction"))
                .Select(f => ((string)f.Attribute("owner") ?? "").Replace("Hero.", ""))
                .Where(id => id.Length > 0),
            StringComparer.Ordinal);
        return (heroes, leaders);
    }

    private static FakeHeroFaceAdapter BuildAdapter(List<Npc> heroes, HashSet<string> leaders)
    {
        var adapter = new FakeHeroFaceAdapter();
        foreach (var n in heroes)
            adapter.Add(new HeroFaceSnapshot(n.Id, n.Race, n.IsFemale, IsPlayer: false, IsOriginal: true,
                IsClanLeader: leaders.Contains(n.Id), StaticKey: n.Key, OnRangeMin: !n.Explicit, RangeHasSpread: n.Spread));
        return adapter;
    }

    [TestMethod]
    public void Data_AfterThePass_OnlyTheTwinsStillShareAFace()
    {
        var (heroes, leaders) = LoadCampaignHeroes();
        Assert.IsTrue(heroes.Count > 500, $"only {heroes.Count} heroes read; the ModuleData layout changed");
        var adapter = BuildAdapter(heroes, leaders);

        var report = new LordFaceVarietyService(adapter, Substitute.For<IModLogger>()).Run();

        Assert.IsTrue(report.RolledInRange > 100, "rule A should reach every hero that starts on a template minimum");
        Assert.AreEqual(0, report.Failed);
        var left = report.StillShared.Select(g => string.Join("|", g)).ToList();
        var expected = string.Join("|", LordFaceVarietyService.SharedByDesignIds.OrderBy(x => x, StringComparer.Ordinal));
        CollectionAssert.AreEqual(new[] { expected }, left,
            "These heroes would still share a face after the pass. Give them distinct faces in the data, or add "
            + "the pair to LordFaceVarietyService.SharedByDesignIds when the sharing is the point.\n  "
            + string.Join("\n  ", left));
        Assert.IsTrue(report.RolledAroundOwnFace <= MaxRolledAroundOwnFace,
            $"{report.RolledAroundOwnFace} heroes would be rolled around their own face (rule B), ceiling "
            + $"{MaxRolledAroundOwnFace}. Rule B keeps a hero recognisable but moves him only a little, so a hero "
            + "who shares an explicit face key is a weaker result than one on a face_key_template. Move the new "
            + "shared group onto its culture's template. Lower the ceiling when a group moves; raise it only "
            + "on purpose.");
        Console.WriteLine($"[HeroFaceVariety] heroes={heroes.Count} ruleA={report.RolledInRange} ruleB={report.RolledAroundOwnFace}");
    }

    /// <summary>
    /// Heroes whose key differs from another hero's only in colour (#1221): before rule B ignored the colour
    /// fields, each counted as a unique face and kept the other's face in other colours.
    /// </summary>
    [TestMethod]
    public void Data_ColourOnlyTwins_AreRolledAroundTheirOwnFace()
    {
        var (heroes, leaders) = LoadCampaignHeroes();
        var adapter = BuildAdapter(heroes, leaders);

        new LordFaceVarietyService(adapter, Substitute.For<IModLogger>()).Run();

        var rolled = new HashSet<string>(adapter.Calls.Where(c => c.Kind == "own").Select(c => c.Id), StringComparer.Ordinal);
        var groups = new[]
        {
            new[] { "lord_E1_3", "lord_E1_6", "lord_E4_4", "lord_E5_2", "lord_E6_2", "lord_E6_3", "lord_E7_11" },
            new[] { "lord_E1_11", "lord_E2_2", "lord_E3_2", "lord_E3_4", "lord_E3_5", "lord_E4_2" },
            new[] { "lord_1_9_5", "lord_AN4_3", "lord_1_45_4" },
        };
        foreach (var group in groups)
        {
            var kept = group.Where(id => !rolled.Contains(id)).ToList();
            Assert.AreEqual(1, kept.Count, "one anchor should keep the face, the rest be rolled; kept: " + string.Join(", ", kept));
        }
    }

    [TestMethod]
    public void Data_EveryCanonAnchorAndTwinIdIsAnExplicitKeyHero()
    {
        var (heroes, _) = LoadCampaignHeroes();
        var explicitIds = new HashSet<string>(heroes.Where(h => h.Explicit).Select(h => h.Id), StringComparer.Ordinal);

        var missing = LordFaceVarietyService.CanonAnchorIds.Concat(LordFaceVarietyService.SharedByDesignIds)
            .Where(id => !explicitIds.Contains(id)).ToList();

        Assert.AreEqual(0, missing.Count,
            "an anchor or twin must be a hero in heroes.xml whose NPCCharacter carries an explicit BodyProperties key "
            + "(a hero on a face_key_template is rolled by rule A, so he is never kept): " + string.Join(", ", missing));
    }

    [TestMethod]
    public void Data_HaradLordsShareNoExplicitFaceKey()
    {
        var (heroes, _) = LoadCampaignHeroes();
        var harad = new HashSet<string> { "abanissa", "shaghana", "umbar" };

        var shared = heroes
            .Where(h => harad.Contains(h.Culture) && h.Explicit)
            .GroupBy(h => (h.Race, h.IsFemale, h.Key))
            .Where(g => g.Count() > 1)
            .Select(g => string.Join("|", g.Select(h => h.Id).OrderBy(x => x, StringComparer.Ordinal)))
            .ToList();

        Assert.AreEqual(0, shared.Count,
            "Harad heroes sharing one explicit face key; use face_key_template so each draws its own face:\n  "
            + string.Join("\n  ", shared));
    }
}
