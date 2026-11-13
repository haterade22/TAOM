using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Core;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Diplomacy;

/// <summary>
/// Kingdom.Deserialize declares war for any relationship whose value is below zero, whatever its
/// isAtWar says (installed Kingdom.cs:780-791), and Clan.Deserialize does the same for a clan's
/// relationship row (Clan.cs ~950-960). The War of the Ring rules a war out until its phase.
/// So the shipped kingdom and clan data may never open a campaign with a war: no negative value, no isAtWar="true".
/// </summary>
[TestClass]
public class ShippedKingdomDataTests
{
    private const string DefaultGameDir = @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord";

    private static IEnumerable<string> WarRows(string xml) =>
        XDocument.Parse(xml).Descendants().Where(e => e.Name.LocalName == "relationship")
            .Where(e => IsWar(e))
            .Select(e => $"kingdom={(string)e.Attribute("kingdom")} value={(string)e.Attribute("value")} "
                         + $"isAtWar={(string)e.Attribute("isAtWar")}");

    private static bool IsWar(XElement row)
    {
        var atWar = string.Equals((string)row.Attribute("isAtWar"), "true", System.StringComparison.OrdinalIgnoreCase);
        var raw = (string)row.Attribute("value");
        var negative = double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value < 0;
        return atWar || negative;
    }

    [DataTestMethod]
    [DataRow("Main/_Module/ModuleData/spkingdoms.xslt")]
    [DataRow("Main/_Module/ModuleData/taom_spkingdoms.xml")]
    [DataRow("Main/_Module/ModuleData/characters/clans.xml")]
    [DataRow("Main/_Module/ModuleData/spclans.xslt")]
    public void ShippedKingdomData_NoRelationshipRowDeclaresWar(string file)
    {
        var rows = WarRows(RepoPaths.ReadSource(file)).ToList();

        Assert.AreEqual(0, rows.Count,
            file + " opens the campaign with a war (negative value or isAtWar=true):\n  " + string.Join("\n  ", rows));
    }

    // The named files above are the ones that carry rows today; this sweep covers a file added later.
    // A file that does not parse is skipped: the engine would reject it long before a war row mattered.
    [TestMethod]
    public void ShippedModuleData_NoFileDeclaresWarInARelationshipRow()
    {
        var dir = RepoPaths.RepoPath("Main", "_Module", "ModuleData");
        var files = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                        || f.EndsWith(".xslt", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.IsTrue(files.Count > 0, "no ModuleData files found under " + dir);

        var offenders = new List<string>();
        var rowsSeen = 0;
        foreach (var file in files)
        {
            XDocument doc;
            try { doc = XDocument.Parse(File.ReadAllText(file)); }
            catch (XmlException) { continue; }

            var rows = doc.Descendants().Where(e => e.Name.LocalName == "relationship").ToList();
            rowsSeen += rows.Count;
            offenders.AddRange(rows.Where(IsWar).Select(e =>
                $"{file.Substring(dir.Length + 1)}: kingdom={(string)e.Attribute("kingdom")} "
                + $"value={(string)e.Attribute("value")} isAtWar={(string)e.Attribute("isAtWar")}"));
        }

        Assert.IsTrue(rowsSeen > 0, "the sweep saw no relationship row at all: it tested nothing");
        Assert.AreEqual(0, offenders.Count,
            "ModuleData opens the campaign with a war (negative value or isAtWar=true):\n  "
            + string.Join("\n  ", offenders));
    }

    [DataTestMethod]
    [DataRow("<k><relationship kingdom=\"Kingdom.a\" value=\"-1\" isAtWar=\"false\" /></k>")]
    [DataRow("<k><relationship kingdom=\"Kingdom.a\" value=\"0\" isAtWar=\"true\" /></k>")]
    [DataRow("<k><relationship kingdom=\"Kingdom.a\" value=\"-0.5\" /></k>")]
    public void WarRows_DeclaringRow_IsFound(string xml)
    {
        Assert.AreEqual(1, WarRows(xml).Count());
    }

    [TestMethod]
    public void WarRows_PeacefulRow_IsIgnored()
    {
        Assert.AreEqual(0,
            WarRows("<k><relationship kingdom=\"Kingdom.a\" value=\"2\" isAtWar=\"false\" /></k>").Count());
    }

    // The source check above reads the stylesheet's own markup, which cannot see a war row that
    // arrives from vanilla's file and passes through the identity template. This runs the real
    // stylesheet over the installed SandBox file and reads the OUTPUT, the way MBObjectManager.ApplyXslt
    // builds it.
    [TestMethod]
    [TestCategory("LiveInstall")]
    public void SpkingdomsXslt_InstalledSandBoxKingdoms_OutputHasNoWarRow()
    {
        var root = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        var file = new[] { root, DefaultGameDir }
            .Where(r => !string.IsNullOrEmpty(r))
            .Select(r => Path.Combine(r!, "Modules", "SandBox", "ModuleData", "spkingdoms.xml"))
            .FirstOrDefault(File.Exists);
        if (file == null)
            Assert.Inconclusive("SandBox spkingdoms.xml not found; needs the game install.");

        var before = WarRows(File.ReadAllText(file!)).Count();
        Assert.IsTrue(before > 0, "the vanilla file has no war rows: the check tested nothing");

        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
        var xslt = CompiledXslt.Get(Path.Combine(CultureDataFixture.ModuleDataPath(), "spkingdoms.xslt"), XsltLoadStyle.IgnoreDtdNoResolver);

        var result = new XDocument();
        using (var input = XmlReader.Create(file!, settings))
        using (var output = result.CreateWriter())
            xslt.Transform(input, output);

        var rows = WarRows(result.ToString()).ToList();
        Assert.AreEqual(0, rows.Count,
            $"{rows.Count} of {before} vanilla war rows survive spkingdoms.xslt:\n  " + string.Join("\n  ", rows));
    }
}
