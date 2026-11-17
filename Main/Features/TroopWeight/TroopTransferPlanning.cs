using System;
using System.Collections.Generic;

namespace TAOM.Features.TroopWeight;

/// <summary>One roster row as Transfer All sees it: its headcount, the weight of one troop, and whether vanilla's own
/// filter (locks, <c>IsTroopTransferable</c>) lets it move.</summary>
public readonly record struct TroopTransferRow(int Count, float Weight, bool Eligible);

/// <summary>
/// Transfer All's member budget in the weighted frame (#1430, from Silas He's v1.5.6 fix package). Vanilla's
/// <c>PartyScreenLogic.TransferAllTroops</c> (v1.5.7 :997-1045) moves whole rows in order, clamps the first row that
/// does not fit and stops. It counts heads; the party pays weight. This returns the headcount that reproduces the same
/// in-order plan when each troop costs its weight, so vanilla's loop runs unchanged on it.
/// </summary>
public static class TroopTransferPlanning
{
    // Absorbs float noise only: 3.0 / 0.1f divides to 29.9999996.
    private const double Epsilon = 1e-4;

    /// <summary>The headcount to hand vanilla's loop. 0 only when there is no room (or no eligible troop), which vanilla
    /// reads as its full-party press: move everything. With some room left it is at least 1.</summary>
    public static int WeightedFillBudget(IReadOnlyList<TroopTransferRow>? rows, double freeSlots)
    {
        if (rows == null || double.IsNaN(freeSlots) || freeSlots <= 0d)
            return 0;

        double free = freeSlots;
        int budget = 0;
        foreach (var row in rows)
        {
            if (!row.Eligible || row.Count <= 0)
                continue;

            double weight = row.Weight > 0f && !float.IsInfinity(row.Weight) ? row.Weight : 1d;
            int fits = (int)Math.Floor(free / weight + Epsilon);
            if (fits >= row.Count)
            {
                budget += row.Count;
                free -= row.Count * weight;
                continue;
            }

            // Vanilla clamps this row to the room left. If nothing moved yet, one troop moves: 0 would make vanilla
            // move the whole roster, and its own headcount budget at this room moves at least one.
            budget += Math.Max(budget == 0 ? 1 : 0, fits);
            break;
        }
        return budget;
    }
}
