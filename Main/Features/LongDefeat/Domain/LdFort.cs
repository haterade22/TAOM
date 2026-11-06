using System;
using System.Collections.Generic;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// One visible gate as the adapter reads it (read each tick, plain numbers and flags): the damage-state index it stores, its hit points, its maximum, the vanilla maximum read at boot before any hit,
/// and whether it is destroyed or open.
/// </summary>
internal readonly record struct LdGateFacts(int StateIndex, float HitPoints, float MaxHitPoints, float VanillaMax, bool Destroyed, bool Open)
{
    /// <summary>The share of the maximum that is gone: 1 for a destroyed gate, 0 for a whole one, and 0 for facts that are not numbers.</summary>
    public float MissingShare
    {
        get
        {
            if (Destroyed)
                return 1f;
            if (!(MaxHitPoints > 0f) || float.IsInfinity(MaxHitPoints) || float.IsNaN(HitPoints))
                return 0f;
            return Math.Max(0f, Math.Min(1f, 1f - HitPoints / MaxHitPoints));
        }
    }
}

/// <summary>What a fortification buy answers. <c>NotOffered</c> is a kind or level this build cannot deliver yet (<see cref="FortOffer"/>), <c>Hidden</c> a row the fortress or shop does not carry.</summary>
internal enum FortResult
{
    Ok,
    NotOpen,
    NotOffered,
    NoGold,
    GarrisonFull,
    MaxLevel,
    NothingToRepair,
    Used,
    Hidden,
    RunOver,
}

internal enum PurchaseKind
{
    Pack,
    GateLevel,
    GateRepair,
    TowerLevel,
    FieldCare,
    VolleyLevel,
}

/// <summary>
/// One thing bought, in order. <c>BeforeWave</c> is the wave that starts next; <c>Id</c> is the offer of a pack and empty for the rest; <c>Level</c> is the level a level kind reached and 0 for the rest;
/// <c>BasePrice</c> is shop.json's price and <c>Price</c> what was paid after the boons' price factor (D29); <c>Share</c> is the missing share a repair mended and the share of the missing health care
/// returned, 0 for the rest.
/// </summary>
internal sealed record FortPurchase(int BeforeWave, PurchaseKind Kind, string Id, int Level, int BasePrice, int Price, float Share);

/// <summary>What the report lists of the fortifications: every purchase in order, and the levels owned at the end.</summary>
internal sealed record RunFort(IReadOnlyList<FortPurchase> Purchases, int GateLevel, int TowerLevel, int VolleyLevel)
{
    public static RunFort None { get; } = new(Array.Empty<FortPurchase>(), 0, 0, 0);
}

/// <summary>
/// The posts to man for the high towers, as the director will hand the adapter: <c>Posts</c> per tower, up to <c>Room</c> soldiers more under the garrison cap. The refill is free and comes before any pack.
/// </summary>
internal sealed record LdPostFill(string Troop, int Posts, int Room);

/// <summary>A wave's gate row: the level in force at its start, the hit points the gates held then and could hold, whether one fell in the wave and when. Sums over the visible gates (Helm's Deep has one).</summary>
internal sealed record GateWaveRecord(int Level, float StartHitPoints, float MaxHitPoints, bool Fell, float? FellAtSeconds)
{
    public static GateWaveRecord None { get; } = new(0, 0f, 0f, false, null);
}

/// <summary>A wave's post counts: filled at its start, left short by the cap, manned when it began, lost in it. Written at 0 until the posts exist (Slice 7).</summary>
internal sealed record PostsWaveRecord(int Filled, int Short, int Manned, int Lost)
{
    public static PostsWaveRecord None { get; } = new(0, 0, 0, 0);
}

/// <summary>A wave's volley counts: uses, arrows loosed, hits, kills and uses withheld. Written at 0 until the volleys exist (Slices 3, 6 and 8).</summary>
internal sealed record VolleyWaveRecord(int Uses, int Arrows, int Hits, int Kills, int Withheld)
{
    public static VolleyWaveRecord None { get; } = new(0, 0, 0, 0, 0);
}

/// <summary>What a cleared wave adds to its report row about the fortifications.</summary>
internal sealed record FortWave(GateWaveRecord Gate, PostsWaveRecord Posts, VolleyWaveRecord Volley, VolleyWaveRecord TowerVolley)
{
    public static FortWave None { get; } = new(GateWaveRecord.None, PostsWaveRecord.None, VolleyWaveRecord.None, VolleyWaveRecord.None);
}
