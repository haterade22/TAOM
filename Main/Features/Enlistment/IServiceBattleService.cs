namespace TAOM.Features.Enlistment;

/// <summary>
/// Battle interception for enlisted service: when the commander's party enters a map
/// event, transition to EnlistedBattle (BEFORE any menu push — the ordering contract the
/// menu guard depends on), ensure a player encounter against that event, and join on the
/// commander's side. Failed joins roll back to parked-attached.
/// </summary>
public interface IServiceBattleService
{
    /// <summary>The commander's party entered a map event. Party ids come from the thin behavior's boundary conversion.</summary>
    void OnCommanderBattleStarted(string commanderPartyId, string attackerPartyId, string defenderPartyId);

    /// <summary>A map event involving the commander ended. Returns to parked-attached unless the loot/aftermath encounter is still open.</summary>
    void OnCommanderBattleEnded();
}
