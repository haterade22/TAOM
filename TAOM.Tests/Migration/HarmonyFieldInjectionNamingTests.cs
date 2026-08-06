using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TAOM.Tests.Migration;

/// <summary>
/// Repo-wide gate on Harmony's private-field injection naming convention.
///
/// Harmony strips EXACTLY three underscores from an injected parameter name and looks the remainder
/// up as a field. Engine fields carry a leading underscore, so binding <c>_race</c> requires
/// <c>____race</c> — FOUR underscores. Writing <c>___race</c> asks for a field literally named
/// "race", and Harmony throws <c>ArgumentException("No such field defined in class …")</c> while
/// applying the whole category.
///
/// WHY THIS EXISTS AS A SEPARATE TEST. Per-patch binding tests (Patch55/Patch58/Patch65/Patch66/
/// Patch67) assert <c>AccessTools.Field(type, "_race") != null</c>. That resolves happily whether the
/// patch parameter is spelled with three underscores or four — it never exercises the naming
/// convention, so it cannot catch this defect. Patch67 shipped with three underscores on
/// 2026-08-06, passed its own binding tests and the full 5588-test suite, and failed only at runtime;
/// it was caught by reading the live game log. In TAOM's isolated preview batch a category that
/// throws is *logged and swallowed*, so this class of bug produces a silently absent patch rather
/// than a crash.
///
/// Runs against every <c>[HarmonyPatch]</c> class in the TAOM assembly, so it also covers patches
/// that have no binding test of their own.
/// </summary>
[TestClass]
public class HarmonyFieldInjectionNamingTests
{
    /// <summary>Harmony's own injected parameters. All use TWO underscores, so they never collide with the three-underscore field form.</summary>
    private static readonly HashSet<string> HarmonySpecials = new(StringComparer.Ordinal)
    {
        "__instance", "__result", "__resultRef", "__state", "__originalMethod", "__args", "__runOriginal", "__exception",
    };

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static Assembly TaomAssembly => typeof(TAOM.SubModule).Assembly;

    private static IEnumerable<(Type PatchClass, Type Target, MethodInfo Method, ParameterInfo Parameter)> InjectedFieldParameters()
    {
        foreach (var patchClass in TaomAssembly.GetTypes())
        {
            var patchAttr = patchClass.GetCustomAttributes(typeof(HarmonyPatch), inherit: false)
                                      .OfType<HarmonyPatch>()
                                      .FirstOrDefault();
            var target = patchAttr?.info?.declaringType;
            if (target == null) continue;

            foreach (var method in patchClass.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                foreach (var p in method.GetParameters())
                {
                    string name = p.Name ?? string.Empty;
                    if (!name.StartsWith("___", StringComparison.Ordinal)) continue;
                    if (HarmonySpecials.Contains(name)) continue;
                    yield return (patchClass, target, method, p);
                }
            }
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void EveryInjectedFieldParameter_ResolvesToARealFieldOnItsPatchTarget()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var failures = new List<string>();
        int checkedCount = 0;

        foreach (var (patchClass, target, method, parameter) in InjectedFieldParameters())
        {
            checkedCount++;
            // Harmony strips exactly three characters, no more.
            string fieldName = parameter.Name.Substring(3);
            if (AccessTools.Field(target, fieldName) != null) continue;

            string suggestion = AccessTools.Field(target, "_" + fieldName) != null
                ? $" Did you mean '_{parameter.Name}' (FOUR underscores) to bind '_{fieldName}'?"
                : string.Empty;

            failures.Add(
                $"{patchClass.Name}.{method.Name}: parameter '{parameter.Name}' resolves to field " +
                $"'{fieldName}' on {target.FullName}, which does not exist.{suggestion}");
        }

        Assert.AreEqual(
            0, failures.Count,
            "Harmony would throw ArgumentException(\"No such field\") applying these patch categories, " +
            "and TAOM's isolated batches log-and-swallow that — the patch would be silently absent in-game:" +
            Environment.NewLine + string.Join(Environment.NewLine, failures));

        // Guards against the scan silently matching nothing (a refactor renaming the attribute, or
        // reflection returning no types, would otherwise make this test vacuously green forever).
        Assert.IsTrue(
            checkedCount > 0,
            "No injected-field parameters were found at all across the TAOM assembly — the scan is broken, " +
            "not the code. TAOM has several patches using the ____field form.");
    }
}
