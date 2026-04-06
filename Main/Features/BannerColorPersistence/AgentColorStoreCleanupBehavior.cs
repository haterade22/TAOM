using TaleWorlds.MountAndBlade;

namespace TAOM.Features.BannerColorPersistence;

public class AgentColorStoreCleanupBehavior : MissionBehavior
{
    private readonly IAgentColorStore _store;

    public AgentColorStoreCleanupBehavior(IAgentColorStore store) => _store = store;

    public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

    protected override void OnEndMission() => _store.Clear();
}
