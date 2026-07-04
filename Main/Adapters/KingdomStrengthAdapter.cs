using System.Linq;
using TaleWorlds.CampaignSystem;

namespace TAOM.Adapters;

public class KingdomStrengthAdapter : IKingdomStrengthAdapter
{
    public float GetTotalStrength(string kingdomId)
    {
        if (string.IsNullOrEmpty(kingdomId))
            return 0f;

        // Resolve via Kingdom.All (== Campaign.Current.Kingdoms), the idiom vanilla and every
        // other TAOM adapter use. MBObjectManager.GetObject<Kingdom>(id) does NOT resolve
        // campaign kingdoms and returned null → all strengths 0 → the daily strength award
        // never fired (Relative Strength stuck at 0). The ~30-kingdom scan is not a hot path
        // (daily tick / per battle), so correctness wins over the micro-optimization.
        var kingdom = Kingdom.All?.FirstOrDefault(k => k?.StringId == kingdomId);
        return kingdom != null && !kingdom.IsEliminated ? kingdom.CurrentTotalStrength : 0f;
    }
}
