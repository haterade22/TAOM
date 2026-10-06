using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.GeneratedLordKits;

/// <summary>
/// Chooses a generated lord's kit from what the XML lords of the same culture, race and sex are defined with.
/// Vanilla's <c>DefaultEquipmentSelectionModel</c> never looks at race, so it would put an uruk lord's kit on
/// a human Mordor lord; this filters on it.
/// </summary>
public static class LordKitSelector
{
    /// <summary>A kit counts once this many distinct lords are defined with it; fewer is one lord's own gear.</summary>
    public const int MinimumDonors = 2;

    /// <summary>
    /// Whether the hero gets a peer kit at all: adult lords outside minor-faction clans. Everyone else keeps the
    /// engine's handling. The adapter asks before it scans the lords.
    /// </summary>
    public static bool Wants(LordKitRequest request) =>
        request != null && request.IsLord && request.IsAdult && !request.InMinorFactionClan;

    /// <summary>
    /// Returns one candidate of a randomly chosen shared kit, or null when the request is refused or its culture,
    /// race and sex share none (the caller then falls back to the engine's template pick). A kit is shared when
    /// <see cref="MinimumDonors"/> lords are defined with it and at least one of them is alive, so the pool keeps
    /// a kit as its wearers die but never revives one only the dead wore. <paramref name="randomBelow"/> returns
    /// an index in [0, bound); it ranges over distinct kits, so a popular kit does not crowd out the rest.
    /// </summary>
    public static LordKitCandidate Pick(LordKitRequest request, IReadOnlyList<LordKitCandidate> candidates,
        Func<int, int> randomBelow)
    {
        if (!Wants(request) || candidates == null)
            return null;

        var kits = candidates
            .Where(c => c != null
                        && string.Equals(c.CultureId, request.CultureId, StringComparison.Ordinal)
                        && c.Race == request.Race
                        && c.IsFemale == request.IsFemale
                        && c.IsCivilian == request.IsCivilian)
            .GroupBy(c => c.Signature, StringComparer.Ordinal)
            .Where(g => g.Any(c => c.IsAlive)
                        && g.Select(c => c.DonorId).Distinct(StringComparer.Ordinal).Count() >= MinimumDonors)
            .ToList();
        return kits.Count == 0 ? null : kits[randomBelow(kits.Count)].First();
    }
}
