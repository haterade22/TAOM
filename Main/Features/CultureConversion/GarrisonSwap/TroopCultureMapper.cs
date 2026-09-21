using System;
using System.Collections.Generic;
using TAOM.Features.CultureConversion.GarrisonSwap.Domain;

namespace TAOM.Features.CultureConversion.GarrisonSwap;

/// <summary>
/// The matching ladder. See <see cref="ITroopCultureMapper"/> for the contract.
///
/// Why a ladder rather than a lookup: TAOM's per-culture rosters are deliberately uneven, so an
/// exact (tier, role) cell is often empty. Measured 2026-09-20 across the 16 <c>troops_*.xml</c>
/// files, tiering with the engine's own <c>clamp(ceil((level-5)/5), 0, 10)</c> — Mirkwood has no
/// troop at all at tiers 4, 5 or 6; Goblin, Blue Craig and the Misty Mountain orcs have no cavalry
/// at any tier; Dunland, Dale and Umbar stop at tier 6, so a captured tier-8 Gondor stack has no
/// same-tier target anywhere. Every rung below exists because a real cell needs it.
/// </summary>
public sealed class TroopCultureMapper : ITroopCultureMapper
{
    /// <summary>
    /// How far rung 2 may stray from the original tier before role-preservation stops being worth
    /// it. Beyond this, keeping the tier (rung 3) protects the garrison's strength better than
    /// keeping the role: turning a tier-5 cavalryman into a tier-1 cavalryman is a far bigger loss
    /// than turning him into tier-5 infantry.
    /// </summary>
    private const int NearbyTierWindow = 2;

    public TroopSwapPlan MapGarrison(
        string settlementId,
        string targetCultureId,
        IReadOnlyList<GarrisonTroopInfo> roster,
        CultureTroopIndex targetIndex)
        => Plan(settlementId, targetCultureId, roster, row => ResolveByLadder(settlementId, row, targetIndex), targetIndex);

    public TroopSwapPlan MapMilitia(
        string settlementId,
        string targetCultureId,
        IReadOnlyList<GarrisonTroopInfo> roster,
        CultureMilitiaTroops? fromMilitia,
        CultureMilitiaTroops? toMilitia,
        CultureTroopIndex? targetIndex)
        => Plan(settlementId, targetCultureId, roster, row =>
        {
            var slot = fromMilitia?.SlotOf(row.TroopId);
            if (slot.HasValue)
            {
                var bySlot = toMilitia?.TroopFor(slot.Value);
                if (!string.IsNullOrEmpty(bySlot))
                    return bySlot;
            }
            // Not one of the old culture's four militia slots (or the new culture omits that slot) —
            // fall back to the ordinary ladder so the stack is still handled.
            return ResolveByLadder(settlementId, row, targetIndex);
        }, targetIndex);

    private static TroopSwapPlan Plan(
        string settlementId,
        string targetCultureId,
        IReadOnlyList<GarrisonTroopInfo> roster,
        Func<GarrisonTroopInfo, string?> resolve,
        CultureTroopIndex? targetIndex)
    {
        if (roster == null || roster.Count == 0)
            return TroopSwapPlan.Empty;

        var swaps = new List<TroopSwap>();
        var unmapped = new List<string>();

        foreach (var row in roster)
        {
            if (row == null || row.IsHero || row.Count <= 0 || string.IsNullOrEmpty(row.TroopId))
                continue;

            // A template with no culture is something we do not understand well enough to replace
            // (and the engine allows it). Leaving it is the fail-safe choice, not an oversight.
            if (string.IsNullOrEmpty(row.CultureId))
                continue;

            // Already right. Two separate reasons, and the second is not redundant: a culture that
            // borrows another's line (Lothlorien fields the Rivendell troops it recruits) would
            // otherwise churn a perfectly correct garrison, because those troops carry the donor's
            // culture tag. Lords donating their own troops into a fief they just took is the common
            // case for the first test.
            if (string.Equals(row.CultureId, targetCultureId, StringComparison.Ordinal))
                continue;
            if (targetIndex != null && targetIndex.Contains(row.TroopId))
                continue;

            var replacement = resolve(row);
            if (string.IsNullOrEmpty(replacement))
            {
                unmapped.Add(row.TroopId);
                continue;
            }
            if (string.Equals(replacement, row.TroopId, StringComparison.Ordinal))
                continue;

            // Clamp rather than trust: a roster row reporting more wounded than bodies would make
            // the engine's AddToCounts assert.
            var wounded = row.WoundedCount < 0 ? 0 : (row.WoundedCount > row.Count ? row.Count : row.WoundedCount);
            swaps.Add(new TroopSwap(row.TroopId, replacement!, row.Count, wounded));
        }

        return swaps.Count == 0 && unmapped.Count == 0 ? TroopSwapPlan.Empty : new TroopSwapPlan(swaps, unmapped);
    }

    private static string? ResolveByLadder(string settlementId, GarrisonTroopInfo row, CultureTroopIndex? index)
    {
        if (index == null || index.IsEmpty)
            return null;

        // Unknown is not a role TAOM troop data produces; treat it as infantry, the one role every
        // culture fields at some tier.
        var role = row.Role == TroopRole.Unknown ? TroopRole.Infantry : row.Role;
        var tier = row.Tier;

        // 1. Exact tier and role.
        var exact = index.Candidates(role, tier);
        if (exact.Count > 0)
            return Pick(settlementId, role, tier, exact);

        // 2. Same role, nearest tier within the window, preferring a lower tier on a tie so a swap
        //    never hands out a free upgrade.
        var near = NearestTier(index.TiersFor(role), tier, NearbyTierWindow);
        if (near.HasValue)
            return Pick(settlementId, role, near.Value, index.Candidates(role, near.Value));

        // 3. Same tier, walking the role fallback chain.
        foreach (var alt in FallbackChain(role))
        {
            var cell = index.Candidates(alt, tier);
            if (cell.Count > 0)
                return Pick(settlementId, alt, tier, cell);
        }

        // 4. Same role at any distance (the culture fields this role, just nowhere near this tier).
        var anyDistance = NearestTier(index.TiersFor(role), tier, int.MaxValue);
        if (anyDistance.HasValue)
            return Pick(settlementId, role, anyDistance.Value, index.Candidates(role, anyDistance.Value));

        // 5. Nearest populated tier, any role, chain order breaking the tie.
        var nearestTier = NearestTier(index.AllTiers, tier, int.MaxValue);
        if (!nearestTier.HasValue)
            return null;

        foreach (var alt in FallbackChain(role))
        {
            var cell = index.Candidates(alt, nearestTier.Value);
            if (cell.Count > 0)
                return Pick(settlementId, alt, nearestTier.Value, cell);
        }
        foreach (var present in index.RolesAt(nearestTier.Value))
        {
            var cell = index.Candidates(present, nearestTier.Value);
            if (cell.Count > 0)
                return Pick(settlementId, present, nearestTier.Value, cell);
        }
        return null;
    }

    /// <summary>
    /// Role substitution order, most-similar first, each chain starting with the role itself.
    /// Mounted degrades to mounted before it degrades to foot; ranged and foot trade places last
    /// because that is the swap a player most notices on a wall.
    /// </summary>
    private static IReadOnlyList<TroopRole> FallbackChain(TroopRole role) => role switch
    {
        TroopRole.HorseArcher => new[] { TroopRole.HorseArcher, TroopRole.Cavalry, TroopRole.Ranged, TroopRole.Infantry },
        TroopRole.Cavalry => new[] { TroopRole.Cavalry, TroopRole.Infantry, TroopRole.Ranged },
        TroopRole.Ranged => new[] { TroopRole.Ranged, TroopRole.Infantry },
        _ => new[] { TroopRole.Infantry, TroopRole.Ranged },
    };

    /// <summary>
    /// Closest tier to <paramref name="target"/> within <paramref name="window"/>, preferring the
    /// lower of two equally distant tiers. <paramref name="tiers"/> is ascending and may be empty.
    /// </summary>
    private static int? NearestTier(IReadOnlyList<int> tiers, int target, int window)
    {
        int? best = null;
        var bestDistance = int.MaxValue;
        foreach (var tier in tiers)
        {
            var distance = Math.Abs(tier - target);
            if (distance > window)
                continue;
            // Strictly-less keeps the first (lower) of two equally distant tiers, because `tiers`
            // is ascending. Do not relax this to <=; it silently flips the tie-break upward.
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = tier;
            }
        }
        return best;
    }

    /// <summary>
    /// Deterministic choice among a cell's candidates, keyed on the settlement plus the cell that
    /// was actually resolved. Two towns converting to the same culture get different troops; the
    /// same town converting twice gets the same answer, which is what keeps the tests stable and
    /// stops a reload from looking like a second swap.
    /// </summary>
    private static string Pick(string settlementId, TroopRole role, int tier, IReadOnlyList<string> candidates)
        => candidates[(int)(StableHash($"{settlementId}|{(int)role}|{tier}") % (uint)candidates.Count)];

    /// <summary>
    /// FNV-1a 32-bit. Deliberately not <c>string.GetHashCode</c>, which is not guaranteed stable
    /// across processes or runtimes — a garrison that re-rolled its troops on every game start would
    /// be a save-visible bug.
    /// </summary>
    private static uint StableHash(string value)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var c in value)
            {
                hash ^= c;
                hash *= 16777619u;
            }
            return hash;
        }
    }
}
