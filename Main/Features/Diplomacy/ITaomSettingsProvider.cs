namespace TAOM.Features.Diplomacy;

public interface ITaomSettingsProvider
{
    bool IsAvailable { get; }
    bool WarOfTheRingEnabled { get; }
    int Phase1TriggerDay { get; }
    int Phase2TriggerDay { get; }
    bool TestMode { get; }
}
