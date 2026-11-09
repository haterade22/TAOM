using System.Collections.Generic;

namespace TAOM.Features.MapEventGuard;

/// <summary>
/// Engine-free data about a besieged settlement at a sally-out check (#1099). The roster is filled only when the
/// besieger leader is inside the player's battle, the one case that reads it.
/// </summary>
public sealed class SallySiegeSnapshot
{
    public SallySiegeSnapshot(
        bool leaderInPlayerBattle,
        bool joinsLiveBattle,
        string settlementId,
        string leaderId,
        IReadOnlyList<(int Number, bool MarkedToDie)> roster)
    {
        LeaderInPlayerBattle = leaderInPlayerBattle;
        JoinsLiveBattle = joinsLiveBattle;
        SettlementId = settlementId;
        LeaderId = leaderId;
        Roster = roster;
    }

    public bool LeaderInPlayerBattle { get; }

    /// <summary>The shared battle is a siege-outside or blockade battle, the branch that opens no encounter.</summary>
    public bool JoinsLiveBattle { get; }

    public string SettlementId { get; }

    public string LeaderId { get; }

    public IReadOnlyList<(int Number, bool MarkedToDie)> Roster { get; }
}
