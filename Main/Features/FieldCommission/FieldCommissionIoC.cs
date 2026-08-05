using DryIoc;
using TAOM.Adapters;
using TAOM.Features.Enlistment;
using TAOM.Features.FieldCommission.Domain;

namespace TAOM.Features.FieldCommission;

/// <summary>
/// Registers Battlefield Promotions (#376). Wiring note for <c>Main/IoC.cs</c> (single-owner,
/// not edited here): call
/// <c>Features.Enlistment.EnlistmentIoC.RegisterEnlistmentFeature(container)</c> BEFORE
/// <c>RegisterFieldCommissionFeature(container)</c> so Enlistment's real
/// <c>IEnlistmentStateQuery</c> registration wins; <see cref="NullEnlistmentStateQuery"/> is
/// registered with <c>IfAlreadyRegistered.Keep</c> as the fallback if Enlistment is ever absent
/// from the registration order.
/// </summary>
internal static class FieldCommissionIoC
{
    internal static void RegisterFieldCommissionFeature(IContainer container)
    {
        // Feature-owned adapters.
        container.Register<ITroopRosterQueryAdapter, TroopRosterQueryAdapter>(Reuse.Singleton);
        container.Register<IHeroCommissionAdapter, HeroCommissionAdapter>(Reuse.Singleton);
        container.Register<IInquiryPresenterAdapter, InquiryPresenterAdapter>(Reuse.Singleton);

        // Pure domain helpers (stateless — registered as concrete types, no interface needed).
        container.Register<CommissionSkillBudget>(Reuse.Singleton);
        container.Register<TroopUpgradeGraph>(Reuse.Singleton);

        // Cross-feature fallback — see the class doc on registration order.
        container.Register<IEnlistmentStateQuery, NullEnlistmentStateQuery>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.Keep);

        container.Register<IFieldCommissionConfigProvider, FieldCommissionConfigProvider>(Reuse.Singleton);
        container.Register<IFieldCommissionMeritService, FieldCommissionMeritService>(Reuse.Singleton);
        container.Register<IFieldCommissionOfferFlowService, FieldCommissionOfferFlowService>(Reuse.Singleton);
    }
}
