using System;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat.Hooks;

/// <summary>
/// The filter the mission logic applies to a missile hit before the run hears of it (phase3-plan.md section 4). A hit is read only when its missile's item id is a volley item, so a soldier's own arrows and
/// every melee blow are dropped here. It is friendly by the <b>victim's</b> side and never the attacker's: the owner may have died, or his slot been reused, while the arrow flew, and the native hit then
/// carries a null or a recycled attacker (A24), which is counted and logged and never trusted. A block (<c>AttackBlockedWithShield</c> or <c>MissileBlockedWithWeapon</c>) is kept apart on either side, because the engine tests it before the friendly-fire cancel (<c>Mission.cs:5823</c>, <c>:5861</c>
/// against <c>:5866-5888</c>): a defender's blocked arrow is never cancelled and does him no harm. A kill is a victim under 1 health after the
/// blow, since <c>HandleBlow</c> writes the health, raises the hit and only then calls <c>Die</c> when "Health &lt; 1f" (<c>Agent.cs:5513</c>, <c>:5527</c>, <c>:5529-5532</c>, E16). Pure: the logic passes primitives.
/// </summary>
internal static class LongDefeatVolleyHitFilter
{
    /// <summary>
    /// The hit as the run reads it, or null when it is not an arrow of a volley item. <paramref name="startingBaseSpeed"/> is the engine's own base speed for the hit (A25) and <paramref name="impactSpeed"/> the missile's speed at the hit: the engine scales a missile's damage by (impact speed over base speed) squared, and gives an arrow air drag, so the pair shows how much of the speed the flight lost.
    /// </summary>
    public static LdVolleyHit? Read(bool isMissile, string? itemId, Func<string, bool> isVolleyItem, bool victimIsDefender, bool blocked, float victimHealth, bool affectorKnown,
        bool affectorIsDefender, float startingBaseSpeed, float impactSpeed)
    {
        if (!isMissile || itemId == null || !isVolleyItem(itemId))
            return null;

        var kind = blocked ? LdVolleyHitKind.Blocked : victimIsDefender ? LdVolleyHitKind.Friendly : LdVolleyHitKind.Foe;
        var kill = kind == LdVolleyHitKind.Foe && victimHealth < 1f;
        return new LdVolleyHit(itemId, kind, kill, !affectorKnown, affectorKnown && !affectorIsDefender, startingBaseSpeed, impactSpeed);
    }

    /// <summary>A friendly hit the engine cancelled (<c>OnMissileHit</c> with <c>isCanceled</c>, E28; a cancelled hit never reaches <c>OnAgentHit</c>, A19), or null when the missile is not a volley item.</summary>
    public static LdVolleyHit? Cancelled(string? itemId, Func<string, bool> isVolleyItem) =>
        itemId != null && isVolleyItem(itemId) ? new LdVolleyHit(itemId, LdVolleyHitKind.Cancelled, false, false, false, 0f, 0f) : null;
}
