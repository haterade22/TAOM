namespace TAOM.Features.CultureMarketplace.Domain;

public sealed class ItemPoolItem
{
    public string ItemId { get; }
    public string CultureId { get; }
    public string PrefixCultureId { get; }

    public ItemPoolItem(string itemId, string cultureId, string prefixCultureId)
    {
        ItemId = itemId;
        CultureId = cultureId;
        PrefixCultureId = prefixCultureId;
    }
}
