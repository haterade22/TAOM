using System;
using System.Collections.Generic;
using System.Diagnostics;
using TAOM.Features.MarshalsWord;
using TAOM.Features.MarshalsWord.Domain;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

// Throwers on an advance (build-spec 9.4). Each step runs as its own --steps subset, in its own battle, with javelineers where the spearmen stand (SeatTrialRoster.PlayerFor).
internal sealed partial class CommandTrialScript
{
    private static void MarshalThrowerSteps(Dictionary<string, Func<CommandTrialScript, IEnumerable<Verdict>>> into)
    {
        into["advance_throwers"] = s => s.AdvanceThrowers();
        into["advance_throwers_vanilla"] = s => s.AdvanceThrowersVanilla();
    }

    private sealed class ThrowerGround
    {
        internal ThrowerCensus Census;
        internal bool Enemy;
        internal bool Controllable;
        internal float Range = float.NaN;
        internal float StandGap = float.NaN;

        internal bool Ok => Census.Armed >= MarshalTrialRules.ThrowerTrialMinArmed && Enemy && Controllable;
    }

    private bool ThrowerBroken() => _host.MarshalFaulted || _host.MarshalThrowerFaulted;

    private string ThrowerFaultFact() => " faulted=" + Bit(ThrowerBroken());

    private IEnumerable<Verdict> ThrowerGroundSteps(ThrowerGround ground)
    {
        foreach (var verdict in MarshalBattle())
            yield return verdict;
        var waitedAt = _now;
        yield return Verdict.InMissionTick;
        ground.Census = _host.MarshalChooseThrowers();
        while (ground.Census.Units == 0 && _now - waitedAt < SeatTrialRules.ShortWaitSeconds)
        {
            yield return Verdict.Waiting("throwers-chosen");
            yield return Verdict.InMissionTick;
            ground.Census = _host.MarshalChooseThrowers();
        }

        if (ground.Census.Units == 0)
            yield break;
        ground.Enemy = _host.MarshalChooseNearestEnemy() > 0;
        ground.Controllable = _host.MarshalControllable(0);
        ground.Range = _host.MarshalThrowerRange(0);
        ground.StandGap = float.IsNaN(ground.Range) ? float.NaN : MarshalThrowerRules.StandGap(ground.Range);
    }

    private IEnumerable<Verdict> ThrowerOrder()
    {
        yield return Verdict.InMissionTick;
        _host.MarshalSelect(0, 1);
        _host.MarshalAdvance();
    }

    private string ThrowerGroundFacts(ThrowerGround ground) =>
        "units=" + Whole(ground.Census.Units) + " armed0=" + Whole(ground.Census.Armed) + " ammo0=" + Whole(ground.Census.Ammo) + " range=" + Num(ground.Range)
        + " stand_gap=" + Num(ground.StandGap);

    private IEnumerable<Verdict> AdvanceThrowers()
    {
        var ground = new ThrowerGround();
        foreach (var verdict in ThrowerGroundSteps(ground))
            yield return verdict;

        var go = ground.Ok && !ThrowerBroken();
        var postfixMoved = false;
        if (go)
        {
            foreach (var verdict in ThrowerOrder())
                yield return verdict;
            var advancedAt = _now;
            while (_host.MarshalThrowerRun(0) == null && _now - advancedAt < SeatTrialRules.ShortWaitSeconds && !ThrowerBroken())
                yield return Verdict.Waiting("run-published");
            go = _host.MarshalThrowerRun(0) != null && !ThrowerBroken();
        }

        if (go)
        {
            var tableAt = _now;
            while (_host.MarshalThrowerCostRead.Moved == 0 && _now - tableAt < MarshalTrialRules.ThrowerPostfixWaitSeconds && !ThrowerBroken())
                yield return Verdict.Waiting("postfix-moved");
            postfixMoved = _host.MarshalThrowerCostRead.Moved > 0;
            go = postfixMoved && !ThrowerBroken();
        }

        if (go)
        {
            var first = _host.MarshalThrowerRun(0)!.Value;
            var limit = MarshalTrialRules.ApproachWaitSeconds(Math.Max(0f, first.Gap0 - ground.StandGap), _host.MarshalSpeed(0));
            var standStart = _now;
            while (ThrowerNow().State == ThrowerState.Approach && !ThrowerNow().ClosedBeforeStand && _now - standStart < limit && !ThrowerBroken())
                yield return Verdict.Waiting("stand");
            go = ThrowerNow().EverStood && !ThrowerNow().ClosedBeforeStand && !ThrowerBroken();
        }

        if (go)
        {
            var stoodAt = _now;
            while (ThrowerNow().State != ThrowerState.Released && _now - stoodAt < MarshalTuning.StandLimitSeconds + MarshalTrialRules.ThrowerReleaseSlackSeconds && !ThrowerBroken())
                yield return Verdict.Waiting("release");
            go = ThrowerNow().State == ThrowerState.Released && !ThrowerBroken();
        }

        if (go && ThrowerNow().Kind == ThrowerReleaseKind.Rule)
        {
            var releasedAt = _now;
            while (ThrowerNow().Melee == "pending" && _now - releasedAt < MarshalTuning.MeleeWaitSeconds + ThrowerMeleeSlackSeconds && !ThrowerBroken())
                yield return Verdict.Waiting("melee");
        }

        yield return Verdict.InMissionTick;
        yield return ThrowerVerdictOfRun(ground);
    }

    private const float ThrowerMeleeSlackSeconds = 10f;

    private ThrowerFacts ThrowerNow() => _host.MarshalThrowerRun(0) ?? default;

    private Verdict ThrowerVerdictOfRun(ThrowerGround ground)
    {
        var latest = _host.MarshalThrowerRun(0);
        var facts = latest ?? default;
        var cost = _host.MarshalThrowerCostRead;
        var pub = MarshalThrowerRules.CostVerdict(cost.Published.Calls, cost.Published.Ticks, cost.Published.Most, Stopwatch.Frequency);
        var adv = MarshalThrowerRules.CostVerdict(cost.Other.Calls, cost.Other.Ticks, cost.Other.Most, Stopwatch.Frequency);
        var read = new ThrowerTrialRead(ThrowerBroken(), ground.Census.Armed, ground.Enemy, ground.Controllable, latest != null, cost.Moved > 0, facts.ClosedBeforeStand, facts.EverStood,
            latest != null && facts.State == ThrowerState.Released, facts.MaxOvershoot, facts.Shots, facts.RunShots, facts.AmmoSpent, facts.Melee == "pass", cost.Published.Calls, pub);
        var reason = MarshalTrialRules.ThrowerTrialReason(read);
        if (reason == "never-published")
            reason += " why=" + _host.MarshalThrowerWhy(0);
        var line = ThrowerGroundFacts(ground) + " gap0=" + Num(facts.Gap0) + " gap_at_stand=" + Num(facts.GapAtStand) + " min_gap=" + Num(facts.MinGap)
            + " overshoot=" + Num(facts.MaxOvershoot) + " stood_s=" + Num((float)facts.StoodSeconds) + " shots=" + Whole(facts.Shots) + " run_shots=" + Whole(facts.RunShots) + " ammo_spent=" + Whole(facts.AmmoSpent)
            + " armed_at_release=" + Whole(facts.ArmedAtRelease) + " release=" + (string.IsNullOrEmpty(facts.Why) ? "none" : facts.Why) + " blows=" + Whole(facts.Blows)
            + " melee_s=" + Num((float)facts.MeleeSeconds) + " advanced_m=" + Num(facts.Advanced) + " pub_calls=" + Whole((int)cost.Published.Calls) + " pub_mean_us=" + Num((float)pub.MeanMicros)
            + " pub_max_us=" + Num((float)pub.MaxMicros) + " adv_calls=" + Whole((int)cost.Other.Calls) + " adv_mean_us=" + Num((float)adv.MeanMicros)
            + " adv_max_us=" + Num((float)adv.MaxMicros) + " replaced=" + Whole((int)cost.Replaced) + " published=" + Whole(_host.MarshalThrowerPublished)
            + " postfix=" + Whole((int)cost.Moved) + ThrowerFaultFact();
        return reason.Length == 0 ? Verdict.Passed(line) : Verdict.Failed("reason=" + reason + " " + line);
    }

    private IEnumerable<Verdict> AdvanceThrowersVanilla()
    {
        var ground = new ThrowerGround();
        foreach (var verdict in ThrowerGroundSteps(ground))
            yield return verdict;

        var go = ground.Ok && !ThrowerBroken();
        var serialBefore = _host.MarshalThrowerRun(0)?.Serial ?? 0;
        var gap0 = float.NaN;
        var closed = false;
        var armedAtClose = 0;
        var walked = 0f;
        var slowest = float.NaN;
        if (go)
        {
            yield return Verdict.InMissionTick;
            _host.MarshalThrowersOff(true);
            var offAt = _now;
            while (_now - offAt < SettleHoldSeconds)
                yield return Verdict.Waiting("off-copied");
            foreach (var verdict in ThrowerOrder())
                yield return verdict;
            gap0 = _host.MarshalFrontGap(0);
            var limit = MarshalTrialRules.ApproachWaitSeconds(Math.Max(0f, gap0 - MarshalTuning.MeleeGapMetres), _host.MarshalSpeed(0));
            var zone = new ThrowerZoneWalk(float.IsNaN(ground.StandGap) ? MarshalTuning.StandMaxGap : ground.StandGap);
            var advancedAt = _now;
            while (!(_host.MarshalFrontGap(0) <= MarshalTuning.MeleeGapMetres) && _now - advancedAt < limit && !ThrowerBroken())
            {
                zone.Sample(_host.MarshalCompanyAt(0), _host.MissionTime, _host.MarshalFrontGap(0));
                yield return Verdict.Waiting("closed-in");
            }

            walked = zone.Walked;
            slowest = zone.Slowest;
            closed = _host.MarshalFrontGap(0) <= MarshalTuning.MeleeGapMetres;
            yield return Verdict.InMissionTick;
            armedAtClose = _host.MarshalThrowerCensus(0).Armed;
        }

        var published = (_host.MarshalThrowerRun(0)?.Serial ?? 0) != serialBefore;
        var minGap = _host.MarshalFrontGap(0);
        yield return Verdict.InMissionTick;
        _host.MarshalThrowersOff(false);

        var reason = MarshalTrialRules.ThrowerVanillaReason(ThrowerBroken(), ground.Census.Armed, ground.Enemy, ground.Controllable, published, closed, slowest);
        var line = ThrowerGroundFacts(ground) + " gap0=" + Num(gap0) + " gap_end=" + Num(minGap) + " published=" + Bit(published) + " armed_at_close=" + Whole(armedAtClose)
            + " walked_m=" + Num(walked) + " slowest=" + Num(slowest) + ThrowerFaultFact();
        yield return reason.Length == 0 ? Verdict.Passed(line) : Verdict.Failed("reason=" + reason + " " + line);
    }
}
