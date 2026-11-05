using TAOM.Features.MixedFormations.Models;

namespace TAOM.Features.MixedFormations;

public class MixedFormationsSettingsProvider : IMixedFormationsSettingsProvider
{
    // Read every frame by MixedFormationsMissionBehavior and per unit by Patch30: cached on the first
    // non-null read, read through (BattleBalanceSettingsProvider pattern).
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public MixedFormationsSettingsProvider() { }
    internal MixedFormationsSettingsProvider(TaomSettings settings) => _settings = settings;

    public bool IsEnabled => Settings?.EnableMixedFormations ?? true;

    public FormationLayoutType DefaultLayout =>
        ResolveLayout(Settings?.MixedFormationsDefaultLayout ?? 0);

    public string CycleHotkey => Settings?.MixedFormationsCycleHotkey ?? "L";

    public bool IsDebugMode => Settings?.MixedFormationsDebug ?? false;

    private static FormationLayoutType ResolveLayout(int raw) => raw switch
    {
        0 => FormationLayoutType.InfantryFrontRangedBack,
        1 => FormationLayoutType.RangedFrontInfantryBack,
        2 => FormationLayoutType.RangedWingsInfantryCenter,
        3 => FormationLayoutType.Checkerboard,
        _ => FormationLayoutType.InfantryFrontRangedBack,
    };
}
