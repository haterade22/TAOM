using System.Collections.Generic;
using TAOM.Features.CharacterCreation.Models;

namespace TAOM.Features.CharacterCreation;

public interface INarrativeDataProvider
{
    IReadOnlyList<NarrativeOptionDefinition> LoadMenuOptions(string menuName);
}
