namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>A capture point changed owner.</summary>
internal sealed record CaptureChange(string PointId, MatchSide NewOwner);
