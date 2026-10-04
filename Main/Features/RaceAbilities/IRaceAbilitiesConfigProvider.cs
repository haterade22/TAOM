using TAOM.Features.RaceAbilities.Domain;

namespace TAOM.Features.RaceAbilities;

// Faked by the resolver's tests.
public interface IRaceAbilitiesConfigProvider
{
    RaceAbilitiesConfig GetConfig();
}
