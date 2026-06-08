namespace TAOM.Features.Music;

public class MusicSettingsProvider : IMusicSettingsProvider
{
    public MusicSettingsSnapshot GetSnapshot()
    {
        return MusicSettingsSnapshot.Default;
    }
}
