using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Core.Logging;

namespace TAOM.Features.Diplomacy;

// Lets a player who leads their own kingdom INITIATE an alliance with another kingdom's
// ruler via conversation. Vanilla never gives the player this initiative — alliance offers
// only ever flow *to* the player (AllianceCampaignBehavior). This thin boundary class
// (ADR-002) registers the dialog and delegates every decision to IDiplomacyService.
// The receive direction (AI offering to the player) is handled by TaomAllianceModel +
// TaomKingdomDecisionPermissionModel; this class owns the initiate direction.
public class PlayerAllianceProposalBehavior : CampaignBehaviorBase
{
    private readonly IDiplomacyService _service;
    private readonly IModLogger _logger;

    // Singleton-lifetime behavior survives across campaigns in one process; re-register the
    // dialog once per new starter (mirrors MessengerCampaignBehavior's guard).
    private CampaignGameStarter _lastSessionStarter;
    private bool _dialogsRegistered;

    public PlayerAllianceProposalBehavior(IDiplomacyService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
    }

    // Stateless: formed alliances persist via vanilla's IAllianceCampaignBehavior save data.
    public override void SyncData(IDataStore dataStore) { }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        if (_lastSessionStarter != starter)
        {
            _lastSessionStarter = starter;
            _dialogsRegistered = false;
        }

        if (!_dialogsRegistered)
        {
            AddDialogOptions(starter);
            _dialogsRegistered = true;
        }
    }

    private void AddDialogOptions(CampaignGameStarter starter)
    {
        starter.AddPlayerLine(
            "taom_alliance_propose_init",
            "hero_main_options",
            "taom_alliance_propose_response",
            "{=taom_alliance_propose}I propose an alliance between our realms.",
            DialogCondition_CanProposeAlliance,
            DialogConsequence_FormAlliance);

        starter.AddDialogLine(
            "taom_alliance_propose_accept",
            "taom_alliance_propose_response",
            "close_window",
            "{=taom_alliance_accept}Then let our realms stand together. This alliance is forged.",
            null, null);
    }

    private bool DialogCondition_CanProposeAlliance()
    {
        var playerKingdom = PlayerKingdomHelper.GetPlayerRuledKingdom();
        if (playerKingdom == null) return false;

        var targetKingdom = GetConversationRulerKingdom();
        if (targetKingdom == null || targetKingdom == playerKingdom) return false;

        return _service.CanPlayerProposeAlliance(playerKingdom.StringId, targetKingdom.StringId);
    }

    private void DialogConsequence_FormAlliance()
    {
        var playerKingdom = PlayerKingdomHelper.GetPlayerRuledKingdom();
        var targetKingdom = GetConversationRulerKingdom();
        if (playerKingdom == null || targetKingdom == null) return;

        _logger.LogInfo($"[Diplomacy] Player proposed alliance via dialog: {playerKingdom.StringId} -> {targetKingdom.StringId}");

        // Only announce success if the alliance actually formed — state can change between the
        // dialog condition and this consequence (kingdom eliminated, war declared, etc.).
        if (!_service.FormPlayerAlliance(playerKingdom.StringId, targetKingdom.StringId))
            return;

        var msg = new TextObject("{=taom_player_alliance_formed}An alliance has been forged between {PLAYER_KINGDOM} and {TARGET_KINGDOM}.");
        msg.SetTextVariable("PLAYER_KINGDOM", playerKingdom.Name);
        msg.SetTextVariable("TARGET_KINGDOM", targetKingdom.Name);
        InformationManager.DisplayMessage(new InformationMessage(msg.ToString()));
    }

    // Alliances are sovereign-to-sovereign: the conversation partner must BE the ruler of their
    // kingdom (not merely a member of the ruling clan — a non-leader royal cannot bind the realm).
    private static Kingdom GetConversationRulerKingdom()
    {
        var hero = Hero.OneToOneConversationHero;
        var kingdom = hero?.Clan?.Kingdom;
        if (kingdom == null) return null;
        return kingdom.Leader == hero ? kingdom : null;
    }
}
