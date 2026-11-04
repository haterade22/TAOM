namespace TAOM.Features.MarshalsWord;

/// <summary>
/// <see cref="IMarshalSettingsProvider"/> over the MCM <c>TaomSettings</c> singleton, read through one lazy accessor, with the defaults
/// here so the fallback and the MCM property cannot drift.
/// </summary>
internal sealed class MarshalSettingsProvider : IMarshalSettingsProvider
{
    internal const bool DefaultEnabled = true;
    internal const bool DefaultKeepArray = true;
    internal const bool DefaultArriveTogether = true;

    private TaomSettings? _settings;

    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public MarshalSettingsProvider() { }

    internal MarshalSettingsProvider(TaomSettings settings) => _settings = settings;

    public bool Enabled => Settings?.MarshalEnabled ?? DefaultEnabled;

    public bool KeepArray => Settings?.MarshalKeepArray ?? DefaultKeepArray;

    public bool ArriveTogether => Settings?.MarshalArriveTogether ?? DefaultArriveTogether;
}
