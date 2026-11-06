using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat.Diagnostics;

/// <summary>
/// The <c>commander</c> and <c>summon</c> verdicts of #1079 (phase2-plan.md section 6). Pure: the director reads the run's wave record and the engine's read-back, these turn them into pass
/// or fail. What a commander must have been is recomputed from the plan's spec, not read from the record, so a record that drifted from the plan fails.
/// </summary>
internal static class LongDefeatCommanderChecks
{
    private const float HealthTolerance = 0.01f;

    /// <summary>An ability due this long before the commander ended must have fired at least once: the first delay plus a margin for the tick length.</summary>
    private const float FireMargin = 2f;

    /// <summary>
    /// A commander at the clear of his wave: the troop, rank and steps are the plan's for that wave, he ended (killed, felled or left, never still standing), no ability fired after his end, each
    /// ability that was due while he stood fired, and, when the engine was read right after he spawned, his health limit over his character's maximum is the rank's factor times the wave's
    /// endless factor within 1%. A null <paramref name="atSpawn"/> means no read was possible (no effects adapter), and the health term is left out; an empty sample with an adapter fails.
    /// No ability fired more often than its cooldown allows while he stood (the cooldown stretched by the Legend boons <paramref name="owned"/>, recomputed here apart from the run's own arithmetic), which
    /// is how a boon that never reached the clocks shows.
    /// </summary>
    internal static CheckVerdict Commander(int wave, CommanderRecord record, CommanderSpec expected, float endlessHealth, LdScaleSample? atSpawn, IReadOnlyCollection<BoonRow>? owned = null)
    {
        var stretch = Math.Max(0f, 1f + (owned?.Sum(b => b.Effects.Concat(b.Drawback).Where(e => e.Key == BoonKeys.CommanderCooldownPercent).Sum(e => e.Value)) ?? 0f) / 100f);
        var rankOk = record.TroopId == expected.Rank.TroopId && record.RankId == expected.Rank.Id && record.Steps == expected.Steps;
        var ended = record.Outcome != CommanderOutcome.Standing;
        var afterEnd = record.UsesAfterEnd == 0;
        var fired = expected.Abilities.All(a => !(record.AliveSeconds >= a.FirstSeconds + FireMargin) || (record.Uses.TryGetValue(a.Id, out var n) && n >= 1));
        var notTooOften = expected.Abilities.All(a => !record.Uses.TryGetValue(a.Id, out var n) || n <= MostUses(record.AliveSeconds, a, stretch));
        var wanted = expected.Health * endlessHealth;
        var health = atSpawn is not { } sample ? "n/a" : sample.HasAgent ? Num(sample.Ratio) : "unread";
        var healthOk = atSpawn is not { } read || (read.HasAgent && read.Ratio > 0f && Math.Abs(read.Ratio - wanted) <= wanted * HealthTolerance);
        var uses = string.Join(",", expected.Abilities.Select(a => a.Id + ":" + (record.Uses.TryGetValue(a.Id, out var count) ? count : 0)));
        return LongDefeatChecks.Verdict(rankOk && ended && afterEnd && fired && notTooOften && healthOk,
            $"w={Num(wave)} troop={record.TroopId} rank={record.RankId} steps={Num(record.Steps)} health_x={health} expected_x={Num(wanted)} outcome={record.Outcome.ToString().ToLowerInvariant()} "
            + $"uses={uses} after_end={Num(record.UsesAfterEnd)} alive={Num(record.AliveSeconds)} cooldown_x={Num(stretch)}");
    }

    /// <summary>The most times an ability can have fired in <paramref name="alive"/> seconds: once at its first delay, then once per cooldown (never under the plan's floor), each fire counted from the one before.</summary>
    private static int MostUses(float alive, CommanderAbility ability, float stretch)
    {
        if (!(alive >= ability.FirstSeconds))
            return 0;

        var cooldown = Math.Max(CommanderPlan.MinCooldownSeconds, ability.CooldownSeconds * stretch);
        return (int)Math.Floor(1.0 + (alive - ability.FirstSeconds) / cooldown + 1e-3);
    }

    /// <summary>
    /// One summon at the clear of its wave: it queued the count the plan gives that ability for that wave, and every queued soldier was sent to the field before the clear. <paramref name="firedAt"/>
    /// and <paramref name="lastSpawnedAt"/> are wave seconds (risk 16: do the summons arrive in time), and <paramref name="waveSeconds"/> the wave's length.
    /// </summary>
    internal static CheckVerdict Summon(int wave, int use, int queued, int expected, int spawned, float firedAt, float lastSpawnedAt, float waveSeconds) =>
        LongDefeatChecks.Verdict(queued == expected && spawned == queued && queued > 0,
            $"w={Num(wave)} s={Num(use)} queued={Num(queued)} expected={Num(expected)} spawned={Num(spawned)} fired_at={Num(firedAt)} spawned_at={Num(lastSpawnedAt)} wave_seconds={Num(waveSeconds)}");

    /// <summary>A summon the run ended before its soldiers had all come: not a pass it did not earn, and not a failure of the mechanic.</summary>
    internal static CheckVerdict SummonUnfinished(int wave, int use, int queued, int spawned) =>
        new(CheckResult.Skip, $"w={Num(wave)} s={Num(use)} queued={Num(queued)} spawned={Num(spawned)} reason=run-ended");

    private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Num(float value) => value.ToString("F2", CultureInfo.InvariantCulture);
}
