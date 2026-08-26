using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.UncapturableHeroes;

/// <summary>
/// Holds no per-campaign state, so it needs no <c>ResetForNewSession</c> and no <c>SyncData</c>.
/// Every answer is derived from the config, the MCM toggle and the hero id it is handed. Keep it
/// that way: if a cache or a per-campaign latch is ever added here, this singleton acquires the
/// two lifecycle holes described in csharp-architecture.md ("Singleton Services Holding
/// Per-Campaign State"), because it lives for the process, not the campaign.
/// </summary>
public sealed class UncapturableHeroService : IUncapturableHeroService
{
    // Captor-neutral on purpose. The battle seam's relevance test is "was this the player's own
    // battle", which is true whether the player won or lost, so a protected ALLY escaping a defeat
    // the player shared would also announce. "your men" would be plainly wrong there. Checking the
    // player's side instead would buy the possessive at the cost of real engine coupling for one
    // line of flavour text, which the simplicity criterion does not justify.
    private const string BattleEscapeKey = "taom_uncapturable_escapes_battle";
    private const string BattleEscapeFallback =
        "{HERO} cannot be held. He slips away from the field before he can be taken.";

    private const string CaptureEscapeKey = "taom_uncapturable_escapes_capture";
    private const string CaptureEscapeFallback =
        "{HERO} cannot be held. He is gone before your men reach him.";

    private readonly IUncapturableRegistry _registry;
    private readonly IUncapturableHeroesSettingsProvider _settings;
    private readonly IUncapturableHeroesConfigProvider _configProvider;
    private readonly IHeroCaptivityAdapter _captivity;
    private readonly IInquiryAdapter _inquiry;
    private readonly IModLogger _logger;

    public UncapturableHeroService(
        IUncapturableRegistry registry,
        IUncapturableHeroesSettingsProvider settings,
        IUncapturableHeroesConfigProvider configProvider,
        IHeroCaptivityAdapter captivity,
        IInquiryAdapter inquiry,
        IModLogger logger)
    {
        _registry = registry;
        _settings = settings;
        _configProvider = configProvider;
        _captivity = captivity;
        _inquiry = inquiry;
        _logger = logger;
    }

    public bool ShouldDenyCapture(string heroStringId, int? raceId)
    {
        // The toggle is checked FIRST so a disabled feature never touches the registry, which is
        // what lets the off-state be asserted without also asserting the identity rules.
        if (!_settings.IsEnabled)
            return false;

        return _registry.IsUncapturable(heroStringId, raceId);
    }

    public void OnBattleCaptureDenied(string heroDisplayName, bool playerRelevant)
    {
        Announce(BattleEscapeKey, BattleEscapeFallback, heroDisplayName, playerRelevant);
    }

    public bool TryPreventCapture(string heroStringId, int? raceId, string heroDisplayName, bool playerRelevant)
    {
        if (!ShouldDenyCapture(heroStringId, raceId))
            return false;

        // STATE TRANSITION FIRST, unconditionally, then the announce gate
        // (harmony-patches.md "Toggles gate I/O, never state transitions"). Announcing is I/O; the
        // escape is the state change, and it must not depend on whether the player is watching.
        if (!_captivity.MakeFugitive(heroStringId))
        {
            // Fail open. The caller will let vanilla capture proceed, which is strictly better
            // than a hero who is neither captured nor escaped.
            _logger.LogWarning(
                $"UncapturableHeroService: could not make '{heroStringId}' a fugitive; deferring to vanilla capture");
            return false;
        }

        Announce(CaptureEscapeKey, CaptureEscapeFallback, heroDisplayName, playerRelevant);
        return true;
    }

    /// <summary>
    /// Nothing in here may throw. By the time this runs on the direct-capture path the hero is
    /// ALREADY a fugitive, so an escaping exception would unwind into the prefix's catch, which
    /// returns true, and vanilla would then capture a hero the world has just been told escaped.
    /// The whole body is guarded for that reason, config read included: the provider is a
    /// <c>Lazy&lt;T&gt;</c> and a faulted Lazy rethrows its cached exception on every later access.
    /// </summary>
    private void Announce(string key, string fallback, string heroDisplayName, bool playerRelevant)
    {
        try
        {
            if (!playerRelevant)
                return;

            if (!_configProvider.GetConfig().AnnounceEscape)
                return;

            _inquiry.ShowMessage(key, fallback, "HERO", heroDisplayName ?? string.Empty);
        }
        catch
        {
            // A toast must never be able to break a capture decision that has already been made.
        }
    }
}
