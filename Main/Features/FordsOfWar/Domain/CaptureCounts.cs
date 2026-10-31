namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>How many live units of each side stand inside a capture point's radius this tick.</summary>
internal sealed record CaptureCounts(string PointId, int West, int East);
