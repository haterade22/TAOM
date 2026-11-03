using System.Threading;
using TaleWorlds.MountAndBlade;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar.Hooks;

/// <summary>
/// The army upgrades on each blow, called from the Custom Battle damage model, so a level reaches the units already on the field with no stat refresh. Inert outside a match:
/// no factors are set, and every other Custom Battle passes straight through. Tower gunners gain nothing from the damage tracks, because towers have their own feats. Blows can run
/// off the main thread, so the factors are immutable values read once per blow.
/// </summary>
internal static class FordsUpgradeHooks
{
    private static volatile CombatUpgradeFactors? _west;
    private static volatile CombatUpgradeFactors? _east;
    private static long _hitsRaised;
    private static long _hitsLowered;

    /// <summary>Blows that the upgrades raised, and blows that they lowered, since the match started. The log line at the end of a match proves that the seam ran.</summary>
    internal static (long Raised, long Lowered) Counts => (Interlocked.Read(ref _hitsRaised), Interlocked.Read(ref _hitsLowered));

    internal static void Set(CombatUpgradeFactors west, CombatUpgradeFactors east)
    {
        _west = west;
        _east = east;
    }

    internal static void Clear()
    {
        _west = null;
        _east = null;
        Interlocked.Exchange(ref _hitsRaised, 0);
        Interlocked.Exchange(ref _hitsLowered, 0);
    }

    public static float Amplify(in AttackInformation attack, in AttackCollisionData collision, float damage)
    {
        if (_west == null)
            return damage;

        var attacker = attack.IsAttackerAgentMount ? attack.AttackerAgent?.RiderAgent : attack.AttackerAgent;
        if (attacker == null || attacker.CurrentMortalityState == Agent.MortalityState.Invulnerable)
            return damage;

        var factor = FactorsOf(attacker)?.Attack(collision.IsMissile) ?? 1f;
        if (factor == 1f)
            return damage;

        Interlocked.Increment(ref _hitsRaised);
        return damage * factor;
    }

    public static float Reduce(in AttackInformation attack, in AttackCollisionData collision, float damage)
    {
        if (_west == null)
            return damage;

        var victim = attack.VictimAgent;
        var body = victim != null && victim.IsMount ? victim.RiderAgent : victim;
        var factor = (body == null ? null : FactorsOf(body))?.Defence(collision.IsMissile) ?? 1f;
        if (factor == 1f)
            return damage;

        Interlocked.Increment(ref _hitsLowered);
        return damage * factor;
    }

    private static CombatUpgradeFactors? FactorsOf(Agent agent)
    {
        var side = agent.Team?.Side;
        return side == FordsMissionLauncher.WestSide ? _west : side == FordsMissionLauncher.EastSide ? _east : null;
    }
}
