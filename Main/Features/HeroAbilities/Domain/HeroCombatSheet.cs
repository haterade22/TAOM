using System;
using System.Globalization;

namespace TAOM.Features.HeroAbilities.Domain;

/// <summary>
/// One hero body's fight, for the balance lines (docs/features/hero-abilities.md, "Balance tally"): the enemies he
/// downs, the health he takes from enemies and loses, how long he lives and what fells him. No engine type. Main thread only.
/// </summary>
public sealed class HeroCombatSheet
{
    internal HeroCombatSheet(string heroId, int serial, float spawnedAt)
    {
        HeroId = heroId;
        Serial = serial;
        SpawnedAt = spawnedAt;
    }

    public string HeroId { get; }

    // The kth body of this hero in the mission.
    public int Serial { get; }

    public float SpawnedAt { get; }

    // Enemies killed or knocked out by him (the engine's removal names him as the affector).
    public int Kills { get; internal set; }

    // Health removed, as the engine caps it at the victim's health (OnScoreHit's damagedHp); blocked hits are not counted.
    public int Dealt { get; internal set; }
    public int HitsLanded { get; internal set; }
    public int Taken { get; internal set; }
    public int HitsTaken { get; internal set; }

    // The highest health limit seen: the spawn path scales it after the agent is built.
    public float MaxHealth { get; private set; }

    // Removed from the battle; Downed when killed or knocked out, else he fled.
    public float? FellAt { get; internal set; }
    public bool Downed { get; internal set; }
    public string Killer { get; internal set; } = "none";
    public string Blow { get; internal set; } = "none";

    // The player took this body over at some point (#1366): his own play is in these figures.
    public bool Played { get; set; }

    public void NoteHealth(float limit)
    {
        if (limit > MaxHealth)
            MaxHealth = limit;
    }

    public float SecondsAlive(float now) => Math.Max(0f, (FellAt ?? now) - SpawnedAt);

    private string State => !FellAt.HasValue ? "alive" : Downed ? "fell" : "fled";

    public string Line(float now, float health) => string.Format(CultureInfo.InvariantCulture,
        "hero={0} body={1} state={2} alive_s={3:F0} kills={4} dealt={5} hits={6} taken={7} hit_by={8} max_health={9:F0} health={10:F0} killer={11} blow={12} played={13}",
        HeroId, Serial, State, SecondsAlive(now), Kills, Dealt, HitsLanded, Taken, HitsTaken,
        MaxHealth, Math.Max(0f, health), Killer, Blow, Played ? 1 : 0);
}
