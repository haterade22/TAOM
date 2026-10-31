namespace TAOM.Features.FordsOfWar.Diagnostics;

/// <summary>The volley item as the engine resolved it: thrust damage N, launch speed S and the reach that speed gives.</summary>
internal sealed record FordsVolleyItemInfo(string ItemId, int Thrust, float Speed, float Reach);

/// <summary>One volley missile that struck an agent, with the numbers the engine used.</summary>
internal sealed record FordsVolleyHit(float BaseMagnitude, int Inflicted, float AbsorbedByArmor, float MissileTotalDamage, float LaunchSpeed,
    float ImpactSpeed, float Multiplier, string BodyPart, bool Shielded, bool Mounted, bool Human);
