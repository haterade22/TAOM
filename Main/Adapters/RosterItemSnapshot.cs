namespace TAOM.Adapters;

/// <summary>
/// TAOM-owned snapshot of one ItemRoster entry — keeps services free of sealed
/// TaleWorlds types per ADR-007. CultureStringId is `item.Culture?.StringId` at
/// capture time; null/empty for vanilla universal items (trade goods, base armour).
/// </summary>
public sealed class RosterItemSnapshot
{
    public string ItemId { get; }
    public string CultureStringId { get; }
    public int Count { get; }

    public RosterItemSnapshot(string itemId, string cultureStringId, int count)
    {
        ItemId = itemId;
        CultureStringId = cultureStringId;
        Count = count;
    }
}
