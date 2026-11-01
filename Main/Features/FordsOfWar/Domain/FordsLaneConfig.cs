namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// Lane order cooldown and the distances the lane march rules compare against. LeadBand is how many metres of lane behind its
/// most advanced unit a formation's lead group reaches.
/// </summary>
internal sealed record FordsLaneConfig(float OrderCooldownSeconds, float EngageRange, float WaypointReachRadius,
    float HoldDistanceBeforeTower, int AssaultThreshold, float LeadBand);
