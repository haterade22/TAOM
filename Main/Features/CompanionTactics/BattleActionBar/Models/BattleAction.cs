using System;

namespace TAOM.Features.CompanionTactics.BattleActionBar.Models;

/// <summary>
/// POCO describing a single contextual action button on the battle action bar.
/// The Execute callback runs in the View Model's UI thread; it must not block.
/// </summary>
public sealed class BattleAction
{
    public string Id { get; }
    public string Label { get; }
    public string Hotkey { get; }
    public ActionCategory Category { get; }
    public Action Execute { get; }

    public BattleAction(string id, string label, string hotkey, ActionCategory category, Action execute)
    {
        Id = id;
        Label = label;
        Hotkey = hotkey;
        Category = category;
        Execute = execute;
    }
}
