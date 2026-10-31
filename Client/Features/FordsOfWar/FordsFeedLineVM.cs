using TaleWorlds.Library;

namespace TAOM.Client.Features.FordsOfWar;

/// <summary>One line of the event feed.</summary>
public sealed class FordsFeedLineVM : FordsViewModel
{
    internal FordsFeedLineVM(string text)
    {
        Text = text;
    }

    [DataSourceProperty]
    public string Text { get; }
}
