namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>Lane order cooldown and the distances the lane march rules compare against.</summary>
internal sealed record FordsLaneConfig(float OrderCooldownSeconds, float EngageRange, float WaypointReachRadius,
    float HoldDistanceBeforeTower, int AssaultThreshold);
