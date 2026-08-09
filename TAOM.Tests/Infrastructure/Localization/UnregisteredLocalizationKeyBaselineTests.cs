using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure.Localization;

/// <summary>
/// A ratchet on unregistered localization keys.
///
/// 317 TAOM-owned <c>{=key}</c> literals in C# have no row in any ModuleData strings XML — 55% of
/// the 567 the code declares (#434). None of them misbehaves in English:
/// <c>MBTextManager.GetLocalizedText</c> short-circuits on English and returns the inline default,
/// so the developer's own game looks correct and the missing row matters only to the other eleven
/// languages.
///
/// Registering all 317 is a content + translation task with a real cost, and it is not this test's
/// job. What this test does is stop the number growing: the live set must be a **subset** of the
/// checked-in baseline, so a newly-added unregistered key fails the build while the backlog is
/// worked down. Fixing one requires deleting its baseline line, which is how the list shrinks.
///
/// Deliberately scoped to `taom_`-prefixed keys. A bare <c>{=SomeVanillaId}</c> is usually a
/// reference to a row the base game already owns, and flagging those would bury the real signal —
/// the same "a check that reports mostly noise gets ignored" failure that let this accumulate.
/// </summary>
[TestClass]
public class UnregisteredLocalizationKeyBaselineTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));

    private static string ModuleDataPath => Path.Combine(RepoRoot, "Main", "_Module", "ModuleData");

    private static string BaselinePath => Path.Combine(RepoRoot, "TAOM.Tests", "Infrastructure",
        "Localization", "unregistered-loc-keys-baseline.txt");

    /// <summary>Matches a TAOM key literal as written in C#: <c>"{=taom_foo}default text"</c>.</summary>
    private static readonly Regex KeyLiteral =
        new Regex(@"""\{=(taom_[A-Za-z0-9_]*)\}", RegexOptions.Compiled);

    /// <summary>Matches a registered default's own embedded prefix: <c>text="{=taom_foo}Foo"</c>.</summary>
    private static readonly Regex EmbeddedPrefix =
        new Regex(@"^\{=([A-Za-z0-9_]+)\}", RegexOptions.Compiled);

    /// <summary>
    /// Every key the mod declares anywhere outside `Languages/`. Two sources, because TAOM registers
    /// in two shapes: a `<string id=...>` row, and a data attribute whose value carries its own
    /// `{=key}` prefix (career display names, quest titles). Counting only the first would report
    /// hundreds of false positives.
    /// </summary>
    private static HashSet<string> LoadRegisteredKeys()
    {
        Assert.IsTrue(Directory.Exists(ModuleDataPath), $"ModuleData not found at {ModuleDataPath}");

        var registered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(ModuleDataPath, "*.xml", SearchOption.AllDirectories))
        {
            if (file.Split(Path.DirectorySeparatorChar).Contains("Languages"))
                continue;

            XDocument document;
            try { document = XDocument.Load(file); }
            catch (System.Xml.XmlException) { continue; }   // not our file to validate
            if (document.Root == null)
                continue;

            foreach (var element in document.Root.DescendantsAndSelf())
            {
                if (element.Name.LocalName == "string")
                {
                    var id = (string?)element.Attribute("id");
                    if (!string.IsNullOrEmpty(id))
                        registered.Add(id!);
                }

                foreach (var attribute in element.Attributes())
                {
                    var prefix = EmbeddedPrefix.Match(attribute.Value);
                    if (prefix.Success)
                        registered.Add(prefix.Groups[1].Value);
                }
            }
        }

        Assert.IsTrue(registered.Count > 1000,
            $"Only {registered.Count} keys parsed from ModuleData — the parse is broken, and a test "
            + "that finds nothing unregistered would pass for the wrong reason.");
        return registered;
    }

    private static Dictionary<string, string> FindDeclaredKeys()
    {
        var mainPath = Path.Combine(RepoRoot, "Main");
        Assert.IsTrue(Directory.Exists(mainPath), $"Main not found at {mainPath}");

        var declared = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(mainPath, "*.cs", SearchOption.AllDirectories))
        {
            var parts = file.Split(Path.DirectorySeparatorChar);
            if (parts.Contains("bin") || parts.Contains("obj"))
                continue;

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (Match match in KeyLiteral.Matches(lines[i]))
                {
                    var key = match.Groups[1].Value;
                    if (!declared.ContainsKey(key))
                        declared[key] = $"{MakeRelative(file)}:{i + 1}";
                }
            }
        }

        Assert.IsTrue(declared.Count > 100,
            $"Only {declared.Count} taom_ key literals found in Main/ — the scan is broken.");
        return declared;
    }

    private static string MakeRelative(string path) =>
        path.StartsWith(RepoRoot, StringComparison.OrdinalIgnoreCase)
            ? path.Substring(RepoRoot.Length).TrimStart(Path.DirectorySeparatorChar).Replace('\\', '/')
            : path;

    private static HashSet<string> LoadBaseline()
    {
        Assert.IsTrue(File.Exists(BaselinePath), $"Baseline not found at {BaselinePath}");
        return new HashSet<string>(
            File.ReadAllLines(BaselinePath)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith("#", StringComparison.Ordinal)),
            StringComparer.Ordinal);
    }

    private static HashSet<string> LiveUnregistered(out Dictionary<string, string> sites)
    {
        var registered = LoadRegisteredKeys();
        sites = FindDeclaredKeys();
        return new HashSet<string>(sites.Keys.Where(k => !registered.Contains(k)), StringComparer.Ordinal);
    }

    [TestMethod]
    public void NoNewUnregisteredLocalizationKeys()
    {
        var live = LiveUnregistered(out var sites);
        var added = live.Except(LoadBaseline()).OrderBy(k => k, StringComparer.Ordinal).ToList();

        Assert.AreEqual(0, added.Count,
            "New localization key(s) with no row in any ModuleData strings XML. This renders "
            + "correctly in English — GetLocalizedText short-circuits and returns the inline default "
            + "— and is untranslatable in the other eleven languages, so nothing else will tell you. "
            + "Register them, then run tools/translate_with_claude.py (#434):\n  "
            + string.Join("\n  ", added.Select(k => $"{k}  ({sites[k]})")));
    }

    [TestMethod]
    public void BaselineHasNoStaleEntries()
    {
        // A baseline entry for a key that has since been registered — or deleted from the code —
        // is dead weight that quietly re-permits the key if it ever comes back unregistered. The
        // list only shrinks if something forces the deletion.
        var stale = LoadBaseline().Except(LiveUnregistered(out _))
            .OrderBy(k => k, StringComparer.Ordinal).ToList();

        Assert.AreEqual(0, stale.Count,
            $"{stale.Count} baseline entries are no longer unregistered (fixed, or the key is gone). "
            + "Delete these lines from unregistered-loc-keys-baseline.txt:\n  "
            + string.Join("\n  ", stale));
    }
}
