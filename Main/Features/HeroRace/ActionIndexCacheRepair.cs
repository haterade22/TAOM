using System;
using System.Collections.Generic;
using System.Reflection;
using TAOM.Features.HeroRace.Diagnostics;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.HeroRace;

/// <summary>
/// Repairs <see cref="ActionIndexCache"/>'s static action indices when they were baked before the
/// engine loaded action types.
///
/// The fault: <c>ActionIndexCache</c> declares 215 <c>static readonly</c> fields (v1.4.7) populated
/// by an explicit static constructor, each via <c>Create(name)</c> →
/// <c>MBAnimation.GetActionCodeWithName</c>. Because the cctor is explicit the type is not
/// <c>beforefieldinit</c>, so ANY static member access — field or the <c>Create</c> method — forces
/// the whole set to initialise. If that happens before action types are loaded, every index is baked
/// to <c>-1</c> for the life of the process, and the fields being <c>readonly</c> means the cctor
/// never re-runs to correct them.
///
/// The consequence is player-visible: vanilla <c>CharacterTableau.GetIdleAction()</c> returns
/// <c>ActionIndexCache.act_inventory_idle_start</c>, so <c>SetAction(-1)</c> is a no-op and every
/// character in every UI tableau renders in its skeleton's bind pose — lying flat. It presents as
/// "all races, intermittent per launch", because whether it happens depends on load-order timing.
///
/// Safety properties:
///   - No-op when the statics are healthy, so it is safe to ship before the diagnosis is confirmed.
///   - Never touches <c>ActionIndexCache</c> until a gate proves action types are loaded, using
///     <c>MBAnimation</c> only — otherwise this class would cause the very fault it repairs.
///   - Never writes an index it cannot prove belongs to the field (see the round-trip check below).
///   - Never throws.
/// </summary>
public static class ActionIndexCacheRepair
{
    private static readonly object _gate = new();
    private static bool _completed;

    /// <summary>
    /// Bounds retries. The primary call site is a prefix on <c>CharacterTableau.RefreshCharacterTableau</c>,
    /// so an unrecoverable failure that returned "retry me" would re-run the whole 215-field
    /// reflection + native scan on every tableau refresh for the rest of the session.
    /// </summary>
    private static int _attempts;
    private const int MaxAttempts = 3;

    /// <summary>Sentinel field that is legitimately -1 and must never be "repaired".</summary>
    private const string NoneFieldName = "act_none";

    /// <summary>
    /// Probe action used only to confirm action types are loaded. MUST be a non-empty literal:
    /// <c>MBAnimation.GetActionCodeWithName</c> reads <c>ActionIndexCache.act_none.Index</c> on a
    /// null/empty name, which would initialise — and therefore potentially poison — the very type
    /// this gate exists to protect.
    /// </summary>
    private const string GateProbeAction = "act_inventory_idle_start";

    /// <summary>
    /// Fields whose name does NOT equal the action name the engine's own cctor assigns them.
    /// Verified against v1.4.7 by diffing all 214 <c>Create(...)</c> call sites in the cctor against
    /// their target field names — <c>act_raid_jump = Create("act_raid_jump_1")</c> is the only
    /// divergence, and there is no action literally named <c>act_raid_jump</c> in
    /// <c>Native/ModuleData/action_types.xml</c>. Without this map the field would be reported as
    /// "unknown to this engine build" and silently left poisoned.
    /// </summary>
    private static readonly Dictionary<string, string> KnownNameOverrides = new(StringComparer.Ordinal)
    {
        ["act_raid_jump"] = "act_raid_jump_1",
    };

    /// <summary>The action name to look up for a given field. Pure; testable without an engine.</summary>
    public static string ResolveActionName(string fieldName)
    {
        if (string.IsNullOrEmpty(fieldName)) return fieldName;
        return KnownNameOverrides.TryGetValue(fieldName, out var mapped) ? mapped : fieldName;
    }

    /// <summary>
    /// The single decision point for whether a field may be written. Pure, so the policy is pinned
    /// by tests rather than by the loop that happens to call it. Repair only a field that is
    /// currently unresolved AND for which a live lookup succeeded; never touch the
    /// <c>act_none</c> sentinel.
    /// </summary>
    public static bool ShouldRepair(string fieldName, int currentIndex, int resolvedIndex)
    {
        if (string.IsNullOrWhiteSpace(fieldName)) return false;
        if (string.Equals(fieldName, NoneFieldName, StringComparison.Ordinal)) return false;
        if (currentIndex >= 0) return false;   // already healthy — never overwrite
        return resolvedIndex >= 0;             // only write a value we actually resolved
    }

    /// <summary>
    /// Attempts the repair. Returns true when the work completed (or was unnecessary), false when it
    /// was deferred or failed — in which case a later call from a later lifecycle point retries.
    /// Safe to call repeatedly and from any preview path; the work happens at most once.
    /// </summary>
    public static bool TryEnsureRepaired(string phase)
    {
        try
        {
            lock (_gate)
            {
                if (_completed) return true;
                if (_attempts >= MaxAttempts) return false;
            }

            // GATE — must not touch ActionIndexCache. MBAnimation is a separate struct with no
            // cctor, and with a non-empty name GetActionCodeWithName goes straight to native interop
            // without reading ActionIndexCache.act_none. If action types are not loaded we return
            // having initialised nothing.
            int actionCodeCount;
            int probeIndex;
            try
            {
                actionCodeCount = MBAnimation.GetNumActionCodes();
                probeIndex = MBAnimation.GetActionCodeWithName(GateProbeAction);
            }
            catch (Exception e)
            {
                TableauDiagnostics.LogDeduped($"aic.gate.threw.{phase}",
                    $"ActionIndexCacheRepair ({phase}): gate threw, deferring: {e.GetType().Name}");
                return false;
            }

            if (actionCodeCount <= 0 || probeIndex < 0)
            {
                // Deduped, NOT LogAlways: this runs on every preview path, so on a machine where the
                // gate keeps failing an unthrottled line here would grow without bound — the exact
                // regression commit ae2ed426 fixed.
                TableauDiagnostics.LogDeduped($"aic.deferred.{phase}",
                    $"ActionIndexCacheRepair ({phase}): DEFERRED — action types not loaded yet " +
                    $"(numActionCodes={actionCodeCount}, probe={probeIndex}). ActionIndexCache left untouched.");
                return false;
            }

            // Whole pass under the lock: the check-then-act above otherwise let two threads into
            // RepairFields concurrently, both mutating the same vanilla statics. Cheap because the
            // body runs at most MaxAttempts times per session.
            lock (_gate)
            {
                if (_completed) return true;
                if (_attempts >= MaxAttempts) return false;
                _attempts++;

                bool ok = RepairFields(phase, IsRoundTripUsable());
                if (ok) _completed = true;
                return ok;
            }
        }
        catch (Exception e)
        {
            TableauDiagnostics.LogError($"ActionIndexCacheRepair ({phase}) failed: {e}");
            return false;
        }
    }

    /// <summary>
    /// Self-test for the round-trip write guard, run against an action we already know resolves.
    ///
    /// The guard rejects any write whose looked-up name the engine does not echo back via
    /// <c>GetName()</c>. That protects against writing a wrong animation index — but it is only
    /// meaningful if native names round-trip EXACTLY. If they do not (different case, a canonical
    /// form, or the first of several aliases sharing an index), the guard would reject every field,
    /// the repair would write nothing, and the failure would look like "all names mismatched"
    /// instead of "the guard is broken". Probing it once with a known-good action turns that silent
    /// total failure into a decision we can log.
    /// </summary>
    private static bool IsRoundTripUsable()
    {
        try
        {
            var probe = ActionIndexCache.Create(GateProbeAction);
            if (probe.Index < 0) return false;
            bool usable = string.Equals(probe.GetName(), GateProbeAction, StringComparison.Ordinal);
            if (!usable)
            {
                TableauDiagnostics.LogError(
                    $"ActionIndexCacheRepair: engine does not round-trip action names " +
                    $"('{GateProbeAction}' came back as '{probe.GetName()}'), so the write guard is " +
                    "unusable on this build. Falling back to the field→action name map alone.");
            }
            return usable;
        }
        catch { return false; }
    }

    /// <summary>
    /// Returns true only when the pass genuinely completed, so a failure can be retried from a later
    /// phase rather than being latched as "done".
    /// </summary>
    private static bool RepairFields(string phase, bool roundTripUsable)
    {
        FieldInfo[] fields;
        try
        {
            // Type.GetFields reads metadata only — it does NOT run the static constructor.
            fields = typeof(ActionIndexCache).GetFields(BindingFlags.Public | BindingFlags.Static);
        }
        catch (Exception e)
        {
            TableauDiagnostics.LogError($"ActionIndexCacheRepair ({phase}): field enumeration threw, will retry later: {e}");
            return false;   // recoverable — do NOT latch _completed
        }

        int total = 0, healthy = 0, repaired = 0, unresolved = 0, failed = 0, skipped = 0, mismatched = 0;
        string? firstFailure = null;
        string? firstMismatch = null;

        foreach (var field in fields)
        {
            if (field.FieldType != typeof(ActionIndexCache)) continue;
            total++;

            try
            {
                // The first static-field read here is what initialises the type — deliberately, and
                // only after the gate proved action types are loaded.
                int currentIndex = ((ActionIndexCache)field.GetValue(null)).Index;

                string actionName = ResolveActionName(field.Name);
                int resolvedIndex = MBAnimation.GetActionCodeWithName(actionName);

                if (!ShouldRepair(field.Name, currentIndex, resolvedIndex))
                {
                    if (string.Equals(field.Name, NoneFieldName, StringComparison.Ordinal)) skipped++;
                    else if (currentIndex >= 0) healthy++;
                    else unresolved++;
                    continue;
                }

                // ROUND-TRIP CHECK. Guards the one way this repair could do real harm: writing a
                // valid-but-WRONG index into a vanilla static. If the engine does not agree that the
                // resolved index maps back to the name we looked up, we do not know what this field
                // is supposed to hold, so we leave it alone. -1 is recoverable; a wrong animation
                // index is a silent, non-crashing corruption.
                var candidate = ActionIndexCache.Create(actionName);
                if (candidate.Index < 0) { unresolved++; continue; }

                if (roundTripUsable)
                {
                    string roundTrip;
                    try { roundTrip = candidate.GetName(); } catch { roundTrip = string.Empty; }
                    if (!string.Equals(roundTrip, actionName, StringComparison.Ordinal))
                    {
                        mismatched++;
                        firstMismatch ??= $"{field.Name} (looked up '{actionName}', engine returned '{roundTrip}')";
                        continue;
                    }
                }

                field.SetValue(null, candidate);

                // Confirm the write landed — some runtimes refuse initonly static writes silently.
                if (((ActionIndexCache)field.GetValue(null)).Index >= 0) repaired++;
                else { failed++; firstFailure ??= $"{field.Name} (write accepted but value unchanged)"; }
            }
            catch (Exception e)
            {
                failed++;
                firstFailure ??= $"{field.Name} ({e.GetType().Name}: {e.Message})";
            }
        }

        // Report the ACTUAL enumerated count rather than a hard-coded figure — the number is the
        // reader's sanity check that the repair walked the set it thinks it walked.
        if (repaired == 0 && failed == 0 && mismatched == 0)
        {
            TableauDiagnostics.LogAlways(
                $"ActionIndexCacheRepair ({phase}): statics healthy — {healthy}/{total} resolved, " +
                $"{unresolved} unknown to this engine build, {skipped} sentinel. No action taken.");
        }

        if (repaired > 0)
        {
            TableauDiagnostics.LogError(
                $"ActionIndexCacheRepair ({phase}): REPAIRED {repaired} of {total} poisoned action index(es) " +
                $"({healthy} already healthy, {unresolved} unknown to this engine build, {mismatched} name-mismatched, " +
                $"{failed} failed). ActionIndexCache's static constructor had run before action types were loaded, " +
                "which renders every UI tableau character in bind pose. Repaired from live lookups.");
        }

        if (mismatched > 0)
        {
            TableauDiagnostics.LogError(
                $"ActionIndexCacheRepair ({phase}): {mismatched} field(s) skipped because the engine did not " +
                $"round-trip the looked-up name (first: {firstMismatch}). These stay unresolved rather than risk " +
                "writing a wrong animation index. If this count is non-zero after an engine bump, the field→action " +
                "name map in KnownNameOverrides needs updating.");
        }

        if (failed > 0)
        {
            TableauDiagnostics.LogError(
                $"ActionIndexCacheRepair ({phase}): {failed} field(s) could NOT be written " +
                $"(first: {firstFailure}). The runtime may be refusing reflection writes to initonly " +
                "statics; the tableau fix will be incomplete this session.");
            return false;   // allow a later phase to retry
        }

        // A pass that repaired nothing BECAUSE the write guard rejected everything is not success.
        // Latching here would leave the fields poisoned for the session while reporting "done" —
        // the guard silently disabling the fix it exists to protect. Retry is bounded by
        // MaxAttempts, so this cannot become a per-refresh rescan.
        if (repaired == 0 && mismatched > 0)
        {
            TableauDiagnostics.LogError(
                $"ActionIndexCacheRepair ({phase}): repaired NOTHING — all {mismatched} candidate field(s) " +
                "were rejected by the round-trip write guard. Not latching, so a later phase can retry.");
            return false;
        }

        return true;
    }
}
