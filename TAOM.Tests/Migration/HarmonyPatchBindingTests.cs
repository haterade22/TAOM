using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Migration;

/// <summary>
/// Binding-verification gate for every Harmony patch in TAOM. Enumerates the patch classes in the TAOM
/// assembly and resolves and binds each one exactly as Lib.Harmony 2.4.2 applies it (through
/// HarmonyTargetResolver) against the *installed* Bannerlord engine, plus the patches
/// ManualPatchApplicator applies by hand. A renamed, moved or re-signatured engine member is a patch
/// Harmony refuses or applies to nothing in game, and a red test here.
///
/// This is the offline-checkable portion of the migration S6 smoke-test gate
/// (docs/migration/TRACKING.md S4 row "Runtime binding verification"). It runs under `dotnet test` with
/// no game launch because TAOM.Tests references the installed TaleWorlds DLLs and pre-loads the
/// SandBox/CustomBattle/StoryMode module assemblies (see GameAssemblies).
/// </summary>
[TestClass]
public class HarmonyPatchBindingTests
{
    private const BindingFlags StaticParameterless =
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static Assembly TaomAssembly => typeof(TAOM.IoC).Assembly;

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void EveryHarmonyPatch_ResolvesTargetMethod_AgainstInstalledEngine()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var patchTypes = DiscoverPatchTypes(out var typesFailedToLoad);

        // The game loaded, so a short discovery is a TAOM type-load failure: fail, never skip.
        if (patchTypes.Count < 30)
            Assert.Fail(
                $"Only {patchTypes.Count} [HarmonyPatch] types discovered (taleworlds-api-snapshot/patch-targets.md has the current count). " +
                $"{typesFailedToLoad} type(s) failed to load. This indicates an assembly-load problem, " +
                "not a genuine pass — investigate before trusting this gate.");

        var failures = new List<string>();
        foreach (var t in patchTypes)
        {
            try
            {
                foreach (var problem in HarmonyTargetResolver.Check(t))
                    failures.Add($"  {t.FullName}\n      -> {problem}");
            }
            catch (Exception ex)
            {
                var inner = (ex as TargetInvocationException)?.InnerException ?? ex;
                failures.Add($"  {t.FullName}\n      → threw during resolution: {inner.GetType().Name}: {inner.Message}");
            }
        }

        if (failures.Count > 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{failures.Count} problem(s) across {patchTypes.Count} Harmony patch classes against the installed engine.");
            sb.AppendLine("Each is a patch Harmony would refuse (or apply to nothing) when its category applies:");
            sb.AppendLine();
            foreach (var f in failures) sb.AppendLine(f);
            Assert.Fail(sb.ToString());
        }
    }

    // Patches ManualPatchApplicator applies with harmony.Patch from a class the discovery cannot find (no [HarmonyPatch],
    // no TargetMethod). Keep in step with Main/ManualPatchApplicator.cs; the source gate below fails when that file
    // names a patch class neither the discovery nor this list covers.
    private static readonly (string Target, string Method, Type PatchClass, string PatchMethod, HarmonyPatchType Kind)[] ManualPatches =
    {
        ("TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle.OrderOfBattleHeroItemVM", "GetCaptainTooltip",
            typeof(TAOM.Features.CompanionTactics.Roles.Hooks.Patch35_OOBHeroItem_GetCaptainTooltip), "Postfix", HarmonyPatchType.Postfix),
    };

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void EveryManualPatch_BindsLikeHarmony()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var failures = new List<string>();
        foreach (var (target, method, patchClass, patchMethod, kind) in ManualPatches)
        {
            // Resolved exactly as ManualPatchApplicator resolves it.
            var original = AccessTools.Method(AccessTools.TypeByName(target), method);
            var patch = AccessTools.Method(patchClass, patchMethod);
            if (original == null) { failures.Add($"{target}.{method} did not resolve against the installed engine"); continue; }
            if (patch == null) { failures.Add($"{patchClass.FullName}.{patchMethod} not found"); continue; }
            failures.AddRange(HarmonyTargetResolver.CheckBinding(original, patch, kind).Select(p => $"{patchClass.Name}: {p}"));
        }

        Assert.AreEqual(0, failures.Count, string.Join("\n", failures));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ManualPatchApplicator_EveryPatchClass_IsCheckedByTheGate()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var source = TAOM.Tests.Infrastructure.RepoPaths.ReadSource("Main/ManualPatchApplicator.cs", stripComments: true);
        var named = Regex.Matches(source, @"new\s+HarmonyMethod\(\s*typeof\(([\w.]+)\)").Cast<Match>()
            .Select(m => m.Groups[1].Value.Split('.').Last()).Distinct().ToList();
        Assert.IsTrue(named.Count >= 8, $"Only {named.Count} patch classes found in ManualPatchApplicator.cs; the scan broke, it did not pass.");

        var covered = new HashSet<string>(DiscoverPatchTypes(out _).Select(t => t.Name));
        covered.UnionWith(ManualPatches.Select(m => m.PatchClass.Name));
        var missing = named.Where(n => !covered.Contains(n)).ToList();

        Assert.AreEqual(0, missing.Count,
            "ManualPatchApplicator applies patch classes this gate never checks: " + string.Join(", ", missing)
            + ". Give the class a TargetMethod() or add a ManualPatches row.");
    }

    // --- discovery ---

    // Shared with Patch37TargetShapeTests so the suite derives a patch target one way only.
    internal static List<Type> DiscoverPatchTypes(out int typesFailedToLoad)
    {
        Type[] all;
        try { all = TaomAssembly.GetTypes(); typesFailedToLoad = 0; }
        catch (ReflectionTypeLoadException ex)
        {
            typesFailedToLoad = ex.Types.Count(t => t == null);
            all = ex.Types.Where(t => t != null).ToArray();
        }

        return all
            .Where(t => t != null && !t.Name.Contains('<')) // skip compiler-generated
            .Where(t => t.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Any()
                        || HasStaticTargetMember(t, "TargetMethod")
                        || HasStaticTargetMember(t, "TargetMethods"))
            .OrderBy(t => t.FullName)
            .ToList();
    }

    private static bool HasStaticTargetMember(Type t, string name) =>
        t.GetMethod(name, StaticParameterless, binder: null, types: Type.EmptyTypes, modifiers: null) != null;

    // One merged spec per class, for Patch37TargetShapeTests. The gate resolves per patch method through HarmonyTargetResolver.
    internal static (Type declaringType, string methodName, MethodType? methodType, Type[] argTypes) MergeSpec(Type patchType)
    {
        var infos = new List<HarmonyMethod>();

        foreach (HarmonyAttribute a in patchType
                     .GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Cast<HarmonyAttribute>())
            infos.Add(a.info);

        foreach (var m in patchType.GetMethods(BindingFlags.Static | BindingFlags.Instance |
                                               BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            foreach (HarmonyAttribute a in m.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Cast<HarmonyAttribute>())
                infos.Add(a.info);

        Type declaringType = null;
        string methodName = null;
        MethodType? methodType = null;
        Type[] argTypes = null;

        foreach (var hm in infos)
        {
            if (hm == null) continue;
            if (hm.declaringType != null) declaringType = hm.declaringType;
            if (hm.methodName != null) methodName = hm.methodName;
            if (hm.methodType != null) methodType = hm.methodType;
            if (hm.argumentTypes != null) argTypes = hm.argumentTypes;
        }

        return (declaringType, methodName, methodType, argTypes);
    }
}
