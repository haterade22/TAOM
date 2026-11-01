using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.FordsOfWar.Hooks;

internal sealed partial class FordsTelemetryLogic
{
    // An enemy's melee blow on a tower body; the probe reads it and changes nothing (FordsTowerProbe).
    public override void OnRegisterBlow(Agent attacker, Agent victim, WeakGameEntity realHitEntity, Blow b, ref AttackCollisionData collisionData,
        in MissionWeapon attackerWeapon) => _probe.Register(attacker, victim, realHitEntity, in b, in collisionData, in attackerWeapon);
}
