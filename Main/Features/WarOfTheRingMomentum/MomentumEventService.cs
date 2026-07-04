using System;
using TAOM.Adapters;
using TAOM.Features.WarOfTheRingMomentum.Domain;
using TAOM.Features.WarOfTheRingMomentum.Models;
using TAOM.Features.WarOfTheRingMomentum.Snapshots;

namespace TAOM.Features.WarOfTheRingMomentum;

/// <summary>
/// LOTRAOM parity source: MomentumEventService. Preserved quirks: kill STATS accrue
/// before the battle-validity filter (so invalid battle types still count kills);
/// multiplied values use truncating int casts; zero-value events are still enqueued.
/// Deliberate deviations: raids require an enrolled attacker kingdom (LOTRAOM sided
/// raids by culture — bandit raids fed Evil); side membership replaces culture checks
/// (enrolled ids are the side source of truth); and CONFIG VALUES ARE INTERNAL-SCALE
/// UNITS scaled ×100 into the store here. LOTRAOM's shipped code added config values
/// raw while comparing (raw/100) against the threshold, so its own tuning comments
/// ("~2 sieges needed for victory") were off by 100× — the meter barely moved and
/// threshold victory needed ~200 sieges. This implements the documented intent:
/// siege 250 + siege 250 ≥ threshold 500.
/// </summary>
public class MomentumEventService : IMomentumEventService
{
    private readonly IMomentumConfigProvider _configProvider;
    private readonly IPlayerMomentumService _playerService;
    private readonly IKingdomStrengthAdapter _strengthAdapter;
    private readonly IMomentumTextService _textService;

    public MomentumEventService(
        IMomentumConfigProvider configProvider,
        IPlayerMomentumService playerService,
        IKingdomStrengthAdapter strengthAdapter,
        IMomentumTextService textService)
    {
        _configProvider = configProvider;
        _playerService = playerService;
        _strengthAdapter = strengthAdapter;
        _textService = textService;
    }

    private MomentumConfig Config => _configProvider.GetConfig();

    public void ProcessBattle(BattleOutcomeSnapshot battle, MomentumWarState state, double nowHours)
    {
        if (battle == null || !state.HasWarStarted || state.HasWarEnded || !battle.HasWinner)
            return;
        if (string.IsNullOrEmpty(battle.AttackerFactionId) || string.IsNullOrEmpty(battle.DefenderFactionId))
            return;
        if (!state.DoesKingdomTakePart(battle.AttackerFactionId) || !state.DoesKingdomTakePart(battle.DefenderFactionId))
            return;

        // Kill stats accrue for BOTH sides before the validity filter (LOTRAOM ordering):
        // each side's kill count grows by the OTHER side's casualties.
        var attackerSide = SideOf(state, battle.AttackerFactionId);
        var defenderSide = SideOf(state, battle.DefenderFactionId);
        state.GetSide(attackerSide).TotalStats.AddKills(battle.DefenderCasualties);
        state.GetSide(defenderSide).TotalStats.AddKills(battle.AttackerCasualties);

        if (!battle.IsValidBattleType || !battle.DefenderIsMobileParty
            || !battle.AttackerIsKingdomFaction || !battle.DefenderIsKingdomFaction)
            return;

        string winnerId = battle.AttackerWon ? battle.AttackerFactionId : battle.DefenderFactionId;
        string loserId = battle.AttackerWon ? battle.DefenderFactionId : battle.AttackerFactionId;
        int loserCasualties = battle.AttackerWon ? battle.DefenderCasualties : battle.AttackerCasualties;

        int momentumGain = CalculateBattleMomentum(state, loserId, loserCasualties, battle.PlayerInvolved);

        if (battle.PlayerInvolved)
            _playerService.RecordPlayerEvent(MomentumActionType.BattleWon);

        string description = _textService.BattleWonDescription(
            battle.AttackerWon ? battle.AttackerFactionName : battle.DefenderFactionName,
            battle.AttackerWon ? battle.AttackerLeaderName : battle.DefenderLeaderName,
            battle.AttackerWon ? battle.DefenderFactionName : battle.AttackerFactionName,
            battle.AttackerWon ? battle.DefenderLeaderName : battle.AttackerLeaderName,
            loserCasualties);

        AddEvent(state, SideOf(state, winnerId), MomentumActionType.BattleWon, momentumGain, description, nowHours);
    }

    public void ProcessSiege(SiegeOutcomeSnapshot siege, MomentumWarState state, double nowHours)
    {
        if (siege == null || !state.HasWarStarted || state.HasWarEnded)
            return;
        if (string.IsNullOrEmpty(siege.CaptorFactionId) || !state.DoesKingdomTakePart(siege.CaptorFactionId))
            return;

        var side = SideOf(state, siege.CaptorFactionId);
        state.GetSide(side).TotalStats.AddSettlementCaptured();

        int value = Config.Events.SiegeMomentum * MomentumWarState.MomentumScale;
        if (siege.PlayerInvolved)
        {
            value = (int)(value * _playerService.GetParticipationMultiplier(true));
            _playerService.RecordPlayerEvent(MomentumActionType.Sieges);
        }

        string description = _textService.SiegeDescription(
            siege.CaptorFactionName, siege.CaptorLeaderName, siege.SettlementName);

        AddEvent(state, side, MomentumActionType.Sieges, value, description, nowHours);
    }

    public void ProcessRaid(RaidOutcomeSnapshot raid, MomentumWarState state, double nowHours)
    {
        if (raid == null || !state.HasWarStarted || state.HasWarEnded || !raid.AttackerVictory)
            return;
        if (string.IsNullOrEmpty(raid.AttackerFactionId) || !state.DoesKingdomTakePart(raid.AttackerFactionId))
            return;

        var side = SideOf(state, raid.AttackerFactionId);
        state.GetSide(side).TotalStats.AddRaid();

        string description = _textService.RaidDescription(
            raid.AttackerPartyName, raid.AttackerFactionName, raid.SettlementName);

        AddEvent(state, side, MomentumActionType.VillageRaided,
            Config.Events.RaidMomentum * MomentumWarState.MomentumScale, description, nowHours);
    }

    public void ProcessArmyGathered(ArmyGatheredSnapshot army, MomentumWarState state, double nowHours)
    {
        if (army == null || !state.HasWarStarted || state.HasWarEnded)
            return;
        if (string.IsNullOrEmpty(army.KingdomId) || !state.DoesKingdomTakePart(army.KingdomId))
            return;

        string description = _textService.ArmyGatheredDescription(army.ArmyLeaderName);

        AddEvent(state, SideOf(state, army.KingdomId), MomentumActionType.ArmyGathered,
            Config.Events.ArmyMomentum * MomentumWarState.MomentumScale, description, nowHours);
    }

    public void ProcessDailyTick(MomentumWarState state, double nowHours)
    {
        if (!state.HasWarStarted || state.HasWarEnded)
            return;

        state.Free.ProcessExpiredEvents(nowHours);
        state.Evil.ProcessExpiredEvents(nowHours);
        AwardDailyStrengthMomentum(state, nowHours);
    }

    private void AwardDailyStrengthMomentum(MomentumWarState state, double nowHours)
    {
        float freeStrength = GetSideStrength(state.Free);
        float evilStrength = GetSideStrength(state.Evil);

        if (freeStrength == 0f || evilStrength == 0f)
            return;

        bool freeIsStronger = freeStrength > evilStrength;
        float ratio = freeIsStronger ? freeStrength / evilStrength : evilStrength / freeStrength;

        float excessRatio = ratio - 1f;
        float maxExcess = Config.StrengthRatioForMaxMomentum - 1f;
        float normalized = Math.Min(excessRatio / maxExcess, 1.0f);

        int value = (int)(normalized * Config.Events.MaxStrengthMomentum * MomentumWarState.MomentumScale);

        if (_playerService.IsPlayerOnStrongerSide(state, freeStrength, evilStrength))
            value = (int)(value * _playerService.GetParticipationMultiplier(true));

        string description = _textService.StrengthDescription((int)(excessRatio * 100));

        AddEvent(state, freeIsStronger ? MomentumSide.Free : MomentumSide.Evil,
            MomentumActionType.RelativeStrength, value, description, nowHours);
    }

    private int CalculateBattleMomentum(MomentumWarState state, string loserKingdomId, int casualties, bool playerInvolved)
    {
        float loserSideStrength = GetSideStrength(state.GetSide(SideOf(state, loserKingdomId)));
        if (loserSideStrength == 0f)
            return 0;

        // ×100 at full precision — this is the "battle precision" the ×100 store exists for.
        // Clamp the ratio at 1.0 so a single lopsided endgame battle (casualties > the
        // loser side's residual strength) can't blow past MaxBattleMomentum and instant-win
        // the war — the config documents this value as a CAP (Codex #327 MED).
        float percentageLost = Math.Min(casualties / loserSideStrength, 1f);
        int baseMomentum = (int)Math.Round(percentageLost * Config.Events.MaxBattleMomentum * MomentumWarState.MomentumScale);

        if (playerInvolved)
            baseMomentum = (int)(baseMomentum * _playerService.GetParticipationMultiplier(true));

        return baseMomentum;
    }

    private float GetSideStrength(MomentumSideData side)
    {
        float total = 0f;
        foreach (var kingdomId in side.KingdomIds)
            total += _strengthAdapter.GetTotalStrength(kingdomId);
        return total;
    }

    private static MomentumSide SideOf(MomentumWarState state, string kingdomId)
    {
        return state.Free.ContainsKingdom(kingdomId) ? MomentumSide.Free : MomentumSide.Evil;
    }

    private void AddEvent(MomentumWarState state, MomentumSide side, MomentumActionType type,
        int value, string description, double nowHours)
    {
        double endHours = nowHours + Config.DurationsHours.GetHours(type);
        state.GetSide(side).AddEvent(new MomentumEvent(value, description, type, endHours));
    }
}
