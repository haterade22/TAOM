using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TAOM.Features.FactionUI.Campaign;

/// <summary>
/// The layer operations of the themed banner editor, on the banner the vanilla BannerEditorVM edits. Layer 0 is the background
/// pattern and stays first; every other layer is a sigil. The engine's own insert and remove calls (v1.5.3 Banner.cs:268-282)
/// leave the cached banner code in place, as do the BannerData setters, so each operation here clears it through
/// GetBannerDataAtIndex, the one public call that does without changing the banner.
/// </summary>
internal static class BannerLayerEditing
{
    /// <summary>The background plus Banner.MaxIconCount sigils: Banner.AddIconData refuses a 34th entry.</summary>
    internal const int MaxLayers = Banner.MaxIconCount + 1;
    internal const float Centre = Banner.BannerFullSize / 2f;
    internal const float DefaultSigilSize = 483f;
    internal const int MinSize = 16;

    internal static void Invalidate(Banner banner) => banner.GetBannerDataAtIndex(0);

    internal static bool CanAdd(Banner banner) => banner.GetBannerDataListCount() is > 0 and < MaxLayers;

    /// <summary>Inserts <paramref name="data"/> as the layer after <paramref name="afterIndex"/>, never before the background.</summary>
    /// <returns>The new layer's index, or -1 when the banner is full or has no background.</returns>
    internal static int Insert(Banner banner, BannerData data, int afterIndex)
    {
        if (!CanAdd(banner))
            return -1;
        int count = banner.GetBannerDataListCount();
        int index = Math.Max(1, Math.Min(afterIndex + 1, count));
        if (index == count)
            banner.AddIconData(data);
        else
            banner.AddIconData(data, index);
        Invalidate(banner);
        return index;
    }

    internal static int Duplicate(Banner banner, int index)
    {
        if (index < 1 || index >= banner.GetBannerDataListCount())
            return -1;
        return Insert(banner, new BannerData(banner.BannerDataList[index]), index);
    }

    internal static bool Remove(Banner banner, int index)
    {
        if (index < 1 || index >= banner.GetBannerDataListCount())
            return false;
        banner.RemoveIconDataAtIndex(index);
        Invalidate(banner);
        return true;
    }

    /// <summary>Moves a sigil <paramref name="delta"/> places; the background never moves and no sigil moves under it.</summary>
    /// <returns>The layer's index afterwards, unchanged when the move is not possible.</returns>
    internal static int Move(Banner banner, int index, int delta)
    {
        int count = banner.GetBannerDataListCount();
        int target = index + delta;
        if (index < 1 || index >= count || target < 1 || target >= count || target == index)
            return index;
        var data = banner.BannerDataList[index];
        banner.RemoveIconDataAtIndex(index);
        banner.AddIconData(data, target);
        Invalidate(banner);
        return target;
    }

    internal static void SwapColors(BannerData data) => (data.ColorId, data.ColorId2) = (data.ColorId2, data.ColorId);

    internal static void CentreLayer(BannerData data) => data.Position = new Vec2(Centre, Centre);

    internal static void ResetSize(BannerData data, bool isPattern)
    {
        float size = isPattern ? Banner.BannerFullSize : DefaultSigilSize;
        data.Size = new Vec2(size, size);
    }

    internal static int ClampPosition(int value) => Math.Max(0, Math.Min(Banner.BannerFullSize, value));

    internal static int ClampSize(int value) => Math.Max(MinSize, Math.Min(Banner.BannerFullSize, value));

    /// <summary>Whole degrees in 0..359, as the rotation slider shows them; BannerData keeps turns.</summary>
    internal static int ToDegrees(float turns)
    {
        int degrees = (int)Math.Round(turns * 360f) % 360;
        return degrees < 0 ? degrees + 360 : degrees;
    }

    internal static float ToTurns(int degrees) => (((degrees % 360) + 360) % 360) / 360f;
}

/// <summary>Undo and redo over whole banner codes: a banner of 33 layers is a few hundred characters.</summary>
internal sealed class BannerHistory
{
    internal const int Limit = 100;
    private readonly List<string> _codes = new List<string>();
    private int _index;

    public BannerHistory(string initial) => _codes.Add(initial);

    public string Current => _codes[_index];
    public bool CanUndo => _index > 0;
    public bool CanRedo => _index < _codes.Count - 1;

    /// <summary>Records <paramref name="code"/> as the newest state and drops the redo branch.</summary>
    /// <returns>False when the code is the current state, so a release without a change records nothing.</returns>
    public bool Record(string code)
    {
        if (code == Current)
            return false;
        _codes.RemoveRange(_index + 1, _codes.Count - _index - 1);
        _codes.Add(code);
        if (_codes.Count > Limit)
            _codes.RemoveAt(0);
        _index = _codes.Count - 1;
        return true;
    }

    public string? Undo() => CanUndo ? _codes[--_index] : null;

    public string? Redo() => CanRedo ? _codes[++_index] : null;
}
