using System;
using System.Collections.Generic;

namespace TAOM.Features.SpecialResources.Domain;

/// <summary>
/// One troop type's share of the daily upkeep bill. <see cref="PerUnit"/> and <see cref="Total"/>
/// already carry the career upkeep modifier, so the lines under a tooltip add up to the total
/// printed beneath them.
/// </summary>
public sealed class TroopUpkeepLine
{
    public string TroopId { get; }
    public int Count { get; }
    public float PerUnit { get; }
    public float Total { get; }

    public TroopUpkeepLine(string troopId, int count, float perUnit)
        : this(troopId, count, perUnit, perUnit * count)
    {
    }

    public TroopUpkeepLine(string troopId, int count, float perUnit, float total)
    {
        TroopId = troopId;
        Count = count;
        PerUnit = perUnit;
        Total = total;
    }
}

/// <summary>
/// The daily special-resource change, computed once and read by every consumer: the tick that
/// applies it, the map-bar tooltip, the daily message and the console dump. Before this existed the
/// tooltip ran its own income (without the career passive) against an EMPTY troop list, so it could
/// never agree with the tick (#558).
///
/// <see cref="UpkeepLines"/> holds only troops whose cost row carries a <c>daily_upkeep</c>; a troop
/// the Elite Emissary merely sells (merchant_cost only) is not an upkeep troop, does not appear here,
/// and does not desert.
/// </summary>
public sealed class DailyResourceBreakdown
{
    public static readonly DailyResourceBreakdown Empty = new(0f, Array.Empty<TroopUpkeepLine>());

    public float Earning { get; }
    public float Upkeep { get; }
    public float Net => Earning - Upkeep;
    public IReadOnlyList<TroopUpkeepLine> UpkeepLines { get; }

    public DailyResourceBreakdown(float earning, IReadOnlyList<TroopUpkeepLine> upkeepLines)
    {
        Earning = earning;
        UpkeepLines = upkeepLines ?? Array.Empty<TroopUpkeepLine>();

        var upkeep = 0f;
        foreach (var line in UpkeepLines)
            upkeep += line.Total;
        Upkeep = Math.Max(0f, upkeep);
    }

    /// <summary>
    /// Whole days until <paramref name="balance"/> reaches zero at this net, rounded up; null when
    /// the balance is not shrinking or is already gone (desertion is then today's problem, not a
    /// countdown).
    /// </summary>
    public int? DaysUntilDepleted(float balance)
    {
        if (!(Net < 0f) || !(balance > 0f)) return null;

        // A net below the balance's float resolution never moves the stored value (the tick adds the
        // same two floats), and dividing by it gives a day count past int.MaxValue that the unchecked
        // cast turned into "Depleted in -2147483648 days". Shipped data reaches it: a Dale player with
        // one town (+0.7) against 0.2 + 0.2 + 0.3 of upkeep nets about -6e-8 (Codex, review 95, F2).
        if (!(balance + Net < balance)) return null;

        var days = Math.Ceiling(balance / (double)-Net);
        if (!(days <= int.MaxValue)) return null;
        return (int)days;
    }
}
