using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.FactionUI.UI.Widgets;

/// <summary>The rich-text counterpart of <see cref="FrontEndTextWidget"/> (#704).</summary>
public class FrontEndRichTextWidget : RichTextWidget
{
    private string _localizedText = "";

    public FrontEndRichTextWidget(UIContext context)
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
