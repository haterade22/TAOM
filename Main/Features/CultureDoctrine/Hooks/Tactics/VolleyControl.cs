using TAOM.Features.CultureDoctrine.Doctrines;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Hooks.Tactics;

/// <summary>
/// The archers' firing order, owned by the tactic that carries volley control and driven once a
/// second from its tick (<see cref="VolleyDecision"/>). Every vanilla archer behaviour resets
/// the order to fire-at-will when it activates, so the tactic re-asserts the hold each tick while
/// the enemy is out of the release range; when the tactic is cancelled or the archers change
/// hands the order is released, so a formation never inherits a hold from a plan that no longer
/// runs. The player's own formations are never written: <c>Formation.IsAIControlled</c> is the gate.
/// </summary>
public sealed class VolleyControl
{
    private Formation? _archers;
    private bool _firing = true;

    public string Status => _archers == null ? "" : _firing ? "Loose" : "Hold";

    /// <summary>One tick for the plan's archers. Returns true when the order changed.</summary>
    public bool Tick(Formation? archers, VolleyTunables tunables)
    {
        if (archers != _archers)
        {
            Release();
            _archers = archers;
            _firing = true;
        }
        if (archers == null)
            return false;
        if (!archers.IsAIControlled)
        {
            // The player took the formation: give it fire-at-will once (the engine's default,
            // and what the player's order panel expects), then leave it alone. This write and
            // the player's own run on threads the engine serialises (Mission.OnPreTick waits
            // for the async tick), so it is one plain write, never a race.
            if (!_firing)
            {
                _firing = true;
                archers.SetFiringOrder(FiringOrder.FiringOrderFireAtWill);
                return true;
            }
            return false;
        }
        var enemy = archers.QuerySystem.ClosestSignificantlyLargeEnemyFormation ?? archers.CachedClosestEnemyFormation;
        var distance = enemy == null ? float.MaxValue : archers.CachedAveragePosition.Distance(enemy.Formation.CachedMedianPosition.AsVec2);
        var fire = VolleyDecision.ShouldFire(_firing, enemy != null, distance, archers.QuerySystem.MissileRangeAdjusted, in tunables);
        var changed = fire != _firing;
        _firing = fire;
        // Re-asserted every tick: a behaviour activation in between set fire-at-will.
        archers.SetFiringOrder(fire ? FiringOrder.FiringOrderFireAtWill : FiringOrder.FiringOrderHoldYourFire);
        return changed;
    }

    /// <summary>Back to fire-at-will and forget the formation.</summary>
    public void Release()
    {
        var archers = _archers;
        if (archers != null && !_firing)
            archers.SetFiringOrder(FiringOrder.FiringOrderFireAtWill);
        _archers = null;
        _firing = true;
    }
}
