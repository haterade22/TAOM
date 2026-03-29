using DryIoc;

namespace TAOM.Features.BattleBalance;

public static class BattleBalanceIoC
{
    public static void RegisterBattleBalanceFeature(IContainer container)
    {
        container.Register<IBattleBalanceConfigProvider, BattleBalanceConfigProvider>(Reuse.Singleton);
        container.Register<IBattleBalanceSettingsProvider, BattleBalanceSettingsProvider>(Reuse.Singleton);
    }
}
