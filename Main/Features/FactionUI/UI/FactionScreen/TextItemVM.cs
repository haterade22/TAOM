using TaleWorlds.Library;

namespace TAOM.Features.FactionUI.UI.FactionScreen;

/// <summary>One line of a faction's strengths or weaknesses (#704, Kysaro's).</summary>
public sealed class TextItemVM : ViewModel
{
    public TextItemVM(string text)
    {
        Text = text;
    }

    [DataSourceProperty]
    public string Text { get; }
}
