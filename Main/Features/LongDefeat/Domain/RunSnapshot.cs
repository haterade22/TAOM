using System.Collections.Generic;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>What the engine reads each tick and hands to the run. <c>Gates</c> are the visible gates; null (nothing reads them yet) is no gate, and the gate rows stay empty.</summary>
internal readonly record struct RunSnapshot(
    float Dt,
    int AttackersStanding,
    int AttackersFleeing,
    int DefendersStanding,
    int AttackersAtKeep,
    int DefendersAtKeep,
    int SiegeBattleSize,
    IReadOnlyList<LdGateFacts>? Gates = null);
