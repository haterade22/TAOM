namespace TAOM.Features.SeatOfSeeing;

/// <summary>The MCM values behind the Seat of Seeing.</summary>
internal interface ISeatSettingsProvider
{
    /// <summary>The Seat key acts. Off, the key does nothing and a Seat that is up comes down.</summary>
    bool Enabled { get; }

    /// <summary>When a battle's deployment ends, the player's view goes into the Seat with no blend instead of back to the hero.</summary>
    bool BeginBattlesInSeat { get; }

    /// <summary>While seated, the cursor shows when the order panel is open.</summary>
    bool CursorWithPanel { get; }

    /// <summary>While seated, the order panel reopens after an order.</summary>
    bool KeepPanelOpen { get; }

    /// <summary>While seated, in a mission that allows it, the hero is handed to the AI and fights on inside his formation.</summary>
    bool HeroFightsOn { get; }

    /// <summary>After the hero falls, a key in the Seat takes the soldier at the pointer.</summary>
    bool TakeSoldierAfterFall { get; }

    /// <summary>With the hero's view in third person and the order panel open, the view lifts so the player can see over the ranks.</summary>
    bool RiseWithPanel { get; }
}
