using System.Collections.Generic;
using TAOM.Features.ArmourAcquisition.Domain;

namespace TAOM.Adapters;

/// <summary>
/// The player's side of an armoury visit (docs/features/armour-acquisition.md): who they are, their
/// purse, and their party inventory. Pieces are handled stack by stack with their quality modifier,
/// never through the modifier-dropping ItemObject overloads (adapters.md, "Modifier-Preserving Overloads").
/// </summary>
public interface IArmouryPlayerAdapter
{
    string HeroId { get; }

    string? KingdomId { get; }

    string? CultureId { get; }

    int Gold { get; }

    /// <summary>Every stack in the party inventory.</summary>
    IReadOnlyList<InventoryPiece> ReadInventory();

    /// <summary>The item ids the main hero wears, battle and civilian sets.</summary>
    IReadOnlyList<string> ReadEquippedItemIds();

    /// <summary>Removes <paramref name="count"/> of an item, any modifier; false when the party carries fewer.</summary>
    bool RemoveItem(string itemId, int count);

    /// <summary>Removes one of exactly this stack (item and modifier); false when there is none.</summary>
    bool RemovePiece(string itemId, string? modifierId);

    /// <summary>Adds pieces with this modifier (none when the id is null or unknown); false when the item is not loaded.</summary>
    bool AddPiece(string itemId, string? modifierId, int count);

    /// <summary>Takes gold from the player (to no one); false when they have less.</summary>
    bool ChargeGold(int amount);

    /// <summary>Changes the player's relation with a hero (and, as the engine does, his kin); false when the hero is unknown.</summary>
    bool ChangeRelationWith(string heroId, int delta);
}
