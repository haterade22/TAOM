using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// The lord's side of the run (phase4-plan.md, Slice 3): his falls, the soldiers he takes, the rise the clear orders, the bot's take-over and the <c>lord_falls</c> test option, and the general's
/// seat while he is down. Pure: the spawn, the hand-back, the take and the blow are the adapters' (Slice 4).
/// </summary>
internal sealed partial class SurvivalRun
{
    private readonly LordLife _life = new();
    private LdGeneralSeat _seat = LdGeneralSeat.Lord;

    /// <summary>Every fall, take-over and rise so far, for the report.</summary>
    public RunLord LordSummary() => _life.Summary();

    /// <summary>A body the player controlled fell: the lord's own, or a soldier he had taken. Each one counts one death (D40).</summary>
    public void PlayerDown(string troopId = "")
    {
        if (Result != null)
            return;

        _stats.PlayerDied();
        _life.Fell(Math.Max(1, _wave), troopId);
    }

    /// <summary>The field's answer to <see cref="RunEffects.RiseLord"/>: the lord stands in a body of his own again, at the place named, with a taken soldier handed back or not.</summary>
    public void LordRose(string at, bool handedBack)
    {
        if (Result == null)
            _life.Rose(_wave + 1, at, handedBack);
    }

    /// <summary>This tick's reading of the lord, taken before the wave is run, so a clear on this tick orders the rise from what the field shows now.</summary>
    private void ObserveLordLife(RunSnapshot s) => _life.Observe(s.Lord, Math.Max(1, _wave), _plan.Options.Autoplay);

    /// <summary>
    /// The last step of a tick: the two clocks of a wave (the bot's take-over under autoplay, the forced fall in the <c>lord_falls</c> wave once its first batch is out), then the seat when it
    /// changed. The clocks run only while the wave still runs after this tick's work, so neither asks on the tick that cleared it. The seat starts as the lord's, which his adoption writes, so a
    /// run in which he never falls hands out no seat.
    /// </summary>
    private void TickLordClocks(RunSnapshot s, RunEffects fx)
    {
        if (_phase == RunPhase.Wave && !_waveInit)
        {
            if (_plan.Options.Autoplay && _life.BotTakeoverDue(s.Dt))
                fx.TakeOverForBot = true;
            if (_plan.Options.LordFalls == _wave && _life.ForcedFallDue(s.Dt, _stage == WaveStage.Fighting, _wave))
                fx.FellLord = true;
        }

        var seat = GeneralSeat.Choose(_life.Body, StandingHeroIds());
        if (seat == _seat)
            return;

        _seat = seat;
        fx.General = seat;
    }

    /// <summary>The clear raises the lord whenever he is not in a body of his own; an order nobody answers is given again at the next clear.</summary>
    private void OrderRise(RunEffects fx)
    {
        if (_life.RiseOwed)
            fx.RiseLord = new LordRiseOrder(_life.HandBack, _plan.Loadout?.Mounted == true);
    }

    /// <summary>The heroes that stand, in the shop's order and then the plan's free heroes: the first holds the seat while the lord is down.</summary>
    private IReadOnlyList<string> StandingHeroIds() => _plan.Shop.Heroes.Select(h => h.Id).Concat(_plan.FreeHeroes.Select(h => h.Id)).Distinct()
        .Where(id => _heroes.TryGetValue(id, out var state) && state == HeroState.Standing).ToList();
}
