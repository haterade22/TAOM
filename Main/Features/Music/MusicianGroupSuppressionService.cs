using System;

namespace TAOM.Features.Music;

public sealed class MusicianGroupSuppressionService : IMusicianGroupSuppressionService
{
    private readonly IMusicSettingsProvider _settingsProvider;
    private readonly IMusicPlaybackService _playbackService;

    public MusicianGroupSuppressionService(
        IMusicSettingsProvider settingsProvider,
        IMusicPlaybackService playbackService)
    {
        _settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
        _playbackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));
    }

    public bool ShouldSuppressVanillaTavernMusic()
    {
        var settings = _settingsProvider.GetSnapshot() ?? MusicSettingsSnapshot.Default;
        if (!settings.MusicEnabled)
            return false;
        if (!settings.RouteSettings.IsBucketEnabled(MusicBucket.Tavern))
            return false;

        return _playbackService.IsActiveBucket(MusicBucket.Tavern);
    }
}
