using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Logging;
using TAOM.Features.DevConsole.Domain;

namespace TAOM.Features.DevConsole.Cheats;

/// <summary>
/// `taom.spawn_troops <troopId> <count> [enemy|ally]` — put troops into the CURRENT mission.
///
/// The single largest gap in vanilla's console: it ships ~80 campaign cheats and 10 mission cheats
/// and not one of them spawns anything. Composing a specific fight today means Custom Battle (which
/// gives you a culture's default roster, not "20 dwarves and one mûmakil") or engineering the
/// campaign into the right encounter.
///
/// Uses the mission gate rather than the campaign gate, so it works in a custom battle — TAOM's main
/// venue for testing creatures, mounts and equipment.
/// </summary>
public static class MissionSpawnCheats
{
    private const int MaxCount = 200;

    private const string Usage =
        "Format is \"taom.spawn_troops [troopId] [count] [enemy|ally]\".\n"
        + "Spawns count troops into the current mission near you. Side defaults to enemy.\n"
        + "Count is clamped to [1,200]. Example: taom.spawn_troops taom_troll 5 enemy";

    [CommandLineFunctionality.CommandLineArgumentFunction("spawn_troops", "taom")]
    public static string SpawnTroops(List<string> strings) =>
        TaomConsole.RunInMission(strings, Usage, args =>
        {
            if (args.Count < 2) return "Expected a troop id and a count.\n" + Usage;
            if (!DevConsoleArgs.TryParseCount(args[1], 1, MaxCount, out var count, out var countError))
                return countError + "\n" + Usage;
            if (!DevConsoleArgs.TryParseSide(args.Count > 2 ? args[2] : null, out var isPlayerSide, out var sideError))
                return sideError + "\n" + Usage;

            return MissionReportFormatter.FormatSpawn(Spawn(args[0], count, isPlayerSide));
        });

    private static SpawnOutcome Spawn(string troopId, int count, bool isPlayerSide)
    {
        var outcome = new SpawnOutcome { TroopId = troopId, Requested = count };

        // GetObject<CharacterObject>, not a BasicCharacterObject accessor: SimpleAgentOrigin's ctor
        // does a hard (CharacterObject) cast, so a non-campaign character would throw inside the
        // engine rather than give us a message we can print.
        var character = MBObjectManager.Instance?.GetObject<CharacterObject>(troopId);
        if (character == null)
        {
            outcome.FailureReason = $"Unknown troop '{troopId}'. Check ModuleData/troops for the id.";
            return outcome;
        }

        var origin = new SimpleAgentOrigin(character);

        // Pre-resolve the team and bail if it is null. SpawnTroop does
        // `.ClothingColor1(agentTeam.Color)` with NO null check, so a null team hard-crashes the game
        // instead of printing a sentence. This is the single biggest crash risk in the suite.
        //
        // Only the ENEMY path can actually return null: GetAgentTeam returns Current.PlayerEnemyTeam
        // unguarded, and that is null in town/village missions. The ally path never returns null — it
        // falls back to Current.PlayerTeam when PlayerAllyTeam is missing — so `ally` in a town spawns
        // onto the player's own team rather than refusing. That is the engine's behaviour, not a bug
        // to work around, but it is why the message below names the enemy case specifically.
        var team = Mission.GetAgentTeam(origin, isPlayerSide);
        if (team == null)
        {
            outcome.FailureReason =
                "This mission has no enemy team — town and village missions only have the player's. "
                + "Run this from a battle, a siege, or a custom battle.";
            return outcome;
        }

        outcome.TeamLabel = team.Side.ToString();

        var anchor = ResolveAnchor();
        var logger = ResolveLogger();

        for (var i = 0; i < count; i++)
        {
            try
            {
                // Ring-offset per index so N troops do not stack inside each other. Direction is
                // passed explicitly: SpawnTroop dereferences initialDirection.Value unconditionally
                // once initialPosition has a value.
                var angle = i * 0.7f;
                var radius = 2f + i * 0.35f;
                var position = new Vec3(
                    anchor.x + (float)Math.Cos(angle) * radius,
                    anchor.y + (float)Math.Sin(angle) * radius,
                    anchor.z);

                Mission.Current.SpawnTroop(
                    origin, isPlayerSide,
                    hasFormation: true, spawnWithHorse: true, isReinforcement: false,
                    formationTroopCount: count, formationTroopIndex: i,
                    isAlarmed: true, wieldInitialWeapons: true,
                    initialPosition: position, initialDirection: new Vec2(0f, 1f));

                outcome.Spawned++;
            }
            catch (Exception ex)
            {
                // Keep going: one bad equipment roll should not abandon the rest of the wave.
                try { logger?.LogError($"[DevConsole] spawn_troops '{troopId}' #{i} failed: {ex}"); } catch { }
            }
        }

        return outcome;
    }

    private static Vec3 ResolveAnchor()
    {
        var main = Mission.Current?.MainAgent;
        if (main != null) return main.Position;

        try { return Mission.Current.GetCameraFrame().origin; }
        catch { return Vec3.Zero; }
    }

    private static IModLogger ResolveLogger()
    {
        try { return IoC.Resolve<IModLogger>(); } catch { return null; }
    }
}
