namespace TAOM.Adapters;

/// <summary>The engine reads and the exit behind the empty meeting guard (#1099).</summary>
public interface IEmptyMeetingAdapter
{
    /// <summary>
    /// The id of the party the player is about to meet when nobody in it can speak; null when someone can, or when
    /// there is no encounter.
    /// </summary>
    string? FindPartnerWithNoSpeaker();

    /// <summary>Closes the encounter the way vanilla's own leave path does.</summary>
    void LeaveEncounter();
}
