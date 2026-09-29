using System.Collections.Generic;
using TAOM.Features.ArmourAcquisition.Domain;

namespace TAOM.Adapters;

/// <summary>
/// The loaded items as the armour acquisition gate sees them (docs/features/armour-acquisition.md), and
/// the one write it makes: <c>ItemObject.NotMerchandise</c>, which the engine sets only from XML
/// (<c>{ get; private set; }</c>, ItemObject.cs:154 in v1.5.3).
/// </summary>
public interface IArmourItemCatalogAdapter
{
    /// <summary>Every loaded item, read from MBObjectManager; empty when there is no object manager.</summary>
    IReadOnlyList<ArmourItemRecord> ReadItems();

    /// <summary>False when the NotMerchandise setter did not resolve (an engine update renamed it).</summary>
    bool CanWriteMerchandise { get; }

    /// <summary>Sets NotMerchandise on one item; false when the item is not loaded or the setter is missing.</summary>
    bool SetNotMerchandise(string itemId, bool notMerchandise);

    /// <summary>The item's display name in the current language, or the id when it is not loaded.</summary>
    string GetName(string itemId);
}
