namespace TAOM.Features.Music;

public interface IMusicSettingsProvider
{
    MusicSettingsSnapshot GetSnapshot();
}
