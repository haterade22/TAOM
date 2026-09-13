using TaleWorlds.Library;

namespace TAOM.Features.SettlementNameplateRelation;

/// <summary>
/// One relation's colours on a settlement plate: the wash multiplied into the parchment bar sprite,
/// the name text's font colour, and the tint multiplied into the gold diamond frame sprite.
/// </summary>
public readonly struct NameplatePaletteEntry
{
    public NameplatePaletteEntry(Color bar, Color text, Color frame)
    {
        Bar = bar;
        Text = text;
        Frame = frame;
    }

    public Color Bar { get; }

    public Color Text { get; }

    public Color Frame { get; }
}
