using System;
using System.Collections.Generic;
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
    private readonly List<GodModeAgent> _godModeHeld = new();
    private int _godModeMission;

    public BattleCheatService(IBattleCheatAdapter battle, ICoopSessionProvider coop, IDedicatedServerProvider dedicated, IModLogger logger)
    {
        _battle = battle;
        _coop = coop;
        _dedicated = dedicated;
        _logger = logger;
    }

    /// <summary>Read live on every call (ICoopSessionProvider's contract).</summary>
    public bool IsSinglePlayer => CoopSessionPolicy.IsSinglePlayerAuthority(_coop, _dedicated);

    /// <summary>
    /// God mode is on: the main agent and the mount it rides take no damage. It does not carry over to another mission
    /// (<see cref="BattleCheatState.MissionKey"/>), and <see cref="TickGodMode"/> turns it off once the battle ends or a side retreats.
    /// </summary>
    public bool GodModeOn { get; private set; }

    /// <summary>The menu's entry for <paramref name="action"/>; the spawn entries add <see cref="MenuSpawnCount"/>.</summary>
    public CheatOutcome Run(BattleCheatAction action) => action switch
    {
        BattleCheatAction.HealSelf => HealSelf(),
        BattleCheatAction.HealSide => HealSide(),
        BattleCheatAction.GodMode => ToggleGodMode(),
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

    /// <summary>The menu's god mode entry: turns it on when off, off when on.</summary>
    public CheatOutcome ToggleGodMode() => ChangeGodMode(null);

    /// <summary>
    /// Turns god mode on or off, behind the same gate as every battle cheat, and applies it at once. Number = 1 when it is now on,
    /// 0 when off. While on, <see cref="TickGodMode"/> keeps it on the main agent and whatever mount it rides.
    /// </summary>
    public CheatOutcome SetGodMode(bool on) => ChangeGodMode(on);

    /// <summary>
    /// Once per mission frame. Does nothing while god mode is off and holds no agent. While on it protects the main agent and
    /// the mount it rides now (a new horse after a remount too) and releases a horse the player left; once the battle ends or a
    /// side retreats it turns itself off and releases everything it holds.
    /// </summary>
    public void TickGodMode()
    {
        if (!GodModeOn && _godModeHeld.Count == 0) return;
        var state = _battle.ReadState();
        if (ForgetOtherMission(state)) return;
        if (GodModeOn && (!IsSinglePlayer || !BattleCheatPolicy.IsBattleOn(state)))
        {
            GodModeOn = false;
            _logger.LogInfo("[CheatMenu] battle god mode: off, the battle is over");
        }
        ApplyGodMode();
    }

    // null = switch it. The co-op check comes before any adapter call, as for every battle cheat.
    private CheatOutcome ChangeGodMode(bool? on)
    {
        if (!IsSinglePlayer) return Refused();
        var state = _battle.ReadState();
        ForgetOtherMission(state);
        if (!BattleCheatPolicy.IsBattleOn(state)) return NoBattle();
        GodModeOn = on ?? !GodModeOn;
        ApplyGodMode();
        _logger.LogInfo($"[CheatMenu] battle god mode: {(GodModeOn ? "on" : "off")}");
        return new CheatOutcome(CheatOutcomeKind.Done, Number: GodModeOn ? 1 : 0);
    }

    // State left from an earlier mission is dropped with no engine call: its agents are gone, and an index may name another
    // agent in this mission. Returns true when it dropped something.
    private bool ForgetOtherMission(BattleCheatState state)
    {
        if (state.MissionKey == _godModeMission) return false;
        _godModeMission = state.MissionKey;
        var had = GodModeOn || _godModeHeld.Count > 0;
        GodModeOn = false;
        _godModeHeld.Clear();
        return had;
    }

    private void ApplyGodMode()
    {
        var held = _godModeHeld.Count == 0 ? Array.Empty<GodModeAgent>() : _battle.ReadAgents(_godModeHeld.ToArray());
        var targets = GodModeOn ? _battle.GodModeTargets() : Array.Empty<GodModeAgent>();
        var plan = BattleCheatPolicy.PlanGodMode(GodModeOn, targets, held);
        foreach (var agent in plan.Release) _battle.SetImmortal(agent.Index, false);
        foreach (var agent in plan.Protect) _battle.SetImmortal(agent.Index, true);
        _godModeHeld.Clear();
        _godModeHeld.AddRange(plan.Held);
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
