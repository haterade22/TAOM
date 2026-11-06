using DryIoc;

namespace TAOM.Features.DwarfAttacks;

public static class DwarfAttacksIoC
{
    public static void RegisterDwarfAttacksFeature(IContainer container)
    {
        // Pure, stateless decisions, a live settings reader, and the per-process check latch -> Singleton
        // (csharp-architecture.md). No interfaces: no test fakes them and nothing else implements them.
        container.Register<DwarfAttackService>(Reuse.Singleton);
        container.Register<DwarfAttacksSettingsProvider>(Reuse.Singleton);
        container.Register<DwarfAttackChecks>(Reuse.Singleton);
    }
}
