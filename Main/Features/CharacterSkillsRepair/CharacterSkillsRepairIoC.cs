using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.CharacterSkillsRepair;

public static class CharacterSkillsRepairIoC
{
    public static void RegisterCharacterSkillsRepairFeature(IContainer container)
    {
        container.Register<ICharacterSkillsAdapter, CharacterSkillsAdapter>(Reuse.Singleton);
        container.Register<ICharacterSkillsRepairService, CharacterSkillsRepairService>(Reuse.Singleton);
    }
}
