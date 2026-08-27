using TAOM.Core.Logging;
using TAOM.Features.PlayerSwitcher.Domain;

namespace TAOM.Features.PlayerSwitcher;

/// <inheritdoc cref="IPlayerSwitchPolicyProvider"/>
/// <remarks>
/// Mirrors AlignmentDesertionSettingsProvider: MCM live values over compiled defaults, because
/// TaomSettings.Instance can be null very early in startup. The settings read sits behind a
/// virtual so the session latch stays provable offline.
/// </remarks>
public class PlayerSwitchPolicyProvider : IPlayerSwitchPolicyProvider
{
    private readonly IModLogger _logger;
    private bool _disabledForSession;

    public PlayerSwitchPolicyProvider(IModLogger logger)
    {
        _logger = logger;
    }

    public PlayerSwitchPolicy Current => _disabledForSession
        ? PlayerSwitchPolicy.Disabled
        : ReadSettings();

    public void DisableForSession(string reason)
    {
        if (_disabledForSession)
            return;

        _disabledForSession = true;
        _logger.LogWarning($"Player Switcher disabled for this session: {reason}");
    }

    /// <summary>
    /// Live MCM values, falling back to the compiled defaults when settings have not loaded.
    /// Virtual so tests can drive the latch without a running MCM.
    /// </summary>
    protected virtual PlayerSwitchPolicy ReadSettings()
    {
        var defaults = PlayerSwitchPolicy.Default;
        var settings = TaomSettings.Instance;

        return new PlayerSwitchPolicy(
            settings?.EnablePlayerSwitcher ?? defaults.Enabled,
            settings?.PlayerSwitcherIncludeWanderers ?? defaults.IncludeWanderers,
            settings?.PlayerSwitcherAllowLoreLockedHeroes ?? defaults.AllowLoreLockedHeroes,
            settings?.PlayerSwitcherTransferStartingGold ?? defaults.TransferStartingGold);
    }
}
