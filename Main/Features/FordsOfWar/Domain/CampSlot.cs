namespace TAOM.Features.FordsOfWar.Domain;

internal sealed record CampSlot(string Id, Vec3f Position, string TroopId, int Count, int Reward);
