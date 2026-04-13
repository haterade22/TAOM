using System.Collections.Generic;

namespace TAOM.Features.SpecialResources.Domain;

public sealed class SpecialResource
{
    public string Id { get; }
    public IReadOnlyList<string> KingdomIds { get; }
    public IReadOnlyList<string> CultureIds { get; }
    public string DisplayName { get; }
    public string IconSpriteName { get; }
    public float Cap { get; }
    public float StartingAmount { get; }
    public float DailyPerTown { get; }
    public float PerBattleVictoryBase { get; }
    public float PerRaid { get; }
    public float PerSiegeVictory { get; }
    public float PerPrisoner { get; }
    public float PerTournamentWin { get; }
    public float PerHideoutClear { get; }

    /// <summary>
    /// Ordered list of tier milestones (ascending by <see cref="ResourceTier.Threshold"/>).
    /// Empty for resources that have no tier system.
    /// </summary>
    public IReadOnlyList<ResourceTier> TierThresholds { get; }

    public SpecialResource(
        string id,
        IReadOnlyList<string> kingdomIds,
        IReadOnlyList<string> cultureIds,
        string displayName,
        string iconSpriteName,
        float cap,
        float startingAmount,
        float dailyPerTown,
        float perBattleVictoryBase,
        float perRaid,
        float perSiegeVictory,
        float perPrisoner,
        float perTournamentWin = 0f,
        float perHideoutClear = 0f,
        IReadOnlyList<ResourceTier> tierThresholds = null)
    {
        Id = id;
        KingdomIds = kingdomIds;
        CultureIds = cultureIds;
        DisplayName = displayName;
        IconSpriteName = iconSpriteName;
        Cap = cap;
        StartingAmount = startingAmount;
        DailyPerTown = dailyPerTown;
        PerBattleVictoryBase = perBattleVictoryBase;
        PerRaid = perRaid;
        PerSiegeVictory = perSiegeVictory;
        PerPrisoner = perPrisoner;
        PerTournamentWin = perTournamentWin;
        PerHideoutClear = perHideoutClear;
        TierThresholds = tierThresholds ?? new List<ResourceTier>();
    }
}
