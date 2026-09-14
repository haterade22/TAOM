using TaleWorlds.Library;

namespace TAOM.Features.SettlementNameplateRelation;

/// <summary>
/// Relation int to plate colours. The int is vanilla's
/// <c>SandBox.ViewModelCollection.Nameplate.SettlementNameplateVM.RelationType</c> (v1.4.8):
/// 0 neutral, 1 same faction, 2 enemy, 3 allied kingdom. Anything else is treated as neutral, so a
/// future enum member can never paint a plate with an uninitialised colour.
///
/// Neutral is identity (white bar and frame, black text): <c>Widget.Color</c> MULTIPLIES the
/// sprite, so vanilla's black neutral tint, correct for its white plate sprite, would turn TAOM's
/// parchment into a black bar under black text. The other three are starting values for the artist
/// to tune; every string is exactly #RRGGBBAA because <c>Color.ConvertStringToColor</c> reads
/// Substring(7, 2): a #RRGGBB value throws inside the attribute loader, which catches it per
/// attribute with a failed assert and keeps the previous colour, so the override is silently lost.
/// </summary>
public static class NameplateRelationPalette
{
    public const int Neutral = 0;
    public const int SameFaction = 1;
    public const int Enemy = 2;
    public const int Ally = 3;

    public const string NeutralBar = "#FFFFFFFF";
    public const string NeutralText = "#000000FF";
    public const string NeutralFrame = "#FFFFFFFF";

    public const string SameFactionBar = "#B8DCA0FF";
    public const string SameFactionText = "#1E5410FF";
    public const string SameFactionFrame = "#90E070FF";

    public const string EnemyBar = "#F0A090FF";
    public const string EnemyText = "#7A0C0CFF";
    public const string EnemyFrame = "#FF7060FF";

    public const string AllyBar = "#A8C8F0FF";
    public const string AllyText = "#174C8CFF";
    public const string AllyFrame = "#80B8FFFF";

    public static readonly string[] DefaultColorStrings =
    {
        NeutralBar, NeutralText, NeutralFrame,
        SameFactionBar, SameFactionText, SameFactionFrame,
        EnemyBar, EnemyText, EnemyFrame,
        AllyBar, AllyText, AllyFrame,
    };

    public static readonly NameplatePaletteEntry DefaultNeutral = Entry(NeutralBar, NeutralText, NeutralFrame);
    public static readonly NameplatePaletteEntry DefaultSameFaction = Entry(SameFactionBar, SameFactionText, SameFactionFrame);
    public static readonly NameplatePaletteEntry DefaultEnemy = Entry(EnemyBar, EnemyText, EnemyFrame);
    public static readonly NameplatePaletteEntry DefaultAlly = Entry(AllyBar, AllyText, AllyFrame);

    /// <summary>The compiled defaults for a relation; unknown values resolve to neutral.</summary>
    public static NameplatePaletteEntry Resolve(int relation)
        => Select(relation, DefaultNeutral, DefaultSameFaction, DefaultEnemy, DefaultAlly);

    /// <summary>Picks one of the caller's four entries (the widget's XML-overridable colours).</summary>
    public static NameplatePaletteEntry Select(
        int relation,
        in NameplatePaletteEntry neutral,
        in NameplatePaletteEntry sameFaction,
        in NameplatePaletteEntry enemy,
        in NameplatePaletteEntry ally)
    {
        switch (relation)
        {
            case SameFaction:
                return sameFaction;
            case Enemy:
                return enemy;
            case Ally:
                return ally;
            default:
                return neutral;
        }
    }

    /// <summary>
    /// The MCM tint slider (#596): blends an entry toward identity (white bar and frame, which
    /// multiply the sprites by nothing; black text, the brush's own colour). 1 is the entry as
    /// authored, 0 is a neutral-looking plate. Strength is clamped to [0, 1]; a non-finite value
    /// paints the full entry rather than a NaN colour.
    /// </summary>
    public static NameplatePaletteEntry Blend(in NameplatePaletteEntry entry, float strength)
    {
        var s = EffectiveStrength(true, strength);
        if (s >= 1f) return entry;
        return new NameplatePaletteEntry(
            Color.Lerp(Color.White, entry.Bar, s),
            Color.Lerp(Color.Black, entry.Text, s),
            Color.Lerp(Color.White, entry.Frame, s));
    }

    /// <summary>The strength a plate should paint with: 0 when the colours are off, else the
    /// slider value clamped to [0, 1], with a non-finite value read as 1.</summary>
    public static float EffectiveStrength(bool colorsEnabled, float strength)
    {
        if (!colorsEnabled) return 0f;
        if (float.IsNaN(strength) || float.IsInfinity(strength)) return 1f;
        if (strength <= 0f) return 0f;
        return strength >= 1f ? 1f : strength;
    }

    private static NameplatePaletteEntry Entry(string bar, string text, string frame)
        => new NameplatePaletteEntry(
            Color.ConvertStringToColor(bar),
            Color.ConvertStringToColor(text),
            Color.ConvertStringToColor(frame));
}
