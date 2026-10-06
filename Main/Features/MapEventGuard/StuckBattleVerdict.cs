namespace TAOM.Features.MapEventGuard;

/// <summary>What the stuck-battle guard does to one map event on one pass.</summary>
public enum StuckBattleVerdict
{
    /// <summary>Leave it to the engine.</summary>
    None,

    /// <summary>Detach the destroyed parties; the next pass judges the event afresh.</summary>
    DetachDestroyed,

    /// <summary>The defenders have no healthy troops: the attackers win.</summary>
    AwardAttacker,

    /// <summary>The attackers have no healthy troops (the defenders hold even when they have none either).</summary>
    AwardDefender,
}
