using System;
using System.Collections.Generic;
using TAOM.Adapters;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// A scripted main menu: records what the Long Defeat start path asks of it, answers each prompt from a queue of ids, and treats an
/// exhausted queue as the player pressing Cancel (the pick callback is never called).
/// </summary>
internal sealed class FakeLongDefeatMenu : ILongDefeatMenuAdapter
{
    internal sealed record Prompt(string Title, string Description, IReadOnlyList<LongDefeatChoice> Choices);
    internal sealed record Option(string Id, string Label, string Hint, int Order, Func<bool> IsHidden, Action OnChosen);

    private readonly Queue<string> _answers;

    public FakeLongDefeatMenu(params string[] answers) => _answers = new Queue<string>(answers);

    public List<string> Messages { get; } = new();
    public List<Prompt> Prompts { get; } = new();
    public Dictionary<string, Option> Options { get; } = new();
    public int AddCalls { get; private set; }
    public int EndGames { get; private set; }
    public bool NotifyThrows { get; set; }
    public bool ChooseThrows { get; set; }

    /// <summary>When above 0, a prompt shown after this many earlier ones throws (the second question of a start flow).</summary>
    public int ChooseThrowsAfter { get; set; }

    public bool HasOption(string id) => Options.ContainsKey(id);

    public void AddOption(string id, string label, string hint, int orderIndex, Func<bool> isHidden, Action onChosen)
    {
        AddCalls++;
        Options[id] = new Option(id, label, hint, orderIndex, isHidden, onChosen);
    }

    public void Choose(string title, string description, IReadOnlyList<LongDefeatChoice> choices, Action<string> onPicked)
    {
        if (ChooseThrows || (ChooseThrowsAfter > 0 && Prompts.Count >= ChooseThrowsAfter))
            throw new InvalidOperationException("the menu could not show the prompt");

        Prompts.Add(new Prompt(title, description, choices));
        if (_answers.Count > 0)
            onPicked(_answers.Dequeue());
    }

    public void Notify(string message)
    {
        if (NotifyThrows)
            throw new InvalidOperationException("the menu could not show the notice");
        Messages.Add(message);
    }

    public void EndGame() => EndGames++;
}
