using System.Collections.Generic;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>What the engine reads each tick and hands to the run. <c>Gates</c> are the visible gates; null (nothing reads them yet) is no gate, and the gate rows stay empty. <c>Lord</c> is the lord as the field reads him (present, in his own body, where), which the near rule of the armoury and the yard reads; null is no lord, so nothing is near. <c>DefenderHealthMean</c> is the defenders' mean health over their limit as the field read it, -1 when nobody was read: the bot reads it to decide a field care buy (#1143).</summary>
internal readonly record struct RunSnapshot(
    float Dt,
    int AttackersStanding,
    int AttackersFleeing,
    int DefendersStanding,
    int AttackersAtKeep,
    int DefendersAtKeep,
    int SiegeBattleSize,
    IReadOnlyList<LdGateFacts>? Gates = null,
    LdLordFacts? Lord = null,
    float DefenderHealthMean = -1f);
