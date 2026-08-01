using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Dependencies.Foundation;

/// <summary>
/// The pure decision behind <see cref="CoopPresence"/>: given what the launcher reported, which ids
/// count as co-op, and whether the force flag is present — which co-op module ids are active?
///
/// Split out for the same reason as <see cref="PatchShieldPolicy"/> and
/// <see cref="SaveShieldPolicy"/>: <see cref="CoopPresence"/> is static and does file + reflection
/// I/O, so the decision inside it was untestable. This is the whole rule, with no I/O.
/// </summary>
public static class CoopPresencePolicy
{
    /// <summary>
    /// Reported in place of a real module id when the force flag supplied presence that detection
    /// did not. Deliberately not a plausible module id — it must never be mistaken for a detection
    /// result in a log or the Harmony census.
    /// </summary>
    public const string ForcedMarkerId = "(forced-by-flag)";

    /// <summary>
    /// Resolves the active co-op module ids.
    ///
    /// Two invariants this encodes, both direction-of-safety:
    /// <list type="bullet">
    /// <item>An empty <paramref name="activeModuleIds"/> means "unknown", not "none" — fail CLOSED
    /// to no co-op, so a session that cannot be inspected behaves exactly like an unmodded one.</item>
    /// <item>The force flag only ever ADDS presence. It supplies a marker when nothing was
    /// detected, and it can never remove or mask a real detection.</item>
    /// </list>
    /// </summary>
    public static IReadOnlyList<string> ResolveActiveIds(
        IEnumerable<string> activeModuleIds,
        IEnumerable<string> knownCoopIds,
        bool forceFlagPresent)
    {
        var detected = MatchKnown(activeModuleIds, knownCoopIds);

        // Order matters: the flag is consulted ONLY when detection produced nothing, which is what
        // makes it purely additive. It can never mask or replace a real id.
        if (detected.Count == 0 && forceFlagPresent)
            return new List<string> { ForcedMarkerId };

        return detected;
    }

    private static List<string> MatchKnown(
        IEnumerable<string> activeModuleIds, IEnumerable<string> knownCoopIds)
    {
        if (activeModuleIds == null || knownCoopIds == null) return new List<string>();

        var known = new HashSet<string>(
            knownCoopIds.Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.OrdinalIgnoreCase);
        if (known.Count == 0) return new List<string>();

        return activeModuleIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Where(known.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
