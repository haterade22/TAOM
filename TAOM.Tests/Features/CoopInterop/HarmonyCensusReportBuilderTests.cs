using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.CoopInterop.Diagnostics;

namespace TAOM.Tests.Features.CoopInterop;

/// <summary>
/// Pins the Harmony census report.
///
/// This report is the sanctioned substitute for decompiling a third-party co-op mod. The
/// BannerlordTogether package ships an explicit no-decompile / no-AI-analysis policy from its
/// copyright holders, and TAOM honours it — so instead of reading their assembly we read
/// HarmonyLib's OWN public runtime registry, which reports owner ids, patch kinds and reflection
/// metadata for every patch in the process. That is enough to answer the questions the co-op work
/// actually depends on: which methods both mods patch, whether two transpilers meet on one method,
/// and what the other mod's Harmony id is.
///
/// The boundary is load-bearing, so <see cref="BuildReport_NeverEmitsMethodBodyOrIlContent"/> pins
/// it: the report must never grow a field carrying IL or decompiled bodies.
/// </summary>
[TestClass]
public class HarmonyCensusReportBuilderTests
{
    private readonly HarmonyCensusReportBuilder _sut = new();

    private static HarmonyCensusMethod Method(string ns, string name, params (string Owner, string Type)[] patches) =>
        new(ns, name, patches.Select(p => new HarmonyCensusPatch(p.Owner, p.Type)).ToList());

    private static string Joined(IReadOnlyList<string> lines) => string.Join("\n", lines);

    [TestMethod]
    public void BuildReport_NoPatches_ProducesHeaderAndDoesNotThrow()
    {
        var lines = _sut.BuildReport(new List<HarmonyCensusMethod>(), Array.Empty<string>());

        Assert.IsTrue(lines.Count >= 1);
        StringAssert.Contains(Joined(lines), "totalPatchedMethods=0");
    }

    [TestMethod]
    public void BuildReport_NullMethods_ReturnsHeaderOnly()
    {
        var lines = _sut.BuildReport(null, null);

        Assert.IsTrue(lines.Count >= 1);
        StringAssert.Contains(Joined(lines), "totalPatchedMethods=0");
    }

    [TestMethod]
    public void BuildReport_CountsOwnersAndSortsByDescendingPatchCount()
    {
        var lines = _sut.BuildReport(new[]
        {
            Method("TaleWorlds.CampaignSystem", "A.M1", ("quiet.owner", "Prefix")),
            Method("TaleWorlds.CampaignSystem", "A.M2", ("loud.owner", "Prefix")),
            Method("TaleWorlds.CampaignSystem", "A.M3", ("loud.owner", "Postfix")),
        }, Array.Empty<string>());

        var ownerLines = lines.Where(l => l.Contains("owner=")).ToList();
        Assert.IsTrue(ownerLines[0].Contains("loud.owner"), "owners must be ordered by descending patch count");
        StringAssert.Contains(ownerLines[0], "patches=2");
    }

    [TestMethod]
    public void BuildReport_RollsUpTargetNamespacesPerOwner()
    {
        var lines = _sut.BuildReport(new[]
        {
            Method("TaleWorlds.CampaignSystem", "A.M1", ("some.owner", "Prefix")),
            Method("TaleWorlds.CampaignSystem", "A.M2", ("some.owner", "Prefix")),
            Method("TaleWorlds.MountAndBlade", "B.M1", ("some.owner", "Prefix")),
        }, Array.Empty<string>());

        var text = Joined(lines);
        StringAssert.Contains(text, "TaleWorlds.CampaignSystem(2)");
        StringAssert.Contains(text, "TaleWorlds.MountAndBlade(1)");
    }

    [TestMethod]
    public void BuildReport_MethodPatchedByTaomAndForeignOwner_IsReportedAsContested()
    {
        var lines = _sut.BuildReport(new[]
        {
            Method("TaleWorlds.CampaignSystem.Actions", "DeclareWarAction.ApplyInternal",
                ("com.taom.mod", "Prefix"), ("some.coop.mod", "Prefix")),
        }, Array.Empty<string>());

        var text = Joined(lines);
        StringAssert.Contains(text, "contested");
        StringAssert.Contains(text, "DeclareWarAction.ApplyInternal");
        StringAssert.Contains(text, "some.coop.mod");
    }

    [TestMethod]
    public void BuildReport_MethodPatchedOnlyByTaom_IsNotContested()
    {
        var lines = _sut.BuildReport(new[]
        {
            Method("TaleWorlds.CampaignSystem", "A.M1", ("com.taom.mod", "Prefix"), ("TAOM.HarmonyGuard", "Finalizer")),
        }, Array.Empty<string>());

        Assert.IsFalse(Joined(lines).Contains("contested "),
            "two TAOM-owned patches on one method are not a cross-mod conflict");
    }

    [TestMethod]
    public void BuildReport_TwoTranspilersFromDifferentOwners_IsFlaggedSeparately()
    {
        // The highest-risk Harmony conflict class: two IL rewrites compounding on one method.
        var lines = _sut.BuildReport(new[]
        {
            Method("TaleWorlds.CampaignSystem", "Risky.Method",
                ("com.taom.mod", "Transpiler"), ("other.mod", "Transpiler")),
        }, Array.Empty<string>());

        var text = Joined(lines);
        StringAssert.Contains(text, "TRANSPILER CONFLICT");
        StringAssert.Contains(text, "Risky.Method");
    }

    [TestMethod]
    public void BuildReport_SingleTranspilerPlusPostfix_IsNotATranspilerConflict()
    {
        var lines = _sut.BuildReport(new[]
        {
            Method("TaleWorlds.CampaignSystem", "Fine.Method",
                ("com.taom.mod", "Transpiler"), ("other.mod", "Postfix")),
        }, Array.Empty<string>());

        Assert.IsFalse(Joined(lines).Contains("TRANSPILER CONFLICT"));
    }

    [TestMethod]
    public void BuildReport_CoopModuleIds_AreNamedInTheHeader()
    {
        var lines = _sut.BuildReport(new List<HarmonyCensusMethod>(), new[] { "BannerlordTogether" });

        var header = lines[0];
        StringAssert.Contains(header, "coopActive=true");
        StringAssert.Contains(header, "BannerlordTogether");
    }

    [TestMethod]
    public void BuildReport_NoCoopModules_HeaderSaysInactive()
    {
        var lines = _sut.BuildReport(new List<HarmonyCensusMethod>(), Array.Empty<string>());

        StringAssert.Contains(lines[0], "coopActive=false");
    }

    [TestMethod]
    public void BuildReport_UnknownOwner_RendersAsPlaceholderNotNull()
    {
        var lines = _sut.BuildReport(new[]
        {
            new HarmonyCensusMethod("Ns", "A.M", new[] { new HarmonyCensusPatch(null!, "Prefix") }),
        }, Array.Empty<string>());

        var text = Joined(lines);
        Assert.IsFalse(text.Contains("owner=null"));
        StringAssert.Contains(text, "(unknown)");
    }

    [TestMethod]
    public void BuildReport_MoreContestedMethodsThanRowCap_EmitsSuppressionLine()
    {
        // A truncated census that reads as a complete one is the failure this cap's own comment
        // exists to prevent — on the only sanctioned view we have into another mod's patches.
        const int over = 5;
        var methods = Enumerable.Range(0, HarmonyCensusReportBuilder.MaxDetailRows + over)
            .Select(i => Method("TaleWorlds.CampaignSystem", $"Type{i}.M",
                ("com.taom.mod", "Prefix"), ("other.mod", "Postfix")))
            .ToList();

        var lines = _sut.BuildReport(methods, Array.Empty<string>());

        // The suppression line also contains the word "contested", so match the row shape instead.
        Assert.AreEqual(HarmonyCensusReportBuilder.MaxDetailRows,
            lines.Count(l => l.Contains("]   contested ")),
            "exactly MaxDetailRows contested rows should be printed");
        Assert.AreEqual(1, lines.Count(l => l.Contains($"{over} further contested method(s) not listed")),
            "the suppression line must state how many rows were dropped");
    }

    [TestMethod]
    public void BuildReport_ShieldFinalizerPlusOneForeignOwner_IsNotContested()
    {
        // PatchShield attaches a finalizer to nearly every patched method in the process. Counting
        // it as an owner made every foreign-patched method "contested", saturating the row cap with
        // noise and answering "who patched anything" instead of "where do the two mods collide".
        var lines = _sut.BuildReport(new[]
        {
            Method("TaleWorlds.CampaignSystem", "Some.Method",
                ("TAOM.Dependencies.Foundation.PatchShield", "Finalizer"), ("other.mod", "Prefix")),
        }, Array.Empty<string>());

        Assert.IsFalse(Joined(lines).Contains("contested "));
    }

    [TestMethod]
    public void BuildReport_UiExtenderExOwnerForTaom_CountsAsTaomNotForeign()
    {
        // UIExtenderEx registers TAOM's mixin patches under its own per-module id, so without
        // recognising it a TAOM-only patch stack reads as a cross-mod conflict.
        var lines = _sut.BuildReport(new[]
        {
            Method("TaleWorlds.CampaignSystem.ViewModelCollection", "MapInfoVM.Refresh",
                ("com.taom.mod", "Postfix"), ("bannerlord.uiextender.ex.viewmodels.TAOM", "Prefix")),
        }, Array.Empty<string>());

        Assert.IsFalse(Joined(lines).Contains("contested "));
    }

    [TestMethod]
    public void BuildReport_NeverEmitsMethodBodyOrIlContent()
    {
        // Pins the copyright boundary. The census reads Harmony's registry and reflection metadata
        // only; if someone later adds an IL-bearing field to the model, this fails loudly.
        var modelProperties = typeof(HarmonyCensusMethod).GetProperties()
            .Concat(typeof(HarmonyCensusPatch).GetProperties())
            .Select(p => p.Name.ToLowerInvariant())
            .ToList();

        foreach (var forbidden in new[] { "il", "body", "instructions", "opcodes", "bytes", "source" })
        {
            Assert.IsFalse(modelProperties.Any(p => p.Contains(forbidden)),
                $"census model gained a '{forbidden}'-bearing property — the report must never carry " +
                "method bodies or IL from another mod's assembly.");
        }
    }
}
