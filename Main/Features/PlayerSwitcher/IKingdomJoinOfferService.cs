namespace TAOM.Features.PlayerSwitcher;

/// <summary>
/// Offers an adopted player the chance to join their culture's kingdom, once, right after
/// character creation ends.
/// </summary>
public interface IKingdomJoinOfferService
{
    void OfferIfEarned();
}
