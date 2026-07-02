namespace TAOM.Features.CombatMechanics.Domain;

// One row of the per-race combat-modifier table (combat_mechanics_config.json → raceModifiers).
// Open table by design: new flavor ("tree spirits dig their feet in") is a data row, not code.
public class RaceCombatModifiers
{
    // Skill points added to the attacker's side of the crush-through skill delta.
    public float CtbAttackBonus { get; set; }

    // Skill points added to the defender's side of the crush-through skill delta.
    public float CtbDefenseBonus { get; set; }

    // Multiplies the victim's knockdown resistance in the charge-knockdown decision (dwarf 2.5).
    public float KnockdownResistanceMultiplier { get; set; } = 1f;

    // Multiplies CalculateStaggerThresholdDamage; feeds vanilla shrug-off via the registered model.
    public float StaggerThresholdMultiplier { get; set; } = 1f;

    // Elves strike true from any angle — skip the non-overhead crush-through penalty.
    public bool RemoveNonOverheadPenalty { get; set; }

    // "Brute": fraction added to effective swing energy inside the crush-through decision
    // (decision bias only — never a native momentum change).
    public float SwingEnergyBonusFactor { get; set; }

    public static readonly RaceCombatModifiers Neutral = new RaceCombatModifiers();
}
