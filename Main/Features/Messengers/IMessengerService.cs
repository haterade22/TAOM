using TAOM.Features.Messengers.Domain;

namespace TAOM.Features.Messengers;

public interface IMessengerService
{
    MessengerValidationResult CanSendMessenger(HeroSnapshot target, int playerGold);
    bool RollAccident();
    PositionUpdate AdvancePosition(MapCoord currentPosition, MapCoord targetPosition, float speed);
    float CalculateMessengerSpeed(float mapDiagonal, int travelDays);
}
