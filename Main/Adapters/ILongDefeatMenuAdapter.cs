using System;
using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>
/// One row of the Long Defeat prompt. <paramref name="Label"/> is raw <c>{=KEY}English</c>; <paramref name="Variables"/> fill its
/// <c>{NAME}</c> slots and are themselves raw text, so a fortress name arrives as its key and a count as digits.
/// </summary>
internal sealed record LongDefeatChoice(string Id, string Label, IReadOnlyDictionary<string, string>? Variables = null);

/// <summary>
/// The engine's main-menu surface the Long Defeat start path uses: one initial-state option, a single-choice prompt, a notice
/// and the way back to the menu. Texts arrive as raw <c>{=KEY}English</c> and are resolved here.
/// </summary>
internal interface ILongDefeatMenuAdapter
{
    /// <summary>True when an initial-state option with this id is registered. False when there is no module (a test host).</summary>
    bool HasOption(string id);

    /// <summary>Adds a main-menu option. <paramref name="isHidden"/> is asked each time the menu draws.</summary>
    void AddOption(string id, string label, string hint, int orderIndex, Func<bool> isHidden, Action onChosen);

    /// <summary>
    /// Shows a one-of-many prompt with a Choose and a Cancel button; <paramref name="onPicked"/> runs only on Choose.
    /// </summary>
    void Choose(string title, string description, IReadOnlyList<LongDefeatChoice> choices, Action<string> onPicked);

    /// <summary>Shows a notice the player closes.</summary>
    void Notify(string message);

    /// <summary>Ends the running game and shows the main menu: <c>MBGameManager.EndGame</c>, what the escape menu's "Exit to Main Menu" calls.</summary>
    void EndGame();
}
