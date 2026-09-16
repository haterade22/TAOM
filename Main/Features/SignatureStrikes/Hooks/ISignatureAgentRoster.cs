using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.SignatureStrikes.Hooks;

/// <summary>
/// The agents on the field with signature strikes, plus each one's cooldown stamps. Probed on
/// every melee hit in the battle (by the model AND the mission logic), so the lookup is one
/// reference-keyed dictionary probe.
///
/// Boundary type: it holds <c>Agent</c> references for the life of one mission and is a process
/// singleton (the model needs it), so <see cref="Clear"/> from mission start AND mission end is
/// its session-reset story. Never keyed by <c>Agent.Index</c>: a deleted agent's index goes to
/// the next agent built (#592), and <c>Agent</c> has no <c>Equals</c> override, so the reference
/// is the identity.
/// </summary>
public interface ISignatureAgentRoster
{
    int Count { get; }

    /// <summary>Registers the agent if it is a signature hero (hero id or race). Idempotent;
    /// true only when this call added it, so a re-scan logs nothing twice.</summary>
    bool TryRegister(Agent? agent);

    /// <summary>One pass over agents already on the field; returns how many this call added.</summary>
    int RegisterAll(IEnumerable<Agent>? agents);

    bool TryGet(Agent? agent, out SignatureAgentEntry entry);

    void Remove(Agent? agent);

    void Clear();
}

/// <summary>Per-attacker cooldown stamps in mission time. NaN means never.</summary>
public sealed class SignatureAgentEntry
{
    public float LastSlamTime { get; set; } = float.NaN;

    public float LastSweepTime { get; set; } = float.NaN;
}
