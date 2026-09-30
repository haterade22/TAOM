using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TAOM.Features.RealmBorders.Domain;

namespace TAOM.Features.RealmBorders.UI;

/// <summary>
/// The lettered realm names over the campaign map. Rebuilt when the labels change; every frame each
/// name is projected to the screen and faded with the borders. The projection is passed in by the
/// map view, which owns the camera.
/// </summary>
public sealed class RealmNamesVM : ViewModel
{
    /// <summary>Names show only once the borders are this opaque, so they never float over a close-up.</summary>
    public const float MinimumAlpha = 0.35f;

    private readonly List<(RealmNameItemVM Item, RealmLabel Label)> _placed = new List<(RealmNameItemVM Item, RealmLabel Label)>();
    private MBBindingList<RealmNameItemVM> _items = new MBBindingList<RealmNameItemVM>();
    private IReadOnlyList<RealmLabel> _labels = Array.Empty<RealmLabel>();

    [DataSourceProperty]
    public MBBindingList<RealmNameItemVM> Items
    {
        get => _items;
        set
        {
            if (_items == value)
                return;
            _items = value;
            OnPropertyChangedWithValue(value, nameof(Items));
        }
    }

    /// <summary>Replaces the names when the labels change; cheap to call with the same list.</summary>
    public void SetLabels(IReadOnlyList<RealmLabel> labels, Func<string, string> nameOf)
    {
        if (ReferenceEquals(labels, _labels))
            return;
        _labels = labels;
        _placed.Clear();
        var items = new MBBindingList<RealmNameItemVM>();
        foreach (var label in labels)
        {
            var (text, aniron) = RealmNameLettering.Letter(nameOf(label.Realm));
            if (text.Length == 0)
                continue;
            var item = new RealmNameItemVM { Name = text, UseTolkienFont = aniron };
            items.Add(item);
            _placed.Add((item, label));
        }
        Items = items;
    }

    /// <summary>
    /// Places every name for this frame. <paramref name="project"/> maps a label to screen pixels and
    /// says whether it is in front of the camera; the offsets centre each full-screen label on its point.
    /// </summary>
    public void Place(float alpha, float screenWidth, float screenHeight, Func<RealmLabel, (bool Visible, float X, float Y)> project)
    {
        bool shown = alpha >= MinimumAlpha;
        foreach (var (item, label) in _placed)
        {
            if (!shown)
            {
                item.IsShown = false;
                continue;
            }
            var (visible, x, y) = project(label);
            item.IsShown = visible;
            if (!visible)
                continue;
            item.OffsetX = x - screenWidth / 2f;
            item.OffsetY = y - screenHeight / 2f;
            item.Alpha = Math.Min(1f, (alpha - MinimumAlpha) / (1f - MinimumAlpha) + 0.25f);
        }
    }
}
