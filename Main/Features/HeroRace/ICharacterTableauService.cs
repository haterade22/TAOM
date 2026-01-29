using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.View.Tableaus;

namespace TAOM.Features.HeroRace;

public interface ICharacterTableauService
{
    void RefreshCharacterTableau(CharacterTableau tableau, Equipment oldEquipment = null);
}
