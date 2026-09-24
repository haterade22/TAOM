using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.CrashReport;

// A Harmony finalizer rewrites the exact method it is given. An override is a different method,
// so a finalizer on a base virtual only runs when an override calls base.X() and the base throws.
// Every Patch37_CrashReport target must therefore be non-virtual (or a sealed override).
[TestClass]
public class Patch37TargetShapeTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void EveryPatch37Target_IsNotAnOverridableVirtual()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var category = TAOM.Features.CrashReport.Hooks.Patch37_CrashReport.Category;
        var patchTypes = HarmonyPatchBindingTests.DiscoverPatchTypes(out _)
            .Where(t => t.GetCustomAttributes(typeof(HarmonyPatchCategory), inherit: false)
                .Cast<HarmonyAttribute>()
                .Any(a => a.info?.category == category))
            .ToList();
        Assert.IsTrue(patchTypes.Count >= 5,
            $"Only {patchTypes.Count} Patch37_CrashReport patch class(es) discovered; discovery is broken, not green.");

        var offenders = new List<string>();
        foreach (var patchType in patchTypes)
        {
            var (declaringType, methodName, methodType, argumentTypes) = HarmonyPatchBindingTests.MergeSpec(patchType);
            Assert.IsNotNull(declaringType, $"{patchType.Name}: no declaringType in its [HarmonyPatch] attributes");
            Assert.IsNotNull(methodName, $"{patchType.Name}: no methodName in its [HarmonyPatch] attributes");
            Assert.IsTrue(methodType == null || methodType == MethodType.Normal,
                $"{patchType.Name}: MethodType {methodType} is not modelled by this test");

            var target = AccessTools.Method(declaringType, methodName, argumentTypes);
            Assert.IsNotNull(target, $"{patchType.Name}: {declaringType.FullName}.{methodName} did not resolve");

            if (target.IsVirtual && !target.IsFinal)
                offenders.Add($"{patchType.Name} -> {declaringType.FullName}.{methodName}");
        }

        Assert.AreEqual(0, offenders.Count,
            "A finalizer on an overridable virtual never runs for an override (a different method), so it " +
            "can never capture the throws it was written for. Patch the non-virtual dispatcher instead:" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }
}
