using System;
using System.Globalization;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat.Diagnostics;

internal enum CheckResult
{
    Pass,
    Fail,
    Skip,
}

internal readonly record struct CheckVerdict(CheckResult Result, string Detail);

/// <summary>
/// The #1079 <c>[Check]</c> ids that <c>tools/ingame_checks/1079.toml</c> reads, and the verdict each one gives. Pure: the director
/// reads the engine and the run, these turn the numbers into pass, fail or skip. A gate on a float is written as the requirement,
/// so a NaN fails it.
/// </summary>
internal static class LongDefeatChecks
{
    internal const string BootId = "taom.long-defeat.boot";
    internal const string GeneralId = "taom.long-defeat.general";
    internal const string MusterId = "taom.long-defeat.muster";
    internal const string KeepId = "taom.long-defeat.keep";
    internal const string WaveId = "taom.long-defeat.wave";
    internal const string StaleId = "taom.long-defeat.stale";
    internal const string NoRoutId = "taom.long-defeat.no-rout";
    internal const string GoldId = "taom.long-defeat.gold";
    internal const string ShopId = "taom.long-defeat.shop";
    internal const string EarlyStartId = "taom.long-defeat.early-start";
    internal const string PlayerDeathId = "taom.long-defeat.player-death";
    internal const string EndId = "taom.long-defeat.end";
    internal const string BestRunId = "taom.long-defeat.best-run";
    internal const string MenuId = "taom.long-defeat.menu";

    /// <summary>A new soldier within this many metres of one from an earlier batch counts as an overlap. A first guess, below a healthy formation's spacing.</summary>
    internal const float OverlapRadius = 0.4f;

    /// <summary>The most soldiers of a muster batch that may overlap an earlier batch, and the floor for those on the frame point (which may be up to half the batch), before the check fails.</summary>
    internal const int MusterLimit = 6;

    /// <summary>The main-menu option was drawn: it needs a fortress to show. A dedicated server hides it on purpose, so that is a skip, not a failure.</summary>
    internal static CheckVerdict Menu(int fortresses, bool dedicatedServer)
    {
        var detail = $"fortresses={Num(fortresses)} dedicated={Flag(dedicatedServer)}";
        if (dedicatedServer)
            return new CheckVerdict(CheckResult.Skip, detail);

        return Verdict(fortresses > 0, detail);
    }

    internal static CheckVerdict Boot(LdBootFacts f)
    {
        var ok = f.DeploymentFinished && f.SiegeTeamAi && f.HasMainAgent && !f.IsFriendlyMission;
        return Verdict(ok, $"deployment_finished={Flag(f.DeploymentFinished)} siege_ai={Flag(f.SiegeTeamAi)} main={Flag(f.HasMainAgent)} friendly={Flag(f.IsFriendlyMission)}");
    }

    /// <summary>A boot that never got as far as the facts: the check still writes its line, so the run reads FAIL, not NOT REACHED.</summary>
    internal static CheckVerdict BootFailed(string problem) => new(CheckResult.Fail, $"reason={problem}");

    internal static CheckVerdict General(LdBootFacts f)
    {
        var ok = f.GeneralIsMain && f.IsPlayerGeneral && f.OrderOwnerIsMain;
        return Verdict(ok, $"general_is_main={Flag(f.GeneralIsMain)} player_general={Flag(f.IsPlayerGeneral)} order_owner_is_main={Flag(f.OrderOwnerIsMain)}");
    }

    /// <summary>A shipped muster point the scene no longer serves: the run still plays, on the scene's frame, but it must not read as a pass.</summary>
    internal static CheckVerdict MusterPlaceLost(FormationSlot slot) => Verdict(false, $"source=scene class={slot} recorded place has no navmesh");

    internal static CheckVerdict Muster(int onPoint, int overlap, int far, int offMesh, int batch, string source = "scene")
    {
        var ok = far == 0 && offMesh == 0 && onPoint <= Math.Max(MusterLimit, batch / 2) && overlap <= MusterLimit;
        return Verdict(ok, $"on_point={Num(onPoint)} of={Num(batch)} overlap={Num(overlap)} far={Num(far)} off_mesh={Num(offMesh)} source={source}");
    }

    /// <summary><paramref name="source"/> is where the point came from: "recorded" for a keep block of fortresses.json, "scene" for the reinforcement frame.</summary>
    internal static CheckVerdict Keep(LdPoint? at, string source = "scene", float radius = 0f)
    {
        if (at is not { } p)
            return new CheckVerdict(CheckResult.Fail, "reason=no-point");

        var ok = FiniteFloatValidator.IsFinite(p.X) && FiniteFloatValidator.IsFinite(p.Y) && FiniteFloatValidator.IsFinite(p.Z);
        return Verdict(ok, $"source={source} at=({F1(p.X)},{F1(p.Y)},{F1(p.Z)}) radius={F1(radius)}");
    }

    internal static CheckVerdict Wave(WaveStats w, int count, float stragglerShare) =>
        Verdict(w.Felled <= stragglerShare * count && w.Kills + w.Felled >= count,
            $"kills={Num(w.Kills)} felled={Num(w.Felled)} lost={Num(w.Lost)} seconds={F1(w.Seconds)} count={Num(count)}");

    internal static CheckVerdict StaleCleared() => new(CheckResult.Pass, "cleared=true");

    /// <summary>A charge that never led to a clear. Only a run that ended as a stalemate proves the charge failed; any other end is not its doing.</summary>
    internal static CheckVerdict StaleStranded(RunEndReason reason) =>
        reason == RunEndReason.Stalemate
            ? new CheckVerdict(CheckResult.Fail, "cleared=false end=Stalemate")
            : new CheckVerdict(CheckResult.Skip, $"cleared=false end={reason}");

    internal static CheckVerdict NoRout(RunStats s)
    {
        if (s.TotalKills <= 0)
            return new CheckVerdict(CheckResult.Skip, "reason=no-kills");

        return Verdict(s.Fled == 0 && s.FleeingPeak == 0, $"fled={Num(s.Fled)} fleeing_peak={Num(s.FleeingPeak)} kills={Num(s.TotalKills)}");
    }

    internal static CheckVerdict Gold(Purse p, RunStats s, int wavesCleared) =>
        Gold(p.Start, p.TotalEarned, p.Spent, p.Balance, p.EarnedFrom(PurseSource.Bounty), p.EarnedFrom(PurseSource.WaveBonus),
            p.EarnedFrom(PurseSource.EarlyStart), s.TotalKills, wavesCleared);

    /// <summary>The books must balance (start plus earned minus spent is the balance), a kill owes a bounty and a cleared wave owes its bonus.</summary>
    internal static CheckVerdict Gold(long start, long earned, long spent, long balance, long bounty, long bonus, long early, int kills, int wavesCleared)
    {
        var books = start + earned - spent == balance;
        var ok = books && (kills == 0 || bounty > 0) && (wavesCleared == 0 || bonus > 0);
        return Verdict(ok, $"start={Num(start)} earned={Num(earned)} spent={Num(spent)} balance={Num(balance)} bounty={Num(bounty)} "
            + $"bonus={Num(bonus)} early={Num(early)}");
    }

    /// <summary>A pack must come back at its full count. Where it landed is detail only: a pile spreads by itself and fails nothing.</summary>
    internal static CheckVerdict Shop(int requested, int spawned, int onPoint, int overlap) =>
        Verdict(spawned == requested, $"spawned={Num(spawned)} of={Num(requested)} on_point={Num(onPoint)} overlap={Num(overlap)}");

    internal static CheckVerdict EarlyStart(float secondsLeft, int paid, RunRules r)
    {
        // Recomputed here on purpose, apart from SurvivalRun.TryStartWave: a check that shared the formula could not catch a change in it.
        var raw = Math.Ceiling(Math.Max(0f, secondsLeft) * (double)r.EarlyGoldPerSecond);
        var expected = raw > 0d ? (int)Math.Min(r.EarlyGoldCap, raw) : 0;
        return Verdict(paid == expected, $"paid={Num(paid)} expected={Num(expected)} left={F1(secondsLeft)}");
    }

    internal static CheckVerdict PlayerDeath(bool adopted, string via) => Verdict(adopted, $"via={via}");

    internal static CheckVerdict End(RunResult r, bool reportBuilt) =>
        Verdict(reportBuilt, $"reason={r.Reason} wave={Num(r.Wave)} cleared={Num(r.WavesCleared)} kills={Num(r.Kills)} report={(reportBuilt ? "built" : "missing")}");

    /// <summary>The run was saved as the fortress's best; it passes only when the file read back holds exactly what was saved.</summary>
    internal static CheckVerdict BestRunSaved(BestRun saved, BestRun? readBack, string? error = null) =>
        Verdict(readBack == saved, $"waves={Num(saved.Waves)} kills={Num(saved.Kills)} read_back={ReadBack(saved, readBack)}{(error == null ? "" : " error=" + error)}");

    internal static CheckVerdict BestRunKept(BestRun run, BestRun best) =>
        new(CheckResult.Skip, $"waves={Num(run.Waves)} kills={Num(run.Kills)} best_waves={Num(best.Waves)} best_kills={Num(best.Kills)}");

    /// <summary>The mission threw in its start or a tick and the run was abandoned: the end check fails, so the run reads FAIL.</summary>
    internal static CheckVerdict Fault(string where, Exception ex) => new(CheckResult.Fail, $"reason=fault where={where} error={ex.GetType().Name}");

    internal static void Record(IOutcomeLog? log, string id, string key, CheckVerdict v)
    {
        switch (v.Result)
        {
            case CheckResult.Pass:
                log?.Pass(id, key, v.Detail);
                break;
            case CheckResult.Fail:
                log?.Fail(id, key, v.Detail);
                break;
            default:
                log?.Skip(id, key, v.Detail);
                break;
        }
    }

    internal static CheckVerdict Verdict(bool ok, string detail) => new(ok ? CheckResult.Pass : CheckResult.Fail, detail);

    private static string Flag(bool v) => v ? "true" : "false";

    private static string ReadBack(BestRun saved, BestRun? readBack) =>
        readBack == null ? "none" : readBack == saved ? "same" : $"{Num(readBack.Waves)}/{Num(readBack.Kills)}";

    private static string Num(long v) => v.ToString(CultureInfo.InvariantCulture);

    private static string F1(float v) => v.ToString("F1", CultureInfo.InvariantCulture);
}
