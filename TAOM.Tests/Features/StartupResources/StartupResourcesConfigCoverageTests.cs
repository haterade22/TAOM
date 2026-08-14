using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TAOM.Tests.Features.StartupResources;

/// <summary>
/// Pins the SHIPPED startup_resources_config.xml against the shipped lord roster.
///
/// Why this class exists: a culture absent from the config is documented as a silent default of 0,
/// and nothing logs it. Goblin-town and Moria shipped CC-selectable in June 2026 with no row at all,
/// so every lord of those cultures started on zero denars for two months and no test, validator or
/// log line noticed. Blue Craig and Lindon repeated it in August when they were promoted out of
/// their borrowed cultures. Three occurrences of one defect, all invisible.
///
/// These assert the RELATIONSHIP between two shipped files, never the tuned numbers themselves.
/// Retuning gold or influence keeps them green with no test edit; adding a culture to
/// characters/lords.xml without adding its config row does not.
/// </summary>
[TestClass]
public class StartupResourcesConfigCoverageTests
{
    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));

    private static string ConfigPath => Path.Combine(
        RepoRoot, @"Main\_Module\ModuleData\startup_resources\startup_resources_config.xml");

    private static string LordsPath => Path.Combine(
        RepoRoot, @"Main\_Module\ModuleData\characters\lords.xml");

    private static string LordsXsltPath => Path.Combine(
        RepoRoot, @"Main\_Module\ModuleData\lords.xslt");

    [TestMethod]
    public void ShippedConfig_EveryLordOwningCulture_HasARow()
    {
        var configured = LoadConfiguredCultures(XDocument.Load(ConfigPath));
        var lordCultures = LoadLordCultures();

        Assert.IsTrue(lordCultures.Count > 0, "Parsed no lord cultures — the lords.xml layout changed.");

        var missing = lordCultures.Where(c => !configured.ContainsKey(c)).OrderBy(c => c).ToList();

        Assert.AreEqual(0, missing.Count,
            $"These cultures own Lord heroes but have no <Culture> row in startup_resources_config.xml, " +
            $"so every one of their lords starts on 0 denars and nothing logs it: {string.Join(", ", missing)}." +
            "\n\nFix by adding a row to Main/_Module/ModuleData/startup_resources/startup_resources_config.xml. " +
            "Derive the gold value with the measurement documented in that file's header rather than " +
            "copying a neighbour's number.");
    }

    [TestMethod]
    public void ShippedConfig_EveryLordOwningCulture_GrantsPositiveGoldAndInfluence()
    {
        var configured = LoadConfiguredCultures(XDocument.Load(ConfigPath));
        var lordCultures = LoadLordCultures();

        var dead = lordCultures
            .Where(configured.ContainsKey)
            .Where(c => configured[c].Gold <= 0 || configured[c].Influence <= 0f)
            .OrderBy(c => c)
            .ToList();

        Assert.AreEqual(0, dead.Count,
            "A row that exists but grants 0 is the same silent no-op as a missing row — " +
            $"StartupGoldService and StartupInfluenceService both skip on a non-positive value: {string.Join(", ", dead)}." +
            "\n\nIf a culture is meant to receive nothing, say so in a comment next to the row so the " +
            "next reader can tell intent from oversight.");
    }

    [TestMethod]
    public void ShippedConfig_CultureIds_AreUnique()
    {
        var ids = XDocument.Load(ConfigPath).Root!
            .Elements("Culture")
            .Select(e => (string?)e.Attribute("id") ?? string.Empty)
            .ToList();

        var duplicates = ids.GroupBy(i => i, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.AreEqual(0, duplicates.Count,
            "StartupResourcesConfigProvider builds a dictionary keyed on culture id, so a duplicate " +
            $"row silently wins by file order and the other is dead markup: {string.Join(", ", duplicates)}.");
    }

    [TestMethod]
    public void MissingCultureDetection_ConfigOmitsALordCulture_IsReported()
    {
        // RED guard for the first test: proves the comparison actually catches an omission rather
        // than passing because both sides parsed empty.
        var config = XDocument.Parse(
            "<StartupResources>" +
            "  <Culture id=\"gondor\" gold=\"85000\" influence=\"600\" />" +
            "</StartupResources>");

        var configured = LoadConfiguredCultures(config);
        var lordCultures = new List<string> { "gondor", "mirkwood" };

        var missing = lordCultures.Where(c => !configured.ContainsKey(c)).ToList();

        CollectionAssert.AreEqual(new[] { "mirkwood" }, missing);
    }

    private static Dictionary<string, (int Gold, float Influence)> LoadConfiguredCultures(XDocument doc)
    {
        var result = new Dictionary<string, (int, float)>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in doc.Root!.Elements("Culture"))
        {
            var id = (string?)e.Attribute("id");
            if (string.IsNullOrWhiteSpace(id))
                continue;

            int.TryParse((string?)e.Attribute("gold"), out int gold);
            float.TryParse((string?)e.Attribute("influence"), out float influence);
            result[id!] = (gold, influence);
        }
        return result;
    }

    /// <summary>
    /// Distinct cultures that carry lords at runtime, from BOTH shipped sources:
    /// <list type="number">
    /// <item><c>occupation="Lord"</c> characters in <c>characters/lords.xml</c>.</item>
    /// <item>Cultures that <c>lords.xslt</c> literally assigns when it retags vanilla's lords.</item>
    /// </list>
    /// The second source matters even though it adds nothing today. Scanning only the repo file
    /// happened to give the right answer because every XSLT-assigned culture also appears there,
    /// which is a coincidence of the current data: a future XSLT-only retag onto a new culture
    /// would carry lords at runtime, receive no startup gold, and this test would still pass.
    /// Both sources are repo files, so the test needs no game install.
    /// </summary>
    private static List<string> LoadLordCultures()
    {
        var fromLordsXml = XDocument.Load(LordsPath)
            .Descendants("NPCCharacter")
            .Where(e => string.Equals((string?)e.Attribute("occupation"), "Lord", StringComparison.OrdinalIgnoreCase))
            .Select(e => ((string?)e.Attribute("culture") ?? string.Empty).Replace("Culture.", string.Empty));

        return fromLordsXml
            .Concat(LoadXsltAssignedCultures())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Cultures emitted by <c>&lt;xsl:attribute name="culture"&gt;Culture.X&lt;/xsl:attribute&gt;</c>
    /// in lords.xslt. Read as text rather than transformed: running the transform would need the
    /// installed vanilla lords.xml, and the literal values are what the stylesheet can ever assign.
    /// </summary>
    private static IEnumerable<string> LoadXsltAssignedCultures()
    {
        if (!File.Exists(LordsXsltPath))
            return Enumerable.Empty<string>();

        return Regex.Matches(File.ReadAllText(LordsXsltPath),
                             @"name=""culture""\s*>\s*Culture\.([A-Za-z0-9_]+)\s*<")
            .Cast<Match>()
            .Select(m => m.Groups[1].Value);
    }
}
