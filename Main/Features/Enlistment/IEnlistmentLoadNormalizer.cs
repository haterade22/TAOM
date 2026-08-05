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
}
