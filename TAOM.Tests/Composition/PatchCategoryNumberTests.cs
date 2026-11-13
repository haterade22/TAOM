using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Composition;
using TAOM.Tests.Migration;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Composition;

/// <summary>
/// Keeps patch category names honest (#938). A <c>PatchNNN</c> number names one feature (the feature folder of the type
/// that declares the category), every category has a section in the Harmony patch registry, and every patch number a
/// feature-map row cites belongs to that row's feature. Categories are read from every <c>[HarmonyPatchCategory]</c>
/// on a TAOM type and from every feature module's <c>PatchCategoryDecl</c>. The scans have floors, so a gate that
/// finds nothing fails instead of passing.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class PatchCategoryNumberTests
{
    private const int MinimumCategories = 100;
    private const int MinimumRegistryHeadings = 100;
    private const int MinimumFeatureMapMentions = 50;

    private static readonly Regex Numbered = new Regex(@"^Patch(\d+[a-z]?)_", RegexOptions.Compiled);

    private static readonly Regex Mention = new Regex(@"Patch(\d+[a-z]?)(_[A-Za-z0-9_]+)?", RegexOptions.Compiled);

    private static readonly Regex FeaturePath = new Regex(@"(?:Main|Client)/Features/(\w+)/", RegexOptions.Compiled);

    private static readonly Regex Heading = new Regex(@"^##\s+(\S+)", RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>Patch numbers that name two features today, keyed by number, with the exact owner set. Mike 2026-10-19: keep, do not renumber.</summary>
    private static readonly Dictionary<string, string[]> SharedNumbers = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["63"] = new[] { "BannerBearers", "BlowDiagnostics" },
    };

    /// <summary>Categories documented under a parent heading instead of their own. A stale entry fails.</summary>
    private static readonly Dictionary<string, string> NoRegistryHeading = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Patch89_MapLoadDiagnostics_Lifecycle"] = "sub-category documented under ## Patch89_MapLoadDiagnostics",
        ["Patch89_MapLoadDiagnostics_MapScreen"] = "sub-category documented under ## Patch89_MapLoadDiagnostics",
        ["Patch89_MapLoadDiagnostics_SceneReady"] = "sub-category documented under ## Patch89_MapLoadDiagnostics",
        ["Patch98_HitchProbe_Camera"] = "sub-category documented under ## Patch98_HitchProbe",
        ["Patch_MissionTime_SmartCavalryAI"] = "documented under ## Patch_MissionTime_SetMovementOrder (split 2026-11-13)",
        ["Patch_MissionTime_CompanionTactics"] = "documented under ## Patch_MissionTime_SetMovementOrder (split 2026-11-13)",
        ["Patch_MissionTime_MarshalsWord"] = "documented under ## Patch_MissionTime_SetMovementOrder (split 2026-11-13)",
        ["Patch101_MapFrameProfiler_Views"] = "sub-category documented under ## Patch101_MapFrameProfiler",
        ["Patch101_MapFrameProfiler_Menu"] = "sub-category documented under ## Patch101_MapFrameProfiler",
    };

    /// <summary>A feature-map row that cites another feature's patch number, keyed "row Patch##". A stale entry fails.</summary>
    private static readonly Dictionary<string, string> RowCitesAnotherFeature = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["SettlementNameplateRelation Patch38"] = "its sliders go through SettlementNameplateFade's postfix",
        ["SmartCavalryAI Patch31"] = "applied through the deferred Patch_MissionTime_SmartCavalryAI category",
    };

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    internal static string OwnerOf(Type type)
    {
        // TAOM.Client.Features.X belongs to feature X too: the patch index applies client patch classes in the core category
        const string prefix = "TAOM.Features.";
        const string clientPrefix = "TAOM.Client.Features.";
        var ns = type.Namespace ?? string.Empty;
        var start = ns.StartsWith(prefix, StringComparison.Ordinal) ? prefix.Length
            : ns.StartsWith(clientPrefix, StringComparison.Ordinal) ? clientPrefix.Length : -1;
        if (start < 0)
            return ns;
        var rest = ns.Substring(start);
        var dot = rest.IndexOf('.');
        return dot < 0 ? rest : rest.Substring(0, dot);
    }

    /// <summary>Groups (category, owner) pairs by patch number into the set of owners each number has.</summary>
    internal static Dictionary<string, SortedSet<string>> OwnersByNumber(IEnumerable<(string Category, string Owner)> declared)
    {
        var result = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var (category, owner) in declared)
        {
            var match = Numbered.Match(category);
            if (!match.Success)
                continue;
            var number = match.Groups[1].Value;
            if (!result.TryGetValue(number, out var owners))
                result[number] = owners = new SortedSet<string>(StringComparer.Ordinal);
            owners.Add(owner);
        }
        return result;
    }

    private static List<(string Category, string Owner)> Declared()
    {
        var found = new List<(string, string)>();
        foreach (var type in TAOM.Tests.Infrastructure.TaomAssemblies.All.SelectMany(AccessTools.GetTypesFromAssembly))
            foreach (var attribute in type.GetCustomAttributes<HarmonyPatchCategory>())
                found.Add((attribute.info.category, OwnerOf(type)));
        foreach (var module in FeatureModules.All)
            foreach (var decl in module.PatchCategories)
                found.Add((decl.Category, OwnerOf(module.GetType())));
        return found.Distinct().ToList();
    }

    private static string Registry => File.ReadAllText(RepoPath("docs", "reference", "harmony-patch-registry.md"));

    private static IEnumerable<(string Row, string Line)> FeatureMapRows() =>
        File.ReadAllLines(RepoPath("docs", "reference", "feature-map.md"))
            .Where(l => l.StartsWith("| ", StringComparison.Ordinal))
            .Select(l => (Row: l.Substring(2, Math.Max(0, l.IndexOf(" |", 2, StringComparison.Ordinal) - 2)), Line: l));

    /// <summary>The feature folder a row describes: the first Main/Features path in it, else the row name without spaces.</summary>
    internal static string RowFeature(string row, string line)
    {
        var path = FeaturePath.Match(line);
        return path.Success ? path.Groups[1].Value : row.Replace(" ", string.Empty);
    }

    private static void RequireGame()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
    }

    [TestMethod]
    public void OwnersByNumber_GroupsSyntheticDuplicates()
    {
        var owners = OwnersByNumber(new[]
        {
            ("Patch7_Alpha", "Alpha"), ("Patch7_Alpha_Views", "Alpha"), ("Patch8_Beta", "Beta"), ("Patch8_Gamma", "Gamma"),
            ("Patch_Shared", "Delta"),
        });

        CollectionAssert.AreEqual(new[] { "Alpha" }, owners["7"].ToArray());
        CollectionAssert.AreEqual(new[] { "Beta", "Gamma" }, owners["8"].ToArray());
        Assert.AreEqual(2, owners.Count, "a category without a number is not grouped");
    }

    [TestMethod]
    public void EveryPatchNumber_HasOneOwningFeature_UnlessListedWithItsExactOwners()
    {
        RequireGame();
        var declared = Declared();
        Assert.IsTrue(declared.Count >= MinimumCategories, "the category scan found only " + declared.Count);

        var owners = OwnersByNumber(declared);
        var unexplained = owners.Where(p => p.Value.Count > 1
                && !(SharedNumbers.TryGetValue(p.Key, out var allowed) && allowed.SequenceEqual(p.Value)))
            .Select(p => "Patch" + p.Key + " names " + string.Join(" and ", p.Value)).ToList();
        var stale = SharedNumbers.Where(p => !owners.TryGetValue(p.Key, out var actual) || !p.Value.SequenceEqual(actual))
            .Select(p => p.Key).ToList();

        Assert.AreEqual(0, unexplained.Count, "A patch number names one feature: " + string.Join("; ", unexplained));
        Assert.AreEqual(0, stale.Count, "SharedNumbers entries that no longer apply: " + string.Join(", ", stale));
    }

    [TestMethod]
    public void EveryCategory_HasASectionInTheHarmonyPatchRegistry()
    {
        RequireGame();
        var headings = Heading.Matches(Registry).Cast<Match>().Select(m => m.Groups[1].Value).ToHashSet();
        Assert.IsTrue(headings.Count >= MinimumRegistryHeadings, "the registry heading scan found only " + headings.Count);
        var categories = Declared().Select(d => d.Category).Distinct().ToList();

        var missing = categories.Where(c => !headings.Contains(c) && !NoRegistryHeading.ContainsKey(c)).OrderBy(c => c).ToList();
        var stale = NoRegistryHeading.Keys.Where(k => !categories.Contains(k) || headings.Contains(k)).OrderBy(k => k).ToList();

        Assert.AreEqual(0, missing.Count, "Categories with no '## <category>' heading in harmony-patch-registry.md: " + string.Join(", ", missing));
        Assert.AreEqual(0, stale.Count, "NoRegistryHeading entries that no longer apply: " + string.Join(", ", stale));
    }

    [TestMethod]
    public void EveryPatchNumberAFeatureMapRowCites_BelongsToThatRowsFeature()
    {
        RequireGame();
        var declared = Declared();
        var owners = OwnersByNumber(declared);
        var categories = declared.Select(d => d.Category).ToHashSet();

        var wrong = new List<string>();
        var cited = new HashSet<string>();
        var mentions = 0;
        foreach (var (row, line) in FeatureMapRows())
        {
            foreach (Match match in Mention.Matches(line))
            {
                mentions++;
                var number = match.Groups[1].Value;
                var key = row + " Patch" + number;
                cited.Add(key);
                if (match.Groups[2].Success && !categories.Contains(match.Value))
                {
                    if (!RowCitesAnotherFeature.ContainsKey(key))
                        wrong.Add(row + " names " + match.Value + ", which no code declares");
                    continue;
                }
                var belongs = owners.TryGetValue(number, out var set) && set.Contains(RowFeature(row, line));
                if (!belongs && !RowCitesAnotherFeature.ContainsKey(key))
                    wrong.Add(row + " cites Patch" + number + ", owned by " + (set == null ? "nobody" : string.Join("/", set)));
            }
        }

        Assert.IsTrue(mentions >= MinimumFeatureMapMentions, "the feature-map scan found only " + mentions + " patch mentions");
        var stale = RowCitesAnotherFeature.Keys.Where(k => !cited.Contains(k)
            || (owners.TryGetValue(k.Substring(k.LastIndexOf("Patch", StringComparison.Ordinal) + 5), out var set)
                && set.Contains(RowFeature(k.Substring(0, k.LastIndexOf(' ')), FeatureMapRows().First(r => r.Row == k.Substring(0, k.LastIndexOf(' '))).Line))))
            .ToList();

        Assert.AreEqual(0, wrong.Count, "feature-map.md rows citing a patch number that is not theirs: " + string.Join("; ", wrong.Distinct()));
        Assert.AreEqual(0, stale.Count, "RowCitesAnotherFeature entries that no longer apply: " + string.Join(", ", stale));
    }
}
