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
}
