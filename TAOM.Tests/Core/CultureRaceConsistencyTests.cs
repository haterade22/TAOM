using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// Every race a culture's lords actually use must be a race that culture is allowed to offer.
///
/// Why this exists: Isengard shipped with `cultures.json` allowing only uruk_hai, berserker and
/// human, while `lord_I2_3` (Sharku) was race="uruk". It was the only culture in the game with a
/// lord race outside its own allowed list, and nothing anywhere noticed. It surfaced only because a
/// player reported the character-creation preview behaving oddly for that one culture, and it took a
/// diagnostic build and a log to find.
///
/// The mismatch matters beyond tidiness. `cultures.json` races drive the character-creation race
/// filter (FaceGenRaceSelectorRebuilder), and races[0] is the default the filter forces on first
/// apply. A lord whose race is absent from that list is a race the player can be shown but never
/// select, and the two halves of the data disagree about what the culture IS.
///
/// Data-only check, so it needs no game and runs in milliseconds.
/// </summary>
[TestClass]
public class CultureRaceConsistencyTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new FileNotFoundException("TAOM.sln not found walking upward from cwd");
    }

    /// <summary>culture_id -> allowed races, parsed straight from the shipped JSON.</summary>
    private static Dictionary<string, List<string>> AllowedRaces(string root)
    {
        var path = Path.Combine(root, "Main", "_Module", "ModuleData", "charactercreation", "cultures.json");
        Assert.IsTrue(File.Exists(path), $"cultures.json not found at {path}");

        var text = File.ReadAllText(path);
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        // Deliberately regex rather than a JSON dependency: the test project has no JSON library
        // reference and the shape here is stable and simple.
        foreach (Match m in Regex.Matches(
            text,
            @"""culture_id""\s*:\s*""(?<id>[^""]+)""\s*,\s*""races""\s*:\s*\[(?<races>[^\]]*)\]",
            RegexOptions.Singleline))
        {
            var races = Regex.Matches(m.Groups["races"].Value, @"""([^""]+)""")
                .Cast<Match>().Select(r => r.Groups[1].Value).ToList();
            result[m.Groups["id"].Value] = races;
        }

        Assert.IsTrue(result.Count > 0, "parsed no cultures from cultures.json; the file shape changed");
        return result;
    }

    /// <summary>culture_id -> the distinct races its lords actually declare.</summary>
    private static Dictionary<string, HashSet<string>> LordRaces(string root)
    {
        var path = Path.Combine(root, "Main", "_Module", "ModuleData", "characters", "lords.xml");
        Assert.IsTrue(File.Exists(path), $"lords.xml not found at {path}");

        var text = File.ReadAllText(path);
        var used = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (Match m in Regex.Matches(
            text,
            @"<NPCCharacter\b[^>]*?race=""(?<race>[a-z_]+)""[^>]*?culture=""Culture\.(?<culture>[a-z_0-9]+)""",
            RegexOptions.Singleline))
        {
            var culture = m.Groups["culture"].Value;
            if (!used.TryGetValue(culture, out var set))
                used[culture] = set = new HashSet<string>(StringComparer.Ordinal);
            set.Add(m.Groups["race"].Value);
        }

        Assert.IsTrue(used.Count > 0, "parsed no lords from lords.xml; the file shape changed");
        return used;
    }

    [TestMethod]
    public void EveryLordRaceIsOfferedByItsOwnCulture()
    {
        var root = FindRepoRoot();
        var allowed = AllowedRaces(root);
        var used = LordRaces(root);

        var problems = new List<string>();

        foreach (var pair in used.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            // Only cultures that declare a character-creation race list are in scope. A culture
            // absent from cultures.json is not player-selectable, so it has no list to violate.
            if (!allowed.TryGetValue(pair.Key, out var permitted))
                continue;

            var strays = pair.Value.Where(r => !permitted.Contains(r)).OrderBy(r => r, StringComparer.Ordinal).ToArray();
            if (strays.Length > 0)
                problems.Add($"{pair.Key}: lords use [{string.Join(", ", strays)}] " +
                             $"but cultures.json allows only [{string.Join(", ", permitted)}]");
        }

        Assert.AreEqual(0, problems.Count,
            "A culture's lords use a race that culture cannot offer at character creation. " +
            "Either add the race to cultures.json or change the lord.\n  " +
            string.Join("\n  ", problems));
    }

    /// <summary>
    /// Race ids the installed skins.xml files register. They live in the Armory, a game-install
    /// path, so a caller skips (Inconclusive) rather than fails when the install is not present.
    /// </summary>
    private static HashSet<string> RegisteredRaces()
    {
        var skins = new[]
        {
            @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData\skins.xml",
            @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\ModuleData\skins.xml",
        }.Where(File.Exists).ToArray();

        if (skins.Length == 0)
            Assert.Inconclusive("Bannerlord install not found; race registration cannot be checked here.");

        var registered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in skins)
            foreach (Match m in Regex.Matches(File.ReadAllText(f), @"<race\b[^>]*?id\s*=\s*""([A-Za-z_0-9]+)""", RegexOptions.Singleline))
                registered.Add(m.Groups[1].Value);

        Assert.IsTrue(registered.Count > 0, "parsed no races from skins.xml; the file shape changed");
        return registered;
    }

    [TestMethod]
    public void EveryCharacterRaceIsARealRegisteredRace()
    {
        // A character's race name reaches FaceGen.GetRaceOrDefault from BasicCharacterObject
        // .Deserialize on every campaign start and load, lords, troops, notables and wanderers
        // alike, and despite its name that is a plain dictionary index (v1.5.3
        // TaleWorlds.MountAndBlade FaceGen.cs:115-118): a name no skins.xml registers throws
        // KeyNotFoundException out of the NPCCharacters load. The races come from the unversioned
        // Armory, so a missing one is a broken install, not a typo anyone would see in review
        // (#644 put nazghul on the Nine).
        var registered = RegisteredRaces();
        var moduleData = Path.Combine(FindRepoRoot(), "Main", "_Module", "ModuleData");

        var stylesheets = Directory.EnumerateFiles(moduleData, "*.xslt", SearchOption.AllDirectories)
            .Select(f => (source: Path.GetFileName(f), scan: RacesEmittedByXslt(File.ReadAllText(f))))
            .ToArray();

        var used = Directory.EnumerateFiles(moduleData, "*.xml", SearchOption.AllDirectories)
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"<NPCCharacter\b[^>]*?\brace=""([^""]+)""")
                .Cast<Match>().Select(m => (source: Path.GetFileName(f), race: m.Groups[1].Value)))
            .Concat(stylesheets.SelectMany(s => s.scan.Races.Select(race => (s.source, race))))
            .ToArray();

        Assert.IsTrue(used.Any(u => u.source == "lords.xml"), "parsed no race from characters/lords.xml; the file shape changed");
        Assert.IsTrue(used.Any(u => u.source == "lords.xslt"), "parsed no race from lords.xslt; the file shape changed");

        var computed = stylesheets.SelectMany(s => s.scan.Unverifiable.Select(u => $"{s.source}: {u}")).ToArray();
        Assert.AreEqual(0, computed.Length,
            "An XSLT computes a race at transform time, which this scan cannot read; spell it as literal text, "
            + "or extend the gate to check that stylesheet's transform output: "
            + string.Join(", ", computed));

        var unknown = used.Where(u => !registered.Contains(u.race))
            .Select(u => $"{u.source} -> '{u.race}'")
            .Distinct()
            .ToArray();

        Assert.AreEqual(0, unknown.Length,
            "A character declares a race no skins.xml registers; loading NPCCharacters will throw: "
            + string.Join(", ", unknown));
    }

    private static readonly XNamespace Xsl = "http://www.w3.org/1999/XSL/Transform";

    /// <summary>
    /// Every race the stylesheet itself spells, read from its structure rather than one spelling
    /// (#644; Codex review 2026-09-23, O2): the text of an <c>xsl:attribute name="race"</c>, and the
    /// <c>race</c> attribute of a literal <c>NPCCharacter</c>, both verbatim, since the transform
    /// emits padding as written and the engine indexes the name exactly. A race the transform
    /// computes (an <c>xsl:attribute</c> holding instructions, or an attribute value template) comes
    /// back as unverifiable and the gate fails on it. Not seen at all: a race copied from the source
    /// (<c>xsl:copy</c>, <c>xsl:copy-of</c>; vanilla <c>lords.xml</c> carries none), an
    /// <c>xsl:attribute</c> whose name is computed, and a stylesheet imported from outside
    /// ModuleData. None ships.
    /// </summary>
    internal static (List<string> Races, List<string> Unverifiable) RacesEmittedByXslt(string xslt)
    {
        var doc = XDocument.Parse(xslt);
        var races = new List<string>();
        var unverifiable = new List<string>();

        foreach (var attribute in doc.Descendants(Xsl + "attribute").Where(a => (string?)a.Attribute("name") == "race"))
        {
            if (attribute.Elements().All(e => e.Name == Xsl + "text"))
                races.Add(attribute.Value);
            else
                unverifiable.Add(attribute.ToString(SaveOptions.DisableFormatting));
        }

        foreach (var literal in doc.Descendants().Where(e => e.Name.LocalName == "NPCCharacter" && e.Name.Namespace != Xsl))
        {
            var race = (string?)literal.Attribute("race");
            if (race == null)
                continue;

            if (race.Contains("{"))
                unverifiable.Add($"<NPCCharacter race=\"{race}\">");
            else
                races.Add(race);
        }

        return (races, unverifiable);
    }

    private static string Stylesheet(string body) =>
        @"<xsl:stylesheet version=""1.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">"
        + @"<xsl:template match=""NPCCharacter[@id='probe']""><xsl:copy>" + body + "</xsl:copy></xsl:template>"
        + "</xsl:stylesheet>";

    [TestMethod]
    public void RacesEmittedByXslt_XslAttributeText_IsReadInEverySpelling()
    {
        var (races, unverifiable) = RacesEmittedByXslt(Stylesheet(
            @"<xsl:attribute name=""race"">nazghul</xsl:attribute>"
            + @"<xsl:attribute name='race'>sauron</xsl:attribute>"
            + @"<xsl:attribute name=""race""><xsl:text>uruk</xsl:text></xsl:attribute>"));

        CollectionAssert.AreEqual(new[] { "nazghul", "sauron", "uruk" }, races);
        Assert.AreEqual(0, unverifiable.Count);
    }

    [TestMethod]
    public void RacesEmittedByXslt_PaddedRace_IsReadVerbatim()
    {
        // The transform keeps text that is not whitespace-only as written, and the engine indexes
        // the name as given (v1.5.3 BasicCharacterObject.cs:327, FaceGen.cs:115-118), so a padded
        // race throws on load; the gate has to see the padding to fail on it.
        var (races, _) = RacesEmittedByXslt(Stylesheet(@"<xsl:attribute name=""race""> sauron </xsl:attribute>"));

        CollectionAssert.AreEqual(new[] { " sauron " }, races);
    }

    [TestMethod]
    public void RacesEmittedByXslt_LiteralResultElement_IsRead()
    {
        // Codex review 2026-09-23 (O2): the regex this scan used saw one spelling of
        // xsl:attribute, so a literal NPCCharacter in a stylesheet emitted an unregistered race
        // that no scan read.
        var (races, _) = RacesEmittedByXslt(Stylesheet(@"<NPCCharacter id=""review_probe"" race=""probe_race"" />"));

        CollectionAssert.AreEqual(new[] { "probe_race" }, races);
    }

    [TestMethod]
    public void RacesEmittedByXslt_RaceBuiltAtTransformTime_IsUnverifiable()
    {
        var (races, unverifiable) = RacesEmittedByXslt(Stylesheet(
            @"<xsl:attribute name=""race""><xsl:value-of select=""@race"" /></xsl:attribute>"
            + @"<NPCCharacter id=""review_probe"" race=""{@race}"" />"));

        Assert.AreEqual(0, races.Count);
        Assert.AreEqual(2, unverifiable.Count);
    }

    [TestMethod]
    public void EveryAllowedRaceIsARealRegisteredRace()
    {
        // Guards the other direction: a typo in cultures.json (say "uruk-hai" for "uruk_hai")
        // would silently produce a filter entry that matches no race.
        var registered = RegisteredRaces();

        var unknown = AllowedRaces(FindRepoRoot())
            .SelectMany(c => c.Value.Select(r => (culture: c.Key, race: r)))
            .Where(x => !registered.Contains(x.race))
            .Select(x => $"{x.culture} -> '{x.race}'")
            .Distinct()
            .ToArray();

        Assert.AreEqual(0, unknown.Length,
            "cultures.json names a race that no skins.xml registers, so the filter entry can never match: "
            + string.Join(", ", unknown));
    }
}
