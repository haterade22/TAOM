using TAOM.Features.Siege.Models;

namespace TAOM.Features.Siege;

public interface ISiegeDefenseConfigProvider
{
    SiegeDefenseConfig LoadConfig();
}
