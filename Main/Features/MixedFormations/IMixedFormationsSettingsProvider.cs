using TAOM.Features.MixedFormations.Models;

namespace TAOM.Features.MixedFormations;

public interface IMixedFormationsSettingsProvider
{
    bool IsEnabled { get; }
    FormationLayoutType DefaultLayout { get; }
    string CycleHotkey { get; }
    bool IsDebugMode { get; }
}
