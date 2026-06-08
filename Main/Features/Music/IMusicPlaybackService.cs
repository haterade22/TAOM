namespace TAOM.Features.Music;

public interface IMusicPlaybackService
{
    MusicPlaybackResult Update(
        MusicRouteSnapshot missionSnapshot,
        MusicRouteSnapshot campaignSnapshot,
        float timerSeconds);

    bool IsActiveBucket(MusicBucket bucket);

    MusicPlaybackResult Stop(string reason = null);
}
