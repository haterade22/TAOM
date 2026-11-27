using System;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.Quests;

/// <summary>Which career tier quest, if any, <see cref="CareerQuestCampaignBehavior"/> offers next.</summary>
public static class CareerQuestOffers
{
    /// <summary>
    /// The lowest tier's quest still worth offering: a tier already open (by level or an earlier quest, as the
    /// career screen reads it), a tier with no authored quest and a quest the player declined are skipped.
    /// </summary>
    public static CareerQuestDefinition NextOffer(ICareerQuestService quests, string careerId, int heroLevel,
        string heroId, Func<string, bool> isDeclined)
    {
        for (int tier = 1; tier <= 3; tier++)
        {
            if (quests.IsTierUnlocked(heroLevel, tier, heroId)) continue;
            var def = quests.GetQuestFor(careerId, tier);
            if (def == null || isDeclined(def.Id)) continue;
            return def;
        }
        return null;
    }
}
