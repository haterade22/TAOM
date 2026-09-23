using TAOM.Features.SignatureStrikes.Domain;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.SignatureStrikes.Hooks;

/// <summary>
/// One ring waiting for the next mission tick. Holds two <c>Agent</c> handles across exactly one
/// frame; the runner re-gates both with <c>AgentSlotIdentity.IsCurrentOccupant</c> before either
/// touches native state (#592). The centre is a value read at the hit, never re-read off an agent.
/// </summary>
public readonly struct StrikeRequest
{
    public StrikeRequest(Agent attacker, Agent? primaryVictim, Vec3 center, StrikeEffect effect, string attackerName)
    {
        Attacker = attacker;
        PrimaryVictim = primaryVictim;
        Center = center;
        Effect = effect;
        AttackerName = attackerName;
    }

    public Agent Attacker { get; }

    /// <summary>Null on a ground hit.</summary>
    public Agent? PrimaryVictim { get; }

    /// <summary>Where the ring is centred: the hit point, or the attacker's position at the hit
    /// for a <see cref="StrikeOrigin.Self"/> strike.</summary>
    public Vec3 Center { get; }

    public StrikeEffect Effect { get; }

    /// <summary>For the log line only; read at enqueue so a stale handle is never dereferenced
    /// for a name.</summary>
    public string AttackerName { get; }
}
