using System;
using System.Collections.Generic;
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
/// BetaDeps parity (DR3 Phase 4 — 2026-05-25, port of BetaDeps.Foundation.PatchShield).
/// This is the single highest-leverage component in BetaDeps's "every BUTR-dependent mod
/// works even when broken" promise.
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

    private static readonly HashSet<MethodBase> _shielded = new();
    private static readonly HashSet<string> _unpatched = new();
    private static readonly object _lock = new();

    private static readonly Dictionary<string, int> _ownerCounts =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly object _ownerLock = new();

    private static long _swallowedMissingMethod;
    private static long _swallowedMissingField;
    private static long _swallowedTypeLoad;
    private static long _swallowedOther;

    public static int ShieldedCount { get { lock (_lock) return _shielded.Count; } }
    public static int UnpatchedCount { get { lock (_lock) return _unpatched.Count; } }
    public static long SwallowedMissingMethod => Interlocked.Read(ref _swallowedMissingMethod);
    public static long SwallowedMissingField => Interlocked.Read(ref _swallowedMissingField);
    public static long SwallowedTypeLoad => Interlocked.Read(ref _swallowedTypeLoad);
    public static long SwallowedOther => Interlocked.Read(ref _swallowedOther);
    public static long SwallowedTotal => SwallowedMissingMethod + SwallowedMissingField + SwallowedTypeLoad + SwallowedOther;

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
        if (IsDisabled())
        {
            DiagLog.Log(Tag, "patchshield-disabled.flag present — PatchShield install skipped");
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

            int added = 0, skipped = 0, alreadyShielded = 0;
            lock (_lock)
            {
                foreach (var method in patched)
                {
                    if (method == null) { skipped++; continue; }
                    if (_shielded.Contains(method)) { alreadyShielded++; continue; }

                    // Don't shield our own methods.
                    try
                    {
                        var declAsm = method.DeclaringType?.Assembly.GetName().Name ?? string.Empty;
                        if (declAsm.StartsWith("TAOM", StringComparison.OrdinalIgnoreCase))
                        {
                            _shielded.Add(method);
                            skipped++;
                            continue;
                        }
                    }
                    catch { }

                    try
                    {
                        bool isVoid = true;
                        if (method is MethodInfo mi) isVoid = mi.ReturnType == typeof(void);
                        var finalizer = isVoid ? voidFinalizer : resultFinalizer;
                        harmony.Patch(method, prefix: null, postfix: null, transpiler: null,
                            finalizer: new HarmonyMethod(finalizer));
                        _shielded.Add(method);
                        added++;
                    }
                    catch (Exception ex)
                    {
                        skipped++;
                        DiagLog.LogCaught(Tag, $"shielding {method.DeclaringType?.FullName}.{method.Name}", ex);
                    }
                }
            }

            if (added > 0 || alreadyShielded == 0)
            {
                DiagLog.Log(Tag, $"shield pass: +{added} new, {alreadyShielded} already-shielded, {skipped} skipped (total: {_shielded.Count})");
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
    /// returning the original exception (Harmony Finalizer convention).
    /// </summary>
    private static Exception? ShieldFinalizerVoid(MethodBase __originalMethod, Exception __exception)
    {
        return ShouldSwallow(__originalMethod, __exception, out var unwrapped) ? null : unwrapped;
    }

    /// <summary>
    /// Finalizer for return-value methods. Same swallow behavior; the patched method
    /// returns its zero/default value when we swallow because we don't have access
    /// to <c>__result</c> in a Finalizer (Harmony quirk). Acceptable trade-off:
    /// the caller gets a "stub" return value, which is far better than a crash.
    /// </summary>
    private static Exception? ShieldFinalizerWithResult(MethodBase __originalMethod, Exception __exception)
    {
        return ShouldSwallow(__originalMethod, __exception, out var unwrapped) ? null : unwrapped;
    }

    private static bool ShouldSwallow(MethodBase originalMethod, Exception exception, out Exception unwrapped)
    {
        unwrapped = exception;
        if (exception == null) return false;

        // Unwrap TargetInvocationException to get at the real reason.
        var ex = exception;
        while (ex is TargetInvocationException && ex.InnerException != null)
            ex = ex.InnerException;
        unwrapped = ex;

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

        Interlocked.Increment(ref _swallowedOther);
        return false;
    }

    private static void TryUnpatchOffendingPatches(MethodBase originalMethod, Exception ex)
    {
        if (originalMethod == null) return;

        string targetKey;
        try
        {
            targetKey = (originalMethod.DeclaringType?.FullName ?? "?") + "::" + originalMethod.Name;
        }
        catch { return; }

        lock (_lock)
        {
            if (_unpatched.Contains(targetKey)) return;  // already cleaned
            _unpatched.Add(targetKey);
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

            var harmony = new Harmony(HarmonyId);
            foreach (var owner in owners)
            {
                if (string.IsNullOrEmpty(owner) || owner == HarmonyId) continue;

                // Refuse to unpatch our own or anything TAOM-owned.
                if (owner.StartsWith("TAOM", StringComparison.OrdinalIgnoreCase))
                {
                    DiagLog.Log(Tag, $"refusing to unpatch TAOM-owned owner '{owner}' on {targetKey}");
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
            DiagLog.Log(Tag,
                $"SESSION SUMMARY: shielded {ShieldedCount} method(s), unpatched {UnpatchedCount} target(s), " +
                $"swallowed {SwallowedTotal} exception(s) " +
                $"(MissingMethod {SwallowedMissingMethod}, MissingField {SwallowedMissingField}, " +
                $"TypeLoad {SwallowedTypeLoad}, other {SwallowedOther}). " +
                $"Top unpatched owner: {topOwner}.");
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught(Tag, "WriteSessionSummary", ex);
        }
    }
}
