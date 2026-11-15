using System;
using System.Collections.Generic;
using TAOM.Features.CheatMenu;
using TAOM.Features.CheatMenu.Battle;

namespace TAOM.Adapters;

/// <summary>Where the battle cheat menu shows: the game's single-choice pop-up list, and the outcome message after a pick.</summary>
public interface IBattleCheatHostAdapter
{
    void ShowList(IReadOnlyList<BattleCheatAction> actions, Action<BattleCheatAction> onPicked);

    void ShowOutcome(BattleCheatAction action, CheatOutcome outcome);
}
