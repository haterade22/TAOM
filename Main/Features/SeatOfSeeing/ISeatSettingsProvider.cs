namespace TAOM.Features.SeatOfSeeing;

/// <summary>The MCM values behind the Seat of Seeing (S1: the switch and the battle-start option).</summary>
internal interface ISeatSettingsProvider
{
    /// <summary>The Seat key acts. Off, the key does nothing and a Seat that is up comes down.</summary>
    bool Enabled { get; }

    /// <summary>When a battle's deployment ends, the player's view goes into the Seat with no blend instead of back to the hero.</summary>
    bool BeginBattlesInSeat { get; }
}
