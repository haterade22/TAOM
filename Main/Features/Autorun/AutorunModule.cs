using DryIoc;
using TAOM.Composition;
using TAOM.Features.Autorun.Scenario;

namespace TAOM.Features.Autorun;

/// <summary>The unattended campaign driver (#1075): no patch or save data; TAOM.Client replaces the null host (ADR-013).</summary>
internal sealed class AutorunModule : TaomFeatureModule
{
    public override string Id => "Autorun";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.RegisterDelegate(_ => ScenarioPlan.Resolve(), Reuse.Singleton);
        registrator.Register<ICampaignAutorunHost, NullCampaignAutorunHost>(Reuse.Singleton);
        registrator.Register<ScenarioAutorunDriver>(Reuse.Singleton);
    }
}
