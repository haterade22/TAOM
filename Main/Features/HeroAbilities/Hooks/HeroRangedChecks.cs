using System.Globalization;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat.Diagnostics;

namespace TAOM.Features.HeroAbilities.Hooks;

/// <summary>
/// The [Check] ids that tools/ingame_checks/1361.toml and 1415.toml read, and the verdict each gives. Pure: the ranged mission
/// logic counts, these judge. A float gate is written as the requirement, so a NaN fails it.
/// </summary>
internal static class HeroRangedChecks
{
    internal const string PrecisionId = "taom.hero-abilities.precision";
    internal const string HitsId = "taom.hero-abilities.hits";
    internal const string PierceId = "taom.hero-abilities.pierce";
    internal const string MultiShotId = "taom.hero-abilities.multishot";
    internal const string ArrowKillId = "taom.hero-abilities.arrow-kill";
    internal const string KeepDistanceId = "taom.hero-abilities.keep-distance";

    // The fewest shots that judge the hit share, and the share that passes.
    private const int ShotsToJudge = 20;
    private const float HitShare = 0.5f;
    private const int ShieldHitsToJudge = 5;
    private const int FiredToPass = 2;
    private const int WindowShotsToJudge = 3;
    private const int FiredToJudgeHits = 10;
    private const int CallsToJudge = 50;
    private const int LooksToJudge = 10;

    // A kit switch that is off judges nothing: its check is a skip (reason=off).
    internal static CheckVerdict Precision(string heroId, bool zeroAimError, int nonZero, float shooterError, float inaccuracy)
    {
        var detail = $"hero={heroId} ranged=1 nonzero={Num(nonZero)} shooter_error={F4(shooterError)} inaccuracy={F4(inaccuracy)}";
        if (!zeroAimError)
            return new CheckVerdict(CheckResult.Skip, "reason=off " + detail);
        return LongDefeatChecks.Verdict(nonZero == 0 && shooterError == 0f && inaccuracy == 0f, detail);
    }

    internal static CheckVerdict NeverRanged(string heroId) => new(CheckResult.Skip, $"reason=never-ranged hero={heroId}");

    internal static CheckVerdict Hits(string heroId, int shots, int contact, int shield)
    {
        var share = shots > 0 ? contact / (float)shots : 0f;
        var detail = $"hero={heroId} shots={Num(shots)} contact={Num(contact)} share={F2(share)} shield={Num(shield)}";
        if (shots < ShotsToJudge)
            return new CheckVerdict(CheckResult.Skip, "reason=few-shots " + detail);
        return LongDefeatChecks.Verdict(share >= HitShare, detail);
    }

    internal static CheckVerdict Pierce(string heroId, bool pierceShields, int shieldHits, int passed, int broken)
    {
        var detail = $"hero={heroId} shield_hits={Num(shieldHits)} passed={Num(passed)} broken={Num(broken)}";
        if (!pierceShields)
            return new CheckVerdict(CheckResult.Skip, "reason=off " + detail);
        if (passed >= 1)
            return new CheckVerdict(CheckResult.Pass, detail);
        return shieldHits >= ShieldHitsToJudge
            ? new CheckVerdict(CheckResult.Fail, detail)
            : new CheckVerdict(CheckResult.Skip, "reason=few-shield-hits " + detail);
    }

    internal static CheckVerdict MultiShot(string heroId, bool item, int windows, int windowShots, int fired, int noTarget, int outOfRange, int hits)
    {
        var detail = $"hero={heroId} item={Bit(item)} windows={Num(windows)} window_shots={Num(windowShots)} fired={Num(fired)} "
                     + $"no_target={Num(noTarget)} out_of_range={Num(outOfRange)} hits={Num(hits)}";
        if ((windows >= 1 && windowShots >= WindowShotsToJudge && fired == 0) || (fired >= FiredToJudgeHits && hits == 0))
            return new CheckVerdict(CheckResult.Fail, detail);
        if (fired >= FiredToPass && hits >= 1)
            return new CheckVerdict(CheckResult.Pass, detail);
        return new CheckVerdict(CheckResult.Skip, (windows == 0 ? "reason=no-window " : "reason=few-shots ") + detail);
    }

    internal static CheckVerdict ArrowKill(string heroId, int bodyHits, int head, int exempt, int deadShooter, int calls, int kills)
    {
        var detail = $"hero={heroId} body_hits={Num(bodyHits)} head={Num(head)} exempt={Num(exempt)} dead_shooter={Num(deadShooter)} calls={Num(calls)} kills={Num(kills)}";
        if (kills >= 1)
            return new CheckVerdict(CheckResult.Pass, detail);
        return calls >= CallsToJudge
            ? new CheckVerdict(CheckResult.Fail, detail)
            : new CheckVerdict(CheckResult.Skip, "reason=few-calls " + detail);
    }

    // #1415: a step is the engine call, moved is his body covering half the step by the end of the hold. One move passes;
    // with no move, ten steps or ten cornered looks fail.
    internal static CheckVerdict KeepDistance(string heroId, bool on, int steps, int moved, int cornered, int meleeSeconds)
    {
        var detail = $"hero={heroId} steps={Num(steps)} moved={Num(moved)} cornered={Num(cornered)} melee_seconds={Num(meleeSeconds)}";
        if (!on)
            return new CheckVerdict(CheckResult.Skip, "reason=off " + detail);
        if (moved >= 1)
            return new CheckVerdict(CheckResult.Pass, detail);
        if (steps >= LooksToJudge || cornered >= LooksToJudge)
            return new CheckVerdict(CheckResult.Fail, detail);
        return new CheckVerdict(CheckResult.Skip, (steps == 0 && cornered == 0 ? "reason=no-threat " : "reason=few-threats ") + detail);
    }

    /// <summary>The mission-end lines of one hero body. Precision was written at its sample, or is a never-ranged skip now.</summary>
    internal static void WriteEnd(IOutcomeLog outcomes, string missionKey, HeroRangedTally t)
    {
        var key = KeyOf(missionKey, t);
        if (!t.PrecisionWritten)
            LongDefeatChecks.Record(outcomes, PrecisionId, key, NeverRanged(t.HeroId));
        LongDefeatChecks.Record(outcomes, HitsId, key, Hits(t.HeroId, t.Shots, t.Contact, t.PrimaryShield));
        LongDefeatChecks.Record(outcomes, PierceId, key, Pierce(t.HeroId, t.PierceShields, t.ShieldHits, t.Passed, t.Broken));
        LongDefeatChecks.Record(outcomes, MultiShotId, key,
            MultiShot(t.HeroId, t.ItemResolved, t.Windows, t.WindowShots, t.Fired, t.NoTarget, t.OutOfRange, t.ExtraHits));
        LongDefeatChecks.Record(outcomes, ArrowKillId, key, ArrowKill(t.HeroId, t.BodyHits, t.Head, t.Exempt, t.DeadShooter, t.Calls, t.Kills));
        LongDefeatChecks.Record(outcomes, KeepDistanceId, key, KeepDistance(t.HeroId, t.KeepDistance, t.Steps, t.Moved, t.Cornered, t.MeleeSeconds));
    }

    internal static string KeyOf(string missionKey, HeroRangedTally t) => $"{missionKey}-{t.HeroId}-{Num(t.Serial)}";

    private static string Bit(bool value) => value ? "1" : "0";

    private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string F2(float value) => value.ToString("F2", CultureInfo.InvariantCulture);

    private static string F4(float value) => value.ToString("F4", CultureInfo.InvariantCulture);
}
