using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.HeroRace.Diagnostics;

/// <summary>
/// TEMPORARY instrumentation for the "prone / bendy man" character-tableau report (2026-07-31).
///
/// The defect renders every race lying in bind pose in Character Customization, the inventory
/// doll, and the encyclopedia. It does not reproduce on the dev machine, it is intermittent
/// per-launch on affected machines, and two users' logs cleared every layer TAOM already
/// instruments (engine version, PatchShield, race registration). The character-preview path
/// itself emits nothing, so the failure window is completely dark — this class lights it up.
///
/// Design constraints, because this ships to players:
///   - NEVER throws. Every public entry point is wrapped; a diagnostic must not create a bug.
///   - Throttled. Tableau refreshes fire per frame in some screens; unthrottled logging produced
///     a 6.4 MB session log once already (commit ae2ed426). Each distinct key logs at most
///     <see cref="MaxLinesPerKey"/> times, and every emitter counts against
///     <see cref="MaxTotalLines"/>.
///   - Resolves the logger lazily and caches the failure, so a pre-IoC call site is free.
///
/// Reduced 2026-08-01: the per-race action-set probe, environment dump and action-index health probe
/// were removed once they had identified the root cause (issue #371). What remains is the reporting
/// the ActionIndexCache repair and the tableau patches need in order to state, in one line, whether
/// a session hit the fault. Remove entirely once #371 is confirmed closed in the wild.
/// </summary>
public static class TableauDiagnostics
{
    private const string Tag = "[TableauDiag]";

    // One line per distinct situation, not per occurrence. Tableau refreshes fire per frame on some
    // screens and the ERROR paths fire on exactly the machines that are already broken, so an
    // unthrottled error is the worst case, not the best one (a 6.4 MB session log already happened
    // once — commit ae2ed426). Repeats add no information: the same race resolving the same set the
    // same way is one fact, however many times it is observed.
    private const int MaxLinesPerKey = 1;
    private const int MaxTotalLines = 600;

    private static readonly object _gate = new();

    // Separate key spaces. _seen is keyed by a caller-supplied token ("p2.3.True"); _seenErrors is
    // keyed by full message text. Sharing one dictionary meant whichever fired first could silently
    // suppress the other, and a missing ERROR line is invisible by construction.
    private static readonly Dictionary<string, int> _seen = new(StringComparer.Ordinal);
    private static readonly HashSet<string> _seenErrors = new(StringComparer.Ordinal);
    private static int _total;
    private static IModLogger? _logger;
    private static bool _loggerUnavailable;

    private static IModLogger? Logger
    {
        get
        {
            if (_logger != null || _loggerUnavailable) return _logger;
            try
            {
                _logger = IoC.Resolve<IModLogger>();
            }
            catch
            {
                // IoC not configured yet (very early call site). Try again on the next call —
                // only give up permanently if resolution returns null rather than throwing.
                return null;
            }
            if (_logger == null) _loggerUnavailable = true;
            return _logger;
        }
    }

    /// <summary>Logs at most <see cref="MaxLinesPerKey"/> lines per distinct <paramref name="key"/>.</summary>
    public static void Log(string key, string message)
    {
        try
        {
            lock (_gate)
            {
                if (_total >= MaxTotalLines) return;
                _seen.TryGetValue(key, out int count);
                if (count >= MaxLinesPerKey) return;
                _seen[key] = count + 1;
                _total++;
            }
            Logger?.LogInfo($"{Tag} {message}");
        }
        catch { /* diagnostics must never surface an exception to the caller */ }
    }

    /// <summary>
    /// For ONE-SHOT probe output. Not per-key throttled, but it does count against
    /// <see cref="MaxTotalLines"/> — an earlier version bypassed the cap entirely, which made the
    /// class doc's "capped" claim false and left the door open for unbounded growth if a caller on a
    /// repeatable path ever used it. Anything reachable more than once per session must use
    /// <see cref="Log"/> or <see cref="LogDeduped"/> instead.
    /// </summary>
    public static void LogAlways(string message)
    {
        try
        {
            lock (_gate)
            {
                if (_total >= MaxTotalLines) return;
                _total++;
            }
            Logger?.LogInfo($"{Tag} {message}");
        }
        catch { }
    }

    /// <summary>
    /// Informational line emitted at most once per <paramref name="key"/>. For call sites that are
    /// reachable repeatedly but whose message is only worth stating once (e.g. a repair deferring on
    /// every preview path while action types are still loading).
    /// </summary>
    public static void LogDeduped(string key, string message)
    {
        try
        {
            lock (_gate)
            {
                if (_total >= MaxTotalLines) return;
                _seen.TryGetValue(key, out int count);
                if (count > 0) return;
                _seen[key] = 1;
                _total++;
            }
            Logger?.LogInfo($"{Tag} {message}");
        }
        catch { }
    }

    /// <summary>
    /// Deduplicated by message text. An error here means the preview is already broken, and that
    /// state repeats on every refresh — so an unthrottled error would flood the log of exactly the
    /// user we most need a readable log from.
    /// </summary>
    public static void LogError(string message)
    {
        try
        {
            lock (_gate)
            {
                if (_total >= MaxTotalLines) return;
                if (!_seenErrors.Add(message)) return;
                _total++;
            }
            Logger?.LogError($"{Tag} {message}");
        }
        catch { }
    }

    /// <summary>Renders an <see cref="MBActionSet"/> as "name/skeleton" or "INVALID".</summary>
    public static string Describe(MBActionSet set)
    {
        try
        {
            if (!set.IsValid) return "INVALID(-1)";
            string name;
            string skeleton;
            try { name = set.GetName() ?? "<null>"; } catch (Exception e) { name = "<threw:" + e.GetType().Name + ">"; }
            try { skeleton = set.GetSkeletonName() ?? "<null>"; } catch (Exception e) { skeleton = "<threw:" + e.GetType().Name + ">"; }
            return $"valid name='{name}' skeleton='{skeleton}'";
        }
        catch { return "<describe-failed>"; }
    }

    /// <summary>
    /// Reports one tableau visual construction from <c>CharacterSpawnerService.InitWithCharacter</c>.
    /// Escalates to ERROR for the two states that produce a bind-pose character: an unresolved
    /// action set, or a pose action index of -1 (the named action does not exist in that set).
    /// </summary>
    public static void LogSpawnerResolution(
        int race, bool isFemale, string? monsterId, string? suffix, MBActionSet set,
        string? poseActionName, int poseActionIndex, int idleStartIndex, float animationProgress)
    {
        try
        {
            string head =
                $"Spawner: race={race} female={isFemale} monster='{monsterId ?? "<null>"}' suffix='{suffix ?? "<null>"}' " +
                $"-> {Describe(set)}; pose='{poseActionName ?? "<null>"}' poseIdx={poseActionIndex} " +
                $"idleStartIdx={idleStartIndex} progress={animationProgress:F3}";

            if (!set.IsValid)
            {
                LogError(head + "  <-- ACTION SET UNRESOLVED (bind-pose condition)");
            }
            else if (poseActionIndex < 0)
            {
                LogError(head + "  <-- POSE ACTION NOT FOUND in this action set (bind-pose condition)");
            }
            else if (idleStartIndex < 0)
            {
                LogError(head + "  <-- act_inventory_idle_start NOT FOUND in this action set");
            }
            else
            {
                Log($"spawn.{race}.{isFemale}.{suffix}", head);
            }
        }
        catch { }
    }




    /// <summary>
    /// Reports one character's tableau resource-residency verdict (issue #389, black silhouettes).
    ///
    /// A STUCK verdict is the smoking gun: vanilla's <c>OnTick</c> decrements
    /// <c>_agentVisualLoadingCounter</c> only when <c>CheckResources</c> returns true, and only shows
    /// the refreshed visual once that counter hits zero. A counter that never reaches zero therefore
    /// means the character's meshes/materials never became resident — while the previous buffer was
    /// already made visible by <c>RefreshCharacterTableau</c> with no resource gate at all. Against
    /// <c>SetSceneUsesSkybox(false)</c> + <c>SetClearColor(0u)</c> that renders as an opaque black
    /// silhouette.
    ///
    /// Read the log by comparing troops: if <c>urukhai_fighter</c> reports STUCK while
    /// <c>isengard_orc_ravager</c> reports RESOLVED, the fault is per-race resource residency and the
    /// asset hypothesis is confirmed. If BOTH resolve, residency is not the mechanism and the black
    /// render is happening downstream of it.
    /// </summary>
    public static void LogRenderCensus(
        string? characterKey,
        TableauResidencyVerdict verdict,
        int ticks,
        string context,
        System.Collections.Generic.IReadOnlyList<string>? census)
    {
        try
        {
            if (verdict == TableauResidencyVerdict.CapacityReached)
            {
                LogError(
                    "Render census tracker is FULL — further characters will produce no line at all. " +
                    "A missing troop below this point means 'not measured', NOT 'nothing wrong'. " +
                    "Restart and open the troop of interest first.");
                return;
            }

            string head = verdict == TableauResidencyVerdict.Timeout
                ? $"Render census (visual NEVER became ready in {ticks} ticks — the tableau is most likely " +
                  $"BLANK rather than black; census below is partial): {context}"
                : $"Render census (ready after {ticks} tick(s)): {context}";

            // Deliberately reported at INFO even for Timeout. This instrument states observations; the
            // reader draws the conclusion. An earlier version asserted "this is the black-silhouette
            // condition", which was refuted against the v1.4.7 decompile.
            var sb = new System.Text.StringBuilder(head);
            if (census != null)
            {
                foreach (var line in census)
                {
                    sb.Append(Environment.NewLine).Append("    ").Append(line);
                }
            }

            Log($"census.{characterKey}", sb.ToString());
        }
        catch { }
    }

    /// <summary>Animation clip bound to <paramref name="action"/> in <paramref name="set"/>, or a marker.</summary>
    public static string DescribeAction(MBActionSet set, ActionIndexCache action)
    {
        try
        {
            if (!set.IsValid) return "<set-invalid>";
            if (action.Index < 0) return "<action-index-(-1)>";
            var anim = set.GetAnimationName(in action);
            return string.IsNullOrEmpty(anim) ? "<NONE>" : $"'{anim}'";
        }
        catch (Exception e) { return "<threw:" + e.GetType().Name + ">"; }
    }

    /// <summary>
    /// Positive predicate for "this set can actually pose a character with this action". Public
    /// because callers must test THIS rather than string-matching <see cref="DescribeAction"/>'s
    /// output — that method has four distinct failure markers and matching only one of them lets the
    /// others through as healthy.
    /// </summary>
    public static bool HasAnimation(MBActionSet set, ActionIndexCache action)
    {
        try
        {
            if (!set.IsValid || action.Index < 0) return false;
            return !string.IsNullOrEmpty(set.GetAnimationName(in action));
        }
        catch { return false; }
    }

    private static string Safe(Func<string?> f)
    {
        try { return f() ?? "<null>"; }
        catch (Exception e) { return "<threw:" + e.GetType().Name + ">"; }
    }
}
