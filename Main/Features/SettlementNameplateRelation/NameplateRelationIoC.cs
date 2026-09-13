using DryIoc;

namespace TAOM.Features.SettlementNameplateRelation;

public static class NameplateRelationIoC
{
    public static void RegisterSettlementNameplateRelationFeature(IContainer container)
    {
        container.Register<INameplateRelationAlphaService, NameplateRelationAlphaService>(Reuse.Singleton);
    }
}
