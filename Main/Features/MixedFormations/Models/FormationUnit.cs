namespace TAOM.Features.MixedFormations.Models;

public readonly struct FormationUnit
{
    public int Index { get; }
    public bool IsRanged { get; }

    public FormationUnit(int index, bool isRanged)
    {
        Index = index;
        IsRanged = isRanged;
    }
}
