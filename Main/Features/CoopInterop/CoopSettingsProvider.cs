namespace TAOM.Features.CoopInterop;

/// <summary>
/// The load order switch in the Co-op group of <see cref="TaomSettings"/>. TAOM.CoopCompat reads it in the co-op server
/// process each time a player's hello arrives. On a dedicated server <see cref="TaomSettings.Instance"/> is the instance
/// ServerSettingsSource loads from taom-server-settings.json once per process, so a change there needs a server restart;
/// in a process where MCM serves the settings, a change made in that process applies to the next hello. Not a hot path.
/// The settings object is taken once, on the first non-null read: MCM edits that one object in place.
/// </summary>
public class CoopSettingsProvider
{
    public const bool DefaultLoadOrderWarning = true;

    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public CoopSettingsProvider() { }
    internal CoopSettingsProvider(TaomSettings? settings) => _settings = settings;

    /// <summary>Whether the co-op server compares a joining player's load order with its own and warns that player.</summary>
    public bool LoadOrderWarning => Settings?.CoopLoadOrderWarning ?? DefaultLoadOrderWarning;
}
