using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Adapters;

/// <summary>One live Fords unit as the battlefield adapter measured it this tick: its opaque key, its side and lane, where it stands.</summary>
internal readonly record struct FordsUnitSample(AgentKey Key, MatchSide Side, int Lane, Vec3f Position);
