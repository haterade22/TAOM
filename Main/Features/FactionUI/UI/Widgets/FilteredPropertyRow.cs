using System;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.FactionUI.UI.Widgets;

/// <summary>
/// A face-generator slider row shown only when its bound <see cref="Name"/> equals
/// <see cref="ShowOnlyName"/>, or hidden when it equals <see cref="HideName"/> (#704, Kysaro's), so one
/// vanilla slider list can be split across his tabs. The slider names are the engine's localized
/// labels, so a filter written as <c>{=key}English</c> (vanilla's own key, e.g. <c>{=G6hYIR5k}Voice
/// Pitch:</c>) is resolved to the player's language before comparing; Kysaro's plain English filter
/// matched nothing in any other language. A trailing colon is ignored on both sides. The row decides
/// when its name or a filter is set (a language change re-binds the name), never per frame.
/// </summary>
public class FilteredPropertyRow : Widget
{
    private static readonly char[] TrimmedFromLabels = { ':', ' ', '\t' };

    private string? _name;
    private string? _showOnlyName;
    private string? _hideName;

    public FilteredPropertyRow(UIContext context)
        : base(context)
    {
    }

    [Editor(false)]
    public string? Name
    {
        get => _name;
        set
        {
            _name = value;
            Refresh();
        }
    }

    [Editor(false)]
    public string? ShowOnlyName
    {
        get => _showOnlyName;
        set
        {
            _showOnlyName = value;
            Refresh();
        }
    }

    [Editor(false)]
    public string? HideName
    {
        get => _hideName;
        set
        {
            _hideName = value;
            Refresh();
        }
    }

    private void Refresh()
    {
        if (string.IsNullOrEmpty(_name))
            return;

        var label = _name!.TrimEnd(TrimmedFromLabels);
        if (!string.IsNullOrEmpty(_showOnlyName))
            IsVisible = string.Equals(label, Label(_showOnlyName!), StringComparison.OrdinalIgnoreCase);
        else if (!string.IsNullOrEmpty(_hideName))
            IsVisible = !string.Equals(label, Label(_hideName!), StringComparison.OrdinalIgnoreCase);
    }

    private static string Label(string filter) =>
        (filter.StartsWith("{=", StringComparison.Ordinal) ? new TextObject(filter).ToString() : filter).TrimEnd(TrimmedFromLabels);
}
