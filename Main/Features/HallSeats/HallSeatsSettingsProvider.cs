namespace TAOM.Features.HallSeats;

/// <summary>
/// The MCM "Fief Management/Hall Seats" switch (docs/features/hall-seats.md). Read once per spawn pass, when the hall
/// spawns its characters, so a change applies from the next visit. No interface: nothing fakes it (ADR-002).
/// </summary>
public sealed class HallSeatsSettingsProvider
{
    public bool IsEnabled => From(TaomSettings.Instance);

    /// <summary>The setting, or on when MCM has not built its settings yet. The fallback is the compiled default; a test pins both.</summary>
    public static bool From(TaomSettings? settings) => settings?.EnableHallSeats ?? true;
}
