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
/// repo), each stylesheet applied to everything merged before it, then its XML merged in. A later copy of an id goes
/// into the first one through MergeElementAttributes (MBObjectManager.cs:799-818, SetAttributeValue per attribute), so
/// an attribute the later copy lacks survives; here a later copy's child elements are added when the first has no
/// identical one, an approximation of the engine's merge by unique key. It keeps both copies of a merged child, so
/// NoOfferedCharacter_ReferencesAHeroTheFilterHides can over-report a reference but never miss one.
/// The old screen's troop picker offers a character when it is a non-obsolete Soldier (ArmyCompositionGroupVM:176);
/// the muster also requires CaptainResolver.IsOffered (TcbVM:314). The muster's commander list is every non-obsolete,
/// non-spc_ hero of the culture (TcbVM:319). An engine update that adds or renames a vanilla soldier, dummy or
/// commander id fails here.
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

    private const string FilterPath = "custom_battle/custom_battle_harad_troop_filter";

    private static readonly Lazy<MergedSet> Loaded = new(BuildMerged);

    private static Dictionary<string, (XElement Element, string FirstModule)> Merged => Loaded.Value.Characters;

    private sealed class MergedSet
    {
        public Dictionary<string, (XElement Element, string FirstModule)> Characters { get; } = new(StringComparer.Ordinal);

        // Ids the filter stylesheet made obsolete: obsolete after it ran, not before.
        public HashSet<string> HiddenByFilter { get; } = new(StringComparer.Ordinal);

        // The module whose file first defined an id. Tracked beside the document, not in an attribute: lords.xslt
        // rebuilds the custom_battle_lords.xml stubs and would drop the marker.
        public Dictionary<string, string> FirstModule { get; } = new(StringComparer.Ordinal);
    }

    [TestMethod]
    public void VanillaSoldiersOfDunlandDaleRohanRhun_AreNotOffered()
    {
        var offered = Merged
            .Where(kv => kv.Value.FirstModule != "TAOM" && IsOfferedSoldier(kv.Value.Element)
                         && HiddenVanillaLineCultures.Contains(CultureOf(kv.Value.Element)))
            .Select(kv => kv.Key).OrderBy(id => id, StringComparer.Ordinal).ToList();

        Assert.AreEqual(0, offered.Count, "vanilla soldiers still offered: " + string.Join(", ", offered));
    }

    [TestMethod]
    public void TrainingDummiesAndPracticeOpponents_AreNotOffered()
    {
        var offered = Merged
            .Where(kv => DummyPrefixes.Any(p => kv.Key.StartsWith(p, StringComparison.Ordinal)) && IsOfferedSoldier(kv.Value.Element))
            .Select(kv => kv.Key).OrderBy(id => id, StringComparer.Ordinal).ToList();

        Assert.AreEqual(0, offered.Count, "dummies still offered: " + string.Join(", ", offered));
    }

    // Every culture has TAOM lords in Custom Battle (characters/custom_battle_lords.xml, rebuilt by lords.xslt), Dunland
    // and Khand included, so no hero that vanilla's own modules define is offered as a commander in a culture the
    // faction list shows.
    [TestMethod]
    public void NoVanillaHero_IsOfferedAsACommander()
    {
        var cultures = CultureDataFixture.MainCultureIds();
        var offered = Merged
            .Where(kv => kv.Value.FirstModule != "TAOM" && IsOfferedHero(kv.Key, kv.Value.Element)
                         && cultures.Contains(CultureOf(kv.Value.Element)))
            .Select(kv => kv.Key + " (" + CultureOf(kv.Value.Element) + ")").OrderBy(s => s, StringComparer.Ordinal).ToList();

        Assert.AreEqual(0, offered.Count, "vanilla heroes still offered as commanders: " + string.Join(", ", offered));
    }

    // What the commander rule's old Dunland and Khand exemption protected: a culture left with no hero of its own, or
    // with soldiers that are all hidden, has an empty list in the muster. A culture that defines no soldier at all
    // (Abanissa today) is a data gap this filter did not cause, so it is not counted.
    [TestMethod]
    public void EveryOfferedCulture_KeepsACommanderAndTroops()
    {
        var cultures = CultureDataFixture.MainCultureIds();
        Assert.IsTrue(cultures.Count > 0, "the playable culture set is empty");
        var noHero = cultures.Where(c => !Merged.Any(kv => IsOfferedHero(kv.Key, kv.Value.Element) && CultureOf(kv.Value.Element) == c))
            .OrderBy(c => c, StringComparer.Ordinal).ToList();
        var emptied = cultures
            .Where(c => Merged.Any(kv => IsSoldier(kv.Value.Element) && CultureOf(kv.Value.Element) == c)
                        && !Merged.Any(kv => IsOfferedSoldier(kv.Value.Element) && CultureOf(kv.Value.Element) == c))
            .OrderBy(c => c, StringComparer.Ordinal).ToList();

        Assert.AreEqual(0, noHero.Count, "cultures with no commander: " + string.Join(", ", noHero));
        Assert.AreEqual(0, emptied.Count, "cultures whose every soldier is hidden: " + string.Join(", ", emptied));
    }

    // Hiding is by flag, so a hidden id still resolves; but nothing the player can field should point at a hidden
    // hero (a commander, the Dunland trader). Troops the Harad rule hides are referenced by id as equipment templates,
    // which is harmless, so only heroes are counted.
    [TestMethod]
    public void NoOfferedCharacter_ReferencesAHeroTheFilterHides()
    {
        var hidden = Loaded.Value.HiddenByFilter
            .Where(id => Merged.TryGetValue(id, out var v) && (string?)v.Element.Attribute("is_hero") == "true").ToHashSet();
        Assert.IsTrue(hidden.Count > 0, "the filter hid no hero: the scan is empty");
        var dangling = new List<string>();
        foreach (var kv in Merged)
        {
            var id = kv.Key;
            var element = kv.Value.Element;
            if ((string?)element.Attribute("is_obsolete") == "true")
                continue;
            foreach (var value in element.DescendantsAndSelf().SelectMany(e => e.Attributes()).Select(a => a.Value))
            {
                var target = value.StartsWith("NPCCharacter.", StringComparison.Ordinal) ? value.Substring("NPCCharacter.".Length) : value;
                if (target != id && hidden.Contains(target))
                    dangling.Add(id + " -> " + target);
            }
        }

        Assert.AreEqual(0, dangling.Count, "offered characters that reference a hidden one: " + string.Join(", ", dangling.OrderBy(s => s, StringComparer.Ordinal)));
    }

    // TAOM has no Khand soldiers, so the vanilla Battanian line stays, with archers and cavalry: no rule names
    // battanian_ or wolfskins_.
    [TestMethod]
    public void Khand_StillOffersInfantryArchersAndCavalry()
    {
        var groups = Merged.Values
            .Where(v => IsOfferedSoldier(v.Element) && CultureOf(v.Element) == "battania")
            .Select(v => (string?)v.Element.Attribute("default_group")).Distinct().ToList();

        CollectionAssert.IsSubsetOf(new[] { "Infantry", "Ranged", "Cavalry" }, groups);
    }

    // Two-way check on the TAOM side: the new rules hide no TAOM soldier that is not a dummy or opponent.
    [TestMethod]
    public void EveryTaomSoldierOutsideTheOlderRules_IsStillOffered()
    {
        string[] olderRulePrefixes = { "legion_of_the_betrayed_tier_", "company_of_the_boar_tier_", "eleftheroi_tier_", "brotherhood_of_woods_tier_" };
        var hidden = Merged
            .Where(kv => kv.Value.FirstModule == "TAOM" && (string?)kv.Value.Element.Attribute("occupation") == "Soldier"
                         && (string?)kv.Value.Element.Attribute("is_hero") != "true"
                         && (string?)kv.Value.Element.Attribute("is_obsolete") == "true"
                         && !DummyPrefixes.Any(p => kv.Key.StartsWith(p, StringComparison.Ordinal))
                         && !olderRulePrefixes.Any(p => kv.Key.StartsWith(p, StringComparison.Ordinal))
                         && CultureOf(kv.Value.Element) is not ("aserai" or "shaghana"))
            .Select(kv => kv.Key).ToList();

        // fords_troops (all obsolete by design, loads after the filter) is the one file allowed here.
        var fords = LoadDocument(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "fords_of_war", "fords_troops.xml"))
            .Descendants("NPCCharacter").Select(e => (string?)e.Attribute("id")).ToHashSet(StringComparer.Ordinal);
        var unexpected = hidden.Where(id => !fords.Contains(id)).OrderBy(id => id, StringComparer.Ordinal).ToList();

        Assert.AreEqual(0, unexpected.Count, "TAOM soldiers hidden: " + string.Join(", ", unexpected));
    }

    private static bool IsSoldier(XElement e) => (string?)e.Attribute("occupation") == "Soldier";

    private static bool IsOfferedSoldier(XElement e) => IsSoldier(e) && (string?)e.Attribute("is_obsolete") != "true";

    // The muster's commander list (TcbVM.cs:319): a non-obsolete hero whose id does not start with spc_.
    private static bool IsOfferedHero(string id, XElement e) =>
        (string?)e.Attribute("is_hero") == "true" && (string?)e.Attribute("is_obsolete") != "true"
        && !id.StartsWith("spc_", StringComparison.Ordinal);

    private static string CultureOf(XElement e)
    {
        var c = (string?)e.Attribute("culture") ?? "";
        return c.StartsWith("Culture.", StringComparison.Ordinal) ? c.Substring("Culture.".Length) : c;
    }

    private static MergedSet BuildMerged()
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

        var result = new MergedSet();
        var merged = new XDocument(new XElement("NPCCharacters"));
        foreach (var (name, dir) in modules)
            foreach (var path in CustomGameRegistrations(dir))
            {
                var xslt = Path.Combine(dir, "ModuleData", path + ".xslt");
                if (File.Exists(xslt))
                {
                    var before = path == FilterPath ? ObsoleteIds(merged) : null;
                    merged = Transform(merged, xslt);
                    if (before != null)
                        result.HiddenByFilter.UnionWith(ObsoleteIds(merged).Where(id => !before.Contains(id)));
                }

                var xml = Path.Combine(dir, "ModuleData", path + ".xml");
                if (!File.Exists(xml))
                    continue;
                var byId = new Dictionary<string, XElement>(StringComparer.Ordinal);
                foreach (var e in merged.Root!.Elements("NPCCharacter"))
                    if (!string.IsNullOrEmpty((string?)e.Attribute("id")))
                        byId[(string)e.Attribute("id")!] = e;
                foreach (var e in LoadDocument(xml).Root!.Elements("NPCCharacter"))
                {
                    var id = (string?)e.Attribute("id");
                    if (!string.IsNullOrEmpty(id) && byId.TryGetValue(id!, out var existing))
                    {
                        foreach (var a in e.Attributes())
                            existing.SetAttributeValue(a.Name, a.Value);
                        foreach (var child in e.Elements().Where(c => !existing.Elements().Any(x => XNode.DeepEquals(x, c))).ToList())
                            existing.Add(new XElement(child));
                        continue;
                    }

                    var copy = new XElement(e);
                    merged.Root!.Add(copy);
                    if (!string.IsNullOrEmpty(id))
                    {
                        byId[id!] = copy;
                        result.FirstModule[id!] = name;
                    }
                }
            }

        foreach (var e in merged.Root!.Elements("NPCCharacter"))
        {
            var id = (string?)e.Attribute("id");
            if (!string.IsNullOrEmpty(id))
                result.Characters[id!] = (e, result.FirstModule.TryGetValue(id!, out var module) ? module : "");
        }

        return result;
    }

    private static HashSet<string> ObsoleteIds(XDocument doc) =>
        doc.Root!.Elements("NPCCharacter").Where(e => (string?)e.Attribute("is_obsolete") == "true")
            .Select(e => (string?)e.Attribute("id") ?? "").ToHashSet(StringComparer.Ordinal);

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
