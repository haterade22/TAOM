using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

internal sealed partial class LongDefeatDirector
{
    /// <summary>Whether the lord's class rides: he joins the cavalry formation at the home point and may sally (spike S9).</summary>
    private bool LordRides => _plan.Loadout?.Mounted == true;

    /// <summary>
    /// Spawns the player's lord in the plan's class kit (the start of the run and every respawn) and judges the spawn: the field reads back what the engine built, and the
    /// <c>class</c> check is written from that. A mounted lord spawns at the home point, which the boot required to be usable. His stats are derived again when <c>AdoptIfNew</c> takes the new body
    /// (<c>DressNewBody</c>, which also refreshes his horse), because the horse was built before him and took no record. False when the field could not build him. A run with no class spawns him as before and writes no class line.
    /// </summary>
    private bool SpawnLord(bool respawn)
    {
        if (!_field.SpawnPlayer(respawn, _plan.Loadout, LordRides ? _homePoint : null))
            return false;

        if (_plan.Loadout is not { } loadout)
            return true;

        var verdict = LongDefeatChecks.Class(loadout, _field.ReadKit());
        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.ClassId, respawn ? $"{_mission}-d{_run.Stats.PlayerDeaths}" : _mission, verdict);
        _logger.LogInfo($"{Tag}[Class] {(respawn ? "respawn" : "start")} {verdict.Result.ToString().ToLowerInvariant()} {verdict.Detail}");
        return true;
    }

    /// <summary>
    /// Serves the rise the run orders at a clear (phase4-plan.md D40). Until the engine half of the death rule lands (Slice 4) this is the respawn it always was: only a lord with no body at all is
    /// raised, where every respawn stands, so a lord who took a soldier keeps that body and the order comes again at the next clear. The place is booked as <c>respawn</c> for a foot lord (he joins
    /// his formation, not the keep the plan's rise will use) and <c>home</c> for a mounted one.
    /// </summary>
    private void RiseLord(LordRiseOrder order)
    {
        if (_run.Result != null || _mainBody != 0)
            return;

        _respawned = true;
        if (SpawnLord(true))
            _run.LordRose(order.AtHome ? "home" : "respawn", false);
        else
            LongDefeatChecks.Record(_outcomes, LongDefeatChecks.PlayerDeathId, "d" + _run.Stats.PlayerDeaths, LongDefeatChecks.PlayerDeath(false, "respawn-refused"));
    }
}
