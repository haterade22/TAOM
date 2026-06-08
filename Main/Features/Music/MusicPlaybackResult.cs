namespace TAOM.Features.Music;

public sealed class MusicPlaybackResult
{
    private MusicPlaybackResult(
        MusicPlaybackOutcome outcome,
        MusicTrackDefinition track,
        MusicRouteDecision decision,
        int channel,
        string reason)
    {
        Outcome = outcome;
        Track = track;
        Decision = decision;
        Channel = channel;
        Reason = reason ?? string.Empty;
    }

    public MusicPlaybackOutcome Outcome { get; }

    public MusicTrackDefinition Track { get; }

    public MusicRouteDecision Decision { get; }

    public int Channel { get; }

    public string Reason { get; }

    public static MusicPlaybackResult None(string reason)
    {
        return new MusicPlaybackResult(MusicPlaybackOutcome.None, null, null, -1, reason);
    }

    public static MusicPlaybackResult Continued(MusicTrackDefinition track, MusicRouteDecision decision, int channel)
    {
        return new MusicPlaybackResult(MusicPlaybackOutcome.Continued, track, decision, channel, "continued");
    }

    public static MusicPlaybackResult Started(MusicTrackDefinition track, MusicRouteDecision decision, int channel)
    {
        return new MusicPlaybackResult(MusicPlaybackOutcome.Started, track, decision, channel, "started");
    }

    public static MusicPlaybackResult Stopped(string reason)
    {
        return new MusicPlaybackResult(MusicPlaybackOutcome.Stopped, null, null, -1, reason);
    }

    public static MusicPlaybackResult Failed(string reason, MusicTrackDefinition track = null, MusicRouteDecision decision = null, int channel = -1)
    {
        return new MusicPlaybackResult(MusicPlaybackOutcome.Failed, track, decision, channel, reason);
    }
}
