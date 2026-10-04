using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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

    // The hot-layer target exclusion lists live in PatchShieldPolicy: by namespace
    // (ExcludedTargetNamespacePrefixes, #331) and by declaring-type+method (ExcludedTargetMethods,
    // Patch92's five per-unit Formation members, whose declaring type is not hot enough to exclude by namespace).

    private static bool IsExcludedTarget(MethodBase method)
    {
        try
        {
            return PatchShieldPolicy.IsExcludedTargetNamespace(method.DeclaringType?.Namespace)
                || PatchShieldPolicy.IsExcludedTargetMethod(method.DeclaringType?.FullName, method.Name);
        }
        catch { return false; /* fail open: an unreadable type just gets shielded as before */ }
    }

    // One diag.log reason line per patched method skipped by name (ExcludedTargetMethods), once per
    // process: the caller has just recorded the method as seen. The namespace exclusions stay counted
    // only; they cover whole layers, and their rationale is in PatchShieldPolicy.
    private static void LogHotMethodSkip(MethodBase method)
    {
        try
        {
            var type = method.DeclaringType?.FullName;
            if (!PatchShieldPolicy.IsExcludedTargetMethod(type, method.Name)) return;
            DiagLog.Log(Tag, PatchShieldPolicy.FormatHotMethodSkip(type, method.Name, Harmony.GetPatchInfo(method)?.Owners));
        }
        catch { /* diagnostic only */ }
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

    // Exceptions whose shielded method ResolveShieldedOriginal could not name (plan 034). The first
    // miss is logged in full, once; the session summary prints the count when it is not zero.
    private static long _unresolvedOriginal;
    private static int _unresolvedOriginalLogged;

    // Swallow lines already written this process, with how often each repeated (maintainer decision D16). A throw
    // the shield swallows but cannot cure comes back on every call, and its line used to be written every time.
    // Examples, not a complete list: a lookup miss, an owner on the protected list, a strip withheld under co-op,
    // a throw from the shielded method's own body or from a method it calls (no strip reaches that), and a target
    // already rescued once (each target is, once per process). The first occurrence of a line goes out in full, and
    // a write that failed is tried again by the next occurrence. Repeats are counted: the running count is written
    // by WriteRepeatCheckpoint when a mission starts (a count whose write failed goes out again at the next one), and
    // the total by WriteSessionSummary at a clean shutdown. The table holds 256 distinct lines (a line includes the
    // exception message); a line past that is written in full every time, never dropped.
    private static readonly LogRepeatLimiter _swallowLines = new(capacity: 256);

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

    /// <summary>Times <see cref="ResolveShieldedOriginal"/> could not name the shielded method.</summary>
    public static long UnresolvedOriginalCount => Interlocked.Read(ref _unresolvedOriginal);

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
                // surface made every one of them pay for the __originalMethod parameter the finalizer
                // took until plan 034, on every call, the same mechanism as the #331 tournament freeze.
                // The finalizer now takes only __exception; the skip stands as decided. Rationale +
                // what it gives up: PatchShieldPolicy.ShouldInstall.
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
                    // per-call __originalMethod finalizer froze tournament exits for ~107s), the
                    // engine's ManagedCallbacks boundary, whose callback shims Native2Managed crash
                    // capture already wraps (plan 007), and Patch92's per-unit Formation members. See
                    // PatchShieldPolicy.ExcludedTargetNamespacePrefixes and ExcludedTargetMethods; the
                    // skip applies to every owner, so another mod's patch on those targets is unshielded too.
                    if (IsExcludedTarget(method))
                    {
                        _coverage.RecordSkipped(method);
                        skipped++;
                        LogHotMethodSkip(method);
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
                DiagLog.Log(Tag, PatchShieldPolicy.FormatShieldPassSummary(
                    added: added, alreadySeen: alreadySeen, skipped: skipped,
                    seenTotal: seenTotal, attachedTotal: attachedTotal, elapsedMs: stopwatch.ElapsedMilliseconds));
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
    /// nothing threw, so the no-exception path must stay one null check. It takes no
    /// <c>__originalMethod</c>: that parameter makes Harmony's wrapper call
    /// <c>MethodBase.GetMethodFromHandle</c> on every call (plan 034 measured about 63 ns), and the
    /// shielded method is needed only after a throw, where <see cref="ResolveShieldedOriginal"/> finds it.
    /// </summary>
    private static Exception? ShieldFinalizerVoid(Exception __exception)
    {
        if (__exception == null) return null;
        var originalMethod = ResolveShieldedOriginal(typeof(PatchShield));
        if (ShouldSwallow(originalMethod, __exception)) return null;

        // Harmony rethrows a returned exception with `throw` (this finalizer returns a value, so
        // the wrapper never uses `rethrow`), which would replace its stack trace with the frames
        // from this method outward (player bundle 2d446100: a childbirth failure reported as five
        // frames ending at MapState.OnTick_Patch2).
        Interlocked.Increment(ref _rethrown);
        return RethrowStackPreserver.PreserveForRethrow(__exception, originalMethod);
    }

    /// <summary>
    /// Finalizer for return-value methods. Same swallow behavior; the patched method
    /// returns its zero/default value when we swallow because we don't have access
    /// to <c>__result</c> in a Finalizer (Harmony quirk). Acceptable trade-off:
    /// the caller gets a "stub" return value, which is far better than a crash.
    /// It takes no <c>__originalMethod</c>, for the reason given on <see cref="ShieldFinalizerVoid"/>.
    /// </summary>
    private static Exception? ShieldFinalizerWithResult(Exception __exception)
    {
        if (__exception == null) return null;
        var originalMethod = ResolveShieldedOriginal(typeof(PatchShield));
        if (ShouldSwallow(originalMethod, __exception)) return null;

        // Same rethrow as ShieldFinalizerVoid; see there.
        Interlocked.Increment(ref _rethrown);
        return RethrowStackPreserver.PreserveForRethrow(__exception, originalMethod);
    }

    /// <summary>
    /// The shielded method whose Harmony replacement called the finalizer that calls this, or null.
    /// Call it directly from the finalizer and pass the finalizer's declaring type. It skips the frames
    /// of that type (the finalizer itself, unless the JIT inlined it into the replacement), and the frame
    /// of a replacement Harmony maps back to that type (another mod's patch on the finalizer runs its body
    /// there), then judges exactly one frame, the next one: the replacement running the finalizer. It
    /// returns that frame's original when Harmony maps it back
    /// (<see cref="Harmony.GetOriginalMethodFromStackframe"/>), and otherwise null. It never climbs further,
    /// because an outer shielded method on the same stack is the wrong method, and the swallow path would
    /// unpatch it. Exception path only: a stack walk costs far more than a call, and the finalizers reach
    /// this only after a throw. The callers already handle null (the log names "?.?", nothing is
    /// unpatched, and the rethrow marker says "an unknown method"). A null result is counted, and the
    /// first one is logged with its reason. Never throws.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static MethodBase? ResolveShieldedOriginal(Type finalizerType)
    {
        string reason;
        try
        {
            reason = "no frame above the finalizer";
            var frames = new StackTrace(1, false).GetFrames();
            if (frames == null) reason = "the stack trace had no frames";
            else
            {
                foreach (var frame in frames)
                {
                    if (frame == null) { reason = "a frame reported no method"; break; }
                    var method = frame.GetMethod();
                    if (method?.DeclaringType == finalizerType) continue;   // the calling finalizer's own frame
                    // A frame with no method still goes to Harmony, which maps such frames by address on Mono.
                    var original = Harmony.GetOriginalMethodFromStackframe(frame);
                    // Still the finalizer's own frame: another mod's patch on the finalizer runs its body inside
                    // a replacement that Harmony maps back to the finalizer. The next frame is the one judged.
                    if (original?.DeclaringType == finalizerType) continue;
                    if (original != null && !ReferenceEquals(original, method)) return original;
                    // The judged frame is not a replacement Harmony maps: stop here, never climb.
                    reason = method == null
                        ? "a frame reported no method"
                        : $"the calling frame {Describe(method)} is not a replacement this Harmony copy maps";
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            // Fail open: a null original degrades the log line and the unpatch, never the swallow.
            reason = $"the stack walk threw {ex.GetType().Name}: {ex.Message}";
        }

        Interlocked.Increment(ref _unresolvedOriginal);
        // Once per session, but a write that did not land does not use the once up: the next miss tries again.
        if (Interlocked.Exchange(ref _unresolvedOriginalLogged, 1) == 0 && !DiagLog.TryLog(Tag, FormatUnresolvedOriginal(reason)))
            Volatile.Write(ref _unresolvedOriginalLogged, 0);
        return null;
    }

    /// <summary>The diag.log line for the first time <see cref="ResolveShieldedOriginal"/> finds nothing.</summary>
    internal static string FormatUnresolvedOriginal(string reason) =>
        $"could not tell which shielded method an exception crossed ({reason}): the shield still swallows or " +
        "rethrows it as before, but its log line names '?.?', nothing is unpatched for it and its rethrow marker " +
        "says 'an unknown method'. Logged once per session; the session summary counts every miss.";

    /// <summary>
    /// The session summary's line for a swallow line that repeated: the line itself, then how many occurrences were
    /// counted instead of written (maintainer decision D16). That is the repeats after the one written in full, plus
    /// any whose write failed; when no write of the line landed it is every occurrence, and this line is then the only
    /// copy of its text in the log.
    /// </summary>
    internal static string FormatRepeatedSwallow(string line, long repeats) =>
        $"{line} (and {repeats} more this session, counted instead of logged)";

    /// <summary>
    /// The line <see cref="WriteRepeatCheckpoint"/> writes for a swallow line that repeated: like
    /// <see cref="FormatRepeatedSwallow"/>, but the count is a running total, because the session is not over.
    /// </summary>
    internal static string FormatRepeatedSwallowSoFar(string line, long repeats) =>
        $"{line} (and {repeats} more so far this session, counted instead of logged)";

    private static string Describe(MethodBase method) =>
        method.DeclaringType == null ? method.Name : method.DeclaringType.FullName + "." + method.Name;

    private static bool ShouldSwallow(MethodBase? originalMethod, Exception exception)
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
                WriteSwallowLine($"swallowed {ex.GetType().Name} from a patch on {owner}.{name}: {ex.Message}");
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

    /// <summary>
    /// Writes a swallow line the first time it appears, and again on a later occurrence if that write did not land
    /// (<see cref="DiagLog.TryLog"/> says; DiagLog swallows its own failures), so a transient I/O fault cannot keep
    /// the line out of the log for the whole session. Every other occurrence is counted by <see cref="_swallowLines"/>.
    /// </summary>
    private static void WriteSwallowLine(string line)
    {
        if (!_swallowLines.ShouldWrite(line)) return;
        var written = false;
        try { written = DiagLog.TryLog(Tag, line); }
        finally { _swallowLines.WriteFinished(line, written); }
    }

    private static void TryUnpatchOffendingPatches(MethodBase? originalMethod, Exception ex)
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
    /// Writes the running count of each swallow line that repeated since the last report
    /// (<see cref="FormatRepeatedSwallowSoFar"/>), and nothing for a line whose count did not change. The summary
    /// below runs only at a clean managed shutdown, so a session that crashes would otherwise keep the first line of
    /// each swallow and lose its count (maintainer decision D6: aggregate, never drop). A count whose write did not
    /// land (<see cref="DiagLog.TryLog"/> says) is not a report: the line goes back to the limiter, so the next
    /// checkpoint writes its running total again. The caller picks the boundary:
    /// <c>SubModule.OnBeforeMissionBehaviorInitialize</c> calls it at the start of every mission. Never throws.
    /// </summary>
    public static void WriteRepeatCheckpoint()
    {
        try
        {
            foreach (var repeated in _swallowLines.TakeUnreported())
            {
                if (!DiagLog.TryLog(Tag, FormatRepeatedSwallowSoFar(repeated.Key, repeated.Value)))
                    _swallowLines.Unreport(repeated.Key);
            }
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught(Tag, "WriteRepeatCheckpoint", ex);
        }
    }

    /// <summary>
    /// Writes a one-line summary of swallow stats, then one line for each swallow line that repeated, with its total
    /// (<see cref="FormatRepeatedSwallow"/>), whether or not <see cref="WriteRepeatCheckpoint"/> reported it earlier.
    /// Wire to AppDomain.ProcessExit.
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
            var unresolved = UnresolvedOriginalCount;
            DiagLog.Log(Tag,
                $"SESSION SUMMARY: shielded {AttachedCount} of {SeenCount} patched method(s) seen, unpatched {UnpatchedCount} target(s)" +
                (withheld > 0 ? $", withheld {withheld} target(s) (co-op active)" : string.Empty) + ", " +
                $"swallowed {SwallowedTotal} exception(s) " +
                $"(MissingMethod {SwallowedMissingMethod}, MissingField {SwallowedMissingField}, " +
                $"TypeLoad {SwallowedTypeLoad}, other {SwallowedOther}), " +
                $"rethrew {RethrownCount} with the stack preserved" +
                (unresolved > 0 ? $"; the shielded method was unknown {unresolved} time(s)" : string.Empty) + ". " +
                $"Top unpatched owner: {topOwner}.");

            // The total of each count. WriteSwallowLine wrote each of these lines in full once, except a line whose
            // every write failed: this line is then its only copy.
            foreach (var repeated in _swallowLines.Repeated())
                DiagLog.Log(Tag, FormatRepeatedSwallow(repeated.Key, repeated.Value));
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught(Tag, "WriteSessionSummary", ex);
        }
    }
}
