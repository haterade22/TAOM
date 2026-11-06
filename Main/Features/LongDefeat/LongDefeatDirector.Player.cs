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
}
