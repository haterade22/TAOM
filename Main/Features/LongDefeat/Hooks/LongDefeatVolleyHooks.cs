using TAOM.Features.LongDefeat.Domain;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.LongDefeat.Hooks;

/// <summary>
/// The Elven Volley's hit callbacks (phase3-plan.md Slice 6), read from the engine and forwarded to the run host. Both may run off the main thread (A21), so each asks the host's volatile window flag before
/// anything else: a wave with no volley in flight costs one read per hit. They read and forward; the director parks the hit on the main thread. They live here, not in the logic, which the entry-point gate
/// keeps under 150 lines (ADR-002).
/// </summary>
internal static class LongDefeatVolleyHooks
{
    // OnAgentHit carries the missile's weapon, so the item id (Mission.cs:5646, :5657-5659); a cancelled friendly hit never gets here (A19). The victim's health is read after the blow, which HandleBlow
    // writes before it raises the hit and only then calls Die under 1 health (Agent.cs:5513, :5527, :5529-5532), so a victim under 1 is a kill.
    internal static void AgentHit(LongDefeatRunHost host, Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon, in Blow blow, in AttackCollisionData collisionData)
    {
        if (!host.WantsVolleyHits || affectedAgent == null || !affectedAgent.IsHuman)
            return;

        var hit = LongDefeatVolleyHitFilter.Read(blow.IsMissile, affectorWeapon.Item?.StringId, host.IsVolleyItem, affectedAgent.Team?.Side == BattleSideEnum.Defender,
            collisionData.AttackBlockedWithShield || collisionData.MissileBlockedWithWeapon, affectedAgent.Health, affectorAgent != null, affectorAgent?.Team?.Side == BattleSideEnum.Defender,
            collisionData.MissileStartingBaseSpeed, collisionData.MissileVelocity.Length);
        if (hit is { } read)
            host.OnVolleyHit(read);
    }

    // OnMissileHit is raised for every missile hit, a cancelled friendly one included (Mission.cs:6023-6026, E28), and carries no weapon: the missile is found by the index the hit carries.
    internal static void MissileHit(LongDefeatRunHost host, Mission mission, Agent victim, bool isCanceled, in AttackCollisionData collisionData)
    {
        if (!isCanceled || !host.WantsVolleyHits || victim == null || !victim.IsHuman || victim.Team?.Side != BattleSideEnum.Defender)
            return;

        if (LongDefeatVolleyHitFilter.Cancelled(MissileItem(mission, collisionData.AffectorWeaponSlotOrMissileIndex), host.IsVolleyItem) is { } cancelled)
            host.OnVolleyHit(cancelled);
    }

    // The list is the main thread's to change, so an off-thread read can meet a change under it; that is no hit to count, not a fault to raise from an engine callback.
    private static string? MissileItem(Mission mission, int index)
    {
        try
        {
            foreach (var missile in mission.MissilesList)
            {
                if (missile.Index == index)
                    return missile.Weapon.Item?.StringId;
            }
        }
        catch (System.InvalidOperationException)
        {
            // swallow-ok: an off-thread read of the main thread's missile list can meet a change under it; the hit is then not counted, and an engine callback must not throw.
        }

        return null;
    }
}
