namespace TAOM.Features.RevoltTuning;

public class RevoltTuningConfig
{
    public int RebellionStartLoyaltyThreshold { get; set; } = 5;
    public int RebelliousStateStartLoyaltyThreshold { get; set; } = 10;
    public float SettlementOwnerDifferentCultureLoyaltyEffect { get; set; } = -1.0f;
    public float GovernorDifferentCultureLoyaltyEffect { get; set; } = -0.5f;
}
