using DryIoc;
using TAOM.Adapters;
using TAOM.Features.Diplomacy.Hooks;

namespace TAOM.Features.Diplomacy;

public static class DiplomacyIoC
{
    public static void RegisterDiplomacyFeature(IContainer container)
    {
        container.Register<IAllianceAdapter, AllianceAdapter>(Reuse.Singleton);
        container.Register<IDiplomacyConfigProvider, DiplomacyConfigProvider>(Reuse.Singleton);
        container.Register<IDiplomacyService, DiplomacyService>(Reuse.Singleton);
        container.Register<IOnAllianceAction, AllianceActionHook>(Reuse.Singleton);
        container.Register<IWarOfTheRingConfigProvider, WarOfTheRingConfigProvider>(Reuse.Singleton);
        container.Register<ITaomSettingsProvider, TaomSettingsProvider>(Reuse.Singleton);
        container.Register<IWarOfTheRingService, WarOfTheRingService>(Reuse.Singleton);
        container.Register<IOnPeaceAction, PeaceActionHook>(Reuse.Singleton);

        // Takes IInquiryAdapter, which EnlistmentIoC registers later in the IoC.cs sequence. Safe
        // because DryIoc resolves constructor dependencies lazily and nothing resolves this service
        // until SubModule applies Patch80, long after every registration has run. Do NOT add an
        // eager Resolve of it inside this method.
        container.Register<IKingdomVoteDeadlockService, KingdomVoteDeadlockService>(Reuse.Singleton);
    }

    public static void InitializeHooks(IOnAllianceAction allianceHook, IOnPeaceAction peaceHook)
    {
        AllianceCampaignBehavior_EndAlliance_Patch.Initialize(allianceHook);
        DeclareWarAction_ApplyInternal_Patch.Initialize(allianceHook);
        MakePeaceAction_ApplyInternal_Patch.Initialize(peaceHook);
    }
}
