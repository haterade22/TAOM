namespace TAOM.Adapters;

public interface IPlayerContextAdapter
{
    string GetPlayerKingdomId();

    /// <summary>
    /// The player hero's culture StringId. Used to place the player on a Free/Evil side when his
    /// clan has no kingdom (independent, mercenary, or enlisted — enlistment deliberately does not
    /// join the commander's kingdom).
    /// </summary>
    string GetPlayerCultureId();

    /// <summary>
    /// The player clan's StringId, for a seam that only receives the victim and must tell whether
    /// the player is the executioner or the bereaved.
    /// </summary>
    string GetPlayerClanId();
    bool IsUnderMercenaryService();
}
