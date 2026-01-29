using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.View.Scripts;

namespace TAOM.Features.HeroRace;

public interface ICharacterSpawnerService
{
    void InitWithCharacter(CharacterSpawner spawner, CharacterCode characterCode, bool useBodyProperties = false);
}
