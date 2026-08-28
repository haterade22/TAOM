using System;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;

namespace TAOM.Features.PlayerSwitcher;

/// <summary>
/// Raises the kingdom-join offer once character creation is over.
/// </summary>
/// <remarks>
/// OnCharacterCreationIsOverEvent is the right seam and the handler that performs the swap is not:
/// the handler runs inside ApplyFinalEffects, while the map state is still being pushed, so a
/// prompt raised there would sit over a screen about to be popped. By the time this event fires
/// the player is on a live map with time running.
/// </remarks>
public class KingdomJoinOfferBehavior : CampaignBehaviorBase
{
    private readonly IKingdomJoinOfferService _offer;
    private readonly IModLogger _logger;

    public KingdomJoinOfferBehavior(IKingdomJoinOfferService offer, IModLogger logger)
    {
        _offer = offer;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnCharacterCreationIsOverEvent.AddNonSerializedListener(this, OnCharacterCreationIsOver);
    }

    public override void SyncData(IDataStore dataStore)
    {
        // Nothing persists; the offer is made once, in the session that created the campaign.
    }

    private void OnCharacterCreationIsOver()
    {
        try
        {
            _offer.OfferIfEarned();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Player Switcher: kingdom-join offer failed: {ex}");
        }
    }
}
