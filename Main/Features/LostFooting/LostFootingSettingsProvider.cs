namespace TAOM.Features.LostFooting;

/// <summary>
/// Production wire-up of <see cref="ILostFootingSettingsProvider"/> over the MCM <c>TaomSettings</c> singleton.
/// Read on every tick, so a read resolves the MCM instance when it has not been taken yet and keeps it
/// (a null is never kept, so a read before MCM is up cannot pin the default).
/// </summary>
public class LostFootingSettingsProvider : ILostFootingSettingsProvider
{
    private TaomSettings? _settings;

    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public LostFootingSettingsProvider() { }

    internal LostFootingSettingsProvider(TaomSettings settings) => _settings = settings;

    public bool CensusEnabled => Settings?.LostFootingCensusEnabled ?? false;
}
