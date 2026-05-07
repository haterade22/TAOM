using System.Collections.Generic;
using TAOM.Features.CompanionTactics.BattleActionBar.Models;

namespace TAOM.Features.CompanionTactics.BattleActionBar;

/// <summary>
/// In-memory per-formation stance tracker. Display-only: stances do NOT alter formation
/// behavior in v1.3.15 (the underlying engine APIs are not exposed). Mission lifecycle:
/// MissionView.OnMissionScreenFinalize calls <see cref="ClearAllStances"/>.
///
/// Sentinel/terminal collision: <see cref="GetStance"/> returns <see cref="TroopStance.None"/>
/// for both "never set" AND "explicitly cleared" — that is INTENTIONAL. The state machine
/// is non-progressive; there is no "completion" branch keyed off stance == None, so the
/// observation matrix from harmony-patches.md does not apply (no terminal-state action).
/// </summary>
public sealed class TroopStanceManager : ITroopStanceManager
{
    private readonly Dictionary<int, TroopStance> _stances = new();

    public void SetStance(int formationIndex, TroopStance stance)
    {
        // Same-stance toggle: setting the currently-active stance clears it.
        if (_stances.TryGetValue(formationIndex, out var existing) && existing == stance)
        {
            _stances.Remove(formationIndex);
            return;
        }
        _stances[formationIndex] = stance;
    }

    public TroopStance GetStance(int formationIndex) =>
        _stances.TryGetValue(formationIndex, out var s) ? s : TroopStance.None;

    public void ClearStance(int formationIndex) => _stances.Remove(formationIndex);

    public void ClearAllStances() => _stances.Clear();
}
