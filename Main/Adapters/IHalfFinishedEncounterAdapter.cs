namespace TAOM.Adapters;

/// <summary>The engine reads and the vanilla calls behind the half-finished encounter repair (Patch170).</summary>
public interface IHalfFinishedEncounterAdapter
{
    /// <summary>The player's encounter as it stands now.</summary>
    HalfFinishedEncounterSnapshot Read();

    /// <summary>Runs the load step the event missed: <c>MapEventComponent.AfterLoad(mapEvent)</c>.</summary>
    void RestoreBackReference();

    /// <summary>Runs <c>MapEventSide.HandleMapEventEnd()</c> on one side (0 defender, 1 attacker); returns the parties it held.</summary>
    int EndSide(int sideIndex);

    /// <summary>Heroes still alive and marked <c>DiedInBattle</c>.</summary>
    int CountMarkedAliveHeroes();
}
