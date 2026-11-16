using TAOM.Features.CheatMenu;

namespace TAOM.Adapters;

/// <summary>
/// Every engine call of the cheat menu (docs/features/cheat-menu.md), so <see cref="CheatMenuService"/> stays testable. Vanilla
/// actions only; ids in, numbers and names out; no engine object is kept between calls. Main thread, campaign running.
/// </summary>
public interface ICheatWorldAdapter
{
    TeleportState ReadTeleportState();

    /// <summary>The main hero's string id, kingdom id and culture id, or null ids with no main hero.</summary>
    (string? HeroId, string? KingdomId, string? CultureId) MainHeroIds();

    /// <summary>Gold after the gift, or null with no main hero.</summary>
    int? GiveGold(int amount);

    /// <summary>False when the main hero has no kingdom (influence belongs to a kingdom's clans).</summary>
    bool AddInfluence(int amount);

    bool AddRenown(int amount);

    /// <summary>Adds <paramref name="count"/> of the main hero's culture's basic or elite basic troop; returns its name, or null when the culture has none.</summary>
    string? AddCultureTroops(bool elite, int count);

    /// <summary>
    /// The faction's troop sources and every character as the picker sees it (CheatTroopList decides). The faction's culture
    /// is the player's kingdom's, or the clan's without a kingdom; null with no main hero or no culture. Troops the
    /// arachnophobia option hides are left out.
    /// </summary>
    (CheatTroopSources Sources, System.Collections.Generic.IReadOnlyDictionary<string, CheatTroopFacts> Troops)? ReadFactionTroops();

    /// <summary>Adds <paramref name="count"/> of the troop to the main party; returns its name, or null for an unknown id or a hero.</summary>
    string? AddTroop(string troopId, int count);

    /// <summary>
    /// Opens the game's own cheat party screen (PartyScreenHelper.OpenScreenAsCheat): every encyclopedia troop, 10 of each, on
    /// the left, the main party on the right. Vanilla opens it only with the engine's cheat mode on; without it the same screen
    /// is opened with vanilla's own list and settings, and prisoners moved to the left are released as vanilla does. False when it could not be opened (no campaign, or the vanilla list
    /// could not be read).
    /// </summary>
    bool OpenCheatPartyScreen();

    /// <summary>Heals every wounded soldier and every hero in the main party; returns how many soldiers were healed.</summary>
    int HealMainParty();

    /// <summary>Changes the player's relation with every living lord of the kingdom; returns how many, or null for an unknown kingdom id.</summary>
    int? AddRelationWithKingdom(string kingdomId, int amount);

    /// <summary>Kingdom id to name for the picker, sorted by name.</summary>
    System.Collections.Generic.IReadOnlyList<(string Id, string Name)> Kingdoms();

    /// <summary>Settlement id to name of one kind (town, castle, village) for the picker, sorted by name.</summary>
    System.Collections.Generic.IReadOnlyList<(string Id, string Name)> Settlements(CheatSettlementKind kind);

    /// <summary>The settlement's name, or null for an unknown id or a settlement with no gate on land.</summary>
    string? SettlementName(string settlementId);

    /// <summary>
    /// Moves the main party (and the parties of the army it leads) to the settlement's gate, holds it there, refreshes its
    /// visual and puts the camera on it. The caller has checked <see cref="TeleportState"/> and the id.
    /// </summary>
    void TeleportToGate(string settlementId);
}
