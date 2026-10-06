using TAOM.Features.MapEventGuard;

namespace TAOM.Adapters;

/// <summary>One live map event, read and written through the engine's own exits.</summary>
public interface IStuckBattleEventAdapter
{
    /// <summary>Engine-free data for this event; null once it is finalized, or when reading it fails. Never throws.</summary>
    StuckBattleSnapshot? Read();

    /// <summary>"attacker (faction) vs defender (faction) at place", for a log or console line. Never throws.</summary>
    string Label();

    /// <summary>
    /// Takes every destroyed, non-quest mobile party other than the player's out of the event, the write vanilla's
    /// <c>DestroyPartyAction</c> makes for a party it destroys outside a quest. Returns how many left. A side left
    /// with no party finalizes the event inside the engine.
    /// </summary>
    int DetachWrecks();

    /// <summary><c>MapEvent.SetOverrideWinner</c>: results are committed now and the next update finishes the battle.</summary>
    void AwardVictory(bool attackerWins);
}
