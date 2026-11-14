using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Core;

/// <summary>
/// Builds the NPCCharacters document a Custom Battle loads, the way MBObjectManager.CreateMergedXmlFile does: every
/// registration for game type CustomGame in module order (installed SandBoxCore and CustomBattle, then TAOM from the
/// repo), each stylesheet applied to everything merged before it, then its XML appended. The last definition of an
/// id is the one the object manager keeps. The troop picker offers a character when it is a non-obsolete Soldier
/// (ArmyCompositionGroupVM, TcbVM), the muster's commander list when it is a non-obsolete hero of the culture.
/// An engine update that adds or renames a vanilla soldier, dummy or commander id fails here.
/// </summary>
[TestClass]
[TestCategory("LiveInstall")]
public class CustomBattleTroopFilterLiveTests
{
    private const string DefaultGameDir = @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord";

    // Cultures TAOM reuses whose vanilla soldiers the filter hides. battania (Khand) keeps its vanilla line, aserai
    // and shaghana are the Harad rule's.
    private static readonly string[] HiddenVanillaLineCultures = { "empire", "sturgia", "vlandia", "khuzait" };

    private static readonly string[] DummyPrefixes = { "gear_practice_dummy_", "gear_dummy_", "weapon_dummy_", "weapon_practice_stage_" };

    private static readonly Lazy<Dictionary<string, (XElement Last, string FirstModule)>> Merged = new(BuildMerged);

    [TestMethod]
    public void VanillaSoldiersOfDunlandDaleRohanRhun_AreNotOffered()
    {
        var offered = Merged.Value
            .Where(kv => kv.Value.FirstModule != "TAOM" && IsOfferedSoldier(kv.Value.Last)
                         && HiddenVanillaLineCultures.Contains(CultureOf(kv.Value.Last)))
            .Select(kv => kv.Key).OrderBy(id => id, StringComparer.Ordinal).ToList();

        Assert.AreEqual(0, offered.Count, "vanilla soldiers still offered: " + string.Join(", ", offered));
    }

    [TestMethod]
    public void TrainingDummiesAndPracticeOpponents_AreNotOffered()
    {
        var offered = Merged.Value
            .Where(kv => DummyPrefixes.Any(p => kv.Key.StartsWith(p, StringComparison.Ordinal)) && IsOfferedSoldier(kv.Value.Last))
            .Select(kv => kv.Key).OrderBy(id => id, StringComparer.Ordinal).ToList();

        Assert.AreEqual(0, offered.Count, "dummies still offered: " + string.Join(", ", offered));
    }

    [TestMethod]
    public void VanillaCommanders_AreOfferedOnlyForDunlandAndKhand()
    {
        var offered = Merged.Value
            .Where(kv => kv.Value.FirstModule == "CustomBattle" && (string?)kv.Value.Last.Attribute("is_hero") == "true"
                         && (string?)kv.Value.Last.Attribute("is_obsolete") != "true")
            .Select(kv => CultureOf(kv.Value.Last)).Distinct().OrderBy(c => c, StringComparer.Ordinal).ToList();

        CollectionAssert.AreEqual(new[] { "battania", "empire" }, offered);
    }

    // The exception that keeps Khand playable: TAOM has no Khand soldiers, so the vanilla Battanian line stays,
    // with archers and cavalry.
    [TestMethod]
    public void Khand_StillOffersInfantryArchersAndCavalry()
    {
        var groups = Merged.Value.Values
            .Where(v => IsOfferedSoldier(v.Last) && CultureOf(v.Last) == "battania")
            .Select(v => (string?)v.Last.Attribute("default_group")).Distinct().ToList();

        CollectionAssert.IsSubsetOf(new[] { "Infantry", "Ranged", "Cavalry" }, groups);
    }

    // Two-way check on the TAOM side: the new rules hide no TAOM soldier that is not a dummy or opponent.
    [TestMethod]
    public void EveryTaomSoldierOutsideTheOlderRules_IsStillOffered()
    {
        string[] olderRulePrefixes = { "legion_of_the_betrayed_tier_", "company_of_the_boar_tier_", "eleftheroi_tier_", "brotherhood_of_woods_tier_" };
        var hidden = Merged.Value
            .Where(kv => kv.Value.FirstModule == "TAOM" && (string?)kv.Value.Last.Attribute("occupation") == "Soldier"
                         && (string?)kv.Value.Last.Attribute("is_hero") != "true"
                         && (string?)kv.Value.Last.Attribute("is_obsolete") == "true"
                         && !DummyPrefixes.Any(p => kv.Key.StartsWith(p, StringComparison.Ordinal))
                         && !olderRulePrefixes.Any(p => kv.Key.StartsWith(p, StringComparison.Ordinal))
                         && CultureOf(kv.Value.Last) is not ("aserai" or "shaghana"))
            .Select(kv => kv.Key).ToList();

        // fords_troops (all obsolete by design, loads after the filter) is the one file allowed here.
        var fords = LoadDocument(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "fords_of_war", "fords_troops.xml"))
            .Descendants("NPCCharacter").Select(e => (string?)e.Attribute("id")).ToHashSet(StringComparer.Ordinal);
        var unexpected = hidden.Where(id => !fords.Contains(id)).OrderBy(id => id, StringComparer.Ordinal).ToList();

        Assert.AreEqual(0, unexpected.Count, "TAOM soldiers hidden: " + string.Join(", ", unexpected));
    }

    private static bool IsOfferedSoldier(XElement e) =>
        (string?)e.Attribute("occupation") == "Soldier" && (string?)e.Attribute("is_obsolete") != "true";

    private static string CultureOf(XElement e)
    {
        var c = (string?)e.Attribute("culture") ?? "";
        return c.StartsWith("Culture.", StringComparison.Ordinal) ? c.Substring("Culture.".Length) : c;
    }

    private static Dictionary<string, (XElement Last, string FirstModule)> BuildMerged()
    {
        var game = GameDir();
        if (game == null)
            Assert.Inconclusive("The Bannerlord SandBoxCore and CustomBattle modules are not installed here (set BANNERLORD_GAME_DIR).");

        var modules = new (string Name, string Dir)[]
        {
            ("SandBoxCore", Path.Combine(game!, "Modules", "SandBoxCore")),
            ("CustomBattle", Path.Combine(game!, "Modules", "CustomBattle")),
            ("TAOM", RepoPaths.RepoPath("Main", "_Module")),
        };

        var merged = new XDocument(new XElement("NPCCharacters"));
        foreach (var (name, dir) in modules)
            foreach (var path in CustomGameRegistrations(dir))
            {
                var xslt = Path.Combine(dir, "ModuleData", path + ".xslt");
                if (File.Exists(xslt))
                    merged = Transform(merged, xslt);
                var xml = Path.Combine(dir, "ModuleData", path + ".xml");
                if (!File.Exists(xml))
                    continue;
                foreach (var e in LoadDocument(xml).Root!.Elements("NPCCharacter"))
                {
                    var copy = new XElement(e);
                    copy.SetAttributeValue("__module", name);
                    merged.Root!.Add(copy);
                }
            }

        var result = new Dictionary<string, (XElement Last, string FirstModule)>(StringComparer.Ordinal);
        foreach (var e in merged.Root!.Elements("NPCCharacter"))
        {
            var id = (string?)e.Attribute("id");
            if (string.IsNullOrEmpty(id))
                continue;
            var first = result.TryGetValue(id!, out var seen) ? seen.FirstModule : (string?)e.Attribute("__module") ?? "";
            result[id!] = (e, first);
        }

        return result;
    }

    private static IEnumerable<string> CustomGameRegistrations(string moduleDir)
    {
        var sub = LoadDocument(Path.Combine(moduleDir, "SubModule.xml"));
        foreach (var node in sub.Descendants("XmlNode"))
        {
            var name = node.Element("XmlName");
            if ((string?)name?.Attribute("id") != "NPCCharacters")
                continue;
            var types = node.Descendants("GameType").Select(t => (string?)t.Attribute("value")).ToList();
            if (types.Count == 0 || types.Contains("CustomGame"))
                yield return (string)name!.Attribute("path")!;
        }
    }

    private static string? GameDir()
    {
        foreach (var root in new[] { Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR"), DefaultGameDir })
        {
            if (!string.IsNullOrEmpty(root)
                && File.Exists(Path.Combine(root, "Modules", "SandBoxCore", "ModuleData", "spnpccharacters.xml"))
                && File.Exists(Path.Combine(root, "Modules", "CustomBattle", "SubModule.xml")))
                return root;
        }

        return null;
    }

    private static XDocument LoadDocument(string path)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null, IgnoreWhitespace = true };
        using var reader = XmlReader.Create(path, settings);
        return XDocument.Load(reader);
    }

    private static XDocument Transform(XDocument source, string stylesheet)
    {
        var xslt = CompiledXslt.Get(stylesheet, XsltLoadStyle.IgnoreDtdNoResolver);

        using var input = source.CreateReader();
        using var buffer = new StringWriter();
        using (var output = XmlWriter.Create(buffer, new XmlWriterSettings { ConformanceLevel = ConformanceLevel.Auto }))
            xslt.Transform(input, output);
        return XDocument.Parse(buffer.ToString());
    }
}
