using System;
using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;

namespace TAOM.Features.MarshalsWord;

/// <summary>
/// Marshal's Word (docs/features/marshals-word.md): registers what the client's <c>MarshalView</c> resolves, the rebindable keys and
/// Patch141. The category applies at the first mission, because the patched members' bodies reach <c>MovementOrder</c>, whose type initializer
/// reads <c>Mission.Current</c> (MovementOrder.cs:175, 1245-1253), and a patch applied earlier can reach it through JIT preparation (as
/// Fords of War does, Main/SubModule.cs:2117-2125).
/// </summary>
internal sealed class MarshalsWordModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch141_MarshalsWord";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.FirstMission),
    };

    public override string Id => "MarshalsWord";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IMarshalSettingsProvider, MarshalSettingsProvider>(Reuse.Singleton);
        registrator.Register<IMarshalOrderAdapter, MarshalOrderAdapter>(Reuse.Transient);
        registrator.Register<IMarshalFireAdapter, MarshalFireAdapter>(Reuse.Transient);
        registrator.Register<IMarshalOutlineAdapter<TaleWorlds.MountAndBlade.Agent>, MarshalOutlineAdapter>(Reuse.Transient);
        registrator.Register<MarshalHotKeys>(Reuse.Singleton);
    }

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;

    // After Native has cleared and filled the key contexts, as the Seat does. A failure leaves the keys unbound; letting it escape would
    // fault the module.
    public override void OnPhase(ApplyPhase phase, IResolver resolver)
    {
        if (phase != ApplyPhase.ProcessLoad)
            return;
        try
        {
            TaomMarshalHotKeyCategory.Register();
        }
        catch (Exception ex)
        {
            resolver.Resolve<IModLogger>().LogError(
                $"[MarshalsWord] hotkey registration failed, the Marshal's Word keys stay unbound: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
