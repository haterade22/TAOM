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
    /// Module ids that identify a co-op mod. Compiled defaults; the shipped
    /// <c>coop-modules.txt</c> can ADD to this but never remove from it.
    /// </summary>
    private static readonly string[] CompiledModuleDefaults =
    {
        "BannerlordTogether",
        "BattleLinkMPClient",
    };

    /// <summary>
    /// Extra Harmony owner-id prefixes to protect from PatchShield's unpatch path.
    ///
    /// Empty on purpose. We do not know any co-op mod's Harmony id, and we will not decompile one
    /// to find out — the BannerlordTogether package ships an explicit no-decompile /
    /// no-AI-analysis policy from its copyright holders. The id is instead obtained at runtime from
    /// Harmony's own public registry (the census writer) or from the mod's authors, then added to
    /// <c>coop-modules.txt</c> without a rebuild. Until then the real protection is the unpatch
    /// gate in PatchShield keyed on <see cref="IsActive"/>, which does not need the id at all.
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

            List<string> active;
            try
            {
                active = IncompatibleModDetector.TryReadActiveModuleIdsViaReflection();
            }
            catch (Exception ex)
            {
                DiagLog.LogCaught(Tag, "EnsureProbed/TryReadActiveModuleIdsViaReflection", ex);
                active = new List<string>();
            }

            if (active.Count == 0)
            {
                // Unknown, not "none". Fail closed — behave exactly as an unmodded session.
                DiagLog.Log(Tag, "EnsureProbed: active module list unavailable; treating co-op as NOT present");
                _activeCoopModuleIds = new List<string>();
                return;
            }

            var known = new HashSet<string>(config.ModuleIds, StringComparer.OrdinalIgnoreCase);
            _activeCoopModuleIds = active.Where(known.Contains).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            DiagLog.Log(Tag,
                _activeCoopModuleIds.Count > 0
                    ? $"EnsureProbed: co-op module(s) ACTIVE: {string.Join(", ", _activeCoopModuleIds)} (scanned {active.Count} active modules)"
                    : $"EnsureProbed: no co-op module active (scanned {active.Count} active modules)");
        }
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
