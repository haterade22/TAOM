using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.SiegeRetreatLoot;

/// <summary>
/// A defender whose siege assault ends by attacker retreat still receives the gear of the attackers who fell
/// (#1121, Patch146). Vanilla's retreat branch finishes the battle without the casualty-loot step; the adapter runs it
/// and opens the loot screen. Only the casualty step runs: the item step would strip the baggage of besiegers who
/// survive and keep the siege, and vanilla itself captures no one from a retreating side.
/// </summary>
public sealed class SiegeRetreatLootService
{
    internal const string CheckId = "taom.siege-retreat-loot.collected";

    private readonly ISiegeRetreatLootAdapter _loot;
    private readonly ICoopSessionProvider _coop;
    private readonly IOutcomeLog _outcomes;
    private readonly IModLogger _logger;

    public SiegeRetreatLootService(
        ISiegeRetreatLootAdapter loot, ICoopSessionProvider coop, IOutcomeLog outcomes, IModLogger logger)
    {
        _loot = loot;
        _coop = coop;
        _outcomes = outcomes;
        _logger = logger;
    }

    /// <summary>True when the casualty loot ran and the loot screen must open after vanilla's handler.</summary>
    public bool CollectLoot(bool enemyRetreated)
    {
        if (!enemyRetreated || !_loot.CanLootCasualties())
            return false;

        // A co-op client moves no item between rosters on its own: the host's roster writes are the shared ones.
        if (!CoopSessionPolicy.MayWriteSaveBackedState(_coop.IsCoopClient))
        {
            _outcomes.Skip(CheckId, "", "reason=coop-client");
            return false;
        }

        if (!_loot.RunCasualtyLoot())
        {
            _logger.LogError("[SiegeRetreatLoot] could not run its loot step, the retreat gives no loot as in vanilla");
            _outcomes.Fail(CheckId, "", "reason=loot-step-missing");
            return false;
        }

        return true;
    }

    /// <summary>Opens the loot screen and reports the outcome: the check proves delivery, not collection.</summary>
    public void OpenLootScreen()
    {
        int units = _loot.OpenLootScreen();
        if (units > 0)
            _outcomes.Pass(CheckId, "", $"items={units}");
        else if (units == 0)
            _outcomes.Skip(CheckId, "", "reason=nothing-to-loot");
        else
            _outcomes.Fail(CheckId, "", "reason=screen-failed");
    }
}
