using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;

namespace TAOM.Features.TournamentRewards;

/// <summary>
/// Tournament rewards (docs/features/tournament-rewards.md, Mike 2026-10-02): the MCM bet cap, renown and influence
/// scaled by the field and the winner's culture, and the prize and skill chosen at Join. The renown and influence
/// overrides live on the Arena feature's TaomTournamentModel, which reaches this module's service through
/// ITournamentService. Patch96's three patches target campaign and SandBox types, so they apply at GameInit.
/// </summary>
internal sealed class TournamentRewardsModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch96_TournamentRewards";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.GameInit),
    };

    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new TournamentRewardsBehavior(
            r.Resolve<TournamentRewardsService>(),
            r.Resolve<TournamentSkillAwardService>())),
    };

    public override string Id => "TournamentRewards";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<ITournamentRewardsSettingsProvider, TournamentRewardsSettingsProvider>(Reuse.Singleton);
        registrator.Register<ITournamentRewardsConfigProvider, TournamentRewardsConfigProvider>(Reuse.Singleton);
        registrator.Register<ITournamentJoinAdapter, TournamentJoinAdapter>(Reuse.Singleton);
        registrator.Register<ITournamentChoicePresenter, TournamentChoicePresenter>(Reuse.Singleton);
        registrator.Register<TournamentRewardsService>(Reuse.Singleton);
        registrator.Register<TournamentJoinService>(Reuse.Singleton);
        registrator.Register<TournamentSkillAwardService>(Reuse.Singleton);
    }

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
