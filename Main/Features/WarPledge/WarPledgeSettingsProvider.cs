namespace TAOM.Features.WarPledge;

public interface IWarPledgeSettings
{
    /// <summary>The AI may court a Neutral realm. Off stops new joins; a realm that pledged stays pledged.</summary>
    bool Enabled { get; }
}

/// <summary>
/// MCM (<c>TaomSettings</c>) over the compiled default, on. The settings object is taken on first use,
/// never in the constructor, because the container can build this before MCM has loaded.
/// </summary>
public sealed class WarPledgeSettingsProvider : IWarPledgeSettings
{
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public WarPledgeSettingsProvider() { }

    internal WarPledgeSettingsProvider(TaomSettings settings) => _settings = settings;

    public bool Enabled => Settings?.WarPledgeEnabled ?? true;
}
