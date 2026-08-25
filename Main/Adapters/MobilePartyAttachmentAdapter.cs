using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;

namespace TAOM.Adapters;

public sealed class MobilePartyAttachmentAdapter : IMobilePartyAttachmentAdapter
{
    /// <summary>
    /// Map units of commander travel between position syncs that count as normal. Measured at ~1.8
    /// in a live session; anything an order of magnitude beyond a tick's travel is a real fault.
    /// </summary>
    private const float DriftWarningThreshold = 15f;

    private readonly IModLogger _logger;
    private readonly IEnlistmentDiagnosticsSettingsProvider _diag;

    public MobilePartyAttachmentAdapter(IModLogger logger, IEnlistmentDiagnosticsSettingsProvider diag)
    {
        _logger = logger;
        _diag = diag;
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
                //
                // WARNING, not ERROR (downgraded 2026-08-09). A commander with no active party is
                // not necessarily a fault: it is the DEFINING condition of the CommanderUnavailable
                // grace, which the reconciler handles deliberately and logs as its own state change.
                // Live on 2026-08-09 this fired at ERROR four seconds before
                // "commander lord_4_1 lost their party — grace until day 26039.4" — and it was the
                // only ERROR in a 3,452-line session, so it is the first line anyone would chase in
                // a crash bundle, and a dead end. The null-MainParty branch above stays ERROR
                // because that one genuinely cannot be a legitimate state.
                _logger?.LogWarning($"[EnlistDiag] PARK skipped — commander '{commanderHeroId}' has no findable ACTIVE party. Player stays where they are (expected during the commander-unavailable grace).");
                return false;
            }

            // GATE SHAPE RULE (every [EnlistDiag] gate in this feature): the gate is an
            // `if (_diag?.IsEnabled == true)` wrapping EXACTLY ONE logging statement. Never a
            // block, never guarding a `return`, never placed above a mutation of a MobileParty
            // property. `?.` + `== true` so a null provider fails quiet.
            //
            // The toggle is read ONCE per call so a mid-method MCM flip cannot make `before` and
            // the log line disagree. `before` is a read-only description whose ONLY consumer is
            // the gated line, so skipping its allocation skips no mutation. The FAILURE paths
            // above (:PARK FAILED) and the catch below are NOT gated — they are the reason the
            // [EnlistDiag] tag stays greppable when the toggle is off.
            var diag = _diag?.IsEnabled == true;
            var before = diag ? DescribeParty(main) : null;

            ClearArmyAttachment();
            main.SetMoveModeHold();
            main.Position = commanderParty.Position;
            main.IsVisible = false;
            main.IsActive = false;
            commanderParty.Party.SetAsCameraFollowParty();

            if (diag)
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

            // Same shape as ParkNear: toggle read once, `before` is a gated read-only snapshot,
            // every mutation below runs unconditionally, `return` sits outside the gate.
            var diag = _diag?.IsEnabled == true;
            var before = diag ? DescribeParty(main) : null;

            ClearArmyAttachment();
            main.IsActive = true;
            main.IsVisible = true;
            main.SetMoveModeHold();
            main.Party.SetAsCameraFollowParty();

            if (diag)
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
            // The WARNING stays always-on: the 72a77b9c threshold raise (1f -> 15f) exists precisely
            // so that this line means something when it fires. Only the routine "SYNC ok" else-branch
            // (~291 lines/session) is gated. The position mutation is above both branches.
            if (drift > DriftWarningThreshold)
                _logger?.LogWarning($"[EnlistDiag] SYNC closed a drift of {drift:F1} to '{commanderHeroId}' — the player had genuinely fallen behind");
            else if (_diag?.IsEnabled == true)
                // INFO, not DEBUG — the toggle controls the volume, the level controls durability,
                // and a trace you deliberately switched on must survive a hard CTD.
                _logger?.LogInfo($"[EnlistDiag] SYNC ok (drift {drift:F2}) to '{commanderHeroId}'");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[EnlistDiag] SYNC THREW for '{commanderHeroId}': {ex.Message}");
            return false;
        }
    }

    // Cached commander-party handle so the pump costs zero lookups on the steady path. Revalidated
    // O(1) against the party id from the SAME pass's tick snapshot. Survives campaign switches
    // within one process, so it MUST be invalidated on discharge, session launch and game load.
    private MobileParty _cachedCommanderParty;

    public bool SyncPositionCached(string commanderHeroId, string expectedCommanderPartyId)
    {
        try
        {
            var main = MobileParty.MainParty;
            if (main == null)
                return false;

            var party = _cachedCommanderParty;
            var cacheHit = party != null
                && party.IsActive
                && !string.IsNullOrEmpty(expectedCommanderPartyId)
                && party.StringId == expectedCommanderPartyId;

            if (!cacheHit)
            {
                party = FindCommanderParty(commanderHeroId);   // the one allowed Find<Hero>
                _cachedCommanderParty = party;
            }

            if (party == null)
                return false;

            // NEVER DRAG A PARTY THAT IS INSIDE A SETTLEMENT. This runs on the pump's cheap tier,
            // every pass while EnlistedAttached, and the enlisted player is deliberately INSIDE the
            // commander's settlement whenever the column rests. Writing Position against a party the
            // engine pins to a settlement gate is meaningless at best, and while a shore-leave pass is
            // held it is a second mechanism tugging the player out of the town they chose to stay in
            // (#512). One field read, so the cheap tier stays cheap.
            if (main.CurrentSettlement != null)
                return false;

            main.Position = party.Position;
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[EnlistDiag] SyncPositionCached('{commanderHeroId}') failed: {ex.Message}");
            return false;
        }
    }

    public void InvalidateCommanderCache() => _cachedCommanderParty = null;

    public PlayerPresenceFlags GetPresenceFlags()
    {
        try
        {
            var main = MobileParty.MainParty;
            if (main == null)
                return new PlayerPresenceFlags(mainPartyExists: false);

            return new PlayerPresenceFlags(
                mainPartyExists: true,
                isCaptive: PlayerCaptivity.IsCaptive,
                isActive: main.IsActive,
                isVisible: main.IsVisible,
                isInMapEvent: main.MapEvent != null,
                hasPlayerEncounter: PlayerEncounter.Current != null,
                settlementId: main.CurrentSettlement?.StringId);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] GetPresenceFlags failed: {ex.Message}");
            return new PlayerPresenceFlags(mainPartyExists: false);
        }
    }

    public bool ClearArmyAttachment()
    {
        try
        {
            var main = MobileParty.MainParty;
            if (main == null)
                return false;

            if (main.AttachedTo != null)
            {
                _logger?.LogWarning("[EnlistDiag] main party had AttachedTo set — clearing it. Enlistment never sets this; see the feature doc for why attaching is a campaign CTD.");
                main.AttachedTo = null;
            }

            // Leader carve-out: clearing Army while we LEAD one would disband it out from under
            // its members. Only detach from an army we merely belong to.
            if (main.Army != null && main.Army.LeaderParty != main)
                main.Army = null;

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[EnlistDiag] ClearArmyAttachment failed: {ex.Message}");
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

    public PlayerPresenceSnapshot GetPresence(string commanderHeroId = null)
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
                hasPlayerEncounter: PlayerEncounter.Current != null,
                distanceToCommander: string.IsNullOrEmpty(commanderHeroId) ? -1f : GetDistanceToCommander(commanderHeroId),
                armyLeaderPartyId: main.Army?.LeaderParty?.StringId);
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
