using System.Collections.Generic;

namespace TAOM.Features.LongDefeat.Domain;

// The fortifications of phase3-plan.md section 5, as shop.json holds them. Plain data: FortificationsReader builds it, SurvivalRun sells from it.

/// <summary>One gate level: the share of the gate's vanilla hit points it brings the maximum to (cumulative, 100 is twice the vanilla maximum) and its price.</summary>
internal sealed record GateLevelPlan(float HitPointsPercent, int Price);

/// <summary>The gate row: what a repair costs in full, and the levels in order. At least one level.</summary>
internal sealed record GatePlan(int RepairPrice, IReadOnlyList<GateLevelPlan> Levels);

/// <summary>One tower level: the posts each recorded tower holds (not falling across levels), whether it adds the tower volley (the last level only), and the price.</summary>
internal sealed record TowerLevelPlan(int Posts, bool Volley, int Price);

/// <summary>The volley the towers loose at their last level: the arrow's item, the arrows per use, the first delay and the cooldown, the group radius, the reach and the height the arrow leaves a recorded spot at.</summary>
internal sealed record TowerVolleyPlan(string ItemId, int Arrows, float FirstSeconds, float CooldownSeconds, float Radius, float Range, float OriginHeight);

/// <summary>The "High towers" row: the troop each post is manned by, the levels in order and the tower volley. At least one level.</summary>
internal sealed record TowerPlan(string Troop, IReadOnlyList<TowerLevelPlan> Levels, TowerVolleyPlan? Volley);

/// <summary>The field care row: the share of the missing health it gives back, its price and how often an intermission allows it.</summary>
internal sealed record CarePlan(float Share, int Price, int PerIntermission);

internal enum VolleyAimMode
{
    Auto,
    Crosshair,
}

/// <summary>One Elven Volley level.</summary>
internal sealed record VolleyLevelPlan(int Price, int Arrows, float CooldownSeconds, float Radius, float Range, VolleyAimMode Aim);

/// <summary>The Elven Volley row: its arrow item, the height the arrow leaves a recorded origin at, and the levels in order (the first aims itself). At least one level.</summary>
internal sealed record VolleyPlan(string ItemId, float OriginHeight, IReadOnlyList<VolleyLevelPlan> Levels);

/// <summary>A shop's fortifications; a null row is absent from shop.json (or failed its rules) and is hidden.</summary>
internal sealed record FortPlan(GatePlan? Gate, TowerPlan? Towers, CarePlan? Care, VolleyPlan? Volley)
{
    public static FortPlan None { get; } = new(null, null, null, null);
}

/// <summary>
/// The kinds and levels the code can deliver (D31): the highest level of each, 0 for none. Each slice raises its kind when its effect lands, as <see cref="AffixPlan.Pool"/> grows with the powers;
/// the Python replay (<c>FORT_OFFERED</c>) mirrors it. A row of shop.json beyond it is not for sale, so a build never sells what it cannot do. Slice 2 ships it empty.
/// </summary>
internal sealed record FortOffer(int Gate, int Towers, bool FieldCare, int Volley)
{
    public static FortOffer Offered { get; } = new(0, 0, false, 0);
}
