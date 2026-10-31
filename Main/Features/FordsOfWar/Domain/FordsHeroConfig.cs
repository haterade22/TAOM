namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>Hero limits, respawn timing and buyback pricing; both scale with the match minute.</summary>
internal sealed record FordsHeroConfig(int MaxAlivePerSide, float RespawnBaseSeconds,
    float RespawnPerMinuteSeconds, float BuybackBaseFraction, float BuybackPerMinuteFraction);
