namespace TAOM.Adapters;

/// <summary>
/// Kingdom military strength lookup (WarOfTheRingMomentum). Wraps
/// Kingdom.CurrentTotalStrength — the 1.4.6 rename of TotalStrength.
/// </summary>
public interface IKingdomStrengthAdapter
{
    /// <summary>Current total strength; 0 for an unknown/destroyed kingdom id.</summary>
    float GetTotalStrength(string kingdomId);
}
