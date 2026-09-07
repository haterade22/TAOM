namespace TAOM.Features.Enlistment;

/// <summary>
/// Battle interception for enlisted service: when the commander's party enters a map
/// event, transition to EnlistedBattle (BEFORE any menu push — the ordering contract the
/// menu guard depends on), ensure a player encounter against that event, and join on the
/// commander's side. Failed joins roll back to parked-attached.
/// </summary>
public interface IServiceBattleService
{
    /// <summary>The commander's party entered a map event. Party id comes from the thin behavior's boundary conversion.</summary>
    void OnCommanderBattleStarted(string commanderPartyId);

    /// <summary>
    /// Retry the join for a commander already in a map event. Raised hourly by the reconciler
    /// when the commander is fighting and the player is not in the event — the recovery path for
    /// a missed MapEventStarted edge (save-load mid-battle, a throw, enlisting mid-fight).
    /// </summary>
    void TryJoinCommanderBattle(string commanderPartyId);

    /// <summary>
    /// A map event involving the commander OR the player ended. Returns to parked-attached unless
    /// the loot/aftermath encounter is still open.
    ///
    /// <paramref name="mainPartyWasInEndingEvent"/> is the boundary's answer to "is the event that
    /// just ended the one the player is in?", and it gates the army detach. It cannot be inferred
    /// in here: <c>IEncounterAdapter.IsMainPartyInMapEvent</c> reads whether the player is in SOME
    /// map event, and at <c>MapEventEnded</c> time that is true for both the event ending and any
    /// other one. Detaching against the wrong answer is issue #551, a CTD.
    /// </summary>
    void OnCommanderBattleEnded(bool mainPartyWasInEndingEvent);
}
