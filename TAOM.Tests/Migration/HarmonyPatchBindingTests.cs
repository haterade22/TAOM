using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Migration;

/// <summary>
/// Binding-verification gate for every Harmony patch in TAOM. Enumerates all <c>[HarmonyPatch]</c>
/// types in the TAOM assembly and resolves each one's target method against the *installed*
/// Bannerlord engine — exactly the resolution Harmony performs when it applies the patch at game
/// load. A renamed / moved / re-signatured engine method makes the resolution return null, which is
/// a silent no-op (or crash) in-game but a red test here.
///
/// This is the offline-checkable portion of the migration S6 smoke-test gate
/// (docs/migration/TRACKING.md S4 row "Runtime binding verification — ⏳ S6 smoke test"). It runs
/// under `dotnet test` with no game launch because TAOM.Tests references the v1.4.5 TaleWorlds DLLs
/// and pre-loads the SandBox/CustomBattle/StoryMode module assemblies (see GameAssemblies).
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

        if (patchTypes.Count < 30)
            Assert.Inconclusive(
                $"Only {patchTypes.Count} [HarmonyPatch] types discovered (expected ~70). " +
                $"{typesFailedToLoad} type(s) failed to load. This indicates an assembly-load problem, " +
                "not a genuine pass — investigate before trusting this gate.");

        var failures = new List<string>();
        foreach (var t in patchTypes)
        {
            try
            {
                var (ok, detail) = ResolveTarget(t);
                if (!ok) failures.Add($"  {t.FullName}\n      → {detail}");
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
            sb.AppendLine($"{failures.Count} of {patchTypes.Count} Harmony patches did not bind against the installed v1.4.5 engine.");
            sb.AppendLine("Each is a patch that would silently fail (or crash) at game load:");
            sb.AppendLine();
            foreach (var f in failures) sb.AppendLine(f);
            Assert.Fail(sb.ToString());
        }
    }

    // --- discovery ---

    private static List<Type> DiscoverPatchTypes(out int typesFailedToLoad)
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

    // --- resolution (mirrors Harmony's own target discovery) ---

    private static (bool ok, string detail) ResolveTarget(Type patchType)
    {
        // 1. TargetMethods() — multi-target patches.
        var tms = patchType.GetMethod("TargetMethods", StaticParameterless, null, Type.EmptyTypes, null);
        if (tms != null)
        {
            if (!(tms.Invoke(null, null) is IEnumerable seq))
                return (false, "TargetMethods() returned null");
            var items = seq.Cast<object>().ToList();
            if (items.Count == 0) return (false, "TargetMethods() returned an empty sequence");
            if (items.Any(x => x == null)) return (false, "TargetMethods() contained a null entry (unresolved target)");
            return (true, $"TargetMethods() → {items.Count} target(s)");
        }

        // 2. TargetMethod() — single explicit target (private methods, types resolved by name, etc.)
        var tm = patchType.GetMethod("TargetMethod", StaticParameterless, null, Type.EmptyTypes, null);
        if (tm != null)
        {
            if (!(tm.Invoke(null, null) is MethodBase mb))
                return (false, "TargetMethod() returned null — target not found in the installed engine");
            return (true, $"TargetMethod() → {Describe(mb)}");
        }

        // 3. Attribute-based — merge all [HarmonyPatch] specs on the type + its methods.
        var (declaringType, methodName, methodType, argTypes) = MergeSpec(patchType);
        if (declaringType == null)
            return (false, "indeterminate — no declaringType in any [HarmonyPatch] and no TargetMethod()");

        var mt = methodType ?? MethodType.Normal;
        MethodBase resolved;
        switch (mt)
        {
            case MethodType.Normal:
                if (methodName == null)
                    return (false, $"indeterminate — declaringType {declaringType.FullName} set but no methodName/methodType");
                resolved = AccessTools.Method(declaringType, methodName, argTypes);
                break;
            case MethodType.Getter:
                resolved = AccessTools.PropertyGetter(declaringType, methodName);
                break;
            case MethodType.Setter:
                resolved = AccessTools.PropertySetter(declaringType, methodName);
                break;
            case MethodType.Constructor:
                resolved = AccessTools.Constructor(declaringType, argTypes);
                break;
            case MethodType.StaticConstructor:
                resolved = AccessTools.Constructor(declaringType, argTypes, searchForStatic: true);
                break;
            default:
                return (true, $"skipped — MethodType.{mt} not statically verified");
        }

        return resolved == null
            ? (false, $"{declaringType.FullName}.{methodName} ({mt}) did not resolve against the installed engine")
            : (true, $"{mt} → {Describe(resolved)}");
    }

    private static (Type declaringType, string methodName, MethodType? methodType, Type[] argTypes) MergeSpec(Type patchType)
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

    private static string Describe(MethodBase mb) =>
        $"{mb.DeclaringType?.FullName}.{mb.Name}({string.Join(", ", mb.GetParameters().Select(p => p.ParameterType.Name))})";
}
