using TAOM.Adapters;

namespace TAOM.Features.Spider;

public interface ISpiderAttackService
{
    int CalculateSpiderBiteDamage(IAgentAdapter target, float velocity, float armorEffectivenessPercent);
    void HandleSpiderTargetHit(IAgentAdapter attacker, IAgentAdapter target, sbyte boneId);
    void SpiderAttack(IAgentAdapter spider);
}
