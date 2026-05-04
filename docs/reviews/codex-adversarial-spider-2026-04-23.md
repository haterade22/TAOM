# Codex Adversarial Review: Spider Feature (2026-04-23)

Read-only review of `Main/Features/Spider/`, its wiring, XML anchor data, tests, and external LOTRLOME_Armory data. Installed DLLs were decompiled with `ilspycmd` from `E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/` (not the v1.4 decompiled tree) except `BehaviorTreeWrapper.dll`, which is bundled in TAOM.

## 1. VANILLA / WRAPPER CODE

### `TaleWorlds.MountAndBlade.Mission.SpawnAgent(AgentBuildData, bool)`

```csharp
public Agent SpawnAgent(AgentBuildData agentBuildData, bool spawnFromAgentVisuals = false)
	{
		Scene.WaitWaterRendererCPUSimulation();
		BasicCharacterObject agentCharacter = agentBuildData.AgentCharacter;
		if (agentCharacter == null)
		{
			throw new MBNullParameterException("npcCharacterObject");
		}
		int forcedAgentIndex = -1;
		if (agentBuildData.AgentIndexOverriden)
		{
			forcedAgentIndex = agentBuildData.AgentIndex;
		}
		Agent agent = CreateAgent(agentBuildData.AgentMonster, agentBuildData.GenderOverriden ? agentBuildData.AgentIsFemale : agentCharacter.IsFemale, 0, Agent.CreationType.FromCharacterObj, agentCharacter.GetStepSize(), forcedAgentIndex, agentBuildData.AgentMonster.Weight, agentCharacter);
		agent.FormationPositionPreference = agentCharacter.FormationPositionPreference;
		float num = (agentBuildData.AgeOverriden ? ((float)agentBuildData.AgentAge) : agentCharacter.Age);
		if (num == 0f)
		{
			agentBuildData.Age(29);
		}
		else if (MBBodyProperties.GetMaturityType(num) < BodyMeshMaturityType.Teenager && (Mode == MissionMode.Battle || Mode == MissionMode.Duel || Mode == MissionMode.Tournament || Mode == MissionMode.Stealth))
		{
			agentBuildData.Age(27);
		}
		if (agentBuildData.BodyPropertiesOverriden)
		{
			agent.UpdateBodyProperties(agentBuildData.AgentBodyProperties);
			if (!agentBuildData.AgeOverriden)
			{
				agent.Age = agentCharacter.Age;
			}
		}
		agent.BodyPropertiesSeed = agentBuildData.AgentEquipmentSeed;
		if (agentBuildData.AgeOverriden)
		{
			agent.Age = agentBuildData.AgentAge;
		}
		if (agentBuildData.GenderOverriden)
		{
			agent.IsFemale = agentBuildData.AgentIsFemale;
		}
		agent.SetTeam(agentBuildData.AgentTeam, sync: false);
		agent.SetClothingColor1(agentBuildData.AgentClothingColor1);
		agent.SetClothingColor2(agentBuildData.AgentClothingColor2);
		agent.SetRandomizeColors(agentBuildData.RandomizeColors);
		agent.Origin = agentBuildData.AgentOrigin;
		Formation agentFormation = agentBuildData.AgentFormation;
		if (agentFormation != null && !agentFormation.HasBeenPositioned)
		{
			if (_deploymentPlan.IsPlanMade(agentFormation.Team))
			{
				SetFormationPositioningFromDeploymentPlan(agentFormation);
			}
			else
			{
				WorldPosition value = new WorldPosition(Scene.Pointer, UIntPtr.Zero, agentBuildData.AgentInitialPosition.Value, hasValidZ: false);
				agentFormation.SetPositioning(value);
			}
		}
		if (!agentBuildData.AgentInitialPosition.HasValue)
		{
			Team agentTeam = agentBuildData.AgentTeam;
			BattleSideEnum side = agentBuildData.AgentTeam.Side;
			Vec3 troopSpawnPosition = Vec3.Invalid;
			Vec2 spawnDirection = Vec2.Invalid;
			if (agentCharacter == Game.Current.PlayerTroop && _deploymentPlan.HasPlayerSpawnFrame(side))
			{
				_deploymentPlan.GetPlayerSpawnFrame(side, out var position, out var direction);
				troopSpawnPosition = position.GetGroundVec3();
				spawnDirection = direction;
			}
			else if (agentFormation != null)
			{
				int num2;
				int num3;
				if (agentBuildData.AgentSpawnsIntoOwnFormation)
				{
					num2 = agentFormation.CountOfUnits;
					num3 = num2 + 1;
				}
				else if (agentBuildData.AgentFormationTroopSpawnIndex >= 0 && agentBuildData.AgentFormationTroopSpawnCount > 0)
				{
					num2 = agentBuildData.AgentFormationTroopSpawnIndex;
					num3 = agentBuildData.AgentFormationTroopSpawnCount;
				}
				else
				{
					num2 = agentFormation.GetNextSpawnIndex();
					num3 = num2 + 1;
				}
				if (num2 >= num3)
				{
					num3 = num2 + 1;
				}
				GetTroopSpawnFrameWithIndex(agentBuildData, num2, num3, out troopSpawnPosition, out spawnDirection);
			}
			else
			{
				GetFormationSpawnFrame(agentTeam, FormationClass.NumberOfAllFormations, agentBuildData.AgentIsReinforcement, out var spawnPosition, out spawnDirection);
				troopSpawnPosition = spawnPosition.GetGroundVec3();
			}
			agentBuildData.InitialPosition(in troopSpawnPosition).InitialDirection(in spawnDirection);
		}
		agent.SetInitialFrame(agentBuildData.AgentInitialPosition.GetValueOrDefault(), agentBuildData.AgentInitialDirection.GetValueOrDefault(), agentBuildData.AgentCanSpawnOutsideOfMissionBoundary);
		if (agentCharacter.BattleEquipments == null && agentCharacter.CivilianEquipments == null)
		{
			TaleWorlds.Library.Debug.Print("characterObject.AllEquipments is null for \"" + agentCharacter.StringId + "\".");
		}
		if (agentCharacter.BattleEquipments != null && agentCharacter.BattleEquipments.Any((Equipment eq) => eq == null) && agentCharacter.CivilianEquipments != null && agentCharacter.CivilianEquipments.Any((Equipment eq) => eq == null))
		{
			TaleWorlds.Library.Debug.Print("Character with id \"" + agentCharacter.StringId + "\" has a null equipment in its AllEquipments.");
		}
		if (agentCharacter.CivilianEquipments == null)
		{
			agentBuildData.CivilianEquipment(civilianEquipment: false);
		}
		if (agentCharacter.IsHero)
		{
			agentBuildData.FixedEquipment(fixedEquipment: true);
		}
		Equipment equipment = ((agentBuildData.AgentOverridenSpawnEquipment != null) ? agentBuildData.AgentOverridenSpawnEquipment.Clone() : ((!agentBuildData.AgentFixedEquipment) ? Equipment.GetRandomEquipmentElements(agent.Character, !Game.Current.GameType.IsCoreOnlyGameMode, agentBuildData.AgentCivilianEquipment ? Equipment.EquipmentType.Civilian : Equipment.EquipmentType.Battle, agentBuildData.AgentEquipmentSeed) : ((!agentBuildData.AgentCivilianEquipment) ? agentCharacter.FirstBattleEquipment.Clone() : agentCharacter.FirstCivilianEquipment.Clone())));
		Agent agent2 = null;
		if (agentBuildData.AgentNoHorses)
		{
			equipment[EquipmentIndex.ArmorItemEndSlot] = default(EquipmentElement);
			equipment[EquipmentIndex.HorseHarness] = default(EquipmentElement);
		}
		if (agentBuildData.AgentNoWeapons)
		{
			equipment[EquipmentIndex.WeaponItemBeginSlot] = default(EquipmentElement);
			equipment[EquipmentIndex.Weapon1] = default(EquipmentElement);
			equipment[EquipmentIndex.Weapon2] = default(EquipmentElement);
			equipment[EquipmentIndex.Weapon3] = default(EquipmentElement);
			equipment[EquipmentIndex.ExtraWeaponSlot] = default(EquipmentElement);
		}
		if (agentCharacter.IsHero)
		{
			ItemObject itemObject = null;
			ItemObject item = equipment[EquipmentIndex.ExtraWeaponSlot].Item;
			if (item != null && item.IsBannerItem && item.BannerComponent != null)
			{
				itemObject = item;
				equipment[EquipmentIndex.ExtraWeaponSlot] = default(EquipmentElement);
			}
			else if (agentBuildData.AgentBannerItem != null)
			{
				itemObject = agentBuildData.AgentBannerItem;
			}
			if (itemObject != null)
			{
				agent.SetFormationBanner(itemObject);
			}
		}
		else if (agentBuildData.AgentBannerItem != null)
		{
			equipment[EquipmentIndex.Weapon1] = default(EquipmentElement);
			equipment[EquipmentIndex.Weapon2] = default(EquipmentElement);
			equipment[EquipmentIndex.Weapon3] = default(EquipmentElement);
			if (agentBuildData.AgentBannerReplacementWeaponItem != null)
			{
				equipment[EquipmentIndex.WeaponItemBeginSlot] = new EquipmentElement(agentBuildData.AgentBannerReplacementWeaponItem);
			}
			else
			{
				equipment[EquipmentIndex.WeaponItemBeginSlot] = default(EquipmentElement);
			}
			equipment[EquipmentIndex.ExtraWeaponSlot] = new EquipmentElement(agentBuildData.AgentBannerItem);
			if (agentBuildData.AgentOverridenSpawnMissionEquipment != null)
			{
				agentBuildData.AgentOverridenSpawnMissionEquipment[EquipmentIndex.ExtraWeaponSlot] = new MissionWeapon(agentBuildData.AgentBannerItem, null, agentBuildData.AgentBanner);
			}
		}
		if (agentBuildData.AgentNoArmor)
		{
			equipment[EquipmentIndex.Gloves] = default(EquipmentElement);
			equipment[EquipmentIndex.Body] = default(EquipmentElement);
			equipment[EquipmentIndex.Cape] = default(EquipmentElement);
			equipment[EquipmentIndex.NumAllWeaponSlots] = default(EquipmentElement);
			equipment[EquipmentIndex.Leg] = default(EquipmentElement);
		}
		for (int num4 = 0; num4 < 5; num4++)
		{
			if (!equipment[(EquipmentIndex)num4].IsEmpty && equipment[(EquipmentIndex)num4].Item.ItemFlags.HasAnyFlag(ItemFlags.CannotBePickedUp))
			{
				equipment[(EquipmentIndex)num4] = default(EquipmentElement);
			}
		}
		agent.InitializeSpawnEquipment(equipment);
		agent.InitializeMissionEquipment(agentBuildData.AgentOverridenSpawnMissionEquipment, agentBuildData.AgentBanner);
		if (agent.RandomizeColors)
		{
			agent.Equipment.SetGlossMultipliersOfWeaponsRandomly(agentBuildData.AgentEquipmentSeed);
		}
		ItemObject item2 = equipment[EquipmentIndex.ArmorItemEndSlot].Item;
		if (item2 != null && item2.HasHorseComponent && item2.HorseComponent.IsRideable)
		{
			int forcedAgentMountIndex = -1;
			if (agentBuildData.AgentMountIndexOverriden)
			{
				forcedAgentMountIndex = agentBuildData.AgentMountIndex;
			}
			agent2 = CreateHorseAgentFromRosterElements(equipment[EquipmentIndex.ArmorItemEndSlot], equipment[EquipmentIndex.HorseHarness], agentBuildData.AgentInitialPosition.GetValueOrDefault(), agentBuildData.AgentInitialDirection.GetValueOrDefault(), forcedAgentMountIndex, agentBuildData.AgentMountKey);
			Equipment spawnEquipment = new Equipment
			{
				[EquipmentIndex.ArmorItemEndSlot] = equipment[EquipmentIndex.ArmorItemEndSlot],
				[EquipmentIndex.HorseHarness] = equipment[EquipmentIndex.HorseHarness]
			};
			agent2.InitializeSpawnEquipment(spawnEquipment);
			agent.SetMountAgentBeforeBuild(agent2);
		}
		if (spawnFromAgentVisuals || !GameNetwork.IsClientOrReplay)
		{
			agent.Equipment.CheckLoadedAmmos();
		}
		if (!agentBuildData.BodyPropertiesOverriden)
		{
			BodyProperties bodyProperties;
			if (this.OnComputeTroopBodyProperties != null)
			{
				bodyProperties = this.OnComputeTroopBodyProperties(agentBuildData, agentCharacter, equipment, agentBuildData.AgentEquipmentSeed);
				agentBuildData.UseFaceCache = !agentCharacter.IsHero;
			}
			else
			{
				bodyProperties = agentCharacter.GetBodyProperties(equipment, agentBuildData.AgentEquipmentSeed);
			}
			agent.UpdateBodyProperties(bodyProperties);
		}
		if (GameNetwork.IsServerOrRecorder && agent.RiderAgent == null)
		{
			Vec3 valueOrDefault = agentBuildData.AgentInitialPosition.GetValueOrDefault();
			Vec2 valueOrDefault2 = agentBuildData.AgentInitialDirection.GetValueOrDefault();
			if (agent.IsMount)
			{
				GameNetwork.BeginBroadcastModuleEvent();
				GameNetwork.WriteMessage(new CreateFreeMountAgent(agent, valueOrDefault, valueOrDefault2));
				GameNetwork.EndBroadcastModuleEvent(GameNetwork.EventBroadcastFlags.AddToMissionRecord);
			}
			else
			{
				bool flag = agentBuildData.AgentMissionPeer != null;
				NetworkCommunicator peer = (flag ? agentBuildData.AgentMissionPeer.GetNetworkPeer() : agentBuildData.OwningAgentMissionPeer?.GetNetworkPeer());
				bool flag2 = agent.MountAgent != null && agent.MountAgent.RiderAgent == agent;
				GameNetwork.BeginBroadcastModuleEvent();
				GameNetwork.WriteMessage(new CreateAgent(agent.Index, agent.Character, agent.Monster, agent.SpawnEquipment, agent.Equipment, agent.BodyPropertiesValue, agent.BodyPropertiesSeed, agent.IsFemale, agent.Team?.TeamIndex ?? (-1), agent.Formation?.Index ?? (-1), agent.ClothingColor1, agent.ClothingColor2, flag2 ? agent.MountAgent.Index : (-1), agent.MountAgent?.SpawnEquipment, flag, valueOrDefault, valueOrDefault2, peer));
				GameNetwork.EndBroadcastModuleEvent(GameNetwork.EventBroadcastFlags.AddToMissionRecord);
			}
		}
		MultiplayerMissionAgentVisualSpawnComponent missionBehavior = GetMissionBehavior<MultiplayerMissionAgentVisualSpawnComponent>();
		if (missionBehavior != null && agentBuildData.AgentMissionPeer != null && agentBuildData.AgentMissionPeer.IsMine && agentBuildData.AgentVisualsIndex == 0)
		{
			missionBehavior.OnMyAgentSpawned();
		}
		if (agent2 != null)
		{
			BuildAgent(agent2, agentBuildData);
			foreach (MissionBehavior missionBehavior2 in MissionBehaviors)
			{
				missionBehavior2.OnAgentBuild(agent2, null);
			}
		}
		BuildAgent(agent, agentBuildData);
		if (agentBuildData.AgentMissionPeer != null)
		{
			agent.MissionPeer = agentBuildData.AgentMissionPeer;
		}
		if (agentBuildData.OwningAgentMissionPeer != null)
		{
			agent.SetOwningAgentMissionPeer(agentBuildData.OwningAgentMissionPeer);
		}
		foreach (MissionBehavior missionBehavior3 in MissionBehaviors)
		{
			missionBehavior3.OnAgentBuild(agent, agentBuildData.AgentBanner ?? agentBuildData.AgentTeam?.Banner);
		}
		agent.AgentVisuals.CheckResources(addToQueue: true);
		if (agent.IsAIControlled)
		{
			if (agent2 == null)
			{
				AgentFlag agentFlags = (AgentFlag)((uint)agent.GetAgentFlags() & 0xFFFFDFFFu);
				agent.SetAgentFlags(agentFlags);
			}
			else if (agent.Formation == null)
			{
				agent.SetRidingOrder(RidingOrder.RidingOrderEnum.Mount);
			}
		}
		Mission current = Current;
		if (current != null && current.IsDeploymentFinished)
		{
			MissionGameModels.Current.AgentStatCalculateModel.InitializeAgentStatsAfterDeploymentFinished(agent);
			MissionGameModels.Current.AgentStatCalculateModel.InitializeMissionEquipmentAfterDeploymentFinished(agent);
		}
		return agent;
	}
```

**Conclusion:** `SpawnAgent` creates the agent with `agentBuildData.AgentMonster` directly: `CreateAgent(agentBuildData.AgentMonster, ...)`. It does not re-derive the monster from the `BasicCharacterObject` after `.Monster(spiderMonster)`.

### `TaleWorlds.MountAndBlade.AgentBuildData.AgentMonster` getter

```csharp
public Monster AgentMonster => AgentData.AgentMonster;
```

Supporting `AgentData` constructor fallback:

```csharp
public AgentData(BasicCharacterObject characterObject)
	{
		AgentCharacter = characterObject;
		AgentRace = characterObject.Race;
		AgentMonster = FaceGen.GetBaseMonsterFromRace(AgentRace);
		AgentOwnerParty = null;
		AgentOverridenEquipment = null;
		AgentEquipmentSeed = 0;
		AgentNoHorses = false;
		AgentNoWeapons = false;
		AgentNoArmor = false;
		AgentFixedEquipment = false;
		AgentCivilianEquipment = false;
		AgentClothingColor1 = uint.MaxValue;
		AgentClothingColor2 = uint.MaxValue;
		BodyPropertiesOverriden = false;
		GenderOverriden = false;
	}
```

**Conclusion:** the constructor initially derives the base monster from race, but the build data getter reads `AgentData.AgentMonster`, which can be overwritten before spawn.

### `TaleWorlds.MountAndBlade.AgentBuildData.Monster(Monster)` setter

```csharp
public AgentBuildData Monster(Monster monster)
	{
		AgentData.Monster(monster);
		return this;
	}
```

Supporting `AgentData.Monster(Monster)`:

```csharp
public AgentData Monster(Monster monster)
	{
		AgentMonster = monster;
		return this;
	}
```

**Conclusion:** `.Monster(spiderMonster)` stores the override; it is not ignored.

### `TaleWorlds.MountAndBlade.CustomBattleAgentLogic`

```csharp
using TaleWorlds.Core;

namespace TaleWorlds.MountAndBlade;

public class CustomBattleAgentLogic : MissionLogic
{
	public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon, in Blow blow, in AttackCollisionData attackCollisionData)
	{
		if (affectedAgent.Character != null && affectorAgent?.Character != null && affectedAgent.State == AgentState.Active)
		{
			bool isFatal = affectedAgent.Health - (float)blow.InflictedDamage < 1f;
			bool isTeamKill = affectedAgent.Team.Side == affectorAgent.Team.Side;
			affectorAgent.Origin.OnScoreHit(affectedAgent.Character, affectorAgent.Formation?.Captain?.Character, blow.InflictedDamage, isFatal, isTeamKill, affectorWeapon.CurrentUsageItem);
		}
	}

	public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow killingBlow)
	{
		if ((affectorAgent == null && affectedAgent.IsMount && agentState == AgentState.Routed) || affectedAgent.Origin == null)
		{
			return;
		}
		switch (agentState)
		{
		case AgentState.Unconscious:
			affectedAgent.Origin.SetWounded();
			if (affectedAgent == base.Mission.MainAgent)
			{
				BecomeGhost();
			}
			break;
		case AgentState.Killed:
			affectedAgent.Origin.SetKilled();
			break;
		default:
			affectedAgent.Origin.SetRouted(isOrderRetreat: false);
			break;
		}
	}

	private void BecomeGhost()
	{
		Agent leader = base.Mission.PlayerEnemyTeam.Leader;
		if (leader != null && leader.IsActive())
		{
			leader.Controller = AgentControllerType.AI;
		}
		Agent mainAgent = base.Mission.MainAgent;
		if (mainAgent != null && mainAgent.IsActive())
		{
			base.Mission.MainAgent.Controller = AgentControllerType.AI;
		}
	}
}

```

`CustomBattleAgentLogic` is added by installed `BannerlordMissions` only in CustomBattle entry points:

```csharp
public static Mission OpenCustomBattleMission(string scene, BasicCharacterObject playerCharacter, CustomBattleCombatant playerParty, CustomBattleCombatant enemyParty, bool isPlayerGeneral, BasicCharacterObject playerSideGeneralCharacter, string sceneLevels = "", string seasonString = "", float timeOfDay = 6f)
	{
		BattleSideEnum playerSide = playerParty.Side;
		bool isPlayerAttacker = playerSide == BattleSideEnum.Attacker;
		IMissionTroopSupplier[] troopSuppliers = new IMissionTroopSupplier[2];
		CustomBattleTroopSupplier customBattleTroopSupplier = new CustomBattleTroopSupplier(playerParty, isPlayerSide: true, isPlayerGeneral, isSallyOut: false);
		troopSuppliers[(int)playerParty.Side] = customBattleTroopSupplier;
		CustomBattleTroopSupplier customBattleTroopSupplier2 = new CustomBattleTroopSupplier(enemyParty, isPlayerSide: false, isPlayerGeneral: false, isSallyOut: false);
		troopSuppliers[(int)enemyParty.Side] = customBattleTroopSupplier2;
		bool isPlayerSergeant = !isPlayerGeneral;
		Mission mission = MissionState.OpenNew("CustomBattle", new MissionInitializerRecord(scene)
		{
			DoNotUseLoadingScreen = false,
			PlayingInCampaignMode = false,
			AtmosphereOnCampaign = CreateAtmosphereInfoForMission(seasonString, (int)timeOfDay),
			SceneLevels = sceneLevels,
			DecalAtlasGroup = 2
		}, (Mission missionController) => new MissionBehavior[26]
		{
			new MissionAgentSpawnLogic(troopSuppliers, playerSide, Mission.BattleSizeType.Battle),
			new BattlePowerCalculationLogic(),
			new CustomBattleAgentLogic(),
			new BannerBearerLogic(),
			new CustomBattleMissionSpawnHandler((!isPlayerAttacker) ? playerParty : enemyParty, isPlayerAttacker ? playerParty : enemyParty),
			new MissionOptionsComponent(),
			new BattleEndLogic(),
			new BattleReinforcementsSpawnController(),
			new MissionCombatantsLogic(null, playerParty, (!isPlayerAttacker) ? playerParty : enemyParty, isPlayerAttacker ? playerParty : enemyParty, Mission.MissionTeamAITypeEnum.FieldBattle, isPlayerSergeant),
			new BattleObserverMissionLogic(),
			new AgentHumanAILogic(),
			new AgentVictoryLogic(),
			new MissionAgentPanicHandler(),
			new BattleMissionAgentInteractionLogic(),
			new AgentMoraleInteractionLogic(),
			new AssignPlayerRoleInTeamMissionController(isPlayerGeneral, isPlayerSergeant, isPlayerInArmy: false, isPlayerSergeant ? Enumerable.Repeat(playerCharacter.StringId, 1).ToList() : new List<string>()),
			new GeneralsAndCaptainsAssignmentLogic((isPlayerAttacker && isPlayerGeneral) ? playerCharacter.GetName() : ((isPlayerAttacker && isPlayerSergeant) ? playerSideGeneralCharacter.GetName() : null), (!isPlayerAttacker && isPlayerGeneral) ? playerCharacter.GetName() : ((!isPlayerAttacker && isPlayerSergeant) ? playerSideGeneralCharacter.GetName() : null)),
			new EquipmentControllerLeaveLogic(),
			new MissionHardBorderPlacer(),
			new MissionBoundaryPlacer(),
			new MissionBoundaryCrossingHandler(),
			new HighlightsController(),
			new BattleHighlightsController(),
			new BattleDeploymentMissionController(isPlayerAttacker),
			new BattleDeploymentHandler(isPlayerAttacker),
			new MissionObjectiveLogic()
		});
		mission.SetPlayerCanTakeControlOfAnotherAgentWhenDead();
		return mission;
	}
```

```csharp
public static Mission OpenSiegeMissionWithDeployment(string scene, BasicCharacterObject playerCharacter, CustomBattleCombatant playerParty, CustomBattleCombatant enemyParty, bool isPlayerGeneral, float[] wallHitPointPercentages, bool hasAnySiegeTower, List<MissionSiegeWeapon> siegeWeaponsOfAttackers, List<MissionSiegeWeapon> siegeWeaponsOfDefenders, bool isPlayerAttacker, int sceneUpgradeLevel = 0, string seasonString = "", bool isSallyOut = false, bool isReliefForceAttack = false, float timeOfDay = 6f)
	{
		string sceneLevels = sceneUpgradeLevel switch
		{
			2 => "level_2", 
			1 => "level_1", 
			_ => "level_3", 
		} + " siege";
		BattleSideEnum playerSide = playerParty.Side;
		IMissionTroopSupplier[] troopSuppliers = new IMissionTroopSupplier[2];
		CustomBattleTroopSupplier customBattleTroopSupplier = new CustomBattleTroopSupplier(playerParty, isPlayerSide: true, isPlayerGeneral, isSallyOut);
		troopSuppliers[(int)playerParty.Side] = customBattleTroopSupplier;
		CustomBattleTroopSupplier customBattleTroopSupplier2 = new CustomBattleTroopSupplier(enemyParty, isPlayerSide: false, isPlayerGeneral: false, isSallyOut);
		troopSuppliers[(int)enemyParty.Side] = customBattleTroopSupplier2;
		bool isPlayerSergeant = !isPlayerGeneral;
		Mission mission = MissionState.OpenNew("CustomSiegeBattle", new MissionInitializerRecord(scene)
		{
			PlayingInCampaignMode = false,
			AtmosphereOnCampaign = CreateAtmosphereInfoForMission(seasonString, (int)timeOfDay),
			SceneLevels = sceneLevels,
			DecalAtlasGroup = 2
		}, delegate
		{
			List<MissionBehavior> list = new List<MissionBehavior>
			{
				new BattleSpawnLogic(isSallyOut ? "sally_out_set" : (isReliefForceAttack ? "relief_force_attack_set" : "battle_set")),
				new MissionOptionsComponent(),
				new BattleEndLogic(),
				new BattleReinforcementsSpawnController(),
				new MissionCombatantsLogic(null, playerParty, (!isPlayerAttacker) ? playerParty : enemyParty, isPlayerAttacker ? playerParty : enemyParty, (!isSallyOut) ? Mission.MissionTeamAITypeEnum.Siege : Mission.MissionTeamAITypeEnum.SallyOut, isPlayerSergeant),
				new SiegeMissionPreparationHandler(isSallyOut, isReliefForceAttack, wallHitPointPercentages, hasAnySiegeTower)
			};
			Mission.BattleSizeType battleSizeType = ((!isSallyOut) ? Mission.BattleSizeType.Siege : Mission.BattleSizeType.SallyOut);
			list.Add(new MissionAgentSpawnLogic(troopSuppliers, playerSide, battleSizeType));
			list.Add(new BattlePowerCalculationLogic());
			if (isSallyOut)
			{
				list.Add(new CustomSallyOutMissionController((!isPlayerAttacker) ? playerParty : enemyParty, isPlayerAttacker ? playerParty : enemyParty));
			}
			else if (isReliefForceAttack)
			{
				list.Add(new CustomSallyOutMissionController((!isPlayerAttacker) ? playerParty : enemyParty, isPlayerAttacker ? playerParty : enemyParty));
			}
			else
			{
				list.Add(new CustomSiegeMissionSpawnHandler((!isPlayerAttacker) ? playerParty : enemyParty, isPlayerAttacker ? playerParty : enemyParty, spawnWithHorses: false));
			}
			list.Add(new BattleObserverMissionLogic());
			list.Add(new CustomBattleAgentLogic());
			list.Add(new BannerBearerLogic());
			list.Add(new AgentHumanAILogic());
			if (!isSallyOut)
			{
				list.Add(new AmmoSupplyLogic(new List<BattleSideEnum> { BattleSideEnum.Defender }));
			}
			list.Add(new AgentVictoryLogic());
			list.Add(new AssignPlayerRoleInTeamMissionController(isPlayerGeneral, isPlayerSergeant, isPlayerInArmy: false));
			list.Add(new GeneralsAndCaptainsAssignmentLogic((isPlayerAttacker && isPlayerGeneral) ? playerCharacter.GetName() : null, null, null, null, createBodyguard: false));
			list.Add(new MissionAgentPanicHandler());
			list.Add(new MissionBoundaryPlacer());
			list.Add(new MissionBoundaryCrossingHandler());
			list.Add(new AgentMoraleInteractionLogic());
			list.Add(new HighlightsController());
			list.Add(new BattleHighlightsController());
			list.Add(new EquipmentControllerLeaveLogic());
			if (isSallyOut)
			{
				list.Add(new MissionSiegeEnginesLogic(new List<MissionSiegeWeapon>(), siegeWeaponsOfAttackers));
			}
			else
			{
				list.Add(new MissionSiegeEnginesLogic(siegeWeaponsOfDefenders, siegeWeaponsOfAttackers));
			}
			list.Add(new SiegeDeploymentHandler(isPlayerAttacker));
			list.Add(new SiegeDeploymentMissionController(isPlayerAttacker));
			return list.ToArray();
		});
		mission.SetPlayerCanTakeControlOfAnotherAgentWhenDead();
		return mission;
	}
```

```csharp
public static Mission OpenCustomBattleLordsHallMission(string scene, BasicCharacterObject playerCharacter, CustomBattleCombatant playerParty, CustomBattleCombatant enemyParty, BasicCharacterObject playerSideGeneralCharacter, string sceneLevels = "", int sceneUpgradeLevel = 0, string seasonString = "")
	{
		int remainingDefenderArcherCount = TaleWorlds.Library.MathF.Round(18.9f);
		BattleSideEnum playerSide = BattleSideEnum.Attacker;
		bool isPlayerAttacker = playerSide == BattleSideEnum.Attacker;
		IMissionTroopSupplier[] troopSuppliers = new IMissionTroopSupplier[2];
		CustomBattleTroopSupplier customBattleTroopSupplier = new CustomBattleTroopSupplier(playerParty, isPlayerSide: true, playerCharacter == playerSideGeneralCharacter, isSallyOut: false);
		troopSuppliers[(int)playerParty.Side] = customBattleTroopSupplier;
		CustomBattleTroopSupplier customBattleTroopSupplier2 = new CustomBattleTroopSupplier(enemyParty, isPlayerSide: false, isPlayerGeneral: false, isSallyOut: false, delegate(BasicCharacterObject basicCharacterObject)
		{
			bool result = true;
			if (basicCharacterObject.IsRanged)
			{
				if (remainingDefenderArcherCount > 0)
				{
					remainingDefenderArcherCount--;
				}
				else
				{
					result = false;
				}
			}
			return result;
		});
		troopSuppliers[(int)enemyParty.Side] = customBattleTroopSupplier2;
		return MissionState.OpenNew("CustomBattleLordsHall", new MissionInitializerRecord(scene)
		{
			DoNotUseLoadingScreen = false,
			PlayingInCampaignMode = false,
			SceneLevels = "siege",
			DecalAtlasGroup = 3
		}, (Mission missionController) => new MissionBehavior[17]
		{
			new MissionOptionsComponent(),
			new BattleEndLogic(),
			new MissionCombatantsLogic(null, playerParty, (!isPlayerAttacker) ? playerParty : enemyParty, isPlayerAttacker ? playerParty : enemyParty, Mission.MissionTeamAITypeEnum.NoTeamAI, isPlayerSergeant: false),
			new BattleMissionStarterLogic(),
			new AgentHumanAILogic(),
			new LordsHallFightMissionController(troopSuppliers, 3f, 0.7f, 19, 27, playerSide),
			new BattleObserverMissionLogic(),
			new CustomBattleAgentLogic(),
			new AgentVictoryLogic(),
			new AmmoSupplyLogic(new List<BattleSideEnum> { BattleSideEnum.Defender }),
			new EquipmentControllerLeaveLogic(),
			new MissionHardBorderPlacer(),
			new MissionBoundaryPlacer(),
			new MissionBoundaryCrossingHandler(),
			new BattleMissionAgentInteractionLogic(),
			new HighlightsController(),
			new BattleHighlightsController()
		});
	}
```

**Conclusion:** `GetMissionBehavior<CustomBattleAgentLogic>() != null` is a reliable installed-v1.3.15 Custom Battle/Custom Siege/Custom Battle Lords Hall gate and does not appear in normal campaign mission openers inspected here. The gate can also match the CustomBattle module's CPU benchmark if that opener is used, but that is not a campaign leak.

### `BehaviorTreeWrapper.BehaviorTreeAgentComponent`

```csharp
using System;
using BehaviorTrees;
using TaleWorlds.MountAndBlade;

namespace BehaviorTreeWrapper;

public class BehaviorTreeAgentComponent : AgentComponent
{
	private float timeSinceLastEvaluation;

	public BehaviorTree? Tree { get; private set; }

	public BehaviorTreeAgentComponent(Agent agent, string treeName, params object[] args)
		: base(agent)
	{
		object[] array = new object[args.Length + 1];
		array[0] = agent;
		Array.Copy(args, 0, array, 1, args.Length);
		args = array;
		Tree = BehaviorTreeBannerlordWrapper.Instance.AddBehaviorTree(treeName, args);
		if (Tree != null)
		{
			Random random = new Random();
			timeSinceLastEvaluation = (float)(random.NextDouble() * (double)((float)Tree._rootEvaluationDelay / 1000f));
		}
	}

	public override void OnAgentRemoved()
	{
		BehaviorTreeBannerlordWrapper.Instance.DisposeTree(base.Agent);
	}

	public override void OnTickAsAI(float dt)
	{
		if (Tree != null)
		{
			timeSinceLastEvaluation += dt;
			if ((float)(Tree._rootEvaluationDelay / 1000) < timeSinceLastEvaluation || Tree.ShouldRunNextTick)
			{
				Tree.RunTree();
				timeSinceLastEvaluation = 0f;
			}
		}
	}
}

```

**Conclusion:** constructor `(Agent, string, params object[])` exists, `Tree` is public get/private set, and `OnTickAsAI(float)` is public override. Spider's BT attachment compiles against the bundled wrapper API.

---

## 2. Feature-specific deep analysis

### A. First Custom Battle launch

1. `SubModule.OnMissionBehaviorInitialize` adds `AdvancedCombatBehavior`, `BehaviorTreeMissionLogic`, `WargMissionBehavior`, then `SpiderMissionBehavior`.
2. First spider tick calls `Initialize()` (SpiderMissionBehavior.cs:89): registers `SpiderTree`; because `AdvancedCombatBehavior` is present, spider does not own `SpatialGrid`.
3. Until `_timeSinceStart >= 1f`, tick returns.
4. At t≈1s, `_spawned` flips true, then `ShouldSpawnInThisMission()` checks `Mission.Current != null`, `Mode == Battle`, and `CustomBattleAgentLogic` present. Installed `BannerlordMissions.OpenCustomBattleMission` adds `new CustomBattleAgentLogic()` to Custom Battle missions, so field Custom Battle passes.
5. `PickEnemyTeam()` uses `Mission.Current.PlayerTeam` and returns the first team for which `team.IsEnemyOf(playerTeam)` is true. TAOM's CustomBattleTeamFixBehavior also forces attacker/defender enemy relationships if vanilla failed to set them.
6. `reference = Mission.Current.MainAgent?.Position ?? Vec3.Zero`; if `MainAgent` were still null at this exact second, spiders would spawn around scene origin. In installed CustomBattle missions, `MissionAgentSpawnLogic`/`CustomBattleMissionSpawnHandler` are mission behaviors opened before TAOM's `OnMissionBehaviorInitialize`, and in normal field Custom Battle the player agent is expected to exist before the 1-second delayed tick. I did not find a vanilla quote proving MainAgent is guaranteed by t=1s, so this remains an observation, not a finding.
7. `_spawnerService.SpawnSpiders` looks up anchor `taom_spider_creature`, looks up Monster `spider`, builds `AgentBuildData(character).Monster(spiderMonster).Team(enemyTeam).InitialPosition(...).Controller(AI).NoHorses(true).NoWeapons(true)`, and calls `Mission.Current.SpawnAgent`.
8. During `SpawnAgent`, mission behaviors receive `OnAgentBuild`; spider's `OnAgentBuild` ignores those agents because `_treesAdded` is still false. After spawning, the same tick sets `_treesAdded = true`, scans `Mission.Current.AllAgents`, and attaches `BehaviorTreeAgentComponent(agent, "SpiderTree", Array.Empty<object>())` to all spider agents.

### B. Second Custom Battle launch in same session

`SpiderMissionBehavior` has instance-local `_spawned`, `_treesAdded`, timers, and component list; a new mission gets a new instance. `BTRegister.RegisterClass("SpiderTree", ...)` is idempotent in `BehaviorTrees.BTRegister.RegisterClass` because it only inserts when the class name is absent. `SpatialGrid.Instance` is reset by `AdvancedCombatBehavior` constructor on every mission. `BehaviorTreeMissionLogic` owns per-mission tree state. I found no stale spider state across Custom Battle relaunches.

### C. Mid-battle spider death

`BehaviorTreeAgentComponent.OnAgentRemoved()` calls `BehaviorTreeBannerlordWrapper.Instance.DisposeTree(base.Agent)`, and `OnSpiderDied` subscribes to `OnSelfRemoved` for debug logging. SpiderMissionBehavior's own `_spiderComponents` list is pruned on the next tick by `if (!spider.IsActive()) RemoveAt(i)`. The code iterates backwards, so removal during tick does not invalidate enumeration. I found no race that corrupts `_spiderComponents`; the worst case is one dead component remains until the next `OnMissionTick`.

### D. Player team has no enemies

If `PickEnemyTeam()` returns null, SpiderMissionBehavior logs `[Spider] No enemy team found for spider spawn` and `_spawned` stays true, so it will not retry if teams appear later. In normal Custom Battle, attacker/defender teams are fixed by mission setup before combat, so this is acceptable for the current Custom Battle-only scope. It would need a retry if the feature is reused for dynamic campaign/scene triggers.

### E. LOTRLOME_Armory not loaded

`LOTRLOME_Armory` is declared as optional in `SubModule.xml`, so TAOM is not intended to be blocked by the dependency declaration. When missing, `MBObjectManager.Instance?.GetObject<Monster>("spider")` returns null; `SpiderSpawnerService` logs an error and returns an empty list. This is a graceful no-op for the feature; the anchor's `dg_uruk` race and bodyproperty live in TAOM, not the armory.

### F. Bone-collision attack with placeholder indices

TAOM spider config:

```csharp
    // Bone indices for fang/bite collision points.
    // PLACEHOLDER values copied from warg pattern (chest 23, jaw 37, fangs 43).
    // Refine after a runtime bone-index dump on the spider skeleton — replace with
    // the indices that resolve to joint5_l, joint5_r, joint12_m (mouth) on as_spider.
    public const sbyte FangBoneIndexPrimary = 23;
    public const sbyte FangBoneIndexSecondaryLeft = 37;
    public const sbyte FangBoneIndexSecondaryRight = 43;
```

TAOM collision code uses those indices as exact attacker skeleton bones, then checks exact distances to every target bone:

```csharp
        {
            Logger.LogWarning($"Agent {_agent?.Name ?? "null"} is no longer valid for bone collision check");
            return false;
        }

        IAgentVisualsAdapter agentVisuals = _agent.AgentVisuals;
        if (agentVisuals == null)
        {
            Logger.LogWarning($"Failed to get visuals for {_agent.Name}");
            return false;
        }

        Skeleton agentSkeleton = agentVisuals.GetSkeleton();
        if (agentSkeleton == null)
        {
            Logger.LogWarning($"Failed to get skeleton for {_agent.Name}");
            return false;
        }
        MatrixFrame agentGlobalFrame = agentVisuals.GetGlobalFrame();

        List<(sbyte, Vec3)> agentBonePositions = new();
        int boneCount = agentSkeleton.GetBoneCount();
        foreach (sbyte bone in _boneIds)
        {
            if (bone < 0 || bone >= boneCount)
            {
                Logger.LogError($"Invalid bone index {bone} for agent {_agent.Name}");
                continue;
            }
            MatrixFrame agentBoneFrame = agentSkeleton.GetBoneEntitialFrameWithIndex(bone);
            Vec3 agentBoneGlobalPos = agentGlobalFrame.TransformToParent(agentBoneFrame.origin);
            agentBonePositions.Add((bone, agentBoneGlobalPos));
        }

        for (int i = 0; i < _targets.Count; i++)
        {
            IAgentAdapter target = _targets[i];

            if (target == null || !target.IsActive() || target.IsFadingOut())
            {
                _targets.RemoveAt(i);
                i--;
                continue;
            }

```

The target shortlist from `SpatialGrid` is only a broad-phase filter; it does not itself create a hit:

```csharp
        _agent.SetActionChannel(0, action, true);

        if (SpatialGrid.Instance == null)
        {
            _logger.LogWarning("AgentAdapter:CustomAttack: SpatialGrid not initialized.");
            return;
        }

        var targets = SpatialGrid.Instance.GetNearAliveAgentsInRange(targetDetectionRange, _agent)
            .FindAll(agt => agt != _agent && agt.RiderAgent != _agent && agt.IsActive())
            .Select(x => _factory.GetAgentAdapter(x))
            .ToList();

        if (targets.Count == 0) return;

        var boneCollisionService = _boneCollisionServiceFactory();
        boneCollisionService.AddBoneCheckComponent(new BoneCheckDuringAnimation(
            action,
            this,
            targets,
            bonesIdsForCollision,
            actionProgressMin,
            actionProgressMax,
            boneCollisionRadius,
            stopOnFirstHit,
            onHitCallback,
            onExpirationCallback
        ));
```

**Conclusion:** this is not cosmetic. Because the spider skeleton has 62 bones, indices 23/37/43 are in range and will not fail as invalid indices, but the bite only damages when those specific indexed spider bones come within `0.3f`/`0.4f` of target bones during the attack window. If they are legs/abdomen rather than `joint5_l`, `joint5_r`, and `joint12_m`, mouth-over-target contact can miss and leg/body contact can produce hits.

---

## 3. CONFIG CROSS-REFERENCE

- `"spider"` Monster id: `monsters.xml:1840: <Monster id="spider"`
- Monster action set: `monsters.xml:1841: action_set="as_spider"`
- Monster usage set: `monsters.xml:1842: monster_usage="spider"`
- `"as_spider"` action_set id: `action_sets.xml:58875: id="as_spider"`
- `"act_spider_attack_front"`: `action_sets.xml:58891: <action type="act_spider_attack_front"    animation="spider_attack_front" />` and `action_types.xml:20: <action name="act_spider_attack_front" />`
- `"act_spider_attack_charge"`: `action_sets.xml:58897: <action type="act_spider_attack_charge"   animation="spider_attack_charge" />` and `action_types.xml:26: <action name="act_spider_attack_charge" />`
- `"spider"` monster_usage id: `monster_usage_sets.xml:11: id="spider">`
- `"taom_spider_creature"` anchor id: `Main/_Module/ModuleData/characters/spider_creature.xml:18`
- `race="dg_uruk"`: `Main/_Module/ModuleData/Races/skins.xml:141445: <race id="dg_uruk">`
- `culture="Culture.dolguldur"`: `Main/_Module/ModuleData/taom_spcultures.xml:2540: id="dolguldur"`
- `BodyProperty.fighter_dolguldur`: `Main/_Module/ModuleData/TAOM_bodyproperties.xml:82: id="fighter_dolguldur">`
- `"SpiderTree"`: exact match in `SpiderMissionBehavior.cs:49` and `SpiderBehaviorTree.cs` class/build registration.

No ID mismatches found (`dolguldur` is correctly spelled; no invalid `dol_guldur`/`rohan` literals in the Spider feature).

---

## 4. Known suspects — confirmed/disputed

1. **Mission API misuse:** DISPUTED. Installed `Mission.SpawnAgent` honors `agentBuildData.AgentMonster`, and `.Monster(Monster)` stores into `AgentData.AgentMonster`.
2. **Custom Battle gating:** DISPUTED for campaign leakage. Installed Custom Battle openers add `CustomBattleAgentLogic`; normal campaign battle openers inspected do not. The gate includes Custom Battle siege/lord's hall and possibly CPU benchmark, but not campaign.
3. **Anchor character bleed-through:** CONFIRMED for Custom Battle troop picker. See HIGH finding below. `hidden_in_encyclopedia` and `is_basic_troop="false"` do not protect the Custom Battle troop selector path.
4. **Bone collision indices are warg placeholders:** CONFIRMED as functional, not cosmetic. Existing indices are valid-range on a 62-bone skeleton but hit detection is tied to those exact bones; wrong indices make bites miss/hit from wrong body parts.
5. **`CustomBattleAgentLogic` reference fragility:** OBSERVATION only. A future TaleWorlds rename would be a compile-time source/API break for TAOM, not a silent false return in this compiled build; current v1.3.15 type exists.
6. **Spawn timing race condition:** OBSERVATION only. The `MainAgent ?? Vec3.Zero` fallback can spawn at origin if MainAgent is null at t≈1s, but I did not find installed vanilla evidence that this occurs in Custom Battle. No finding filed.

---

## 5. Findings

### HIGH

[HIGH] Main/_Module/ModuleData/characters/spider_creature.xml:24 — Anchor bleed-through — `occupation="Soldier"` makes the anchor eligible for the Custom Battle troop selector despite `hidden_in_encyclopedia` / `is_basic_troop="false"` — Fix: make the anchor non-soldier while still loadable for `AgentBuildData`, or patch TAOM's Custom Battle troop list to exclude `taom_spider_creature` explicitly.

TAOM source:

```xml
  <NPCCharacter id="taom_spider_creature"
                race="dg_uruk"
                default_group="Infantry"
                is_hero="false"
                is_basic_troop="false"
                hidden_in_encyclopedia="true"
                occupation="Soldier"
                culture="Culture.dolguldur"
                name="{=taom_spider_creature_name}Giant Spider">
```

Installed vanilla Custom Battle source:

```csharp
public ArmyCompositionGroupVM(TroopTypeSelectionPopUpVM troopTypeSelectionPopUp)
	{
		MinArmySize = 1;
		MaxArmySize = BannerlordConfig.MaxBattleSize;
		foreach (BasicCharacterObject item in ((IEnumerable<BasicCharacterObject>)Game.Current.ObjectManager.GetObjectTypeList<BasicCharacterObject>()).Where((BasicCharacterObject c) => c.IsSoldier && !c.IsObsolete))
		{
			_allCharacterObjects.Add(item);
		}
		CompositionValues = new int[4];
		CompositionValues[0] = 25;
		CompositionValues[1] = 25;
		CompositionValues[2] = 25;
		CompositionValues[3] = 25;
		MeleeInfantryComposition = new ArmyCompositionItemVM(ArmyCompositionItemVM.CompositionType.MeleeInfantry, _allCharacterObjects, _allSkills, UpdateSliders, troopTypeSelectionPopUp, CompositionValues);
		RangedInfantryComposition = new ArmyCompositionItemVM(ArmyCompositionItemVM.CompositionType.RangedInfantry, _allCharacterObjects, _allSkills, UpdateSliders, troopTypeSelectionPopUp, CompositionValues);
		MeleeCavalryComposition = new ArmyCompositionItemVM(ArmyCompositionItemVM.CompositionType.MeleeCavalry, _allCharacterObjects, _allSkills, UpdateSliders, troopTypeSelectionPopUp, CompositionValues);
		RangedCavalryComposition = new ArmyCompositionItemVM(ArmyCompositionItemVM.CompositionType.RangedCavalry, _allCharacterObjects, _allSkills, UpdateSliders, troopTypeSelectionPopUp, CompositionValues);
		ArmySize = BannerlordConfig.GetRealBattleSize() / 5;
		((ViewModel)this).RefreshValues();
	}
```

```csharp
private bool IsValidUnitItem(BasicCharacterObject o)
	{
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Invalid comparison between Unknown and I4
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Invalid comparison between Unknown and I4
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Invalid comparison between Unknown and I4
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Invalid comparison between Unknown and I4
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Invalid comparison between Unknown and I4
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Invalid comparison between Unknown and I4
		if (o != null && _culture == o.Culture)
		{
			switch (_type)
			{
			case CompositionType.MeleeInfantry:
				if ((int)o.DefaultFormationClass != 0)
				{
					return (int)o.DefaultFormationClass == 5;
				}
				return true;
			case CompositionType.RangedInfantry:
				return (int)o.DefaultFormationClass == 1;
			case CompositionType.MeleeCavalry:
				if ((int)o.DefaultFormationClass != 2 && (int)o.DefaultFormationClass != 7)
				{
					return (int)o.DefaultFormationClass == 6;
				}
				return true;
			case CompositionType.RangedCavalry:
				return (int)o.DefaultFormationClass == 3;
			default:
				return false;
			}
		}
		return false;
	}
```

Installed `BasicCharacterObject` XML load source:

```csharp
			Race = FaceGen.GetRaceOrDefault(xmlAttribute2.Value);
		}
		XmlNode xmlNode = node.Attributes["occupation"];
		if (xmlNode != null)
		{
			IsSoldier = xmlNode.InnerText.IndexOf("soldier", StringComparison.OrdinalIgnoreCase) >= 0;
		}
		_isBasicHero = XmlHelper.ReadBool(node, "is_hero");
```

Runtime scenario: when Dol Guldur is selected in TAOM Custom Battle, `ArmyCompositionGroupVM` collects every `BasicCharacterObject` where `c.IsSoldier && !c.IsObsolete`; the anchor's `occupation="Soldier"` sets `IsSoldier=true`, and `ArmyCompositionItemVM.IsValidUnitItem` only checks same culture plus formation class. It does not check `hidden_in_encyclopedia` or `is_basic_troop`, so the empty humanoid anchor can appear as a melee-infantry troop option and be spawned through the normal troop pipeline.

### MEDIUM / LOW

No additional confirmed findings beyond the suspects. The placeholder fang indices are a confirmed functional open item, but they are already documented in `docs/features/spider.md` and `CHANGELOG.md`, so I am treating them as an observation/open issue rather than a new finding.

---

## Summary

CRITICAL: 0 | HIGH: 1 | MEDIUM: 0 | LOW: 0
VERDICT: ISSUES FOUND
