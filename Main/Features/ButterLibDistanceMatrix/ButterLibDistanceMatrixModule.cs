using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;

namespace TAOM.Features.ButterLibDistanceMatrix;

/// <summary>
/// Switches ButterLib's Distance Matrix off at the first main menu (#740, docs/features/butter-lib-distance-matrix.md).
/// MainMenu is the earliest phase that holds: MCM applies its stored ButterLib toggle (<c>Enable()</c> or
/// <c>Disable()</c>) in its own first main-menu hook, which runs before TAOM's, so a switch at ProcessLoad would be
/// undone. ButterLib ignores <c>Disable()</c> once a campaign has started.
/// </summary>
internal sealed class ButterLibDistanceMatrixModule : TaomFeatureModule
{
    public override string Id => "ButterLibDistanceMatrix";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IButterLibDistanceMatrixAdapter, ButterLibDistanceMatrixAdapter>(Reuse.Singleton);
        registrator.Register<DistanceMatrixSwitch>(Reuse.Singleton);
    }

    public override void OnPhase(ApplyPhase phase, IResolver resolver)
    {
        if (phase == ApplyPhase.MainMenu)
            resolver.Resolve<DistanceMatrixSwitch>().Apply();
    }
}
