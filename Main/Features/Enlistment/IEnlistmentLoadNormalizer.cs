namespace TAOM.Features.Enlistment;

/// <summary>
/// Load-time state normalization (the Entity State Matrix). Restore-direction mutations
/// are safe everywhere; park-direction only when every precondition verifies; and no load
/// path may ever leave an ownerless hidden MainParty. Runs once per game load, authority-
/// gated by the behavior.
/// </summary>
public interface IEnlistmentLoadNormalizer
{
    void Normalize(string currentMainHeroId, double nowDays);

    /// <summary>
    /// The heir-mismatch discharge that a load held back because the soldier was still in his
    /// battle (#1030). The hourly tick calls it; it discharges once the party is out of the map
    /// event and out of captivity, and returns true when it did. Without a held discharge it does
    /// nothing: a mid-session heir change keeps its old handling (the next load).
    /// </summary>
    bool DischargeHeirMismatchOutsideBattle(string currentMainHeroId);
}
