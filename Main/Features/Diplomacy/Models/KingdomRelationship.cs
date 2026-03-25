namespace TAOM.Features.Diplomacy.Models;

public class KingdomRelationship
{
    public string KingdomA { get; set; } = "";
    public string KingdomB { get; set; } = "";
    public AllianceTier Tier { get; set; } = AllianceTier.Neutral;
}
