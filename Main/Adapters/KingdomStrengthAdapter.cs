using System.Linq;
using TaleWorlds.CampaignSystem;

namespace TAOM.Adapters;

public class KingdomStrengthAdapter : IKingdomStrengthAdapter
{
    public float GetTotalStrength(string kingdomId)
    {
        if (string.IsNullOrEmpty(kingdomId))
            return 0f;

        var kingdom = Kingdom.All?.FirstOrDefault(k => k?.StringId == kingdomId);
        return kingdom?.CurrentTotalStrength ?? 0f;
    }
}
