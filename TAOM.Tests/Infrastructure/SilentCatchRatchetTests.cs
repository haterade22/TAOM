using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// The silent catch ratchet (#1073). A catch in a Harmony patch or Hooks file of Main or Client is silent when its
/// body neither logs, rethrows, calls <c>Outcomes.Swallowed</c> nor carries a <c>// swallow-ok: &lt;reason&gt;</c>
/// comment (<see cref="SilentCatchScan"/>). <c>SilentCatchBaseline.txt</c> holds the per-file counts that existed
/// when the gate landed; a count may only fall, and a fall lowers its line in the same commit.
/// </summary>
[TestClass]
public class SilentCatchRatchetTests
{
    private const string Fixes =
        "Report it with Outcomes.Swallowed(\"<feature folder>\", \"<Class.Method>\", ex), or mark a catch where " +
        "silence is right with // swallow-ok: <reason>.";

    private static readonly Regex BaselineLine = new(@"^(\d+) (\S+)$");
    private static readonly Regex TotalsLine = new(@"^# Files: (\d+)\. Silent catches: (\d+)\.$");

    private static readonly Lazy<TreeScan> Tree = new(ScanTree);

    private sealed class TreeScan
    {
        internal readonly Dictionary<string, List<int>> SilentLines = new(StringComparer.Ordinal);
        internal readonly HashSet<string> Selected = new(StringComparer.Ordinal);
        internal readonly List<string> Unparsed = new();
    }

    private static TreeScan ScanTree()
    {
        var scan = new TreeScan();
        var repoDir = RepoPaths.RepoPath();
        foreach (var file in RepoPaths.CSharpFiles())
        {
            var rel = file.Substring(repoDir.Length).TrimStart('\\', '/').Replace('\\', '/');
            var text = File.ReadAllText(file).Replace("\r\n", "\n");
            if (!SilentCatchScan.IsSelected(rel, text))
                continue;

            scan.Selected.Add(rel);
            foreach (var c in SilentCatchScan.Catches(text))
            {
                if (!c.Parsed)
                    scan.Unparsed.Add(rel + ":" + c.Line);
                else if (c.Silent)
                {
                    if (!scan.SilentLines.TryGetValue(rel, out var lines))
                        scan.SilentLines[rel] = lines = new List<int>();
                    lines.Add(c.Line);
                }
            }
        }

        return scan;
    }

    private static List<string> BaselineLines()
    {
        var path = RepoPaths.RepoPath("TAOM.Tests", "Infrastructure", "SilentCatchBaseline.txt");
        Assert.IsTrue(File.Exists(path), "Baseline not found at " + path);
        return File.ReadAllLines(path).Select(l => l.TrimEnd('\r')).ToList();
    }

    private static Dictionary<string, int> Baseline()
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var line in BaselineLines())
        {
            var m = BaselineLine.Match(line);
            if (m.Success)
                counts[m.Groups[2].Value] = int.Parse(m.Groups[1].Value);
        }

        return counts;
    }

    private static int Live(string path) =>
        Tree.Value.SilentLines.TryGetValue(path, out var lines) ? lines.Count : 0;

    [TestMethod]
    public void NoFileGainsASilentCatch()
    {
        var baseline = Baseline();
        var risen = Tree.Value.SilentLines
            .Where(kv => kv.Value.Count > (baseline.TryGetValue(kv.Key, out var b) ? b : 0))
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key}: {kv.Value.Count} silent, baseline " +
                          $"{(baseline.TryGetValue(kv.Key, out var b) ? b : 0)}, at lines {string.Join(", ", kv.Value)}")
            .ToList();

        Assert.AreEqual(0, risen.Count, $"Files with more silent catches than the baseline allows. {Fixes}\n" +
                                        string.Join("\n", risen));
    }

    [TestMethod]
    public void BaselineHasNoStaleCounts()
    {
        var stale = Baseline()
            .Where(kv => Live(kv.Key) < kv.Value)
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => Live(kv.Key) == 0
                ? $"delete the line: {kv.Value} {kv.Key}"
                : $"replace with: {Live(kv.Key)} {kv.Key}")
            .ToList();

        Assert.AreEqual(0, stale.Count, "A file holds fewer silent catches than SilentCatchBaseline.txt says. Lower " +
                                        "the line, and the totals header, in the same commit:\n" + string.Join("\n", stale));
    }

    [TestMethod]
    public void BaselineIsWellFormed()
    {
        var problems = new List<string>();
        var paths = new List<string>();
        var sum = 0;
        int? files = null, catches = null;
        foreach (var line in BaselineLines())
        {
            if (line.Length == 0)
                continue;
            if (line.StartsWith("#", StringComparison.Ordinal))
            {
                var t = TotalsLine.Match(line);
                if (t.Success)
                {
                    files = int.Parse(t.Groups[1].Value);
                    catches = int.Parse(t.Groups[2].Value);
                }

                continue;
            }

            var m = BaselineLine.Match(line);
            if (!m.Success)
            {
                problems.Add("malformed: " + line);
                continue;
            }

            var count = int.Parse(m.Groups[1].Value);
            if (count < 1)
                problems.Add("count below 1: " + line);
            paths.Add(m.Groups[2].Value);
            sum += count;
        }

        var sorted = paths.OrderBy(p => p, StringComparer.Ordinal).ToList();
        if (!paths.SequenceEqual(sorted))
            problems.Add("lines are not sorted by path (ordinal)");
        problems.AddRange(paths.GroupBy(p => p, StringComparer.Ordinal).Where(g => g.Count() > 1)
            .Select(g => "duplicate: " + g.Key));
        if (files == null)
            problems.Add("no '# Files: N. Silent catches: M.' header");
        else if (files != paths.Count || catches != sum)
            problems.Add($"header says {files} files and {catches} catches; the lines hold {paths.Count} and {sum}");

        Assert.AreEqual(0, problems.Count, string.Join("\n", problems));
    }

    [TestMethod]
    public void EveryCatchParses()
    {
        Assert.AreEqual(0, Tree.Value.Unparsed.Count,
            "catch clauses the scanner could not read (fix SilentCatchScan, never the source): " +
            string.Join(", ", Tree.Value.Unparsed));
    }

    [TestMethod]
    public void SelectionPositiveControl()
    {
        var selected = Tree.Value.Selected;
        Assert.IsTrue(selected.Contains("Main/Features/HeroRace/Hooks/CharacterSpawner_InitWithCharacter_Patch.cs"),
            "a Hooks file must be scanned");
        Assert.IsTrue(selected.Contains("Main/Features/TroopWeight/Diagnostics/TroopRoster_SetElementNumber_Patch.cs"),
            "a [HarmonyPatch] class outside Hooks must be scanned");
        Assert.IsFalse(selected.Contains("Main/Features/CompanionTactics/FormationPresets/OOBOverlayService.cs"),
            "a service outside Hooks is not a patch file");
        Assert.IsFalse(selected.Contains("Main/Features/DevConsole/HarmonyPatchInspector.cs"),
            "the patch inspector console tool is not a patch file");
        Assert.IsTrue(selected.Count > 500, $"only {selected.Count} files selected: the walk lost a root");
    }

    // Sweep pins (#1073): each swept catch reports through Outcomes.Swallowed and keeps its fallback.
    [DataTestMethod]
    [DataRow("Main/Features/Enlistment/Hooks/LordConversationsConditionPatches.cs", "Enlistment", "LordConversationsConditionGate.ShouldSuppress", "return false;")]
    [DataRow("Main/Features/CharacterCreation/Hooks/CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs", "CharacterCreation", "CharacterCreationNarrativeStageView_SpawnNonHuman_Patch.Prefix", "return true;")]
    [DataRow("Main/Features/CharacterCreation/Hooks/FaceGenVM_Refresh_RaceFilter_Patch.cs", "CharacterCreation", "FaceGenVM_Refresh_RaceFilter_Patch.ResolveFilterService", "(none)")]
    [DataRow("Main/Features/CharacterCreation/Hooks/FaceGenVM_Refresh_RaceFilter_Patch.cs", "CharacterCreation", "FaceGenVM_Refresh_RaceFilter_Patch.ResolvePlayerSwitchSession", "(none)")]
    [DataRow("Main/Features/CharacterCreation/Hooks/FaceGenVM_Refresh_RaceFilter_Patch.cs", "CharacterCreation", "FaceGenVM_Refresh_RaceFilter_Patch.ResolveFactionPresets", "(none)")]
    [DataRow("Main/Features/CharacterCreation/Hooks/FaceGenVM_Refresh_RaceFilter_Patch.cs", "CharacterCreation", "FaceGenVM_Refresh_RaceFilter_Patch.ResolveLogger", "(none)")]
    [DataRow("Main/Features/HeroRace/Hooks/CharacterSpawner_InitWithCharacter_Patch.cs", "HeroRace", "CharacterSpawner_InitWithCharacter_Patch.Prefix", "return true;")]
    [DataRow("Main/Features/CompanionTactics/FormationPresets/Hooks/Patch35_OOBUIHandler_Tick.cs", "CompanionTactics", "Patch35_OOBUIHandler_Tick.Postfix", "(none)")]
    [DataRow("Main/Features/CompanionTactics/FormationPresets/Hooks/Patch35_OOBUIHandler_Finalize.cs", "CompanionTactics", "Patch35_OOBUIHandler_Finalize.Postfix", "(none)")]
    [DataRow("Main/Features/CompanionTactics/FormationPresets/Hooks/Patch35_OrderOfBattleVM_Ctor.cs", "CompanionTactics", "Patch35_OrderOfBattleVM_Ctor.Postfix", "(none)")]
    [DataRow("Main/Features/CompanionTactics/Roles/Hooks/Patch35_OOBHeroItem_RefreshValues.cs", "CompanionTactics", "Patch35_OOBHeroItem_RefreshValues.Postfix", "(none)")]
    [DataRow("Main/Features/CompanionTactics/FormationPresets/OOBOverlayService.cs", "CompanionTactics", "OOBOverlayService.OnTick", "return;")]
    [DataRow("Main/Features/ArmyTargeting/Hooks/Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.cs", "ArmyTargeting", "Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.Finalizer", "(none)")]
    [DataRow("Main/Features/ArmyTargeting/Hooks/Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.cs", "ArmyTargeting", "Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.ReachedKnownDeadEnd", "return false;")]
    [DataRow("Main/Features/ArmyTargeting/Hooks/AiMilitaryBehavior_CalculateDistanceScoreForBesieging_Patch.cs", "ArmyTargeting", "AiMilitaryBehavior_CalculateDistanceScoreForBesieging_Patch.Postfix", "(none)")]
    [DataRow("Main/BehaviorTreeWrapper/BehaviorTreeBannerlordWrapper.cs", "BehaviorTreeWrapper", "BehaviorTreeBannerlordWrapper.AddBehaviorTree.BuildThrew", "return null;")]
    public void SweptSite_CallsSwallowed_AndKeepsItsFallback(string file, string feature, string site, string fallback)
    {
        var masks = SilentCatchScan.Masks(RepoPaths.ReadSource(file));
        var call = new Regex($@"\bOutcomes\.Swallowed\(\s*""{Regex.Escape(feature)}"",\s*""{Regex.Escape(site)}"",\s*ex\s*\);");
        var hits = SilentCatchScan.Catches(masks)
            .Where(c => c.Parsed && call.IsMatch(Body(masks, c)))
            .ToList();

        Assert.AreEqual(1, hits.Count, $"{file}: expected one catch holding {call}");
        if (fallback != "(none)")
            StringAssert.Contains(Body(masks, hits[0]), fallback, $"{file}: {site} lost its fallback");
    }

    [TestMethod]
    public void BehaviorTreeWrapper_ReportsEveryFailurePath_AndKeepsTheScreenMessages()
    {
        var code = SilentCatchScan.Masks(RepoPaths.ReadSource("Main/BehaviorTreeWrapper/BehaviorTreeBannerlordWrapper.cs")).NoComments;

        Assert.AreEqual(4, Regex.Matches(code, @"InformationManager\.DisplayMessage\(").Count, "the four screen messages stay");
        foreach (var path in new[] { "NoMissionLogic", "DuplicateTree", "UnknownTree", "BuildThrew" })
        {
            var site = $@"\bOutcomes\.Swallowed\(\s*""BehaviorTreeWrapper"",\s*""BehaviorTreeBannerlordWrapper\.AddBehaviorTree\.{path}"",";
            Assert.AreEqual(1, Regex.Matches(code, site).Count, path);
        }

        StringAssert.Contains(code, "logic.trees[agent] = behaviorTree;", "the store, and so the duplicate overwrite, stays");
    }

    private static string Body(SourceMasks masks, CatchBlock c) =>
        masks.NoComments.Substring(c.BodyStart, c.BodyEnd - c.BodyStart + 1);

    // Scanner fixtures: (source, catches parsed, silent, unparsed).
    [DataTestMethod]
    [DataRow("try { X(); } catch { }", 1, 1, 0)]
    [DataRow("try { } catch (Exception) { return true; }", 1, 1, 0)]
    [DataRow("try { } catch (Exception ex) { _logger.LogError(\"x\"); }", 1, 0, 0)]
    [DataRow("try { } catch (Exception ex) { Outcomes.Swallowed(\"F\", \"S\", ex); return false; }", 1, 0, 0)]
    [DataRow("try { } catch { throw; }", 1, 0, 0)]
    [DataRow("try { } catch (Exception ex) { throw new InvalidOperationException(\"x\", ex); }", 1, 0, 0)]
    [DataRow("try { } catch { /* swallow-ok: logger threw */ }", 1, 0, 0)]
    [DataRow("try { } catch { // swallow-ok: reason\n}", 1, 0, 0)]
    [DataRow("try { } catch { /* swallow-ok: */ }", 1, 1, 0)]
    [DataRow("try { } catch { // swallow-ok:\n}", 1, 1, 0)]
    [DataRow("try { } catch { var s = \"// swallow-ok: x\"; }", 1, 1, 0)]
    [DataRow("try { } catch { /* fail open, LogError later */ }", 1, 1, 0)]
    [DataRow("try { } catch { var s = \"LogError(\"; }", 1, 1, 0)]
    [DataRow("try { } catch { var s = \"}\"; }\nvoid After() { try { } catch { Log(\"x\"); } }", 2, 1, 0)]
    [DataRow("try { } catch { var c = '}'; var d = '{'; var e = '\\''; }", 1, 1, 0)]
    [DataRow("try { } catch { var s = @\"a\"\"}\"; }", 1, 1, 0)]
    [DataRow("try { } catch { var s = $\"{{}} {x} {(y ? \"}\" : \"{\")}\"; }", 1, 1, 0)]
    [DataRow("try { } catch { var s = $@\"}}{x}\"\"{{\"; var t = @$\"{{{y}\"; }", 1, 1, 0)]
    [DataRow("try { } catch { // }\n}", 1, 1, 0)]
    [DataRow("try { } catch (Exception ex) when (ex is IOException) { }", 1, 1, 0)]
    [DataRow("try { } catch (Exception ex) when (Filter(ex)) { _logger.LogWarning(\"x\"); }", 1, 0, 0)]
    [DataRow("// catch { }\nvar s = \"catch { }\";", 0, 0, 0)]
    [DataRow("int catchCount = 0;", 0, 0, 0)]
    [DataRow("try { } catch (Exception ex) Foo();", 0, 0, 1)]
    [DataRow("try { } catch (Exception ex) { Debug.Print(\"x\"); }", 1, 0, 0)]
    [DataRow("try { } catch (Exception ex) { Fail(\"x\"); }", 1, 0, 0)]
    [DataRow("try { } catch (Exception ex) { return RethrowStackPreserver.PreserveForRethrow(ex, null); }", 1, 0, 0)]
    public void Scan_CountsCatchesSilentAndUnparsed(string source, int parsed, int silent, int unparsed)
    {
        var catches = SilentCatchScan.Catches(source);

        Assert.AreEqual(parsed, catches.Count(c => c.Parsed), "parsed catches");
        Assert.AreEqual(silent, catches.Count(c => c.Silent), "silent catches");
        Assert.AreEqual(unparsed, catches.Count(c => !c.Parsed), "unparsed catches");
    }

    [TestMethod]
    public void Scan_ReportsTheLineOfTheCatchKeyword()
    {
        Assert.AreEqual(1, SilentCatchScan.Catches("try { X(); } catch { }").Single().Line);
        Assert.AreEqual(3, SilentCatchScan.Catches("try\n{ }\ncatch\n{ }").Single().Line);
    }

    [TestMethod]
    public void Scan_NestedEmptyCatch_IsSilentOnItsOwnLine_AndTheLoggingOuterIsNot()
    {
        const string source = "try { }\ncatch (Exception ex)\n{\n    try { _logger.LogError(\"x\"); }\n    catch { }\n}";

        var catches = SilentCatchScan.Catches(source);

        Assert.AreEqual(2, catches.Count);
        Assert.AreEqual(5, catches.Single(c => c.Silent).Line);
    }

    [TestMethod]
    public void Masks_KeepLengthAndLineBreaks_AndBlankOnlyWhatEachMaskBlanks()
    {
        const string source = "var s = \"a\\\"b\"; // LogError(\n/* x\ny */ char c = 'q';";

        var masks = SilentCatchScan.Masks(source);

        foreach (var mask in new[] { masks.Code, masks.CommentVisible, masks.NoComments })
        {
            Assert.AreEqual(source.Length, mask.Length);
            Assert.AreEqual(source.Count(ch => ch == '\n'), mask.Count(ch => ch == '\n'));
        }

        StringAssert.DoesNotMatch(masks.Code, new System.Text.RegularExpressions.Regex("LogError|a.b|q"));
        StringAssert.Contains(masks.CommentVisible, "LogError(");
        Assert.IsFalse(masks.CommentVisible.Contains("a\\\"b"), "string contents are blank in the comment-visible mask");
        StringAssert.Contains(masks.NoComments, "\"a\\\"b\"");
        Assert.IsFalse(masks.NoComments.Contains("LogError"), "comments are blank in the no-comments mask");
    }

    [DataTestMethod]
    [DataRow("Main/Features/X/Hooks/Thing.cs", "class Thing { }", true)]
    [DataRow("Main/Features/X/Thing.cs", "[HarmonyPatch(typeof(A), \"B\")]\nclass Thing { }", true)]
    [DataRow("Main/Features/X/Thing.cs", "[HarmonyLib.HarmonyPatch]\nclass Thing { }", true)]
    [DataRow("Main/Features/X/Thing.cs", "static class Patch12_Thing { }", true)]
    [DataRow("Main/Features/X/Thing.cs", "class Thing_Patch { }", true)]
    [DataRow("Main/Features/X/Thing.cs", "static class ThingPatches { }", true)]
    [DataRow("Main/Features/X/Thing.cs", "[HarmonyPatchCategory(\"x\")]\nclass Thing { }", false)]
    [DataRow("Main/Features/X/Thing.cs", "class HarmonyPatchInspector { }", false)]
    [DataRow("Main/Features/X/Thing.cs", "class PatchCategoryIndex { }", false)]
    [DataRow("Main/Features/X/Thing.cs", "// [HarmonyPatch]\nvar s = \"class Patch1\";", false)]
    [DataRow("Main/Features/Hooks.cs", "class Thing { }", false)]
    public void IsSelected_FollowsThePathAndPatchRules(string path, string source, bool expected)
    {
        Assert.AreEqual(expected, SilentCatchScan.IsSelected(path, source));
    }
}
