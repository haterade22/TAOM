using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.FactionUI.UI.Widgets;

/// <summary>
/// A text widget whose fixed text is localized (#704). Gauntlet shows a literal <c>Text</c> attribute as
/// written, so Kysaro's hard-coded labels ("IDENTITY", "Back") would stay English in every language;
/// these prefabs write <c>LocalizedText="{=key}English"</c> instead, resolved through TAOM's string
/// tables when the widget is built, item templates included.
/// </summary>
public class FrontEndTextWidget : TextWidget
{
    private string _localizedText = "";

    public FrontEndTextWidget(UIContext context)
        : base(context)
    {
    }

    [Editor(false)]
    public string LocalizedText
    {
        get => _localizedText;
        set
        {
            if (_localizedText == value)
                return;
            _localizedText = value;
            Text = string.IsNullOrEmpty(value) ? "" : new TextObject(value).ToString();
        }
    }
}
