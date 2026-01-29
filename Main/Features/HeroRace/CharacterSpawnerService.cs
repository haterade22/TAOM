using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Features.HeroRace.Configuration;
using TAOM.Core.Infrastructure;
using System;
using System.Collections.Generic;
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
    private readonly RacePositionConfig _config;
    private readonly Dictionary<string, RacePositionConfigItem> _configLookup;

    public CharacterSpawnerService(IRaceManager raceManager, IFaceGenAdapter faceGenAdapter)
    {
        _raceManager = raceManager ?? throw new ArgumentNullException(nameof(raceManager));
        _faceGenAdapter = faceGenAdapter ?? throw new ArgumentNullException(nameof(faceGenAdapter));
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

        // 1.3: ActionCode takes in ActionIndexCache
        var idleStart = ActionIndexCache.Create("act_inventory_idle_start");
        agentVisuals = AgentVisuals.Create(new AgentVisualsData().Equipment(characterCode.CalculateEquipment()).BodyProperties(bodyProperties).Race(characterCode.Race)
            .Frame(spawnFrame)
            .Scale(1f)
            .SkeletonType(characterCode.IsFemale ? SkeletonType.Female : SkeletonType.Male)
            .Entity(agentEntity)
            .ActionSet(MBGlobals.GetActionSetWithSuffix(baseMonsterFromRace, characterCode.IsFemale, spawner.ActionSetSuffix))
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
            ReflectionHelper.SetFieldValue(spawner, "_horseEntity", horseEntity);
            ReflectionHelper.CallPrivateMethod(spawner, "SpawnMount", new Object[] { characterCode });
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
}
