namespace TAOM.Features.CoopInterop;

/// <summary>
/// The switches in the Co-op group of <see cref="TaomSettings"/>, read in the co-op server process: the load order
/// switch by TAOM.CoopCompat each time a player's hello arrives, the career sync switch by CareerSyncHost (TAOM.dll)
/// each time a player reports a career and each time the server changes one. On a dedicated server
/// <see cref="TaomSettings.Instance"/> is the instance
/// ServerSettingsSource loads from taom-server-settings.json once per process, so a change there needs a server restart;
/// in a process where MCM serves the settings, a change made in that process applies to the next read. Not a hot path.
/// The settings object is taken once, on the first non-null read: MCM edits that one object in place. The group's third
/// switch, CoopGuestLadder, is read through IArmourAcquisitionSettingsProvider, not here.
/// </summary>
public class CoopSettingsProvider
{
    public const bool DefaultLoadOrderWarning = true;
    public const bool DefaultCareerSync = true;

    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public CoopSettingsProvider() { }
    internal CoopSettingsProvider(TaomSettings? settings) => _settings = settings;

    /// <summary>Whether the co-op server compares a joining player's load order with its own and warns that player.</summary>
    public bool LoadOrderWarning => Settings?.CoopLoadOrderWarning ?? DefaultLoadOrderWarning;

    /// <summary>Whether the co-op server checks, stores and passes on the careers players report (CareerSyncHost).</summary>
    public bool CareerSync => Settings?.CoopCareerSync ?? DefaultCareerSync;
}
