using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// The female-with-beard invariant over the lord roster, in both lords files.
///
/// Why this exists: `lord_WE8_c` shipped as vanilla's female "Icratia" long after TAOM had renamed
/// him to Pelendur, son of Golasgil. The registry said Pelendur, all 12 language files said
/// Pelendur, the encyclopedia bio said "son of Golasgil", and his father and mother were wired in
/// `heroes.xslt` - but `characters/lords.xml` still carried the vanilla name and `is_female="true"`,
/// and that file is the one that wins at runtime. Because there is no English language folder, the
/// inline literal IS the English text, so English players saw a name nobody else in the world saw.
/// Thirteen other lords had drifted the same way. `lord_1_46_1` (Thorwen) had the mirror defect:
/// female everywhere in prose, `is_female="false"` with a beard in the data.
///
/// Neither `validate_moduledata.py` nor `LanguageFileCoverageTests` can see either problem. The
/// former has no rule touching names or sex; the latter checks that a key HAS a row, not what the
/// row says, so renaming under an existing key stays green.
///
/// The name half is LordIdentityConsistencyTests.EveryLordNameInLordsXsltMatchesTheRegistry. Since #648 no
/// characters/lords.xml row carries a name key that taom_xslt_strings.xml registers (those were the
/// vanilla-id rows), so the copy of that check over lords.xml compared no row and was removed.
///
/// Data-only, needs no game, runs in milliseconds.
/// </summary>
[TestClass]
public class LordNameAndSexConsistencyTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new FileNotFoundException("TAOM.sln not found walking upward from cwd");
    }

    private static string ReadModuleData(string root, params string[] parts)
    {
        var path = Path.Combine(new[] { root, "Main", "_Module", "ModuleData" }.Concat(parts).ToArray());
        Assert.IsTrue(File.Exists(path), $"not found at {path}");
        return File.ReadAllText(path);
    }

    [TestMethod]
    public void NoFemaleLordCarriesBeardTags()
    {
        var root = FindRepoRoot();
        var lords = ReadModuleData(root, "characters", "lords.xml");

        var offenders = new List<string>();
        foreach (Match m in Regex.Matches(lords, @"<NPCCharacter\b(?<attrs>[^>]*)>"))
        {
            var id = Regex.Match(m.Groups["attrs"].Value, @"\bid=""(?<v>[^""]*)""");
            if (!id.Success) continue;
            var female = Regex.Match(m.Groups["attrs"].Value, @"is_female=""(?<v>\w+)""");
            // Six entries use is_female="True"; the engine's bool parse tolerates either casing.
            if (!female.Success ||
                !string.Equals(female.Groups["v"].Value, "true", StringComparison.OrdinalIgnoreCase))
                continue;

            var close = lords.IndexOf("</NPCCharacter>", m.Index, StringComparison.Ordinal);
            if (close < 0) continue;
            if (lords.IndexOf("<beard_tags>", m.Index, close - m.Index, StringComparison.Ordinal) >= 0)
                offenders.Add("  " + id.Groups["v"].Value);
        }

        // A vanilla-id lord exists only as its lords.xslt template since #648, and reusing a vanilla male id
        // for a woman is how this shipped. The template's xsl:copy writes every attribute and the face itself.
        XNamespace xsl = "http://www.w3.org/1999/XSL/Transform";
        var templates = XDocument.Parse(ReadModuleData(root, "lords.xslt")).Descendants(xsl + "template")
            .Select(t => (Match: Regex.Match((string)t.Attribute("match") ?? "", @"^NPCCharacter\[@id='(?<id>[^']+)'\]$"),
                          Copy: t.Element(xsl + "copy")))
            .Where(t => t.Match.Success && t.Copy != null)
            .ToList();
        Assert.IsTrue(templates.Count > 300, $"only {templates.Count} lord templates found; the pattern no longer matches lords.xslt");
        var femaleTemplates = 0;
        foreach (var (match, copy) in templates)
        {
            var female = copy.Elements(xsl + "attribute").FirstOrDefault(a => (string)a.Attribute("name") == "is_female");
            if (female == null || !string.Equals(female.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase))
                continue;
            femaleTemplates++;
            if (copy.Descendants("beard_tags").Any())
                offenders.Add("  " + match.Groups["id"].Value + " (lords.xslt)");
        }
        Assert.IsTrue(femaleTemplates > 100, $"only {femaleTemplates} female lord templates found; the is_female reader no longer matches lords.xslt");

        Assert.AreEqual(0, offenders.Count,
            "These lords are is_female=\"true\" but still carry a <beard_tags> block, which is what\n" +
            "actually renders facial hair. This is the leftover you get when a vanilla male id is\n" +
            "reused for a TAOM woman (lord_1_46_1 Thorwen shipped this way). Delete the block.\n" +
            string.Join("\n", offenders));
    }
}
