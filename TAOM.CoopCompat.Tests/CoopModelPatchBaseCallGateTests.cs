using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.RaceAge.Models;

namespace TAOM.CoopCompat.Tests;

/// <summary>
/// #1151: Coop fixes vanilla GameModel methods with Harmony patches on the Default*Model method body.
/// The engine calls through TAOM's override, so a TAOM override that never calls base hides every
/// Coop patch on that method (#1141: pregnancy used Hero.MainHero, the server's own hero). This gate
/// reads the patches that the Coop build the tests reference declares by attribute (a target on the
/// patch method, on the class, or from TargetMethod and TargetMethods) and fails on any TAOM override
/// of a patched method with no base call that is not on the reviewed list below. Patches registered at
/// run time (harmony.Patch, AutoSync) are outside its reach. An override that calls base on some paths
/// passes; the #1151 audit read those by hand. Calling base does not prove that TAOM's own rule around
/// it treats co-op players right.
/// </summary>
[TestClass]
public class CoopModelPatchBaseCallGateTests
{
    private const BindingFlags AnyDeclared =
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static readonly string[] PatchKinds = { "Prefix", "Postfix", "Transpiler", "Finalizer" };

    // An override here skips base on purpose and carries Coop's rule itself. Say where. A base call made
    // only inside a lambda or a helper is not seen either: such an override belongs here too, saying so.
    private static readonly Dictionary<string, string> Reviewed = new()
    {
        ["TaomPregnancyModel.GetDailyChanceOfPregnancyForHero"] =
            "#1141: IsPlayerMarriage reads IPlayerControlQuery, Coop's IsPlayerHero rule in place of Hero.MainHero",
    };

    // One known target per way Coop declares one, so losing a way fails here instead of passing quietly.
    private static readonly string[] ShapeCanaries =
    {
        "DefaultPregnancyModel.GetDailyChanceOfPregnancyForHero", // target on the patch method
        "DefaultPartyMoraleModel.GetEffectivePartyMorale", // target on the class only
        "DefaultArmyManagementCalculationModel.CanPlayerCreateArmy", // TargetMethods()
    };

    private static ResolveEventHandler? _resolver;

    // Some Coop view patches carry attributes from module DLLs that the test bin does not copy.
    [ClassInitialize]
    public static void Init(TestContext _)
    {
        var game = typeof(CoopModelPatchBaseCallGateTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "TaomGameFolder")?.Value;
        if (string.IsNullOrEmpty(game) || !Directory.Exists(Path.Combine(game, "Modules"))) return;
        var folders = Directory.GetDirectories(Path.Combine(game, "Modules"))
            .Select(m => Path.Combine(m, "bin", "Win64_Shipping_Client")).Where(Directory.Exists).ToList();
        _resolver = (_, args) =>
        {
            var name = new AssemblyName(args.Name).Name;
            var loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == name);
            if (loaded != null) return loaded;
            var path = folders.Select(f => Path.Combine(f, name + ".dll")).FirstOrDefault(File.Exists);
            return path == null ? null : Assembly.LoadFrom(path);
        };
        AppDomain.CurrentDomain.AssemblyResolve += _resolver;
    }

    [ClassCleanup]
    public static void Cleanup()
    {
        if (_resolver != null) AppDomain.CurrentDomain.AssemblyResolve -= _resolver;
        _resolver = null;
    }

    [TestMethod]
    public void EveryTaomOverrideOfACoopPatchedModelMethod_CallsBase_OrIsReviewed()
    {
        var problems = new List<string>();
        var patched = CoopPatchedModelMethods(problems);
        var overrides = TaomOverridesOf(patched, problems);

        var misses = overrides.Where(o => !CallsBase(o.Override, o.Patched)).Select(o => Name(o.Override)).Distinct().ToList();
        var unreviewed = misses.Where(m => !Reviewed.ContainsKey(m)).OrderBy(m => m).ToList();
        var stale = Reviewed.Keys.Where(k => !misses.Contains(k)).OrderBy(k => k).ToList();
        var lostShapes = ShapeCanaries.Where(c => !patched.Select(Name).Contains(c)).ToList();

        CollectionAssert.AreEqual(new string[0], problems, "The gate could not read everything: " + string.Join("; ", problems));
        CollectionAssert.AreEqual(new string[0], lostShapes, "Known Coop targets no longer found, so a way of declaring a patch is lost: " + string.Join(", ", lostShapes));
        Assert.IsTrue(overrides.Count > 5, $"Found {overrides.Count} TAOM overrides of {patched.Count} patched methods: too few to prove anything.");
        CollectionAssert.AreEqual(new string[0], unreviewed,
            "These TAOM overrides skip base, so Coop's patch on the method never runs. Call base, or port Coop's rule and list the override in Reviewed: "
            + string.Join(", ", unreviewed));
        CollectionAssert.AreEqual(new string[0], stale, "Reviewed entries that now call base or no longer exist: " + string.Join(", ", stale));
    }

    private static string Name(MethodInfo method) => method.DeclaringType!.Name + "." + method.Name;

    // Each Default*Model method a Coop patch class targets, the way Harmony's PatchClassProcessor finds it.
    private static HashSet<MethodInfo> CoopPatchedModelMethods(List<string> problems)
    {
        var found = new HashSet<MethodInfo>();
        foreach (var type in LoadableTypes(typeof(GameInterface.Services.Kingdoms.KingdomDecisionTypeRegistry).Assembly, problems))
        {
            var onClass = HarmonyMethod.Merge(type.GetCustomAttributes<HarmonyPatch>(false).Select(a => a.info).ToList());
            var methods = type.GetMethods(AnyDeclared);
            var targeters = methods.Where(IsTargeter).ToList();
            var targets = new List<HarmonyMethod>();
            var classTargetUsed = false;
            foreach (var method in methods)
            {
                var onMethod = method.GetCustomAttributes<HarmonyPatch>(false).Select(a => a.info).ToList();
                if (onMethod.Count > 0) targets.Add(onClass.Merge(HarmonyMethod.Merge(onMethod)));
                else if (IsPatch(method)) classTargetUsed = true;
            }

            if (classTargetUsed && targeters.Count > 0)
                foreach (var targeter in targeters) found.UnionWith(TargetedModelMethods(type, targeter, problems));
            else if (classTargetUsed && onClass.methodName != null)
                targets.Add(onClass);

            foreach (var target in targets.Where(t => t.declaringType != null && t.methodName != null && IsDefaultModel(t.declaringType)))
            {
                var resolved = Resolve(target).ToList();
                if (resolved.Count == 0) problems.Add($"{type.FullName} targets {target.declaringType.Name}.{target.methodName}, which this gate cannot resolve");
                found.UnionWith(resolved);
            }
        }

        return found;
    }

    private static bool IsPatch(MethodInfo method) =>
        PatchKinds.Contains(method.Name)
        || method.GetCustomAttributes(false).Any(a => PatchKinds.Any(k => a.GetType().Name == "Harmony" + k));

    private static bool IsTargeter(MethodInfo method) =>
        method.IsStatic && (method.Name is "TargetMethod" or "TargetMethods"
            || method.GetCustomAttributes(false).Any(a => a is HarmonyTargetMethod or HarmonyTargetMethods));

    private static IEnumerable<MethodInfo> TargetedModelMethods(Type type, MethodInfo targeter, List<string> problems)
    {
        if (targeter.GetParameters().Length != 0)
        {
            problems.Add($"{type.FullName}.{targeter.Name} takes parameters; this gate cannot call it");
            return Enumerable.Empty<MethodInfo>();
        }

        try
        {
            var result = targeter.Invoke(null, null);
            var all = result is MethodBase one ? new[] { one } : ((IEnumerable)result).Cast<MethodBase>();
            return all.OfType<MethodInfo>().Where(m => m.DeclaringType != null && IsDefaultModel(m.DeclaringType)).ToList();
        }
        catch (Exception ex)
        {
            problems.Add($"{type.FullName}.{targeter.Name} threw {(ex.InnerException ?? ex).GetType().Name}: {(ex.InnerException ?? ex).Message}");
            return Enumerable.Empty<MethodInfo>();
        }
    }

    private static IEnumerable<MethodInfo> Resolve(HarmonyMethod target)
    {
        switch (target.methodType)
        {
            case MethodType.Getter:
                return new[] { AccessTools.PropertyGetter(target.declaringType, target.methodName) }.Where(m => m != null);
            case MethodType.Setter:
                return new[] { AccessTools.PropertySetter(target.declaringType, target.methodName) }.Where(m => m != null);
            default:
                return target.declaringType.GetMethods(AnyDeclared).Where(m => m.Name == target.methodName
                    && (target.argumentTypes == null || m.GetParameters().Select(p => p.ParameterType).SequenceEqual(target.argumentTypes)));
        }
    }

    private static bool IsDefaultModel(Type type) =>
        type.Namespace?.StartsWith("TaleWorlds", StringComparison.Ordinal) == true
        && type.Name.StartsWith("Default", StringComparison.Ordinal) && type.Name.EndsWith("Model", StringComparison.Ordinal);

    private static List<(MethodInfo Override, MethodInfo Patched)> TaomOverridesOf(HashSet<MethodInfo> patched, List<string> problems)
    {
        var result = new List<(MethodInfo, MethodInfo)>();
        foreach (var type in LoadableTypes(typeof(TaomPregnancyModel).Assembly, problems))
        foreach (var method in type.GetMethods(AnyDeclared))
        {
            if (!method.IsVirtual) continue;
            var root = method.GetBaseDefinition();
            foreach (var target in patched)
                if (type.IsSubclassOf(target.DeclaringType!) && target.GetBaseDefinition() == root)
                    result.Add((method, target));
        }

        return result;
    }

    // A non-virtual call to the same slot: base.X(...) to the patched method or to an intermediate TAOM override.
    private static bool CallsBase(MethodInfo method, MethodInfo patched) =>
        PatchProcessor.ReadMethodBody(method).Any(op => op.Key == System.Reflection.Emit.OpCodes.Call
            && op.Value is MethodInfo callee && callee.GetBaseDefinition() == patched.GetBaseDefinition());

    // A type that does not load could be a patch class or a model the gate then never sees: report it.
    private static IEnumerable<Type> LoadableTypes(Assembly assembly, List<string> problems)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            problems.AddRange(ex.LoaderExceptions.Select(e => $"{assembly.GetName().Name}: {e.Message}").Distinct());
            return ex.Types.Where(t => t != null)!;
        }
    }
}
