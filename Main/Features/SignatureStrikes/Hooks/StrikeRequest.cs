using TAOM.Features.SignatureStrikes.Domain;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.SignatureStrikes.Hooks;

/// <summary>
/// One ring waiting for the next mission tick. Holds two <c>Agent</c> handles across exactly one
/// frame; the runner re-gates both with <c>AgentSlotIdentity.IsCurrentOccupant</c> before either
/// touches native state (#592). The impact is a value, never re-read off the primary victim.
/// </summary>
public readonly struct StrikeRequest
{
    public StrikeRequest(Agent attacker, Agent? primaryVictim, Vec3 impact, StrikeEffect effect, string attackerName)
    {
        Attacker = attacker;
        PrimaryVictim = primaryVictim;
        Impact = impact;
        Effect = effect;
        AttackerName = attackerName;
    }

    public Agent Attacker { get; }

    /// <summary>Null on a ground hit.</summary>
    public Agent? PrimaryVictim { get; }

    public Vec3 Impact { get; }

    public StrikeEffect Effect { get; }

    /// <summary>For the log line only; read at enqueue so a stale handle is never dereferenced
    /// for a name.</summary>
    public string AttackerName { get; }
}
