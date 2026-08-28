using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CharacterCreation;

namespace TAOM.Features.PlayerSwitcher;

/// <summary>
/// Registers the handover handler at priority 1100. Mirrors CharacterCreationRegistrationBehavior
/// with one difference that is not optional: the registration is wrapped.
/// </summary>
/// <remarks>
/// CharacterCreationManager._handlers is a SortedList&lt;int, ICharacterCreationContentHandler&gt;
/// and RegisterCharacterCreationContentHandler does a plain Add, so a duplicate priority throws
/// ArgumentException from inside OnCharacterCreationInitializedEvent dispatch. Unhandled, that
/// takes character creation down entirely. Degrading to "the switcher is unavailable" is always
/// the better trade: the player still gets to start a campaign.
///
/// 1100 is currently free. Vanilla's core handler is 800, StoryMode 900, NavalDLC 1000, and both
/// TAOM and DOTS use 1050 (they are mutually exclusive total conversions, so that is not a live
/// collision).
/// </remarks>
public class PlayerSwitchRegistrationBehavior : CampaignBehaviorBase
{
    /// <summary>
    /// Must stay above TAOM's own 1050. Below it, CharacterCreationContentService's finalize would
    /// run SetPlayerRace and the startup grants against the lord instead of the throwaway hero,
    /// silently overwriting a canonical race (Sauron's race="sauron" becoming a culture default),
    /// which RacePersistenceService would then faithfully persist.
    /// </summary>
    public const int HandlerPriority = 1100;

    private readonly IHeroSwitchService _switchService;
    private readonly ISwitchPlanner _planner;
    private readonly IPlayerSwitchSession _session;
    private readonly IPlayerSwitchSessionWriter _sessionWriter;
    private readonly IPlayerSwitchPolicyProvider _policy;
    private readonly ICareerMenuService _careerMenu;
    private readonly IInquiryAdapter _inquiry;
    private readonly IModLogger _logger;

    public PlayerSwitchRegistrationBehavior(
        IHeroSwitchService switchService,
        ISwitchPlanner planner,
        IPlayerSwitchSession session,
        IPlayerSwitchSessionWriter sessionWriter,
        IPlayerSwitchPolicyProvider policy,
        ICareerMenuService careerMenu,
        IInquiryAdapter inquiry,
        IModLogger logger)
    {
        _switchService = switchService;
        _planner = planner;
        _session = session;
        _sessionWriter = sessionWriter;
        _policy = policy;
        _careerMenu = careerMenu;
        _inquiry = inquiry;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnCharacterCreationInitializedEvent.AddNonSerializedListener(
            this,
            OnCharacterCreationInitialized);
    }

    public override void SyncData(IDataStore dataStore)
    {
        // Nothing persists. The feature runs only during the character creation of a new campaign.
    }

    private void OnCharacterCreationInitialized(CharacterCreationManager manager)
    {
        try
        {
            var handler = new PlayerSwitchContentHandler(
                _switchService, _planner, _session, _sessionWriter, _policy, _careerMenu, _inquiry, _logger);

            manager.RegisterCharacterCreationContentHandler(handler, HandlerPriority);
            _logger.LogInfo($"Registered TAOM player switcher handler at priority {HandlerPriority}");
        }
        catch (Exception ex)
        {
            _policy.DisableForSession($"handler registration at priority {HandlerPriority} failed: {ex.Message}");
            _logger.LogError($"Player Switcher: could not register its character creation handler: {ex}");
        }
    }
}
