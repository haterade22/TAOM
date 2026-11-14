namespace TAOM.Features.EncounterLines;

/// <summary>The MCM "World/Encounter Lines" switch for Encounter Lines (Patch149).</summary>
public interface IEncounterLinesSettingsProvider
{
    /// <summary>True when lords, caravans, villagers and bandits may speak TAOM's encounter lines. Read on the main thread, during conversations.</summary>
    bool Enabled { get; }
}
