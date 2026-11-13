using TAOM.Features.Messengers.Domain;

namespace TAOM.Features.Messengers;

public interface IMessengerService
{
    /// <param name="quotedCost">What <see cref="QuoteCost"/> said for this target; the gold check uses it.</param>
    MessengerValidationResult CanSendMessenger(HeroSnapshot? target, int playerGold, int quotedCost, bool inPerson);

    /// <summary>The whole look at one target: the verdict against this trip's quote, the price and the days.</summary>
    MessengerOffer GetOffer(HeroSnapshot? target, MessengerQuoteInput trip, int playerGold, bool inPerson);

    /// <summary>What to do with an arrived messenger: lose it, report the mix-up, wait an hour, or deliver it.</summary>
    MessengerArrivalVerdict ClassifyArrival(HeroSnapshot target, bool playerAvailable, bool targetPartyInMapEvent);

    /// <summary>The denars one messenger costs: the base price plus the trip, raised for war, a stranger and a siege.</summary>
    int QuoteCost(MessengerQuoteInput input);

    /// <summary>Whole in-game days the trip takes, at least one.</summary>
    int QuoteTravelDays(MessengerQuoteInput input);
    bool RollAccident();
    PositionUpdate AdvancePosition(MapCoord currentPosition, MapCoord targetPosition, float speed);
    float CalculateMessengerSpeed(float mapDiagonal, int travelDays);
}
