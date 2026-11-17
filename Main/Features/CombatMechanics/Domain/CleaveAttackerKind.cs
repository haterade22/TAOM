namespace TAOM.Features.CombatMechanics.Domain;

// Who swings, as far as Cleaving Blows cares. Player: the agent a human controls (the main agent, a soldier taken over, a
// co-op human on his own peer). Ai: a soldier the AI controls. None: anything else (a co-op puppet), which gets vanilla.
public enum CleaveAttackerKind
{
    None,
    Player,
    Ai,
}
