namespace TAOM.Features.WarChronicle.Events;

/// <summary>
/// Where a chronicle event stands. SAVED AS ITS INT in the <c>chronicle</c> section of the War Chronicle
/// payload: never renumber a value, only append. Pending and Active are open; every other value is a
/// resolution and final.
/// </summary>
public enum ChronicleEventState
{
    /// <summary>Not opened yet: its anchor is unknown or its open day is still ahead.</summary>
    Pending = 0,

    /// <summary>A siege whose window is open; the army focus stands.</summary>
    Active = 1,

    /// <summary>A narrative event that happened; its effects were applied.</summary>
    Occurred = 2,

    /// <summary>A siege the defender held through an assault.</summary>
    Held = 3,

    /// <summary>A siege target an attacker took.</summary>
    Fell = 4,

    /// <summary>A siege window that closed with no assault, or a target lost to a third party. No effects.</summary>
    Lapsed = 5,

    /// <summary>Never opened: a precondition failed, its anchor was skipped, its time passed before the save knew it, or the war ended.</summary>
    Skipped = 6,

    /// <summary>A Narrative event with a hero story (#1105 slice 3) that the player turned: the hero lives. Its own outcome block pays.</summary>
    Averted = 7,
}
