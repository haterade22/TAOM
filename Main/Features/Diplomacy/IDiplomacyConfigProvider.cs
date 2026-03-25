using TAOM.Features.Diplomacy.Models;

namespace TAOM.Features.Diplomacy;

public interface IDiplomacyConfigProvider
{
    DiplomacyConfig LoadConfig();
}
