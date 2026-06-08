using TAOM.Features.Music;

namespace TAOM.Adapters;

public interface IMusicMissionContextAdapter
{
    MusicRouteSnapshot CaptureSnapshot(string fallbackCultureId = null);
}
