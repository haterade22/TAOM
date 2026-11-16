using TAOM.Features.HeroAbilities.Domain;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.HeroAbilities.Hooks;

/// <summary>
/// What the stat and damage models ask of a ranged hero kit (#1361). The roster lists only the heroes the gated host
/// owns, keyed by agent reference, so a Custom Battle that merely contains the same character arms nothing. Model
/// methods run on any thread: these read the roster's published map only. HeroAbilitiesModule.InitializeStatics sets
/// <see cref="Roster"/>; until then every answer is "no kit".
/// </summary>
public static class HeroRangedHooks
{
    internal static HeroRangedRoster<Agent>? Roster { get; set; }

    // Both stat models call this last in UpdateAgentStats, for every agent. The lookup comes before agent.IsMount, which
    // reads the native flags (Agent.cs:644), so an agent the roster does not list costs one dictionary read.
    public static void ApplyStats(Agent agent, AgentDrivenProperties properties)
    {
        var roster = Roster;
        if (agent == null || roster == null || roster.KitOf(agent) == null)
            return;
        ApplyFor(roster, agent, agent.IsMount, properties);
    }

    internal static void ApplyFor<TKey>(HeroRangedRoster<TKey>? roster, TKey? key, bool isMount, AgentDrivenProperties properties)
        where TKey : class
    {
        if (isMount || roster == null)
            return;
        var kit = roster.KitOf(key);
        if (kit == null || !kit.ZeroAimError)
            return;
        HeroPrecisionApplier.Apply(properties);
    }

    internal static bool PiercesShields(Agent? shooter) => Roster?.KitOf(shooter)?.PierceShields == true;
}
