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
        // Diagnostics 2026-07-31: re-probe at the first REAL tableau construction. If this disagrees
        // with the startup probe, action sets were still merging after OnGameInitializationFinished.
        Diagnostics.TableauDiagnostics.ProbeActionSets("first-tableau");

        // 2026-08-01: the ActionIndexCache static/live comparison. Safe here because this method
        // already resolves actions, so action types are loaded by definition.
        //
        // This is a CONFIRMATION, not the primary evidence: SubModule.OnGameInitializationFinished
        // and the CharacterTableau patches will normally have repaired already, so a "healthy"
        // verdict here is expected. The repair's own "REPAIRED n" line is what records that the
        // fault occurred. Note also that this path never reads a poisoned static itself — it
        // resolves via live Create() calls below — so it cannot be the backstop for the fault.
        Diagnostics.TableauDiagnostics.ProbeActionIndexHealth("first-tableau");
        ActionIndexCacheRepair.TryEnsureRepaired("first-tableau");

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

        // 1.3: ActionCode takes in ActionIndexCache
        var idleStart = ActionIndexCache.Create("act_inventory_idle_start");

        // Diagnostics 2026-07-31 ("bendy man"): this method builds the tableau's actual visual, so
        // it is where a prone character is decided. Hoisted into locals purely so they can be
        // reported before use — behaviour is unchanged. Two failure modes are visible here:
        // an INVALID action set, or a pose action that the resolved set does not contain (index -1),
        // either of which leaves the skeleton sitting in its bind pose.
        string actionSetSuffix = spawner.ActionSetSuffix;
        var tableauActionSet = MBGlobals.GetActionSetWithSuffix(baseMonsterFromRace, characterCode.IsFemale, actionSetSuffix);
        string poseActionName = spawner.PoseAction;
        var poseAction = ActionIndexCache.Create(poseActionName);

        Diagnostics.TableauDiagnostics.LogSpawnerResolution(
            characterCode.Race, characterCode.IsFemale, baseMonsterFromRace?.StringId,
            actionSetSuffix, tableauActionSet, poseActionName, poseAction.Index, idleStart.Index,
            spawner.AnimationProgress);

        agentVisuals = AgentVisuals.Create(new AgentVisualsData().Equipment(characterCode.CalculateEquipment()).BodyProperties(bodyProperties).Race(characterCode.Race)
            .Frame(spawnFrame)
            .Scale(1f)
            .SkeletonType(characterCode.IsFemale ? SkeletonType.Female : SkeletonType.Male)
            .Entity(agentEntity)
            .ActionSet(tableauActionSet)
            .ActionCode(in idleStart)
            .Scene(spawner.GameEntity.Scene)
            .Monster(baseMonsterFromRace)
            .PrepareImmediately(CreateFaceImmediately)
            .Banner(characterCode.Banner)
            .ClothColor1(spawner.ClothColor1)
            .ClothColor2(spawner.ClothColor2)
            .UseMorphAnims(useMorphAnims: true), "TableauCharacterAgentVisuals", isRandomProgress: false, needBatchedVersionForWeaponMeshes: false, forceUseFaceCache: false);

        agentVisuals.SetAction(poseAction, MBMath.ClampFloat(spawner.AnimationProgress, 0f, 1f));
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

        // Diagnostics 2026-07-31: the frame is REPLACED with Identity(+offsets) rather than derived
        // from spawnFrame, so a wrong rotation here would present as a character lying down. Report
        // the final frame so a broken launch can be compared against a good one.
        Diagnostics.TableauDiagnostics.Log($"spawn.frame.{raceName}",
            $"Spawner frame: race='{raceName}' configItem={(configitem == null ? "none" : $"h={configitem.Horizontal} z={configitem.Zoom} v={configitem.Vertical}")} " +
            $"origin=({frame.origin.x:F3},{frame.origin.y:F3},{frame.origin.z:F3}) " +
            $"rotF=({frame.rotation.f.x:F3},{frame.rotation.f.y:F3},{frame.rotation.f.z:F3}) " +
            $"rotU=({frame.rotation.u.x:F3},{frame.rotation.u.y:F3},{frame.rotation.u.z:F3})");

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
        Diagnostics.TableauDiagnostics.Log($"spawn.skeleton.{characterCode.Race}",
            $"Spawner skeleton stage: race={characterCode.Race} skeletonNull={skeleton == null} " +
            $"animationProgress={MBMath.ClampFloat(spawner.AnimationProgress, 0f, 1f):F3}");
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
            // NondeterministicRandomInt, not RandomInt(): MBRandom's default stream is
            // Game.Current.RandomGenerator, which is state on the saved Game root. This draw only
            // picks a mount mesh variant for a character-screen tableau, so spending a value from
            // the campaign's deterministic stream offsets every later campaign roll for no reason.
            // The engine ships a separate non-deterministic generator for exactly this case.
            MountVisualCreator.AddMountMeshToEntity(horse, mountItem, harnessItem, MountCreationKey.GetRandomMountKeyString(mountItem, MBRandom.NondeterministicRandomInt));

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
