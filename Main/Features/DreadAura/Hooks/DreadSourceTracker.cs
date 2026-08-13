using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.DreadAura.Hooks;

/// <summary>
/// Tracks which agents on the field project dread, so <see cref="DreadAuraMissionLogic"/> is left
/// with only the gate, the schedule and the pulse (ADR-002).
///
/// Registration deliberately does NOT consult the master toggle. Identity is not a decision the
/// toggle should change: gating it would leave a wraith that spawned during an off-window
/// permanently unregistered once the player switched the feature back on, because the one-shot
/// mission scan never re-runs. The toggle gates the DRAIN instead.
///
/// Boundary class: it holds <c>Agent</c> references for the life of one mission and hands the pure
/// layer nothing but primitives. Those references must not outlive the mission, which is what
/// <see cref="Clear"/> is for; <c>Agent.Index</c> is reused within a mission, so nothing here may
/// ever key on it.
/// </summary>
public sealed class DreadSourceTracker
{
    private readonly IDreadRegistry _registry;
    private readonly List<DreadSource> _sources = new List<DreadSource>();

    public DreadSourceTracker(IDreadRegistry registry)
    {
        _registry = registry;
    }

    public IReadOnlyList<DreadSource> Sources => _sources;

    public int Count => _sources.Count;

    /// <summary>One-shot pass over the agents already spawned. Agents built later arrive through
    /// <see cref="TryRegister"/>.</summary>
    public void ScanMission(Mission mission)
    {
        var agents = mission?.AllAgents;
        if (agents == null)
            return;

        foreach (var agent in agents)
            TryRegister(agent, mission.CurrentTime);
    }

    public void TryRegister(Agent agent, float now)
    {
        if (agent == null || !agent.IsHuman || agent.IsMount)
            return;

        var character = agent.Character;
        if (character == null)
            return;

        // Hero StringId is the axis that actually finds the Nine: eight of them carry no race in
        // TAOM's data. `as CharacterObject` rather than a cast, because a mission agent can carry
        // a plain BasicCharacterObject outside a campaign.
        var heroStringId = (character as CharacterObject)?.HeroObject?.StringId;

        if (!_registry.IsDreadSource(heroStringId, character.Race))
            return;

        foreach (var existing in _sources)
        {
            if (ReferenceEquals(existing.Agent, agent))
                return;
        }

        _sources.Add(new DreadSource(agent, now));
    }

    public void Prune()
    {
        for (var i = _sources.Count - 1; i >= 0; i--)
        {
            var agent = _sources[i].Agent;
            if (agent == null || !agent.IsActive())
                _sources.RemoveAt(i);
        }
    }

    public void Clear() => _sources.Clear();

    public sealed class DreadSource
    {
        public DreadSource(Agent agent, float registeredAt)
        {
            Agent = agent;
            LastPulseTime = registeredAt;
        }

        public Agent Agent { get; }

        /// <summary>Mission time of this source's last pulse. Each source integrates against its
        /// OWN elapsed time, which is what keeps the drain rate-exact while sources are
        /// round-robined across frames.</summary>
        public float LastPulseTime { get; set; }
    }
}
