namespace TAOM.Features.TrainingGrounds.Waves;

/// <summary>
/// The MCM switch over its default (on). The settings object is taken once and only a non-null instance is kept, so a read before
/// MCM is up cannot pin the default (CampaignHotPathSettingsProvidersTests); MCM edits its one <see cref="TaomSettings"/> in place, so
/// a change applies at the next conversation.
/// </summary>
public sealed class TrainingWavesSettingsProvider : ITrainingWavesSettingsProvider
{
    internal const bool DefaultEnabled = true;

    private TaomSettings? _settings;

    public TrainingWavesSettingsProvider()
    {
    }

    internal TrainingWavesSettingsProvider(TaomSettings settings) => _settings = settings;

    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public bool Enabled => EnabledFrom(Settings);

    /// <summary>The switch, or on when MCM has not built its settings yet.</summary>
    public static bool EnabledFrom(TaomSettings? settings) => settings?.EnableTrainingWaves ?? DefaultEnabled;
}
