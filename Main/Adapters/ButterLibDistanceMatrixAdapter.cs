using System;
using System.Reflection;

namespace TAOM.Adapters;

/// <summary>
/// Calls <c>DistanceMatrixSubSystem.Disable()</c> by reflection, the same way the crash report suspends ButterLib's
/// exception handler (<c>ButterLibExceptionHandlerAdapter</c>), so TAOM keeps no compile-time reference to ButterLib.
/// ButterLib enables the subsystem in its OnSubModuleLoad (and MCM's ButterLib page applies its stored toggle in its
/// first main-menu hook); both run before TAOM's main-menu phase. ButterLib honours <c>Disable()</c> until a campaign
/// starts; with the subsystem off, its <c>GeopoliticsBehavior</c> is never added.
/// </summary>
public sealed class ButterLibDistanceMatrixAdapter : IButterLibDistanceMatrixAdapter
{
    internal const string SubSystemTypeName = "Bannerlord.ButterLib.Implementation.DistanceMatrix.DistanceMatrixSubSystem";

    private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;
    private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;

    private readonly Func<string, Type?> _findType;

    public ButterLibDistanceMatrixAdapter() : this(FindByExactName) { }

    internal ButterLibDistanceMatrixAdapter(Func<string, Type?> findType) => _findType = findType;

    public string? TryDisable(out bool wasAlreadyOff)
    {
        wasAlreadyOff = false;
        var type = _findType(SubSystemTypeName);
        if (type == null) return "ButterLib's DistanceMatrixSubSystem is not loaded";

        var problem = ShapeProblem(type);
        if (problem != null) return problem;

        var instance = type.GetProperty("Instance", PublicStatic)!.GetValue(null);
        if (instance == null) return "DistanceMatrixSubSystem has no instance";

        var isEnabled = type.GetProperty("IsEnabled", PublicInstance)!;
        if (!(bool)isEnabled.GetValue(instance))
        {
            wasAlreadyOff = true;
            return null;
        }

        type.GetMethod("Disable", PublicInstance, null, Type.EmptyTypes, null)!.Invoke(instance, null);
        return (bool)isEnabled.GetValue(instance) ? "Disable() did not take: IsEnabled is still true" : null;
    }

    /// <summary>Null when <paramref name="type"/> has the members <see cref="TryDisable"/> calls. Internal for the binding test.</summary>
    internal static string? ShapeProblem(Type type)
    {
        if (type.GetProperty("Instance", PublicStatic) == null) return "DistanceMatrixSubSystem shape changed: no static Instance";
        if (type.GetProperty("IsEnabled", PublicInstance)?.PropertyType != typeof(bool)) return "DistanceMatrixSubSystem shape changed: no bool IsEnabled";
        if (type.GetMethod("Disable", PublicInstance, null, Type.EmptyTypes, null) == null) return "DistanceMatrixSubSystem shape changed: no Disable()";
        return null;
    }

    private static Type? FindByExactName(string fullName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(fullName, throwOnError: false);
            if (type != null) return type;
        }
        return null;
    }
}
