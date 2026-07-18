namespace TAOM.Features.BlowDiagnostics.Domain;

// Primitive snapshot of a blow + its victim, extracted at the Harmony boundary so the
// diagnostic service never touches sealed TaleWorlds types (ADR-007). Every field is
// best-effort: the hook fills what it can read behind `?.` and leaves the rest at the
// defaults below. This is a throwaway diagnostic DTO — mutable public fields on purpose,
// no immutability ceremony.
//
// InflictedDamage is deliberately the raw engine int. A NaN damage multiplier upstream
// (the CombatMechanics/career finiteness hypothesis) casts to int.MinValue on net472, so a
// bizarre value here is itself the signal — no separate finiteness hook needed.
public sealed class BlowDiagRecord
{
    public string VictimName = "?";
    public int VictimRace = -1;
    public bool VictimIsPlayer;
    public bool VictimIsMounted;
    public string MountMonster = "";     // mount Monster.StringId, "" when not mounted
    public float VictimHealth = float.NaN;
    public string BlowFlags = "";
    public string DamageType = "";
    public int InflictedDamage;
    public float BaseMagnitude;
    public bool IsMissile;
    public bool IsFallDamage;
    public string VictimBodyPart = "";
    public int AttackerIndex = -1;       // Blow.OwnerId — the attacker's Agent.Index
}
