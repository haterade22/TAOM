using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// ADR-002: entry points stay under 150 lines and delegate to a service. An entry point here is a class that derives
/// (directly, or through another class in Main) from CampaignBehaviorBase, MissionBehavior, MissionLogic or
/// MissionView; a class named Taom*Model; or a Harmony patch class (a HarmonyPatch attribute on the class or on a member
/// inside it, or a static method named Prefix, Postfix, Transpiler or Finalizer, which a patch applied by hand has in
/// place of an attribute). The lines of a partial class add up across its files, and a class counts from its declaration
/// line to its closing brace. A class of 150 lines or more fails.
///
/// <c>EntryPointLineCountBaseline.txt</c> lists the classes that were over the limit when this gate landed, each with the
/// most lines it may have. It only shrinks: a class not in it fails at 150 lines, a listed class fails when it grows, a
/// listed class that shrank fails until its number is lowered, and one that fell under 150 fails until its line is
/// removed. To prune or lower, set
/// <c>TAOM_WRITE_ENTRY_POINT_BASELINE=1</c> and run <see cref="Baseline_Regenerate_WhenAsked"/>. Source text is read, not
/// the compiled assembly, so the gate needs no game install.
/// </summary>
[TestClass]
public class EntryPointLineCountGateTests
{
    private const int Limit = 150;
    private const string WriteEnvVar = "TAOM_WRITE_ENTRY_POINT_BASELINE";

    /// <summary>The baseline's line count. Lower it when you prune; a larger file means a line was added by hand.</summary>
    private const int MaxBaselineEntries = 18;

    private static readonly HashSet<string> RootBases = new(StringComparer.Ordinal)
    {
        "CampaignBehaviorBase", "MissionBehavior", "MissionLogic", "MissionView",
    };

    private static readonly Regex ClassKeyword = new(@"\bclass\s+(\w+)", RegexOptions.Compiled);
    private static readonly Regex Namespace = new(@"\bnamespace\s+([\w.]+)", RegexOptions.Compiled);
    private static readonly Regex HarmonyMember = new(@"\[\s*Harmony(?:Patch|Prefix|Postfix|Transpiler|Finalizer)\b", RegexOptions.Compiled);
    private static readonly Regex ManualPatchMethod = new(@"\bstatic\b[^;={}()]*\b(?:Prefix|Postfix|Transpiler|Finalizer)\s*\(", RegexOptions.Compiled);
    private static readonly Regex TaomModelName = new(@"^Taom\w*Model$", RegexOptions.Compiled);

    private static string BaselinePath => RepoPaths.RepoPath("TAOM.Tests", "Infrastructure", "EntryPointLineCountBaseline.txt");

    internal sealed class Decl
    {
        public string Key = "";
        public string Name = "";
        public List<string> Bases = new();
        public bool Harmony;
        public int Lines;
    }

    [TestMethod]
    public void EntryPoints_StayUnder150Lines_ExceptTheShrinkOnlyBaseline()
    {
        var failures = Compare(LiveOffenders(), LoadBaseline());

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }

    internal static List<string> Compare(IEnumerable<KeyValuePair<string, int>> live, Dictionary<string, int> baseline)
    {
        var failures = new List<string>();
        foreach (var pair in live)
        {
            var key = pair.Key;
            var lines = pair.Value;
            if (!baseline.TryGetValue(key, out var allowed))
                failures.Add($"{key}: {lines} lines (limit {Limit}); move the logic into a service (ADR-002)");
            else if (lines > allowed)
                failures.Add($"{key}: grew to {lines} lines from {allowed}; the baseline only shrinks");
            else if (lines < allowed)
                failures.Add($"{key}: {lines} lines is below its baseline {allowed}; lower it (set {WriteEnvVar}=1 and run Baseline_Regenerate_WhenAsked)");
        }

        return failures;
    }

    [TestMethod]
    public void Baseline_HasNoStaleEntriesAndIsSortedUnique()
    {
        var live = LiveOffenders().ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var lines = File.ReadAllLines(BaselinePath).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#")).ToList();
        var keys = lines.Select(l => l.Split(' ')[0]).ToList();

        var stale = keys.Where(k => !live.ContainsKey(k)).ToList();
        Assert.AreEqual(0, stale.Count, "These classes are under " + Limit + " lines or gone; remove them (set " + WriteEnvVar
            + "=1 and run Baseline_Regenerate_WhenAsked):" + Environment.NewLine + string.Join(Environment.NewLine, stale));
        CollectionAssert.AreEqual(keys.OrderBy(k => k, StringComparer.Ordinal).Distinct().ToList(), keys,
            "EntryPointLineCountBaseline.txt must be sorted (ordinal) with no duplicates.");
        Assert.AreEqual(MaxBaselineEntries, keys.Count, "The baseline has " + keys.Count + " lines and MaxBaselineEntries is "
            + MaxBaselineEntries + ". It only shrinks: never add a line. When you prune, lower MaxBaselineEntries.");
    }

    [TestMethod]
    public void Baseline_Regenerate_WhenAsked()
    {
        if (Environment.GetEnvironmentVariable(WriteEnvVar) != "1")
            Assert.Inconclusive("Set " + WriteEnvVar + "=1 to write the baseline: a missing file is created from the live offenders, an "
                + "existing one is pruned (it never gains a line).");

        var live = LiveOffenders();
        IEnumerable<KeyValuePair<string, int>> keep = live;
        if (File.Exists(BaselinePath))
        {
            var old = LoadBaseline();
            keep = live.Where(p => old.ContainsKey(p.Key)).Select(p => new KeyValuePair<string, int>(p.Key, Math.Min(p.Value, old[p.Key])));
        }

        var rows = keep.OrderBy(p => p.Key, StringComparer.Ordinal).ToList();
        var header = new[]
        {
            "# Entry-point classes (ADR-002) at " + Limit + " lines or more: <namespace.Class> <most lines it may have>.",
            "# Entry point: a CampaignBehaviorBase, MissionBehavior, MissionLogic or MissionView subclass, a Taom*Model, or a Harmony patch class (attributed, or with a static Prefix, Postfix, Transpiler or Finalizer).",
            "# The list may only shrink. Never add a line: move the logic into a service. To prune: set " + WriteEnvVar + "=1,",
            "# run EntryPointLineCountGateTests.Baseline_Regenerate_WhenAsked.",
            "# Entries: " + rows.Count + ".",
        };
        File.WriteAllLines(BaselinePath, header.Concat(rows.Select(r => r.Key + " " + r.Value.ToString(CultureInfo.InvariantCulture))));
    }

    [TestMethod]
    public void Scan_FindsEachEntryPointKindAndSumsPartialFiles()
    {
        var a = "namespace N;\npublic partial class Big : CampaignBehaviorBase\n{\n" + Filler(100) + "}\n";
        var b = "namespace N;\npublic partial class Big\n{\n" + Filler(100) + "}\n";
        var view = "namespace N;\nclass V : MissionView\n{\n" + Filler(160) + "}\n";
        var derived = "namespace N;\nclass Mid : MissionLogic { }\nclass Leaf : Mid\n{\n" + Filler(160) + "}\n";
        var model = "namespace N;\nclass TaomFooModel\n{\n" + Filler(160) + "}\n";
        var patch = "namespace N;\n[HarmonyPatch(typeof(X), \"Y\")]\nclass P\n{\n" + Filler(160) + "}\n";
        var memberPatch = "namespace N;\nclass Q\n{\n[HarmonyPrefix]\nstatic void A() { }\n" + Filler(160) + "}\n";
        var plain = "namespace N;\nclass Plain\n{\n" + Filler(300) + "}\n";

        var found = Offenders(new[] { a, b, view, derived, model, patch, memberPatch, plain }).Select(p => p.Key).OrderBy(k => k).ToList();

        CollectionAssert.AreEqual(new[] { "N.Big", "N.Leaf", "N.P", "N.Q", "N.TaomFooModel", "N.V" }, found, string.Join(",", found));
    }

    [TestMethod]
    public void Compare_ListedClassBelowItsNumber_FailsUntilTheNumberIsLowered()
    {
        var baseline = new Dictionary<string, int> { ["N.Big"] = 668 };

        var shrunk = Compare(new[] { new KeyValuePair<string, int>("N.Big", 200) }, baseline);
        var equal = Compare(new[] { new KeyValuePair<string, int>("N.Big", 668) }, baseline);

        Assert.AreEqual(1, shrunk.Count, string.Join(",", shrunk));
        StringAssert.Contains(shrunk[0], "Baseline_Regenerate_WhenAsked");
        Assert.AreEqual(0, equal.Count);

        var unlisted = Compare(new[] { new KeyValuePair<string, int>("N.New", 151) }, baseline);
        var grew = Compare(new[] { new KeyValuePair<string, int>("N.Big", 669) }, baseline);

        Assert.AreEqual(1, unlisted.Count, string.Join(",", unlisted));
        StringAssert.Contains(unlisted[0], "N.New: 151 lines");
        Assert.AreEqual(1, grew.Count, string.Join(",", grew));
        StringAssert.Contains(grew[0], "grew to 669 lines from 668");
    }

    [TestMethod]
    public void Scan_TreatsAManuallyAppliedPatchClassAsAnEntryPoint()
    {
        var postfix = "namespace N;\nstatic class ManualPost\n{\npublic static void Postfix(object __instance) { }\n" + Filler(160) + "}\n";
        var finalizer = "namespace N;\nstatic class ManualFin\n{\nstatic Exception? Finalizer(Exception e) => e;\n" + Filler(160) + "}\n";
        var instance = "namespace N;\nclass NotPatch\n{\npublic void Prefix() { }\n" + Filler(160) + "}\n";
        var lookalike = "namespace N;\nclass Alike\n{\npublic static void PrefixLoader() { }\n" + Filler(160) + "}\n";

        var found = Offenders(new[] { postfix, finalizer, instance, lookalike }).Select(p => p.Key).OrderBy(k => k).ToList();

        CollectionAssert.AreEqual(new[] { "N.ManualFin", "N.ManualPost" }, found, string.Join(",", found));
    }

    [TestMethod]
    public void Scan_IgnoresBracesInStringsAndComments()
    {
        var source = "namespace N;\nclass C : MissionLogic\n{\n    string s = \"}}}\"; // }\n    /* } */\n    char c = '}';\n}\nclass After { }\n";

        var decl = Parse(source).Single(d => d.Name == "C");

        Assert.AreEqual(6, decl.Lines);
    }

    [TestMethod]
    public void Parse_ClassWithNoClosingBrace_FailsNamingTheClass()
    {
        var source = "namespace N;\nclass Broken : MissionLogic\n{\n    int x;\n";

        var ex = Assert.ThrowsException<AssertFailedException>(() => Parse(source));

        StringAssert.Contains(ex.Message, "N.Broken");
    }

    [TestMethod]
    public void Mask_VerbatimStringStartingWithAnEscapedQuote_DoesNotBlankTheRestOfTheFile()
    {
        var source = "namespace N;\nclass A { string s = @\"\"\"x\"\" y\"; }\nclass After : MissionLogic { }\n";

        var masked = Mask(source);

        StringAssert.Contains(masked, "class After");
        Assert.AreEqual(source.Length, masked.Length);
        Assert.IsFalse(Mask("var r = \"\"\"}}\"\"\";").Contains("}"), "a raw string literal is still blanked");
    }

    [TestMethod]
    public void Scan_LeavesAClassUnder150LinesAlone()
    {
        var source = "namespace N;\nclass Small : CampaignBehaviorBase\n{\n" + Filler(100) + "}\n";

        Assert.AreEqual(0, Offenders(new[] { source }).Count);
    }

    private static string Filler(int lines) => string.Concat(Enumerable.Repeat("    int x;\n", lines));

    private static Dictionary<string, int> LoadBaseline()
    {
        Assert.IsTrue(File.Exists(BaselinePath), "Baseline not found at " + BaselinePath
            + ". Set " + WriteEnvVar + "=1 and run Baseline_Regenerate_WhenAsked to create it.");
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(BaselinePath).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#")))
        {
            var parts = line.Split(' ');
            Assert.AreEqual(2, parts.Length, "baseline line must be '<class> <lines>': " + line);
            result[parts[0]] = int.Parse(parts[1], CultureInfo.InvariantCulture);
        }

        return result;
    }

    private static List<KeyValuePair<string, int>> LiveOffenders()
    {
        var sources = RepoPaths.CSharpFiles().Select(File.ReadAllText).ToList();
        Assert.IsTrue(sources.Count > 500, "Main sources were not found");
        var offenders = Offenders(sources);
        Assert.IsTrue(offenders.Count > 0 || File.Exists(BaselinePath), "no entry point was recognised");
        return offenders;
    }

    internal static List<KeyValuePair<string, int>> Offenders(IEnumerable<string> sources)
    {
        var all = sources.SelectMany(Parse).ToList();
        var entryNames = new HashSet<string>(RootBases, StringComparer.Ordinal);
        bool grew;
        do
        {
            grew = false;
            foreach (var d in all)
                if (d.Bases.Any(entryNames.Contains) && entryNames.Add(d.Name))
                    grew = true;
        }
        while (grew);

        return all
            .GroupBy(d => d.Key)
            .Where(g => g.Any(d => d.Bases.Any(entryNames.Contains) || TaomModelName.IsMatch(d.Name) || d.Harmony))
            .Select(g => new KeyValuePair<string, int>(g.Key, g.Sum(d => d.Lines)))
            .Where(p => p.Value >= Limit)
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .ToList();
    }

    internal static List<Decl> Parse(string source)
    {
        var masked = Mask(source);
        var ns = Namespace.Match(masked) is { Success: true } m ? m.Groups[1].Value + "." : "";
        var result = new List<Decl>();
        foreach (Match match in ClassKeyword.Matches(masked))
        {
            var open = masked.IndexOfAny(new[] { '{', ';' }, match.Index);
            if (open < 0 || masked[open] != '{')
                continue;

            var close = MatchingBrace(masked, open);
            if (close < 0)
                Assert.Fail($"Class {ns}{match.Groups[1].Value} has no matching closing brace, so its lines cannot be counted. "
                    + "Skipping it would let an entry point of any size pass; fix the scan or the source.");

            var headerStart = match.Index + match.Length;
            var header = masked.Substring(headerStart, open - headerStart);
            var where = header.IndexOf(" where ", StringComparison.Ordinal);
            if (where >= 0)
                header = header.Substring(0, where);
            var bases = new List<string>();
            var colon = header.IndexOf(':');
            if (colon >= 0)
                bases = SplitTopLevel(header.Substring(colon + 1))
                    .Select(b => Regex.Replace(b.Trim(), @"<.*$", "").Trim().Split('.').Last())
                    .Where(b => b.Length > 0).ToList();

            var lead = LeadingText(masked, match.Index);
            var body = masked.Substring(open, close - open + 1);
            var name = match.Groups[1].Value;
            result.Add(new Decl
            {
                Key = ns + name,
                Name = name,
                Bases = bases,
                Harmony = HarmonyMember.IsMatch(lead) || HarmonyMember.IsMatch(body) || ManualPatchMethod.IsMatch(body),
                Lines = LineOf(masked, close) - LineOf(masked, match.Index) + 1,
            });
        }

        return result;
    }

    private static string LeadingText(string text, int index)
    {
        var start = text.LastIndexOfAny(new[] { ';', '}', '{' }, Math.Max(index - 1, 0));
        return text.Substring(start + 1, index - start - 1);
    }

    private static IEnumerable<string> SplitTopLevel(string text)
    {
        var depth = 0;
        var sb = new StringBuilder();
        foreach (var c in text)
        {
            if (c == '<') depth++;
            if (c == '>') depth--;
            if (c == ',' && depth == 0)
            {
                yield return sb.ToString();
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        yield return sb.ToString();
    }

    private static int LineOf(string text, int index)
    {
        var line = 1;
        for (var i = 0; i < index; i++)
            if (text[i] == '\n')
                line++;
        return line;
    }

    private static int MatchingBrace(string text, int open)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}' && --depth == 0) return i;
        }

        return -1;
    }

    // A quote opening @"...", $@"..." or @$"...": a run of three quotes there is an escaped quote, not a raw string.
    private static bool IsVerbatimStart(string s, int i) =>
        i > 0 && (s[i - 1] == '@' || (i > 1 && s[i - 1] == '$' && s[i - 2] == '@') || (i > 1 && s[i - 1] == '@' && s[i - 2] == '$'));

    /// <summary>Blanks comments, string literals and char literals to spaces, keeping length and line breaks.</summary>
    internal static string Mask(string s)
    {
        var o = s.ToCharArray();
        var i = 0;
        while (i < s.Length)
        {
            var c = s[i];
            if (c == '/' && i + 1 < s.Length && s[i + 1] == '/')
            {
                while (i < s.Length && s[i] != '\n') o[i++] = ' ';
            }
            else if (c == '/' && i + 1 < s.Length && s[i + 1] == '*')
            {
                var end = s.IndexOf("*/", i + 2, StringComparison.Ordinal);
                end = end < 0 ? s.Length : end + 2;
                for (; i < end; i++) if (s[i] != '\n') o[i] = ' ';
            }
            else if (c == '"' && i + 2 < s.Length && s[i + 1] == '"' && s[i + 2] == '"' && !IsVerbatimStart(s, i))
            {
                var end = s.IndexOf("\"\"\"", i + 3, StringComparison.Ordinal);
                end = end < 0 ? s.Length : end + 3;
                for (; i < end; i++) if (s[i] != '\n') o[i] = ' ';
            }
            else if (c == '"')
            {
                var verbatim = IsVerbatimStart(s, i);
                o[i++] = ' ';
                while (i < s.Length)
                {
                    if (!verbatim && s[i] == '\\' && i + 1 < s.Length) { o[i++] = ' '; if (s[i] != '\n') o[i] = ' '; i++; continue; }
                    if (s[i] == '"')
                    {
                        if (verbatim && i + 1 < s.Length && s[i + 1] == '"') { o[i++] = ' '; o[i++] = ' '; continue; }
                        o[i++] = ' ';
                        break;
                    }

                    if (s[i] == '\n' && !verbatim) break;
                    if (s[i] != '\n') o[i] = ' ';
                    i++;
                }
            }
            else if (c == '\'' && i + 2 < s.Length && (s[i + 2] == '\'' || (s[i + 1] == '\\' && s.IndexOf('\'', i + 2) is var q && q > 0 && q - i <= 10)))
            {
                var end = s[i + 1] == '\\' ? s.IndexOf('\'', i + 2) : i + 2;
                for (; i <= end; i++) o[i] = ' ';
            }
            else
            {
                i++;
            }
        }

        return new string(o);
    }
}
