using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace TAOM.Adapters;

public sealed class ItemModifierLookupAdapter : IItemModifierLookupAdapter
{
    public bool ExistsOrEmpty(string? modifierStringId)
    {
        if (string.IsNullOrEmpty(modifierStringId)) return true;
        return MBObjectManager.Instance?.GetObject<ItemModifier>(modifierStringId!) != null;
    }
}
