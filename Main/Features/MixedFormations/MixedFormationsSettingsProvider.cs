using TAOM.Features.MixedFormations.Models;

namespace TAOM.Features.MixedFormations;

public class MixedFormationsSettingsProvider : IMixedFormationsSettingsProvider
{
    public bool IsEnabled => TaomSettings.Instance?.EnableMixedFormations ?? true;

    public FormationLayoutType DefaultLayout =>
        ResolveLayout(TaomSettings.Instance?.MixedFormationsDefaultLayout ?? 0);

    public string CycleHotkey => TaomSettings.Instance?.MixedFormationsCycleHotkey ?? "L";

    public bool IsDebugMode => TaomSettings.Instance?.MixedFormationsDebug ?? false;

    private static FormationLayoutType ResolveLayout(int raw) => raw switch
    {
        0 => FormationLayoutType.InfantryFrontRangedBack,
        1 => FormationLayoutType.RangedFrontInfantryBack,
        2 => FormationLayoutType.RangedWingsInfantryCenter,
        3 => FormationLayoutType.Checkerboard,
        _ => FormationLayoutType.InfantryFrontRangedBack,
    };
}
