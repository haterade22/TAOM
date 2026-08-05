using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

public sealed class MobilePartyAttachmentAdapter : IMobilePartyAttachmentAdapter
{
    private readonly IModLogger _logger;

    public MobilePartyAttachmentAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public bool ParkNear(string commanderHeroId)
    {
        try
        {
            var main = MobileParty.MainParty;
            var commanderParty = FindCommanderParty(commanderHeroId);
            if (main == null || commanderParty == null)
                return false;

            if (main.AttachedTo != null)
                main.AttachedTo = null;
            main.SetMoveModeHold();
            main.Position = commanderParty.Position;
            main.IsVisible = false;
            main.IsActive = false;
            commanderParty.Party.SetAsCameraFollowParty();
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] ParkNear('{commanderHeroId}') failed: {ex.Message}");
            return false;
        }
    }

    public bool RestorePresence()
    {
        try
        {
            var main = MobileParty.MainParty;
            if (main == null)
                return false;

            main.IsActive = true;
            main.IsVisible = true;
            main.SetMoveModeHold();
            main.Party.SetAsCameraFollowParty();
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] RestorePresence failed: {ex.Message}");
            return false;
        }
    }

    public bool SyncPositionTo(string commanderHeroId)
    {
        try
        {
            var main = MobileParty.MainParty;
            var commanderParty = FindCommanderParty(commanderHeroId);
            if (main == null || commanderParty == null)
                return false;

            main.Position = commanderParty.Position;
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] SyncPositionTo('{commanderHeroId}') failed: {ex.Message}");
            return false;
        }
    }

    public PlayerPresenceSnapshot GetPresence()
    {
        try
        {
            var main = MobileParty.MainParty;
            if (main == null)
                return new PlayerPresenceSnapshot(mainPartyExists: false);

            return new PlayerPresenceSnapshot(
                mainPartyExists: true,
                isCaptive: PlayerCaptivity.IsCaptive,
                isActive: main.IsActive,
                isVisible: main.IsVisible,
                settlementId: main.CurrentSettlement?.StringId,
                isInMapEvent: main.MapEvent != null);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] GetPresence failed: {ex.Message}");
            return new PlayerPresenceSnapshot(mainPartyExists: false);
        }
    }

    public bool MoveIntoSettlement(string settlementId)
    {
        try
        {
            var main = MobileParty.MainParty;
            var settlement = string.IsNullOrEmpty(settlementId)
                ? null
                : Campaign.Current?.CampaignObjectManager?.Find<Settlement>(settlementId);
            if (main == null || settlement == null)
                return false;
            if (main.CurrentSettlement == settlement)
                return true;

            EnterSettlementAction.ApplyForParty(main, settlement);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] MoveIntoSettlement('{settlementId}') failed: {ex.Message}");
            return false;
        }
    }

    public bool LeaveSettlement()
    {
        try
        {
            var main = MobileParty.MainParty;
            if (main == null)
                return false;
            if (main.CurrentSettlement == null)
                return true;

            LeaveSettlementAction.ApplyForParty(main);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] LeaveSettlement failed: {ex.Message}");
            return false;
        }
    }

    private static MobileParty FindCommanderParty(string commanderHeroId)
    {
        if (string.IsNullOrEmpty(commanderHeroId))
            return null;
        var party = Campaign.Current?.CampaignObjectManager?.Find<Hero>(commanderHeroId)?.PartyBelongedTo;
        return party != null && party.IsActive ? party : null;
    }
}
