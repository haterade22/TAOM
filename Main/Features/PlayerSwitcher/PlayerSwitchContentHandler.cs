using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TAOM.Core.Logging;
using TAOM.Features.CharacterCreation;
using TAOM.Features.PlayerSwitcher.Domain;

namespace TAOM.Features.PlayerSwitcher;

/// <summary>
/// The handover seam. Registered at character-creation handler priority 1100, which is the entire
/// design in one number.
/// </summary>
/// <remarks>
/// CharacterCreationManager.ApplyFinalEffects runs, in order: Clan.PlayerClan.Renown = 0,
/// CharacterCreationContent.ApplyCulture (which rewrites the hero's culture and BornSettlement and
/// calls ResetPlayerHomeAndFactionMidSettlement), the narrative options, the trait XP update, the
/// culture start-point teleport, and only then the handler loop in priority order. Vanilla's core
/// handler sits at 800 and TAOM's own content handler at 1050, so at 1100 every one of those
/// effects and every TAOM grant (race, career, gold, starting equipment) has already landed on the
/// throwaway character-creation hero and the throwaway player_faction clan, both of which this
/// handover deletes moments later.
///
/// That is why the lord being taken over needs no repair: no BornSettlement fix, no party
/// reposition, no grant suppression, and no edit anywhere inside the 41-file CharacterCreation
/// module. Registering any earlier would apply all of it to a real lore clan instead, so Erebor
/// would start at renown zero with a relocated home settlement.
/// </remarks>
public class PlayerSwitchContentHandler : ICharacterCreationContentHandler
{
    private readonly IHeroSwitchService _switchService;
    private readonly ISwitchPlanner _planner;
    private readonly IPlayerSwitchSession _session;
    private readonly IPlayerSwitchSessionWriter _sessionWriter;
    private readonly IPlayerSwitchPolicyProvider _policy;
    private readonly ICareerMenuService _careerMenu;
    private readonly IModLogger _logger;

    public PlayerSwitchContentHandler(
        IHeroSwitchService switchService,
        ISwitchPlanner planner,
        IPlayerSwitchSession session,
        IPlayerSwitchSessionWriter sessionWriter,
        IPlayerSwitchPolicyProvider policy,
        ICareerMenuService careerMenu,
        IModLogger logger)
    {
        _switchService = switchService;
        _planner = planner;
        _session = session;
        _sessionWriter = sessionWriter;
        _policy = policy;
        _careerMenu = careerMenu;
        _logger = logger;
    }

    /// <summary>A new character creation starts with no lord chosen.</summary>
    public void InitializeContent(CharacterCreationManager characterCreationManager)
        => _sessionWriter.Clear();

    public void AfterInitializeContent(CharacterCreationManager characterCreationManager)
    {
    }

    public void OnStageCompleted(CharacterCreationStageBase stage)
    {
    }

    public void OnCharacterCreationFinalize(CharacterCreationManager characterCreationManager)
    {
        if (!_session.HasSelection)
            return;

        var plan = _planner.Plan(_session.SelectedRow, _policy.Current, _careerMenu.SelectedCareerStringId);
        var outcome = _switchService.Execute(plan);

        if (outcome != SwitchOutcome.Switched)
            _logger.LogWarning($"Player Switcher: handover ended as {outcome} for '{plan.HeroId}'");

        // The selection has been consumed either way. Leaving it set would let a second character
        // creation in the same process inherit it.
        _sessionWriter.Clear();
    }
}
