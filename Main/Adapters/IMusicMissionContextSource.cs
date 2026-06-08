using TAOM.Features.Music;

namespace TAOM.Adapters;

public interface IMusicMissionContextSource
{
    MusicMissionContextState Capture(string fallbackCultureId);
}
