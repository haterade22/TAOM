using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TAOM.Features.LotrIssues;
using TAOM.Features.LotrIssues.Templates;

namespace TAOM.Tests.Features.LotrIssues;

// NOTE: SandBox.dll is not on the unit-test host's probe path, so the 7 SandBox issue types resolve
// only in-game. These tests therefore pin the compile-checked 36 CampaignSystem types + the 7 declared
// SandBox names + the intended total of 43 — without forcing a SandBox.dll load.
[TestClass]
public class LotrIssueSuppressionTests
{
    [TestMethod]
    public void ExpectedVanillaIssueCount_Is43()
        => Assert.AreEqual(43, LotrIssueSuppression.ExpectedVanillaIssueCount);

    [TestMethod]
    public void CampaignSystemIssueTypes_AreExactly36()
        => Assert.AreEqual(36, LotrIssueSuppression.CampaignSystemIssueTypes.Count);

    [TestMethod]
    public void SandBoxIssueTypeNames_AreExactly7()
        => Assert.AreEqual(7, LotrIssueSuppression.SandBoxIssueTypeNames.Length);

    [TestMethod]
    public void CampaignSystemIssueTypes_AllDistinct()
    {
        var set = new HashSet<Type>(LotrIssueSuppression.CampaignSystemIssueTypes);
        Assert.AreEqual(LotrIssueSuppression.CampaignSystemIssueTypes.Count, set.Count);
    }

    [TestMethod]
    public void CampaignSystemIssueTypes_AllAreCampaignBehaviors()
    {
        foreach (var t in LotrIssueSuppression.CampaignSystemIssueTypes)
            Assert.IsTrue(typeof(CampaignBehaviorBase).IsAssignableFrom(t), $"{t.Name} is not a CampaignBehaviorBase");
    }

    [TestMethod]
    public void CampaignSystemIssueTypes_AllInIssuesNamespace()
    {
        foreach (var t in LotrIssueSuppression.CampaignSystemIssueTypes)
            Assert.AreEqual("TaleWorlds.CampaignSystem.Issues", t.Namespace, $"{t.Name} is in an unexpected namespace");
    }

    [TestMethod]
    public void DoesNotIncludeHostSpawner()
    {
        Assert.IsFalse(LotrIssueSuppression.CampaignSystemIssueTypes.Any(t => t.Name == "IssuesCampaignBehavior"));
        Assert.IsFalse(LotrIssueSuppression.SandBoxIssueTypeNames.Any(n => n.Contains("IssuesCampaignBehavior")));
    }

    [TestMethod]
    public void SandBoxIssueTypeNames_AllAssemblyQualifiedToSandBox()
    {
        foreach (var n in LotrIssueSuppression.SandBoxIssueTypeNames)
        {
            Assert.IsTrue(n.StartsWith("SandBox.Issues."), $"'{n}' not in SandBox.Issues");
            Assert.IsTrue(n.EndsWith(", SandBox"), $"'{n}' not assembly-qualified to SandBox");
        }
    }

    // --- Loaded-assembly type resolution (the in-game Type.GetType fix, 2026-07-21 crash report) ---
    // Type.GetType("T, SandBox") returns null in-game: SandBox.dll is LoadFrom-loaded from its module
    // folder, and the engine's AssemblyResolve only matches exact FullName. The resolver must instead
    // scan already-loaded assemblies by simple name.

    [TestMethod]
    public void ResolveTypesFromLoadedAssemblies_AssemblyNotLoaded_ReturnsEmpty()
    {
        var result = LotrIssueSuppression.ResolveTypesFromLoadedAssemblies(
            "TAOM.NoSuchAssembly", new[] { "SandBox.Issues.FamilyFeudIssueBehavior" }, AppDomain.CurrentDomain.GetAssemblies());
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void ResolveTypesFromLoadedAssemblies_MatchingAssembly_ResolvesTypeBySimpleName()
    {
        // Uses the test assembly itself to prove the mechanism without needing SandBox.dll.
        var ownAssembly = typeof(LotrIssueSuppressionTests).Assembly;
        var result = LotrIssueSuppression.ResolveTypesFromLoadedAssemblies(
            ownAssembly.GetName().Name,
            new[] { typeof(LotrIssueSuppressionTests).FullName },
            AppDomain.CurrentDomain.GetAssemblies());
        Assert.AreEqual(1, result.Count);
        Assert.AreSame(typeof(LotrIssueSuppressionTests), result[0]);
    }

    [TestMethod]
    public void ResolveTypesFromLoadedAssemblies_NullAssemblyName_ReturnsEmpty()
        => Assert.AreEqual(0, LotrIssueSuppression.ResolveTypesFromLoadedAssemblies(
            null, new[] { "SandBox.Issues.FamilyFeudIssueBehavior" }, AppDomain.CurrentDomain.GetAssemblies()).Count);

    [TestMethod]
    public void ResolveTypesFromLoadedAssemblies_NullTypeNames_ReturnsEmpty()
        => Assert.AreEqual(0, LotrIssueSuppression.ResolveTypesFromLoadedAssemblies(
            "SandBox", null, AppDomain.CurrentDomain.GetAssemblies()).Count);

    [TestMethod]
    public void ResolveTypesFromLoadedAssemblies_NullAssemblies_ReturnsEmpty()
        => Assert.AreEqual(0, LotrIssueSuppression.ResolveTypesFromLoadedAssemblies(
            "SandBox", new[] { "SandBox.Issues.FamilyFeudIssueBehavior" }, null).Count);

    [TestMethod]
    public void ResolveTypesFromLoadedAssemblies_TypeMissingInAssembly_SkipsIt()
    {
        var ownAssembly = typeof(LotrIssueSuppressionTests).Assembly;
        var result = LotrIssueSuppression.ResolveTypesFromLoadedAssemblies(
            ownAssembly.GetName().Name,
            new[] { "TAOM.Tests.DoesNotExist", typeof(LotrIssueSuppressionTests).FullName },
            AppDomain.CurrentDomain.GetAssemblies());
        Assert.AreEqual(1, result.Count);
        Assert.AreSame(typeof(LotrIssueSuppressionTests), result[0]);
    }

    // The cached property depends on whether SandBox.dll happened to be loaded when first evaluated
    // (test ordering), so pin only the two legal shapes: all 7 resolved, or graceful degradation to 36.
    [TestMethod]
    public void VanillaIssueBehaviorTypes_Always36Or43_NeverPartial()
    {
        var count = LotrIssueSuppression.VanillaIssueBehaviorTypes.Count;
        Assert.IsTrue(count == 36 || count == 43, $"unexpected resolved count {count}");
    }

    // The engine-bump canary: every declared SandBox short name must exist in the REAL SandBox.dll.
    // Deterministic — loads the assembly explicitly instead of relying on AppDomain state.
    [TestMethod]
    public void ResolveTypesFromLoadedAssemblies_RealSandBoxAssembly_ResolvesAll7()
    {
        var sandBox = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "SandBox");
        if (sandBox == null)
        {
            var candidate = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SandBox.dll");
            if (System.IO.File.Exists(candidate)) sandBox = Assembly.LoadFrom(candidate);
        }
        if (sandBox == null)
            Assert.Inconclusive("SandBox.dll not available in this test host");

        var resolved = LotrIssueSuppression.ResolveTypesFromLoadedAssemblies(
            "SandBox", LotrIssueSuppression.SandBoxIssueTypeFullNames, new[] { sandBox });
        Assert.AreEqual(7, resolved.Count,
            "a SandBox issue type name no longer resolves — engine bump renamed it? Update SandBoxIssueTypeNames.");
    }

    // --- Vanilla-issue classifier (drives the save-load safety-net sweep) ---

    [TestMethod]
    public void IsVanillaIssueType_CampaignSystemBehavior_True()
        => Assert.IsTrue(LotrIssueSuppression.IsVanillaIssueType(typeof(ArmyNeedsSuppliesIssueBehavior)));

    [TestMethod]
    public void IsVanillaIssueType_CampaignSystemNestedIssue_True()
    {
        var nested = typeof(ArmyNeedsSuppliesIssueBehavior)
            .GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(t => typeof(IssueBase).IsAssignableFrom(t));
        Assert.IsNotNull(nested, "expected a nested IssueBase type on the vanilla behavior");
        Assert.IsTrue(LotrIssueSuppression.IsVanillaIssueType(nested));
    }

    [TestMethod]
    public void IsVanillaIssueType_TaomTemplate_False()
        => Assert.IsFalse(LotrIssueSuppression.IsVanillaIssueType(typeof(DeliverGoodsLotrIssue)));

    [TestMethod]
    public void IsVanillaIssueType_Null_False()
        => Assert.IsFalse(LotrIssueSuppression.IsVanillaIssueType(null));
}
