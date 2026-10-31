namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// One capture point as the UI and AI see it. Progress (0..1) belongs to Capturer, the side working to take the point
/// from its Owner; it is 0 when no one is capturing. Rising is true when the last step raised the progress; a Capturer with
/// Rising false and Contested false is a capture that is decaying (nobody, or the owner, stands on the point).
/// </summary>
internal sealed record CapturePointState(string PointId, MatchSide? Owner, MatchSide? Capturer, float Progress, bool Contested, bool Rising);
