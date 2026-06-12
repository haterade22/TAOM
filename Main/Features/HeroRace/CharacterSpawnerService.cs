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

    // TEMP-DIAG (spider-mount tableau AV, 2026-06-10): one-shot probe battery, fired the
    // first time a spider mount reaches the tableau spawner this process. Each probe is a
    // fresh entity + isolated catch (a caught AV provably does not poison later spawns —
    // 3 warg mounts succeeded right after the spider AV in the 20:53 session log).
    // Interpretation: T1 AV + T3 AV -> the Kit-compiled spider_skeleton resource is the
    // poison. T1 AV + T3 OK -> the as_spider/spider-usage native data is the poison.
    // T1 OK + T2 AV -> setting the (unbound) canter pose channel is the poison. All OK ->
    // the poison needs AddMountMeshToEntity (probes omit it; the main path includes it).
    private static bool _spiderDiagRan;

    [HandleProcessCorruptedStateExceptions]
    private void RunSpiderMountDiagnostics(CharacterSpawner spawner, Monster spiderMonster)
    {
        try
        {
            MBActionSet asSpider = MBActionSet.GetActionSet("as_spider");
            MBActionSet asWarg = MBActionSet.GetActionSet("as_warg");
            ActionIndexCache canter = ActionIndexCache.Create("act_horse_forward_canter");
            string spiderCanterAnim = asSpider.IsValid ? asSpider.GetAnimationName(in canter) : "<set invalid>";
            string wargCanterAnim = asWarg.IsValid ? asWarg.GetAnimationName(in canter) : "<set invalid>";
            _logger.LogInfo($"[SpiderDiag] canter binding: as_spider='{spiderCanterAnim}' as_warg='{wargCanterAnim}'");

            // Which of the 2026-06-11 typed mount-verb/fall bindings did the ENGINE actually compile?
            // (the 15:19 battle warned 'as_spider does not contain act_spider_strike_back' although the
            // XML on disk binds it -- split file-truth from engine-truth.)
            if (asSpider.IsValid)
            {
                foreach (string code in new[]
                         {
                             "act_spider_strike_back", "act_spider_strike_front", "act_spider_rear",
                             "act_spider_rear_damaged", "act_spider_fall_roll", "act_spider_fall_roll_continue",
                             "act_spider_attack_back", "act_spider_idle",
                         })
                {
                    ActionIndexCache ac = ActionIndexCache.Create(code);
                    _logger.LogInfo($"[SpiderDiag] engine binding {code} -> '{asSpider.GetAnimationName(in ac)}'");
                }
                MBActionSet asHuman = MBActionSet.GetActionSet("as_human_warrior");
                ActionIndexCache rocky = ActionIndexCache.Create("act_spider_fall_roll");
                _logger.LogInfo($"[SpiderDiag] rider partial: as_human_warrior x act_spider_fall_roll -> '{(asHuman.IsValid ? asHuman.GetAnimationName(in rocky) : "<set invalid>")}'");

                // The actual riders are GOBLINS (as_goblin_warrior, base_set=as_human_warrior).
                // base-set inheritance snapshots at definition time, so the rider partial must
                // load FIRST or goblins never see the spider bindings (the rider-death AV +
                // thrust-loop suspect). act_warg_forward_walk = the warg-inheritance control.
                MBActionSet asGoblin = MBActionSet.GetActionSet("as_goblin_warrior");
                if (asGoblin.IsValid)
                {
                    foreach (string code in new[] { "act_spider_walk_forward", "act_spider_fall_roll", "act_spider_run_forward", "act_warg_forward_walk" })
                    {
                        ActionIndexCache gc = ActionIndexCache.Create(code);
                        _logger.LogInfo($"[SpiderDiag] GOBLIN rider set: {code} -> '{asGoblin.GetAnimationName(in gc)}'");
                    }
                }
            }
            _logger.LogInfo($"[SpiderDiag] usage indices: spider={Agent.GetMonsterUsageIndex("spider")} warg={Agent.GetMonsterUsageIndex("warg")} elephant={Agent.GetMonsterUsageIndex("elephant")} human={Agent.GetMonsterUsageIndex("human")}");

            TickProbe("T1 spider_skeleton + as_spider/spider, no pose", "spider_skeleton", asSpider, "spider", spiderMonster, setPoseChannel: false, spawner);
            TickProbe("T2 spider_skeleton + as_spider/spider, canter pose", "spider_skeleton", asSpider, "spider", spiderMonster, setPoseChannel: true, spawner);
            if (asWarg.IsValid)
            {
                TickProbe("T3 spider_skeleton + as_warg/warg, no pose", "spider_skeleton", asWarg, "warg", spiderMonster, setPoseChannel: false, spawner);
                TickProbe("T4 spider_skeleton + as_warg/warg, canter pose", "spider_skeleton", asWarg, "warg", spiderMonster, setPoseChannel: true, spawner);
                // Cross probes: split the failing (set x usage) pair into halves.
                // T5 OK + T6 AV  -> the "spider" usage-set native data is the poison.
                // T5 AV + T6 OK  -> as_spider itself is the poison.
                TickProbe("T5 CROSS spider_skeleton + as_spider/WARG-usage, no pose", "spider_skeleton", asSpider, "warg", spiderMonster, setPoseChannel: false, spawner);
                TickProbe("T6 CROSS spider_skeleton + as_warg/SPIDER-usage, no pose", "spider_skeleton", asWarg, "spider", spiderMonster, setPoseChannel: false, spawner);
            }
            _logger.LogInfo("[SpiderDiag] battery complete");
        }
        catch (Exception ex)
        {
            _logger.LogError($"[SpiderDiag] battery itself failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    [HandleProcessCorruptedStateExceptions]
    private void TickProbe(string label, string skeletonName, MBActionSet actionSet, string usageName, Monster monster, bool setPoseChannel, CharacterSpawner spawner)
    {
        GameEntity probe = null;
        string step = "CreateEmpty";
        try
        {
            probe = GameEntity.CreateEmpty(spawner.GameEntity.Scene, isModifiableFromEditor: false);
            probe.Name = "SpiderDiagProbe";
            step = "CreateAgentSkeleton";
            probe.CreateAgentSkeleton(skeletonName, isHumanoid: false, actionSet, usageName, monster);
            step = "CopyComponentsToSkeleton";
            probe.CopyComponentsToSkeleton();
            if (setPoseChannel)
            {
                step = "SetAgentActionChannel";
                ActionIndexCache pose = ActionIndexCache.Create(spawner.PoseActionForHorse);
                probe.Skeleton.SetAgentActionChannel(0, in pose, 0f);
            }
            step = "TickAnimations";
            AgentVisuals visuals = ReflectionHelper.GetFieldValue<CharacterSpawner, AgentVisuals>(spawner, "_agentVisuals");
            probe.Skeleton.TickAnimations(0.01f, visuals.GetVisuals().GetGlobalFrame(), tickAnimsForChildren: true);
            _logger.LogInfo($"[SpiderDiag] {label}: OK");
        }
        catch (Exception ex)
        {
            _logger.LogError($"[SpiderDiag] {label}: FAILED at '{step}' with {ex.GetType().Name}");
        }
        finally
        {
            try
            {
                if (probe != null)
                {
                    spawner.GameEntity.Scene?.RemoveEntity(probe, 98);
                }
            }
            catch
            {
                // probe entity may be half-built
            }
        }
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

            if (monster.StringId == "spider" && !_spiderDiagRan)
            {
                _spiderDiagRan = true;
                RunSpiderMountDiagnostics(spawner, monster);
            }

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
