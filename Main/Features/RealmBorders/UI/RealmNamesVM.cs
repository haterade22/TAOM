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

    private readonly List<string> _rawNames = new List<string>();
    private int _nameVersion;

    /// <summary>
    /// Replaces the names when the labels change. With the same list it re-letters, in place, any realm whose name
    /// changed when <paramref name="nameVersion"/> moved on (<see cref="RealmBorderService.NameVersion"/>: a kingdom
    /// renamed in play, Erebor as Durin's Folk), and is otherwise two comparisons.
    /// </summary>
    public void SetLabels(IReadOnlyList<RealmLabel> labels, Func<string, string> nameOf, int nameVersion = 0)
    {
        if (ReferenceEquals(labels, _labels))
        {
            if (nameVersion != _nameVersion)
            {
                _nameVersion = nameVersion;
                RefreshNames(nameOf);
            }
            return;
        }
        _labels = labels;
        _nameVersion = nameVersion;
        _placed.Clear();
        _rawNames.Clear();
        var items = new MBBindingList<RealmNameItemVM>();
        foreach (var label in labels)
        {
            var raw = nameOf(label.Realm);
            var (text, aniron) = RealmNameLettering.Letter(raw);
            if (text.Length == 0)
                continue;
            var item = new RealmNameItemVM { Name = text, UseTolkienFont = aniron };
            items.Add(item);
            _placed.Add((item, label));
            _rawNames.Add(raw);
        }
        Items = items;
    }

    private void RefreshNames(Func<string, string> nameOf)
    {
        for (var i = 0; i < _placed.Count; i++)
        {
            var raw = nameOf(_placed[i].Label.Realm);
            if (string.Equals(raw, _rawNames[i], StringComparison.Ordinal))
                continue;
            var (text, aniron) = RealmNameLettering.Letter(raw);
            if (text.Length == 0)
                continue; // keep the old name rather than leave a realm unnamed
            _rawNames[i] = raw;
            _placed[i].Item.Name = text;
            _placed[i].Item.UseTolkienFont = aniron;
        }
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
