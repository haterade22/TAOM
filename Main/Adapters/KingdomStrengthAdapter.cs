using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace TAOM.Adapters;

public class KingdomStrengthAdapter : IKingdomStrengthAdapter
{
    public float GetTotalStrength(string kingdomId)
    {
        if (string.IsNullOrEmpty(kingdomId))
            return 0f;

        // Hash lookup by StringId — called per enrolled kingdom inside per-battle / daily
        // strength scoring, so avoid the Kingdom.All linear scan.
        var kingdom = MBObjectManager.Instance?.GetObject<Kingdom>(kingdomId);
        return kingdom != null && !kingdom.IsEliminated ? kingdom.CurrentTotalStrength : 0f;
    }
}
