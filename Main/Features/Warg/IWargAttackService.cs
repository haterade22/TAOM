using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Warg;

public interface IWargAttackService
{
    int CalculateWargAttackDamage(Agent target, float velocity);
    void HandleWargTargetHit(Agent attacker, Agent target, sbyte boneId);
    void WargAttack(Agent warg);
}
