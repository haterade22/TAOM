using System;
using System.Collections.Generic;

namespace TAOM.Features.DevConsole;

/// <summary>
/// What <c>GauntletInformationView.OnShowTooltip</c> left behind, read after it returned. Derived
/// from its two private fields, which <c>OnHideTooltip()</c> nulls at the top of every call and
/// which the success path assigns in order: <c>_dataSource</c> after the view model is constructed,
/// <c>_movie</c> only after <c>LoadMovie</c> returns. That order is what makes the three outcomes
/// distinguishable from outside.
/// </summary>
internal enum TooltipBuildOutcome
{
    /// <summary>View model constructed and movie loaded. The tooltip exists and should render.</summary>
    Built,

    /// <summary>
    /// View model constructed, then <c>LoadMovie</c> threw: <c>_dataSource</c> set, <c>_movie</c>
    /// null. The tooltip's data is fine; the prefab or movie is not.
    /// </summary>
    ConstructedButMovieFailed,

    /// <summary>
    /// Nothing usable was built: the type is missing from <c>RegisteredTypes</c>, its constructor
    /// threw, or the registered type is not a <c>TooltipBaseVM</c>.
    /// </summary>
    NotConstructed,
}

/// <summary>
/// Rate limiting and message text for the tooltip probes (Patch79).
///
/// Both probes sit on per-hover engine paths, so they cannot log unconditionally. They equally cannot
/// use a single global latch: that was the defect in SpecialResourceMapBarMixin, where the first
/// failure was logged and every later DIFFERENT failure was invisible for the life of the process.
/// So the limit is per key, and the build probe keys on outcome as well as type, which means a
/// tooltip that fails and later succeeds reports both. That transition is the most informative thing
/// these probes can produce and a naive per-type latch would swallow it.
///
/// Both key sets are built without allocating. The build key is a value tuple rather than an
/// interpolated string, because the membership check runs on every tooltip request, including the
/// steady-state path where nothing is logged, and a string key would allocate on each of them.
///
/// Pure and engine-free so the limiting is unit tested directly; the patches hold one instance each.
/// </summary>
internal sealed class TooltipProbeLog
{
    private const string Unknown = "(unknown)";

    private const string Invisible =
        "The engine downgrades this to Debug.FailedAssert, which reaches only rgl_log, so this line "
        + "is the sole evidence on an install without one.";

    private readonly HashSet<string> _hovers = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<(string Type, TooltipBuildOutcome Outcome)> _builds =
        new HashSet<(string Type, TooltipBuildOutcome Outcome)>();

    /// <summary>
    /// True the first time this bar item is hovered. A hover that never produces one of these means
    /// the command never reached the view model, which is the binding-path failure: Gauntlet walks
    /// MapInfo to PrimaryInfoItems to [index] to ExecuteBeginHint and invokes it null-conditionally,
    /// so a failed path resolution is completely silent.
    /// </summary>
    internal bool TryRecordHover(string itemId, out string message)
    {
        var key = Key(itemId);
        if (!_hovers.Add(key))
        {
            message = string.Empty;
            return false;
        }

        message = $"[TooltipProbe] hover reached MapInfoItemVM '{key}'. The binding path resolves for "
                + "this item, so any missing tooltip for it is downstream of the view model.";
        return true;
    }

    /// <summary>True the first time this tooltip type is seen with this outcome.</summary>
    internal bool TryRecordBuild(string tooltipTypeName, TooltipBuildOutcome outcome, out string message)
    {
        var type = Key(tooltipTypeName);
        if (!_builds.Add((type, outcome)))
        {
            message = string.Empty;
            return false;
        }

        message = outcome switch
        {
            TooltipBuildOutcome.Built =>
                $"[TooltipProbe] tooltip built for '{type}': view model constructed and movie loaded.",

            TooltipBuildOutcome.ConstructedButMovieFailed =>
                $"[TooltipProbe] tooltip build FAILED for '{type}' at the MOVIE stage: the view model was "
                + "constructed but LoadMovie threw, so the tooltip data is fine and the prefab or movie "
                + "is not. " + Invisible,

            _ =>
                $"[TooltipProbe] tooltip build FAILED for '{type}' at the CONSTRUCTION stage: nothing "
                + "usable was built. Either the type is missing from RegisteredTypes, its view model "
                + "constructor threw, or the registered type is not a TooltipBaseVM. " + Invisible,
        };
        return true;
    }

    /// <summary>Lets everything report again, for a fresh campaign in the same process.</summary>
    internal void Reset()
    {
        _hovers.Clear();
        _builds.Clear();
    }

    private static string Key(string value) => string.IsNullOrEmpty(value) ? Unknown : value;
}
