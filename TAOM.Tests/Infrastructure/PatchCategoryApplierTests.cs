using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
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
    internal const string UnresolvableCategory = "Plan009_UnresolvableTargetProbe";

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

        Assert.IsNull(sut.TakeFailureSummary("game initialization"));
    }

    [TestMethod]
    public void TakeFailureSummary_NamesThePhaseAndEveryFailedCategoryInOrder()
    {
        var sut = ApplierFailingOn("Patch_B", "Patch_D");
        foreach (var category in new[] { "Patch_A", "Patch_B", "Patch_C", "Patch_D" })
            sut.TryApply(category);

        Assert.AreEqual(
            "TAOM: patch groups failed to apply during game initialization: Patch_B, Patch_D. "
            + "Those fixes are off this session; the TAOM log names the cause.",
            sut.TakeFailureSummary("game initialization"));
    }

    [TestMethod]
    public void TakeFailureSummary_ClearsTheList_SoTheNextPhaseReportsOnlyItsOwnFailures()
    {
        var sut = ApplierFailingOn("Patch_B", "Patch_M");

        sut.TryApply("Patch_B");
        Assert.IsNotNull(sut.TakeFailureSummary("module load"));
        Assert.IsNull(sut.TakeFailureSummary("module load"));

        sut.TryApply("Patch_M");
        var second = sut.TakeFailureSummary("mission start")!;
        StringAssert.Contains(second, "Patch_M");
        Assert.IsFalse(second.Contains("Patch_B"), second);
    }

    // Pins the premise against the pinned Harmony 2.4.2: a category whose target does not resolve
    // THROWS out of PatchCategory (it is not a silent no-op), with the cause in the inner exception.
    [TestMethod]
    public void RealHarmony_ACategoryWhoseTargetDoesNotResolve_ThrowsHarmonyException()
    {
        var harmony = new Harmony("taom.tests.plan009.premise");

        var ex = Assert.ThrowsException<HarmonyException>(
            () => harmony.PatchCategory(typeof(PatchCategoryApplierTests).Assembly, UnresolvableCategory));

        StringAssert.Contains(ex.InnerException?.Message ?? string.Empty, "Undefined target method");
    }

    [TestMethod]
    public void TryApply_RealHarmonyCategoryWhoseTargetDoesNotResolve_ReturnsFalseAndLogsTheMissingTarget()
    {
        var harmony = new Harmony("taom.tests.plan009.applier");
        var sut = new PatchCategoryApplier(
            category => harmony.PatchCategory(typeof(PatchCategoryApplierTests).Assembly, category), _logger);

        Assert.IsFalse(sut.TryApply(UnresolvableCategory));
        _logger.Received(1).LogError(Arg.Is<string>(s =>
            s.Contains(UnresolvableCategory) && s.Contains("Undefined target method")));
    }

    // Source gate: the only direct Harmony PatchCategory call left in Main is the one inside the
    // applier's delegate in SubModule. A new bare call would re-open the fail-as-a-group hole.
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

        Assert.AreEqual(1, hits.Count,
            "Every category must be applied with TryPatchCategory(\"...\") in SubModule.cs. Direct calls found: "
            + string.Join(" | ", hits));
        StringAssert.Contains(hits[0], "SubModule.cs: ");
        StringAssert.Contains(hits[0], "PatchCategory(typeof(SubModule).Assembly, category)");
    }
}

/// <summary>
/// A patch class whose target cannot resolve, for the real-Harmony tests above. Nothing outside
/// <c>PatchCategoryApplierTests</c> applies this category.
/// </summary>
[HarmonyPatchCategory(PatchCategoryApplierTests.UnresolvableCategory)]
[HarmonyPatch(typeof(PatchCategoryApplierTests), "NoSuchMethod_Plan009")]
internal static class Plan009UnresolvableTargetProbe
{
    internal static void Postfix() { }
}
