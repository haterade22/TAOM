using System;
using System.Collections.Generic;
using System.Text;
using TAOM.Core.Logging;
using TaleWorlds.MountAndBlade;
using FaceGen = TaleWorlds.Core.FaceGen;
using Monster = TaleWorlds.Core.Monster;

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
///     <see cref="MaxLinesPerKey"/> times, and the whole class is capped at
///     <see cref="MaxTotalLines"/>.
///   - Resolves the logger lazily and caches the failure, so a pre-IoC call site is free.
///
/// Remove this file and its call sites once the root cause is found.
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
    private static readonly Dictionary<string, int> _seen = new(StringComparer.Ordinal);
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

    /// <summary>Unthrottled — for one-shot startup probes only.</summary>
    public static void LogAlways(string message)
    {
        try { Logger?.LogInfo($"{Tag} {message}"); }
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
                if (!_seen.TryGetValue(message, out int seen) || seen == 0)
                {
                    _seen[message] = 1;
                    _total++;
                }
                else return;
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

    private static bool _envDumped;

    /// <summary>
    /// One-shot environment dump. The defect does not reproduce on the dev machine, so the whole
    /// question is "what is different here" — this records the things that could plausibly differ
    /// and that no other TAOM log captures.
    /// </summary>
    public static void DumpEnvironment()
    {
        try
        {
            lock (_gate)
            {
                if (_envDumped) return;
                _envDumped = true;
            }

            LogAlways("===== ENVIRONMENT =====");
            LogAlways($"OS={Environment.OSVersion} 64bit={Environment.Is64BitProcess} cores={Environment.ProcessorCount} CLR={Environment.Version}");

            // Assembly identity for the stack that actually drives patching and UI. A duplicate or
            // unexpected location for any of these is a load-order/dependency problem.
            foreach (var name in new[] { "0Harmony", "Bannerlord.UIExtenderEx", "TAOM", "TAOM.Dependencies", "Bannerlord.ButterLib" })
            {
                try
                {
                    var matches = new List<string>();
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        var n = asm.GetName();
                        if (!string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
                        string loc;
                        try { loc = asm.Location; } catch { loc = "<no location>"; }
                        matches.Add($"v{n.Version} @ {loc}");
                    }
                    if (matches.Count == 0) LogAlways($"  assembly '{name}': NOT LOADED");
                    else if (matches.Count == 1) LogAlways($"  assembly '{name}': {matches[0]}");
                    else LogError($"  assembly '{name}': {matches.Count} COPIES LOADED -> {string.Join(" | ", matches)}");
                }
                catch (Exception e) { LogError($"  assembly '{name}': probe threw {e.GetType().Name}"); }
            }

            LogAlways("===== END ENVIRONMENT =====");
        }
        catch (Exception e)
        {
            LogError($"DumpEnvironment failed: {e}");
        }
    }

    private static readonly HashSet<string> _probedPhases = new(StringComparer.Ordinal);
    private static string? _firstProbeSignature;

    /// <summary>
    /// Dump of the entire race → monster → action-set resolution table, plus the engine's global
    /// action-set count. Runs once per distinct <paramref name="phase"/>.
    ///
    /// Called at two phases deliberately: once when patches are applied, and again on the first
    /// real tableau construction. If the counts or per-race results DIFFER between the two, module
    /// action sets are still loading/merging after startup — which is the shape of a defect that
    /// appears on some launches and not others.
    ///
    /// The single most valuable line here is the action-set TOTAL: if a broken launch reports a
    /// different count from a good launch on the same machine, the module action_sets did not all
    /// merge that boot, which would explain both the symptom and its per-launch randomness.
    /// </summary>
    public static void ProbeActionSets(string phase)
    {
        try
        {
            lock (_gate)
            {
                if (!_probedPhases.Add(phase)) return;
            }

            int totalSets = -1;
            try { totalSets = MBActionSet.GetNumberOfActionSets(); } catch { }

            int raceCount = -1;
            try { raceCount = FaceGen.GetRaceCount(); } catch { }

            // Re-probes exist only to catch action sets still merging AFTER startup. When nothing
            // changed there is nothing to report, so collapse the whole second dump to one line
            // rather than repeating ~50 identical lines.
            string signature = $"{totalSets}/{raceCount}";
            if (_firstProbeSignature != null)
            {
                if (_firstProbeSignature == signature)
                {
                    LogAlways($"re-probe ({phase}): unchanged ({signature}) — action sets stable since startup.");
                    return;
                }
                LogError($"re-probe ({phase}): CHANGED since startup — was {_firstProbeSignature}, now {signature}. " +
                         "Action sets were still loading after startup; full re-dump follows.");
            }
            _firstProbeSignature = signature;

            LogAlways($"===== ACTION-SET PROBE ({phase}) =====");
            LogAlways($"engine action_set count = {totalSets}   race count = {raceCount}");

            string[]? raceNames = null;
            try { raceNames = FaceGen.GetRaceNames(); } catch (Exception e) { LogError($"GetRaceNames threw: {e}"); }

            if (raceNames == null)
            {
                LogError("GetRaceNames returned null — cannot probe per-race action sets.");
                return;
            }

            LogAlways($"race names ({raceNames.Length}): {string.Join(", ", raceNames)}");

            for (int race = 0; race < raceNames.Length; race++)
            {
                string raceName = raceNames[race] ?? "<null>";
                Monster? monster = null;
                try { monster = FaceGen.GetBaseMonsterFromRace(race); }
                catch (Exception e) { LogError($"race {race} '{raceName}': GetBaseMonsterFromRace threw: {e.Message}"); }

                if (monster == null)
                {
                    LogError($"race {race} '{raceName}': monster is NULL — tableau cannot resolve an action set for this race.");
                    continue;
                }

                var sb = new StringBuilder();
                sb.Append($"race {race} '{raceName}' monster='{Safe(() => monster.StringId)}' ")
                  .Append($"base='{Safe(() => monster.BaseMonster)}' ")
                  .Append($"actionSetCode='{Safe(() => monster.ActionSetCode)}' ")
                  .Append($"femaleActionSetCode='{Safe(() => monster.FemaleActionSetCode)}'");
                LogAlways(sb.ToString());

                // The two suffixes the preview paths actually use: the customization screen goes
                // through "_facegen", the inventory/encyclopedia tableau through "_warrior".
                ProbeSuffix(raceName, monster, isFemale: false, suffix: "_facegen");
                ProbeSuffix(raceName, monster, isFemale: true, suffix: "_facegen");
                ProbeSuffix(raceName, monster, isFemale: false, suffix: "_warrior");
            }

            LogAlways("===== END ACTION-SET PROBE =====");
        }
        catch (Exception e)
        {
            LogError($"ProbeActionSets failed: {e}");
        }
    }

    private static void ProbeSuffix(string raceName, Monster monster, bool isFemale, string suffix)
    {
        try
        {
            var set = MBGlobals.GetActionSetWithSuffix(monster, isFemale, suffix);
            string tag = isFemale ? "female" : "male";
            string line = $"  {raceName} {tag}{suffix}: {Describe(set)}";

            if (!set.IsValid)
            {
                LogError(line + "  <-- UNRESOLVED");
                return;
            }

            // A VALID action set is not sufficient. CharacterTableau.GetIdleAction() falls back to
            // act_inventory_idle_start, and the LOTRLOME snapshot README records that the engine
            // does NOT fall through base_set for act_inventory_* — so a race whose set is a thin
            // base_set stub can resolve fine here and still have no idle animation to play, which
            // the engine renders as the skeleton's bind pose. Resolve the actual clip to find out.
            line += "  idleStart-anim=" + DescribeAction(set, ActionIndexCache.act_inventory_idle_start);
            if (!HasAnimation(set, ActionIndexCache.act_inventory_idle_start))
                LogError(line + "  <-- NO act_inventory_idle_start CLIP (bind-pose risk)");
            else
                LogAlways(line);
        }
        catch (Exception e)
        {
            LogError($"  {raceName} {suffix}: probe threw {e.GetType().Name}: {e.Message}");
        }
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

    private static bool HasAnimation(MBActionSet set, ActionIndexCache action)
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
