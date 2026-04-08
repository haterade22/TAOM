using System.Collections.Generic;
using TAOM.Features.NamedCompanions.Domain;

namespace TAOM.Features.NamedCompanions;

public interface INamedCompanionConfigProvider
{
    IReadOnlyList<NamedCompanionDefinition> GetCompanions();
}
