namespace TAOM.Features.Messengers;

public interface IMessengerSettingsProvider
{
    bool EnableMessengers { get; }
    int MessengerGoldCost { get; }
    int MessengerTravelDays { get; }
    bool MessengerAccidents { get; }
}
