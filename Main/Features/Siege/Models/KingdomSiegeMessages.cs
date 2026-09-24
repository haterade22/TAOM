namespace TAOM.Features.Siege.Models;

/// <summary>
/// One kingdom's entry in siege_defense_config.json. Newtonsoft leaves a property null when its
/// key is missing from the entry, so every message is nullable; consumers fall back to "".
/// </summary>
public class KingdomSiegeMessages
{
    public string? Title { get; set; }
    public string? Body { get; set; }
    public string? AcceptButton { get; set; }
    public string? AcceptMessage { get; set; }
    public string? RewardMessage { get; set; }
}
