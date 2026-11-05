namespace TAOM.Adapters;

public interface IPlayerContextAdapter
{
    string GetPlayerKingdomId();

    /// <summary>The player clan's StringId, or "" when there is none (the #832 join-time war check).</summary>
    string GetPlayerClanId();

    /// <summary>
    /// The player hero's culture StringId. Used to place the player on a Free/Evil side when his
    /// clan has no kingdom (independent, mercenary, or enlisted — enlistment deliberately does not
    /// join the commander's kingdom).
    /// </summary>
    string GetPlayerCultureId();
    bool IsUnderMercenaryService();
}
