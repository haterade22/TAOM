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
    }

    public static void InitializeHooks(IOnAllianceAction allianceHook, IOnPeaceAction peaceHook)
    {
        AllianceCampaignBehavior_EndAlliance_Patch.Initialize(allianceHook);
        DeclareWarAction_ApplyInternal_Patch.Initialize(allianceHook);
        MakePeaceAction_ApplyInternal_Patch.Initialize(peaceHook);
    }
}
