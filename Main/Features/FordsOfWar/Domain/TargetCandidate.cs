namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>An enemy a tower could shoot, with its distance to the tower.</summary>
internal sealed record TargetCandidate(AgentKey Key, float Distance, bool IsHero);
