using TAOM.Features.Execution;

namespace TAOM.Features.StoryMissions.Domain;

/// <summary>The player's kingdom in a story window: neither side's, the defender, or one of the attackers.</summary>
internal enum StoryTie
{
    None,
    Defender,
    Attacker,
}

/// <summary>The sides of a story window the player may take. "Let fate decide" needs no flag: it is there whenever a side is.</summary>
internal readonly struct StoryChoices
{
    public StoryChoices(bool defend, bool attack)
    {
        Defend = defend;
        Attack = attack;
    }

    public bool Defend { get; }

    public bool Attack { get; }

    public bool Any => Defend || Attack;

    public override string ToString() => Defend && Attack ? "Defend+Attack" : Defend ? "Defend" : Attack ? "Attack" : "None";
}

/// <summary>
/// Which sides of a story window the player may take (#1326). He never fights for a side of the opposite alignment: a free player
/// defends a free fortress, an evil player attacks it, a neutral player may do either, and a neutral side is open to everyone. His own
/// kingdom comes first: when it defends he may only defend, and when it attacks he may only attack, so he never fights against his
/// own realm.
/// </summary>
internal static class StorySideGate
{
    public static StoryChoices Choose(FactionSide player, FactionSide defenders, FactionSide attackers, StoryTie tie, bool hasDefend, bool hasAttack)
    {
        var defend = hasDefend && tie != StoryTie.Attacker && !Opposed(player, defenders);
        var attack = hasAttack && tie != StoryTie.Defender && !Opposed(player, attackers);
        return new StoryChoices(defend, attack);
    }

    // IAlignmentService.AreEnemyAlignments counts Neutral as everybody's enemy; here a neutral party may stand with either side.
    private static bool Opposed(FactionSide a, FactionSide b) => a != FactionSide.Neutral && b != FactionSide.Neutral && a != b;
}
