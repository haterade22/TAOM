namespace TAOM.Features.CultureMarketplace.Domain;

public sealed class ItemPoolItem
{
    public string ItemId { get; }
    public string CultureId { get; }
    public string PrefixCultureId { get; }

    /// <summary>Head, body, leg, hand or cape armour: what a culture drawing on another's armour takes.</summary>
    public bool IsCharacterArmour { get; }

    /// <summary>A melee weapon, launcher, ammunition, thrown weapon or shield.</summary>
    public bool IsWeapon { get; }

    public ItemPoolItem(string itemId, string cultureId, string prefixCultureId, bool isCharacterArmour = false,
        bool isWeapon = false)
    {
        ItemId = itemId;
        CultureId = cultureId;
        PrefixCultureId = prefixCultureId;
        IsCharacterArmour = isCharacterArmour;
        IsWeapon = isWeapon;
    }
}
