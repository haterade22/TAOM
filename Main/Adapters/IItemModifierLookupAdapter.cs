namespace TAOM.Adapters;

/// <summary>
/// Validity check for a saved <c>ItemModifier</c> StringId. Per the
/// "Lookup Functions With Fallbacks: Validate Before Lookup" rule
/// (<c>.claude/rules/csharp-architecture.md</c>), services validate the StringId via this
/// adapter BEFORE asking <see cref="IEquipmentSlotAdapter.ApplySlot"/> to use it. The
/// difference matters: <c>MBObjectManager.GetObject&lt;ItemModifier&gt;</c> can return
/// <c>null</c> for unknown ids; the service must distinguish "no modifier in the preset"
/// from "modifier was in the preset but the item-XML no longer defines it" so the user
/// gets a correct status report.
/// </summary>
public interface IItemModifierLookupAdapter
{
    /// <summary>True when an empty/null id was given (treat as "no modifier", not as missing)
    /// OR when the id resolves to a real <c>ItemModifier</c>.</summary>
    bool ExistsOrEmpty(string? modifierStringId);
}
