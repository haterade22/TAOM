using System;
using System.Collections.Generic;
using TAOM.Features.SeatOfSeeing.Domain;
using TAOM.Features.Spider;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

/// <summary>The spider gait step of the command trial (#1541): <see cref="SpiderTrialRows"/> names it and <see cref="SpiderTrialRules"/> judges it.</summary>
internal sealed partial class CommandTrialScript
{
    private static void SpiderSteps(Dictionary<string, Func<CommandTrialScript, IEnumerable<Verdict>>> into)
    {
        into[SpiderTrialRows.Gaits] = s => s.SpiderGaits();
    }

    /// <summary>What the step has seen so far. The speeds are the highest of each phase, so a spider that ran and then fell still counts.</summary>
    private sealed class SpiderRun
    {
        internal int Riders;
        internal int Mounted;
        internal float Stand;
        internal float Top;
        internal float Stop;
        internal int Pictures;
        internal float PressedAt;
        internal bool KeepAgain;
    }

    private IEnumerable<Verdict> SpiderGaits()
    {
        if (!Seated)
            yield return NotSeated();

        var run = new SpiderRun();
        var startedAt = _now;
        while (true)
        {
            yield return Verdict.InMissionTick;
            var first = _host.ReadSpiders();
            if (first.Alive > 0 && _host.ChooseSpiderRider())
            {
                run.Riders = first.Alive;
                run.Mounted = first.Mounted;
                break;
            }

            if (_now - startedAt > SpiderTrialRules.SpiderWaitSeconds)
                yield return Verdict.Failed("reason=no-spiders");
        }

        _host.HoldAll();
        yield return Verdict.Waiting("keep");
        _host.SetInput(NoInput);
        _host.PressKeep();
        run.PressedAt = _now;
        while (_host.Keeping == SeatKeepKind.None && _now - run.PressedAt < SeatTrialRules.ShortWaitSeconds)
            yield return Verdict.Waiting("keep-taken");
        if (_host.Keeping == SeatKeepKind.None)
            yield return Verdict.Failed("reason=not-kept said=" + _host.Said);

        var kind = _host.Keeping;
        var same = _host.KeepsChosen;

        foreach (var waiting in SpiderPhase(run, "stand", SpiderTrialRules.StandSeconds, new[] { SpiderTrialRules.StandSeconds },
                     (reading, since) =>
                     {
                         if (since >= SpiderTrialRules.StandSeconds - SpiderTrialRules.StandWindowSeconds)
                             run.Stand = Math.Max(run.Stand, reading.TopSpeed);
                     }))
            yield return waiting;

        yield return Verdict.InMissionTick;
        _host.ChargeAll();
        foreach (var waiting in SpiderPhase(run, "charge", SpiderTrialRules.ChargeSeconds, SpiderTrialRules.ChargePictureSeconds,
                     (reading, _) => run.Top = Math.Max(run.Top, reading.TopSpeed)))
            yield return waiting;

        yield return Verdict.InMissionTick;
        _host.HoldAll();
        foreach (var waiting in SpiderPhase(run, "stop", SpiderTrialRules.StopSeconds, new[] { SpiderTrialRules.StopSeconds },
                     (reading, since) =>
                     {
                         if (since >= SpiderTrialRules.StopSeconds - SpiderTrialRules.StopWindowSeconds)
                             run.Stop = Math.Max(run.Stop, reading.TopSpeed);
                     }))
            yield return waiting;

        yield return Verdict.Waiting("let-go");
        if (_host.Keeping != SeatKeepKind.None)
        {
            _host.PressKeep();
            var letGoAt = _now;
            while (_host.Keeping != SeatKeepKind.None && _now - letGoAt < SeatTrialRules.ShortWaitSeconds)
                yield return Verdict.Waiting("let-go");
        }

        var letGo = _host.Keeping == SeatKeepKind.None;
        var facts = SpiderTrialRules.Facts(run.Riders, run.Mounted, run.Stand, run.Top, run.Stop, run.Pictures)
            + " kind=" + kind + " chosen=" + Bit(same) + " letgo=" + Bit(letGo) + Standing();
        var pass = SpiderTrialRules.Passes(run.Riders, run.Stand, run.Top, run.Pictures)
            && kind == SeatKeepKind.Soldier && same && letGo && Seated && !_host.Faulted;
        yield return pass ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    /// <summary>
    /// One phase of the watch: each mission tick reads the spiders, and each screen tick takes the pictures due. The keep goes to another rider when the
    /// kept one fell. The view stays at the keep's own distance. <paramref name="read"/> gets the reading and the seconds since the phase began.
    /// The phase's first segment runs in the mission tick that its order was given in, so no picture is due in it.
    /// </summary>
    private IEnumerable<Verdict> SpiderPhase(SpiderRun run, string name, float seconds, IReadOnlyList<float> pictureAt, Action<SpiderReading, float> read)
    {
        var begunAt = _now;
        var taken = 0;
        while (true)
        {
            var since = _now - begunAt;
            if (run.KeepAgain && _host.Keeping == SeatKeepKind.None && _now - run.PressedAt >= SeatTrialRules.ShortWaitSeconds)
            {
                _host.PressKeep();
                run.PressedAt = _now;
            }

            run.KeepAgain = false;
            if (taken < pictureAt.Count && since >= pictureAt[taken])
            {
                taken++;
                run.Pictures++;
                _host.Screenshot(SeatTrialRules.PictureName(SpiderTrialRows.Gaits, run.Pictures));
            }

            if (!(since < seconds))
                yield break;

            yield return Verdict.InMissionTick;
            var reading = _host.ReadSpiders();
            run.Mounted = reading.Mounted;
            read(reading, _now - begunAt);
            run.KeepAgain = _host.Keeping == SeatKeepKind.None && _host.ChooseSpiderRider();
            yield return Verdict.Waiting(name);
        }
    }
}
