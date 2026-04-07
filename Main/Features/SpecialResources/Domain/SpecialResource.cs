namespace TAOM.Features.SpecialResources.Domain;

public sealed class SpecialResource
{
    public string Id { get; }
    public string KingdomId { get; }
    public string DisplayName { get; }
    public string IconSpriteName { get; }
    public float Cap { get; }
    public float StartingAmount { get; }
    public float DailyPerTown { get; }
    public float PerBattleVictoryBase { get; }
    public float PerRaid { get; }
    public float PerSiegeVictory { get; }
    public float PerPrisoner { get; }

    public SpecialResource(
        string id,
        string kingdomId,
        string displayName,
        string iconSpriteName,
        float cap,
        float startingAmount,
        float dailyPerTown,
        float perBattleVictoryBase,
        float perRaid,
        float perSiegeVictory,
        float perPrisoner)
    {
        Id = id;
        KingdomId = kingdomId;
        DisplayName = displayName;
        IconSpriteName = iconSpriteName;
        Cap = cap;
        StartingAmount = startingAmount;
        DailyPerTown = dailyPerTown;
        PerBattleVictoryBase = perBattleVictoryBase;
        PerRaid = perRaid;
        PerSiegeVictory = perSiegeVictory;
        PerPrisoner = perPrisoner;
    }
}
