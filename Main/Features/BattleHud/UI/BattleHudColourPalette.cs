namespace TAOM.Features.BattleHud.UI;

/// <summary>
/// The battle HUD colour option (Mod Options, Accessibility). Default keeps the stone rings and their
/// colours exactly as they were. The three colour vision choices give the player health, mount health
/// and shield rings flat colours picked to be told apart more easily with that colour vision type, on
/// one dark solid track; each ring keeps its place and its fill, which tell them apart as well. The
/// ability centre, the ammo capsule and the rest of the UI are not changed. Pure, with no TaleWorlds or
/// MCM type, so the standalone GauntletPreview compiles it beside the widget.
/// </summary>
public static class BattleHudColourPalette
{
    /// <summary>The MCM dropdown's choices. MCM persists the index: never reorder or remove one.</summary>
    public static readonly string[] Choices =
    {
        "Default",
        "Deuteranopia (red-green)",
        "Protanopia (red-green)",
        "Tritanopia (blue-yellow)",
    };

    // The dark solid track of every accessible palette: the stone track's own colour, drawn flat.
    private const uint AccessibleTrack = 0x2B231B;

    /// <summary>The dropdown index to a mode; no index, or one outside the four choices, is Default.</summary>
    public static BattleHudColourMode Resolve(int? selectedIndex) => selectedIndex switch
    {
        1 => BattleHudColourMode.Deuteranopia,
        2 => BattleHudColourMode.Protanopia,
        3 => BattleHudColourMode.Tritanopia,
        _ => BattleHudColourMode.Default,
    };

    /// <summary>The flat palette for a colour vision mode; null for Default (and any unknown mode), which
    /// draws the stone rings unchanged.</summary>
    public static BattleHudRingPalette? Accessible(BattleHudColourMode mode) => mode switch
    {
        BattleHudColourMode.Deuteranopia => new BattleHudRingPalette(0xF0E442, 0x56B4E9, 0xF5F5F5, AccessibleTrack),
        BattleHudColourMode.Protanopia => new BattleHudRingPalette(0xE6B450, 0x66C2FF, 0xF5F5F5, AccessibleTrack),
        BattleHudColourMode.Tritanopia => new BattleHudRingPalette(0xF28EAB, 0x55D6C2, 0xF5F5F5, AccessibleTrack),
        _ => null,
    };
}

/// <summary>The persisted dropdown index of each choice in <see cref="BattleHudColourPalette.Choices"/>.</summary>
public enum BattleHudColourMode
{
    Default = 0,
    Deuteranopia = 1,
    Protanopia = 2,
    Tritanopia = 3,
}

/// <summary>Opaque 0xRRGGBB colours for the three channel fills and their shared track.</summary>
public readonly struct BattleHudRingPalette
{
    public BattleHudRingPalette(uint health, uint horse, uint shield, uint track)
    {
        Health = health;
        Horse = horse;
        Shield = shield;
        Track = track;
    }

    public uint Health { get; }
    public uint Horse { get; }
    public uint Shield { get; }
    public uint Track { get; }
}
