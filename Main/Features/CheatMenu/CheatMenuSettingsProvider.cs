namespace TAOM.Features.CheatMenu;

public interface ICheatMenuSettingsProvider
{
    /// <summary>The "Enable Cheat Menu" MCM switch; false before MCM is up.</summary>
    bool EnableCheatMenu { get; }
}

/// <summary>Reads the switch through a lazily taken settings reference (a null, MCM not up yet, is not cached).</summary>
public sealed class CheatMenuSettingsProvider : ICheatMenuSettingsProvider
{
    private TaomSettings? _settings;

    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public bool EnableCheatMenu => Settings?.EnableCheatMenu ?? false;
}
