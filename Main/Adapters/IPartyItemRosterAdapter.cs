namespace TAOM.Adapters;

/// <summary>
/// ADR-007 boundary over PartyBase.MainParty.ItemRoster for the enlistment
/// equipment pipeline: add issued gear to the player's INVENTORY (never equip),
/// count it, and reclaim it at discharge.
///
/// <para><b>Deliberate (ItemObject, int) overload choice</b> — the adapters.md
/// modifier-overload rule says to PREFER the (EquipmentElement, int) overloads
/// because the (ItemObject, int) forms wrap <c>new EquipmentElement(item)</c> and
/// drop any ItemModifier. Here the simpler overload is the CORRECT surface, on
/// purpose: freshly-issued service gear carries no ItemModifier, so adding via
/// (ItemObject, int) lands on exactly the unmodified stack; and removal via the
/// same overload resolves through <c>ItemRoster.FindIndexOfElement</c>, which
/// matches item+modifier — so a reclaim can only ever drain the UNMODIFIED stack,
/// and the player's own modified variants ("Sharp", "Battered", cosmetic) survive
/// untouched (ItemRoster.cs:185/194/200, v1.4.7 decompile).</para>
/// </summary>
public interface IPartyItemRosterAdapter
{
    /// <summary>False outside a campaign or before the main party exists.</summary>
    bool IsMainPartyAvailable();

    /// <summary>Adds count unmodified instances of the item. False when the item or party is missing.</summary>
    bool AddItem(string itemId, int count);

    /// <summary>Count held in the UNMODIFIED stack (modified variants are not counted — see class doc).</summary>
    int GetItemCount(string itemId);

    /// <summary>Removes up to count from the UNMODIFIED stack only (clamped to what is held).
    /// Returns the number actually removed.</summary>
    int RemoveItem(string itemId, int count);
}
