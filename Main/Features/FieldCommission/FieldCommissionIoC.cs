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

        // Two-layer config: the JSON provider is registered as a CONCRETE type and the interface
        // resolves to the MCM-merging decorator around it, so every consumer reads live MCM values
        // without a constructor change. RegisterDelegate rather than DryIoc's Setup.Decorator — the
        // dependency is spelled out, so it cannot silently resolve to itself and recurse.
        container.Register<FieldCommissionConfigProvider>(Reuse.Singleton);
        container.RegisterDelegate<IFieldCommissionConfigProvider>(
            r => new FieldCommissionSettingsProvider(r.Resolve<FieldCommissionConfigProvider>()),
            Reuse.Singleton);
        container.Register<IFieldCommissionMeritService, FieldCommissionMeritService>(Reuse.Singleton);
        container.Register<IFieldCommissionOfferFlowService, FieldCommissionOfferFlowService>(Reuse.Singleton);

        // Dismissal back to the ranks (#540): the stateless verdict + inquiry chain, and its two
        // thin entry points, resolved by SubModule for AddBehavior (the Enlistment precedent).
        container.Register<IFieldCommissionDismissService, FieldCommissionDismissService>(Reuse.Singleton);
        container.Register<Hooks.FieldCommissionDismissDialogBehavior>(Reuse.Singleton);
        container.Register<Hooks.FieldCommissionDismissMenuBehavior>(Reuse.Singleton);
    }
}
