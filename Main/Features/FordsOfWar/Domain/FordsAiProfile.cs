namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>One AI difficulty: how often it decides, how much passive income it gets, how often it fumbles, and the gold it keeps back from heroes and spare upgrades.</summary>
internal sealed record FordsAiProfile(string Id, float DecisionIntervalSeconds, float IncomeMultiplier,
    float MistakeChance, int HeroReserveGold);
