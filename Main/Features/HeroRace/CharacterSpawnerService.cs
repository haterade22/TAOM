using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Features.HeroRace.Configuration;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.Scripts;
using static TAOM.Features.HeroRace.Configuration.RacePositionConfig;

namespace TAOM.Features.HeroRace;

public class CharacterSpawnerService : ICharacterSpawnerService
{
    private readonly IRaceManager _raceManager;
    private readonly IFaceGenAdapter _faceGenAdapter;
    private readonly IModLogger _logger;
    private readonly RacePositionConfig _config;
    private readonly Dictionary<string, RacePositionConfigItem> _configLookup;

    public CharacterSpawnerService(IRaceManager raceManager, IFaceGenAdapter faceGenAdapter, IModLogger logger)
    {
        _raceManager = raceManager ?? throw new ArgumentNullException(nameof(raceManager));
        _faceGenAdapter = faceGenAdapter ?? throw new ArgumentNullException(nameof(faceGenAdapter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = RacePositionConfig.LoadConfig("CharacterImagePatch");
        _configLookup = BuildConfigLookup(_config);
    }

    private static Dictionary<string, RacePositionConfigItem> BuildConfigLookup(RacePositionConfig config)
    {
        var lookup = new Dictionary<string, RacePositionConfigItem>(StringComparer.OrdinalIgnoreCase);
        if (config?.Items != null)
        {
            foreach (var item in config.Items)
            {
                if (!string.IsNullOrEmpty(item.Race))
                {
                    lookup[item.Race] = item;
                }
            }
        }
        return lookup;
    }

    public void InitWithCharacter(CharacterSpawner spawner, CharacterCode characterCode, bool useBodyProperties = false)
    {
        GameEntity agentEntity = ReflectionHelper.GetFieldValue<CharacterSpawner, GameEntity>(spawner, "_agentEntity");
        GameEntity horseEntity = ReflectionHelper.GetFieldValue<CharacterSpawner, GameEntity>(spawner, "_horseEntity");
        AgentVisuals agentVisuals = ReflectionHelper.GetFieldValue<CharacterSpawner, AgentVisuals>(spawner, "_agentVisuals");
        MatrixFrame spawnFrame = ReflectionHelper.GetFieldValue<CharacterSpawner, MatrixFrame>(spawner, "_spawnFrame");
        bool CreateFaceImmediately = ReflectionHelper.GetFieldValue<CharacterSpawner, bool>(spawner, "CreateFaceImmediately");

        spawner.GameEntity.BreakPrefab();
        if (agentEntity != null && agentEntity.Parent == spawner.GameEntity)
        {
            spawner.GameEntity.RemoveChild(agentEntity.WeakEntity, keepPhysics: false, keepScenePointer: false, callScriptCallbacks: true, 35);
        }

        agentVisuals?.Reset();
        agentVisuals?.GetVisuals()?.ManualInvalidate();
        if (horseEntity != null && horseEntity.Parent == spawner.GameEntity)
        {
            horseEntity.Scene.RemoveEntity(horseEntity, 98);
        }

        // 1.3: GameEntity.CreateEmpty signature
        agentEntity = GameEntity.CreateEmpty(spawner.GameEntity.Scene, isModifiableFromEditor: false);
        agentEntity.Name = "TableauCharacterAgentVisualsEntity";
        spawnFrame = agentEntity.GetFrame();
        agentEntity.SetFrame(ref spawnFrame);
        ReflectionHelper.SetFieldValue(spawner, "_spawnFrame", spawnFrame);
        ReflectionHelper.SetFieldValue(spawner, "_agentEntity", agentEntity);

        BodyProperties bodyProperties = characterCode.BodyProperties;

        if (useBodyProperties)
        {
            // 1.3: BodyProperties.FromString uses ref parameter
            BodyProperties.FromString(spawner.BodyPropertiesString, out bodyProperties);
        }

        if (characterCode.Color1 != uint.MaxValue)
        {
            ReflectionHelper.SetPropertyValue(spawner, "ClothColor1", characterCode.Color1);
        }

        if (characterCode.Color2 != uint.MaxValue)
        {
            ReflectionHelper.SetPropertyValue(spawner, "ClothColor2", characterCode.Color2);
        }

        Monster baseMonsterFromRace = _faceGenAdapter.GetBaseMonsterFromRace(characterCode.Race);

        // Resolve the action set by RACE NAME ("as_dwarf_warrior"), not via the engine's
        // GetActionSetWithSuffix(monster,…). That engine path resolves a custom-race base monster to
        // the HUMAN action set ("as_human_warrior"), which loads the human skeleton; race-specific
        // clothing meshes (rigged to e.g. dwarf_skeleton_a) then can't bind and render invisible —
        // the naked arena-spectator crowd (519 scene CharacterSpawner "crowd" entities route through
        // here). See docs/features/face-morph-compat.md "Arena-spectator naked-dwarf".
        MBActionSet raceActionSet = ResolveRaceActionSet(characterCode.Race, characterCode.IsFemale, spawner.ActionSetSuffix, baseMonsterFromRace);

        // 1.3: ActionCode takes in ActionIndexCache
        var idleStart = ActionIndexCache.Create("act_inventory_idle_start");
        agentVisuals = AgentVisuals.Create(new AgentVisualsData().Equipment(characterCode.CalculateEquipment()).BodyProperties(bodyProperties).Race(characterCode.Race)
            .Frame(spawnFrame)
            .Scale(1f)
            .SkeletonType(characterCode.IsFemale ? SkeletonType.Female : SkeletonType.Male)
            .Entity(agentEntity)
            .ActionSet(raceActionSet)
            .ActionCode(in idleStart)
            .Scene(spawner.GameEntity.Scene)
            .Monster(baseMonsterFromRace)
            .PrepareImmediately(CreateFaceImmediately)
            .Banner(characterCode.Banner)
            .ClothColor1(spawner.ClothColor1)
            .ClothColor2(spawner.ClothColor2)
            .UseMorphAnims(useMorphAnims: true), "TableauCharacterAgentVisuals", isRandomProgress: false, needBatchedVersionForWeaponMeshes: false, forceUseFaceCache: false);

        agentVisuals.SetAction(ActionIndexCache.Create(spawner.PoseAction), MBMath.ClampFloat(spawner.AnimationProgress, 0f, 1f));
        spawner.GameEntity.AddChild(agentEntity.WeakEntity);

        ReflectionHelper.SetFieldValue(spawner, "_agentVisuals", agentVisuals);

        ReflectionHelper.CallPrivateMethod(spawner, "WieldWeapon", new Object[] { characterCode });
        agentVisuals = ReflectionHelper.GetFieldValue<CharacterSpawner, AgentVisuals>(spawner, "_agentVisuals");
        MatrixFrame frame = MatrixFrame.Identity;

        var raceName = _raceManager.GetRaceNameFromId(characterCode.Race);
        _configLookup.TryGetValue(raceName, out var configitem);

        if (configitem != null)
        {
            frame.origin.x = frame.origin.x + configitem.Horizontal;
            frame.origin.y = frame.origin.y + configitem.Zoom;
            frame.origin.z = frame.origin.z + configitem.Vertical;
        }

        agentVisuals.GetVisuals().SetFrame(ref frame);

        if (spawner.HasMount)
        {
            ReflectionHelper.SetFieldValue(spawner, "_horseEntity", (GameEntity)null);
            SpawnMountLogged(spawner, characterCode);
            horseEntity = ReflectionHelper.GetFieldValue<CharacterSpawner, GameEntity>(spawner, "_horseEntity");
        }

        spawner.GameEntity.SetVisibilityExcludeParents(visible: true);
        agentEntity.SetVisibilityExcludeParents(visible: true);
        if (horseEntity != null)
        {
            horseEntity.SetVisibilityExcludeParents(visible: true);
        }

        Skeleton skeleton = agentVisuals.GetVisuals().GetSkeleton();
        skeleton.Freeze(p: false);
        skeleton.TickAnimationsAndForceUpdate(0.001f, agentVisuals.GetVisuals().GetGlobalFrame(), tickAnimsForChildren: false);
        skeleton.SetUptoDate(value: false);
        skeleton.Freeze(p: true);
        agentEntity.SetBoundingboxDirty();
        skeleton.Freeze(p: false);
        skeleton.TickAnimationsAndForceUpdate(0.001f, agentVisuals.GetVisuals().GetGlobalFrame(), tickAnimsForChildren: false);
        skeleton.SetAnimationParameterAtChannel(0, MBMath.ClampFloat(spawner.AnimationProgress, 0f, 1f));
        skeleton.SetUptoDate(value: false);
        skeleton.Freeze(p: true);
        skeleton.ManualInvalidate();

        if (horseEntity != null)
        {
            horseEntity.Skeleton.Freeze(p: false);
            horseEntity.Skeleton.TickAnimationsAndForceUpdate(0.001f, horseEntity.GetGlobalFrame(), tickAnimsForChildren: false);
            horseEntity.Skeleton.SetUptoDate(value: false);
            horseEntity.Skeleton.Freeze(p: true);
            horseEntity.SetBoundingboxDirty();
        }

        if (horseEntity != null)
        {
            horseEntity.Skeleton.Freeze(p: false);
            horseEntity.Skeleton.TickAnimationsAndForceUpdate(0.001f, horseEntity.GetGlobalFrame(), tickAnimsForChildren: false);
            horseEntity.Skeleton.SetAnimationParameterAtChannel(0, MBMath.ClampFloat(spawner.HorseAnimationProgress, 0f, 1f));
            horseEntity.Skeleton.SetUptoDate(value: false);
            horseEntity.Skeleton.Freeze(p: true);
        }

        spawner.GameEntity.SetBoundingboxDirty();
        if (!spawner.GameEntity.Scene.IsEditorScene())
        {
            if (agentEntity != null)
            {
                agentEntity.ManualInvalidate();
            }

            if (horseEntity != null)
            {
                horseEntity.ManualInvalidate();
            }
        }

        ReflectionHelper.SetFieldValue(spawner, "_agentEntity", agentEntity);
        ReflectionHelper.SetFieldValue(spawner, "_horseEntity", horseEntity);
        ReflectionHelper.SetFieldValue(spawner, "_agentVisuals", agentVisuals);
        ReflectionHelper.SetFieldValue(spawner, "_spawnFrame", spawnFrame);
    }

    // Resolves the humanoid action set for a spawned tableau/scene character by RACE NAME.
    // The arena cheering crowd is built from scene CharacterSpawner entities through this service.
    // The engine's GetActionSetWithSuffix resolves a custom-race base monster to the HUMAN action
    // set ("as_human_warrior") — loading the human skeleton, so race-rigged clothing meshes can't
    // bind and render invisible (naked dwarf spectators, confirmed in the rgl/taom_debug logs).
    // We build "as_<race>_<suffix>" explicitly (e.g. as_dwarf_warrior), mirroring the proven-correct
    // CharacterTableau_RefreshCharacterTableau_Patch (which is why CC + encyclopedia dwarves are
    // already right), and fall back to "as_<race>_warrior" if the suffix-specific set isn't authored.
    // Human / unknown races keep the original engine resolution — no change for vanilla-mesh races.
    private MBActionSet ResolveRaceActionSet(int race, bool isFemale, string suffix, Monster baseMonster)
    {
        var raceName = _raceManager.GetRaceNameFromId(race);

        if (!_raceManager.IsValidRaceId(race) || string.Equals(raceName, "human", StringComparison.OrdinalIgnoreCase))
        {
            var vanilla = MBGlobals.GetActionSetWithSuffix(baseMonster, isFemale, suffix);
            _logger.LogDebug($"[HeroRace][CrowdSpawn] race={race}('{raceName}') -> vanilla GetActionSetWithSuffix(suffix='{suffix}', valid={vanilla.IsValid})");
            return vanilla;
        }

        var (primary, fallback) = BuildRaceActionSetNames(raceName, isFemale, suffix);
        var set = MBActionSet.GetActionSet(primary);
        bool usedFallback = !set.IsValid;
        if (usedFallback)
        {
            set = MBActionSet.GetActionSet(fallback);
        }

        _logger.LogDebug(
            $"[HeroRace][CrowdSpawn] race={race}('{raceName}') monster='{baseMonster?.StringId}'/base='{baseMonster?.BaseMonster}' " +
            $"suffix='{suffix}' primary='{primary}'(valid={!usedFallback})" +
            (usedFallback ? $" -> fallback='{fallback}'(valid={set.IsValid})" : string.Empty) +
            $" final-valid={set.IsValid}");

        return set;
    }

    // Pure: build the race-prefixed action-set name + a guaranteed-base "_warrior" fallback.
    // e.g. ("dwarf", false, "_villager") -> ("as_dwarf_villager", "as_dwarf_warrior").
    internal static (string primary, string fallback) BuildRaceActionSetNames(string raceName, bool isFemale, string suffix)
    {
        var prefix = isFemale ? $"as_{raceName}_female" : $"as_{raceName}";
        return ($"{prefix}{suffix ?? string.Empty}", $"{prefix}_warrior");
    }

    // DIAGNOSTIC (spider-mount tableau AV, 2026-06-10): replicates the private
    // CharacterSpawner.SpawnMount body (decompiled v1.4.5, Native/bin View dll) with a
    // write-ahead log line before each native call so the TAOM log names the exact dying
    // step and the skeleton's native pointer. On ANY failure the half-built mount entity
    // is removed and the spawn continues mount-less instead of crashing the game.
    // HandleProcessCorruptedStateExceptions lets the catch see a native AccessViolation.
    [HandleProcessCorruptedStateExceptions]
    private void SpawnMountLogged(CharacterSpawner spawner, CharacterCode characterCode)
    {
        GameEntity horse = null;
        string step = "CalculateEquipment";
        try
        {
            Equipment equipment = characterCode.CalculateEquipment();
            ItemObject mountItem = equipment[(EquipmentIndex)10].Item;
            if (mountItem == null)
            {
                spawner.HasMount = false;
                return;
            }

            Monster monster = mountItem.HorseComponent.Monster;
            _logger.LogDebug($"[MountSpawn] item={mountItem.StringId} monster={monster.StringId} actionSet={monster.ActionSetCode} usage={monster.MonsterUsage} pose={spawner.PoseActionForHorse}");

            step = "GameEntity.CreateEmpty";
            horse = GameEntity.CreateEmpty(spawner.GameEntity.Scene, isModifiableFromEditor: false);
            horse.Name = "MountEntity";

            step = "MBActionSet.GetActionSet";
            MBActionSet actionSet = MBActionSet.GetActionSet(monster.ActionSetCode);
            string skeletonName = actionSet.IsValid ? actionSet.GetSkeletonName() : null;
            _logger.LogDebug($"[MountSpawn] actionSet.IsValid={actionSet.IsValid} skeletonName='{skeletonName}'");

            step = "CreateAgentSkeleton";
            horse.CreateAgentSkeleton(skeletonName, isHumanoid: false, actionSet, monster.MonsterUsage, monster);
            step = "CopyComponentsToSkeleton";
            horse.CopyComponentsToSkeleton();
            Skeleton skeleton = horse.Skeleton;
            _logger.LogDebug($"[MountSpawn] skeleton={(skeleton == null ? "NULL" : "ok")} nativePtr=0x{(skeleton?.Pointer ?? UIntPtr.Zero):X}");

            step = "SetAgentActionChannel";
            ActionIndexCache pose = ActionIndexCache.Create(spawner.PoseActionForHorse);
            skeleton.SetAgentActionChannel(0, in pose, MBMath.ClampFloat(spawner.HorseAnimationProgress, 0f, 1f));

            step = "AddChild";
            spawner.GameEntity.AddChild(horse.WeakEntity);

            step = "AddMountMeshToEntity";
            ItemObject harnessItem = equipment[(EquipmentIndex)11].Item;
            MountVisualCreator.AddMountMeshToEntity(horse, mountItem, harnessItem, MountCreationKey.GetRandomMountKeyString(mountItem, MBRandom.RandomInt()));

            step = "SetVisibilityExcludeParents";
            horse.SetVisibilityExcludeParents(visible: true);

            step = "TickAnimations";
            AgentVisuals visuals = ReflectionHelper.GetFieldValue<CharacterSpawner, AgentVisuals>(spawner, "_agentVisuals");
            _logger.LogDebug("[MountSpawn] entering TickAnimations (the historical AV site)...");
            horse.Skeleton.TickAnimations(0.01f, visuals.GetVisuals().GetGlobalFrame(), tickAnimsForChildren: true);

            ReflectionHelper.SetFieldValue(spawner, "_horseEntity", horse);
            _logger.LogDebug("[MountSpawn] success");
        }
        catch (Exception ex)
        {
            _logger.LogError($"[MountSpawn] FAILED at step '{step}': {ex.GetType().Name}: {ex.Message} -- skipping mount for this tableau spawn");
            try
            {
                if (horse != null)
                {
                    spawner.GameEntity.Scene?.RemoveEntity(horse, 98);
                }
            }
            catch
            {
                // entity may be half-built; never let cleanup mask the diagnostic
            }
            ReflectionHelper.SetFieldValue(spawner, "_horseEntity", (GameEntity)null);
            spawner.HasMount = false;
        }
    }
}
