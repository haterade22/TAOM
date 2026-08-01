using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.DevConsole.Cheats;

/// <summary>
/// `taom.damage_agent <amount> [agentName]` — apply raw HP damage to an agent.
///
/// **Scope, stated because the name invites the wrong assumption:** this builds a synthetic blow and
/// calls <c>Agent.RegisterBlow</c>, which goes straight to <c>HandleBlow</c>. The melee/missile hit
/// callbacks — where <c>DecideAgentShrugOffBlow</c>, and therefore TAOM's unstoppable/knockdown
/// models, actually run — sit UPSTREAM of that. So this command cannot test the unstoppable damage
/// threshold or any shrug-off behaviour; use a real weapon hit for those, with
/// <c>taom.spawn_troops</c> to compose the fight. What it IS good for: HP attrition, death
/// thresholds, and the dismount-on-death paths.
///
/// Modelled on the engine's own <c>Mission.KillAgentCheat</c>, including its FULL guard: the three
/// mission modes where damage is a no-op, plus <c>GameNetwork.IsClientOrReplay</c> — which is not
/// multiplayer-only, since <c>IsReplay</c> covers the singleplayer replay viewer. Reporting a
/// successful hit in any of those states would be a lie.
///
/// The engine's <c>InputManager</c> swing-direction branches are deliberately omitted: there is no
/// meaningful "which way was I holding" for a console command, so the blow uses the target's look
/// direction.
/// </summary>
public static class AgentDamageCheats
{
    // Comfortably above any creature's health (the engine's own kill cheat uses 2000) and far below
    // the range where the float→int cast degenerates.
    private const float MaxDamage = 100_000f;

    private const string Usage =
        "Format is \"taom.damage_agent [amount] [agentName]\".\n"
        + "Omit the name to damage your own agent. Amount must be a finite number above 0.\n"
        + "NOTE: this bypasses the melee/missile hit path, so shrug-off, unstoppable and knockdown\n"
        + "models do NOT run. Use a real weapon hit to test those.";

    [CommandLineFunctionality.CommandLineArgumentFunction("damage_agent", "taom")]
    public static string DamageAgent(List<string> strings) =>
        TaomConsole.RunInMission(strings, Usage, args =>
        {
            if (args.Count == 0) return "Expected an amount.\n" + Usage;
            if (!DevConsoleArgs.TryParseAmount(args[0], out var amount, out var parseError))
                return parseError + "\n" + Usage;

            // Upper bound, not just a sign check. TryParseAmount rejects NaN/Infinity but not a large
            // finite float, and `(int)amount` on an out-of-int-range float is unchecked — it yields a
            // degenerate value, so BaseMagnitude would stay huge while InflictedDamage went negative
            // and read downstream as healing. That float→int cast class has shipped five times in
            // TAOM (csharp-architecture.md, "Engine-Float Decision Gates"); this is the sixth site.
            if (!(amount > 0f) || amount > MaxDamage)
                return $"Amount must be above 0 and at most {MaxDamage:N0}.\n" + Usage;

            var mission = Mission.Current;

            // The full guard the engine's KillAgentCheat uses, IsClientOrReplay included. That clause
            // is not multiplayer-only: IsReplay covers MBCommon.GameType.SingleReplay, which is
            // reachable in singleplayer via the replay viewer. Reporting a successful hit in any of
            // these states would be a lie.
            if (GameNetwork.IsClientOrReplay)
                return "This mission is a network client or a replay — the engine ignores damage here.";
            if (mission.Mode == MissionMode.Conversation || mission.Mode == MissionMode.Barter
                || mission.Mode == MissionMode.CutScene)
                return $"The mission is in {mission.Mode} mode — the engine ignores damage here.";

            var wanted = args.Count > 1 ? args[1] : null;
            var target = string.IsNullOrWhiteSpace(wanted)
                ? mission.MainAgent
                : mission.Agents?.FirstOrDefault(a => Matches(a, wanted));

            if (target == null)
                return string.IsNullOrWhiteSpace(wanted)
                    ? "No main agent to damage. Pass an agent name."
                    : $"No agent matching '{wanted}'. Use taom.print_agent_info * to list agents.";

            var before = SafeHealth(target);
            ApplyBlow(mission, target, amount);
            var after = SafeHealth(target);

            return MissionReportFormatter.FormatDamage(SafeName(target), amount, before, after);
        });

    // Mirrors Mission.KillAgentCheat's blow construction, with the magnitude taken from the argument
    // instead of its hardcoded 2000, and without the InputManager swing-direction branches (there is
    // no meaningful "which way was I holding" for a console command).
    private static void ApplyBlow(Mission mission, Agent target, float amount)
    {
        var source = mission.MainAgent ?? target;

        var blow = new Blow(source.Index)
        {
            DamageType = DamageTypes.Blunt,
            BoneIndex = target.Monster.HeadLookDirectionBoneIndex,
            GlobalPosition = target.Position,
            BaseMagnitude = amount,
            InflictedDamage = (int)amount,
            SwingDirection = target.LookDirection,
            DamageCalculated = true,
        };
        blow.GlobalPosition.z += target.GetEyeGlobalHeight();
        blow.WeaponRecord.FillAsMeleeBlow(null, null, -1, -1);
        blow.Direction = blow.SwingDirection;

        var collisionData = AttackCollisionData.GetAttackCollisionDataForDebugPurpose(
            _attackBlockedWithShield: false, _correctSideShieldBlock: false, _isAlternativeAttack: false,
            _isColliderAgent: true, _collidedWithShieldOnBack: false, _isMissile: false,
            _isMissileBlockedWithWeapon: false, _missileHasPhysics: false, _entityExists: false,
            _thrustTipHit: false, _missileGoneUnderWater: false, _missileGoneOutOfBorder: false,
            CombatCollisionResult.StrikeAgent, -1, 0, 2, blow.BoneIndex, BoneBodyPartType.Head,
            source.Monster.MainHandItemBoneIndex, Agent.UsageDirection.AttackLeft, -1,
            CombatHitResultFlags.NormalHit, 0.5f, 1f, 0f, 0f, 0f, 0f, 0f, 0f,
            Vec3.Up, blow.Direction, blow.GlobalPosition, Vec3.Zero, Vec3.Zero, target.Velocity, Vec3.Up);

        target.RegisterBlow(blow, in collisionData);
    }

    private static bool Matches(Agent agent, string wanted)
    {
        try
        {
            return agent?.Name != null && agent.Name.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch { return false; }
    }

    // Nullable for the same reason AgentSnapshot.Health is: 0 is a plausible health value, so a
    // defaulted read would make a live agent look dead in the before/after echo.
    private static float? SafeHealth(Agent agent)
    {
        try { return agent.Health; } catch { return null; }
    }

    private static string SafeName(Agent agent)
    {
        try { return agent.Name ?? "(unnamed)"; } catch { return "(unnamed)"; }
    }
}
