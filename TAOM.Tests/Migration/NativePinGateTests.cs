using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Features.MissionPerf.AnimMemory;
using TAOM.Tests.Features.SkeletonBuffer;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Migration;

/// <summary>
/// Keeps every per-build native address table (a type that declares a static <c>KnownBuilds</c> member)
/// inside the engine-bump gate. The pin tests read the installed <c>TaleWorlds.Native.dll</c>, so they are
/// LiveInstall; without <c>BindingVerification</c> an unlisted build reads Skipped and a bump ships without
/// its row (the v1.5.5 SkeletonBuffer row was missed that way). The tables are found by reflection over the
/// test assembly and the tags are read from the compiled class attributes, so comments, partial classes and
/// file names cannot mislead it. Needs no game install, so it runs in every suite. Limit: a test type that fails
/// to load is invisible to the scan (AccessTools.GetTypesFromAssembly drops it), so the two known tables are also
/// pinned by name; a new table belongs in that list.
/// </summary>
[TestClass]
public class NativePinGateTests
{
    private static readonly Lazy<List<Type>> Tables = new(ScanPinTables);

    private static List<Type> ScanPinTables() =>
        AccessTools.GetTypesFromAssembly(typeof(NativePinGateTests).Assembly)
            .Where(t => t.GetMember("KnownBuilds",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length > 0)
            .ToList();

    /// <summary>Whether <paramref name="category"/> is on the class declaration of <paramref name="type"/>
    /// itself, not on a method or a base class.</summary>
    private static bool IsClassTagged(Type type, string category) =>
        type.GetCustomAttributes<TestCategoryBaseAttribute>(false).Any(a => a.TestCategories.Contains(category));

    [TestMethod]
    public void KnownBuildsTables_IncludeTheKnownPinClasses()
    {
        CollectionAssert.IsSubsetOf(
            new[] { typeof(SkeletonBufferInstalledBinaryTests), typeof(ClipBudgetSignatureInstalledBinaryTests) },
            Tables.Value);
    }

    [TestMethod]
    public void KnownBuildsTables_EachClass_IsTaggedBindingVerificationAndLiveInstall()
    {
        foreach (var type in Tables.Value)
        {
            Assert.IsTrue(IsClassTagged(type, "LiveInstall"), type.Name + " must be tagged LiveInstall.");
            Assert.IsTrue(IsClassTagged(type, "BindingVerification"),
                type.Name + " must be tagged BindingVerification, or an unlisted engine build skips instead of failing the gate.");
        }
    }

    [TestMethod]
    public void IsClassTagged_ATagOnAMethodInsteadOfTheClass_DoesNotCount()
    {
        Assert.IsTrue(IsClassTagged(typeof(ClassTagged), "BindingVerification"));
        Assert.IsFalse(IsClassTagged(typeof(MethodTagged), "BindingVerification"));
    }

    [TestMethod]
    public void KnownBuildsTables_EachClass_IsNamedInTheEngineBumpSkill()
    {
        var skill = RepoPaths.ReadSource(".claude/skills/engine-bump/SKILL.md");
        foreach (var type in Tables.Value)
            Assert.IsTrue(skill.Contains(type.Name), type.Name + " holds a KnownBuilds table and must be listed in the engine-bump skill.");
    }

    [TestCategory("BindingVerification")]
    private sealed class ClassTagged { }

    private sealed class MethodTagged
    {
        [TestCategory("BindingVerification")]
        public void M() { }
    }
}
