using System.Collections.Generic;
using TAOM.Features.MonsterSize;

namespace TAOM.Adapters;

/// <summary>
/// The engine side of the Monster size pass (docs/features/monster-size.md): reads TAOM's size attribute off every
/// Monster, lists the Horse items, and writes a Horse item's body_length, which the engine exposes read-only.
/// </summary>
public interface IMonsterSizeCatalogAdapter
{
    /// <summary>Every &lt;Monster&gt; in the merged Monsters XML carrying <see cref="MonsterSizeConfig.AttributeName"/>:
    /// its id and the attribute's raw text, unvalidated.</summary>
    IReadOnlyList<KeyValuePair<string, string>> ReadDeclaredSizes();

    /// <summary>Every loaded item with a HorseComponent.</summary>
    IReadOnlyList<HorseItemRecord> ReadHorseItems();

    /// <summary>Sets the item's HorseComponent.BodyLength. False when the item is gone or the engine's setter is
    /// not found.</summary>
    bool SetBodyLength(string itemId, int bodyLength);
}
