using System;

namespace TAOM.Features.FactionUI.Campaign;

/// <summary>One stat cell in panel pixels. <see cref="Top"/> is relative to its tier plate's offset;
/// the icon sits at the cell's top centre and the value box <see cref="ValueTop"/> below that.</summary>
public readonly struct CampaignStatCell
{
    public CampaignStatCell(float x, float top, float width, float height, float iconSize, float valueTop, float valueHeight)
    {
        X = x;
        Top = top;
        Width = width;
        Height = height;
        IconSize = iconSize;
        ValueTop = valueTop;
        ValueHeight = valueHeight;
    }

    public float X { get; }
    public float Top { get; }
    public float Width { get; }
    public float Height { get; }
    public float IconSize { get; }
    public float ValueTop { get; }
    public float ValueHeight { get; }
}

/// <summary>
/// Where the native map-bar stats sit on the stepped two-tier stats art (#704). The upper tier keeps its
/// approved fixed columns. The lower tier centres whatever lower stats are shown, across both native
/// repeaters, on the stone between the lower plate's leaf stem and its right bevel, tightening its pitch
/// only when a fifth stat (the culture currency) needs the room.
/// </summary>
public static class CampaignStatsLayout
{
    public const int FirstLowerSlot = 5;
    public const int LastSlot = 9;

    // Panel x of the lower plate's usable stone (cb_stats_lower_join_fixed drawn 356 wide from x=100).
    public const float LowerContentLeft = 156f;
    public const float LowerContentRight = 438f;
    public const float LowerMaxPitch = 68f;
    public const float LowerCellGap = 4f;

    // Below the upper plate's bottom border (JoinBorder ends 90 px into the expanded panel, the lower
    // plate starts at 74) and clear of the lower gold border.
    private const float LowerTop = 18f;
    private const float LowerHeight = 62f;
    private const float LowerIconSize = 38f;
    private const float LowerValueTop = 39f;
    private const float LowerValueHeight = 22f;

    private const int LowerSlotsMask = ((1 << (LastSlot + 1)) - 1) & ~((1 << FirstLowerSlot) - 1);

    public static int Slot(string? id)
    {
        switch (id)
        {
            case "gold": return 0;
            case "speed": case "speed_at_sea": return 1;
            case "hit_points": case "hit_points_sick": return 2;
            case "troops": return 3;
            case "food": return 4;
            case "morale": return 5;
            case "influence": return 6;
            case "view_distance": return 7;
            case "troop_wage": return 8;
            case "special_resource": return 9;
            default: return -1;
        }
    }

    public static bool IsLower(int slot) => slot >= FirstLowerSlot && slot <= LastSlot;

    /// <summary>Adds a shown stat to the set of displayed lower slots; upper and unknown ids add nothing.</summary>
    public static int Include(int displayedLowerMask, string? statId)
    {
        int slot = Slot(statId);
        return IsLower(slot) ? displayedLowerMask | (1 << slot) : displayedLowerMask;
    }

    public static CampaignStatCell Upper(int slot) =>
        new CampaignStatCell(62f + slot * 70f, 16f, 68f, 72f, 42f, 36f, 26f);

    /// <summary>The cell of a lower slot, given every lower slot currently shown.</summary>
    public static CampaignStatCell Lower(int slot, int displayedLowerMask)
    {
        int mask = (displayedLowerMask | (1 << slot)) & LowerSlotsMask;
        int count = BitCount(mask);
        int column = BitCount(mask & ((1 << slot) - 1));
        float pitch = Math.Min(LowerMaxPitch, (float)Math.Floor((LowerContentRight - LowerContentLeft + LowerCellGap) / count));
        float width = pitch - LowerCellGap;
        float row = count * pitch - LowerCellGap;
        float start = (float)Math.Floor((LowerContentLeft + LowerContentRight - row) / 2f);
        return new CampaignStatCell(start + column * pitch, LowerTop, width, LowerHeight, LowerIconSize, LowerValueTop, LowerValueHeight);
    }

    private static int BitCount(int value)
    {
        int count = 0;
        for (; value != 0; value &= value - 1)
            count++;
        return count;
    }
}
