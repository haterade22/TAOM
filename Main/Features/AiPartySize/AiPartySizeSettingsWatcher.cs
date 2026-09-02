using System.ComponentModel;
using MCM.Abstractions.Base;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.AiPartySize;

/// <summary>
/// Makes an MCM party-size change visible in a running campaign instead of a campaign day later.
///
/// The knobs themselves are already live the instant a slider moves: MCM's PropertyRef setter writes
/// the registered settings object by reflection, and that object IS TaomSettings.Instance. What lags
/// is the ENGINE's cache. PartyBase.PartySizeLimit only recalls the model when
/// MemberRoster.VersionNo changes, so until some unrelated roster event bumps that counter the party
/// keeps enforcing its old cap. In practice PartyHealingBehavior's 6-hourly heal/starve tick refreshes
/// most AI parties within a day, but an idle, healthy, non-recruiting party can stay stale
/// indefinitely, and until now the only deterministic fix was a save and reload (PartyBase.OnLoad
/// calls InitCache).
///
/// TroopRoster.UpdateVersion() is public and is the engine's own cache-busting idiom for exactly this:
/// vanilla calls it after a perk change and after a building alters garrison capacity. Bumping it has
/// no gameplay effect of its own; the dependent caches simply recompute lazily.
///
/// Paired requirement: every attribute in the AI Party Size group sets RequireRestart = false. MCM's
/// BaseSettingPropertyAttribute defaults that to TRUE, and with it true the only path that reaches
/// SaveSettings also quits the game, so SAVE_TRIGGERED would never arrive mid-session and this
/// watcher would never fire.
/// </summary>
public sealed class AiPartySizeSettingsWatcher : IAiPartySizeSettingsWatcher
{
    private readonly IModLogger _logger;

    private BaseSettings? _subscribedTo;

    public AiPartySizeSettingsWatcher(IModLogger logger) => _logger = logger;

    /// <summary>
    /// Attach to the live settings object, at most once per object. Callable from any campaign start:
    /// re-attaching to the same instance is a no-op, and if MCM ever hands back a different instance
    /// the old subscription is dropped first. That idempotence is what keeps a second campaign in the
    /// same process from stacking a second handler.
    /// </summary>
    public void EnsureSubscribed(BaseSettings? settings)
    {
        // A null instance means MCM has not registered its settings yet, or is absent. There is no
        // retry, so this session simply never gets the immediate sweep, and the only symptom would be
        // "my slider change did nothing until I reloaded" — indistinguishable from the bug this class
        // exists to fix. Leave a breadcrumb rather than failing silently. Deep review 2026-09-01.
        if (settings == null)
        {
            _logger?.LogWarning(
                "[AiPartySize] MCM settings were unavailable when the party-size watcher tried to attach. "
                + "Party size limits will not refresh until a save/reload after an MCM change this session.");
            return;
        }

        if (ReferenceEquals(settings, _subscribedTo))
            return;

        if (_subscribedTo != null)
            _subscribedTo.PropertyChanged -= OnSettingsPropertyChanged;

        _subscribedTo = settings;
        _subscribedTo.PropertyChanged += OnSettingsPropertyChanged;
    }

    /// <summary>
    /// Whether this notification is the one that means "the player pressed Done and something
    /// actually changed". MCM raises LOADING_COMPLETE at startup too, which must NOT trigger a sweep:
    /// there is no campaign then, and nothing has changed. Engine-free; unit-tested.
    /// </summary>
    public static bool ShouldInvalidate(string? propertyName, bool campaignActive)
        => campaignActive && propertyName == BaseSettings.SaveTriggered;

    private void OnSettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (ShouldInvalidate(e?.PropertyName, Campaign.Current != null))
            InvalidatePartySizeCaches();
    }

    /// <summary>
    /// One counter increment per party. Garrisons are MobileParty too, so the garrison multiplier is
    /// covered by the same sweep.
    /// </summary>
    private static void InvalidatePartySizeCaches()
    {
        foreach (var party in MobileParty.All)
            party?.MemberRoster?.UpdateVersion();
    }
}
