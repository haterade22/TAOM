using System.Collections.Generic;
using TAOM.Core.Validation;
using TAOM.Features.LotrIssues.Domain;

namespace TAOM.Features.LotrIssues;

/// <summary>Pure rules of the HuntBand quest: which clan the band is drawn from and who counts as its killer.</summary>
internal static class LotrQuestBandRules
{
    /// <summary>
    /// The non-creature clan whose culture's nearest hideout is closest to the issue settlement. A distance that is
    /// not a finite value of at least zero never wins. With no usable distance the first non-creature clan stands;
    /// with no non-creature clan the answer is null.
    /// </summary>
    internal static string? PickClan(IReadOnlyList<LotrBandClanOption>? options)
    {
        if (options == null) return null;
        string? first = null;
        string? nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (var option in options)
        {
            if (option.IsCreature || string.IsNullOrEmpty(option.ClanId)) continue;
            first ??= option.ClanId;
            float d = option.HideoutDistanceSquared;
            if (!FiniteFloatValidator.IsFiniteAtLeast(d, 0f)) continue;
            if (nearest == null || d < nearestDistance)
            {
                nearest = option.ClanId;
                nearestDistance = d;
            }
        }
        return nearest ?? first;
    }

    /// <summary>The band counts as beaten by the player when the main party, or the army the player fights in, destroyed it.</summary>
    internal static bool IsDefeatedByPlayer(bool destroyerIsMainParty, bool destroyerLeadsPlayersArmy)
        => destroyerIsMainParty || destroyerLeadsPlayersArmy;
}
