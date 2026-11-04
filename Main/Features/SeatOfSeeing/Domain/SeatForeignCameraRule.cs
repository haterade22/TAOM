using System;

namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>
/// Build-spec 5.6. A mission view whose <c>UpdateOverridenCamera</c> is declared outside the TaleWorlds and SandBox assemblies
/// means another camera owns the mission, so the Seat stays out. No list of names: this one rule covers the Fords commander
/// camera and any other mod's view. The Seat's own view is never foreign.
/// </summary>
internal static class SeatForeignCameraRule
{
    internal static bool IsForeign(string? declaringAssemblyName, bool isSeatView)
    {
        if (isSeatView)
            return false;
        return string.IsNullOrEmpty(declaringAssemblyName) || !IsVanilla(declaringAssemblyName!);
    }

    private static bool IsVanilla(string name) =>
        name == "SandBox" || name.StartsWith("SandBox.", StringComparison.Ordinal)
        || name == "TaleWorlds" || name.StartsWith("TaleWorlds.", StringComparison.Ordinal);
}
