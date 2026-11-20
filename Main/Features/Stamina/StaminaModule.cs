using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Features.Stamina.Hooks;

namespace TAOM.Features.Stamina;

/// <summary>
/// Combat stamina (#1497, docs/features/stamina.md): every soldier in a battle has a stamina band that slows him as he
/// tires and locks an exhausted soldier's attack, block, run and jump. No save data: stamina lives inside one battle.
/// The band reaches the engine through the two existing stat models (<see cref="StaminaHooks.ApplyStats"/>), never a
/// model of its own: a second stat model would replace the first. Patch171 is the player half of the lock; it applies
/// at the first mission, as Marshal's Word's patches do, so that preparing the patched controller method cannot run a
/// type initializer that needs Mission.Current.
/// </summary>
internal sealed class StaminaModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch171_Stamina";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.FirstMission),
    };

    private static readonly MissionBehaviorDecl[] Missions =
    {
        MissionBehaviorDecl.Of((_, r) => new StaminaMissionLogic(r.Resolve<StaminaRuntime>())),
    };

    public override string Id => "Stamina";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<StaminaConfigProvider>(Reuse.Singleton);
        registrator.Register<StaminaSettingsProvider>(Reuse.Singleton);
        registrator.Register<StaminaRuntime>(Reuse.Singleton);
    }

    public override void InitializeStatics(IResolver resolver) => StaminaHooks.Runtime = resolver.Resolve<StaminaRuntime>();

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;

    public override IReadOnlyList<MissionBehaviorDecl> MissionBehaviors => Missions;
}
