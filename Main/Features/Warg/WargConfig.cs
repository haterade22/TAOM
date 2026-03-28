namespace TAOM.Features.Warg;

public static class WargConfig
{
    internal static float WargAttackRange = 1f;
    internal static float TargetDetectionRange = 3.5f;
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
