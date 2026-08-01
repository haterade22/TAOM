using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TAOM.Dependencies.Foundation;

/// <summary>
/// Detects whether a third-party co-op module (BannerlordTogether and friends) is active in this
/// launcher session, and exposes the extra Harmony owner prefixes PatchShield should protect.
///
/// STATIC, NOT IoC — deliberately. Its first consumer is <see cref="PatchShield.Install"/>, which
/// runs in TAOM.Dependencies' <c>OnSubModuleLoad</c>, long before TAOM.dll's <c>IoC.Configure()</c>
/// exists. It follows the <see cref="VersionProbe"/> shape for the same reason.
///
/// PROCESS-CONSTANT BY DESIGN. "A co-op module is in the active module list" is decided by the
/// launcher and never changes for the life of the process, which makes it the ONLY co-op fact that
/// is safe to read near a Harmony patch-application site: TAOM's late patch batch is a process
/// one-shot (<c>_gameInitPatchesApplied</c>) and one of its transpilers is non-idempotent, so a
/// gate that varied per campaign could never be undone or re-run. Anything that varies per session
/// — "is a co-op session live", "am I the host" — belongs in a campaign-scoped service, not here.
///
/// Detection is best-effort and fails CLOSED: when the active-module list cannot be read,
/// <see cref="IsActive"/> is false and TAOM behaves exactly as it does today.
/// </summary>
public static class CoopPresence
{
    private const string Tag = "CoopPresence";
    private const string ConfigFileName = "coop-modules.txt";

    /// <summary>
    /// Escape hatch for detection failure. Place this file in the TAOM.Dependencies module
    /// directory to force <see cref="IsActive"/> true regardless of what the launcher reports.
    ///
    /// Detection matches module IDs, so it silently fails for a renamed BannerlordTogether build, a
    /// fork, or a co-op mod we have never heard of — and the failure is the expensive direction:
    /// TAOM keeps enforcing vetoes and UI it should have yielded, which desyncs a live session
    /// rather than crashing it. <c>coop-modules.txt</c> already covers the case where the player
    /// knows the new ID; this covers the case where they do not.
    ///
    /// Matches the <c>patchshield-disabled.flag</c> / <c>saveshield-swallow-disabled.flag</c> idiom
    /// rather than an MCM setting on purpose: MCM persists a saved value over a changed compiled
    /// default, which is what forced NavalTravel and NativeSkinFixes to be disabled at the wiring
    /// level instead. A file the player creates has no persistence trap and is invisible to
    /// everyone who never creates it.
    /// </summary>
    private const string ForceActiveFlagName = "coop-force-active.flag";

    /// <summary>
    /// Module ids that identify a co-op mod. Compiled defaults; the shipped
    /// <c>coop-modules.txt</c> can ADD to this but never remove from it.
    /// </summary>
    internal static readonly string[] CompiledModuleDefaults =
    {
        "BannerlordTogether",
        "BattleLinkMPClient",
        // BannerlordCoop (Steam Workshop 3770450698, upstream Bannerlord-Coop-Team/BannerlordCoop).
        // A DIFFERENT mod from BannerlordTogether, with its own architecture — the launcher id is
        // the bare string "Coop" (its SubModule.xml <Id value="Coop"/>), and matching below is exact
        // equality, so it was invisible to every shield until 2026-08-01.
        // Internals + evidence: docs/research/bannerlordcoop-internals.md.
        "Coop",
    };

    /// <summary>
    /// Extra Harmony owner-id prefixes to protect from PatchShield's unpatch path.
    ///
    /// Empty on purpose — but no longer because the ids are unknowable.
    ///
    /// BannerlordTogether's package ships an explicit no-decompile / no-AI-analysis policy from its
    /// copyright holders, so its Harmony id is still obtained only at runtime from Harmony's own
    /// public registry (the census writer) or from its authors, then added to
    /// <c>coop-modules.txt</c> without a rebuild.
    ///
    /// BannerlordCoop is a DIFFERENT mod and carries no such policy (public upstream project, and it
    /// ships 893 of its own generated .cs files in plaintext), so it was decompiled on 2026-08-01 and
    /// its four owner ids are now compiled directly into
    /// <see cref="PatchShieldPolicy.CompiledProtectedOwnerPrefixes"/> — not here, because this list
    /// exists for ids learned at runtime. See docs/research/bannerlordcoop-internals.md.
    ///
    /// Either way the real protection is the unpatch gate in PatchShield keyed on
    /// <see cref="IsActive"/>, which does not need any id at all.
    /// </summary>
    private static readonly string[] CompiledOwnerDefaults = Array.Empty<string>();

    private static readonly object _lock = new();
    private static bool _probed;
    private static List<string> _activeCoopModuleIds = new();
    private static List<string> _extraProtectedOwnerPrefixes = new();

    /// <summary>True when at least one known co-op module id is active in this session.</summary>
    public static bool IsActive
    {
        get { EnsureProbed(); lock (_lock) return _activeCoopModuleIds.Count > 0; }
    }

    /// <summary>The co-op module ids actually found active. Empty when none (or when unknown).</summary>
    public static IReadOnlyList<string> ActiveCoopModuleIds
    {
        get { EnsureProbed(); lock (_lock) return _activeCoopModuleIds.ToList(); }
    }

    /// <summary>Owner prefixes to union into PatchShield's protected-owner allowlist.</summary>
    public static IReadOnlyList<string> ExtraProtectedOwnerPrefixes
    {
        get { EnsureProbed(); lock (_lock) return _extraProtectedOwnerPrefixes.ToList(); }
    }

    /// <summary>
    /// Re-runs detection. Cheap and idempotent. Call once in <c>OnSubModuleLoad</c> (before
    /// PatchShield's first pass) and again in <c>OnGameInitializationFinished</c>, since
    /// ModuleHelper's active-module list may not be populated at the earlier point.
    /// </summary>
    public static void Refresh()
    {
        lock (_lock)
        {
            _probed = false;
        }
        EnsureProbed();
    }

    // KNOWN GAP, deliberately not papered over: detection is based on the launcher's ACTIVE-module
    // list, which reflects what the player enabled, not what successfully constructed. If a co-op
    // module's SubModule constructor throws, SubModuleConstructionGuard swallows it (crashing the
    // launcher helps nobody) and this probe still reports the module as active — so TAOM runs the
    // session in co-op mode for a co-op layer that is dead: PatchShield withholds its rescue and
    // SaveShield rethrows save-load faults. An earlier draft carried a MarkConstructionFailed()
    // method for this, but nothing called it and Refresh() would have re-added the id from the
    // launcher list anyway, so it was a documented guarantee the code did not provide. Closing this
    // properly needs the construction guard to map a failing assembly back to its module id AND a
    // suppression set that survives re-probing; until then the honest statement is that this gap
    // exists. See docs/features/bannerlord-together-compat.md "Known limitations".

    private static void EnsureProbed()
    {
        lock (_lock)
        {
            if (_probed) return;
            _probed = true;

            var config = LoadConfig();
            _extraProtectedOwnerPrefixes = config.OwnerPrefixes.ToList();

            var active = ReadActiveModuleIds();
            var forced = IsForcedActive();

            // The decision — fail-closed on unknown, flag adds but never removes — lives in
            // CoopPresencePolicy so it can be unit-tested without file or reflection I/O.
            _activeCoopModuleIds =
                CoopPresencePolicy.ResolveActiveIds(active, config.ModuleIds, forced).ToList();

            LogProbeResult(active.Count, forced);
        }
    }

    private static List<string> ReadActiveModuleIds()
    {
        try
        {
            return IncompatibleModDetector.TryReadActiveModuleIdsViaReflection();
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught(Tag, "EnsureProbed/TryReadActiveModuleIdsViaReflection", ex);
            return new List<string>();
        }
    }

    private static void LogProbeResult(int scannedCount, bool forced)
    {
        if (scannedCount == 0)
        {
            // Unknown, not "none".
            DiagLog.Log(Tag, "EnsureProbed: active module list unavailable; treating co-op as NOT present");
        }

        if (_activeCoopModuleIds.Count == 0)
        {
            DiagLog.Log(Tag, $"EnsureProbed: no co-op module active (scanned {scannedCount} active modules)");
            return;
        }

        if (forced && _activeCoopModuleIds.Contains(CoopPresencePolicy.ForcedMarkerId))
        {
            DiagLog.Log(Tag,
                $"EnsureProbed: {ForceActiveFlagName} present — forcing co-op ACTIVE " +
                $"(no known co-op module id among {scannedCount} active modules)");
            return;
        }

        DiagLog.Log(Tag,
            $"EnsureProbed: co-op module(s) ACTIVE: {string.Join(", ", _activeCoopModuleIds)} " +
            $"(scanned {scannedCount} active modules)");
    }

    /// <summary>Mirrors <c>PatchShield.IsDisabled()</c> — never throws, absent file means false.</summary>
    private static bool IsForcedActive()
    {
        try
        {
            var dir = RuntimeLog.ModuleDir;
            if (string.IsNullOrEmpty(dir)) return false;
            return File.Exists(Path.Combine(dir, ForceActiveFlagName));
        }
        catch { return false; }
    }

    private static CoopModuleListResult LoadConfig()
    {
        try
        {
            var dir = RuntimeLog.ModuleDir;
            if (!string.IsNullOrEmpty(dir))
            {
                var path = Path.Combine(dir, ConfigFileName);
                if (File.Exists(path))
                {
                    var result = CoopModuleList.Parse(
                        File.ReadAllLines(path), CompiledModuleDefaults, CompiledOwnerDefaults);
                    DiagLog.Log(Tag,
                        $"LoadConfig: read {ConfigFileName} — {result.ModuleIds.Count} module id(s), " +
                        $"{result.OwnerPrefixes.Count} owner prefix(es){(result.Truncated ? " [TRUNCATED: entry cap hit]" : "")}");
                    return result;
                }
            }
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught(Tag, "LoadConfig", ex);
        }

        return CoopModuleList.Parse(null, CompiledModuleDefaults, CompiledOwnerDefaults);
    }
}
