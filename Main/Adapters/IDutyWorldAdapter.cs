namespace TAOM.Adapters;

/// <summary>
/// Engine boundary for the Enlistment field-duty system (issue #375 Phase 5). Every
/// method that touches a sealed TaleWorlds type for duty spawning/AI/food/enemy-scan
/// lives here so <c>FieldDutyRuntime</c> stays a plain testable service (ADR-007).
/// All methods fail soft (null/0/false + a logged warning) — a duty world-read failing
/// must never throw into the daily/hourly campaign tick.
/// </summary>
public interface IDutyWorldAdapter
{
    /// <summary>
    /// Spawns a looter-clan party (<c>Clan.BanditFactions</c> "looters") anchored to the
    /// given settlement, near its gate. <paramref name="patrolNotEngage"/> selects the
    /// initial AI (patrol the anchor vs. engage the player immediately — <see cref="SetPartyAi"/>
    /// can also be called after spawn to change it). Returns the new party's StringId, or
    /// null when the looter clan or settlement can't be resolved.
    /// </summary>
    string SpawnLooterParty(string idPrefix, string anchorSettlementId, bool patrolNotEngage);

    /// <summary>Re-issues the hunt target's AI order: engage the player's main party, or patrol its anchor settlement.</summary>
    void SetPartyAi(string partyId, bool engagePlayer, string anchorSettlementId);

    /// <summary>Idempotent — a party already destroyed (e.g. by the player's own kill) is a silent no-op.</summary>
    void DestroyParty(string partyId);

    /// <summary>Nearest settlement sharing the commander's faction, or null when none exist. One bounded <c>Settlement.All</c> scan — call only at duty start, never per tick.</summary>
    string FindNearestFriendlySettlement(string commanderHeroId);

    /// <summary>Nearest VILLAGE sharing the commander's faction, or null.</summary>
    string FindNearestFriendlyVillage(string commanderHeroId);

    /// <summary>Nearest settlement of a different, non-hostile faction (an "ally" in the duty-content sense), or null.</summary>
    string FindNearestAllySettlement(string commanderHeroId);

    /// <summary>Total food count in the player's main party roster (<c>ItemRoster.TotalFood</c>).</summary>
    int CountPlayerFood();

    /// <summary>Removes up to <paramref name="amount"/> food, cheapest items first. No-op for amount &lt;= 0.</summary>
    /// <summary>Removes up to <paramref name="amount"/> deliverable food; returns how much it actually took.</summary>
    int ConsumePlayerFood(int amount);

    /// <summary>Grants food (as grain) to the player's main party. No-op for amount &lt;= 0.</summary>
    void GrantPlayerFood(int amount);

    /// <summary>True when a hostile party is within <paramref name="radius"/> of the player's main party (locator-grid bounded scan, not a full MobileParty.All sweep).</summary>
    bool IsEnemyNearPlayer(float radius);
}
