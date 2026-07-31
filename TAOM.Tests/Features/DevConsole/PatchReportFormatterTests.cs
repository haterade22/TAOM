using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.DevConsole;

namespace TAOM.Tests.Features.DevConsole;

/// <summary>
/// Pins the `taom.print_patches` report — declared `[HarmonyPatchCategory]` versus what Harmony
/// actually applied.
///
/// Why it matters: TAOM applies patches by category (`PatchAll` is never called), so a category that
/// is declared but never registered, or that silently fails to apply after an engine bump, is dead
/// code with no error and no warning. Today "did Patch63 apply?" is only answerable by grepping
/// `taom_debug`. Two behaviours are deliberately encoded rather than treated as bugs: manual patches
/// carry no category by design, and `Patch0_BattleScenes` is deliberately disabled — so
/// declared-but-not-applied is sometimes the correct output, and the report has to say which is which
/// rather than crying wolf.
/// </summary>
[TestClass]
public class PatchReportFormatterTests
{
    private static readonly IReadOnlyList<string> NoStrings = new string[0];
    private static readonly IReadOnlyDictionary<string, int> NoCounts = new Dictionary<string, int>();

    [TestMethod]
    public void Format_DeclaredAndApplied_MarksCategoryApplied()
    {
        var report = PatchReportFormatter.Format(
            declared: new[] { "Patch10_Foo" },
            appliedCounts: new Dictionary<string, int> { ["Patch10_Foo"] = 3 },
            uncategorized: NoStrings, otherOwners: NoCounts, totalPatchedMethods: 3, filter: null);

        StringAssert.Contains(report, "Patch10_Foo");
        StringAssert.Contains(report, "APPLIED");
        StringAssert.Contains(report, "3");
    }

    [TestMethod]
    public void Format_DeclaredButNotApplied_MarksCategoryNotApplied()
    {
        var report = PatchReportFormatter.Format(
            declared: new[] { "Patch10_Foo", "Patch11_Bar" },
            appliedCounts: new Dictionary<string, int> { ["Patch10_Foo"] = 1 },
            uncategorized: NoStrings, otherOwners: NoCounts, totalPatchedMethods: 1, filter: null);

        StringAssert.Contains(report, "Patch11_Bar");
        StringAssert.Contains(report, "NOT APPLIED");
    }

    /// <summary>
    /// Manual patches have no category attribute by design. Reporting them as "missing categories"
    /// would make the command cry wolf on every run, so they get their own bucket.
    /// </summary>
    [TestMethod]
    public void Format_UncategorizedTaomPatches_GetTheirOwnBucketNotAMissingCategory()
    {
        var report = PatchReportFormatter.Format(
            declared: new[] { "Patch10_Foo" },
            appliedCounts: new Dictionary<string, int> { ["Patch10_Foo"] = 1 },
            uncategorized: new[] { "TAOM.Hooks.ManualThing.Prefix" },
            otherOwners: NoCounts, totalPatchedMethods: 2, filter: null);

        StringAssert.Contains(report, "uncategorized");
        StringAssert.Contains(report, "ManualThing");
        Assert.IsFalse(report.Contains("ManualThing NOT APPLIED"));
    }

    [TestMethod]
    public void Format_OtherOwners_AreCountedSeparatelyFromTaom()
    {
        var report = PatchReportFormatter.Format(
            declared: new[] { "Patch10_Foo" },
            appliedCounts: new Dictionary<string, int> { ["Patch10_Foo"] = 1 },
            uncategorized: NoStrings,
            otherOwners: new Dictionary<string, int> { ["com.other.mod"] = 7 },
            totalPatchedMethods: 8, filter: null);

        StringAssert.Contains(report, "com.other.mod");
        StringAssert.Contains(report, "7");
    }

    [TestMethod]
    public void Format_Filter_NarrowsToMatchingCategoriesCaseInsensitively()
    {
        var report = PatchReportFormatter.Format(
            declared: new[] { "Patch10_Banner", "Patch11_Siege" },
            appliedCounts: new Dictionary<string, int> { ["Patch10_Banner"] = 1, ["Patch11_Siege"] = 1 },
            uncategorized: NoStrings, otherOwners: NoCounts, totalPatchedMethods: 2, filter: "banner");

        StringAssert.Contains(report, "Patch10_Banner");
        Assert.IsFalse(report.Contains("Patch11_Siege"));
    }

    /// <summary>
    /// An empty Harmony state is a valid reading (patches not applied yet at this lifecycle point),
    /// not a crash. The report must render it as a zero report.
    /// </summary>
    [TestMethod]
    public void Format_NothingDeclaredOrApplied_RendersAZeroReportWithoutThrowing()
    {
        var report = PatchReportFormatter.Format(
            declared: NoStrings, appliedCounts: NoCounts,
            uncategorized: NoStrings, otherOwners: NoCounts, totalPatchedMethods: 0, filter: null);

        Assert.IsFalse(string.IsNullOrWhiteSpace(report));
        StringAssert.Contains(report, "0");
    }

    [TestMethod]
    public void Format_NullInputs_DoNotThrow()
    {
        var report = PatchReportFormatter.Format(null, null, null, null, 0, null);

        Assert.IsFalse(string.IsNullOrWhiteSpace(report));
    }

    /// <summary>The summary is what a triage session reads first — it must count both sides.</summary>
    [TestMethod]
    public void Format_Summary_CountsAppliedAndMissingCategories()
    {
        var report = PatchReportFormatter.Format(
            declared: new[] { "A", "B", "C" },
            appliedCounts: new Dictionary<string, int> { ["A"] = 1, ["B"] = 2 },
            uncategorized: NoStrings, otherOwners: NoCounts, totalPatchedMethods: 3, filter: null);

        StringAssert.Contains(report, "2 applied");
        StringAssert.Contains(report, "1 not applied");
    }
}
