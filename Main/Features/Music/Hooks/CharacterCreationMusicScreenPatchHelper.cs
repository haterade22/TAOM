using System;

namespace TAOM.Features.Music.Hooks;

internal static class CharacterCreationMusicScreenPatchHelper
{
    private static ICharacterCreationMusicContextService _contextOverride;
    private static IMusicPlaybackService _playbackOverride;
    private static Func<object, bool> _ambientSuppressorOverride;
    private static bool _screenActive;
    private static bool _taomCharacterCreationTrackObserved;
    private static bool _vanillaAmbientSuppressed;
    private static float _timerSeconds;

    internal static void InitializeForTests(
        ICharacterCreationMusicContextService context,
        IMusicPlaybackService playback)
    {
        InitializeForTests(context, playback, null);
    }

    internal static void InitializeForTests(
        ICharacterCreationMusicContextService context,
        IMusicPlaybackService playback,
        Func<object, bool> ambientSuppressor)
    {
        _contextOverride = context;
        _playbackOverride = playback;
        _ambientSuppressorOverride = ambientSuppressor;
        _screenActive = false;
        _taomCharacterCreationTrackObserved = false;
        _vanillaAmbientSuppressed = false;
        _timerSeconds = 0f;
    }

    internal static void ResetForTests()
    {
        _contextOverride = null;
        _playbackOverride = null;
        _ambientSuppressorOverride = null;
        _screenActive = false;
        _taomCharacterCreationTrackObserved = false;
        _vanillaAmbientSuppressed = false;
        _timerSeconds = 0f;
    }

    internal static MusicPlaybackResult OnFrameTick(float dt)
    {
        return OnFrameTick(null, dt);
    }

    internal static MusicPlaybackResult OnFrameTick(object screen, float dt)
    {
        try
        {
            var context = ResolveContext();
            var playback = ResolvePlayback();
            if (context == null || playback == null)
                return MusicPlaybackResult.None("character_creation_music_unavailable");

            if (!_screenActive)
            {
                _screenActive = true;
                _timerSeconds = 0f;
                context.EnterCharacterCreation();
            }

            _timerSeconds += SanitizeDelta(dt);
            var snapshot = context.CaptureSnapshot();
            var result = playback.Update(MusicRouteSnapshot.Empty, snapshot, _timerSeconds);
            TrySuppressVanillaAmbient(screen, result);
            return result;
        }
        catch
        {
            return MusicPlaybackResult.Failed("character_creation_music_hook_failed");
        }
    }

    internal static MusicPlaybackResult OnFinalize()
    {
        try
        {
            ResolveContext()?.ExitCharacterCreation("character_creation_screen_finalized");
            return ResolvePlayback()?.Stop("character_creation_screen_finalized")
                   ?? MusicPlaybackResult.None("character_creation_music_unavailable");
        }
        catch
        {
            return MusicPlaybackResult.Failed("character_creation_music_finalize_failed");
        }
        finally
        {
            _screenActive = false;
            _taomCharacterCreationTrackObserved = false;
            _vanillaAmbientSuppressed = false;
            _timerSeconds = 0f;
        }
    }

    private static ICharacterCreationMusicContextService ResolveContext()
    {
        return _contextOverride ?? IoC.Resolve<ICharacterCreationMusicContextService>();
    }

    private static IMusicPlaybackService ResolvePlayback()
    {
        return _playbackOverride ?? IoC.Resolve<IMusicPlaybackService>();
    }

    private static void TrySuppressVanillaAmbient(object screen, MusicPlaybackResult result)
    {
        if (!ShouldSuppressVanillaAmbient(result))
            return;

        if (!_taomCharacterCreationTrackObserved)
        {
            _taomCharacterCreationTrackObserved = true;
            CharacterCreationMusicSmokeTrace.CharacterCreationBucketOwned(result);
        }

        if (_vanillaAmbientSuppressed)
            return;

        var suppressor = _ambientSuppressorOverride ?? CharacterCreationAmbientSuppressor.Suppress;
        if (!suppressor(screen))
            return;

        _vanillaAmbientSuppressed = true;
        CharacterCreationMusicSmokeTrace.VanillaAmbientSuppressed(result);
    }

    private static bool ShouldSuppressVanillaAmbient(MusicPlaybackResult result)
    {
        if (result == null || result.Decision?.Bucket != MusicBucket.CharacterCreation)
            return false;

        return result.Outcome == MusicPlaybackOutcome.Started
               || result.Outcome == MusicPlaybackOutcome.Continued;
    }

    private static float SanitizeDelta(float dt)
    {
        if (float.IsNaN(dt) || float.IsInfinity(dt) || dt < 0f)
            return 0f;

        return dt;
    }
}
