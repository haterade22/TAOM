using System.Collections.Generic;
using DryIoc;

namespace TAOM.Composition;

/// <summary>When a feature module's patch categories apply: one value per SubModule hook that applies categories.</summary>
internal enum ApplyPhase
{
    /// <summary>OnSubModuleLoad, once per process.</summary>
    ProcessLoad,

    /// <summary>The first OnBeforeInitialModuleScreenSetAsRoot, once per process.</summary>
    MainMenu,

    /// <summary>The first OnGameInitializationFinished, once per process.</summary>
    GameInit,

    /// <summary>The first OnMissionBehaviorInitialize, once Mission.Current exists, once per process.</summary>
    FirstMission,
}

/// <summary>Compile-time only; never read from MCM (persisted MCM values outlive default flips).</summary>
internal enum FeatureState
{
    Enabled,
    Parked,
}

/// <summary>
/// One feature's complete wiring, owned by the feature's folder instead of SubModule.cs and IoC.cs.
/// The runner visits modules in <see cref="FeatureModules.All"/> order: <see cref="RegisterServices"/>
/// for every module (parked ones included), then <see cref="InitializeStatics"/> after every
/// registration, then per lifecycle phase its categories, <see cref="OnPhase"/>, behaviors, models
/// and mission behaviors. Derive from <see cref="TaomFeatureModule"/>, which supplies empty defaults
/// (net472 has no default interface members).
/// </summary>
internal interface ITaomFeatureModule
{
    /// <summary>The feature name, as in "WandererAllegiance"; used in every log line and test message.</summary>
    string Id { get; }

    FeatureState State { get; }

    /// <summary>Issue references for a parked module; null when enabled.</summary>
    string? ParkedReason { get; }

    /// <summary>True when any of the module's behaviors persists data in SyncData; such a module fails closed.</summary>
    bool OwnsSaveData { get; }

    /// <summary>Container registrations only. IRegistrator has no Resolve, so an eager resolve does not compile.</summary>
    void RegisterServices(IRegistrator registrator);

    /// <summary>Patch-static handshakes, after every module and hand-wired feature has registered.</summary>
    void InitializeStatics(IResolver resolver);

    IReadOnlyList<PatchCategoryDecl> PatchCategories { get; }

    IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors { get; }

    IReadOnlyList<GameModelDecl> GameModels { get; }

    IReadOnlyList<MissionBehaviorDecl> MissionBehaviors { get; }

    /// <summary>Non-patch side effects for a phase (hotkeys, watchdog starts), after that phase's categories.</summary>
    void OnPhase(ApplyPhase phase, IResolver resolver);
}
