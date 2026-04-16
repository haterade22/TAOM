using System.Collections.Generic;

namespace TAOM.Features.CareerSystem.Abilities;

public static class CareerAbilityBuffTracker
{
    private static readonly Dictionary<string, ActiveBuffs> _buffs = new Dictionary<string, ActiveBuffs>();
    private static readonly Dictionary<int, ActiveBuffs> _allyBuffs = new Dictionary<int, ActiveBuffs>();

    public static void SetBuff(string heroId, ActiveBuffs buffs) => _buffs[heroId] = buffs;
    public static ActiveBuffs GetBuff(string heroId) => _buffs.TryGetValue(heroId, out var b) ? b : null;
    public static void ClearBuff(string heroId) => _buffs.Remove(heroId);

    public static void SetAllyBuff(int agentIndex, ActiveBuffs buffs) => _allyBuffs[agentIndex] = buffs;
    public static ActiveBuffs GetAllyBuff(int agentIndex) => _allyBuffs.TryGetValue(agentIndex, out var b) ? b : null;
    public static void ClearAllyBuff(int agentIndex) => _allyBuffs.Remove(agentIndex);
    public static void ClearAllAllyBuffs() => _allyBuffs.Clear();

    public static void ClearAll()
    {
        _buffs.Clear();
        _allyBuffs.Clear();
    }
}

public class ActiveBuffs
{
    public float SpeedMultiplier { get; set; } = 0f;
    public float CombatSpeedMultiplier { get; set; } = 0f;
    public float DamageBonus { get; set; } = 0f;
    public float ArmorReduction { get; set; } = 0f;
    public float DrawSpeedBonus { get; set; } = 0f;
    public float MountSpeedBonus { get; set; } = 0f;
    public float ChargeDamageBonus { get; set; } = 0f;
    public float DamageReductionBonus { get; set; } = 0f;
    public float ExpiresAt { get; set; }
    public bool IsExpired(float currentTime) => currentTime >= ExpiresAt;
}
