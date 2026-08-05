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
    CommanderSnapshot GetSnapshot(string heroId);

    /// <summary>Live culture StringId of the commander, or null. Read at issuance/decision time only.</summary>
    string GetCultureId(string heroId);

    /// <summary>True when the hero resolves and is a lord (dialog-gate check).</summary>
    bool IsLord(string heroId);

    /// <summary>True when the hero's map faction is currently at war with the given faction id.</summary>
    bool IsAtWarWithFaction(string heroId, string factionId);

    /// <summary>Adjust the player's relation with the hero (quiet — no toast).</summary>
    bool ApplyPlayerRelation(string heroId, int delta);
}
