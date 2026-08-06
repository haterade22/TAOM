using DryIoc;

namespace TAOM.Features.EconomyDiagnostics;

public static class EconomyDiagnosticsIoC
{
    public static void RegisterEconomyDiagnosticsFeature(IContainer container)
    {
        // MUST be Singleton — shared state: the ChangeGold recorder writes, the console command
        // reads, and the behavior rolls the day.
        container.Register<ITownGoldLedger, TownGoldLedger>(Reuse.Singleton);
        container.Register<EconomyDiagnosticsBehavior>(Reuse.Singleton);
    }
}
