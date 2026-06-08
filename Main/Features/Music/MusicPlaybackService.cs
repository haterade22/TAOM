using System;
using System.Collections.Generic;
using TAOM.Adapters;

namespace TAOM.Features.Music;

public sealed class MusicPlaybackService : IMusicPlaybackService
{
    private const int NoChannel = -1;
    private const float StartPlaybackGraceSeconds = 3f;
    private const float NoFreeChannelRetryBackoffSeconds = 1f;
    private const float LostOwnedChannelBackoffSeconds = 1f;

    private readonly IMusicEngineAdapter _engine;
    private readonly IMusicSettingsProvider _settingsProvider;
    private readonly MusicTrackIndex _trackIndex;
    private readonly MusicTransitionResolver _resolver;
    private readonly NoRepeatShufflePicker _picker;

    private int _activeChannel = NoChannel;
    private bool _activeClipLoaded;
    private string _activeRouteKey;
    private MusicTrackDefinition _activeTrack;
    private MusicRouteDecision _activeDecision;
    private float _nextRotateAt;
    private float _activeStartedAt = float.MinValue;
    private string _noFreeChannelRetryRouteKey;
    private float _noFreeChannelRetryAt = float.MinValue;
    private string _lostOwnedChannelRouteKey;
    private float _lostOwnedChannelRetryAt = float.MinValue;

    public MusicPlaybackService(
        IMusicEngineAdapter engine,
        IMusicSettingsProvider settingsProvider,
        MusicTrackIndex trackIndex,
        MusicTransitionResolver resolver,
        NoRepeatShufflePicker picker)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
        _trackIndex = trackIndex ?? throw new ArgumentNullException(nameof(trackIndex));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _picker = picker ?? throw new ArgumentNullException(nameof(picker));
    }

    public MusicPlaybackResult Update(
        MusicRouteSnapshot missionSnapshot,
        MusicRouteSnapshot campaignSnapshot,
        float timerSeconds)
    {
        var settings = _settingsProvider.GetSnapshot() ?? MusicSettingsSnapshot.Default;
        if (!settings.MusicEnabled)
            return Stop("music_disabled");

        var effectiveMission = settings.MissionContextEnabled ? missionSnapshot : MusicRouteSnapshot.Empty;
        var effectiveCampaign = settings.CampaignContextEnabled ? campaignSnapshot : MusicRouteSnapshot.Empty;

        var decision = _resolver.Resolve(
            _trackIndex,
            settings.RouteSettings,
            effectiveMission,
            effectiveCampaign);
        if (!decision.HasSelection)
            return Stop(decision.Reason);

        var routeKey = BuildRouteKey(decision);
        if (IsWaitingForFreeChannelRetry(routeKey, timerSeconds))
            return MusicPlaybackResult.None("waiting_for_free_music_channel");

        var routeChanged = !string.Equals(_activeRouteKey, routeKey, StringComparison.OrdinalIgnoreCase);
        if (routeChanged)
        {
            _nextRotateAt = 0f;
            ClearLostOwnedChannelBackoff();
        }

        var rotation = MusicRotationPolicy.ShouldRotate(
            settings.Rotation,
            decision.Bucket,
            timerSeconds,
            _nextRotateAt);
        _nextRotateAt = rotation.NextRotateAt;

        if (_activeTrack != null && !routeChanged && !rotation.Rotate)
        {
            if (IsActiveChannelStillPlaying())
            {
                return MusicPlaybackResult.Continued(_activeTrack, _activeDecision, _activeChannel);
            }

            if (IsWithinStartGrace(timerSeconds))
            {
                return MusicPlaybackResult.Continued(_activeTrack, _activeDecision, _activeChannel);
            }

            if (ShouldBackOffForLostOwnedChannel(decision.Source, routeKey, timerSeconds))
                return MusicPlaybackResult.None("waiting_for_lost_owned_music_channel");

            StopActiveChannel();
            ClearActiveState();
        }

        return StartResolvedTrack(decision, routeKey, settings, timerSeconds);
    }

    public MusicPlaybackResult Stop(string reason = null)
    {
        var hadActiveTrack = _activeTrack != null || _activeChannel >= 0;
        StopActiveChannel();
        ClearActiveState();

        return hadActiveTrack
            ? MusicPlaybackResult.Stopped(reason ?? "stopped")
            : MusicPlaybackResult.None(reason ?? "nothing_active");
    }

    public bool IsActiveBucket(MusicBucket bucket)
    {
        return _activeTrack != null
            && _activeDecision != null
            && _activeDecision.Bucket == bucket;
    }

    private MusicPlaybackResult StartResolvedTrack(
        MusicRouteDecision decision,
        string routeKey,
        MusicSettingsSnapshot settings,
        float timerSeconds)
    {
        var tracks = decision.Pool?.Tracks;
        var attemptLimit = tracks?.Count ?? 0;
        if (attemptLimit <= 0)
            return Stop("no_candidate_track");

        var attempted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var volume = settings.GetBucketVolume(decision.Bucket);
        MusicPlaybackResult lastFailure = null;

        for (var i = 0; i < attemptLimit; i++)
        {
            var track = _picker.Pick(
                routeKey,
                tracks,
                settings.UseNoRepeatShuffle,
                settings.NoRepeatHistorySize);
            if (track == null)
                break;

            if (!attempted.Add(track.EventName))
                continue;

            var result = StartTrack(track, decision, routeKey, volume, timerSeconds);
            if (result.Outcome == MusicPlaybackOutcome.Started)
                return result;

            if (result.Outcome != MusicPlaybackOutcome.Failed || !CanRecoverFromStartFailure(result))
                return result;

            lastFailure = result;
        }

        return lastFailure ?? Stop("no_candidate_track");
    }

    private static bool CanRecoverFromStartFailure(MusicPlaybackResult result)
    {
        if (result == null)
            return false;

        return string.Equals(result.Reason, "missing_clip_path", StringComparison.OrdinalIgnoreCase)
            || string.Equals(result.Reason, "clip_not_loaded", StringComparison.OrdinalIgnoreCase)
            || string.Equals(result.Reason, "engine_music_start_failed", StringComparison.OrdinalIgnoreCase);
    }

    private MusicPlaybackResult StartTrack(
        MusicTrackDefinition track,
        MusicRouteDecision decision,
        string routeKey,
        float volume,
        float timerSeconds)
    {
        StopActiveChannel();
        ClearActivePlaybackState();

        var channel = _engine.GetFreeMusicChannelIndex();
        if (channel < 0)
        {
            // All channels occupied — evict vanilla music. After ClearActivePlaybackState()
            // TAOM owns no channel, so anything on channels 0..3 is vanilla.
            for (var evict = 0; evict < 4 && channel < 0; evict++)
            {
                TryStop(evict);
                TryUnload(evict);
                channel = _engine.GetFreeMusicChannelIndex();
            }
        }

        if (channel < 0)
        {
            SetNoFreeChannelRetryBackoff(routeKey, timerSeconds);
            return MusicPlaybackResult.Failed("no_free_music_channel", track, decision);
        }

        try
        {
            ClearNoFreeChannelRetryBackoff();

            var path = (track.AbsolutePath ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(path))
                return MusicPlaybackResult.Failed("missing_clip_path", track, decision, channel);

            _engine.LoadClip(channel, path);
            if (!_engine.IsClipLoaded(channel))
            {
                var forwardSlashPath = path.Replace('\\', '/');
                if (!string.Equals(forwardSlashPath, path, StringComparison.Ordinal))
                {
                    TryUnload(channel);
                    _engine.LoadClip(channel, forwardSlashPath);
                }
            }

            if (!_engine.IsClipLoaded(channel))
            {
                TryUnload(channel);
                return MusicPlaybackResult.Failed("clip_not_loaded", track, decision, channel);
            }

            _engine.SetVolume(channel, Clamp01(volume));
            _engine.PlayMusic(channel);

            _activeChannel = channel;
            _activeClipLoaded = true;
            _activeRouteKey = routeKey;
            _activeTrack = track;
            _activeDecision = decision;
            _activeStartedAt = timerSeconds;

            return MusicPlaybackResult.Started(track, decision, channel);
        }
        catch
        {
            TryStop(channel);
            TryUnload(channel);
            return MusicPlaybackResult.Failed("engine_music_start_failed", track, decision, channel);
        }
    }

    private void StopActiveChannel()
    {
        if (_activeChannel < 0)
            return;

        TryStop(_activeChannel);

        if (_activeClipLoaded)
            TryUnload(_activeChannel);
    }

    private void ClearActivePlaybackState()
    {
        _activeChannel = NoChannel;
        _activeClipLoaded = false;
        _activeRouteKey = null;
        _activeTrack = null;
        _activeDecision = null;
        _activeStartedAt = float.MinValue;
    }

    private void ClearActiveState()
    {
        ClearActivePlaybackState();
        ClearNoFreeChannelRetryBackoff();
        ClearLostOwnedChannelBackoff();
        _nextRotateAt = 0f;
    }

    private void TryStop(int channel)
    {
        try
        {
            _engine.StopMusic(channel);
        }
        catch
        {
        }
    }

    private void TryUnload(int channel)
    {
        try
        {
            _engine.UnloadClip(channel);
        }
        catch
        {
        }
    }

    private bool IsActiveChannelStillPlaying()
    {
        if (_activeChannel < 0)
            return false;

        try
        {
            return _engine.IsMusicPlaying(_activeChannel);
        }
        catch
        {
            return true;
        }
    }

    private bool IsWithinStartGrace(float timerSeconds)
    {
        var elapsed = timerSeconds - _activeStartedAt;
        return elapsed >= 0f && elapsed < StartPlaybackGraceSeconds;
    }

    private bool IsWaitingForFreeChannelRetry(string routeKey, float timerSeconds)
    {
        return !string.IsNullOrWhiteSpace(_noFreeChannelRetryRouteKey)
            && string.Equals(_noFreeChannelRetryRouteKey, routeKey, StringComparison.OrdinalIgnoreCase)
            && timerSeconds < _noFreeChannelRetryAt;
    }

    private void SetNoFreeChannelRetryBackoff(string routeKey, float timerSeconds)
    {
        _noFreeChannelRetryRouteKey = routeKey;
        _noFreeChannelRetryAt = timerSeconds + NoFreeChannelRetryBackoffSeconds;
    }

    private void ClearNoFreeChannelRetryBackoff()
    {
        _noFreeChannelRetryRouteKey = null;
        _noFreeChannelRetryAt = float.MinValue;
    }

    private bool ShouldBackOffForLostOwnedChannel(MusicRouteSource source, string routeKey, float timerSeconds)
    {
        // Only back off for campaign-context music (world map). Mission music (battle/siege) restarts immediately.
        if (source != MusicRouteSource.Campaign)
            return false;

        if (string.IsNullOrWhiteSpace(_lostOwnedChannelRouteKey)
            || !string.Equals(_lostOwnedChannelRouteKey, routeKey, StringComparison.OrdinalIgnoreCase))
        {
            _lostOwnedChannelRouteKey = routeKey;
            _lostOwnedChannelRetryAt = timerSeconds + LostOwnedChannelBackoffSeconds;
            return true;
        }

        if (timerSeconds < _lostOwnedChannelRetryAt)
            return true;

        ClearLostOwnedChannelBackoff();
        return false;
    }

    private void ClearLostOwnedChannelBackoff()
    {
        _lostOwnedChannelRouteKey = null;
        _lostOwnedChannelRetryAt = float.MinValue;
    }

    private static string BuildRouteKey(MusicRouteDecision decision)
    {
        return $"{decision.Source}:{decision.Bucket}:{decision.Pool.ResolvedCultureId}";
    }

    private static float Clamp01(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return 0f;
        if (value < 0f)
            return 0f;
        return value > 1f ? 1f : value;
    }
}
