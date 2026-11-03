using DryIoc;
using TAOM.Composition;
using TAOM.Features.LongDefeat.Spike;

namespace TAOM.Features.LongDefeat;

/// <summary>The Long Defeat (#1079, docs/features/long-defeat.md): no mission behavior, patch or save data; TAOM.Client replaces the null game-manager factory (ADR-013).</summary>
internal sealed class LongDefeatModule : TaomFeatureModule
{
    public override string Id => "LongDefeat";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<ILongDefeatGameManagerFactory, NullLongDefeatGameManagerFactory>(Reuse.Singleton);
        registrator.Register<LongDefeatSpikeLauncher>(Reuse.Singleton);
    }
}
