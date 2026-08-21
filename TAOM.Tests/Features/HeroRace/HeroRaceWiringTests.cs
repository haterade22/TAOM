using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HeroRace.Hooks;

namespace TAOM.Tests.Features.HeroRace;

/// <summary>
/// Wiring regression guard for Patch72.
///
/// <para>The bug this whole changeset fixed was "registered in IoC, invoked by nothing":
/// <c>CharacterTableauService</c> sat in <c>HeroRaceIoC</c> for months while the 3D framing offsets
/// it consumed were parsed and thrown away, and every test stayed green throughout. The replacement
/// hangs off two lines that no other test touches: the <c>Initialize</c> call in
/// <c>HeroRaceIoC</c>, and the category string in <c>SubModule.cs</c>.</para>
///
/// <para>Both fail silently. Drop the <c>Initialize</c> and the patch's own null guard turns the
/// feature into vanilla framing with no log line; drop or mistype the category string and Harmony is
/// never asked to apply the postfix at all. <c>Patch72TableauRacePositionBindingTests</c> proves the
/// patch COULD bind; it cannot prove anything asks it to. Same reasoning and same shape as
/// <c>BannerTripletOrderingTests</c> and <c>SiegeDismountWiringTests</c>.</para>
/// </summary>
[TestClass]
public class HeroRaceWiringTests
{
    private const string Category = "Patch72_TableauRacePosition";

    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));

    private static string ReadSource(params string[] parts)
    {
        var path = Path.Combine(new[] { RepoRoot }.Concat(parts).ToArray());
        Assert.IsTrue(File.Exists(path), $"Expected source file not found: {path}");
        return File.ReadAllText(path);
    }

    [TestMethod]
    public void HeroRaceIoC_InitializesThePositionPatch()
    {
        var src = ReadSource("Main", "Features", "HeroRace", "HeroRaceIoC.cs");

        StringAssert.Contains(
            src, "CharacterTableau_RefreshCharacterTableau_PositionPatch.Initialize",
            "HeroRaceIoC no longer hands the position service to Patch72. The patch null-guards a "
            + "missing service, so the 3D race framing would silently revert to vanilla with no error.");
    }

    [TestMethod]
    public void HeroRaceIoC_RegistersTheStoreAndPositionService()
    {
        var src = ReadSource("Main", "Features", "HeroRace", "HeroRaceIoC.cs");

        StringAssert.Contains(src, "IRacePositionStore, RacePositionStore",
            "The single owner of both framing configs is no longer registered.");
        StringAssert.Contains(src, "ITableauPositionService, TableauPositionService",
            "The 3D framing service is no longer registered.");
    }

    [TestMethod]
    public void SubModule_AppliesThePatch72Category()
    {
        var src = ReadSource("Main", "SubModule.cs");

        StringAssert.Contains(
            src, "\"" + Category + "\",",
            "SubModule.cs no longer lists " + Category + " in the guarded preview-category loop, so "
            + "Harmony is never asked to apply the postfix and the offsets go dead silently.");
    }

    // The category string is matched case-sensitively by Harmony. A typo on either side is a dead
    // patch with no error, so pin that the two literals are actually the same string.
    [TestMethod]
    public void PatchCategoryAttribute_MatchesTheStringSubModuleApplies()
    {
        var attribute = typeof(CharacterTableau_RefreshCharacterTableau_PositionPatch)
            .GetCustomAttributes(typeof(HarmonyPatchCategory), inherit: false)
            .Cast<HarmonyPatchCategory>()
            .SingleOrDefault();

        Assert.IsNotNull(attribute,
            "Patch72 has no [HarmonyPatchCategory]. TAOM never calls Harmony.PatchAll, so a patch "
            + "without a category is unreachable dead code.");
        Assert.AreEqual(Category, attribute.info.category,
            "The [HarmonyPatchCategory] literal drifted from the string SubModule.cs applies.");

        var subModule = ReadSource("Main", "SubModule.cs");
        StringAssert.Contains(subModule, "\"" + attribute.info.category + "\"",
            "SubModule.cs does not apply the category this patch actually declares.");
    }

    // Apply timing: the preview categories are applied together in one guarded loop so that a single
    // throwing category cannot silently prevent the rest. Patch72 belongs in that batch, next to the
    // other CharacterTableau patches, not in an unguarded call somewhere else.
    [TestMethod]
    public void Patch72_IsAppliedInTheGuardedPreviewBatch()
    {
        var src = ReadSource("Main", "SubModule.cs");

        var batchStart = src.IndexOf("\"Patch1_FirstTimeInit\"", StringComparison.Ordinal);
        var batchEnd = src.IndexOf("\"Patch67_TableauResidencyDiag\"", StringComparison.Ordinal);
        Assert.IsTrue(batchStart >= 0 && batchEnd > batchStart,
            "The guarded preview-category batch in SubModule.cs could not be located; this test needs updating.");

        var categoryIndex = src.IndexOf("\"" + Category + "\"", StringComparison.Ordinal);
        Assert.IsTrue(categoryIndex > batchStart,
            Category + " is not inside the guarded preview-category batch, so a failure applying it "
            + "would not be isolated the way the other tableau categories are.");
    }
}
