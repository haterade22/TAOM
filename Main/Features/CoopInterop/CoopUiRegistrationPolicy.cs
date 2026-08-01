using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bannerlord.UIExtenderEx.Attributes;

namespace TAOM.Features.CoopInterop;

/// <summary>
/// Decides which UIExtenderEx extension types TAOM registers for this session.
///
/// Pure and engine-free so it can be unit-tested — the same shape as
/// <c>PatchShieldPolicy.ShouldUnpatchForeignOwners(bool coopActive)</c> in TAOM.Dependencies.
/// </summary>
public static class CoopUiRegistrationPolicy
{
    /// <summary>
    /// Returns the types to register. When <paramref name="coopActive"/> is false this is the
    /// input unchanged; when true, types marked <see cref="CoopSuppressedUiAttribute"/> are
    /// dropped.
    /// </summary>
    public static IReadOnlyList<Type> Filter(IEnumerable<Type> candidates, bool coopActive)
    {
        if (candidates == null) return Array.Empty<Type>();

        var all = candidates.Where(t => t != null).ToList();
        if (!coopActive) return all;

        return all.Where(t => !IsSuppressed(t)).ToList();
    }

    /// <summary>
    /// The types <see cref="Filter"/> would drop, for logging. Always computed against the
    /// co-op-active case so the caller can report what a co-op session gives up.
    /// </summary>
    public static IReadOnlyList<Type> Suppressed(IEnumerable<Type> candidates)
    {
        if (candidates == null) return Array.Empty<Type>();
        return candidates.Where(t => t != null && IsSuppressed(t)).ToList();
    }

    /// <summary>
    /// The UIExtenderEx extension types in <paramref name="assembly"/> — the same selection
    /// UIExtenderEx's own <c>Register(Assembly)</c> makes (any type carrying an attribute derived
    /// from <c>BaseUIExtenderAttribute</c>).
    ///
    /// Tolerates a partial type load: <c>Assembly.GetTypes()</c> throws
    /// <see cref="ReflectionTypeLoadException"/> when any referenced type cannot be resolved, and
    /// the resolvable types are still available on the exception. In-game
    /// <c>CollectAssemblyTypesShim</c> already converts this to a partial list, but relying on it
    /// would make registration depend on module load order — so handle it here too.
    /// </summary>
    public static IReadOnlyList<Type> CollectUiExtensionTypes(Assembly assembly)
    {
        if (assembly == null) return Array.Empty<Type>();

        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types ?? Array.Empty<Type>();
        }

        return types.Where(t => t != null).Where(HasUiExtenderAttribute).ToList();
    }

    /// <summary>
    /// Reading <c>CustomAttributes</c> resolves each attribute's type, which throws for a type
    /// whose attribute lives in an assembly this process cannot load (e.g. a MissionView-derived
    /// type referencing TaleWorlds.MountAndBlade.View outside the game). One unloadable type must
    /// not cost us the whole registration, so failures are per-type and mean "not an extension".
    /// </summary>
    private static bool HasUiExtenderAttribute(Type type)
    {
        try
        {
            return type.CustomAttributes.Any(a =>
                a.AttributeType.IsSubclassOf(typeof(BaseUIExtenderAttribute)));
        }
        catch
        {
            return false;
        }
    }

    private static bool IsSuppressed(Type type) =>
        Attribute.IsDefined(type, typeof(CoopSuppressedUiAttribute));
}
