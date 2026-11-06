using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>What one cleared wave cost and earned. <c>Felled</c> is attackers the stalemate rule removed, which pay no bounty. The factors are the health and damage its attackers took (1 inside the authored list), and <c>Affixes</c> the power ids it carried.</summary>
internal sealed record WaveStats(int Wave, int Kills, int Felled, int Lost, float Seconds, float HealthFactor = 1f, float DamageFactor = 1f)
{
    public IReadOnlyList<string> Affixes { get; init; } = Array.Empty<string>();

    /// <summary>Soldiers the wave's commander summoned (queued), how many of them were sent to the field, and how many were killed and felled. Roster counts above stay roster only.</summary>
    public int Summoned { get; init; }

    public int SummonSpawned { get; init; }

    public int SummonKills { get; init; }

    public int SummonFelled { get; init; }

    /// <summary>The wave's commander, or null when it had none.</summary>
    public CommanderRecord? Commander { get; init; }

    /// <summary>What the wave's gate did, and the posts and volleys it saw (the last two at 0 until those slices land).</summary>
    public FortWave Fort { get; init; } = FortWave.None;
}

/// <summary>What the report lists of a wave's commander: who, which rank and how many steps past the ranks, how it ended, how often each ability fired, uses after his end (always 0 unless a clock misbehaves) and the wave seconds he stood.</summary>
internal sealed record CommanderRecord(string TroopId, string RankId, int Steps, CommanderOutcome Outcome, IReadOnlyDictionary<string, int> Uses, int UsesAfterEnd, float AliveSeconds);

/// <summary>The role counts of a wave as the run closes it.</summary>
internal sealed record WaveRoles(int Summoned, int SummonSpawned, int SummonKills, int SummonFelled, CommanderRecord? Commander)
{
    public static WaveRoles None { get; } = new(0, 0, 0, 0, null);
}

/// <summary>
/// What Morgul Blades did, counted over the run: victims newly poisoned (<c>Doses</c>), every accepted hit (<c>Hits</c>, refreshes included), the health the doses took (<c>Damage</c>), the poison ticks whose owner was
/// not active at the call (<c>OwnerMissing</c>, which must stay 0) and the killing ticks withheld because no attacker stood to own the kill (<c>Withheld</c>).
/// </summary>
internal sealed class PoisonTally
{
    public int Doses { get; set; }
    public int Hits { get; set; }
    public int Damage { get; set; }
    public int OwnerMissing { get; set; }
    public int Withheld { get; set; }

    public PoisonTally Copy() => new() { Doses = Doses, Hits = Hits, Damage = Damage, OwnerMissing = OwnerMissing, Withheld = Withheld };

    /// <summary>What was counted after <paramref name="earlier"/>, a copy taken before: one wave's share of the run's tally.</summary>
    public PoisonTally Since(PoisonTally earlier) => new()
    {
        Doses = Doses - earlier.Doses,
        Hits = Hits - earlier.Hits,
        Damage = Damage - earlier.Damage,
        OwnerMissing = OwnerMissing - earlier.OwnerMissing,
        Withheld = Withheld - earlier.Withheld,
    };
}

/// <summary>Per-wave and total counts for the report and its checks.</summary>
internal sealed class RunStats
{
    private readonly List<WaveStats> _waves = new();
    private int _kills;
    private int _lost;

    public IReadOnlyList<WaveStats> Waves => _waves;

    /// <summary>Morgul Blades over the run so far. The run writes it; a check takes the share of one wave with <see cref="PoisonTally.Since"/>.</summary>
    public PoisonTally Poison { get; } = new();

    /// <summary>Kills of the waves closed so far plus the one in progress.</summary>
    public int TotalKills => _waves.Sum(w => w.Kills) + _kills;

    /// <summary>Cleared waves that needed the stalemate rule to fell stragglers: roster soldiers, summoned ones or the commander.</summary>
    public int WavesFelled => _waves.Count(w => w.Felled > 0 || w.SummonFelled > 0 || w.Commander?.Outcome == CommanderOutcome.Felled);

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

    public void CloseWave(int wave, float seconds, int felled, float healthFactor = 1f, float damageFactor = 1f, IReadOnlyList<string>? affixes = null, WaveRoles? roles = null, FortWave? fort = null)
    {
        var r = roles ?? WaveRoles.None;
        _waves.Add(new WaveStats(wave, _kills, felled, _lost, seconds, healthFactor, damageFactor)
        {
            Affixes = affixes ?? Array.Empty<string>(),
            Summoned = r.Summoned,
            SummonSpawned = r.SummonSpawned,
            SummonKills = r.SummonKills,
            SummonFelled = r.SummonFelled,
            Commander = r.Commander,
            Fort = fort ?? FortWave.None,
        });
        _kills = 0;
        _lost = 0;
    }
}
