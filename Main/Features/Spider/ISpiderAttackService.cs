using TAOM.Adapters;

namespace TAOM.Features.Spider;

public interface ISpiderAttackService
{
    /// <summary>True when the given Monster StringId is the giant spider (drives BT attach + mount-lock).</summary>
    bool IsSpiderMonster(string? monsterId);

    int CalculateSpiderBiteDamage(IAgentAdapter target, float velocity, float armorEffectivenessPercent);
    void HandleSpiderTargetHit(IAgentAdapter attacker, IAgentAdapter target, sbyte boneId);
    void SpiderAttack(IAgentAdapter spider);
}
