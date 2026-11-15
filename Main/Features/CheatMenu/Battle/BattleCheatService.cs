using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.CheatMenu.Battle;

/// <summary>
/// The battle cheats behind both the Esc-menu list and the <c>taom.cheat_battle_*</c> console commands
/// (docs/features/cheat-menu.md, "Battle"). Every action refuses in co-op (<see cref="CoopSessionPolicy.IsSinglePlayerAuthority"/>,
/// read live, before any adapter call), then outside a running battle (<see cref="BattleCheatPolicy.IsBattleOn"/>). The engine
/// calls are <see cref="IBattleCheatAdapter"/>'s, which copy vanilla's own mission cheats.
/// </summary>
public sealed class BattleCheatService
{
    /// <summary>The most troops one spawn adds, the same limit as <c>taom.spawn_troops</c>.</summary>
    public const int MaxSpawn = 200;

    /// <summary>What the menu's "+10 troops" entries add.</summary>
    public const int MenuSpawnCount = 10;

    private readonly IBattleCheatAdapter _battle;
    private readonly ICoopSessionProvider _coop;
    private readonly IDedicatedServerProvider _dedicated;
    private readonly IModLogger _logger;

    public BattleCheatService(IBattleCheatAdapter battle, ICoopSessionProvider coop, IDedicatedServerProvider dedicated, IModLogger logger)
    {
        _battle = battle;
        _coop = coop;
        _dedicated = dedicated;
        _logger = logger;
    }

    /// <summary>Read live on every call (ICoopSessionProvider's contract).</summary>
    public bool IsSinglePlayer => CoopSessionPolicy.IsSinglePlayerAuthority(_coop, _dedicated);

    /// <summary>The menu's entry for <paramref name="action"/>; the spawn entries add <see cref="MenuSpawnCount"/>.</summary>
    public CheatOutcome Run(BattleCheatAction action) => action switch
    {
        BattleCheatAction.HealSelf => HealSelf(),
        BattleCheatAction.HealSide => HealSide(),
        BattleCheatAction.Rout => RoutEnemies(),
        BattleCheatAction.Kill => KillEnemies(),
        BattleCheatAction.SpawnAllies => SpawnTroops(playerSide: true, MenuSpawnCount),
        BattleCheatAction.SpawnEnemies => SpawnTroops(playerSide: false, MenuSpawnCount),
        _ => new CheatOutcome(CheatOutcomeKind.NothingToDo),
    };

    /// <summary>The main agent, and its mount, back to full health. Nothing to do without a living main agent.</summary>
    public CheatOutcome HealSelf() => Act("heal self", () => _battle.HealMainAgent());

    /// <summary>Every active soldier of the player's team and its allies, and their mounts. Number = agents healed.</summary>
    public CheatOutcome HealSide() => Act("heal side", () => _battle.HealPlayerSide());

    /// <summary>Vanilla <c>mission.flee_enemies</c>: every active enemy soldier panics. Number = agents routed.</summary>
    public CheatOutcome RoutEnemies() => Act("rout", () => _battle.RoutEnemies());

    /// <summary>Vanilla <c>mission.kill_all_enemies</c>: every AI-controlled enemy dies. Number = agents killed.</summary>
    public CheatOutcome KillEnemies() => Act("kill", () => _battle.KillEnemies());

    /// <summary>
    /// <paramref name="count"/> copies of the non-hero soldiers already on that side (<see cref="BattleCheatPolicy.PlanSpawn"/>),
    /// placed through the same spawner as <c>taom.spawn_troops</c>. Number = troops spawned.
    /// </summary>
    public CheatOutcome SpawnTroops(bool playerSide, int count)
    {
        if (!IsSinglePlayer) return Refused();
        if (count < 1 || count > MaxSpawn) return new CheatOutcome(CheatOutcomeKind.BadAmount, Number: MaxSpawn);
        if (!BattleCheatPolicy.IsBattleOn(_battle.ReadState())) return NoBattle();

        var plan = BattleCheatPolicy.PlanSpawn(_battle.SideTroops(playerSide), count);
        if (plan.Count == 0) return Nothing();

        var spawned = 0;
        var placed = 0;
        foreach (var (id, n) in plan)
        {
            spawned += _battle.SpawnTroops(id, n, playerSide, placed);
            placed += n;
        }

        return spawned == 0 ? Nothing() : Log(playerSide ? "spawn allies" : "spawn enemies", spawned);
    }

    private CheatOutcome Act(string what, System.Func<int> act)
    {
        if (!IsSinglePlayer) return Refused();
        if (!BattleCheatPolicy.IsBattleOn(_battle.ReadState())) return NoBattle();
        var n = act();
        return n <= 0 ? Nothing() : Log(what, n);
    }

    private static CheatOutcome Refused() => new(CheatOutcomeKind.CoopRefused);

    private static CheatOutcome NoBattle() => new(CheatOutcomeKind.NoBattle);

    private static CheatOutcome Nothing() => new(CheatOutcomeKind.NothingToDo);

    private CheatOutcome Log(string what, int number)
    {
        _logger.LogInfo($"[CheatMenu] battle {what}: {number}");
        return new CheatOutcome(CheatOutcomeKind.Done, Number: number);
    }
}
