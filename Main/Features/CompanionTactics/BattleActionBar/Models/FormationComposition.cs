namespace TAOM.Features.CompanionTactics.BattleActionBar.Models;

/// <summary>
/// Read-only composition flags for a formation's action-bar context. Computed once per
/// refresh by <c>IFormationCompositionAnalyzer</c>; the BattleActionBarService maps these
/// onto button categories.
/// </summary>
public readonly struct FormationComposition
{
    public bool HasRanged { get; }
    public bool HasPolearm { get; }
    public bool HasShields { get; }
    public bool HasCavalry { get; }
    public bool IsRangedDominant { get; }
    public bool IsCavalryDominant { get; }

    public FormationComposition(bool hasRanged, bool hasPolearm, bool hasShields, bool hasCavalry,
        bool isRangedDominant, bool isCavalryDominant)
    {
        HasRanged = hasRanged;
        HasPolearm = hasPolearm;
        HasShields = hasShields;
        HasCavalry = hasCavalry;
        IsRangedDominant = isRangedDominant;
        IsCavalryDominant = isCavalryDominant;
    }
}
