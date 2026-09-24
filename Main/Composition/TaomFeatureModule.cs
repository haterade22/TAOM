using System;
using System.Collections.Generic;
using DryIoc;

namespace TAOM.Composition;

/// <summary>
/// Empty defaults for <see cref="ITaomFeatureModule"/>: an enabled module that registers, declares
/// and does nothing. A feature overrides only the dimensions it has.
/// </summary>
internal abstract class TaomFeatureModule : ITaomFeatureModule
{
    public abstract string Id { get; }

    public virtual FeatureState State => FeatureState.Enabled;

    public virtual string? ParkedReason => null;

    public virtual bool OwnsSaveData => false;

    public virtual void RegisterServices(IRegistrator registrator) { }

    public virtual void InitializeStatics(IResolver resolver) { }

    public virtual IReadOnlyList<PatchCategoryDecl> PatchCategories => Array.Empty<PatchCategoryDecl>();

    public virtual IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Array.Empty<CampaignBehaviorDecl>();

    public virtual IReadOnlyList<GameModelDecl> GameModels => Array.Empty<GameModelDecl>();

    public virtual IReadOnlyList<MissionBehaviorDecl> MissionBehaviors => Array.Empty<MissionBehaviorDecl>();

    public virtual void OnPhase(ApplyPhase phase, IResolver resolver) { }
}
