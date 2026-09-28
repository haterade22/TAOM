namespace TAOM.Features.ArmourAcquisition.Domain;

/// <summary>
/// One loaded item as the gate sees it, read once per game init by <c>IArmourItemCatalogAdapter</c>
/// (items reload from XML with every game, so the snapshot does too). <see cref="IsMerchandise"/> is
/// the value the XML gave the item, before the gate touches it. Names are looked up when shown, not here.
/// </summary>
public sealed class ArmourItemRecord
{
    public ArmourItemRecord(string itemId, bool isCharacterArmour, int engineTier, bool isMerchandise,
        string? cultureId, int value)
    {
        ItemId = itemId;
        IsCharacterArmour = isCharacterArmour;
        EngineTier = engineTier;
        IsMerchandise = isMerchandise;
        CultureId = cultureId;
        Value = value;
    }

    public string ItemId { get; }

    /// <summary>Head, body, leg, hand or cape armour: the slots a character wears. Horse harness is not.</summary>
    public bool IsCharacterArmour { get; }

    /// <summary><c>(int)ItemObject.Tier</c>: Tier1 = 0 ... Tier6 = 5; -1 for a Tierf under 0.5.</summary>
    public int EngineTier { get; }

    public bool IsMerchandise { get; }

    /// <summary>The item's own culture StringId, or null for a culture-less item.</summary>
    public string? CultureId { get; }

    public int Value { get; }
}
