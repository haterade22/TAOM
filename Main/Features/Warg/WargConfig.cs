namespace TAOM.Features.Warg;

public static class WargConfig
{
    internal static float WargAttackRange = 1f;
    // 2026-05-24: restored to 20f to match the original LOTRAOM value. This was
    // hardcoded as `float targetDetectionRange = 20f;` inside WargLogic.WargAttack
    // in LOTRAOM (WargLogic.cs:80). When TAOM extracted it into config during the
    // v1.4.5 refactor the default was reset to 3.5f, costing the BT enough lead
    // time to fire WargAttack while the warg was still closing distance on a
    // running enemy at 8-10 m/s. Earlier session experiments (1.0f, then 3.5f)
    // were compensating for the BoneCheck `_maxRangeForCheck` gate bug (fixed
    // in BoneCheckDuringAnimation 5f63606); with that gate fixed AND the 10-bone
    // impact cone (8a5c89f), the original 20f is the right value. See issue #219.
    internal static float TargetDetectionRange = 20f;
    internal static int SleepAfterAttack = 3;
    public const float SpeedForMaxDamage = 20f;
    public const int MaxSpeedDamage = 20;
    public const int MaxBaseDamage = 40;
    public const int DamageToFlinch = 10;
    public const int DamageToFall = 40;
    public const int minDamageReceivedForRage = 10;
    public const double rageChance = 0.1;
    public const int maxDistanceFromWargToRollForRage = 20;
    public const int minRageAttacks = 2;
    public const int maxRageAttacks = 3;
}
