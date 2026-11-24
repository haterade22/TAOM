namespace TAOM.Adapters;

/// <summary>What the faction screen's 3D viewport shows for a resolved pick: the same body, race, sex and
/// battle gear (an engine <c>Equipment</c>, or null) that the face generator and the player then get. The
/// viewport may still draw another race or hide weapons (FactionRoster.ShownRace, HideViewportWeapons).</summary>
public sealed class PresetDisplay
{
    public PresetDisplay(string bodyProperties, int race, bool isFemale, object? battle)
    {
        BodyProperties = bodyProperties;
        Race = race;
        IsFemale = isFemale;
        Battle = battle;
    }

    public string BodyProperties { get; }

    public int Race { get; }

    public bool IsFemale { get; }

    public object? Battle { get; }
}
