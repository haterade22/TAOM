namespace TAOM.Features.BanditManagement;

public class BanditScalingConfig
{
    public float DensityCurve { get; set; } = 1.5f;
    public float PartySizeCurve { get; set; } = 1.5f;
    public float BossFightCurve { get; set; } = 1.5f;
    public int MaxHideoutsPerFactionCap { get; set; } = 15;
    public int MaxPartiesPerHideoutCap { get; set; } = 5;
    public int MinPartiesToInfest { get; set; } = 2;
}
