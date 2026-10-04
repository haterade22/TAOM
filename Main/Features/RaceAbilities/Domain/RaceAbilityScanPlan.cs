namespace TAOM.Features.RaceAbilities.Domain;

// What the sensor gathers for one decision, from RaceAbilityService.PlanScan. The default (all zero) scans
// nothing: no trigger can hold this pass.
public readonly struct RaceAbilityScanPlan
{
    public RaceAbilityScanPlan(float enemyRange, float closingRange, float kinRange, bool fallenKin)
    {
        EnemyRange = enemyRange;
        ClosingRange = closingRange;
        KinRange = kinRange;
        FallenKin = fallenKin;
    }

    // Enemies this close are sensed; 0 means no enemy scan.
    public float EnemyRange { get; }

    // A mounted enemy this close is checked for closing on the soldier (two engine reads each).
    public float ClosingRange { get; }

    // Kin this close are sensed; 0 means no ally scan.
    public float KinRange { get; }

    // Whether the remembered deaths of kin are walked.
    public bool FallenKin { get; }
}
