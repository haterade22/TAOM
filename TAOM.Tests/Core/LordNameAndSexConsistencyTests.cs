using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// Two invariants over the lord roster that nothing else in the repo checks.
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
/// Data-only, needs no game, runs in milliseconds.
/// </summary>
[TestClass]
public class LordNameAndSexConsistencyTests
{
    /// <summary>
    /// Name keys where lords.xml deliberately differs from the registry.
    /// Keep this list tiny and justify every entry.
    /// </summary>
    private static readonly HashSet<string> AcceptedNameDifferences = new(StringComparer.Ordinal)
    {
        // lords.xml has the fuller "Duinhir, Lord of Morthond"; the registry has the bare given
        // name. English is the only locale that renders the literal, so syncing down would drop
        // the title from the one place it shows.
        "aom_lord_WE9_l_name",
    };

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
    public void EveryLordNameFallbackMatchesTheRegisteredEnglishText()
    {
        var root = FindRepoRoot();
        var strings = ReadModuleData(root, "taom_xslt_strings.xml");
        var lords = ReadModuleData(root, "characters", "lords.xml");

        var registry = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(
            strings, @"<string id=""(?<id>aom_lord_[^""]+_name)"" text=""\{=\k<id>\}(?<text>[^""]*)"""))
        {
            registry[m.Groups["id"].Value] = m.Groups["text"].Value;
        }

        Assert.IsTrue(registry.Count > 0, "parsed no lord name strings; taom_xslt_strings.xml shape changed");

        var drift = new List<string>();
        // Attribute-order independent on purpose. The original pattern required id first and name
        // second, which silently skipped the 600 entries written `<NPCCharacter id="..." race="..."
        // name="...">` - the whole Dol Guldur roster among them.
        foreach (Match m in Regex.Matches(lords, @"<NPCCharacter\b(?<attrs>[^>]*)>"))
        {
            var attrs = m.Groups["attrs"].Value;
            var idMatch = Regex.Match(attrs, @"\bid=""(?<v>[^""]*)""");
            var nameMatch = Regex.Match(attrs, @"\bname=""\{=(?<key>aom_lord_[^}]+)\}(?<literal>[^""]*)""");
            if (!idMatch.Success || !nameMatch.Success) continue;

            var key = nameMatch.Groups["key"].Value;
            if (AcceptedNameDifferences.Contains(key)) continue;
            if (!registry.TryGetValue(key, out var registered)) continue;

            var literal = nameMatch.Groups["literal"].Value;
            if (!string.Equals(literal, registered, StringComparison.Ordinal))
                drift.Add($"  {idMatch.Groups["v"].Value}: lords.xml says \"{literal}\", registry says \"{registered}\"");
        }

        Assert.AreEqual(0, drift.Count,
            "The English name fallback in characters/lords.xml disagrees with taom_xslt_strings.xml.\n" +
            "There is no English language folder, so the literal IS what English players see, while\n" +
            "the other 12 locales render the registry text. Sync with\n" +
            "tools/oneoff/sync_lord_name_fallbacks.py --apply\n" +
            string.Join("\n", drift));
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

        Assert.AreEqual(0, offenders.Count,
            "These lords are is_female=\"true\" but still carry a <beard_tags> block, which is what\n" +
            "actually renders facial hair. This is the leftover you get when a vanilla male id is\n" +
            "reused for a TAOM woman (lord_1_46_1 Thorwen shipped this way). Delete the block.\n" +
            string.Join("\n", offenders));
    }
}
