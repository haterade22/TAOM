namespace TAOM.Features.CombatMechanics.Domain;

// Boundary DTO for the weight-driven charge-knockdown decision (ADR-007). This struct is the
// designated extension point for future factors (user list, 2026-07-02): collision-angle dot,
// attacker/victim troop tier, attacker race, perks on either side. Add a field + one extractor
// line in the model when a factor becomes real — the service formula changes, the boundary shape
// doesn't.
public readonly struct ChargeKnockdownContext
{
    public readonly bool IsHorseCharge;
    public readonly float ChargeVelocity;

    // Monster.Weight of the charging mount / its rider / the victim (engine default 1 when the
    // XML attribute is absent; rider 0 when riderless).
    public readonly int ChargerWeight;
    public readonly int RiderWeight;
    public readonly int VictimWeight;

    public readonly int? VictimRaceId;
    public readonly float VictimMaxHealth;
    public readonly float InflictedDamage;

    // AgentStatCalculateModel.GetKnockDownResistance(victim) — extracted at the boundary so the
    // registered stat model (career effects included) feeds the decision.
    public readonly float VictimKnockDownResistance;

    // Charger Monster.RelativeSpeedLimitForCharge — float.MaxValue when the XML attribute is
    // absent (all humanoids); the service substitutes DefaultChargeSpeedReference.
    public readonly float ChargerSpeedLimitForCharge;

    public readonly bool HasShrugOffFlag;
    public readonly bool HasKnockBackFlag;

    public ChargeKnockdownContext(
        bool isHorseCharge,
        float chargeVelocity,
        int chargerWeight,
        int riderWeight,
        int victimWeight,
        int? victimRaceId,
        float victimMaxHealth,
        float inflictedDamage,
        float victimKnockDownResistance,
        float chargerSpeedLimitForCharge,
        bool hasShrugOffFlag,
        bool hasKnockBackFlag)
    {
        IsHorseCharge = isHorseCharge;
        ChargeVelocity = chargeVelocity;
        ChargerWeight = chargerWeight;
        RiderWeight = riderWeight;
        VictimWeight = victimWeight;
        VictimRaceId = victimRaceId;
        VictimMaxHealth = victimMaxHealth;
        InflictedDamage = inflictedDamage;
        VictimKnockDownResistance = victimKnockDownResistance;
        ChargerSpeedLimitForCharge = chargerSpeedLimitForCharge;
        HasShrugOffFlag = hasShrugOffFlag;
        HasKnockBackFlag = hasKnockBackFlag;
    }
}
