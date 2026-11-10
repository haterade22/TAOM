using System;
using System.Collections.Generic;
using TAOM.Features.Execution;

namespace TAOM.Features.WarPledge;

/// <summary>One row of the lean table: a Neutral realm and the side it leans to (Neutral for a true neutral).</summary>
public sealed class WarPledgeLean
{
    public WarPledgeLean(string kingdomId, FactionSide lean)
    {
        KingdomId = kingdomId;
        Lean = lean;
    }

    public string KingdomId { get; }

    public FactionSide Lean { get; }
}

/// <summary>
/// The validated content of <c>diplomacy/war_pledge.json</c> (#1113). <see cref="WarPledgeConfigProvider"/>
/// is the only builder, so every value here is inside its range and every lean row names one realm once.
/// </summary>
public sealed class WarPledgeConfig
{
    public const int DefaultLosingBalance = 25;
    public const int MinLosingBalance = 1;
    public const int MaxLosingBalance = 100;

    public const int DefaultDaysBetweenAiJoins = 30;
    public const int MinDaysBetweenAiJoins = 1;
    public const int MaxDaysBetweenAiJoins = 3650;

    public WarPledgeConfig(int losingBalance, int daysBetweenAiJoins, IReadOnlyList<WarPledgeLean> leans)
    {
        LosingBalance = losingBalance;
        DaysBetweenAiJoins = daysBetweenAiJoins;
        Leans = leans ?? Array.Empty<WarPledgeLean>();
    }

    /// <summary>The compiled defaults with an empty lean table: the AI courts no realm.</summary>
    public static WarPledgeConfig Defaults { get; } =
        new WarPledgeConfig(DefaultLosingBalance, DefaultDaysBetweenAiJoins, Array.Empty<WarPledgeLean>());

    /// <summary>
    /// A side is losing when the momentum balance (the map meter, -100 to +100, positive for the Free
    /// Peoples) stands this far against it.
    /// </summary>
    public int LosingBalance { get; }

    /// <summary>Whole days between two AI joins, and from the first Full War day to the first one.</summary>
    public int DaysBetweenAiJoins { get; }

    /// <summary>The lean rows in file order, which is the order a side courts its realms in.</summary>
    public IReadOnlyList<WarPledgeLean> Leans { get; }
}
