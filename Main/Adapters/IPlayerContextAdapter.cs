using TAOM.Features.Execution;

namespace TAOM.Adapters;

public interface IPlayerContextAdapter
{
    string GetPlayerKingdomId();

    /// <summary>The player clan's StringId, or "" when there is none (the #832 join-time war check).</summary>
    string GetPlayerClanId();

    /// <summary>
    /// The player clan's map faction id with that faction's own culture (<c>FactionSides.OfPlayer</c>):
    /// the kingdom served, or the player clan itself when it serves none. <c>SideIds.None</c> outside a
    /// campaign. Places the player on a Free/Evil/Neutral side (#957).
    /// </summary>
    SideIds GetPlayerSideIds();
    bool IsUnderMercenaryService();
}
