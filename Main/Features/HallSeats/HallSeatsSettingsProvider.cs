namespace TAOM.Features.HallSeats;

/// <summary>
/// The MCM "Fief Management/Hall Seats" switch (docs/features/hall-seats.md). Read once per spawn pass, when the hall
/// spawns its characters, so a change applies from the next visit. No interface: nothing fakes it (ADR-002).
/// </summary>
public sealed class HallSeatsSettingsProvider
{
    // Takes the MCM instance once, on the first non-null read, and reads through it (AlignmentDesertionSettingsProvider).
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public bool IsEnabled => From(Settings);

    /// <summary>The setting, or on when MCM has not built its settings yet. The fallback is the compiled default; a test pins both.</summary>
    public static bool From(TaomSettings? settings) => settings?.EnableHallSeats ?? true;
}
