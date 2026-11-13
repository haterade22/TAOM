namespace TAOM.Features.Diplomacy;

public class TaomSettingsProvider : ITaomSettingsProvider
{
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public bool IsAvailable => Settings != null;
    public bool WarOfTheRingEnabled => Settings?.WarOfTheRingEnabled ?? true;
    public int Phase1TriggerDay => Settings?.WarPhase1TriggerDay ?? 30;
    public int Phase2TriggerDay => Settings?.WarPhase2TriggerDay ?? 44;
    public bool TestMode => Settings?.TestMode ?? false;
}
