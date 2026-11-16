using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.FactionUI.UI.Widgets;

/// <summary>
/// A text box whose empty-box hint is localized (#1385). Gauntlet shows a literal
/// <c>DefaultSearchText</c> as written, so a prefab writes
/// <c>LocalizedDefaultSearchText="{=key}English"</c> instead, resolved through TAOM's string tables
/// when the widget is built, as <see cref="FrontEndTextWidget"/> does for labels.
/// </summary>
public class FrontEndEditableTextWidget : EditableTextWidget
{
    private string _localizedDefaultSearchText = "";

    public FrontEndEditableTextWidget(UIContext context)
        : base(context)
    {
    }

    [Editor(false)]
    public string LocalizedDefaultSearchText
    {
        get => _localizedDefaultSearchText;
        set
        {
            if (_localizedDefaultSearchText == value)
                return;
            _localizedDefaultSearchText = value;
            DefaultSearchText = string.IsNullOrEmpty(value) ? "" : new TextObject(value).ToString();
        }
    }
}
