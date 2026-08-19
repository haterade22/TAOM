namespace TAOM.Features.RevoltTuning;

public class RevoltTuningConfig
{
    public int RebellionStartLoyaltyThreshold { get; set; } = 5;
    public int RebelliousStateStartLoyaltyThreshold { get; set; } = 10;

    // v1.5.0's Advanced Starting Options "Civil Unrest" modifier raises vanilla's thresholds from
    // 15/25 to 50/60 (DefaultSettlementLoyaltyModel, gated on Options.IsHighRebellionEnabled).
    // TAOM overrides both properties outright, so without a high-rebellion pair the modifier is
    // silently INERT: the player selects it and nothing changes. These keep TAOM's softening ratio
    // (vanilla / 3 and / 2.5, the same reduction applied to the base pair) so Civil Unrest does
    // what it says while the campaign stays less revolt-happy than vanilla.
    public int HighRebellionStartLoyaltyThreshold { get; set; } = 17;
    public int HighRebellionRebelliousStateStartLoyaltyThreshold { get; set; } = 24;
    public float SettlementOwnerDifferentCultureLoyaltyEffect { get; set; } = -1.0f;
}
