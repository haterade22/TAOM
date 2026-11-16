namespace TAOM.Features.MixedFormations.Models;

/// <summary>Player-facing names for the layouts the cycle key steps through, each as <c>{=key}English</c>.</summary>
public static class FormationLayoutLabels
{
    public static string Describe(FormationLayoutType layout) => layout switch
    {
        FormationLayoutType.InfantryFrontRangedBack => "{=taom_mf_layout_inf_front}Infantry front, Ranged back",
        FormationLayoutType.RangedFrontInfantryBack => "{=taom_mf_layout_ranged_front}Ranged front, Infantry back",
        FormationLayoutType.RangedWingsInfantryCenter => "{=taom_mf_layout_ranged_wings}Ranged wings, Infantry center",
        FormationLayoutType.Checkerboard => "{=taom_mf_layout_checkerboard}Checkerboard",
        _ => layout.ToString(),
    };
}
