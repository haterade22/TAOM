using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;

namespace TAOM.Features.MapEventGuard;

/// <summary>
/// Decides when a sally-out check must stop (#1099, Patch142): only when its besieger leader is inside the player's
/// battle and nobody is left to speak for that party. Every other check runs vanilla, because the vanilla call on
/// this path also closes the player's decided battle.
/// </summary>
public sealed class SallyOutGuardService
{
    internal const string CheckId = "taom.map-event-guard.sally-skip";
    private const string BattleMenuId = "encounter";

    private readonly IOutcomeLog _outcomes;
    private readonly IModLogger _logger;
    private readonly IGameMenuAdapter _menus;
    private string? _lastWarned;

    public SallyOutGuardService(IOutcomeLog outcomes, IModLogger logger, IGameMenuAdapter menus)
    {
        _outcomes = outcomes;
        _logger = logger;
        _menus = menus;
    }

    public bool ShouldStopSally(ISallySiegeAdapter siege)
    {
        var snapshot = siege.Read();
        if (snapshot == null || !snapshot.LeaderInPlayerBattle || snapshot.JoinsLiveBattle)
            return false;

        if (snapshot.Roster.Any(entry => entry.Number > 0 && !entry.MarkedToDie))
            return false;

        Report(snapshot);

        // A stop leaves the player's battle open, and only the battle menu closes it.
        if (_menus.CurrentMenuId == EnlistmentMenuService.ServiceWaitMenuId)
            _menus.SwitchTo(BattleMenuId);

        return true;
    }

    private void Report(SallySiegeSnapshot snapshot)
    {
        string detail = $"settlement={snapshot.SettlementId} leader={snapshot.LeaderId}";
        _outcomes.Pass(CheckId, snapshot.SettlementId, detail);
        if (detail == _lastWarned)
            return;

        _lastWarned = detail;
        _logger.LogWarning($"[MapEventGuard] stopped a sally-out check, the besieger leader would be empty: {detail}");
    }
}
