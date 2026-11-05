using System;
using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;

namespace TAOM.Features.SeatOfSeeing;

/// <summary>
/// Seat of Seeing (docs/features/seat-of-seeing.md): registers what the client's <c>SeatView</c> resolves, the rebindable key and Patch140.
/// </summary>
internal sealed class SeatOfSeeingModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch140_SeatOfSeeing";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.GameInit),
    };

    public override string Id => "SeatOfSeeing";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<ISeatSettingsProvider, SeatSettingsProvider>(Reuse.Singleton);
        registrator.Register<ISeatHeroAdapter, SeatHeroAdapter>(Reuse.Singleton);
        registrator.Register<SeatHotKeys>(Reuse.Singleton);
    }

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;

    // The order panel's transpiler logs a drifted anchor at error level, which fails a field run.
    public override void InitializeStatics(IResolver resolver) => SeatPatchLog.Logger = resolver.Resolve<IModLogger>();

    // After Native has cleared and filled the key contexts, as the time controls do. A failure leaves the key unbound; letting it
    // escape would fault the module.
    public override void OnPhase(ApplyPhase phase, IResolver resolver)
    {
        if (phase != ApplyPhase.ProcessLoad)
            return;
        try
        {
            TaomSeatHotKeyCategory.Register();
        }
        catch (Exception ex)
        {
            resolver.Resolve<IModLogger>().LogError(
                $"[SeatOfSeeing] hotkey registration failed, the Seat key stays unbound: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
