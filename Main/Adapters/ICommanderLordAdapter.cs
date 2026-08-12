namespace TAOM.Adapters;

/// <summary>
/// Read-only world queries about an enlistment commander, keyed by hero StringId and
/// resolved via CampaignObjectManager (never Hero.FindFirst — it scans linearly and
/// returns dead heroes). Culture is read live at call time per the culture-conversion
/// rule; callers must not cache it.
/// </summary>
public interface ICommanderLordAdapter
{
    /// <summary>Never null — returns <see cref="CommanderSnapshot.Missing"/> when the hero doesn't resolve.</summary>
    /// <summary>
    /// The full commander read. FORBIDDEN on any per-frame path: it renders
    /// <c>hero.Name.ToString()</c> and walks Culture / Clan.MapFaction / CurrentSettlement.
    /// Pumps use <see cref="GetTickSnapshot"/>; this is for the hourly reconciler, the load
    /// normalizer and the duty runtime.
    /// </summary>
    CommanderSnapshot GetSnapshot(string heroId);
    /// <summary>
    /// The cheap per-tick read: exactly ONE <c>Find&lt;Hero&gt;</c>, no allocation, no TextObject
    /// render. This is the only commander read permitted on a pump.
    ///
    /// A pump may make at most one <c>Find&lt;Hero&gt;</c> per expensive pass and ZERO
    /// <c>Find&lt;MobileParty&gt;</c> — both are unindexed linear scans over every hero / every
    /// mobile party in the world.
    /// </summary>
    CommanderTickSnapshot GetTickSnapshot(string heroId);


    /// <summary>Live culture StringId of the commander, or null. Read at issuance/decision time only.</summary>
    string GetCultureId(string heroId);

    /// <summary>
    /// StringId of the hero's current party, or null. Cheap counterpart to
    /// <see cref="GetSnapshot"/> for callers that only need the party id — the full snapshot
    /// walks Culture/Clan/MapFaction/Settlement and allocates via Name.ToString(), which is
    /// wasteful on a path that runs for every map event in the world.
    /// </summary>
    string GetPartyId(string heroId);

    /// <summary>
    /// StringId of the party leading the army the commander belongs to, or null when he belongs to
    /// none. Returns his OWN party id when he leads the army himself.
    ///
    /// Deliberately a second cheap lookup rather than a <c>GetSnapshot</c> call: the one consumer
    /// runs for every map event in the world while enlisted, and the full snapshot walks
    /// Culture/Clan/MapFaction/Settlement and allocates through <c>Name.ToString()</c> for data it
    /// never reads.
    /// </summary>
    string GetArmyLeaderPartyId(string heroId);

    /// <summary>True when the hero resolves and is a lord (dialog-gate check).</summary>
    bool IsLord(string heroId);

    /// <summary>True when the hero's map faction is currently at war with the given faction id.</summary>
    bool IsAtWarWithFaction(string heroId, string factionId);

    /// <summary>Adjust the player's relation with the hero (quiet — no toast).</summary>
    bool ApplyPlayerRelation(string heroId, int delta);
}
