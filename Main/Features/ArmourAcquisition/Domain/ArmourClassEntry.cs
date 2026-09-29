namespace TAOM.Features.ArmourAcquisition.Domain;

/// <summary>
/// One row of the generated class table (Main/_Module/ModuleData/armour_acquisition/armour_classes.xml):
/// a piece's class and the piece the armoury upgrades it into, if any.
/// </summary>
public sealed class ArmourClassEntry
{
    public ArmourClassEntry(string itemId, ArmourClass cls, string? nextItemId)
    {
        ItemId = itemId;
        Class = cls;
        NextItemId = string.IsNullOrEmpty(nextItemId) ? null : nextItemId;
    }

    public string ItemId { get; }

    public ArmourClass Class { get; }

    public string? NextItemId { get; }
}
