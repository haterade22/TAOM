using System;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.StoryMissions;

/// <summary>A story mission's own track (#1105), for one mission; main thread only, because the mission screen's hooks call it.</summary>
internal sealed class StoryMusicCue
{
    internal const string RequestedId = "taom.story-missions.music.requested";
    internal const string StoppedId = "taom.story-missions.music.stopped";

    /// <summary>A play shorter than this ends the loop; it assumes that a file which does not decode ends sooner (UNVERIFIED: the bank sets it).</summary>
    internal const float MinimumPlaySeconds = 10f;

    private const string Feature = "StoryMissions";
    private const string LoopOk = "ok";

    private enum Phase { Idle, Refused, Playing, Ended, Closed }

    private readonly IMissionMusicAdapter _music;
    private readonly string _path;
    private readonly Func<string, bool> _fileExists;
    private readonly IModLogger _logger;
    private readonly IOutcomeLog _outcomes;
    private readonly string _key;
    private Phase _phase;
    private bool _silenced;
    private string _loop = LoopOk;
    private int _restarts;
    private float _playedSeconds;

    public StoryMusicCue(IMissionMusicAdapter music, string path, Func<string, bool> fileExists, IModLogger logger, IOutcomeLog outcomes, string missionKey)
    {
        _music = music;
        _path = path;
        _fileExists = fileExists;
        _logger = logger;
        _outcomes = outcomes;
        _key = missionKey;
    }

    public void Start()
    {
        if (_phase != Phase.Idle)
            return;

        _phase = Phase.Refused;
        if (!_fileExists(_path))
        {
            Refused("missing-file", null);
            return;
        }

        try
        {
            if (!_music.TryPlay(_path))
            {
                Drop();
                Refused("play-refused", null);
                return;
            }
        }
        catch (Exception ex)
        {
            Drop();
            Refused("fault", ex);
            return;
        }

        _phase = Phase.Playing;
        Silence();
        _logger.LogInfo($"[StoryMissions] {_key} music started: {_path} (vanilla music silenced: {_silenced})");
        _outcomes.Pass(RequestedId, _key, $"sound={_path} silenced={_silenced}");
    }

    public void Tick(float dt)
    {
        if (_phase != Phase.Playing)
            return;

        if (dt > 0f && !float.IsInfinity(dt))
            _playedSeconds += dt;

        try
        {
            if (!_music.IsStopped)
                return;

            if (_playedSeconds < MinimumPlaySeconds)
            {
                _logger.LogWarning($"[StoryMissions] {_key} music ended after {_playedSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} s; it is not played again");
                EndLoop("early");
                return;
            }

            // Native releases an event that ends by itself, so Play on the old event fails, or plays nothing before the release: TryPlay makes a new one.
            _playedSeconds = 0f;
            if (_music.TryPlay(_path))
            {
                _restarts++;
                return;
            }

            _logger.LogWarning($"[StoryMissions] {_key} the engine refused to play the music again");
            EndLoop("refused");
        }
        catch (Exception ex)
        {
            _outcomes.Swallowed(Feature, "music-loop", ex);
            EndLoop("fault");
        }
    }

    public void Release(string via)
    {
        if (_phase == Phase.Closed)
            return;

        var played = _phase is Phase.Playing or Phase.Ended;
        _phase = Phase.Closed;
        GiveBack();
        if (played)
            Drop();

        var detail = $"via={via} played={played} restarts={_restarts} loop={_loop}";
        if (_loop == LoopOk)
            _outcomes.Pass(StoppedId, _key, detail);
        else
            _outcomes.Fail(StoppedId, _key, detail);
    }

    private void EndLoop(string why)
    {
        _phase = Phase.Ended;
        _loop = why;
        GiveBack();
    }

    private void GiveBack()
    {
        if (!_silenced)
            return;

        _silenced = false;
        try
        {
            _music.Unsilence();
        }
        catch (Exception ex)
        {
            _outcomes.Swallowed(Feature, "music-unsilence", ex);
        }
    }

    // Stop is the only release the API offers, also for an event the engine created but did not start: native ReleaseEvent only logs.
    private void Drop()
    {
        try
        {
            _music.Stop();
        }
        catch (Exception ex)
        {
            _outcomes.Swallowed(Feature, "music-stop", ex);
        }
    }

    private void Silence()
    {
        _silenced = true;
        try
        {
            _silenced = _music.Silence();
        }
        catch (Exception ex)
        {
            _outcomes.Swallowed(Feature, "music-silence", ex);
        }
    }

    private void Refused(string reason, Exception? ex)
    {
        var error = ex == null ? "" : " error=" + ex.GetType().Name;
        _logger.LogWarning($"[StoryMissions] {_key} music not played ({reason}{(ex == null ? "" : ": " + ex.Message)}): {_path}; the mission keeps its music");
        _outcomes.Fail(RequestedId, _key, $"reason={reason} sound={_path}{error}");
    }
}
