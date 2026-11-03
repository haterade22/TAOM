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

    private float _age;

    /// <summary>Adds to the line's age and returns it.</summary>
    internal float Age(float dt) => _age += dt;
}
