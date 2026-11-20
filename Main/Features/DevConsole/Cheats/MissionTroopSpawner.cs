using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Logging;
using TAOM.Features.CreatureBandits;
using TAOM.Features.DevConsole.Domain;

namespace TAOM.Features.DevConsole.Cheats;

/// <summary>
/// Puts troops into the CURRENT mission near the player: the one spawner behind <c>taom.spawn_troops</c>
/// (<see cref="MissionSpawnCheats"/>) and the battle cheat menu's "+10 troops" (<c>BattleCheatAdapter</c>,
/// docs/features/cheat-menu.md). Works in a custom battle, TAOM's main venue for testing creatures, mounts and equipment.
/// </summary>
internal static class MissionTroopSpawner
{
    internal const int MaxCount = 200;

    /// <param name="ringStart">The first ring slot to use: a caller that spawns several troop types in a row passes the
    /// number already placed, so the next type does not stand inside the last one. 0 for a single call.</param>
    internal static SpawnOutcome Spawn(string troopId, int count, bool isPlayerSide, int ringStart = 0)
    {
        var outcome = new SpawnOutcome { TroopId = troopId, Requested = count };

        // BasicCharacterObject, not CharacterObject: Custom Battle registers NPCCharacter as
        // BasicCharacterObject (v1.5.3 CustomGame.cs:135), so a CharacterObject lookup found no troop at
        // all there. The base type is not sealed, so GetObject<T> matches by assignability and a campaign
        // still hands back its CharacterObject (MBObjectManager.cs:586-594).
        var character = MBObjectManager.Instance?.GetObject<BasicCharacterObject>(troopId);
        if (character == null)
        {
            outcome.FailureReason = $"Unknown troop '{troopId}'. Check ModuleData/troops for the id.";
            return outcome;
        }

        var origin = CreateOrigin(character);

        // A creature bandit is an enemy at all times (#742): asked for as an ally, it spawns on the enemy side.
        var requestedPlayerSide = isPlayerSide;
        isPlayerSide = CreatureBanditRules.SpawnsOnPlayerSide(troopId, requestedPlayerSide);
        if (isPlayerSide != requestedPlayerSide)
            outcome.Note = $"{troopId} is a creature bandit and fights only as an enemy, so the player's side is ignored.";

        // SpawnTroop does `.ClothingColor1(agentTeam.Color)` with NO null check, so a null team
        // hard-crashes the game instead of printing a sentence.
        // Only the ENEMY path can actually return null: GetAgentTeam returns Current.PlayerEnemyTeam
        // unguarded, and that is null in town/village missions. The ally path never returns null: it
        // falls back to Current.PlayerTeam when PlayerAllyTeam is missing, so `ally` in a town spawns
        // onto the player's own team rather than refusing (except a creature bandit, sent to the enemy side above). That is the engine's behaviour, not a bug
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
                // Ring-offset per slot so N troops do not stack inside each other. Direction is
                // passed explicitly: SpawnTroop dereferences initialDirection.Value unconditionally
                // once initialPosition has a value.
                var slot = ringStart + i;
                var angle = slot * 0.7f;
                var radius = 2f + slot * 0.35f;
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

    /// <summary>
    /// A campaign troop gets SimpleAgentOrigin, which hard-casts its troop to CharacterObject
    /// (SimpleAgentOrigin.cs:136); anything else, a Custom Battle troop, gets BasicBattleAgentOrigin.
    /// </summary>
    internal static IAgentOriginBase CreateOrigin(BasicCharacterObject character) =>
        character is CharacterObject campaignCharacter
            ? new SimpleAgentOrigin(campaignCharacter)
            : new BasicBattleAgentOrigin(character);

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
