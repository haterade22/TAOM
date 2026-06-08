namespace TAOM.Features.Music;

public sealed class MusicRouteDecision
{
    private MusicRouteDecision(
        bool hasSelection,
        MusicRouteSource source,
        MusicBucket bucket,
        MusicTrackPool pool,
        string reason)
    {
        HasSelection = hasSelection;
        Source = source;
        Bucket = bucket;
        Pool = pool;
        Reason = reason ?? string.Empty;
    }

    public bool HasSelection { get; }

    public MusicRouteSource Source { get; }

    public MusicBucket Bucket { get; }

    public MusicTrackPool Pool { get; }

    public string Reason { get; }

    public static MusicRouteDecision None(string reason)
    {
        return new MusicRouteDecision(false, MusicRouteSource.None, MusicBucket.World, null, reason);
    }

    public static MusicRouteDecision Selected(
        MusicRouteSource source,
        MusicBucket bucket,
        MusicTrackPool pool,
        string reason)
    {
        return new MusicRouteDecision(true, source, bucket, pool, reason);
    }
}
