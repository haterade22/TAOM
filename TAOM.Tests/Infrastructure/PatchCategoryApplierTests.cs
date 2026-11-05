using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Localization;
using TAOM.Core.Logging;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// Pins <c>PatchCategoryApplier</c>: one category whose target no longer resolves must cost only
/// that category, name itself in the log, and appear in the phase's on-screen summary. Harmony
/// 2.4.2's <c>PatchCategory</c> has no catch, so before this guard a single drifted binding failed
/// the module load (OnSubModuleLoad) or skipped every later category in the game-init batch.
/// </summary>
[TestClass]
public class PatchCategoryApplierTests
{
    internal const string UnresolvableCategory = "Test_UnresolvableTargetProbe";

    private static readonly Regex CommentPattern =
        new(@"/\*.*?\*/|//[^\n]*", RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex DirectPatchCategoryCall =
        new(@"\.PatchCategory\s*\(", RegexOptions.Compiled);

    private IModLogger _logger = null!;
    private List<string> _applied = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _applied = new List<string>();
    }

    private PatchCategoryApplier ApplierFailingOn(params string[] failing)
    {
        return new PatchCategoryApplier(category =>
        {
            if (Array.IndexOf(failing, category) >= 0)
                throw new InvalidOperationException("Undefined target method for patch method " + category + "_Probe");
            _applied.Add(category);
        }, _logger);
    }

    [TestMethod]
    public void Constructor_WithANullApplyAction_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new PatchCategoryApplier(null!, _logger));
    }

    [TestMethod]
    public void Constructor_WithANullLogger_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new PatchCategoryApplier(_ => { }, null!));
    }

    [TestMethod]
    public void TryApply_WhenTheApplySucceeds_ReturnsTrueAndLogsNoError()
    {
        var sut = ApplierFailingOn();

        Assert.IsTrue(sut.TryApply("Patch_A"));
        CollectionAssert.AreEqual(new[] { "Patch_A" }, _applied);
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void TryApply_WhenTheApplyThrows_ReturnsFalseAndLogsTheCategoryAndTheCause()
    {
        var sut = ApplierFailingOn("Patch_B");

        Assert.IsFalse(sut.TryApply("Patch_B"));
        _logger.Received(1).LogError(Arg.Is<string>(s =>
            s.Contains("[PatchApply]") && s.Contains("Patch_B") && s.Contains("FAILED")
            && s.Contains("Undefined target method for patch method Patch_B_Probe")));
    }

    // The regression this guard exists for: one bad category used to abort every later one.
    [TestMethod]
    public void TryApply_AfterAFailedCategory_StillAppliesTheNextOne()
    {
        var sut = ApplierFailingOn("Patch_B");

        sut.TryApply("Patch_A");
        sut.TryApply("Patch_B");
        sut.TryApply("Patch_C");

        CollectionAssert.AreEqual(new[] { "Patch_A", "Patch_C" }, _applied);
    }

    [TestMethod]
    public void TakeFailureSummary_WhenNothingFailed_ReturnsNull()
    {
        var sut = ApplierFailingOn();
        sut.TryApply("Patch_A");

        Assert.IsNull(sut.TakeFailureSummary(new TextObject("game initialization")));
    }

    // Harmony applies a category class by class with no rollback, so classes before the failing
    // one stay patched: the summary must not claim the whole group is off. The summary is a
    // registered {=taom_...} TextObject so the player reads it in their language; the category
    // ids stay literal.
    [TestMethod]
    public void TakeFailureSummary_NamesThePhaseAndEveryFailedCategoryInOrder()
    {
        var sut = ApplierFailingOn("Patch_B", "Patch_D");
        foreach (var category in new[] { "Patch_A", "Patch_B", "Patch_C", "Patch_D" })
            sut.TryApply(category);
        var phase = new TextObject("{=taom_test_phase}game initialization");

        var summary = sut.TakeFailureSummary(phase)!;

        Assert.AreEqual(
            "{=taom_patch_apply_failed}TAOM: patch groups failed to apply during {PHASE}: {GROUPS}. "
            + "Some fixes in those groups are off this session; the TAOM log names the cause.",
            summary.Value);
        Assert.AreEqual(phase.Value, Variable(summary, "PHASE"));
        Assert.AreEqual("Patch_B, Patch_D", Variable(summary, "GROUPS"));
    }

    [TestMethod]
    public void TakeFailureSummary_ClearsTheList_SoTheNextPhaseReportsOnlyItsOwnFailures()
    {
        var sut = ApplierFailingOn("Patch_B", "Patch_M");

        sut.TryApply("Patch_B");
        Assert.IsNotNull(sut.TakeFailureSummary(new TextObject("startup")));
        Assert.IsNull(sut.TakeFailureSummary(new TextObject("startup")));

        sut.TryApply("Patch_M");
        var second = sut.TakeFailureSummary(new TextObject("mission start"))!;
        Assert.AreEqual("Patch_M", Variable(second, "GROUPS"));
    }

    internal static string Variable(TextObject text, string tag)
    {
        Assert.IsTrue(text.GetVariableValue(tag, out var value), "no " + tag + " variable");
        return value.Value;
    }

    // Also pins the premise against the pinned Harmony 2.4.2: a class whose target does not
    // resolve THROWS out of its class processor rather than being a silent no-op. Goes through
    // PatchCategoryIndex, as SubModule does.
    [TestMethod]
    public void TryApply_RealHarmonyCategoryWhoseTargetDoesNotResolve_ReturnsFalseAndLogsTheMissingTarget()
    {
        var harmony = new Harmony("taom.tests.patchcategoryapplier");
        var index = PatchCategoryIndex.Build(typeof(PatchCategoryApplierTests).Assembly);
        var sut = new PatchCategoryApplier(category => index.Apply(harmony, category), _logger);

        Assert.IsFalse(sut.TryApply(UnresolvableCategory),
            "Harmony no longer throws on an unresolvable target; the guard's premise changed");
        _logger.Received(1).LogError(Arg.Is<string>(s =>
            s.Contains(UnresolvableCategory) && s.Contains("Undefined target method")));
    }

    // Source gate: no direct Harmony PatchCategory call is left in Main. SubModule applies every
    // category through TryPatchCategory, whose delegate goes through PatchCategoryIndex. A bare call
    // would re-open the fail-as-a-group hole, and Harmony's own category index fails every category
    // over one class whose attributes cannot be read.
    [TestMethod]
    public void MainSource_AppliesEveryPatchCategoryThroughTheGuardedHelper()
    {
        var hits = new List<string>();
        foreach (var file in Directory.GetFiles(RepoPaths.RepoPath("Main"), "*.cs", SearchOption.AllDirectories))
        {
            var sep = Path.DirectorySeparatorChar;
            if (file.Contains(sep + "obj" + sep) || file.Contains(sep + "bin" + sep))
                continue;

            var code = CommentPattern.Replace(File.ReadAllText(file), string.Empty);
            foreach (Match match in DirectPatchCategoryCall.Matches(code))
            {
                var lineStart = code.LastIndexOf('\n', match.Index) + 1;
                var lineEnd = code.IndexOf('\n', match.Index);
                if (lineEnd < 0) lineEnd = code.Length;
                hits.Add(Path.GetFileName(file) + ": " + code.Substring(lineStart, lineEnd - lineStart).Trim());
            }
        }

        Assert.AreEqual(0, hits.Count,
            "Every category must be applied with TryPatchCategory(\"...\") in SubModule.cs. Direct calls found: "
            + string.Join(" | ", hits));

        var subModule = CommentPattern.Replace(File.ReadAllText(RepoPaths.RepoPath("Main", "SubModule.cs")), string.Empty);
        StringAssert.Matches(subModule, new Regex(
            @"var (\w+) = PatchCategoryIndex\.Build\(typeof\(SubModule\)\.Assembly\);\s*"
            + @"_patches = new PatchCategoryApplier\(\s*category => \1\.Apply\(_harmony, category\),[^;]+;\s*"
            + @"_patches\.RecordSkippedClasses\(\1\.SkippedClasses\);"));
    }

    // Nothing receives InformationManager.DisplayMessage during OnSubModuleLoad: Native builds the
    // chat log (the only subscriber) in its OnBeforeInitialModuleScreenSetAsRoot, and the event
    // has no queue. A report there drains the failure list into nothing.
    [TestMethod]
    public void SubModuleSource_OnSubModuleLoad_DoesNotReportPatchFailures()
    {
        var body = SubModuleMethodBody("protected override void OnSubModuleLoad()");

        Assert.IsFalse(body.Contains("ReportPatchFailures("),
            "OnSubModuleLoad must leave its failures for the main-menu startup report");
    }

    // The initial screen clears the chat log after the splash video, so the startup report is an
    // inquiry (which it does not clear), shown once the Native inquiry subscriber exists.
    [TestMethod]
    public void SubModuleSource_MainMenuSetup_ReportsStartupFailuresInAnInquiry()
    {
        var body = SubModuleMethodBody("protected override void OnBeforeInitialModuleScreenSetAsRoot()");

        StringAssert.Contains(body,
            "ReportPatchFailures(new TextObject(\"{=taom_patch_apply_phase_startup}startup\"), persistent: true);");
    }

    // The notice, its inquiry title and its button are localized; every phase name is a
    // registered key, and the button reuses vanilla's own "Ok" row (global_strings.xml str_ok).
    [TestMethod]
    public void SubModuleSource_PatchFailureNotice_IsLocalized()
    {
        var body = SubModuleMethodBody("private void ReportPatchFailures(TextObject phase, bool persistent = false)");

        StringAssert.Contains(body, "new TextObject(\"{=taom_patch_apply_notice_title}TAOM\").ToString()");
        StringAssert.Contains(body, "new TextObject(\"{=oHaWR73d}Ok\").ToString()");
        Assert.IsFalse(body.Contains("\"OK\""), "the inquiry button is still a literal");

        var code = CommentPattern.Replace(File.ReadAllText(RepoPaths.RepoPath("Main", "SubModule.cs")), string.Empty);
        StringAssert.Contains(code, "ReportPatchFailures(new TextObject(\"{=taom_patch_apply_phase_game_init}game initialization\"));");
        StringAssert.Contains(code, "ReportPatchFailures(new TextObject(\"{=taom_patch_apply_phase_mission_start}mission start\"));");
        Assert.IsFalse(Regex.IsMatch(code, @"ReportPatchFailures\(\s*"""), "a phase name is still a literal");
    }

    // The three rewrites whose failure branch carries behaviour: Patch37 attaches its hooks only on
    // success, a Patch77 failure disables the Player Switcher, and the preview log says FAILED.
    [TestMethod]
    public void SubModuleSource_KeepsTheSideEffectsOfAFailedApply()
    {
        var code = CommentPattern.Replace(File.ReadAllText(RepoPaths.RepoPath("Main", "SubModule.cs")), string.Empty);

        StringAssert.Matches(code, new Regex(
            @"if \(TryPatchCategory\(""Patch37_CrashReport""\)\)\s*\{\s*IoC\.Resolve<[\w.]+AppDomainExceptionHook>\(\)\.Subscribe\(\);"));
        StringAssert.Matches(code, new Regex(
            @"if \(!TryPatchCategory\(""Patch77_PlayerSwitcher""\)\)\s*\{\s*IoC\.Resolve<[\w.]+IPlayerSwitchPolicyProvider>\(\)\s*\.DisableForSession\("));
        StringAssert.Matches(code, new Regex(
            @"if \(TryPatchCategory\(previewCategory\)\)[^;]+applied OK[^;]+;\s*else[^;]+FAILED"));
    }

    private static string SubModuleMethodBody(string signature)
    {
        var code = CommentPattern.Replace(File.ReadAllText(RepoPaths.RepoPath("Main", "SubModule.cs")), string.Empty);
        var start = code.IndexOf(signature, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "SubModule.cs no longer declares " + signature);

        var open = code.IndexOf('{', start);
        var depth = 0;
        for (var i = open; i < code.Length; i++)
        {
            if (code[i] == '{') depth++;
            else if (code[i] == '}' && --depth == 0)
                return code.Substring(open, i - open + 1);
        }

        Assert.Fail("Unbalanced braces after " + signature);
        return string.Empty;
    }
}

/// <summary>
/// A patch class whose target cannot resolve, for the real-Harmony test above. Nothing outside
/// <c>PatchCategoryApplierTests</c> applies this category.
/// </summary>
[HarmonyPatchCategory(PatchCategoryApplierTests.UnresolvableCategory)]
[HarmonyPatch(typeof(PatchCategoryApplierTests), "NoSuchMethod_UnresolvableTargetProbe")]
internal static class UnresolvableTargetProbe
{
    internal static void Postfix() { }
}
