namespace TAOM.Features.MonsterSize;

/// <summary>One item with a HorseComponent, as the size pass sees it: its id, its Monster's id (null when it names
/// none) and its current body_length (0 when its XML declares none).</summary>
public sealed class HorseItemRecord
{
    public HorseItemRecord(string itemId, string? monsterId, int bodyLength)
    {
        ItemId = itemId;
        MonsterId = monsterId;
        BodyLength = bodyLength;
    }

    public string ItemId { get; }
    public string? MonsterId { get; }
    public int BodyLength { get; }
}
