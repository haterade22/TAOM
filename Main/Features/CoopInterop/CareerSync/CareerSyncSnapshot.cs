using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CoopInterop.CareerSync;

/// <summary>
/// One hero's career as the co-op career sync carries it (docs/features/coop-interop.md, "Career sync"): the same
/// fields <see cref="HeroCareerData"/> holds and CareerPersistenceBehavior saves, with the choices in the order they
/// were taken. A null or empty <see cref="CareerId"/> is a cleared career.
/// </summary>
public sealed record CareerSyncSnapshot(
    string HeroId,
    string? CareerId,
    IReadOnlyList<string> Choices,
    IReadOnlyList<int> Tiers,
    IReadOnlyList<string> Flags)
{
    public static CareerSyncSnapshot Of(HeroCareerData data) => new(
        data.HeroStringId ?? "",
        data.CareerStringId,
        (data.ChoiceIds ?? new List<string>()).ToArray(),
        (data.TierUnlocks ?? new List<int>()).ToArray(),
        (data.Flags ?? new List<string>()).ToArray());

    public HeroCareerData ToData()
    {
        var data = new HeroCareerData(HeroId) { CareerStringId = string.IsNullOrEmpty(CareerId) ? null : CareerId };
        data.ChoiceIds.AddRange(Choices ?? Array.Empty<string>());
        data.TierUnlocks.AddRange(Tiers ?? Array.Empty<int>());
        data.Flags.AddRange(Flags ?? Array.Empty<string>());
        return data;
    }

    /// <summary>The same hero and the same career, list by list and in order. No career equals an empty career id.</summary>
    public bool SameAs(CareerSyncSnapshot? other) =>
        other != null
        && string.Equals(HeroId, other.HeroId, StringComparison.Ordinal)
        && string.Equals(CareerId ?? "", other.CareerId ?? "", StringComparison.Ordinal)
        && (Choices ?? Array.Empty<string>()).SequenceEqual(other.Choices ?? Array.Empty<string>())
        && (Tiers ?? Array.Empty<int>()).SequenceEqual(other.Tiers ?? Array.Empty<int>())
        && (Flags ?? Array.Empty<string>()).SequenceEqual(other.Flags ?? Array.Empty<string>());
}
