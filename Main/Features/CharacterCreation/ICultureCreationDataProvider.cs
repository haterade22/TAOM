using System.Collections.Generic;
using TAOM.Features.CharacterCreation.Models;

namespace TAOM.Features.CharacterCreation;

public interface ICultureCreationDataProvider
{
    IReadOnlyList<CultureCreationData> LoadCultures();
    CultureCreationData GetCultureData(string cultureId);
}
