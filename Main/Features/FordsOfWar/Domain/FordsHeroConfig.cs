namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>Hero limits, respawn timing and buyback pricing; both scale with the match minute. FreeArrivalSeconds is how often each side's next hero comes for nothing; 0 turns that off. DamageFactor and DamageTakenFactor multiply a hero's blows and the blows it takes.</summary>
internal sealed record FordsHeroConfig(int MaxAlivePerSide, float RespawnBaseSeconds,
    float RespawnPerMinuteSeconds, float BuybackBaseFraction, float BuybackPerMinuteFraction, float FreeArrivalSeconds, float DamageFactor, float DamageTakenFactor);
