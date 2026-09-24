using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;

namespace TAOM.Dependencies.Foundation;

/// <summary>
/// Wraps every Harmony-patched method in the AppDomain with a Finalizer that catches
/// the trinity of "mod compiled against an old Bannerlord version" exceptions:
/// <c>MissingMethodException</c>, <c>MissingFieldException</c>, <c>TypeLoadException</c>.
/// On catch, logs the failure, increments per-category counters, and removes the
/// offending owner's prefixes/postfixes/transpilers from this method via
/// <see cref="Harmony.Unpatch(MethodBase, HarmonyPatchType, string)"/>. The patched
/// method continues running uncaught from the user's perspective — the game keeps going.
///
/// BetaDeps parity (DR3 Phase 4 — 2026-05-25, re-implementation of BetaDeps.Foundation.PatchShield).
/// This is the single highest-leverage component in BetaDeps's "every BUTR-dependent mod
/// works even when broken" promise. Provenance and licence status:
/// docs/reference/provenance-register.md (BetaDeps row).
///
/// Opt-out: place a file named <c>patchshield-disabled.flag</c> in the
/// TAOM.Dependencies module directory to skip install. Useful for diagnosing whether
/// a crash is masked by PatchShield vs an actual problem in TAOM.
///
/// Install timing: should run AFTER all other mods have applied their Harmony patches
/// — i.e., late in the load lifecycle, NOT in SubModule ctors. See SubModule.cs
/// OnSubModuleLoad or OnBeforeInitialModuleScreenSetAsRoot.
/// </summary>
public static class PatchShield
{
    private const string Tag = "PatchShield";
    private const string HarmonyId = "TAOM.Dependencies.Foundation.PatchShield";
    private const string DisableFlagName = "patchshield-disabled.flag";

    private static readonly ShieldCoverage _coverage = new();
    private static readonly HashSet<string> _unpatched = new();
    private static readonly HashSet<string> _withheld = new();
    private static readonly object _lock = new();

    // The protected-owner allowlist and the two decisions that use it live in PatchShieldPolicy so
    // they can be unit-tested without Harmony or a running game (this class is static and
    // Harmony-bound). The effective list is the compiled defaults UNIONED with any co-op owner
    // prefixes from coop-modules.txt — union only, so a bad config edit can never unprotect the
    // BUTR/MCM stack. Built once per unpatch attempt in TryUnpatchOffendingPatches.

    // The hot-layer target exclusion list lives in PatchShieldPolicy.ExcludedTargetNamespacePrefixes (#331).

    private static bool IsExcludedTarget(MethodBase method)
    {
        try { return PatchShieldPolicy.IsExcludedTargetNamespace(method.DeclaringType?.Namespace); }
        catch { return false; /* fail open: an unreadable type just gets shielded as before */ }
    }

    private static readonly Dictionary<string, int> _ownerCounts =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly object _ownerLock = new();

    private static long _swallowedMissingMethod;
    private static long _swallowedMissingField;
    private static long _swallowedTypeLoad;
    private static long _swallowedOther;

    // Exceptions handed back to Harmony with their stack preserved. Kept apart from the swallow
    // counters (Codex A2: a rethrow is not a swallow). The preserver's cost lands only here, and how
    // often exceptions cross a shield in normal play is decided by the modlist, so the session
    // summary prints it.
    private static long _rethrown;

    /// <summary>Methods carrying PatchShield's finalizer.</summary>
    public static int AttachedCount { get { lock (_lock) return _coverage.AttachedCount; } }

    /// <summary>Patched methods the passes examined and decided on, skipped ones included.</summary>
    public static int SeenCount { get { lock (_lock) return _coverage.SeenCount; } }
    public static int UnpatchedCount { get { lock (_lock) return _unpatched.Count; } }

    /// <summary>Targets where the rescue unpatch was suppressed because a co-op module is active.</summary>
    public static int WithheldCount { get { lock (_lock) return _withheld.Count; } }
    public static long SwallowedMissingMethod => Interlocked.Read(ref _swallowedMissingMethod);
    public static long SwallowedMissingField => Interlocked.Read(ref _swallowedMissingField);
    public static long SwallowedTypeLoad => Interlocked.Read(ref _swallowedTypeLoad);
    public static long SwallowedOther => Interlocked.Read(ref _swallowedOther);
    public static long SwallowedTotal => SwallowedMissingMethod + SwallowedMissingField + SwallowedTypeLoad + SwallowedOther;
    public static long RethrownCount => Interlocked.Read(ref _rethrown);

    public static bool IsDisabled()
    {
        try
        {
            var dir = RuntimeLog.ModuleDir;
            if (string.IsNullOrEmpty(dir)) return false;
            return File.Exists(Path.Combine(dir, DisableFlagName));
        }
        catch { return false; }
    }

    /// <summary>
    /// Installs the shield: iterates all currently-patched methods, attaches a
    /// Finalizer to each. Idempotent — methods already shielded are skipped.
    /// Safe to call multiple times to "shield-pass" new patches added by mods
    /// that load after our first install (call from a late lifecycle hook).
    /// </summary>
    public static void Install()
    {
        var coopActive = CoopPresence.IsActive;
        if (!PatchShieldPolicy.ShouldInstall(coopActive, IsDisabled()))
        {
            DiagLog.Log(Tag, coopActive
                // Player-reported 2026-08-02: this is a frame-rate fix, not a safety change. Coop's
                // AutoSync transpiles every declared method of 43 campaign types, and shielding that
                // surface makes every one of them pay the __originalMethod binding tax per call —
                // the same mechanism as the #331 tournament freeze. Rationale + what it gives up:
                // PatchShieldPolicy.ShouldInstall.
                ? $"co-op module(s) active ({string.Join(", ", CoopPresence.ActiveCoopModuleIds)}) — " +
                  "PatchShield install skipped (finalizer tax on Coop's AutoSync surface)"
                : "patchshield-disabled.flag present — PatchShield install skipped");
            return;
        }

        try
        {
            var harmony = new Harmony(HarmonyId);
            var voidFinalizer = typeof(PatchShield).GetMethod(
                nameof(ShieldFinalizerVoid),
                BindingFlags.Static | BindingFlags.NonPublic);
            var resultFinalizer = typeof(PatchShield).GetMethod(
                nameof(ShieldFinalizerWithResult),
                BindingFlags.Static | BindingFlags.NonPublic);
            if (voidFinalizer == null || resultFinalizer == null)
            {
                DiagLog.Log(Tag, "could not resolve shield finalizer methods; aborting install");
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            List<MethodBase> patched;
            try
            {
                patched = Harmony.GetAllPatchedMethods().ToList();
            }
            catch (Exception ex)
            {
                DiagLog.LogCaught(Tag, "GetAllPatchedMethods", ex);
                return;
            }

            int added = 0, skipped = 0, alreadySeen = 0, seenTotal, attachedTotal;
            lock (_lock)
            {
                foreach (var method in patched)
                {
                    if (method == null) { skipped++; continue; }
                    if (_coverage.HasSeen(method)) { alreadySeen++; continue; }

                    // Don't shield our own methods.
                    try
                    {
                        var declAsm = method.DeclaringType?.Assembly.GetName().Name ?? string.Empty;
                        if (declAsm.StartsWith("TAOM", StringComparison.OrdinalIgnoreCase))
                        {
                            _coverage.RecordSkipped(method);
                            skipped++;
                            continue;
                        }
                    }
                    catch { }

                    // Never shield the excluded hot layers: the Gauntlet/2D UI (#331 round 2: a
                    // per-call __originalMethod finalizer froze tournament exits for ~107s) and the
                    // engine's ManagedCallbacks boundary, whose callback shims Native2Managed crash
                    // capture already wraps (plan 007). See PatchShieldPolicy.ExcludedTargetNamespacePrefixes.
                    if (IsExcludedTarget(method))
                    {
                        _coverage.RecordSkipped(method);
                        skipped++;
                        continue;
                    }

                    // Never stack on SaveShield's own targets. Harmony runs every finalizer on a
                    // method against ONE shared exception slot and the last non-void return wins,
                    // so our unconditional trinity swallow would override SaveShield's co-op
                    // SAVE-LOAD rethrow — silently continuing a partially deserialised load, the
                    // precise failure that rethrow exists to prevent. SaveShield already handles a
                    // broader exception set on those methods, so we add nothing by shielding them.
                    if (SaveShield.IsShielding(method))
                    {
                        _coverage.RecordSkipped(method);
                        skipped++;
                        continue;
                    }

                    try
                    {
                        bool isVoid = true;
                        if (method is MethodInfo mi) isVoid = mi.ReturnType == typeof(void);
                        var finalizer = isVoid ? voidFinalizer : resultFinalizer;
                        harmony.Patch(method, prefix: null, postfix: null, transpiler: null,
                            finalizer: new HarmonyMethod(finalizer));
                        _coverage.RecordAttached(method);
                        added++;
                    }
                    catch (Exception ex)
                    {
                        skipped++;
                        DiagLog.LogCaught(Tag, $"shielding {method.DeclaringType?.FullName}.{method.Name}", ex);
                    }
                }

                seenTotal = _coverage.SeenCount;
                attachedTotal = _coverage.AttachedCount;
            }

            if (added > 0 || alreadySeen == 0)
            {
                DiagLog.Log(Tag, PatchShieldPolicy.FormatShieldPassSummary(added, alreadySeen, skipped, seenTotal, attachedTotal, stopwatch.ElapsedMilliseconds));
            }
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught(Tag, "Install", ex);
        }
    }

    /// <summary>
    /// Finalizer for void-return methods. Catches the swallow-trinity and returns
    /// silently to suppress the exception; non-matching exceptions are re-thrown by
    /// returning the ORIGINAL exception (Harmony Finalizer convention).
    ///
    /// Harmony calls this on EVERY call of the patched method, with a null exception when
    /// nothing threw, so the no-exception path must stay one null check.
    /// </summary>
    private static Exception? ShieldFinalizerVoid(MethodBase __originalMethod, Exception __exception)
    {
        if (__exception == null || ShouldSwallow(__originalMethod, __exception)) return null;

        // Harmony rethrows a returned exception with `throw` (this finalizer returns a value, so
        // the wrapper never uses `rethrow`), which would replace its stack trace with the frames
        // from this method outward (player bundle 2d446100: a childbirth failure reported as five
        // frames ending at MapState.OnTick_Patch2).
        Interlocked.Increment(ref _rethrown);
        return RethrowStackPreserver.PreserveForRethrow(__exception, __originalMethod);
    }

    /// <summary>
    /// Finalizer for return-value methods. Same swallow behavior; the patched method
    /// returns its zero/default value when we swallow because we don't have access
    /// to <c>__result</c> in a Finalizer (Harmony quirk). Acceptable trade-off:
    /// the caller gets a "stub" return value, which is far better than a crash.
    /// </summary>
    private static Exception? ShieldFinalizerWithResult(MethodBase __originalMethod, Exception __exception)
    {
        if (__exception == null || ShouldSwallow(__originalMethod, __exception)) return null;

        // Same rethrow as ShieldFinalizerVoid; see there.
        Interlocked.Increment(ref _rethrown);
        return RethrowStackPreserver.PreserveForRethrow(__exception, __originalMethod);
    }

    private static bool ShouldSwallow(MethodBase originalMethod, Exception exception)
    {
        if (exception == null) return false;

        // Unwrap TargetInvocationException ONLY to classify the real reason. The
        // rethrow path must return the ORIGINAL exception: Harmony's generated
        // `throw finalizerResult;` resets that object's stack trace to the patched
        // frame, so returning the unwrapped inner exception here destroyed the real
        // throw-site frames of every reflection-invoked handler crash (the #354
        // education CTD bundle showed a bare NRE anchored at ExecuteCommand_Patch3
        // with no inner exception). Returning the original TIE keeps the inner
        // exception — and its intact stack — for the crash reporter, and restores
        // vanilla propagation semantics. The same reset hits a plain exception's OWN
        // frames, which is what the finalizers' RethrowStackPreserver call repairs.
        var ex = exception;
        while (ex is TargetInvocationException && ex.InnerException != null)
            ex = ex.InnerException;

        if (ex is MissingMethodException || ex is MissingFieldException || ex is TypeLoadException)
        {
            if (ex is MissingMethodException) Interlocked.Increment(ref _swallowedMissingMethod);
            else if (ex is MissingFieldException) Interlocked.Increment(ref _swallowedMissingField);
            else Interlocked.Increment(ref _swallowedTypeLoad);

            try
            {
                var owner = originalMethod?.DeclaringType?.FullName ?? "?";
                var name = originalMethod?.Name ?? "?";
                DiagLog.Log(Tag, $"swallowed {ex.GetType().Name} from a patch on {owner}.{name}: {ex.Message}");
            }
            catch { }

            TryUnpatchOffendingPatches(originalMethod, ex);
            return true;
        }

        // Codex A2 LOW fix 2026-05-27: do NOT increment _swallowedOther here — this
        // path RETHROWS the exception. The counter previously misled WriteSessionSummary
        // into reporting rethrown exceptions as swallowed.
        return false;
    }

    private static void TryUnpatchOffendingPatches(MethodBase originalMethod, Exception ex)
    {
        if (originalMethod == null) return;

        // Codex A3 LOW fix 2026-05-27: overload-safe dedupe key. Was
        // <DeclaringType>::<methodName> — overloaded methods shared a key, so the
        // second overload's failure would skip cleanup. Now uses
        // <Module.ModuleVersionId>:<MetadataToken> which is unique per method handle.
        string targetKey;
        try
        {
            targetKey = $"{originalMethod.Module.ModuleVersionId}:{originalMethod.MetadataToken}";
        }
        catch
        {
            // Fallback if Module/MetadataToken unavailable for this method handle.
            try { targetKey = originalMethod.ToString(); }
            catch { return; }
        }

        // Two separate sets. `_unpatched` means "we stripped owners here" and backs UnpatchedCount
        // + the session summary; `_withheld` means "we would have, but co-op is active". Recording
        // a withheld target as unpatched made the one summary line a triager reads ("unpatched N
        // target(s)") contradict the "(withheld)" lines above it — bad in a changeset whose whole
        // purpose is making divergence legible. Both still dedupe once per target.
        var coopActive = CoopPresence.IsActive;
        var mayUnpatch = PatchShieldPolicy.ShouldUnpatchForeignOwners(coopActive);
        lock (_lock)
        {
            if (_unpatched.Contains(targetKey) || _withheld.Contains(targetKey)) return;  // already handled
            if (mayUnpatch) _unpatched.Add(targetKey);
            else _withheld.Add(targetKey);
        }

        try
        {
            var patches = Harmony.GetPatchInfo(originalMethod);
            if (patches == null) return;

            var owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in patches.Prefixes) if (p != null) owners.Add(p.owner ?? string.Empty);
            foreach (var p in patches.Postfixes) if (p != null) owners.Add(p.owner ?? string.Empty);
            foreach (var p in patches.Transpilers) if (p != null) owners.Add(p.owner ?? string.Empty);
            foreach (var p in patches.Finalizers) if (p != null) owners.Add(p.owner ?? string.Empty);

            // Co-op interop (2026-07-31): when a co-op module is active, observe and log but never
            // strip. Under a host-authoritative co-op mod, removing one peer's sync patch does not
            // crash — it silently desynchronises two campaigns, which corrupts both saves and
            // cannot be diagnosed from a log. The swallow half of the shield still runs, so the
            // session survives exactly as before; only the irreversible mutation is withheld.
            // (`mayUnpatch` was decided above, alongside the dedupe bookkeeping.)
            var protectedPrefixes = PatchShieldPolicy.BuildEffectiveOwnerPrefixes(
                CoopPresence.ExtraProtectedOwnerPrefixes);

            var harmony = new Harmony(HarmonyId);
            foreach (var owner in owners)
            {
                if (string.IsNullOrEmpty(owner) || owner == HarmonyId) continue;

                // Refuse to unpatch protected infrastructure owners (Codex S1 HIGH fix
                // 2026-05-27). Filter now covers TAOM + vendored BUTR/MCM/Harmony.
                if (PatchShieldPolicy.IsProtectedOwner(owner, protectedPrefixes))
                {
                    DiagLog.Log(Tag, $"refusing to unpatch protected owner '{owner}' on {targetKey}");
                    continue;
                }

                if (!mayUnpatch)
                {
                    DiagLog.Log(Tag, $"co-op active — would unpatch owner '{owner}' on {targetKey} (withheld)");
                    continue;
                }

                try
                {
                    harmony.Unpatch(originalMethod, HarmonyPatchType.Prefix, owner);
                    harmony.Unpatch(originalMethod, HarmonyPatchType.Postfix, owner);
                    harmony.Unpatch(originalMethod, HarmonyPatchType.Transpiler, owner);
                    DiagLog.Log(Tag, $"unpatched owner '{owner}' on {targetKey}");

                    lock (_ownerLock)
                    {
                        _ownerCounts.TryGetValue(owner, out var count);
                        _ownerCounts[owner] = count + 1;
                    }
                }
                catch (Exception unpatchEx)
                {
                    DiagLog.LogCaught(Tag, $"Unpatch owner='{owner}' on {targetKey}", unpatchEx);
                }
            }
        }
        catch (Exception ex2)
        {
            DiagLog.LogCaught(Tag, $"TryUnpatchOffendingPatches({targetKey})", ex2);
        }
    }

    /// <summary>
    /// Writes a one-line summary of swallow stats. Wire to AppDomain.ProcessExit.
    /// </summary>
    public static void WriteSessionSummary()
    {
        try
        {
            string topOwner = "(none)";
            lock (_ownerLock)
            {
                if (_ownerCounts.Count > 0)
                {
                    var top = _ownerCounts.OrderByDescending(k => k.Value).First();
                    topOwner = $"{top.Key} ({top.Value})";
                }
            }
            var withheld = WithheldCount;
            DiagLog.Log(Tag,
                $"SESSION SUMMARY: shielded {AttachedCount} of {SeenCount} patched method(s) seen, unpatched {UnpatchedCount} target(s)" +
                (withheld > 0 ? $", withheld {withheld} target(s) (co-op active)" : string.Empty) + ", " +
                $"swallowed {SwallowedTotal} exception(s) " +
                $"(MissingMethod {SwallowedMissingMethod}, MissingField {SwallowedMissingField}, " +
                $"TypeLoad {SwallowedTypeLoad}, other {SwallowedOther}), " +
                $"rethrew {RethrownCount} with the stack preserved. " +
                $"Top unpatched owner: {topOwner}.");
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught(Tag, "WriteSessionSummary", ex);
        }
    }
}
