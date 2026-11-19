namespace TAOM.Adapters;

/// <summary>One half-finalized map event, with the same repair steps Patch170 runs on the player's encounter.</summary>
public interface IOrphanedBattleEventAdapter
{
    /// <summary>Who fought and where, for the log.</summary>
    string Label();

    /// <summary>The event as it stands now (<c>HasBattle</c> is always true).</summary>
    HalfFinishedEncounterSnapshot Read();

    /// <summary>Runs the load step the event missed: <c>MapEventComponent.AfterLoad(mapEvent)</c>.</summary>
    void RestoreBackReference();

    /// <summary>Runs <c>MapEventSide.HandleMapEventEnd()</c> on one side (0 defender, 1 attacker); returns the parties it held.</summary>
    int EndSide(int sideIndex);

    /// <summary>
    /// The rest of vanilla's finish after the sides end (<c>MapEvent.FinalizeEventAux</c>):
    /// <c>MapEventVisual.OnMapEventEnd()</c>, then <c>MapEventComponent.FinalizeComponent()</c>.
    /// </summary>
    void FinishEvent();
}
