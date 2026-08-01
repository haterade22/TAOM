using System;
using System.Collections.Generic;
using System.Text;
using TAOM.Core.Logging;
using TaleWorlds.MountAndBlade;
using ActionSetCode = TaleWorlds.Core.ActionSetCode;
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
///     <see cref="MaxLinesPerKey"/> times, and every emitter counts against
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
            var set = MBActionSet.GetActionSet(
                ActionSetCode.GenerateActionSetNameWithSuffix(monster, isFemale, suffix));
            string tag = isFemale ? "female" : "male";
            string line = $"  {raceName} {tag}{suffix}: {Describe(set)}";

            if (!set.IsValid)
            {
                LogError(line + "  <-- UNRESOLVED");
                return;
            }

            // NOTE: deliberately does NOT resolve an action here. `ActionIndexCache` has an explicit
            // static constructor, so the type is not beforefieldinit and ANY static access — field
            // OR the Create() method — forces 215 fields (v1.4.7) to be populated via
            // MBAnimation.GetActionCodeWithName. Do that before the engine has loaded action types
            // and every index is baked to -1 for the process lifetime, because the fields are
            // readonly and the cctor never re-runs. A diagnostic must not be able to cause that.
            // The clip check lives in ProbeActionIndexHealth(), called from a late path.
            LogAlways(line);
        }
        catch (Exception e)
        {
            LogError($"  {raceName} {suffix}: probe threw {e.GetType().Name}: {e.Message}");
        }
    }

    private static bool _indexHealthProbed;

    /// <summary>
    /// The decisive A/B test for the prone-tableau report, and the reason it can be answered without
    /// reproducing the bug.
    ///
    /// `ActionIndexCache.act_inventory_idle_start` is a static readonly field baked ONCE by the type's
    /// static constructor. `ActionIndexCache.Create(name)` performs a LIVE native lookup on every
    /// call. Reading both at the same instant therefore separates two states that look identical
    /// from any single reading:
    ///
    ///   static == Create   -> the statics were initialised while action types were loaded. Healthy.
    ///   static == -1, Create valid -> the cctor ran TOO EARLY and every one of its 215 fields (v1.4.7) is
    ///                                 permanently -1. Vanilla CharacterTableau.GetIdleAction()
    ///                                 returns that field, so SetAction(-1) is a no-op and every
    ///                                 character in every tableau stays in bind pose. This is the
    ///                                 reported defect, and it is unrecoverable for the session.
    ///   both -1 -> action types genuinely are not loaded yet at this call site; the call site is
    ///              too early and must move later (and must not have touched the type at all).
    ///
    /// MUST only be called from a late path — first tableau construction or later. Calling it early
    /// would itself force the initialisation it is trying to detect.
    /// </summary>
    public static void ProbeActionIndexHealth(string phase)
    {
        try
        {
            lock (_gate)
            {
                if (_indexHealthProbed) return;
                _indexHealthProbed = true;
            }

            // Live lookup FIRST. If the type is still uninitialised this is what triggers the cctor,
            // and at this call site action types are loaded — so triggering it here is the safe
            // outcome, not the harmful one.
            int live = -1;
            try { live = ActionIndexCache.Create("act_inventory_idle_start").Index; } catch { }

            int baked = -1;
            try { baked = ActionIndexCache.act_inventory_idle_start.Index; } catch { }

            LogAlways($"===== ACTION-INDEX HEALTH ({phase}) =====");
            LogAlways($"act_inventory_idle_start: static(baked)={baked}  Create(live)={live}");

            if (baked < 0 && live >= 0)
            {
                LogError(
                    "ActionIndexCache STATICS ARE POISONED — the type's static constructor ran before the " +
                    "engine loaded action types, so all 215 static action indices are permanently -1 while " +
                    "live lookups work. Vanilla CharacterTableau.GetIdleAction() returns the static, so " +
                    "SetAction is a no-op and every tableau character renders in bind pose. Unrecoverable " +
                    "this session: the fields are readonly and the cctor does not re-run.");
            }
            else if (baked < 0 && live < 0)
            {
                LogError(
                    "act_inventory_idle_start is -1 from BOTH the static and a live lookup — action types " +
                    "are not loaded even at this late call site. Investigate module/ModuleData load order " +
                    "before drawing conclusions about the statics.");
            }
            else
            {
                LogAlways("ActionIndexCache statics are healthy (baked index matches a live lookup).");
            }

            // Now that an index is known good, the per-race clip check is meaningful. This is the
            // check that distinguishes "action set resolves" from "action set can actually pose".
            ProbeIdleClipsPerRace();

            LogAlways("===== END ACTION-INDEX HEALTH =====");
        }
        catch (Exception e)
        {
            LogError($"ProbeActionIndexHealth failed: {e}");
        }
    }

    /// <summary>
    /// Per race, whether the sets the preview paths actually use bind a clip to
    /// act_inventory_idle_start. A set can be VALID and still bind nothing — the snapshot README
    /// records that the engine does not fall through base_set for act_inventory_*, and
    /// `as_&lt;race&gt;_warrior` is a zero-action stub for several races.
    /// </summary>
    private static void ProbeIdleClipsPerRace()
    {
        string[]? raceNames = null;
        try { raceNames = FaceGen.GetRaceNames(); } catch { }
        if (raceNames == null) return;

        var idle = ActionIndexCache.Create("act_inventory_idle_start");
        for (int race = 0; race < raceNames.Length; race++)
        {
            Monster? monster = null;
            try { monster = FaceGen.GetBaseMonsterFromRace(race); }
            catch (Exception e) { LogError($"  {raceNames[race]}: monster lookup threw {e.GetType().Name}"); }
            if (monster == null) continue;

            foreach (var suffix in new[] { "_facegen", "_warrior" })
            {
                // try/catch INSIDE the suffix loop: with it outside, a throw while probing _facegen
                // skipped _warrior entirely and silently halved the coverage for that race.
                try
                {
                    var set = MBActionSet.GetActionSet(
                        ActionSetCode.GenerateActionSetNameWithSuffix(monster, false, suffix));
                    string line = $"  {raceNames[race]}{suffix}: idleStart-anim={DescribeAction(set, idle)}";

                    // Positive requirement, not a string match on one sentinel. DescribeAction has
                    // FOUR failure markers and an earlier version compared only against "<NONE>", so
                    // "<action-index-(-1)>" — the poisoned-index case this whole feature exists to
                    // catch — fell into the healthy branch and logged as INFO.
                    if (!HasAnimation(set, idle)) LogError(line + "  <-- no usable idle clip (bind-pose)");
                    else LogAlways(line);
                }
                catch (Exception e)
                {
                    LogError($"  {raceNames[race]}{suffix}: idle-clip probe threw {e.GetType().Name}");
                }
            }
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
