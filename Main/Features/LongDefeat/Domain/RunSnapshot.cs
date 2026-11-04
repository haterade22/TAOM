namespace TAOM.Features.LongDefeat.Domain;

/// <summary>What the engine reads each tick and hands to the run.</summary>
internal readonly record struct RunSnapshot(
    float Dt,
    int AttackersStanding,
    int AttackersFleeing,
    int DefendersStanding,
    int AttackersAtKeep,
    int DefendersAtKeep,
    int SiegeBattleSize);
