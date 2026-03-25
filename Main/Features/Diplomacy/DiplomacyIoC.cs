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
    }

    public static void InitializeHooks(IOnAllianceAction hook)
    {
        AllianceCampaignBehavior_EndAlliance_Patch.Initialize(hook);
        DeclareWarAction_ApplyInternal_Patch.Initialize(hook);
    }
}
