using System;
using System.Collections.Generic;
using TAOM.Features.FordsOfWar;
using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

/// <summary>
/// The engine's main-menu surface the Fords of War start flow uses: one initial-state option, a single-choice prompt, a short
/// notice, the game start and the way back to the menu. Every text arrives as raw <c>{=KEY}English</c> and is resolved here. Internal because the prompt
/// rows are internal types.
/// </summary>
internal interface IFordsMenuAdapter
{
    /// <summary>True when an initial-state option with this id is registered. False when there is no module (a test host).</summary>
    bool HasOption(string id);

    /// <summary>Adds a main-menu option. <paramref name="isHidden"/> is asked each time the menu draws.</summary>
    void AddOption(string id, string label, string hint, int orderIndex, Func<bool> isHidden, Action onChosen);

    /// <summary>
    /// Shows a one-of-many prompt with a Choose and a Cancel button; <paramref name="onPicked"/> runs only on Choose and
    /// <paramref name="onCancelled"/> (when given) on Cancel.
    /// </summary>
    void Choose(string title, string description, IReadOnlyList<FordsChoice> choices, Action<string> onPicked, Action? onCancelled = null);

    /// <summary>Shows a notice the player closes; <paramref name="onOk"/> (when given) runs when they press Ok.</summary>
    void Notify(string message, Action? onOk = null);

    /// <summary>Starts loading the game: <c>MBGameManager.StartNewGame</c>.</summary>
    void StartGame(MBGameManager manager);

    /// <summary>Ends the running game and shows the main menu: <c>MBGameManager.EndGame</c>, what the escape menu's "Exit to Main Menu" calls.</summary>
    void EndGame();
}
