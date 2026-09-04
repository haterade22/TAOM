using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.MountDespawn;

public class DeadMountDespawnService : IDeadMountDespawnService
{
    /// <summary>
    /// Corpses retired per sweep. A cavalry line breaking can kill thirty mounts inside a second,
    /// and thirty fades in one frame is its own hitch — exactly the stutter this feature exists to
    /// remove. The remainder comes back on the next sweep half a second later.
    /// </summary>
    public const int MaxFadesPerSweep = 8;

    /// <summary>Below this the corpse pops while the death animation is still playing.</summary>
    public const float MinDelaySeconds = 3f;
    public const float MaxDelaySeconds = 30f;
    public const float DefaultDelaySeconds = 5f;

    private readonly IMountDespawnSettingsProvider _settings;
    private readonly IModLogger _logger;

    // index -> mission time at death.
    private readonly Dictionary<int, float> _deathTimes = new();

    // Reused so a sweep allocates nothing. The caller copies it before iterating, so no cross-file
    // invariant rides on nothing else touching it.
    private readonly List<int> _dueBuffer = new();

    // The sweep runs twice a second, so a bad delay would otherwise repeat the same line forever.
    private bool _loggedInvalidDelay;

    public DeadMountDespawnService(IMountDespawnSettingsProvider settings, IModLogger logger)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool IsEnabled => _settings.IsEnabled;

    public int PendingCount => _deathTimes.Count;

    public void OnMountKilled(int agentIndex, float missionTime)
    {
        if (!_settings.IsEnabled) return;
        if (!FiniteFloatValidator.IsFinite(missionTime)) return;

        _deathTimes[agentIndex] = missionTime;
    }

    public void Forget(int agentIndex) => _deathTimes.Remove(agentIndex);

    public IReadOnlyList<int> CollectDue(float missionTime)
    {
        _dueBuffer.Clear();

        if (!_settings.IsEnabled) return _dueBuffer;
        if (!FiniteFloatValidator.IsFinite(missionTime)) return _dueBuffer;

        var delay = ResolveDelaySeconds();

        foreach (var scheduled in _deathTimes)
        {
            if (_dueBuffer.Count >= MaxFadesPerSweep) break;

            // Positive requirement, not `elapsed < delay -> continue`. Every NaN comparison returns
            // false, so an inverted early-exit would let a poisoned time through into the fade.
            if (!(missionTime - scheduled.Value >= delay)) continue;

            _dueBuffer.Add(scheduled.Key);
        }

        for (var i = 0; i < _dueBuffer.Count; i++)
            _deathTimes.Remove(_dueBuffer[i]);

        return _dueBuffer;
    }

    public void OnMissionEnd()
    {
        // The service is Reuse.Singleton, so it outlives the mission. Mission time restarts near
        // zero in the next battle, and agent indices are reused, so a surviving entry would schedule
        // a fade against a completely different agent.
        _deathTimes.Clear();
        _dueBuffer.Clear();
        _loggedInvalidDelay = false;
    }

    private float ResolveDelaySeconds()
    {
        var raw = _settings.DespawnDelaySeconds;
        if (FiniteFloatValidator.IsFiniteInRange(raw, MinDelaySeconds, MaxDelaySeconds))
            return raw;

        // Never fall back silently: the MCM slider cannot produce this, but a hand-edited json2 can,
        // and a player who set 100 would otherwise see 5 with nothing anywhere saying why.
        if (!_loggedInvalidDelay)
        {
            _loggedInvalidDelay = true;
            _logger.LogWarning(
                $"[MountDespawn] despawn delay '{raw}' is not a finite value within " +
                $"[{MinDelaySeconds}, {MaxDelaySeconds}]; using {DefaultDelaySeconds}s instead. " +
                "Check the Dead Mount Cleanup setting in MCM.");
        }

        return DefaultDelaySeconds;
    }
}
