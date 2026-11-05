using System;
using System.Threading;
using TaleWorlds.MountAndBlade;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar.Hooks;

/// <summary>
/// The army upgrades and the hero factors on each blow, called from the Custom Battle damage model, so a level reaches the units already on the field with no stat refresh. Inert outside a match:
/// no factors are set, and every other Custom Battle passes straight through. Tower gunners gain nothing from the damage tracks, because towers have their own feats. Blows can run
/// off the main thread, so the factors are immutable values read once per blow.
/// </summary>
internal static class FordsUpgradeHooks
{
    private static volatile CombatUpgradeFactors? _west;
    private static volatile CombatUpgradeFactors? _east;
    private static volatile HeroCombatFactors? _heroes;
    private static volatile Func<object, object, bool>? _isOwnTowerShot;
    private static long _hitsRaised;
    private static long _hitsLowered;
    private static long _heroHitsRaised;
    private static long _heroHitsLowered;
    private static long _ownTowerShots;

    /// <summary>Blows that the upgrades raised, and blows that they lowered, since the match started. The log line at the end of a match proves that the seam ran.</summary>
    internal static (long Raised, long Lowered) Counts => (Interlocked.Read(ref _hitsRaised), Interlocked.Read(ref _hitsLowered));

    /// <summary>Blows from a tower's missile owner on its own side that were zeroed, since the match started.</summary>
    internal static long OwnTowerShots => Interlocked.Read(ref _ownTowerShots);

    /// <summary>Blows that the hero factors raised, and blows that they lowered, since the match started.</summary>
    internal static (long Raised, long Lowered) HeroCounts => (Interlocked.Read(ref _heroHitsRaised), Interlocked.Read(ref _heroHitsLowered));

    /// <summary>Whether a troop is one of this match's heroes.</summary>
    internal static bool IsHero(string? troopId) => _heroes?.IsHero(troopId) == true;

    internal static void Set(CombatUpgradeFactors west, CombatUpgradeFactors east, HeroCombatFactors heroes)
    {
        _west = west;
        _east = east;
        _heroes = heroes;
    }

    internal static void SetTowerShots(Func<object, object, bool> isOwnShot) => _isOwnTowerShot = isOwnShot;

    /// <summary>
    /// A tower's missile owner is a friend of both sides, so nothing cancels its hit on its own side: an arrow would hurt a defender, and a rock's
    /// ring hits friends in any case. The owner is the only invulnerable attacker here, which keeps this cheap on the hit path.
    /// </summary>
    private static bool IsOwnTowerShot(in AttackInformation attack)
    {
        var check = _isOwnTowerShot;
        var attacker = attack.AttackerAgent;
        if (check == null || attacker == null || attacker.CurrentMortalityState != Agent.MortalityState.Invulnerable)
            return false;

        var body = attack.IsVictimAgentMount ? attack.VictimAgent?.RiderAgent : attack.VictimAgent;
        return body != null && check(attacker, body);
    }

    internal static void Clear()
    {
        Interlocked.Exchange(ref _ownTowerShots, 0);
        _isOwnTowerShot = null;
        _west = null;
        _east = null;
        _heroes = null;
        Interlocked.Exchange(ref _hitsRaised, 0);
        Interlocked.Exchange(ref _hitsLowered, 0);
        Interlocked.Exchange(ref _heroHitsRaised, 0);
        Interlocked.Exchange(ref _heroHitsLowered, 0);
    }

    public static float Amplify(in AttackInformation attack, in AttackCollisionData collision, float damage)
    {
        if (_west == null)
            return damage;

        var attacker = attack.IsAttackerAgentMount ? attack.AttackerAgent?.RiderAgent : attack.AttackerAgent;
        if (attacker == null || attacker.CurrentMortalityState == Agent.MortalityState.Invulnerable)
            return damage;

        var hero = _heroes?.Damage(attacker.Character?.StringId) ?? 1f;
        if (hero != 1f)
            Interlocked.Increment(ref _heroHitsRaised);

        var factor = FactorsOf(attacker)?.Attack(collision.IsMissile) ?? 1f;
        if (factor == 1f)
            return damage * hero;

        Interlocked.Increment(ref _hitsRaised);
        return damage * factor * hero;
    }

    public static float Reduce(in AttackInformation attack, in AttackCollisionData collision, float damage)
    {
        if (IsOwnTowerShot(in attack))
        {
            Interlocked.Increment(ref _ownTowerShots);
            return 0f;
        }

        if (_west == null)
            return damage;

        var victim = attack.VictimAgent;
        var body = victim != null && victim.IsMount ? victim.RiderAgent : victim;
        var hero = _heroes?.Taken(body?.Character?.StringId) ?? 1f;
        if (hero != 1f)
            Interlocked.Increment(ref _heroHitsLowered);

        var factor = (body == null ? null : FactorsOf(body))?.Defence(collision.IsMissile) ?? 1f;
        if (factor == 1f)
            return damage * hero;

        Interlocked.Increment(ref _hitsLowered);
        return damage * factor * hero;
    }

    private static CombatUpgradeFactors? FactorsOf(Agent agent)
    {
        var side = agent.Team?.Side;
        return side == FordsMissionLauncher.WestSide ? _west : side == FordsMissionLauncher.EastSide ? _east : null;
    }
}
