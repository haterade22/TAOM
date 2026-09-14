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
///
/// Every method takes one lock (#595). <c>Patch35</c> clears a stance from
/// <c>Formation.SetMovementOrder</c>, which the engine calls on its asynchronous agent tick for
/// the player's own team whenever its formations are AI-controlled (<c>Team.Tick</c>'s retreat
/// branch, <c>Formation.Tick</c>'s substitute orders), while the action bar reads and writes
/// from the main thread. An unsynchronised <c>Dictionary</c> mutated from two threads can spin
/// forever on its next lookup, which is how #592 froze the game.
/// </summary>
public sealed class TroopStanceManager : ITroopStanceManager
{
    private readonly object _gate = new();
    private readonly Dictionary<int, TroopStance> _stances = new();

    public void SetStance(int formationIndex, TroopStance stance)
    {
        lock (_gate)
        {
            // Same-stance toggle: setting the currently-active stance clears it.
            if (_stances.TryGetValue(formationIndex, out var existing) && existing == stance)
            {
                _stances.Remove(formationIndex);
                return;
            }
            _stances[formationIndex] = stance;
        }
    }

    public TroopStance GetStance(int formationIndex)
    {
        lock (_gate)
            return _stances.TryGetValue(formationIndex, out var s) ? s : TroopStance.None;
    }

    public void ClearStance(int formationIndex)
    {
        lock (_gate) _stances.Remove(formationIndex);
    }

    public void ClearAllStances()
    {
        lock (_gate) _stances.Clear();
    }
}
