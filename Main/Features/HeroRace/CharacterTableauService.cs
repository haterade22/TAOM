using TAOM.Core.Domain;
using TAOM.Features.HeroRace.Configuration;
using TAOM.Core.Infrastructure;
using System;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.Tableaus;
using static TAOM.Features.HeroRace.Configuration.RacePositionConfig;

namespace TAOM.Features.HeroRace;

public class CharacterTableauService : ICharacterTableauService
{
    private readonly IRaceManager _raceManager;
    private readonly RacePositionConfig _config;

    public CharacterTableauService(IRaceManager raceManager)
    {
        _raceManager = raceManager ?? throw new ArgumentNullException(nameof(raceManager));
        _config = RacePositionConfig.LoadConfig("CharacterAvatarPatch");
    }

    public void RefreshCharacterTableau(CharacterTableau tableau, Equipment oldEquipment = null)
    {
        // 1.3: _stanceIndex is CharacterViewModel.StanceTypes enum
        CharacterViewModel.StanceTypes stanceIndex = ReflectionHelper.GetFieldValue<CharacterTableau, CharacterViewModel.StanceTypes>(tableau, "_stanceIndex");
        UpdateMount(tableau, stanceIndex == CharacterViewModel.StanceTypes.OnMount);
        ReflectionHelper.CallPrivateMethod(tableau, "UpdateBannerItem", new Object[] { });

        AgentVisuals agentVisuals = ReflectionHelper.GetFieldValue<CharacterTableau, AgentVisuals>(tableau, "_agentVisuals");
        AgentVisuals mountVisuals = ReflectionHelper.GetFieldValue<CharacterTableau, AgentVisuals>(tableau, "_mountVisuals");
        AgentVisuals oldAgentVisuals = ReflectionHelper.GetFieldValue<CharacterTableau, AgentVisuals>(tableau, "_oldAgentVisuals");
        int agentVisualLoadingCounter = ReflectionHelper.GetFieldValue<CharacterTableau, int>(tableau, "_agentVisualLoadingCounter");
        MatrixFrame initialSpawnFrame = ReflectionHelper.GetFieldValue<CharacterTableau, MatrixFrame>(tableau, "_initialSpawnFrame");
        MatrixFrame characterMountPositionFrame = ReflectionHelper.GetFieldValue<CharacterTableau, MatrixFrame>(tableau, "_characterMountPositionFrame");
        bool isCharacterMountPlacesSwapped = ReflectionHelper.GetFieldValue<CharacterTableau, bool>(tableau, "_isCharacterMountPlacesSwapped");
        float mainCharacterRotation = ReflectionHelper.GetFieldValue<CharacterTableau, float>(tableau, "_mainCharacterRotation");
        MBActionSet characterActionSet = ReflectionHelper.GetFieldValue<CharacterTableau, MBActionSet>(tableau, "_characterActionSet");
        BodyProperties bodyProperties = ReflectionHelper.GetFieldValue<CharacterTableau, BodyProperties>(tableau, "_bodyProperties");
        bool isFemale = ReflectionHelper.GetFieldValue<CharacterTableau, bool>(tableau, "_isFemale");
        Equipment equipment = ReflectionHelper.GetFieldValue<CharacterTableau, Equipment>(tableau, "_equipment");
        Banner banner = ReflectionHelper.GetFieldValue<CharacterTableau, Banner>(tableau, "_banner");
        uint clothColor1 = ReflectionHelper.GetFieldValue<CharacterTableau, uint>(tableau, "_clothColor1");
        uint clothColor2 = ReflectionHelper.GetFieldValue<CharacterTableau, uint>(tableau, "_clothColor2");
        int initialLoadingCounter = ReflectionHelper.GetFieldValue<CharacterTableau, int>(tableau, "_initialLoadingCounter");
        float animationFrequencyThreshold = ReflectionHelper.GetFieldValue<CharacterTableau, float>(tableau, "_animationFrequencyThreshold");
        float animationGap = ReflectionHelper.GetFieldValue<CharacterTableau, float>(tableau, "_animationGap");
        bool isEquipmentAnimActive = ReflectionHelper.GetFieldValue<CharacterTableau, bool>(tableau, "_isEquipmentAnimActive");
        int race = ReflectionHelper.GetFieldValue<CharacterTableau, int>(tableau, "_race");

        if (mountVisuals == null && isCharacterMountPlacesSwapped)
        {
            isCharacterMountPlacesSwapped = false;
            mainCharacterRotation = 0f;
            ReflectionHelper.SetFieldValue(tableau, "_isCharacterMountPlacesSwapped", isCharacterMountPlacesSwapped);
            ReflectionHelper.SetFieldValue(tableau, "_mainCharacterRotation", mainCharacterRotation);
        }

        if (agentVisuals != null)
        {
            bool visibilityExcludeParents = oldAgentVisuals.GetEntity().GetVisibilityExcludeParents();
            AgentVisuals tempAgentVisuals = agentVisuals;
            agentVisuals = oldAgentVisuals;
            oldAgentVisuals = tempAgentVisuals;
            agentVisualLoadingCounter = 1;
            AgentVisualsData copyAgentVisualsData = agentVisuals.GetCopyAgentVisualsData();

            RacePositionConfigItem configitem = _config.Items.FirstOrDefault(item => item.Race == _raceManager.GetRaceNameFromId(race).ToLower());
            RacePositionConfigItem mountconfigitem = _config.Items.FirstOrDefault(item => item.Race == String.Concat("mount_", _raceManager.GetRaceNameFromId(race).ToLower()));

            MatrixFrame charframe = initialSpawnFrame;
            MatrixFrame mountframe = characterMountPositionFrame;

            if (configitem != null)
            {
                charframe = new MatrixFrame(initialSpawnFrame.rotation, initialSpawnFrame.origin);
                charframe.origin.y = charframe.origin.y + configitem.Horizontal;
                charframe.origin.z = charframe.origin.z + configitem.Vertical;
                charframe.origin.x = charframe.origin.x + configitem.Zoom;
            }

            if (mountconfigitem != null)
            {
                mountframe = new MatrixFrame(characterMountPositionFrame.rotation, characterMountPositionFrame.origin);
                mountframe.origin.y = mountframe.origin.y + mountconfigitem.Horizontal;
                mountframe.origin.z = mountframe.origin.z + mountconfigitem.Vertical;
                mountframe.origin.x = mountframe.origin.x + mountconfigitem.Zoom;
            }

            MatrixFrame frame = (isCharacterMountPlacesSwapped ? mountframe : charframe);
            if (!isCharacterMountPlacesSwapped)
            {
                frame.rotation.RotateAboutUp(mainCharacterRotation);
            }

            characterActionSet = MBGlobals.GetActionSetWithSuffix(copyAgentVisualsData.MonsterData, isFemale, "_warrior");
            copyAgentVisualsData.BodyProperties(bodyProperties).SkeletonType(isFemale ? SkeletonType.Female : SkeletonType.Male).Frame(frame)
                .ActionSet(characterActionSet)
                .Equipment(equipment)
                .Banner(banner)
                .UseMorphAnims(useMorphAnims: true)
                .ClothColor1(clothColor1)
                .ClothColor2(clothColor2)
                .Race(race);
            if (initialLoadingCounter > 0)
            {
                initialLoadingCounter--;
                ReflectionHelper.SetFieldValue(tableau, "_initialLoadingCounter", initialLoadingCounter);
            }

            agentVisuals.Refresh(needBatchedVersionForWeaponMeshes: false, copyAgentVisualsData);
            agentVisuals.SetVisible(value: false);

            if (initialLoadingCounter == 0)
            {
                oldAgentVisuals.SetVisible(visibilityExcludeParents);
            }

            if (oldEquipment != null && animationFrequencyThreshold <= animationGap && isEquipmentAnimActive)
            {
                // 1.3: act_inventory_glove_equip and act_inventory_cloth_equip are now
                // static fields on ActionIndexCache, not instance fields on CharacterTableau
                if (equipment[EquipmentIndex.Gloves].Item != null && oldEquipment[EquipmentIndex.Gloves].Item != equipment[EquipmentIndex.Gloves].Item)
                {
                    var gloveEquip = ActionIndexCache.act_inventory_glove_equip;
                    agentVisuals.GetVisuals().GetSkeleton().SetAgentActionChannel(0, gloveEquip);
                    ReflectionHelper.SetFieldValue(tableau, "_animationGap", 0f);
                }
                else if (equipment[EquipmentIndex.Body].Item != null && oldEquipment[EquipmentIndex.Body].Item != equipment[EquipmentIndex.Body].Item)
                {
                    var clothEquip = ActionIndexCache.act_inventory_cloth_equip;
                    agentVisuals.GetVisuals().GetSkeleton().SetAgentActionChannel(0, clothEquip);
                    ReflectionHelper.SetFieldValue(tableau, "_animationGap", 0f);
                }
            }

            agentVisuals.GetEntity().CheckResources(addToQueue: true, checkFaceResources: true);
        }

        ReflectionHelper.SetFieldValue(tableau, "_agentVisuals", agentVisuals);
        ReflectionHelper.SetFieldValue(tableau, "_mountVisuals", mountVisuals);
        ReflectionHelper.SetFieldValue(tableau, "_oldAgentVisuals", oldAgentVisuals);
        ReflectionHelper.SetFieldValue(tableau, "_characterActionSet", characterActionSet);

        ReflectionHelper.CallPrivateMethod(tableau, "AdjustCharacterForStanceIndex", new Object[] { });
    }

    private void UpdateMount(CharacterTableau tableau, bool isRiderAgentMounted = false)
    {
        AgentVisuals agentVisuals = ReflectionHelper.GetFieldValue<CharacterTableau, AgentVisuals>(tableau, "_agentVisuals");
        AgentVisuals oldMountVisuals = ReflectionHelper.GetFieldValue<CharacterTableau, AgentVisuals>(tableau, "_oldMountVisuals");
        AgentVisuals mountVisuals = ReflectionHelper.GetFieldValue<CharacterTableau, AgentVisuals>(tableau, "_mountVisuals");
        Equipment equipment = ReflectionHelper.GetFieldValue<CharacterTableau, Equipment>(tableau, "_equipment");
        bool isCharacterMountPlacesSwapped = ReflectionHelper.GetFieldValue<CharacterTableau, bool>(tableau, "_isCharacterMountPlacesSwapped");
        uint clothColor1 = ReflectionHelper.GetFieldValue<CharacterTableau, uint>(tableau, "_clothColor1");
        uint clothColor2 = ReflectionHelper.GetFieldValue<CharacterTableau, uint>(tableau, "_clothColor2");
        MatrixFrame mountSpawnPoint = ReflectionHelper.GetFieldValue<CharacterTableau, MatrixFrame>(tableau, "_mountSpawnPoint");
        MatrixFrame mountCharacterPositionFrame = ReflectionHelper.GetFieldValue<CharacterTableau, MatrixFrame>(tableau, "_mountCharacterPositionFrame");
        Banner banner = ReflectionHelper.GetFieldValue<CharacterTableau, Banner>(tableau, "_banner");
        float mainCharacterRotation = ReflectionHelper.GetFieldValue<CharacterTableau, float>(tableau, "_mainCharacterRotation");
        int mountVisualLoadingCounter = ReflectionHelper.GetFieldValue<CharacterTableau, int>(tableau, "_mountVisualLoadingCounter");
        Scene tableauScene = ReflectionHelper.GetFieldValue<CharacterTableau, Scene>(tableau, "_tableauScene");
        string mountCreationKey = ReflectionHelper.GetFieldValue<CharacterTableau, string>(tableau, "_mountCreationKey");
        int race = ReflectionHelper.GetFieldValue<CharacterTableau, int>(tableau, "_race");

        if (equipment[EquipmentIndex.ArmorItemEndSlot].Item?.HorseComponent != null)
        {
            ItemObject item = equipment[EquipmentIndex.ArmorItemEndSlot].Item;
            Monster monster = item.HorseComponent.Monster;
            Equipment mountEquipment = new Equipment
            {
                [EquipmentIndex.ArmorItemEndSlot] = equipment[EquipmentIndex.ArmorItemEndSlot],
                [EquipmentIndex.HorseHarness] = equipment[EquipmentIndex.HorseHarness]
            };

            RacePositionConfigItem configitem = _config.Items.FirstOrDefault(item => item.Race == _raceManager.GetRaceNameFromId(race).ToLower());
            RacePositionConfigItem mountconfigitem = _config.Items.FirstOrDefault(item => item.Race == String.Concat("mount_", _raceManager.GetRaceNameFromId(race).ToLower()));

            MatrixFrame charframe = mountCharacterPositionFrame;
            MatrixFrame mountframe = mountSpawnPoint;

            if (configitem != null)
            {
                charframe = new MatrixFrame(mountCharacterPositionFrame.rotation, mountCharacterPositionFrame.origin);
                charframe.origin.y = charframe.origin.y + configitem.Horizontal;
                charframe.origin.z = charframe.origin.z + configitem.Vertical;
                charframe.origin.x = charframe.origin.x + configitem.Zoom;
            }

            if (mountconfigitem != null)
            {
                mountframe = new MatrixFrame(mountSpawnPoint.rotation, mountSpawnPoint.origin);
                mountframe.origin.y = mountframe.origin.y + mountconfigitem.Horizontal;
                mountframe.origin.z = mountframe.origin.z + mountconfigitem.Vertical;
                mountframe.origin.x = mountframe.origin.x + mountconfigitem.Zoom;
            }

            MatrixFrame frame = (isCharacterMountPlacesSwapped ? charframe : mountframe);
            if (isCharacterMountPlacesSwapped)
            {
                frame.rotation.RotateAboutUp(mainCharacterRotation);
            }

            if (oldMountVisuals != null)
            {
                oldMountVisuals.ResetNextFrame();
            }

            oldMountVisuals = mountVisuals;
            mountVisualLoadingCounter = 3;
            AgentVisualsData agentVisualsData = new AgentVisualsData();

            // 1.3: act_camel_stand/act_horse_stand replaced with ActionIndexCache.act_inventory_idle_start
            var idleAction = (ActionIndexCache)ReflectionHelper.CallPrivateMethod(tableau, "GetIdleAction", new Object[] { });
            var mountIdleAction = isRiderAgentMounted ? ActionIndexCache.act_inventory_idle_start : idleAction;

            agentVisualsData.Banner(banner).Equipment(mountEquipment).Frame(frame)
                .Scale(item.ScaleFactor)
                .ActionSet(MBGlobals.GetActionSet(monster.ActionSetCode))
                .ActionCode(in mountIdleAction)
                .Scene(tableauScene)
                .Monster(monster)
                .PrepareImmediately(prepareImmediately: false)
                .ClothColor1(clothColor1)
                .ClothColor2(clothColor2)
                .MountCreationKey(mountCreationKey);
            mountVisuals = AgentVisuals.Create(agentVisualsData, "MountTableau", isRandomProgress: false, needBatchedVersionForWeaponMeshes: false, forceUseFaceCache: false);
            mountVisuals.SetAgentLodZeroOrMaxExternal(makeZero: true);
            mountVisuals.SetVisible(value: false);
            mountVisuals.GetEntity().CheckResources(addToQueue: true, checkFaceResources: true);
        }
        else if (mountVisuals != null)
        {
            mountVisuals.Reset();
            mountVisuals = null;
            mountVisualLoadingCounter = 0;
        }

        ReflectionHelper.SetFieldValue(tableau, "_mountVisualLoadingCounter", mountVisualLoadingCounter);
        ReflectionHelper.SetFieldValue(tableau, "_mountVisuals", mountVisuals);
        ReflectionHelper.SetFieldValue(tableau, "_oldMountVisuals", oldMountVisuals);
    }
}
