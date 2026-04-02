using TAOM.Features;

namespace TAOM.Features.Siege;

public class SiegeDefenseSettingsProvider : ISiegeDefenseSettingsProvider
{
    public bool EnableSiegeDefenseEvents => TaomSettings.Instance?.EnableSiegeDefenseEvents ?? true;
    public int SiegeDefenseResponseDays => TaomSettings.Instance?.SiegeDefenseResponseDays ?? 3;
}
