using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

public sealed class MobilePartyAttachmentAdapter : IMobilePartyAttachmentAdapter
{
    /// <summary>
    /// Map units of commander travel between position syncs that count as normal. Measured at ~1.8
    /// in a live session; anything an order of magnitude beyond a tick's travel is a real fault.
    /// </summary>
    private const float DriftWarningThreshold = 15f;

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
            if (main == null)
            {
                _logger?.LogError("[EnlistDiag] PARK FAILED — MobileParty.MainParty is null");
                return false;
            }

            if (commanderParty == null)
            {
                // FindCommanderParty returns null when the hero is missing, has no party, or the
                // party is inactive. This is the silent failure behind "still enlisted but left
                // behind": no park, no sync, no signal.
                _logger?.LogError($"[EnlistDiag] PARK FAILED — commander '{commanderHeroId}' has no findable ACTIVE party. Player stays where they are.");
                return false;
            }

            var before = DescribeParty(main);
            if (main.AttachedTo != null)
                main.AttachedTo = null;
            main.SetMoveModeHold();
            main.Position = commanderParty.Position;
            main.IsVisible = false;
            main.IsActive = false;
            commanderParty.Party.SetAsCameraFollowParty();

            _logger?.LogInfo($"[EnlistDiag] PARK ok on '{commanderHeroId}' | before: {before} | after: {DescribeParty(main)}");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[EnlistDiag] PARK THREW for '{commanderHeroId}': {ex.Message}");
            return false;
        }
    }

    public bool RestorePresence()
    {
        try
        {
            var main = MobileParty.MainParty;
            if (main == null)
            {
                _logger?.LogError("[EnlistDiag] RESTORE FAILED — MobileParty.MainParty is null");
                return false;
            }

            var before = DescribeParty(main);
            main.IsActive = true;
            main.IsVisible = true;
            main.SetMoveModeHold();
            main.Party.SetAsCameraFollowParty();

            _logger?.LogInfo($"[EnlistDiag] RESTORE ok | before: {before} | after: {DescribeParty(main)}");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[EnlistDiag] RESTORE THREW: {ex.Message}");
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
            {
                _logger?.LogError($"[EnlistDiag] SYNC FAILED — main={(main == null ? "null" : "ok")} commanderParty={(commanderParty == null ? "NOT FOUND/INACTIVE" : "ok")} for '{commanderHeroId}'. Player will drift.");
                return false;
            }

            var drift = main.Position.Distance(commanderParty.Position);
            main.Position = commanderParty.Position;

            // Threshold calibrated against a live session: ordinary inter-tick drift is ~1.8 while
            // the commander marches, so the original `> 1f` fired on essentially every sync and
            // produced 291 of that session's 299 warnings — burying real signal. Only a drift far
            // beyond one tick's travel means the player genuinely fell behind (a missed sync
            // window, a teleporting commander, a stalled tick).
            if (drift > DriftWarningThreshold)
                _logger?.LogWarning($"[EnlistDiag] SYNC closed a drift of {drift:F1} to '{commanderHeroId}' — the player had genuinely fallen behind");
            else
                _logger?.LogDebug($"[EnlistDiag] SYNC ok (drift {drift:F2}) to '{commanderHeroId}'");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[EnlistDiag] SYNC THREW for '{commanderHeroId}': {ex.Message}");
            return false;
        }
    }

    public float GetDistanceToCommander(string commanderHeroId)
    {
        try
        {
            var main = MobileParty.MainParty;
            var commanderParty = FindCommanderParty(commanderHeroId);
            if (main == null || commanderParty == null)
                return -1f;
            return main.Position.Distance(commanderParty.Position);
        }
        catch
        {
            return -1f;
        }
    }

    private static string DescribeParty(MobileParty p) =>
        $"active={p.IsActive} visible={p.IsVisible} attachedTo={(p.AttachedTo != null)} mapEvent={(p.MapEvent != null)} settlement={p.CurrentSettlement?.StringId ?? "-"}";

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
                isInMapEvent: main.MapEvent != null,
                isAttachedToParty: main.AttachedTo != null,
                hasPlayerEncounter: PlayerEncounter.Current != null);
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
            // Settlement.Find, NOT CampaignObjectManager.Find<Settlement> — the latter returns null
            // unconditionally on 1.4.7 (CampaignObjectManager registers no Settlement type), which
            // is why MoveIntoSettlement could never have worked.
            var settlement = string.IsNullOrEmpty(settlementId) ? null : Settlement.Find(settlementId);
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
