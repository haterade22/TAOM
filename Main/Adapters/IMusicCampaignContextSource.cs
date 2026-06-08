using TAOM.Features.Music;

namespace TAOM.Adapters;

public interface IMusicCampaignContextSource
{
    MusicCampaignContextState Capture();
}
