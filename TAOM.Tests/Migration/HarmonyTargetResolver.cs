using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace TAOM.Tests.Migration;

/// <summary>
/// Resolves a patch class's targets and binds its patch parameters the way Lib.Harmony 2.4.2 does when it applies
/// the class, without patching anything. PatchClassProcessor merges the attributes per patch method;
/// PatchTools.GetOriginalMethod resolves an attribute target on the DECLARED type only (an ambiguous declared name
/// throws); TargetMethod or TargetMethods replace the attributes; MethodCreatorTools.EmitCallParameter binds every
/// prefix, postfix and finalizer parameter by name. AccessTools.Method, which the old gate used, walks base types
/// and falls back to the parameterless overload, so it passed targets Harmony cannot patch (verify report D4, G2).
/// Three Harmony internals are read by reflection; HarmonyTargetResolverTests pins them and the Harmony version.
/// </summary>
internal static class HarmonyTargetResolver
{
    private static readonly Assembly HarmonyAssembly = typeof(PatchClassProcessor).Assembly;

    internal static readonly FieldInfo? PatchMethodsField = AccessTools.Field(typeof(PatchClassProcessor), "patchMethods");
    internal static readonly FieldInfo? AuxilaryMethodsField = AccessTools.Field(typeof(PatchClassProcessor), "auxilaryMethods");
    internal static readonly Type? AttributePatchType = HarmonyAssembly.GetType("HarmonyLib.AttributePatch");
    internal static readonly FieldInfo? AttributePatchInfoField =
        AttributePatchType == null ? null : AccessTools.Field(AttributePatchType, "info");
    internal static readonly FieldInfo? AttributePatchKindField =
        AttributePatchType == null ? null : AccessTools.Field(AttributePatchType, "type");
    internal static readonly MethodInfo? GetOriginalMethod = HarmonyAssembly.GetType("HarmonyLib.PatchTools") is { } tools
        ? AccessTools.Method(tools, "GetOriginalMethod", new[] { typeof(HarmonyMethod) })
        : null;

    private static readonly Harmony Owner = new Harmony("taom.tests.bindinggate");

    // Names Harmony injects whatever the original's parameters are (InjectedParameter, 2.4.2).
    private static readonly HashSet<string> Injected = new HashSet<string>(StringComparer.Ordinal)
    {
        "__instance", "__originalMethod", "__args", "__state", "__exception", "__runOriginal",
    };

    /// <summary>Every problem Harmony would hit applying this patch class; empty when it binds.</summary>
    internal static List<string> Check(Type patchClass)
    {
        var problems = new List<string>();
        foreach (var (patch, kind, original) in Bind(patchClass, problems))
            problems.AddRange(CheckBinding(original, patch, kind));
        return problems;
    }

    /// <summary>
    /// The body and parameter checks for one patch method against one original. Also used for patches applied by
    /// hand with harmony.Patch, as ManualPatchApplicator does.
    /// </summary>
    internal static List<string> CheckBinding(MethodBase original, MethodInfo patch, HarmonyPatchType? kind)
    {
        var problems = new List<string>();
        if (!HasBody(original))
            problems.Add($"{Describe(original)} has no body (abstract or extern); Harmony cannot patch it");
        if (kind == HarmonyPatchType.Prefix || kind == HarmonyPatchType.Postfix || kind == HarmonyPatchType.Finalizer)
            problems.AddRange(ParameterProblems(patch, kind.Value, original));
        return problems;
    }

    /// <summary>The methods Harmony would patch for this class, without duplicates; problems as in Check.</summary>
    internal static List<MethodBase> ResolveOriginals(Type patchClass, List<string> problems) =>
        Bind(patchClass, problems).Select(b => b.Original).Distinct().ToList();

    // Resolving is not patching: an abstract or extern target resolves by name and leaves Harmony nothing to rewrite.
    internal static bool HasBody(MethodBase method) => !method.IsAbstract && method.GetMethodBody() != null;

    internal static string Describe(MethodBase method) =>
        $"{method.DeclaringType?.FullName}.{method.Name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name))})";

    private static List<(MethodInfo Patch, HarmonyPatchType? Kind, MethodBase Original)> Bind(Type patchClass, List<string> problems)
    {
        if (PatchMethodsField == null || AuxilaryMethodsField == null || AttributePatchInfoField == null
            || AttributePatchKindField == null || GetOriginalMethod == null)
            throw new InvalidOperationException("Lib.Harmony internals moved: see HarmonyTargetResolverTests.HarmonyInternals_StillWhereTheResolverReadsThem.");

        var bound = new List<(MethodInfo, HarmonyPatchType?, MethodBase)>();
        if (patchClass.GetCustomAttributes(true).Any(a => a.GetType().FullName == "HarmonyLib.HarmonyPatchAll"))
        {
            problems.Add("[HarmonyPatchAll] is not modelled by this gate");
            return bound;
        }

        PatchClassProcessor processor;
        try
        {
            processor = new PatchClassProcessor(Owner, patchClass);
        }
        catch (Exception ex)
        {
            problems.Add("Harmony cannot read the class: " + Unwrap(ex));
            return bound;
        }

        var patches = ((IEnumerable)PatchMethodsField.GetValue(processor)).Cast<object>()
            .Select(p => ((HarmonyMethod)AttributePatchInfoField.GetValue(p), (HarmonyPatchType?)AttributePatchKindField.GetValue(p)))
            .ToList();
        var auxiliary = (IDictionary)AuxilaryMethodsField.GetValue(processor);

        if (auxiliary.Contains(typeof(HarmonyTargetMethods)) || auxiliary.Contains(typeof(HarmonyTargetMethod)))
        {
            var originals = BulkTargets(auxiliary, problems);
            foreach (var (info, kind) in patches)
            {
                if (info.methodName != null || (info.methodType.HasValue && info.methodType.Value != MethodType.Normal) || info.argumentTypes != null)
                    problems.Add($"{info.method.Name}: Harmony refuses TargetMethod or TargetMethods combined with a target annotation");
                foreach (var original in originals)
                    bound.Add((info.method, kind, original));
            }
            return bound;
        }

        foreach (var (info, kind) in patches)
        {
            MethodBase? original;
            try
            {
                original = (MethodBase?)GetOriginalMethod.Invoke(null, new object[] { info });
            }
            catch (Exception ex)
            {
                problems.Add($"{info.method.Name}: {Unwrap(ex)}");
                continue;
            }

            if (original == null)
                problems.Add($"Undefined target method for patch method {info.method.Name}: {info.declaringType?.FullName}.{info.methodName} ({info.methodType})");
            else
                bound.Add((info.method, kind, original));
        }
        return bound;
    }

    private static List<MethodBase> BulkTargets(IDictionary auxiliary, List<string> problems)
    {
        var result = new List<MethodBase>();
        try
        {
            if (auxiliary[typeof(HarmonyTargetMethods)] is MethodInfo many)
            {
                if (many.GetParameters().Length > 0)
                {
                    problems.Add($"{many.Name} takes parameters; this gate models a parameterless TargetMethods only");
                    return result;
                }
                if (many.Invoke(null, null) is IEnumerable sequence)
                {
                    var items = sequence.Cast<object?>().ToList();
                    if (items.Count == 0) problems.Add("TargetMethods() returned an empty sequence");
                    if (items.Any(i => i == null)) problems.Add("TargetMethods() contained a null entry (unresolved target)");
                    result.AddRange(items.OfType<MethodBase>());
                    return result;
                }
                if (!(auxiliary[typeof(HarmonyTargetMethod)] is MethodInfo))
                {
                    problems.Add("TargetMethods() returned null");
                    return result;
                }
            }

            if (auxiliary[typeof(HarmonyTargetMethod)] is MethodInfo one)
            {
                if (one.GetParameters().Length > 0)
                {
                    problems.Add($"{one.Name} takes parameters; this gate models a parameterless TargetMethod only");
                    return result;
                }
                if (one.Invoke(null, null) is MethodBase target)
                    result.Add(target);
                else
                    problems.Add("TargetMethod() returned null: the target is not in the installed engine");
            }
        }
        catch (Exception ex)
        {
            problems.Add("TargetMethod or TargetMethods threw: " + Unwrap(ex));
        }
        return result;
    }

    private static IEnumerable<string> ParameterProblems(MethodInfo patch, HarmonyPatchType kind, MethodBase original)
    {
        if (UsesHarmonyArgument(patch))
        {
            yield return $"{patch.Name} uses [HarmonyArgument], which this gate does not model";
            yield break;
        }

        var parameters = patch.GetParameters().ToList();
        // A pass-through postfix returns the result type and takes it first (MethodCreator.AddPostfixes).
        if (kind == HarmonyPatchType.Postfix && patch.ReturnType != typeof(void) && parameters.Count > 0
            && parameters[0].ParameterType == patch.ReturnType)
            parameters.RemoveAt(0);

        var originalParameters = original.GetParameters();
        var originalNames = originalParameters.Select(p => p.Name).ToArray();
        var returnType = original is MethodInfo method ? method.ReturnType : typeof(void);

        foreach (var parameter in parameters)
        {
            var name = parameter.Name ?? string.Empty;
            if (Injected.Contains(name))
                continue;

            if (name.StartsWith("___", StringComparison.Ordinal))
            {
                var field = name.Substring(3);
                var found = field.Length > 0 && field.All(char.IsDigit)
                    ? AccessTools.DeclaredField(original.DeclaringType, int.Parse(field))
                    : AccessTools.Field(original.DeclaringType, field);
                if (found == null)
                    yield return $"{patch.Name}: parameter {name} injects field '{field}', which {original.DeclaringType?.FullName} does not have";
                continue;
            }

            if (name == "__result")
            {
                if (returnType == typeof(void))
                {
                    yield return $"{patch.Name}: Cannot get result from void method {Describe(original)}";
                    continue;
                }
                var type = parameter.ParameterType.IsByRef && !returnType.IsByRef
                    ? parameter.ParameterType.GetElementType()!
                    : parameter.ParameterType;
                if (!type.IsAssignableFrom(returnType))
                    yield return $"{patch.Name}: cannot assign {returnType.FullName} to __result of type {type.FullName}";
                continue;
            }

            if (name == "__resultRef")
            {
                yield return $"{patch.Name}: __resultRef is not modelled by this gate";
                continue;
            }

            if (name.StartsWith("__", StringComparison.Ordinal))
            {
                if (!int.TryParse(name.Substring(2), out var index))
                    yield return $"{patch.Name}: parameter {name} does not contain a valid index";
                else if (index < 0 || index >= originalParameters.Length)
                    yield return $"{patch.Name}: no parameter at index {index} of {Describe(original)}";
                continue;
            }

            if (Array.IndexOf(originalNames, name) < 0)
                yield return $"{patch.Name}: Parameter \"{name}\" not found in method {Describe(original)}";
        }
    }

    private static bool UsesHarmonyArgument(MethodInfo patch) =>
        IsHarmonyArgument(patch.GetCustomAttributes(true))
        || patch.GetParameters().Any(p => IsHarmonyArgument(p.GetCustomAttributes(true)))
        || (patch.DeclaringType != null && IsHarmonyArgument(patch.DeclaringType.GetCustomAttributes(true)));

    private static bool IsHarmonyArgument(object[] attributes) => attributes.Any(a => a.GetType().Name == "HarmonyArgument");

    private static string Unwrap(Exception ex)
    {
        while (ex is TargetInvocationException && ex.InnerException != null)
            ex = ex.InnerException;
        return ex.GetType().Name + ": " + ex.Message;
    }
}
