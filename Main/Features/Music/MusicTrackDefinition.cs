namespace TAOM.Features.Music;

public sealed class MusicTrackDefinition
{
    public MusicTrackDefinition(
        MusicBucket bucket,
        string cultureId,
        string eventName,
        string relativePath,
        string absolutePath)
    {
        Bucket = bucket;
        CultureId = cultureId ?? string.Empty;
        EventName = eventName ?? string.Empty;
        RelativePath = relativePath ?? string.Empty;
        AbsolutePath = absolutePath ?? string.Empty;
    }

    public MusicBucket Bucket { get; }

    public string CultureId { get; }

    public string EventName { get; }

    public string RelativePath { get; }

    public string AbsolutePath { get; }
}
