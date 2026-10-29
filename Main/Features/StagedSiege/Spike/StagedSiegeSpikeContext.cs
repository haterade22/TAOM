using TaleWorlds.MountAndBlade;

namespace TAOM.Features.StagedSiege.Spike;

/// <summary>What a spike command needs from the live mission, or the sentence that says why it cannot run.</summary>
internal sealed class StagedSiegeSpikeContext
{
    public Mission Mission;
    public Agent Main;
    public StagedSiegeSpikeMissionBehavior Behavior;

    /// <summary>Returns the refusal sentence, or null with <paramref name="context"/> filled. Status skips the siege check.</summary>
    internal static string TryGet(bool needSiege, bool needMain, bool needBehavior, out StagedSiegeSpikeContext context)
    {
        context = null;
        var mission = Mission.Current;
        if (mission == null) return "No mission is running.";
        if (needSiege && !mission.IsSiegeBattle) return "This mission is not a siege battle.";
        var main = mission.MainAgent;
        if (needMain && (main == null || !main.IsActive())) return "There is no main agent (you are not controlling a soldier).";
        var behavior = mission.GetMissionBehavior<StagedSiegeSpikeMissionBehavior>();
        if (needBehavior && behavior == null) return "The StagedSiegeSpike mission behavior is not attached to this mission.";
        context = new StagedSiegeSpikeContext { Mission = mission, Main = main, Behavior = behavior };
        return null;
    }
}
