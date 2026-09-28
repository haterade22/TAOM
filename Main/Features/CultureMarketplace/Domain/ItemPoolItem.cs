namespace TAOM.Features.CultureMarketplace.Domain;

public sealed class ItemPoolItem
{
    public string ItemId { get; }
    public string CultureId { get; }
    public string PrefixCultureId { get; }

    /// <summary>Head, body, leg, hand or cape armour: what a culture drawing on another's armour takes.</summary>
    public bool IsCharacterArmour { get; }

    public ItemPoolItem(string itemId, string cultureId, string prefixCultureId, bool isCharacterArmour = false)
    {
        ItemId = itemId;
        CultureId = cultureId;
        PrefixCultureId = prefixCultureId;
        IsCharacterArmour = isCharacterArmour;
    }
}
