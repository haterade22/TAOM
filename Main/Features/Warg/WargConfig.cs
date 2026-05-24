namespace TAOM.Features.Warg;

public static class WargConfig
{
    internal static float WargAttackRange = 1f;
    // 2026-05-24: reduced 3.5f → 1.0f per user observation that running attacks
    // were initiating too early — at 8-10 m/s the warg traverses 5+ meters
    // during the 0.6s bone-check window, so a target detected at 3.5m is
    // already 1-2m PAST the warg by mid-bite. Restricting detection to 1m
    // means the BT only fires WargAttack when the target is genuinely in
    // bite range, increasing hit rate. See issue #219.
    internal static float TargetDetectionRange = 1.0f;
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
