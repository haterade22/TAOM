namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>One AI difficulty: how often it decides, how much passive income it gets, how often it fumbles.</summary>
internal sealed record FordsAiProfile(string Id, float DecisionIntervalSeconds, float IncomeMultiplier,
    float MistakeChance, int HeroReserveGold);
