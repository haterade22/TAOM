namespace TAOM.Features.FactionPower;

/// <summary>One slider: a realm culture id, the name text of its factions.json entry, and its side (evil, free or neutral).</summary>
public sealed class FactionPowerEntry
{
    public FactionPowerEntry(string cultureId, string name, string side)
    {
        CultureId = cultureId;
        Name = name;
        Side = side;
    }

    public string CultureId { get; }
    public string Name { get; }
    public string Side { get; }
}
