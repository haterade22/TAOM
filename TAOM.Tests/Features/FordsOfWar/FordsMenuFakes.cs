using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Features.FordsOfWar;

namespace TAOM.Tests.Features.FordsOfWar;

/// <summary>
/// A scripted main menu: records every prompt, answers each from a queue of ids, and treats an exhausted queue as the player
/// pressing Cancel (the callback is never called).
/// </summary>
internal sealed class FakeFordsMenu : IFordsMenuAdapter
{
    internal sealed record Prompt(string Title, string Description, IReadOnlyList<FordsChoice> Choices);
    internal sealed record Option(string Id, string Label, string Hint, int Order, Func<bool> IsHidden, Action OnChosen);

    private readonly Queue<string> _answers;

    public FakeFordsMenu(params string[] answers) => _answers = new Queue<string>(answers);

    public List<Prompt> Prompts { get; } = new();
    public List<string> Messages { get; } = new();
    public List<MBGameManager> Started { get; } = new();
    public Dictionary<string, Option> Options { get; } = new();
    public int AddCalls { get; private set; }
    public int EndGames { get; private set; }

    public bool HasOption(string id) => Options.ContainsKey(id);

    public void AddOption(string id, string label, string hint, int orderIndex, Func<bool> isHidden, Action onChosen)
    {
        AddCalls++;
        Options[id] = new Option(id, label, hint, orderIndex, isHidden, onChosen);
    }

    public void Choose(string title, string description, IReadOnlyList<FordsChoice> choices, Action<string> onPicked)
    {
        Prompts.Add(new Prompt(title, description, choices));
        if (_answers.Count > 0)
            onPicked(_answers.Dequeue());
    }

    public void Notify(string message) => Messages.Add(message);

    public void StartGame(MBGameManager manager) => Started.Add(manager);

    public void EndGame() => EndGames++;
}

/// <summary>An <c>MBGameManager</c> that was never constructed (its constructor reads native config); only its identity matters.</summary>
internal sealed class StubGameManager : MBGameManager
{
    protected override void DoLoadingForGameManager(GameManagerLoadingSteps gameManagerLoadingStep, out GameManagerLoadingSteps nextStep) =>
        nextStep = GameManagerLoadingSteps.None;

    public override void OnAfterCampaignStart(Game game)
    {
    }

    internal static StubGameManager Create() => (StubGameManager)FormatterServices.GetUninitializedObject(typeof(StubGameManager));
}
