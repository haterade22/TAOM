namespace TAOM.Features.MixedFormations.Models;

/// <summary>Player-facing names for the layouts the cycle key steps through.</summary>
public static class FormationLayoutLabels
{
    public static string Describe(FormationLayoutType layout) => layout switch
    {
        FormationLayoutType.InfantryFrontRangedBack => "Infantry front, Ranged back",
        FormationLayoutType.RangedFrontInfantryBack => "Ranged front, Infantry back",
        FormationLayoutType.RangedWingsInfantryCenter => "Ranged wings, Infantry center",
        FormationLayoutType.Checkerboard => "Checkerboard",
        _ => layout.ToString(),
    };
}
