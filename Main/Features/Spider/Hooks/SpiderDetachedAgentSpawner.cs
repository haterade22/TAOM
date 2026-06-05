using System;
using System.Reflection;
using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace TAOM.Features.Spider.Hooks;

/// <summary>
/// Boundary glue that spawns the giant spider as a DETACHED non-humanoid combatant — mirroring the
/// engine's free-mount path (Mission.SpawnMonster / CreateHorseAgentFromRosterElements) and adding
/// Team + Origin so it fights and counts as a casualty.
///
/// Why this avoids the crash: <c>CreationType.FromHorseObj</c> makes <c>EquipItemsFromSpawnEquipment</c>
/// skip <c>AddSkinMeshes</c> (Agent.cs:4532-4563), so no humanoid skin is built on the spider skeleton;
/// <c>BuildAgent(null)</c> leaves <c>Formation</c> null and forces <c>Controller = AI</c> for a
/// non-humanoid (Agent.cs:5172-5173) — i.e. a detached, AI-driven, team-loyal combatant. The body mesh
/// comes from the <c>spider_mount_a</c> Horse item seated at <c>EquipmentIndex.ArmorItemEndSlot</c>
/// (== Horse, slot 10); the Monster supplies the skeleton + animation. <c>Mission.CreateAgent</c> and
/// <c>Mission.BuildAgent</c> are private, so they are reflected (cached). This is engine-coupled
/// boundary glue — verified in-game, not unit-tested. See docs/reviews/rca-spider-troop-2026-06-04.md.
///
/// NOTE: because the Mission.SpawnAgent prefix returns false and we call BuildAgent directly, we must
/// fire OnAgentBuild ourselves — vanilla SpawnAgent fires it at Mission.cs:4346 (after BuildAgent), and
/// that is what attaches the SpiderTree BT (and registers the agent with casualty/upgrade trackers) for
/// reinforcement-wave spiders. See <see cref="NotifyAgentBuilt"/>.
/// </summary>
internal static class SpiderDetachedAgentSpawner
{
    private static readonly MethodInfo CreateAgentMI = AccessTools.Method(
        typeof(Mission), "CreateAgent",
        new[]
        {
            typeof(Monster), typeof(bool), typeof(int), typeof(Agent.CreationType),
            typeof(float), typeof(int), typeof(int), typeof(BasicCharacterObject)
        });

    private static readonly MethodInfo BuildAgentMI = AccessTools.Method(
        typeof(Mission), "BuildAgent", new[] { typeof(Agent), typeof(AgentBuildData) });

    // Mount visual/render init (internal). The engine's free-mount path
    // (Mission.CreateHorseAgentFromRosterElements) calls this right after CreateAgent; it seats the
    // display name + MountCreationKey that drive mesh-variant selection. WITHOUT it the agent's render
    // data is uninitialized and BuildAgent -> Agent.PreloadForRendering throws a native AccessViolation.
    private static readonly MethodInfo SetMountInitialValuesMI = AccessTools.Method(
        typeof(Agent), "SetMountInitialValues", new[] { typeof(TextObject), typeof(string) });

    private static IModLogger? _logger;
    private static bool _bindingErrorLogged;
    private static bool _assetErrorLogged;
    private static bool _onAgentBuildErrorLogged;

    /// <summary>True when both private Mission methods bound — asserted by a BindingVerification test so
    /// an engine-version bump fails fast in CI instead of silently fail-opening to the humanoid anchor.</summary>
    internal static bool BindingsResolved =>
        CreateAgentMI != null && BuildAgentMI != null && SetMountInitialValuesMI != null;

    public static void Initialize(IModLogger logger) => _logger = logger;

    /// <summary>
    /// Spawns the detached spider for <paramref name="source"/> (the build data the Mission.SpawnAgent
    /// prefix intercepted) and returns the agent, or <c>null</c> to fail open (caller then lets the
    /// vanilla humanoid-anchor spawn proceed — no crash). Never throws.
    /// </summary>
    public static Agent? TrySpawnDetachedSpider(Mission mission, AgentBuildData source)
    {
        if (mission == null || source == null)
            return null;

        if (CreateAgentMI == null || BuildAgentMI == null || SetMountInitialValuesMI == null)
        {
            if (!_bindingErrorLogged)
            {
                _bindingErrorLogged = true;
                _logger?.LogError("[Spider] Detached spawn unavailable — Mission.CreateAgent/BuildAgent or " +
                                  "Agent.SetMountInitialValues reflection did not bind (engine signature drift?). " +
                                  "Spawning humanoid anchor.");
            }
            return null;
        }

        Monster spiderMonster = MBObjectManager.Instance?.GetObject<Monster>(SpiderConfig.SpiderMonsterId);
        ItemObject meshItem = MBObjectManager.Instance?.GetObject<ItemObject>(SpiderConfig.SpiderMountItemId);
        BasicCharacterObject character = source.AgentCharacter;
        if (spiderMonster == null || meshItem == null || character == null)
        {
            if (!_assetErrorLogged)
            {
                _assetErrorLogged = true;
                _logger?.LogError(
                    $"[Spider] Detached spawn unavailable — Monster '{SpiderConfig.SpiderMonsterId}'={spiderMonster != null}, " +
                    $"item '{SpiderConfig.SpiderMountItemId}'={meshItem != null}, character={character != null}. " +
                    "Verify LOTRLOME_Armory loads before TAOM. Spawning humanoid anchor instead.");
            }
            return null;
        }

        // --- Spawn frame ----------------------------------------------------------------------------
        // A normally-spawned troop is positioned by vanilla Mission.SpawnAgent from its Formation's
        // deployment slot (Mission.cs:4133-4177 -> the PUBLIC GetTroopSpawnFrameWithIndex). Because the
        // prefix returns false and bypasses SpawnAgent, we must reproduce that slot frame ourselves — or
        // the agent lands at the build-data position, which for a deployment spawn is UNSET (0,0,0). The
        // native SetInitialFrame then relocates (0,0,0) to a far navmesh point, away from the army, which
        // is exactly the "8 spawned but I was all alone" symptom.
        Formation? formation = source.AgentFormation;
        Vec3 position;
        Vec2 direction;
        string frameSource;
        if (formation != null && source.AgentTeam != null && !source.AgentInitialPosition.HasValue)
        {
            try
            {
                // Computes the exact per-unit slot frame vanilla uses, and self-guarantees a valid
                // position (internally falls back to the formation spawn position — Mission.cs:3966-3968).
                mission.GetTroopSpawnFrameWithIndex(
                    source, source.AgentFormationTroopSpawnIndex, source.AgentFormationTroopSpawnCount,
                    out position, out direction);
                frameSource = "formation-slot";
            }
            catch (Exception fe)
            {
                position = source.AgentInitialPosition.GetValueOrDefault();
                direction = source.AgentInitialDirection.GetValueOrDefault();
                frameSource = "fallback-after-throw";
                _logger?.LogError($"[Spider][diag] GetTroopSpawnFrameWithIndex threw " +
                                  $"{fe.GetType().Name}: {fe.Message} — using build-data frame {position}.");
            }
        }
        else
        {
            position = source.AgentInitialPosition.GetValueOrDefault();
            direction = source.AgentInitialDirection.GetValueOrDefault();
            frameSource = "build-data";
        }

        // A zero/NaN direction makes the native SetInitialFrame normalize a zero vector -> NaN rotation
        // matrix, which propagates into the agent's frame and crashes BuildAgent -> PreloadForRendering
        // with an AccessViolation. Fall back to a valid facing (the formation reorients on the first tick).
        if (!direction.IsValid || !direction.IsNonZero())
            direction = Vec2.Forward;

        // TEMP verbose diagnostic ([Spider][diag] — grep + remove once the spider is shipping). Dumps
        // everything we know about the intercepted spawn so we can see WHERE the agent goes and WHETHER it
        // has a formation to join.
        _logger?.LogInfo(
            $"[Spider][diag] intercept char={character.StringId} monster={SpiderConfig.SpiderMonsterId} " +
            $"mesh={SpiderConfig.SpiderMountItemId} team={source.AgentTeam?.Side.ToString() ?? "null"} " +
            $"formation={(formation != null ? formation.FormationIndex.ToString() : "null")} " +
            $"formationUnits={(formation != null ? formation.CountOfUnits.ToString() : "n/a")} " +
            $"formationPositioned={(formation != null ? formation.HasBeenPositioned.ToString() : "n/a")} " +
            $"spawnIdx={source.AgentFormationTroopSpawnIndex} spawnCount={source.AgentFormationTroopSpawnCount} " +
            $"hasInitPos={source.AgentInitialPosition.HasValue} frameSource={frameSource} pos={position} dir={direction}");

        Agent agent;
        try
        {
            // 1) FromHorseObj creation — no humanoid skin. Passing the spider character sets
            //    agent.Character (+ Health) inside CreateAgent (Mission.cs:4048).
            agent = (Agent)CreateAgentMI.Invoke(mission, new object[]
            {
                spiderMonster, character.IsFemale, 0, Agent.CreationType.FromHorseObj,
                1f, -1, spiderMonster.Weight, character
            });
            if (agent == null)
                return null;

            // 2) Position/direction (public).
            agent.SetInitialFrame(in position, in direction);

            // 2.5) Initialize the mount visual/render data (display name + MountCreationKey) EXACTLY as
            //      the engine's CreateHorseAgentFromRosterElements does. The MountCreationKey selects the
            //      mesh variant/material; without it the agent's render data is uninitialized and
            //      BuildAgent -> Agent.PreloadForRendering throws a native AccessViolation. Reflected
            //      because SetMountInitialValues is internal.
            string mountKey = MountCreationKey.GetRandomMountKeyString(meshItem, MBRandom.RandomInt());
            SetMountInitialValuesMI.Invoke(agent, new object[]
            {
                new EquipmentElement(meshItem).GetModifiedItemName(), mountKey
            });

            // 3) Combatant wiring the free-mount path lacks: Team (friend/foe) + Origin (casualty/XP).
            if (source.AgentTeam != null)
                agent.SetTeam(source.AgentTeam, false);
            agent.Origin = source.AgentOrigin;

            // 4) Seat the spider body mesh in the mount slot (public; SpawnEquipment's setter is private).
            var spawnEquipment = new Equipment
            {
                [EquipmentIndex.ArmorItemEndSlot] = new EquipmentElement(meshItem)
            };
            agent.InitializeSpawnEquipment(spawnEquipment);

            // 5) Build via the private method (null build data == the free-mount path: Formation null, AI).
            BuildAgentMI.Invoke(mission, new object[] { agent, null });
        }
        catch (Exception e)
        {
            Exception inner = e.InnerException ?? e;
            _logger?.LogError($"[Spider] Detached spider spawn threw {inner.GetType().Name}: {inner.Message}. " +
                              $"Spawning humanoid anchor instead.\n{inner.StackTrace}");
            return null;
        }

        // 6) The agent is built + committed (in Mission._activeAgents/_allAgents).
        // TEMP verbose diagnostic — agent state immediately after BuildAgent.
        _logger?.LogInfo(
            $"[Spider][diag] BUILT name='{agent.Name}' isMount={agent.IsMount} isHuman={agent.IsHuman} " +
            $"hasBuilt={agent.HasBeenBuilt} formationNullAfterBuild={agent.Formation == null} " +
            $"pos={agent.Position} health={agent.Health} team={agent.Team?.Side.ToString() ?? "null"}");

        // 6.5) Give the agent a valid (empty) MissionEquipment. The FromHorseObj build path leaves
        //      Agent.Equipment (MissionEquipment) null — a mount carries no weapons — but the moment the
        //      agent becomes a formation member the engine classifies it via QueryLibrary.IsInfantry ->
        //      Agent.IsRangedCached -> Equipment.ContainsNonConsumableRangedWeaponWithAmmo() (Agent.cs:887)
        //      and NREs on the null Equipment (confirmed stack: AddUnit -> OnUnitAddedOrRemoved ->
        //      FormationQuerySystem -> IsInfantry). InitializeMissionEquipment(null, null) builds an empty
        //      MissionEquipment from the agent's (weaponless) SpawnEquipment, so every weapon-cache query
        //      returns false instead of throwing. MUST run before AttachToFormation.
        EnsureMissionEquipment(agent);

        // 7) Formation membership — GATED OFF for now (SpiderConfig.EnableFormationMembership == false).
        //    Detached + positioned (above) is the verified non-crashing state: the agent spawns in the
        //    deployment zone but is NOT a formation member, so the engine's team-AI never reads its
        //    (uninitialized) native weapon state.
        //
        //    Why it is gated: enrolling the agent (agent.Formation = formation) triggers
        //    TeamAIGeneral.OnUnitAddedToFormationForTheFirstTime -> BehaviorSkirmish ->
        //    FormationQuerySystem reads agent.MaximumMissileRange -> native GetMissileRange() ->
        //    AccessViolation, because the FromHorseObj build path skips EquipItemsFromSpawnEquipment's
        //    native WeaponEquipped loop (Agent.cs:4532 has no FromHorseObj case) and the agent's native
        //    per-slot weapon records are never initialized. InitializeNativeWeaponState (below) is the
        //    root fix — it runs the SAME native WeaponEquipped(Invalid) for each slot WITHOUT AddSkinMeshes
        //    — but it is UNVERIFIED in-game (native bodies unreadable), so it stays behind the flag until
        //    tested. Detached spiders are also currently PASSIVE: the SpiderTree BT only bites adjacent
        //    targets and has no move-to-enemy node — advancing requires either formation movement orders
        //    (this path) or a new BT movement node. See docs/reviews/rca-spider-troop-2026-06-04.md.
        if (SpiderConfig.EnableFormationMembership)
        {
            InitializeNativeWeaponState(agent);
            AttachToFormation(agent, formation);
        }
        else
        {
            _logger?.LogInfo("[Spider][diag] formation membership disabled (EnableFormationMembership=false) — " +
                             "agent stays detached + positioned (non-crashing; passive until an enemy is adjacent).");
        }

        // 8) Fire OnAgentBuild — vanilla SpawnAgent does this at Mission.cs:4346 but we replaced its body.
        //    Attaches the SpiderTree BT (and casualty/upgrade trackers) for reinforcement-wave spiders.
        NotifyAgentBuilt(mission, agent);

        _logger?.LogInfo(
            $"[Spider] Spawned detached spider agent (team={source.AgentTeam?.Side.ToString() ?? "none"}, " +
            $"formation={(agent.Formation != null ? agent.Formation.FormationIndex.ToString() : "null")}, " +
            $"formationUnits={(agent.Formation != null ? agent.Formation.CountOfUnits.ToString() : "n/a")}, " +
            $"pos={agent.Position}).");
        return agent;
    }

    /// <summary>
    /// Ensures the detached agent has a non-null <see cref="Agent.Equipment"/> (MissionEquipment). The
    /// FromHorseObj build path never creates one (mounts hold no weapons), but formation classification
    /// (<c>QueryLibrary.IsInfantry</c> -> <c>Agent.IsRangedCached</c> -> <c>Equipment.Contains...()</c>)
    /// dereferences it. <c>InitializeMissionEquipment(null, null)</c> (Agent.cs:3626) builds an empty
    /// MissionEquipment from the agent's weaponless SpawnEquipment. Guarded + logged; never throws out.
    /// </summary>
    private static void EnsureMissionEquipment(Agent agent)
    {
        try
        {
            bool wasNull = agent.Equipment == null;
            if (wasNull)
                agent.InitializeMissionEquipment(null, null);
            _logger?.LogInfo(
                $"[Spider][diag] MissionEquipment ready (wasNull={wasNull}, nowNull={agent.Equipment == null}, " +
                $"isRangedCached={agent.IsRangedCached}, hasMeleeCached={agent.HasMeleeWeaponCached}).");
        }
        catch (Exception e)
        {
            Exception inner = e.InnerException ?? e;
            _logger?.LogError($"[Spider] EnsureMissionEquipment threw {inner.GetType().Name}: {inner.Message} — " +
                              "weapon-cache queries may still NRE when the agent joins a formation.");
        }
    }

    /// <summary>
    /// Initializes the agent's NATIVE per-slot weapon records to empty (no weapons). The FromHorseObj
    /// build path skips EquipItemsFromSpawnEquipment's WeaponEquipped loop (Agent.cs:4532 has no
    /// FromHorseObj case), leaving the native weapon/wield struct uninitialized — native reads such as
    /// <c>GetMissileRange()</c>/<c>CurrentAimingError</c> walk that struct and AccessViolation once the
    /// agent is a formation member or enters combat. <c>RemoveEquippedWeapon(slot)</c> runs the SAME
    /// native <c>WeaponEquipped(InvalidWeaponData)</c> the normal path runs for empty slots
    /// (Agent.cs:4814-4825) WITHOUT <c>AddSkinMeshes</c> — so no humanoid skin build. UNVERIFIED in-game
    /// (native bodies unreadable); only invoked when <see cref="SpiderConfig.EnableFormationMembership"/>
    /// is on, behind which it is being validated. Guarded + logged; never throws out.
    /// </summary>
    private static void InitializeNativeWeaponState(Agent agent)
    {
        try
        {
            for (int slot = (int)EquipmentIndex.WeaponItemBeginSlot; slot < (int)EquipmentIndex.NumAllWeaponSlots; slot++)
                agent.RemoveEquippedWeapon((EquipmentIndex)slot);
            _logger?.LogInfo("[Spider][diag] native weapon state initialized (RemoveEquippedWeapon x5).");
        }
        catch (Exception e)
        {
            Exception inner = e.InnerException ?? e;
            _logger?.LogError($"[Spider] InitializeNativeWeaponState threw {inner.GetType().Name}: {inner.Message} — " +
                              "native weapon reads (GetMissileRange/aiming) may still AccessViolation.");
        }
    }

    /// <summary>
    /// Enrolls the freshly-built detached agent into the formation the intercepted troop was destined for
    /// (<paramref name="formation"/> == <c>source.AgentFormation</c>). This is what makes the spider appear
    /// in the player's army line and be commandable — the free-mount build path leaves it formation-less.
    /// The <see cref="Agent.Formation"/> setter (Agent.cs:1104) does the real work (AddUnit + positioning),
    /// has no IsMount guard, and is designed to run post-build. Guarded + heavily logged; never throws out.
    /// </summary>
    private static void AttachToFormation(Agent agent, Formation? formation)
    {
        if (formation == null)
        {
            _logger?.LogInfo("[Spider][diag] no formation on build data — agent stays detached (Formation=null).");
            return;
        }

        try
        {
            int before = formation.CountOfUnits;
            agent.Formation = formation;
            _logger?.LogInfo(
                $"[Spider][diag] attach formation={formation.FormationIndex}: units {before} -> {formation.CountOfUnits}, " +
                $"agent.Formation={(agent.Formation != null ? agent.Formation.FormationIndex.ToString() : "null")}, " +
                $"agentPosAfterAttach={agent.Position}.");
        }
        catch (Exception e)
        {
            Exception inner = e.InnerException ?? e;
            _logger?.LogError($"[Spider] AttachToFormation threw {inner.GetType().Name}: {inner.Message} — " +
                              "agent remains detached (no army-line membership).");
        }
    }

    private static void NotifyAgentBuilt(Mission mission, Agent agent)
    {
        foreach (MissionBehavior behavior in mission.MissionBehaviors)
        {
            try
            {
                behavior.OnAgentBuild(agent, null);
            }
            catch (Exception e)
            {
                if (!_onAgentBuildErrorLogged)
                {
                    _onAgentBuildErrorLogged = true;
                    Exception inner = e.InnerException ?? e;
                    _logger?.LogError($"[Spider] OnAgentBuild({behavior.GetType().Name}) threw " +
                                      $"{inner.GetType().Name}: {inner.Message} (suppressing further OnAgentBuild errors).");
                }
            }
        }
    }
}
