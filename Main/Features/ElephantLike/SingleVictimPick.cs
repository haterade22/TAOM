using TAOM.Core.Validation;

namespace TAOM.Features.ElephantLike;

/// <summary>
/// Pure running choice of ONE victim for an elephant-like attack whose profile sets
/// <see cref="BehaviorTreeElements.ElephantLikeCombatProfile.SingleTarget"/> (the war ram's head-butt, #618). The
/// attack task offers every live enemy in its scan, in order, with its facing dot (normalised toEnemy .
/// lookDirection) and distance, and keeps the agent whenever <see cref="Offer"/> returns true. The enemy faced most
/// squarely wins, the nearer one breaks a tie, the first one keeps a full tie. It is the rule
/// <c>ElephantLikeEngageDecorator</c> used to commit the attack, applied to the full damage radius rather than the
/// trigger range, so the victim is usually, not always, the enemy the creature lowered its head at. Engine-sourced
/// floats: a candidate whose dot or distance is not finite never wins and never disturbs the current pick.
/// </summary>
public struct SingleVictimPick
{
    private bool _any;
    private float _dot;
    private float _distance;

    /// <summary>True when this candidate becomes the pick.</summary>
    public bool Offer(float facingDot, float distance)
    {
        if (!FiniteFloatValidator.IsFinite(facingDot) || !FiniteFloatValidator.IsFinite(distance)) return false;
        if (_any && !(facingDot > _dot || (facingDot == _dot && distance < _distance))) return false;
        _any = true;
        _dot = facingDot;
        _distance = distance;
        return true;
    }
}
