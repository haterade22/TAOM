namespace TAOM.Features.Diplomacy;

public class TaomSettingsProvider : ITaomSettingsProvider
{
    public bool IsAvailable => TaomSettings.Instance != null;
    public bool WarOfTheRingEnabled => TaomSettings.Instance?.WarOfTheRingEnabled ?? true;
    public int Phase1TriggerDay => TaomSettings.Instance?.WarPhase1TriggerDay ?? 30;
    public int Phase2TriggerDay => TaomSettings.Instance?.WarPhase2TriggerDay ?? 44;
    public bool TestMode => TaomSettings.Instance?.TestMode ?? false;
}
