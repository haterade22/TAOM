using System;
using System.Collections.Generic;
using TAOM.Features.Warg;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

/// <summary>The warg gait step of the command trial (#1563): <see cref="WargTrialRows"/> names it and <see cref="WargTrialRules"/> judges it.</summary>
internal sealed partial class CommandTrialScript
{
    private static void WargSteps(Dictionary<string, Func<CommandTrialScript, IEnumerable<Verdict>>> into)
    {
        into[WargTrialRows.Gaits] = s => s.WargGaits();
    }

    /// <summary>A reading that is no number stays in <c>top</c> through <c>Math.Max</c> and fails the pass rule.</summary>
    private IEnumerable<Verdict> WargGaits()
    {
        if (!Seated)
            yield return NotSeated();

        var startedAt = _now;
        while (true)
        {
            yield return Verdict.InMissionTick;
            if (_host.ReadWargs().Alive > 0)
                break;
            if (_now - startedAt > WargTrialRules.WargWaitSeconds)
                yield return Verdict.Failed("reason=no-wargs");
        }

        _host.HoldAll();
        var stand = 0f;
        var begunAt = _now;
        while (_now - begunAt < WargTrialRules.StandSeconds)
        {
            yield return Verdict.InMissionTick;
            var reading = _host.ReadWargs();
            if (_now - begunAt >= WargTrialRules.StandSeconds - WargTrialRules.StandWindowSeconds)
                stand = Math.Max(stand, reading.WargTop);
        }

        _host.ChargeAll();
        var last = default(WargReading);
        var top = 0f;
        var horse = 0f;
        var limit = 0f;
        var scale = 0f;
        var riders = 0;
        begunAt = _now;
        while (_now - begunAt < WargTrialRules.ChargeSeconds)
        {
            yield return Verdict.InMissionTick;
            last = _host.ReadWargs();
            top = Math.Max(top, last.WargTop);
            horse = Math.Max(horse, last.HorseTop);
            limit = Math.Max(limit, last.WargLimit);
            scale = Math.Max(scale, last.WargScale);
            riders = Math.Max(riders, last.Alive);
        }

        _host.HoldAll();
        var facts = WargTrialRules.Facts(riders, last.Mounted, stand, top, limit, horse, scale) + Standing();
        var pass = WargTrialRules.Passes(riders, top) && Seated && !_host.Faulted;
        yield return pass ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }
}
