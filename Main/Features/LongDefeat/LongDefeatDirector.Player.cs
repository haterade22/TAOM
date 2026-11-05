using TAOM.Features.LongDefeat.Diagnostics;

namespace TAOM.Features.LongDefeat;

internal sealed partial class LongDefeatDirector
{
    /// <summary>
    /// Spawns the player's lord in the plan's class kit (the start of the run and every respawn) and judges the spawn: the field reads back what the engine built, and the
    /// <c>class</c> check is written from that. False when the field could not build him. A run with no class spawns him as before and writes no class line.
    /// </summary>
    private bool SpawnLord(bool respawn)
    {
        if (!_field.SpawnPlayer(respawn, _plan.Loadout))
            return false;

        if (_plan.Loadout is not { } loadout)
            return true;

        var verdict = LongDefeatChecks.Class(loadout, _field.ReadKit());
        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.ClassId, respawn ? $"{_mission}-d{_run.Stats.PlayerDeaths}" : _mission, verdict);
        _logger.LogInfo($"{Tag}[Class] {(respawn ? "respawn" : "start")} {verdict.Result.ToString().ToLowerInvariant()} {verdict.Detail}");
        return true;
    }
}
