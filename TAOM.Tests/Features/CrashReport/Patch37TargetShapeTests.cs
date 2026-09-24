using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    public void EveryPatch37Target_IsNotAnOverridableVirtual()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var patchTypes = DiscoverPatch37Types();
        Assert.IsTrue(patchTypes.Count >= 5,
            $"Only {patchTypes.Count} Patch37_CrashReport patch class(es) discovered; discovery is broken, not green.");

        var offenders = new List<string>();
        foreach (var patchType in patchTypes)
        {
            var (declaringType, methodName, argumentTypes) = MergeSpec(patchType);
            Assert.IsNotNull(declaringType, $"{patchType.Name}: no declaringType in its [HarmonyPatch] attributes");
            Assert.IsNotNull(methodName, $"{patchType.Name}: no methodName in its [HarmonyPatch] attributes");

            var target = AccessTools.Method(declaringType, methodName, argumentTypes);
            Assert.IsNotNull(target, $"{patchType.Name}: {declaringType!.FullName}.{methodName} did not resolve");

            if (target.IsVirtual && !target.IsFinal)
                offenders.Add($"{patchType.Name} -> {declaringType!.FullName}.{methodName}");
        }

        Assert.AreEqual(0, offenders.Count,
            "A finalizer on an overridable virtual never runs for an override (a different method), so it " +
            "can never capture the throws it was written for. Patch the non-virtual dispatcher instead:" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    private static List<Type> DiscoverPatch37Types()
    {
        Type[] all;
        try { all = typeof(TAOM.IoC).Assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { all = ex.Types.Where(t => t != null).ToArray()!; }

        var category = TAOM.Features.CrashReport.Hooks.Patch37_CrashReport.Category;
        return all
            .Where(t => t != null
                        && t.GetCustomAttributes(typeof(HarmonyPatchCategory), inherit: false)
                            .Cast<HarmonyAttribute>()
                            .Any(a => a.info?.category == category)
                        && t.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Any())
            .OrderBy(t => t.FullName)
            .ToList();
    }

    // Class-level [HarmonyPatch] infos only; later non-null values win, as Harmony merges them.
    private static (Type? declaringType, string? methodName, Type[]? argumentTypes) MergeSpec(Type patchType)
    {
        Type? declaringType = null;
        string? methodName = null;
        Type[]? argumentTypes = null;

        foreach (var attribute in patchType.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Cast<HarmonyAttribute>())
        {
            var info = attribute.info;
            if (info == null) continue;
            if (info.declaringType != null) declaringType = info.declaringType;
            if (info.methodName != null) methodName = info.methodName;
            if (info.argumentTypes != null) argumentTypes = info.argumentTypes;
        }

        return (declaringType, methodName, argumentTypes);
    }
}
