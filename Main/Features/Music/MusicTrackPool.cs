using System.Collections.Generic;

namespace TAOM.Features.Music;

public sealed class MusicTrackPool
{
    public MusicTrackPool(
        MusicBucket bucket,
        string requestedCultureId,
        string resolvedCultureId,
        IReadOnlyList<MusicTrackDefinition> tracks)
    {
        Bucket = bucket;
        RequestedCultureId = requestedCultureId ?? string.Empty;
        ResolvedCultureId = resolvedCultureId ?? string.Empty;
        Tracks = tracks ?? new List<MusicTrackDefinition>();
    }

    public MusicBucket Bucket { get; }

    public string RequestedCultureId { get; }

    public string ResolvedCultureId { get; }

    public IReadOnlyList<MusicTrackDefinition> Tracks { get; }

    public bool HasTracks => Tracks.Count > 0;

    public bool UsedNeutralFallback =>
        HasTracks && !string.Equals(RequestedCultureId, ResolvedCultureId, System.StringComparison.OrdinalIgnoreCase);
}
