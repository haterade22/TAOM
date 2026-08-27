namespace TAOM.Features.PlayerSwitcher.Domain;

/// <summary>
/// Immutable snapshot of the MCM knobs, taken once per read so a setting cannot change
/// underneath a decision. <see cref="IPlayerSwitchPolicyProvider"/> is the only thing that
/// builds one from live settings.
/// </summary>
public readonly struct PlayerSwitchPolicy
{
    public PlayerSwitchPolicy(
        bool enabled,
        bool includeWanderers,
        bool allowLoreLockedHeroes,
        bool transferStartingGold)
    {
        Enabled = enabled;
        IncludeWanderers = includeWanderers;
        AllowLoreLockedHeroes = allowLoreLockedHeroes;
        TransferStartingGold = transferStartingGold;
    }

    /// <summary>Master toggle. Off means the movie never loads and the handler no-ops.</summary>
    public bool Enabled { get; }

    /// <summary>Whether the Wanderers group is populated at all.</summary>
    public bool IncludeWanderers { get; }

    /// <summary>
    /// Whether Sauron and the Nazgul are offered. Default off: Patch76 defers to vanilla for
    /// Hero.MainHero, so a player-controlled dark lord can be captured and ransomed, which
    /// silently contradicts docs/features/uncapturable-heroes.md.
    /// </summary>
    public bool AllowLoreLockedHeroes { get; }

    /// <summary>Whether character-creation starting gold follows the player onto the lord.</summary>
    public bool TransferStartingGold { get; }

    /// <summary>
    /// The compiled fallback, used when TaomSettings has not loaded. Matches the shipped
    /// MCM defaults so behaviour does not change when settings arrive late.
    /// </summary>
    public static PlayerSwitchPolicy Default => new PlayerSwitchPolicy(
        enabled: true,
        includeWanderers: true,
        allowLoreLockedHeroes: false,
        transferStartingGold: false);

    /// <summary>A policy that disables the whole feature. Used when a reflection probe fails.</summary>
    public static PlayerSwitchPolicy Disabled => new PlayerSwitchPolicy(
        enabled: false,
        includeWanderers: false,
        allowLoreLockedHeroes: false,
        transferStartingGold: false);
}
