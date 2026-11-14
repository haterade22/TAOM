namespace TAOM.Features.EncounterLines;

/// <summary>
/// The Encounter Lines switch. It takes the MCM settings object through one lazy accessor, as its siblings do: a non-null
/// instance only is kept, so a read before MCM is up cannot pin the default, and MCM edits its one registered
/// <see cref="TaomSettings"/> in place, so a change applies at the next conversation.
/// </summary>
public sealed class EncounterLinesSettingsProvider : IEncounterLinesSettingsProvider
{
    private TaomSettings? _settings;

    public EncounterLinesSettingsProvider()
    {
    }

    internal EncounterLinesSettingsProvider(TaomSettings settings) => _settings = settings;

    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public bool Enabled => From(Settings);

    /// <summary>The setting, or on when MCM has not built its settings yet.</summary>
    public static bool From(TaomSettings? settings) => settings?.EnableEncounterLines ?? true;
}
