using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.Elephant;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

/// <summary>The crew steps of the command trial (#1503): <see cref="CrewTrialRows"/> names them and <see cref="CrewTrialRules"/> judges them.</summary>
internal sealed partial class CommandTrialScript
{
    private static readonly SeatInput ZoomOutOneNotch = new(Right: 0f, Forward: 0f, Lift: 0f, ZoomNotches: -1f, LookYaw: 0f, LookPitch: 0f, Fast: false);

    private static void CrewSteps(Dictionary<string, Func<CommandTrialScript, IEnumerable<Verdict>>> into)
    {
        into[CrewTrialRows.Mumakil] = s => s.Crew(CrewBeast.Mumakil, CrewTrialRows.Mumakil);
        into[CrewTrialRows.Howdah] = s => s.Crew(CrewBeast.Howdah, CrewTrialRows.Howdah);
    }

    private IEnumerable<Verdict> Crew(CrewBeast beast, string step)
    {
        if (!Seated)
            yield return NotSeated();

        var startedAt = _now;
        int seats;
        while (true)
        {
            yield return Verdict.InMissionTick;
            seats = _host.ChooseCrew(beast);
            if (seats > 0)
                break;
            if (_now - startedAt > CrewTrialRules.CrewWaitSeconds)
                yield return Verdict.Failed("reason=no-crew beast=" + beast);
        }

        _host.ChargeAll();
        var shots = new int[seats];
        var rescues = new int[seats];
        var first = _host.ReadCrew();
        var alive = first.BeastAlive;
        var moved = 0f;
        Fold(first, shots, rescues, ref moved);

        yield return Verdict.Waiting("keep");
        _host.SetInput(NoInput);
        _host.PressKeep();
        var pressedAt = _now;
        while (_host.Keeping == SeatKeepKind.None && _now - pressedAt < SeatTrialRules.ShortWaitSeconds)
            yield return Verdict.Waiting("keep-taken");
        if (_host.Keeping == SeatKeepKind.None)
            yield return Verdict.Failed("reason=not-kept said=" + _host.Said);

        var kind = _host.Keeping;
        var same = _host.KeepsChosen;
        var keptAt = _now;
        var pictures = 0;
        var keepAgain = false;
        while (true)
        {
            if (keepAgain && _host.Keeping == SeatKeepKind.None && _now - pressedAt >= SeatTrialRules.ShortWaitSeconds)
            {
                _host.PressKeep();
                pressedAt = _now;
            }

            keepAgain = false;

            if (pictures < CrewTrialRules.PictureSeconds.Count && _now - keptAt >= CrewTrialRules.PictureSeconds[pictures])
            {
                pictures++;
                _host.Screenshot(CrewTrialRules.PictureName(step, pictures));
                if (pictures == 1)
                {
                    foreach (var waiting in ZoomOut())
                        yield return waiting;
                }
            }

            yield return Verdict.InMissionTick;
            var reading = _host.ReadCrew();
            alive = reading.BeastAlive;
            Fold(reading, shots, rescues, ref moved);
            if (CrewTrialRules.WatchIsOver(_now - keptAt, CrewTrialRules.EveryoneShot(shots), alive))
                break;
            keepAgain = _host.Keeping == SeatKeepKind.None && _host.ChooseCrewArcher();
            yield return Verdict.Waiting("watch");
        }

        var watched = _now - keptAt;
        yield return Verdict.Waiting("let-go");
        if (_host.Keeping != SeatKeepKind.None)
        {
            _host.PressKeep();
            var letGoAt = _now;
            while (_host.Keeping != SeatKeepKind.None && _now - letGoAt < SeatTrialRules.ShortWaitSeconds)
                yield return Verdict.Waiting("let-go");
        }

        var letGo = _host.Keeping == SeatKeepKind.None;
        var rescued = rescues.Sum();
        var facts = CrewTrialRules.Facts(seats, shots, rescued, moved, alive, watched, pictures)
            + " kind=" + kind + " chosen=" + Bit(same) + " letgo=" + Bit(letGo) + Standing();
        var pass = CrewTrialRules.Passes(seats, shots, rescued, moved) && kind == SeatKeepKind.Soldier && same && letGo && Seated && !_host.Faulted;
        yield return pass ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private IEnumerable<Verdict> ZoomOut()
    {
        var zoomAt = _now;
        while (!(_host.DistanceToKept() >= CrewTrialRules.WideMetres) && _now - zoomAt < CrewTrialRules.ZoomSeconds)
        {
            _host.SetInput(ZoomOutOneNotch);
            yield return Verdict.Waiting("zoom");
        }

        _host.SetInput(NoInput);
    }

    private static void Fold(CrewReading reading, int[] shots, int[] rescues, ref float moved)
    {
        var count = Math.Min(shots.Length, reading.Seats.Count);
        for (var i = 0; i < count; i++)
        {
            shots[i] = Math.Max(shots[i], reading.Seats[i].Shots);
            rescues[i] = Math.Max(rescues[i], reading.Seats[i].Rescues);
        }

        if (reading.Moved > moved)
            moved = reading.Moved;
    }
}
