namespace TAOM.Features.PlayerSwitcher.Domain;

/// <summary>
/// One selectable lord, as the UI and the planner see them. Deliberately engine-free: the row
/// carries only what a decision or a binding needs, so every rule about it is unit testable.
/// </summary>
public readonly struct HeroPickRow
{
    public HeroPickRow(
        string heroId,
        string name,
        HeroPickerGroup group,
        int race,
        bool isFemale,
        bool isLeader,
        bool hasClan)
    {
        HeroId = heroId;
        Name = name;
        Group = group;
        Race = race;
        IsFemale = isFemale;
        IsLeader = isLeader;
        HasClan = hasClan;
    }

    public string HeroId { get; }
    public string Name { get; }
    public HeroPickerGroup Group { get; }

    /// <summary>FaceGen race index, used to drive the live preview.</summary>
    public int Race { get; }

    public bool IsFemale { get; }

    /// <summary>True when this hero leads their clan. Drives the tuple's leader marker.</summary>
    public bool IsLeader { get; }

    /// <summary>
    /// False only for clanless heroes and wanderers. This is what <see cref="SwitchPath"/>
    /// selection keys off, so the planner stays a pure function of the row.
    /// </summary>
    public bool HasClan { get; }

    public bool IsEmpty => string.IsNullOrEmpty(HeroId);
}
