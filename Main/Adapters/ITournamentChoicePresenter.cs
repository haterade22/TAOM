using System;
using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>
/// Boundary over the multi-selection inquiry and its <c>TextObject</c> text (ADR-007) for the tournament Join
/// flow. Every callback receives the picked id; closing a dialog calls the cancel action.
/// </summary>
public interface ITournamentChoicePresenter
{
    /// <summary>Pick one prize by item id; the first id is the advertised prize.</summary>
    void ShowPrizeChoice(IReadOnlyList<string> itemIds, Action<string> onPicked, Action onCancel);

    /// <summary>Pick the combat skill the tournament trains, by skill id.</summary>
    void ShowSkillChoice(IReadOnlyList<string> skillIds, Action<string> onPicked, Action onCancel);

    /// <summary>
    /// A message line naming the trained skill. It shows no number: the engine scales the XP by the hero's learning
    /// rate (HeroDeveloper.AddSkillXp, v1.5.3), so the amount awarded is not the amount the skill gains.
    /// </summary>
    void ShowSkillTrained(string skillId);
}
