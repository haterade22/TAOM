using TAOM.Adapters;

namespace TAOM.Features.Warg;

public interface IWargAttackService
{
    int CalculateWargAttackDamage(IAgentAdapter target, float velocity, float armorEffectivenessPercent);
    void HandleWargTargetHit(IAgentAdapter attacker, IAgentAdapter target, sbyte boneId);
    void WargAttack(IAgentAdapter warg);
}
