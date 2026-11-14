using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Features.MarshalsWord.Hooks;

/// <summary>
/// Patch141 (docs/features/marshals-word.md, build-spec 9.3): the spacing repair. Vanilla's four spacing limits and the three public
/// <c>GetUnitPositionWithIndexAccordingToNewOrder</c> overloads test <c>HasAnyMountedUnit</c>, so one rider gives an infantry company cavalry limits, while the live values
/// (<c>UnitDiameter</c>, <c>Interval</c>, <c>Distance</c>) test <c>CalculateHasSignificantNumberOfMounted</c>. One transpiler over those seven methods swaps the one call, so the
/// limits agree with the live values. It applies at patch time only, when Marshal's Word and the setting are on and neither co-op nor a dedicated server is present, because a host
/// and a client must not compute different widths. It reads no setting and no state at run time: the getters run on workers.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(MarshalsWordModule.PatchCategory)]
public static class MarshalSpacingPatch
{
    private static readonly string[] LimitGetters =
    {
        nameof(Formation.MinimumDistance), nameof(Formation.MaximumDistance), nameof(Formation.MinimumInterval), nameof(Formation.MaximumInterval),
    };

    /// <summary>Why the repair is not applied, an empty string once it is, or "not-prepared" before the patch category runs.</summary>
    public static string WhyNot { get; internal set; } = "not-prepared";

    [HarmonyPrepare]
    public static bool Prepare()
    {
        try
        {
            var settings = IoC.Resolve<IMarshalSettingsProvider>();
            WhyNot = MarshalSpacingRules.WhyNot(settings.Enabled, settings.SpacingRepair, IoC.Resolve<ICoopPresenceProvider>().IsCoopActive,
                IoC.Resolve<IDedicatedServerProvider>().IsDedicatedServer);
        }
        catch (Exception ex)
        {
            WhyNot = "prepare-failed";
            Outcomes.Swallowed("MarshalsWord", "MarshalSpacingPatch.Prepare", ex);
        }

        return WhyNot.Length == 0;
    }

    [HarmonyTargetMethods]
    public static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var name in LimitGetters)
            yield return AccessTools.PropertyGetter(typeof(Formation), name);
        foreach (var method in typeof(Formation).GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => m.Name == nameof(Formation.GetUnitPositionWithIndexAccordingToNewOrder)))
            yield return method;
    }

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        var list = instructions.ToList();
        var from = AccessTools.PropertyGetter(typeof(Formation), nameof(Formation.HasAnyMountedUnit));
        var to = AccessTools.PropertyGetter(typeof(Formation), nameof(Formation.CalculateHasSignificantNumberOfMounted));
        var sites = list.Where(i => i.operand is MethodBase m && m == from).ToList();
        if (sites.Count != 1)
        {
            LogDegraded($"found {sites.Count} calls of HasAnyMountedUnit in {original?.Name} where it expects one, so that method is left as it is.");
            return list;
        }

        sites[0].operand = to;
        return list;
    }

    private static void LogDegraded(string detail)
    {
        SelfDisables.Report(MarshalsWordModule.PatchCategory + ".SpacingRepair", detail);
        try
        {
            IoC.Resolve<IModLogger>()?.LogError("[MarshalsWord] the spacing repair " + detail);
        }
        catch
        {
            // swallow-ok: a logger that cannot be resolved must not surface to the transpiler's caller
        }
    }
}
