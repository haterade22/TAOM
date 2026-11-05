using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>What one cleared wave cost and earned. <c>Felled</c> is attackers the stalemate rule removed, which pay no bounty. The factors are the health and damage its attackers took (1 inside the authored list).</summary>
internal sealed record WaveStats(int Wave, int Kills, int Felled, int Lost, float Seconds, float HealthFactor = 1f, float DamageFactor = 1f);

/// <summary>Per-wave and total counts for the report and its checks.</summary>
internal sealed class RunStats
{
    private readonly List<WaveStats> _waves = new();
    private int _kills;
    private int _lost;

    public IReadOnlyList<WaveStats> Waves => _waves;

    /// <summary>Kills of the waves closed so far plus the one in progress.</summary>
    public int TotalKills => _waves.Sum(w => w.Kills) + _kills;

    /// <summary>Cleared waves that needed the stalemate rule to fell stragglers.</summary>
    public int WavesFelled => _waves.Count(w => w.Felled > 0);

    /// <summary>Defenders lost in the waves closed so far: the leaderboard's second rank key, so the end panel shows the same number.</summary>
    public int LostInClearedWaves => _waves.Sum(w => w.Lost);

    public int Fled { get; private set; }
    public int FleeingPeak { get; private set; }
    public int PlayerDeaths { get; private set; }

    public void AttackerKilled() => _kills++;

    public void DefenderLost() => _lost++;

    public void AttackerFled() => Fled++;

    public void PlayerDied() => PlayerDeaths++;

    /// <summary>The engine's own fleeing flag fires no callback, so the run also keeps the most attackers seen running at once.</summary>
    public void ObserveFleeing(int fleeingNow) => FleeingPeak = Math.Max(FleeingPeak, fleeingNow);

    public void CloseWave(int wave, float seconds, int felled, float healthFactor = 1f, float damageFactor = 1f)
    {
        _waves.Add(new WaveStats(wave, _kills, felled, _lost, seconds, healthFactor, damageFactor));
        _kills = 0;
        _lost = 0;
    }
}
