namespace TAOM.Features.BanditManagement;

public class BanditScalingConfig
{
    public float DensityCurve { get; set; } = 1.5f;
    public float PartySizeCurve { get; set; } = 1.5f;
    public float BossFightCurve { get; set; } = 1.5f;
    public int MaxHideoutsPerFactionCap { get; set; } = 100;
    public int MaxPartiesPerHideoutCap { get; set; } = 3;
    public int MinPartiesToInfest { get; set; } = 1;

    // Hideouts each bandit faction starts with on a new campaign (vanilla = 7). This is the
    // early-game density lever: the spawn behaviour fills this many hideouts per faction at
    // new-game init, so a higher value = a denser opening world that settles toward the
    // steady-state max as the player clears hideouts.
    public int InitialHideoutsPerFaction { get; set; } = 14;
}
