# Bannerlord API Diff: v1.3.15 -> v1.4.0

Generated: 2026-04-09 10:21
Scope: 108 TaleWorlds types referenced by TAOM

## Summary

| Metric | Count |
|--------|-------|
| Types checked | 108 |
| Unchanged | 46 |
| **Changed** | **37** |
| Removed (BREAKING) | 0 |
| New in v1.4.0 | 4 |

## Changed Types (sorted by size of change)

### DefaultAllianceModel (+454 lines)

- Old: 307 lines | New: 761 lines
- Path: `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultAllianceModel.cs`

**Signature changes (public/protected API):**
```diff
-	private const int _thresholdForCallToWarWallet = 100000;
+	private const int ThresholdForCallToWarWallet = 100000;
-	private const float SharedWarsEffect = 25f;
+	private const float FirstDegreeNeighborScore = 1f;
-	private const int MaxRelationshipEffect = 20;
+	private const int ThreatScoreCoefficient = 130;
-	private const int PotentialAllyBonus = 5;
+	private const float AllianceScoreNormalizationFactor = 0.08f;
-	private const int TooPowerfulEffect = 20;
+	private const float MarriageEffect = 50f;
-	private static readonly TextObject _sharedWarsText = new TextObject("{=Pg7bxzcY}Effect of shared wars");
+	private const float AtWarEffect = -5f;
-	private static readonly TextObject _unsharedWarsText = new TextObject("{=9YFVXAZ3}Unshared wars");
+	private const float AtPeaceEffect = 25f;
-	private static readonly TextObject _lackOfCommonEnemiesText = new TextObject("{=ugMAk9nb}Lack of common enemies");
+	private const float AtWarWithAllyEffect = -100f;
-	private static readonly TextObject _relationText = new TextObject("{=3YVDMg5X}Low relations between rulers");
+	private const float AtWarWithEnemyEffect = 100f;
-	private static readonly TextObject _traitLevelText = new TextObject("{=iUURpauf}Effect of trait level");
+	private const float HonorableRulerEffect = 50f;
-	private static readonly TextObject _receivedTributeText = new TextObject("{=pV1LM0aE}Receiving tribute");
+	private const float DishonorableRulerEffect = -50f;
-	private static readonly TextObject _paidTributeText = new TextObject("{=lyxa5jbH}Effect of tribute paying to the declared");
+	private const float TradeAgreementEffect = 50f;
-	private static readonly TextObject _threatenedText = new TextObject("{=92m8jTWP}Feels threatened");
+	private const float CommonThreatEffect = 100f;
-	private static readonly TextObject _townsText = new TextObject("{=WaYxP7bX}Effect of having less than 3 towns");
+	private const float ThresholdForThreatScoreForQuerier = 430f;
-	private static readonly TextObject _warWithTheirAllyText = new TextObject("{=EOkS8gn8}Effect of having an ally that we are at war with");
+	private const float ThresholdForThreatScoreForQueried = 430f;
-	private static readonly TextObject _allyWithTheirEnemyText = new TextObject("{=LhrU9cu3}Effect of having a ally that they are at war with");
+	private const float SecondAlliancePenaltyForAllianceScore = -600f;
-	private static readonly TextObject _conflictingAllianceText = new TextObject("{=IeGgrMlx}Conflicting alliances");
+	private const float WarDeclarationScorePenaltyAgainstAllies = 0.5f;
+	private const float WarDeclarationScoreBonusAgainstEnemiesOfAllies = 0.3f;
+	private const float WarDeclarationScoreBonusAgainstBiggestThreat = 0.5f;
+	private const float PeaceDeclarationScorePenaltyAgainstEnemiesOfAllies = 0.7f;
+	private const float AllianceScoreThreshold = 50f;
+	private readonly TextObject _relationshipText = new TextObject("{=3YVDMg5X}Low relations between rulers.");
+	private readonly TextObject _kingdomsNotNeighborsText = new TextObject("{=*}Kingdoms aren't neighbors.");
+	private readonly TextObject _kingdomsNotSeekingAllianceText = new TextObject("{=*}{KINGDOM_NAME} is not seeking alliances at the moment.");
+	private readonly TextObject _atWarWithAllyText = new TextObject("{=*}Your realm is at war with their ally.");
+	private readonly TextObject _sameCultureFiefsText = new TextObject("{=*}Your realm is occupying fiefs belonging to their culture.");
+	private readonly TextObject _atWarText = new TextObject("{=*}Your realm is participating in a war.");
+	private readonly TextObject _lowHonorText = new TextObject("{=*}{RULER.NAME} has low honor.");
+	private readonly TextObject _kingdomNotConsederingAllianceText = new TextObject("{=*}{KINGDOM} is not considering an alliance with your realm due to:{newline}{newline}{REASONS_BY_LINE}");
+	private readonly TextObject _allianceNotFormedExplanationText = new TextObject("{=*}An alliance cannot be formed due to:{newline}{newline}{REASON}");
+	private readonly TextObject _tooManyAlliancePlayerPenaltyText = new TextObject("{=*}Number of alliances your realm's already in: {NUMBER_OF_ALLIES}/{MAX_NUMBER_OF_ALLIES}");
+	private readonly TextObject _tooManyAllianceAIPenaltyText = new TextObject("{=*}Number of alliances {KINGDOM_NAME} is already in: {NUMBER_OF_ALLIES}/{MAX_NUMBER_OF_ALLIES}");
+	private readonly TextObject _allianceScoreNotEnoughText = new TextObject("{=*}{KINGDOM_NAME} currently does not consider your realm to be a possible ally.");
+	private readonly TextObject _threatEffect = new TextObject("{=!}Threat Effect");
+	private readonly TextObject _marriageEffect = new TextObject("{=!}Marriage Effect");
+	private readonly TextObject _atWarWithEnemyEffect = new TextObject("{=!}At war with enemy Effect");
+	private readonly TextObject _tradeAgreementEffect = new TextObject("{=!}Trade Agreement Effect");
+	private readonly TextObject _commonThreatEffect = new TextObject("{=!}Common Threat Effect");
+	private ITradeAgreementsCampaignBehavior _tradeAgreementsBehavior;
+	private ITradeAgreementsCampaignBehavior TradeAgreementsCampaignBehavior
-	public override ExplainedNumber GetScoreOfStartingAlliance(Kingdom kingdomDeclaresAlliance, Kingdom kingdomDeclaredAlliance, IFaction evaluatingFaction, out TextObject explanationText, bool includeDescription = false)
+	public override ExplainedNumber GetScoreOfStartingAlliance(Kingdom querierKingdom, Kingdom queriedKingdom, out TextObject explanationText, bool includeDescription = false)
+	public override float GetSupportScoreOfStartingAllianceForClan(Kingdom querierKingdom, Kingdom queriedKingdom, Clan evaluatingClan, out TextObject explanationText, bool includeDescriptions = false)
-	private void AddHonorEffectToExplanationTooltip(int honor, Hero ruler, ref ExplainedNumber explanation)
+	public override bool CanMakeAlliance(Kingdom kingdom, Kingdom targetKingdom, IFaction evaluatingFaction, out TextObject reason, bool includeReason = false)
-	private void AddConflictingAlliancesEffectToExplanationTooltip(int enemyAllyEffectOnOurSide, int enemyAllyEffectOnTheirSide, ref ExplainedNumber explanation)
-	private void AddSharedWarsEffectToExplanationTooltip(int numberOfSharedWars, float sharedWarsEffect, float unsharedWarsEffect, int numberOfWarsOfDeclaredKingdom, int numberOfWarsOfDeclaringKingdom, ref ExplainedNumber explanation)
-	private void AddNoWarsEffectToExplanationTooltip(ref ExplainedNumber explanation)
-	private void AddTributeEffectToExplanationTooltip(float tributeEffect, ref ExplainedNumber explanation)
-	private void AddTooPowerfulEffectToExplanationTooltip(ref ExplainedNumber explanation)
-	private void AddLowRelationEffectToExplanationTooltip(int relationshipEffect, ref ExplainedNumber explanation)
-	private TextObject BuildExplanationForAlliance(Kingdom other, ExplainedNumber tooltip)
-	private TextObject GetAllianceExplanation(ExplainedNumber explainedNumber)
+	public override float GetAllianceFactorForDeclaringWar(IFaction factionDeclaresWar, IFaction factionDeclaredWar)
+	public override float GetAllianceFactorForDeclaringPeace(IFaction factionDeclaresPeace, IFaction factionDeclaredPeace)
+	public override Clan GetProposerClanForAllianceDecision(Kingdom proposerKingdom, Kingdom proposedKingdom)
+	private (Kingdom threateningKingdom, float threatScore) GetThreateningNeighbor(Kingdom querierKingdom, out float exposureScore, out float powerRatio)
+	private bool IsThereMarriageBetweenClans(Clan clan1, Clan clan2)
+	private TextObject BuildExplanationForAlliance(Kingdom other, List<(float, TextObject)> explanationList)
+	private bool AreKingdomsNeighbors(Kingdom kingdom1, Kingdom kingdom2)
+	private float CalculateThreatScore(float neighborScore, float totalNeighborScore, float powerOfThreat, float powerOfQuerier, out float exposureScore, out float powerRatio)
+	private float GetThreatEffect(float threatScoreForQuerier, float threatScoreForQueried)
+	private float GetRelationshipEffect(Kingdom querierKingdom, Kingdom queriedKingdom)
+	private float GetMarriageEffect(Kingdom querierKingdom, Kingdom queriedKingdom)
+	private float GetAtWarWithAllyEffect(Kingdom querierKingdom, Kingdom queriedKingdom)
+	private float GetAtWarWithEnemyEffect(Kingdom querierKingdom, Kingdom queriedKingdom)
+	private float GetAtWarOrPeaceEffect(Kingdom queriedKingdom)
+	private float GetFiefWithSameCultureEffect(Kingdom querierKingdom, Kingdom queriedKingdom)
+	private float GetHonorableKingEffect(Kingdom querierKingdom, Kingdom queriedKingdom)
+	private float GetTradeAgreementEffect(Kingdom querierKingdom, Kingdom queriedKingdom)
+	private float GetCommonThreatEffect(Kingdom threateningKingdomForQuerier, Kingdom threateningKingdomForQueried)
+	private float GetAlliancePenalty(Kingdom kingdom)
+	private TextObject GetAlliancePenaltyText(Kingdom kingdom, bool includeDescription)
+	private bool CanMakeAllianceWithPlayerSupport(Kingdom proposingKingdom, Kingdom proposedKingdom, Clan evaluatingClan)
+	private float CalculateKingdomStrength(Kingdom kingdom)
```

<details>
<summary>Full diff (454 line delta)</summary>

```diff
@@ -1,8 +1,11 @@
 using System.Collections.Generic;
 using System.Linq;
+using TaleWorlds.CampaignSystem.CampaignBehaviors;
 using TaleWorlds.CampaignSystem.CharacterDevelopment;
 using TaleWorlds.CampaignSystem.ComponentInterfaces;
+using TaleWorlds.CampaignSystem.Election;
 using TaleWorlds.CampaignSystem.Extensions;
+using TaleWorlds.CampaignSystem.Party;
 using TaleWorlds.CampaignSystem.Settlements;
 using TaleWorlds.Core;
 using TaleWorlds.Library;
@@ -13,42 +16,98 @@ namespace TaleWorlds.CampaignSystem.GameComponents;
 
 public class DefaultAllianceModel : AllianceModel
 {
-	private const int _thresholdForCallToWarWallet = 100000;
+	private const int ThresholdForCallToWarWallet = 100000;
 
-	private const float SharedWarsEffect = 25f;
+	private const float FirstDegreeNeighborScore = 1f;
 
-	private const int MaxRelationshipEffect = 20;
+	private const int ThreatScoreCoefficient = 130;
 
-	private const int PotentialAllyBonus = 5;
+	private const float AllianceScoreNormalizationFactor = 0.08f;
 
-	private const int TooPowerfulEffect = 20;
+	private const float MarriageEffect = 50f;
 
-	private static readonly TextObject _sharedWarsText = new TextObject("{=Pg7bxzcY}Effect of shared wars");
+	private const float AtWarEffect = -5f;
 
-	private static readonly TextObject _unsharedWarsText = new TextObject("{=9YFVXAZ3}Unshared wars");
+	private const float AtPeaceEffect = 25f;
 
-	private static readonly TextObject _lackOfCommonEnemiesText = new TextObject("{=ugMAk9nb}Lack of common enemies");
+	private const float AtWarWithAllyEffect = -100f;
 
-	private static readonly TextObject _relationText = new TextObject("{=3YVDMg5X}Low relations between rulers");
+	private const float AtWarWithEnemyEffect = 100f;
 
-	private static readonly TextObject _traitLevelText = new TextObject("{=iUURpauf}Effect of trait level");
+	private const float HonorableRulerEffect = 50f;
 
-	private static readonly TextObject _receivedTributeText = new TextObject("{=pV1LM0aE}Receiving tribute");
+	private const float DishonorableRulerEffect = -50f;
 
-	private static readonly TextObject _paidTributeText = new TextObject("{=lyxa5jbH}Effect of tribute paying to the declared");
+	private const float TradeAgreementEffect = 50f;
 
-	private static readonly TextObject _threatenedText = new TextObject("{=92m8jTWP}Feels threatened");
+	private const float CommonThreatEffect = 100f;
 
-	private static readonly TextObject _townsText = new TextObject("{=WaYxP7bX}Effect of having less than 3 towns");
+	private const float ThresholdForThreatScoreForQuerier = 430f;
 
-	private static readonly TextObject _warWithTheirAllyText = new TextObject("{=EOkS8gn8}Effect of having an ally that we are at war with");
+	private const float ThresholdForThreatScoreForQueried = 430f;
 
-	private static readonly TextObject _allyWithTheirEnemyText = new TextObject("{=LhrU9cu3}Effect of having a ally that they are at war with");
+	private const float SecondAlliancePenaltyForAllianceScore = -600f;
 
-	private static readonly TextObject _conflictingAllianceText = new TextObject("{=IeGgrMlx}Conflicting alliances");
+	private const float WarDeclarationScorePenaltyAgainstAllies = 0.5f;
+
+	private const float WarDeclarationScoreBonusAgainstEnemiesOfAllies = 0.3f;
+
+	private const float WarDeclarationScoreBonusAgainstBiggestThreat = 0.5f;
+
+	private const float PeaceDeclarationScorePenaltyAgainstEnemiesOfAllies = 0.7f;
+
+	private const float AllianceScoreThreshold = 50f;
+
+	private readonly TextObject _relationshipText = new TextObject("{=3YVDMg5X}Low relations between rulers.");
+
+	private readonly TextObject _kingdomsNotNeighborsText = new TextObject("{=*}Kingdoms aren't neighbors.");
+
+	private readonly TextObject _kingdomsNotSeekingAllianceText = new TextObject("{=*}{KINGDOM_NAME} is not seeking alliances at the moment.");
+
+	private readonly TextObject _atWarWithAllyText = new TextObject("{=*}Your realm is at war with their ally.");
+
+	private readonly TextObject _sameCultureFiefsText = new TextObject("{=*}Your realm is occupying fiefs belonging to their culture.");
+
+	private readonly TextObject _atWarText = new TextObject("{=*}Your realm is participating in a war.");
+
+	private readonly TextObject _lowHonorText = new TextObject("{=*}{RULER.NAME} has low honor.");
+
+	private readonly TextObject _kingdomNotConsederingAllianceText = new TextObject("{=*}{KINGDOM} is not considering an alliance with your realm due to:{newline}{newline}{REASONS_BY_LINE}");
+
+	private readonly TextObject _allianceNotFormedExplanationText = new TextObject("{=*}An alliance cannot be formed due to:{newline}{newline}{REASON}");
+
+	private readonly TextObject _tooManyAlliancePlayerPenaltyText = new TextObject("{=*}Number of alliances your realm's already in: {NUMBER_OF_ALLIES}/{MAX_NUMBER_OF_ALLIES}");
+
+	private readonly TextObject _tooManyAllianceAIPenaltyText = new TextObject("{=*}Number of alliances {KINGDOM_NAME} is already in: {NUMBER_OF_ALLIES}/{MAX_NUMBER_OF_ALLIES}");
+
+
... (truncated, 37105 chars total)
```
</details>

### Agent (+133 lines)

- Old: 5465 lines | New: 5598 lines
- Path: `E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Agent.cs`

**Signature changes (public/protected API):**
```diff
+	private bool _canPerformBrace;
+	private bool _isBracingCacheValid;
+	public bool CanPerformBraceCached => CanPerformBrace();
+	public bool GetBaseFormationFrame(out WorldPosition formationPosition, out Vec2 formationDirection)
+	public void OnConversationStarted()
+	public MatrixFrame GetBoneEntitialFrame(sbyte boneIndex, bool useBoneMapping)
-	private void UpdateCachedAndFormationValues(bool updateOnlyMovement, bool arrangementChangeAllowed)
+	private void ParallelUpdateCachedAndFormationValuesForAIAgent(bool updateOnlyMovement)
+	private void ApplyFormationValuesPostUpdate(bool updateOnlyMovement, bool arrangementChangeAllowed)
-	private void ParallelUpdateCachedAndFormationValues(bool updateOnlyMovement)
+	public void UpdateDirectionChangeTendency()
+	internal bool TrySetFormationFrame(in WorldPosition formationPosition, in Vec2 formationDirection)
+	public bool CanPerformBrace()
+	private void OnWeaponSlotUpdated()
```

<details>
<summary>Full diff (133 line delta)</summary>

```diff
@@ -615,6 +615,10 @@ public sealed class Agent : DotNetObject, IAgent, IFocusable, IUsable, IFormatio
 
 	private Vec2 _localPositionError;
 
+	private bool _canPerformBrace;
+
+	private bool _isBracingCacheValid;
+
 	public static Agent Main => Mission.Current?.MainAgent;
 
 	public bool IsPlayerControlled
@@ -902,6 +906,8 @@ public sealed class Agent : DotNetObject, IAgent, IFocusable, IUsable, IFormatio
 
 	public bool HasThrownCached => Equipment.ContainsThrownWeapon();
 
+	public bool CanPerformBraceCached => CanPerformBrace();
+
 	public AIStateFlag AIStateFlags
 	{
 		get
@@ -1067,7 +1073,7 @@ public sealed class Agent : DotNetObject, IAgent, IFocusable, IUsable, IFormatio
 				SetAlarmState(AIStateFlag.Alarmed);
 				break;
 			default:
-				TaleWorlds.Library.Debug.FailedAssert("false", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.MountAndBlade\\Agent.cs", "CurrentWatchState", 929);
+				TaleWorlds.Library.Debug.FailedAssert("false", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.MountAndBlade\\Agent.cs", "CurrentWatchState", 933);
 				break;
 			}
 		}
@@ -1261,7 +1267,7 @@ public sealed class Agent : DotNetObject, IAgent, IFocusable, IUsable, IFormatio
 			{
 				return Team.Color;
 			}
-			TaleWorlds.Library.Debug.FailedAssert("Clothing color is not set.", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.MountAndBlade\\Agent.cs", "ClothingColor1", 1146);
+			TaleWorlds.Library.Debug.FailedAssert("Clothing color is not set.", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.MountAndBlade\\Agent.cs", "ClothingColor1", 1150);
 			return uint.MaxValue;
 		}
 	}
@@ -1750,6 +1756,7 @@ public sealed class Agent : DotNetObject, IAgent, IFocusable, IUsable, IFormatio
 		}
 		UpdateAgentStats();
 		OnAgentMountedStateChanged?.Invoke();
+		_isBracingCacheValid = false;
 		if (GameNetwork.IsServerOrRecorder)
 		{
 			mount.SyncHealthToClients();
@@ -1769,6 +1776,7 @@ public sealed class Agent : DotNetObject, IAgent, IFocusable, IUsable, IFormatio
 			component.OnDismount(mount);
 		}
 		Mission.OnAgentDismount(this);
+		_isBracingCacheValid = false;
 		if (IsActive())
 		{
 			UpdateAgentStats();
@@ -2041,6 +2049,56 @@ public sealed class Agent : DotNetObject, IAgent, IFocusable, IUsable, IFormatio
 		return MBAPI.IMBAgent.GetAIMoveStopTolerance(GetPtr());
 	}
 
+	public bool GetBaseFormationFrame(out WorldPosition formationPosition, out Vec2 formationDirection)
+	{
+		bool result = false;
+		if (Formation != null && ((MovementMode & AgentMovementMode.WaterDiving) == AgentMovementMode.Land || Mission.IsTeleportingAgents))
+		{
+			formationPosition = Formation.GetOrderPositionOfUnit(this);
+			if (IsDetachedFromFormation)
+			{
+				Formation formation = Formation;
+				WorldFrame? worldFrame = null;
+				if (formation.GetReadonlyMovementOrderReference().MovementState != MovementOrder.MovementStateEnum.Charge || (Detachment != null && (!Detachment.IsLoose || formationPosition.IsValid)))
+				{
+					worldFrame = formation.GetDetachmentFrame(this);
+				}
+				if (worldFrame.HasValue)
+				{
+					formationDirection = worldFrame.Value.Rotation.f.AsVec2.Normalized();
+					result = true;
+				}
+				else
+				{
+					formationDirection = Vec2.Invalid;
+				}
+			}
+			else
+			{
+				formationDirection = Formation.GetDirectionOfUnit(this);
+				result = formationPosition.IsValid;
+			}
+		}
+		else
+		{
+			formationPosition = WorldPosition.Invalid;
+			formationDirection = Vec2.Invalid;
+		}
+		if (formationPosition.IsValid && formationPosition.GetNavMeshMT() == UIntPtr.Zero)
+		{
+			UIntPtr nearestNavMesh = formationPosition.GetNearestNavMesh();
+			if (nearestNavMesh != UIntPtr.Zero)
+			{
+				Vec2 vec = Mission.Current.Scene.FindClosestExitPositionForPositionOnABoundaryFace(formationPosition.GetVec3WithoutValidity(), nearestNavMesh);
+				if (vec.IsValid)
+				{
+					formationPosition.SetVec2(vec);
+				}
+			}
+		}
+		return result;
+	}
+
 	public bool IsAIAtMoveDestination()
 	{
 		float aIMoveStartTolerance = GetAIMoveStartTolerance();
@@ -2179,6 +2237,12 @@ public sealed class Agent : DotNetObject, IAgent, IFocusable, IUsable, IFormatio
 		}
 	}
 
+	public void OnConversationStarted()
+	{
+		SetActionChannel(0, in ActionIndexCache.act_none, ignorePriority: false, AnimFlags.amf_priority_reload, 0f, 1f, 0f);
+		SetActionChannel(1, in ActionIndexCache.act_none, ignorePriority: false, AnimFlags.amf_priority_reload, 0f, 1f, 0f);
+	}
+
 	public void SetCrouchMode(bool set)
 	{
 		if (set)
@@ -2956,6 +3020,13 @@ public sealed class Agent : DotNetObject, IAgent, IFocusable, IUsable, IFormatio
 		return MBAPI.IMBAgent.GetBoneEntitialFrameAtAnimationProgress(GetPtr(), boneIndex, animationIndex, progress);
 	}
 
+	public MatrixFrame GetBoneEntitialFrame(sbyte boneIndex, bool useBoneMapping)
+	{
+		MatrixFrame outFrame = MatrixFrame.Identity;
+		MBAPI.IMBAgent.GetBoneEntitialFrame(GetPtr(), boneIndex, useBoneMapping, ref outFrame);
+		return outFrame;
+	}
+
 	pub
... (truncated, 15413 chars total)
```
</details>

### AllianceCampaignBehavior (+95 lines)

- Old: 690 lines | New: 785 lines
- Path: `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs`

**Signature changes (public/protected API):**
```diff
+	private const int BreakingAllianceRelationPenalty = -100;
+	private const int DenyingCallToWarRelationPenalty = -50;
+	private const int AcceptingCallToWarRelationBonus = 10;
+	public void OnAllianceOfferedToPlayer(Kingdom offeringKingdom)
+	public void OnAllianceOfferedToPlayerKingdom(Kingdom offeringKingdom)
+	public void OnCallToWarAgreementProposedToPlayer(Kingdom proposerKingdom, Kingdom kingdomToCallToWarAgainst)
+	public void OnCallToWarAgreementProposedToPlayerKingdom(Kingdom proposerKingdom, Kingdom kingdomToCallToWarAgainst)
+	public void OnCallToWarAgreementProposedByPlayer(Kingdom proposedKingdom, Kingdom kingdomToCallToWarAgainst)
+	public CampaignTime GetAllianceEndDate(Kingdom kingdom1, Kingdom kingdom2)
-	public bool IsAtWarByCallToWarAgreement(Kingdom calledKingdom, Kingdom kingdomToCallToWarAgainst)
+	public bool IsAtWarByCallToWarAgreement(Kingdom calledKingdom, Kingdom kingdomToCallToWarAgainst, out Kingdom callingKingdom)
+	public void DenyCallToWarAgreement(Kingdom callingKingdom, Kingdom calledKingdom)
-	private void AcceptProposalOfCallToWarAgreement(Kingdom proposedKingdom, Kingdom kingdomToCallToWarAgainst, int callToWarCost)
-	private void AcceptCallToWarAgreement(Kingdom proposerKingdom, Kingdom kingdomToCallToWarAgainst, int callToWarCost)
+	private void UpdateAllianceEndTime(Kingdom kingdom1, Kingdom kingdom2, CampaignTime newEndTime)
+	private void ApplyBrokenAlliancePenalty(Kingdom kingdom, Kingdom otherKingdom, DeclareWarAction.DeclareWarDetail detail)
+	private void ApplyDenyingCallToWarOfferPenalty(Kingdom callingKingdom, Kingdom calledKingdom)
+	private void ApplyAcceptingCallToWarOfferBonus(Kingdom callingKingdom, Kingdom calledKingdom)
+	private void AddAllianceDecision(Kingdom kingdomToAddDecision, Kingdom kingdomToOffer)
+	private static void RefreshAlliedKingdoms()
+	private void OnGameLoadFinished()
+	private void OnNewGameCreated(CampaignGameStarter starter)
+	private void OnGameLoaded(CampaignGameStarter starter)
```

<details>
<summary>Full diff (95 line delta)</summary>

```diff
@@ -1,6 +1,7 @@
 using System.Collections.Generic;
 using System.Linq;
 using TaleWorlds.CampaignSystem.Actions;
+using TaleWorlds.CampaignSystem.CharacterDevelopment;
 using TaleWorlds.CampaignSystem.Election;
 using TaleWorlds.CampaignSystem.Extensions;
 using TaleWorlds.CampaignSystem.MapNotificationTypes;
@@ -136,6 +137,12 @@ public class AllianceCampaignBehavior : CampaignBehaviorBase, IAllianceCampaignB
 		}
 	}
 
+	private const int BreakingAllianceRelationPenalty = -100;
+
+	private const int DenyingCallToWarRelationPenalty = -50;
+
+	private const int AcceptingCallToWarRelationBonus = 10;
+
 	private List<Alliance> _alliances = new List<Alliance>();
 
 	private List<CallToWarAgreement> _callToWarAgreements = new List<CallToWarAgreement>();
@@ -146,6 +153,9 @@ public class AllianceCampaignBehavior : CampaignBehaviorBase, IAllianceCampaignB
 		CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
 		CampaignEvents.MakePeace.AddNonSerializedListener(this, OnMakePeace);
 		CampaignEvents.KingdomDestroyedEvent.AddNonSerializedListener(this, OnKingdomDestroyed);
+		CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(this, OnGameLoadFinished);
+		CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
+		CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
 	}
 
 	public override void SyncData(IDataStore dataStore)
@@ -154,7 +164,7 @@ public class AllianceCampaignBehavior : CampaignBehaviorBase, IAllianceCampaignB
 		dataStore.SyncData("_callToWarAgreements", ref _callToWarAgreements);
 	}
 
-	void IAllianceCampaignBehavior.OnAllianceOfferedToPlayer(Kingdom offeringKingdom)
+	public void OnAllianceOfferedToPlayer(Kingdom offeringKingdom)
 	{
 		if (Clan.PlayerClan.Kingdom.Clans.Count == 1)
 		{
@@ -183,24 +193,21 @@ public class AllianceCampaignBehavior : CampaignBehaviorBase, IAllianceCampaignB
 		}
 	}
 
-	void IAllianceCampaignBehavior.OnAllianceOfferedToPlayerKingdom(Kingdom offeringKingdom)
+	public void OnAllianceOfferedToPlayerKingdom(Kingdom offeringKingdom)
 	{
 		if (Clan.PlayerClan.Kingdom.Clans.Count == 1)
 		{
 			TextObject textObject = new TextObject("{=1V8f9vRM}A courier bearing an alliance offer from the {PROPOSER_KINGDOM} has arrived at the court of your realm.");
 			textObject.SetTextVariable("PROPOSER_KINGDOM", offeringKingdom.InformalName);
 			Campaign.Current.CampaignInformationManager.NewMapNoticeAdded(new AllianceOfferMapNotification(offeringKingdom, textObject));
-			return;
 		}
-		KingdomDecision kingdomDecision = Clan.PlayerClan.Kingdom.UnresolvedDecisions.FirstOrDefault((KingdomDecision s) => s is StartAllianceDecision startAllianceDecision && startAllianceDecision.KingdomToStartAllianceWith == offeringKingdom);
-		if (kingdomDecision != null)
+		else
 		{
-			Clan.PlayerClan.Kingdom.RemoveDecision(kingdomDecision);
+			AddAllianceDecision(Clan.PlayerClan.Kingdom, offeringKingdom);
 		}
-		Clan.PlayerClan.Kingdom.AddDecision(new StartAllianceDecision(StartAllianceDecision.GetProposerClanForPlayerKingdom(offeringKingdom), offeringKingdom), ignoreInfluenceCost: true);
 	}
 
-	void IAllianceCampaignBehavior.OnCallToWarAgreementProposedToPlayer(Kingdom proposerKingdom, Kingdom kingdomToCallToWarAgainst)
+	public void OnCallToWarAgreementProposedToPlayer(Kingdom proposerKingdom, Kingdom kingdomToCallToWarAgainst)
 	{
 		if (Clan.PlayerClan.Kingdom.Clans.Count == 1)
 		{
@@ -213,12 +220,15 @@ public class AllianceCampaignBehavior : CampaignBehaviorBase, IAllianceCampaignB
 			textObject2.SetTextVariable("CALLING_KINGDOM", proposerKingdom.Name);
 			textObject2.SetTextVariable("KINGDOM_TO_CALL_TO_WAR_AGAINST", kingdomToCallToWarAgainst.Name);
 			textObject2.SetTextVariable("CALL_TO_WAR_COST", callToWarCost);
-			textObject2.SetTextVariable("GOLD_ICON", "{=!}<img src=\"General\\Icons\\Coin@2x\" extend=\"8\">");
+			textObject2.SetTextVariable("GOLD_ICON", "{=!}<img src=\"General\\Icons\\Coin@2x\" extend=\"6\">");
 			TextObject textObject4 = new TextObject("{=Y94H6XnK}Accept");
 			InformationManager.ShowInquiry(new InquiryData(negativeText: new TextObject("{=cOgmdp9e}Decline").ToString(), titleText: textObject.ToString(), text: textObject2.ToString(), isAffirmativeOptionShown: true, isNegativeOptionShown: true, affirmativeText: textObject4.ToString(), affirmativeAction: delegate
 			{
-				AcceptCallToWarAgreement(proposerKingdom, kingdomToCallToWarAgainst, callToWarCost);
-			}, negativeAction: null));
+				StartCallToWarAgreement(proposerKingdom, Clan.PlayerClan.Kingdom, kingdomToCallToWarAgainst, callToWarCost);
+			}, negativeAction: delegate
+			{
+				DenyCallToWarAgreement(proposerKingdom, Clan.PlayerClan.Kingdom);
+			}));
 		}
 		else
 		{
@@ -234,7 +244,7 @@ public class AllianceCampaignBehavior : CampaignBehaviorBase, IAllianceCampaignB
 		}
 	}
 
-	void IAllianceCampaignBehavior.OnCallToWarAgreementProposedToPlayerKingdom(Kingdom proposerKingdom, Kingdom kingdomToCallToWarAgainst)

... (truncated, 24253 chars total)
```
</details>

### Hero (+91 lines)

- Old: 2363 lines | New: 2454 lines
- Path: `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs`

**Signature changes (public/protected API):**
```diff
+	private Clan _originClan;
+	public Clan OriginClan => _originClan;
+	internal static object AutoGeneratedGetMemberValue_originClan(object o)
```

<details>
<summary>Full diff (91 line delta)</summary>

```diff
@@ -102,6 +102,9 @@ public sealed class Hero : MBObjectBase, ITrackableCampaignObject, ITrackableBas
 	[SaveableField(500)]
 	private Clan _clan;
 
+	[SaveableField(501)]
+	private Clan _originClan;
+
 	[SaveableField(510)]
 	private Clan _supporterOf;
 
@@ -491,6 +494,8 @@ public sealed class Hero : MBObjectBase, ITrackableCampaignObject, ITrackableBas
 	[SaveableProperty(481)]
 	public long LastExaminedLogEntryID { get; set; }
 
+	public Clan OriginClan => _originClan;
+
 	public Clan Clan
 	{
 		get
@@ -501,6 +506,10 @@ public sealed class Hero : MBObjectBase, ITrackableCampaignObject, ITrackableBas
 		{
 			if (_clan != value)
 			{
+				if (_clan == null)
+				{
+					_originClan = value;
+				}
 				_homeSettlement = null;
 				if (_clan != null)
 				{
@@ -908,6 +917,7 @@ public sealed class Hero : MBObjectBase, ITrackableCampaignObject, ITrackableBas
 		CampaignTime.AutoGeneratedStaticCollectObjectsCampaignTime(_birthDay, collectedObjects);
 		CampaignTime.AutoGeneratedStaticCollectObjectsCampaignTime(_deathDay, collectedObjects);
 		collectedObjects.Add(_clan);
+		collectedObjects.Add(_originClan);
 		collectedObjects.Add(_supporterOf);
 		collectedObjects.Add(_governorOf);
 		collectedObjects.Add(_ownedWorkshops);
@@ -1142,6 +1152,11 @@ public sealed class Hero : MBObjectBase, ITrackableCampaignObject, ITrackableBas
 		return ((Hero)o)._clan;
 	}
 
+	internal static object AutoGeneratedGetMemberValue_originClan(object o)
+	{
+		return ((Hero)o)._originClan;
+	}
+
 	internal static object AutoGeneratedGetMemberValue_supporterOf(object o)
 	{
 		return ((Hero)o)._supporterOf;
@@ -1440,8 +1455,10 @@ public sealed class Hero : MBObjectBase, ITrackableCampaignObject, ITrackableBas
 
 	public void ClearPerks()
 	{
-		_heroPerks.ClearAllProperty();
-		HitPoints = TaleWorlds.Library.MathF.Min(HitPoints, MaxHitPoints);
+		foreach (SkillObject property in _heroSkills.GetProperties())
+		{
+			PerkHelper.ClearPerksForSkill(this, property);
+		}
 	}
 
 	public Hero(string stringId, CharacterObject characterObject, CampaignTime birthDay)
@@ -1545,6 +1562,10 @@ public sealed class Hero : MBObjectBase, ITrackableCampaignObject, ITrackableBas
 				Name.SetTextVariable("FEMALE", IsFemale ? 1 : 0);
 			}
 		}
+		if (MBSaveLoad.IsUpdatingGameVersion && MBSaveLoad.LastLoadedGameVersion.IsOlderThan(ApplicationVersion.FromString("v1.4.0")) && Name.Attributes != null && Name.Attributes.ContainsKey("FIRSTNAME") && Name.Attributes["FIRSTNAME"] is TextObject textObject && textObject != null && (object)Name == textObject)
+		{
+			Name.Attributes["FIRSTNAME"] = new TextObject(Name.Value);
+		}
 		if (MBSaveLoad.IsUpdatingGameVersion && MBSaveLoad.LastLoadedGameVersion.IsOlderThan(ApplicationVersion.FromString("v1.2.8.31599")) && !CharacterObject.IsTemplate && !CharacterObject.HiddenInEncyclopedia && PartyBelongedTo != null && PartyBelongedTo.LeaderHero != this && (CharacterObject.Occupation == Occupation.Soldier || CharacterObject.Occupation == Occupation.Mercenary || CharacterObject.Occupation == Occupation.Bandit || CharacterObject.Occupation == Occupation.Gangster || CharacterObject.Occupation == Occupation.CaravanGuard || (CharacterObject.Occupation == Occupation.Villager && CharacterObject.UpgradeTargets.Length != 0)))
 		{
 			PartyBelongedTo.MemberRoster.AddToCounts(CharacterObject, -PartyBelongedTo.MemberRoster.GetTroopCount(CharacterObject));
@@ -1587,6 +1608,17 @@ public sealed class Hero : MBObjectBase, ITrackableCampaignObject, ITrackableBas
 				ChangeState(CharacterStates.Fugitive);
 			}
 		}
+		if (MBSaveLoad.IsUpdatingGameVersion && MBSaveLoad.LastLoadedGameVersion < ApplicationVersion.FromString("v1.4.0") && OriginClan == null)
+		{
+			if (Father != null)
+			{
+				_originClan = Father.Clan;
+			}
+			else
+			{
+				_originClan = Clan;
+			}
+		}
 		UpdatePowerModifier();
 	}
 
@@ -1688,31 +1720,85 @@ public sealed class Hero : MBObjectBase, ITrackableCampaignObject, ITrackableBas
 			}
 			UpdateHomeSettlement();
 		}
-		if (!MBSaveLoad.IsUpdatingGameVersion || !MBSaveLoad.LastLoadedGameVersion.IsOlderThan(ApplicationVersion.FromString("v1.2.8.31599")) || CharacterObject.IsTemplate || CharacterObject.HiddenInEncyclopedia || (CharacterObject.Occupation != Occupation.Soldier && CharacterObject.Occupation != Occupation.Mercenary && CharacterObject.Occupation != Occupation.Bandit && CharacterObject.Occupation != Occupation.Gangster && CharacterObject.Occupation != Occupation.CaravanGuard && (CharacterObject.Occupation != Occupation.Villager || CharacterObject.UpgradeTargets.Length == 0)))
+		if (MBSaveLoad.IsUpdatingGameVersion && MBSaveLoad.LastLoadedGameVersion.IsOlderThan(ApplicationVersion.FromString("v1.2.8.31599")) && !CharacterObject.IsTemplate && !CharacterObject.HiddenInEncyclopedia && (CharacterObject.Occupation == Occupation.Soldier || CharacterObject.Occupation == Occupation.Mercenary || CharacterObject.Occupation == Occupation.Bandit || CharacterObject.Occupation == Occupation.Gangster || Char
... (truncated, 9685 chars total)
```
</details>

### DefaultClanFinanceModel (+68 lines)

- Old: 915 lines | New: 983 lines
- Path: `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultClanFinanceModel.cs`

**Signature changes (public/protected API):**
```diff
-	private static readonly string _tributeIncomeStr = "str_finance_tribute_income";
+	private static readonly TextObject _projectsIncomeText = Game.Current.GameTextManager.FindText("str_finance_projects_income");
-	private static readonly TextObject _projectsIncomeText = Game.Current.GameTextManager.FindText("str_finance_projects_income");
+	private static readonly string _tributeIncomeStr = "str_finance_tribute_income";
+	private ITradeAgreementsCampaignBehavior _tradeAgreementsCampaignBehavior;
+	private ITradeAgreementsCampaignBehavior TradeAgreementsBehavior
+	private void AddIncomeFromTradeAgreements(Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals, bool includeDetails)
+	private void AddIncomeFromTradeAgreements(Clan clan, ref TradeAgreementsCampaignBehavior.TradeAgreement tradeAgreement, ref ExplainedNumber goldChange, bool applyWithdrawals, bool includeDetails)
```

<details>
<summary>Full diff (68 line delta)</summary>

```diff
@@ -1,5 +1,6 @@
 using System.Linq;
 using Helpers;
+using TaleWorlds.CampaignSystem.CampaignBehaviors;
 using TaleWorlds.CampaignSystem.CharacterDevelopment;
 using TaleWorlds.CampaignSystem.ComponentInterfaces;
 using TaleWorlds.CampaignSystem.Party;
@@ -36,22 +37,22 @@ public class DefaultClanFinanceModel : ClanFinanceModel
 
 	private static readonly string _partyExpensesStr = "str_finance_party_expenses";
 
-	private static readonly string _tributeIncomeStr = "str_finance_tribute_income";
-
 	private static readonly string _tariffTaxStr = "str_finance_tariff_tax";
 
+	private static readonly TextObject _projectsIncomeText = Game.Current.GameTextManager.FindText("str_finance_projects_income");
+
 	private static readonly string _caravanIncomeStr = "str_finance_caravan_income";
 
 	private static readonly string _convoyIncomeStr = "str_finance_convoy_income";
 
-	private static readonly TextObject _projectsIncomeText = Game.Current.GameTextManager.FindText("str_finance_projects_income");
-
 	private static readonly TextObject _shopExpenseText = Game.Current.GameTextManager.FindText("str_finance_shop_expense");
 
 	private static readonly TextObject _mercenaryText = Game.Current.GameTextManager.FindText("str_finance_mercenary");
 
 	private static readonly TextObject _mercenaryExpensesText = Game.Current.GameTextManager.FindText("str_finance_mercenary_expenses");
 
+	private static readonly string _tributeIncomeStr = "str_finance_tribute_income";
+
 	private static readonly TextObject _tributeExpensesText = Game.Current.GameTextManager.FindText("str_finance_tribute_expenses");
 
 	private static readonly TextObject _tributeIncomes = Game.Current.GameTextManager.FindText("str_finance_tribute_incomes");
@@ -80,6 +81,8 @@ public class DefaultClanFinanceModel : ClanFinanceModel
 
 	private static readonly TextObject _shopIncomeText = Game.Current.GameTextManager.FindText("str_finance_shop_income");
 
+	private ITradeAgreementsCampaignBehavior _tradeAgreementsCampaignBehavior;
+
 	private const int PartyGoldIncomeThreshold = 10000;
 
 	private const int payGarrisonWagesTreshold = 8000;
@@ -88,6 +91,18 @@ public class DefaultClanFinanceModel : ClanFinanceModel
 
 	private const int payLeaderPartyWageTreshold = 2000;
 
+	private ITradeAgreementsCampaignBehavior TradeAgreementsBehavior
+	{
+		get
+		{
+			if (_tradeAgreementsCampaignBehavior == null)
+			{
+				_tradeAgreementsCampaignBehavior = Campaign.Current.GetCampaignBehavior<ITradeAgreementsCampaignBehavior>();
+			}
+			return _tradeAgreementsCampaignBehavior;
+		}
+	}
+
 	public override int PartyGoldLowerThreshold => 5000;
 
 	public override ExplainedNumber CalculateClanGoldChange(Clan clan, bool includeDescriptions = false, bool applyWithdrawals = false, bool includeDetails = false)
@@ -107,41 +122,46 @@ public class DefaultClanFinanceModel : ClanFinanceModel
 
 	private void CalculateClanIncomeInternal(Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals = false, bool includeDetails = false)
 	{
-		if (!clan.IsEliminated)
+		if (clan.IsEliminated)
 		{
-			if (clan.Kingdom?.RulingClan == clan)
-			{
-				AddRulingClanIncome(clan, ref goldChange, applyWithdrawals, includeDetails);
-			}
-			if (clan != Clan.PlayerClan && (!clan.MapFaction.IsKingdomFaction || clan.IsUnderMercenaryService) && clan.Fiefs.Count == 0)
-			{
-				int num = clan.Tier * (80 + (clan.IsUnderMercenaryService ? 40 : 0));
-				goldChange.Add(num);
-			}
-			AddMercenaryIncome(clan, ref goldChange, applyWithdrawals);
-			AddSettlementIncome(clan, ref goldChange, applyWithdrawals, includeDetails);
-			CalculateHeroIncomeFromWorkshops(clan.Leader, ref goldChange, applyWithdrawals);
-			AddIncomeFromParties(clan, ref goldChange, applyWithdrawals, includeDetails);
-			if (clan == Clan.PlayerClan)
-			{
-				AddPlayerClanIncomeFromOwnedAlleys(ref goldChange);
-			}
-			if (!clan.IsUnderMercenaryService)
-			{
-				AddIncomeFromTribute(clan, ref goldChange, applyWithdrawals, includeDetails);
-				AddIncomeFromCallToWarAgrements(clan, ref goldChange, applyWithdrawals);
-			}
-			if (clan.Gold < 30000 && clan.Kingdom != null && clan.Leader != Hero.MainHero && !clan.IsUnderMercenaryService)
-			{
-				AddIncomeFromKingdomBudget(clan, ref goldChange, applyWithdrawals);
-			}
-			Hero leader = clan.Leader;
-			if (leader != null && leader.GetPerkValue(DefaultPerks.Trade.SpringOfGold))
+			return;
+		}
+		if (clan.Kingdom?.RulingClan == clan)
+		{
+			AddRulingClanIncome(clan, ref goldChange, applyWithdrawals, includeDetails);
+		}
+		if (clan != Clan.PlayerClan && (!clan.MapFaction.IsKingdomFaction || clan.IsUnderMercenaryService) && clan.Fiefs.Count == 0)
+		{
+			int num = clan.Tier * (80 + (clan.IsUnderMercenaryService ? 40 : 0));
+			goldChange.Add(num);
+		}
+		AddMercenaryIncome(clan, ref goldChange, applyWithdrawals);
+		AddSettlementIncome(clan, ref goldChange, applyWithdrawals, includeDetails);
+		CalculateHeroIncomeFromWorkshops(clan.Leader, ref goldChange, applyWithdraw
... (truncated, 11237 chars total)
```
</details>

### SPInventoryVM (+56 lines)

- Old: 5357 lines | New: 5413 lines
- Path: `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.Inventory\SPInventoryVM.cs`

**Signature changes (public/protected API):**
```diff
+	private string _noSaddleText;
-	private string _noSaddleText;
+	private bool _isMainPartyLandCapacityWarned;
-	private bool _isMainPartyLandCapacityWarned;
-	private bool CanCharacterUserItemBasedOnUsability(ItemRosterElement itemRosterElement)
+	private bool CanCharacterUseItem(ItemRosterElement itemRosterElement, out TextObject reason)
-	private bool CanCharacterUseItemBasedOnSkills(ItemRosterElement itemRosterElement)
+	private bool CanCharacterUseItem(ItemRosterElement itemRosterElement)
+	private void UpdateCharacterArmorColor()
-	public void ExecuteResetAndCompleteTranstactions()
+	public void ExecuteResetAndCompleteTranstactionsWithoutInquiry()
+	public void ExecuteResetAndCompleteTranstactions(bool showCancelInquiry = false)
```

<details>
<summary>Full diff (56 line delta)</summary>

```diff
@@ -353,16 +353,16 @@ public class SPInventoryVM : ViewModel
 
 	private bool _playerEquipmentCountWarned;
 
+	private string _noSaddleText;
+
 	private string _mainPartyLandCapacityText;
 
 	private string _mainPartySeaCapacityText;
 
-	private string _noSaddleText;
+	private bool _isMainPartyLandCapacityWarned;
 
 	private string _leftSearchText = "";
 
-	private bool _isMainPartyLandCapacityWarned;
-
 	private bool _isMainPartySeaCapacityWarned;
 
 	private bool _showMainPartyLandCapacityWarning;
@@ -3238,7 +3238,7 @@ public class SPInventoryVM : ViewModel
 		SetSellAllHint();
 		if (_usageType == InventoryScreenHelper.InventoryMode.Loot || _usageType == InventoryScreenHelper.InventoryMode.Stash)
 		{
-			SellHint = new HintViewModel(GameTexts.FindText("str_inventory_give"));
+			SellHint = new HintViewModel(GameTexts.FindText("str_give"));
 		}
 		else if (_usageType == InventoryScreenHelper.InventoryMode.Default)
 		{
@@ -3475,7 +3475,7 @@ public class SPInventoryVM : ViewModel
 	private void ProcessEquipItem(ItemVM draggedItem)
 	{
 		SPItemVM sPItemVM = draggedItem as SPItemVM;
-		if ((sPItemVM.IsCivilianItem || _equipmentMode != EquipmentModes.Civilian) && (sPItemVM.IsStealthItem || _equipmentMode != EquipmentModes.Stealth) && (sPItemVM.IsTransferable || _currentCharacter.IsPlayerCharacter))
+		if (sPItemVM.IsTransferable || _currentCharacter.IsPlayerCharacter)
 		{
 			IsRefreshed = false;
 			EquipEquipment(sPItemVM);
@@ -3516,7 +3516,7 @@ public class SPInventoryVM : ViewModel
 		}
 		if (TransactionCount == 0)
 		{
-			Debug.FailedAssert("Transaction count should not be zero", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem.ViewModelCollection\\Inventory\\SPInventoryVM.cs", "ProcessBuyItem", 640);
+			Debug.FailedAssert("Transaction count should not be zero", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem.ViewModelCollection\\Inventory\\SPInventoryVM.cs", "ProcessBuyItem", 634);
 			return;
 		}
 		IsRefreshed = false;
@@ -3555,7 +3555,7 @@ public class SPInventoryVM : ViewModel
 		}
 		if (TransactionCount == 0)
 		{
-			Debug.FailedAssert("Transaction count should not be zero", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem.ViewModelCollection\\Inventory\\SPInventoryVM.cs", "ProcessSellItem", 690);
+			Debug.FailedAssert("Transaction count should not be zero", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem.ViewModelCollection\\Inventory\\SPInventoryVM.cs", "ProcessSellItem", 684);
 			return;
 		}
 		IsRefreshed = false;
@@ -3877,12 +3877,12 @@ public class SPInventoryVM : ViewModel
 				SPItemVM sPItemVM2 = null;
 				if (transferCommandResult.ResultSide == InventoryLogic.InventorySide.OtherInventory)
 				{
-					newItem = new SPItemVM(_inventoryLogic, MainCharacter.IsFemale, CanCharacterUseItemBasedOnSkills(transferCommandResult.EffectedItemRosterElement), _usageType, transferCommandResult.EffectedItemRosterElement, InventoryLogic.InventorySide.OtherInventory, _inventoryLogic.GetCostOfItemRosterElement(transferCommandResult.EffectedItemRosterElement, transferCommandResult.ResultSide), null);
+					newItem = new SPItemVM(_inventoryLogic, MainCharacter.IsFemale, CanCharacterUseItem(transferCommandResult.EffectedItemRosterElement), _usageType, transferCommandResult.EffectedItemRosterElement, InventoryLogic.InventorySide.OtherInventory, _inventoryLogic.GetCostOfItemRosterElement(transferCommandResult.EffectedItemRosterElement, transferCommandResult.ResultSide), null);
 					sPItemVM2 = RightItemListVM.FirstOrDefault((SPItemVM x) => x.ItemRosterElement.EquipmentElement.IsEqualTo(newItem.ItemRosterElement.EquipmentElement));
 				}
 				else
 				{
-					newItem = new SPItemVM(_inventoryLogic, MainCharacter.IsFemale, CanCharacterUseItemBasedOnSkills(transferCommandResult.EffectedItemRosterElement), _usageType, transferCommandResult.EffectedItemRosterElement, InventoryLogic.InventorySide.PlayerInventory, _inventoryLogic.GetCostOfItemRosterElement(transferCommandResult.EffectedItemRosterElement, transferCommandResult.ResultSide), null);
+					newItem = new SPItemVM(_inventoryLogic, MainCharacter.IsFemale, CanCharacterUseItem(transferCommandResult.EffectedItemRosterElement), _usageType, transferCommandResult.EffectedItemRosterElement, InventoryLogic.InventorySide.PlayerInventory, _inventoryLogic.GetCostOfItemRosterElement(transferCommandResult.EffectedItemRosterElement, transferCommandResult.ResultSide), null);
 					sPItemVM2 = LeftItemListVM.FirstOrDefault((SPItemVM x) => x.ItemRosterElement.EquipmentElement.IsEqualTo(newItem.ItemRosterElement.EquipmentElement));
 				}
 				UpdateFilteredStatusOfItem(newItem);
@@ -3902,7 +3902,7 @@ public class SPInventoryVM : ViewModel
 				SPItemVM sPItemVM3 = null;
 				if (transferCommandResult.FinalNumber > 0)
 				{
-					sPItemVM3 = new SPItemVM(_inventoryLogic, MainCharacter.IsFemale, CanCharacterUseItemBasedOnSkills(transferCommandResult.EffectedIte
... (truncated, 17852 chars total)
```
</details>

### Mission (+53 lines)

- Old: 6957 lines | New: 7010 lines
- Path: `E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs`

**Signature changes (public/protected API):**
```diff
+	private enum GetNearbyAgentsAuxType
-	private enum GetNearbyAgentsAuxType
+	public delegate void OnCameraShakeTriggeredDelegate(in Vec3 position, float radius);
+	private static readonly object GetNearbyAgentsAuxLock = new object();
-	private static readonly object GetNearbyAgentsAuxLock = new object();
+	public event OnCameraShakeTriggeredDelegate OnCameraShakeTriggered;
+	public event Action DeploymentFinishedEvent;
-	private void AssertTimeSpeedRequestDoesntExist(TimeSpeedRequest request)
+	private void AssertTimeSpeedRequestDoesNotExist(TimeSpeedRequest request)
-	internal void OnEntityHit(WeakGameEntity entity, Agent attackerAgent, int inflictedDamage, DamageTypes damageType, Vec3 impactPosition, Vec3 impactDirection, in MissionWeapon weapon, int affectorWeaponSlotOrMissileIndex, ref CombatLogData combatLog)
+	internal void OnEntityHit(WeakGameEntity entity, Agent attackerAgent, AttackCollisionData collisionData, int inflictedDamage, DamageTypes damageType, Vec3 impactPosition, Vec3 impactDirection, in MissionWeapon weapon, int affectorWeaponSlotOrMissileIndex, ref CombatLogData combatLog)
+	public bool TryGetMissileVelocityFromMissileIndex(int missileIndex, out Vec3 velocity)
-	public void GetFormationSpawnFrame(Team team, FormationClass formationClass, bool isReinforcement, out WorldPosition spawnPosition, out Vec2 spawnDirection)
+	public void GetFormationSpawnFrame(Team team, FormationClass formationClass, bool isReinforcement, out WorldPosition spawnPosition, out Vec2 spawnDirection, bool useDefaultClassIfNotFound = true)
-	public static float GetBattleSizeOffset(int battleSize, Path path)
-	public static float GetPathOffsetFromDistance(float distance, Path path)
+	public static float ComputeSpawnPathDeploymentOffset(int troopCount, Path path)
-	public static float GetBattleSizeFactor(int battleSize, float normalizationFactor)
```

<details>
<summary>Full diff (53 line delta)</summary>

```diff
@@ -280,6 +280,13 @@ public sealed class Mission : DotNetObject, IMission
 		}
 	}
 
+	private enum GetNearbyAgentsAuxType
+	{
+		Friend = 1,
+		Enemy,
+		All
+	}
+
 	public class DynamicallyCreatedEntity
 	{
 		public string Prefab;
@@ -373,13 +380,6 @@ public sealed class Mission : DotNetObject, IMission
 		}
 	}
 
-	private enum GetNearbyAgentsAuxType
-	{
-		Friend = 1,
-		Enemy,
-		All
-	}
-
 	public static class MissionNetworkHelper
 	{
 		public static Agent GetAgentFromIndex(int agentIndex, bool canBeNull = false)
@@ -463,6 +463,7 @@ public sealed class Mission : DotNetObject, IMission
 			result.IsRangedAttack = message.IsRangedAttack;
 			result.IsFriendlyFire = message.IsFriendlyFire;
 			result.IsFatalDamage = message.IsFatalDamage;
+			result.IsSpecialDamage = message.IsSpecialDamage;
 			result.BodyPartHit = message.BodyPartHit;
 			result.HitSpeed = message.HitSpeed;
 			result.InflictedDamage = message.InflictedDamage;
@@ -577,7 +578,7 @@ public sealed class Mission : DotNetObject, IMission
 				num10 = 0.4f * weight * 0.4f * 0.4f;
 				break;
 			default:
-				TaleWorlds.Library.Debug.FailedAssert("Unknown missile type!", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.MountAndBlade\\Missions\\Mission.cs", "CalculateBounceBackVelocity", 272);
+				TaleWorlds.Library.Debug.FailedAssert("Unknown missile type!", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.MountAndBlade\\Missions\\Mission.cs", "CalculateBounceBackVelocity", 275);
 				num10 = 0f;
 				break;
 			}
@@ -667,7 +668,8 @@ public sealed class Mission : DotNetObject, IMission
 		RemoveEquippedWeapon,
 		TryToWieldWeaponInSlot,
 		DropItem,
-		RegisterDrownBlow
+		RegisterDrownBlow,
+		RegisterBurnBlow
 	}
 
 	public delegate void OnBeforeAgentRemovedDelegate(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow killingBlow);
@@ -676,6 +678,8 @@ public sealed class Mission : DotNetObject, IMission
 
 	public delegate void OnMainAgentChangedDelegate(Agent oldAgent);
 
+	public delegate void OnCameraShakeTriggeredDelegate(in Vec3 position, float radius);
+
 	public delegate BodyProperties ComputeTroopBodyPropertiesDelegate(AgentBuildData agentBuildData, BasicCharacterObject characterObject, Equipment equipment, int seed);
 
 	public sealed class TeamCollection : List<Team>
@@ -892,6 +896,8 @@ public sealed class Mission : DotNetObject, IMission
 
 	public const int MaxRuntimeMissionObjects = 8191;
 
+	private static readonly object GetNearbyAgentsAuxLock = new object();
+
 	private int _lastSceneMissionObjectIdCount;
 
 	private int _lastRuntimeMissionObjectIdCount;
@@ -918,8 +924,6 @@ public sealed class Mission : DotNetObject, IMission
 
 	private float _cachedMissionTime;
 
-	private static readonly object GetNearbyAgentsAuxLock = new object();
-
 	public const int MaxNavMeshId = 1000000;
 
 	private const float NavigationMeshHeightLimit = 1.5f;
@@ -1537,10 +1541,14 @@ public sealed class Mission : DotNetObject, IMission
 
 	public event OnMainAgentChangedDelegate OnMainAgentChanged;
 
+	public event OnCameraShakeTriggeredDelegate OnCameraShakeTriggered;
+
 	public event ComputeTroopBodyPropertiesDelegate OnComputeTroopBodyProperties;
 
 	public event Func<BattleSideEnum, BasicCharacterObject, FormationClass> GetAgentTroopClass_Override;
 
+	public event Action DeploymentFinishedEvent;
+
 	public event Action<Agent, SpawnedItemEntity> OnItemPickUp;
 
 	public event Action<Agent, SpawnedItemEntity> OnItemDrop;
@@ -2086,7 +2094,7 @@ public sealed class Mission : DotNetObject, IMission
 	}
 
 	[Conditional("_RGL_KEEP_ASSERTS")]
-	private void AssertTimeSpeedRequestDoesntExist(TimeSpeedRequest request)
+	private void AssertTimeSpeedRequestDoesNotExist(TimeSpeedRequest request)
 	{
 		for (int i = 0; i < _timeSpeedRequests.Count; i++)
 		{
@@ -2110,11 +2118,11 @@ public sealed class Mission : DotNetObject, IMission
 
 	public bool GetRequestedTimeSpeed(int timeSpeedRequestID, out float requestedTime)
 	{
-		for (int i = 0; i < _timeSpeedRequests.Count; i++)
+		foreach (TimeSpeedRequest timeSpeedRequest in _timeSpeedRequests)
 		{
-			if (_timeSpeedRequests[i].RequestID == timeSpeedRequestID)
+			if (timeSpeedRequest.RequestID == timeSpeedRequestID)
 			{
-				requestedTime = _timeSpeedRequests[i].RequestedTimeSpeed;
+				requestedTime = timeSpeedRequest.RequestedTimeSpeed;
 				return true;
 			}
 		}
@@ -2219,20 +2227,26 @@ public sealed class Mission : DotNetObject, IMission
 		}
 	}
 
-	internal void OnEntityHit(WeakGameEntity entity, Agent attackerAgent, int inflictedDamage, DamageTypes damageType, Vec3 impactPosition, Vec3 impactDirection, in MissionWeapon weapon, int affectorWeaponSlotOrMissileIndex, ref CombatLogData combatLog)
+	internal void OnEntityHit(WeakGameEntity entity, Agent attackerAgent, AttackCollisionData collisionData, int inflictedDamage, DamageTypes damageType, Vec3 impactPosition, Vec3 impactDirection, in MissionWeapon weapon, int affectorWeaponSlotOrMissileIndex, ref 
... (truncated, 27216 chars total)
```
</details>

### CombatLogData (+51 lines)

- Old: 364 lines | New: 415 lines
- Path: `E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\CombatLogData.cs`

**Signature changes (public/protected API):**
```diff
+	public int InflictedFireDamage;
+	public int ModifiedFireDamage;
+	private bool IsSpecialSelfDamage
+	public int TotalFireDamage => InflictedFireDamage + ModifiedFireDamage;
```

<details>
<summary>Full diff (51 line delta)</summary>

```diff
@@ -75,6 +75,10 @@ public struct CombatLogData
 
 	public int ModifiedDamage;
 
+	public int InflictedFireDamage;
+
+	public int ModifiedFireDamage;
+
 	public int ReflectedDamage;
 
 	public float Distance;
@@ -99,7 +103,7 @@ public struct CombatLogData
 	{
 		get
 		{
-			if (TotalDamage <= 0 && !CrushedThrough)
+			if (TotalDamage <= 0 && TotalFireDamage <= 0 && !CrushedThrough)
 			{
 				return Chamber;
 			}
@@ -107,6 +111,18 @@ public struct CombatLogData
 		}
 	}
 
+	private bool IsSpecialSelfDamage
+	{
+		get
+		{
+			if (IsSpecialDamage)
+			{
+				return IsVictimAgentSameAsAttackerAgent;
+			}
+			return false;
+		}
+	}
+
 	private bool IsAttackerPlayer
 	{
 		get
@@ -145,12 +161,14 @@ public struct CombatLogData
 
 	public int TotalDamage => InflictedDamage + ModifiedDamage;
 
+	public int TotalFireDamage => InflictedFireDamage + ModifiedFireDamage;
+
 	public float AttackProgress { get; internal set; }
 
 	public List<(string, uint)> GetLogString()
 	{
 		_logStringCache.Clear();
-		if (IsValidForPlayer && ManagedOptions.GetConfig(ManagedOptions.ManagedOptionsType.ReportDamage) > 0f)
+		if (IsValidForPlayer && !IsSpecialSelfDamage && ManagedOptions.GetConfig(ManagedOptions.ManagedOptionsType.ReportDamage) > 0f)
 		{
 			if (IsSneakAttack && IsAttackerPlayer)
 			{
@@ -185,6 +203,7 @@ public struct CombatLogData
 			GameTexts.SetVariable("DAMAGE_TYPE", GameTexts.FindText("combat_log_damage_type", damageType.ToString()));
 			MBStringBuilder mBStringBuilder = default(MBStringBuilder);
 			mBStringBuilder.Initialize(16, "GetLogString");
+			TextObject textObject = null;
 			if (IsEntityToEntityCollisionDamage)
 			{
 				if (IsAttackerPlayer)
@@ -227,28 +246,21 @@ public struct CombatLogData
 			}
 			else if (MissionObjectHit != null)
 			{
-				mBStringBuilder.Append(GameTexts.FindText("ui_delivered_number_damage_to_entity"));
 				WeakGameEntity weakGameEntity = MissionObjectHit.GameEntity;
-				TextObject hitObjectName = MissionObjectHit.HitObjectName;
-				while (weakGameEntity != null && TextObject.IsNullOrEmpty(hitObjectName))
+				textObject = MissionObjectHit.HitObjectName;
+				while (weakGameEntity != null && TextObject.IsNullOrEmpty(textObject))
 				{
-					foreach (MissionObject scriptComponent in weakGameEntity.GetScriptComponents<MissionObject>())
+					int scriptCount = weakGameEntity.GetScriptCount();
+					for (int i = 0; i < scriptCount; i++)
 					{
-						if (TextObject.IsNullOrEmpty(hitObjectName) && !TextObject.IsNullOrEmpty(scriptComponent.HitObjectName))
+						if (weakGameEntity.GetScriptAtIndex(i) is MissionObject missionObject && TextObject.IsNullOrEmpty(textObject) && !TextObject.IsNullOrEmpty(missionObject.HitObjectName))
 						{
-							hitObjectName = scriptComponent.HitObjectName;
+							textObject = missionObject.HitObjectName;
 							break;
 						}
 					}
 					weakGameEntity = weakGameEntity.Parent;
 				}
-				if (!TextObject.IsNullOrEmpty(hitObjectName))
-				{
-					GameTexts.SetVariable("OBJECT_NAME", hitObjectName.ToString());
-					mBStringBuilder.Append("<Detail>");
-					mBStringBuilder.Append(GameTexts.FindText("combat_log_detail_entity_name"));
-					mBStringBuilder.Append("</Detail>");
-				}
 			}
 			else if (IsAttackerMount)
 			{
@@ -260,6 +272,10 @@ public struct CombatLogData
 				mBStringBuilder.Append(GameTexts.FindText(IsAttackerPlayer ? "ui_delivered_number_damage" : "ui_received_number_damage"));
 				item = (IsAttackerPlayer ? 4210351871u : 4292917946u);
 			}
+			if (MissionObjectHit != null && TotalDamage > 0)
+			{
+				mBStringBuilder.Append(GameTexts.FindText("ui_delivered_number_damage_to_entity"));
+			}
 			if (BodyPartHit != BoneBodyPartType.None)
 			{
 				damageType = (int)BodyPartHit;
@@ -282,32 +298,65 @@ public struct CombatLogData
 				mBStringBuilder.Append(GameTexts.FindText("combat_log_detail_distance"));
 				mBStringBuilder.Append("</Detail>");
 			}
-			if (AbsorbedDamage > 0)
+			if (TotalDamage > 0)
 			{
-				GameTexts.SetVariable("ABSORBED_DAMAGE", AbsorbedDamage);
-				mBStringBuilder.Append("<Detail>");
-				mBStringBuilder.Append(GameTexts.FindText("combat_log_detail_absorbed_damage"));
-				mBStringBuilder.Append("</Detail>");
+				if (AbsorbedDamage > 0)
+				{
+					GameTexts.SetVariable("ABSORBED_DAMAGE", AbsorbedDamage);
+					mBStringBuilder.Append("<Detail>");
+					mBStringBuilder.Append(GameTexts.FindText("combat_log_detail_absorbed_damage"));
+					mBStringBuilder.Append("</Detail>");
+				}
+				if (ModifiedDamage != 0)
+				{
+					GameTexts.SetVariable("MODIFIED_DAMAGE", TaleWorlds.Library.MathF.Abs(ModifiedDamage));
+					mBStringBuilder.Append("<Detail>");
+					if (ModifiedDamage > 0)
+					{
+						mBStringBuilder.Append(GameTexts.FindText("combat_log_detail_extra_damage"));
+					}
+					else if (ModifiedDamage < 0)
+					{
+						mBStringBuilder.Append(GameTexts.FindText("combat_log_detail_reduced_damage"));
+					}
+					mBStringBuilder.Append("</Detail>");
+				}
+				if (ReflectedDam
... (truncated, 7327 chars total)
```
</details>

### DefaultKingdomDecisionPermissionModel (+48 lines)

- Old: 61 lines | New: 109 lines
- Path: `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultKingdomDecisionPermissionModel.cs`

**Signature changes (public/protected API):**
```diff
+	private IAllianceCampaignBehavior _allianceCampaignBehavior;
+	private IAllianceCampaignBehavior AllianceCampaignBehavior
+	private TextObject GetExplanationForPeaceOfferWithCallToWar(Kingdom callingKingdom, Kingdom calledKingdom, Kingdom kingdomToCallToWarAgainst)
```

<details>
<summary>Full diff (48 line delta)</summary>

```diff
@@ -7,6 +7,20 @@ namespace TaleWorlds.CampaignSystem.GameComponents;
 
 public class DefaultKingdomDecisionPermissionModel : KingdomDecisionPermissionModel
 {
+	private IAllianceCampaignBehavior _allianceCampaignBehavior;
+
+	private IAllianceCampaignBehavior AllianceCampaignBehavior
+	{
+		get
+		{
+			if (_allianceCampaignBehavior == null)
+			{
+				_allianceCampaignBehavior = Campaign.Current.GetCampaignBehavior<IAllianceCampaignBehavior>();
+			}
+			return _allianceCampaignBehavior;
+		}
+	}
+
 	public override bool IsPolicyDecisionAllowed(PolicyObject policy)
 	{
 		return true;
@@ -21,21 +35,30 @@ public class DefaultKingdomDecisionPermissionModel : KingdomDecisionPermissionMo
 	public override bool IsPeaceDecisionAllowedBetweenKingdoms(Kingdom kingdom1, Kingdom kingdom2, out TextObject reason)
 	{
 		reason = null;
-		if (!Campaign.Current.Models.DiplomacyModel.IsAtConstantWar(kingdom1, kingdom2))
+		Kingdom callingKingdom = null;
+		if (Campaign.Current.Models.DiplomacyModel.IsAtConstantWar(kingdom1, kingdom2))
 		{
-			IAllianceCampaignBehavior campaignBehavior = Campaign.Current.GetCampaignBehavior<IAllianceCampaignBehavior>();
-			if (campaignBehavior == null || !campaignBehavior.IsAtWarByCallToWarAgreement(kingdom1, kingdom2))
-			{
-				if (!Campaign.Current.Models.DiplomacyModel.IsPeaceSuitable(kingdom1, kingdom2))
-				{
-					reason = new TextObject("{=JkQ7fmcX}The enemy is not open to negotiations.");
-					return false;
-				}
-				return true;
-			}
+			reason = new TextObject("{=eNPupZOp}These kingdoms can not declare peace at this time.");
+			return false;
+		}
+		IAllianceCampaignBehavior allianceCampaignBehavior = AllianceCampaignBehavior;
+		if (allianceCampaignBehavior != null && allianceCampaignBehavior.IsAtWarByCallToWarAgreement(kingdom1, kingdom2, out callingKingdom))
+		{
+			reason = GetExplanationForPeaceOfferWithCallToWar(callingKingdom, kingdom1, kingdom2);
+			return false;
+		}
+		IAllianceCampaignBehavior allianceCampaignBehavior2 = AllianceCampaignBehavior;
+		if (allianceCampaignBehavior2 != null && allianceCampaignBehavior2.IsAtWarByCallToWarAgreement(kingdom2, kingdom1, out callingKingdom))
+		{
+			reason = GetExplanationForPeaceOfferWithCallToWar(callingKingdom, kingdom2, kingdom1);
+			return false;
+		}
+		if (!Campaign.Current.Models.DiplomacyModel.IsPeaceSuitable(kingdom1, kingdom2))
+		{
+			reason = new TextObject("{=JkQ7fmcX}The enemy is not open to negotiations.");
+			return false;
 		}
-		reason = new TextObject("{=eNPupZOp}These kingdoms can not declare peace at this time.");
-		return false;
+		return true;
 	}
 
 	public override bool IsAnnexationDecisionAllowed(Settlement annexedSettlement)
@@ -58,4 +81,29 @@ public class DefaultKingdomDecisionPermissionModel : KingdomDecisionPermissionMo
 		reason = null;
 		return true;
 	}
+
+	private TextObject GetExplanationForPeaceOfferWithCallToWar(Kingdom callingKingdom, Kingdom calledKingdom, Kingdom kingdomToCallToWarAgainst)
+	{
+		TextObject empty = TextObject.GetEmpty();
+		if (calledKingdom == Clan.PlayerClan.Kingdom)
+		{
+			empty = new TextObject("{=*}Your realm is not allowed to negotiate peace with {KINGDOM_TO_CALL_TO_WAR_AGAINST} due to your Call to War Agreement with {CALLING_KINGDOM}.");
+			empty.SetTextVariable("KINGDOM_TO_CALL_TO_WAR_AGAINST", kingdomToCallToWarAgainst.Name);
+			empty.SetTextVariable("CALLING_KINGDOM", callingKingdom.Name);
+		}
+		else if (kingdomToCallToWarAgainst == Clan.PlayerClan.Kingdom)
+		{
+			empty = new TextObject("{=*}Your realm is not allowed to negotiate peace with {CALLED_KINGDOM} due to their Call to War Agreement with {CALLING_KINGDOM}.");
+			empty.SetTextVariable("CALLED_KINGDOM", calledKingdom.Name);
+			empty.SetTextVariable("CALLING_KINGDOM", callingKingdom.Name);
+		}
+		else
+		{
+			empty = new TextObject("{=*}{KINGDOM_NAME}  is not allowed to negotiate peace with {CALLED_KINGDOM} due to their Call to War Agreement with {CALLING_KINGDOM}.");
+			empty.SetTextVariable("KINGDOM_NAME", kingdomToCallToWarAgainst.Name);
+			empty.SetTextVariable("CALLED_KINGDOM", calledKingdom.Name);
+			empty.SetTextVariable("CALLING_KINGDOM", callingKingdom.Name);
+		}
+		return empty;
+	}
 }
```
</details>

### WeakGameEntity (+37 lines)

- Old: 1519 lines | New: 1556 lines
- Path: `E:\Decompiled_Bannerlord\Engine\TaleWorlds.Engine\TaleWorlds.Engine\WeakGameEntity.cs`

**Signature changes (public/protected API):**
```diff
-	private int ScriptCount => EngineApplicationInterface.IGameEntity.GetScriptComponentCount(Pointer);
+	public int GetScriptCount()
-	internal ScriptComponentBehavior GetScriptAtIndex(int index)
+	public ScriptComponentBehavior GetScriptAtIndex(int index)
-	public bool HasScriptOfType(Type t)
+	public bool HasScriptWithInterfaceOfType<T>()
+	public T GetFirstScriptWithInterfaceOfType<T>() where T : class
+	public int GetScriptCountOfType<T>() where T : ScriptComponentBehavior
```

<details>
<summary>Full diff (37 line delta)</summary>

```diff
@@ -1,6 +1,5 @@
 using System;
 using System.Collections.Generic;
-using System.Linq;
 using TaleWorlds.Library;
 
 namespace TaleWorlds.Engine;
@@ -29,8 +28,6 @@ public struct WeakGameEntity
 
 	public Vec3 CenterOfMass => EngineApplicationInterface.IGameEntity.GetCenterOfMass(Pointer);
 
-	private int ScriptCount => EngineApplicationInterface.IGameEntity.GetScriptComponentCount(Pointer);
-
 	public Vec3 GlobalPosition => GetGlobalFrame().origin;
 
 	public string[] Tags => EngineApplicationInterface.IGameEntity.GetTags(Pointer).Split(new char[1] { ' ' });
@@ -395,6 +392,11 @@ public struct WeakGameEntity
 		EngineApplicationInterface.IGameEntity.CallScriptCallbacks(Pointer, registerScriptComponents);
 	}
 
+	public int GetScriptCount()
+	{
+		return EngineApplicationInterface.IGameEntity.GetScriptComponentCount(Pointer);
+	}
+
 	public bool IsGhostObject()
 	{
 		return EngineApplicationInterface.IGameEntity.IsGhostObject(Pointer);
@@ -415,7 +417,7 @@ public struct WeakGameEntity
 		EngineApplicationInterface.IGameEntity.SetEntityEnvMapVisibility(Pointer, value);
 	}
 
-	internal ScriptComponentBehavior GetScriptAtIndex(int index)
+	public ScriptComponentBehavior GetScriptAtIndex(int index)
 	{
 		return EngineApplicationInterface.IGameEntity.GetScriptComponentAtIndex(Pointer, index);
 	}
@@ -442,7 +444,7 @@ public struct WeakGameEntity
 
 	public IEnumerable<ScriptComponentBehavior> GetScriptComponents()
 	{
-		int count = ScriptCount;
+		int count = GetScriptCount();
 		for (int i = 0; i < count; i++)
 		{
 			yield return GetScriptAtIndex(i);
@@ -451,7 +453,7 @@ public struct WeakGameEntity
 
 	public IEnumerable<T> GetScriptComponents<T>() where T : ScriptComponentBehavior
 	{
-		int count = ScriptCount;
+		int count = GetScriptCount();
 		for (int i = 0; i < count; i++)
 		{
 			if (GetScriptAtIndex(i) is T val)
@@ -463,7 +465,7 @@ public struct WeakGameEntity
 
 	public bool HasScriptOfType<T>() where T : ScriptComponentBehavior
 	{
-		int scriptCount = ScriptCount;
+		int scriptCount = GetScriptCount();
 		for (int i = 0; i < scriptCount; i++)
 		{
 			if (GetScriptAtIndex(i) is T)
@@ -474,9 +476,17 @@ public struct WeakGameEntity
 		return false;
 	}
 
-	public bool HasScriptOfType(Type t)
+	public bool HasScriptWithInterfaceOfType<T>()
 	{
-		return GetScriptComponents().Any((ScriptComponentBehavior sc) => sc.GetType().IsAssignableFrom(t));
+		int scriptCount = GetScriptCount();
+		for (int i = 0; i < scriptCount; i++)
+		{
+			if (GetScriptAtIndex(i) is T)
+			{
+				return true;
+			}
+		}
+		return false;
 	}
 
 	public T GetFirstScriptOfTypeInFamily<T>() where T : ScriptComponentBehavior
@@ -508,7 +518,20 @@ public struct WeakGameEntity
 
 	public T GetFirstScriptOfType<T>() where T : ScriptComponentBehavior
 	{
-		int scriptCount = ScriptCount;
+		int scriptCount = GetScriptCount();
+		for (int i = 0; i < scriptCount; i++)
+		{
+			if (GetScriptAtIndex(i) is T result)
+			{
+				return result;
+			}
+		}
+		return null;
+	}
+
+	public T GetFirstScriptWithInterfaceOfType<T>() where T : class
+	{
+		int scriptCount = GetScriptCount();
 		for (int i = 0; i < scriptCount; i++)
 		{
 			if (GetScriptAtIndex(i) is T result)
@@ -521,7 +544,7 @@ public struct WeakGameEntity
 
 	public T GetFirstScriptOfTypeRecursive<T>() where T : ScriptComponentBehavior
 	{
-		int scriptCount = ScriptCount;
+		int scriptCount = GetScriptCount();
 		for (int i = 0; i < scriptCount; i++)
 		{
 			if (GetScriptAtIndex(i) is T result)
@@ -553,9 +576,23 @@ public struct WeakGameEntity
 		return Invalid;
 	}
 
+	public int GetScriptCountOfType<T>() where T : ScriptComponentBehavior
+	{
+		int scriptCount = GetScriptCount();
+		int num = 0;
+		for (int i = 0; i < scriptCount; i++)
+		{
+			if (GetScriptAtIndex(i) is T)
+			{
+				num++;
+			}
+		}
+		return num;
+	}
+
 	public int GetScriptCountOfTypeRecursive<T>() where T : ScriptComponentBehavior
 	{
-		int scriptCount = ScriptCount;
+		int scriptCount = GetScriptCount();
 		int num = 0;
 		for (int i = 0; i < scriptCount; i++)
 		{
```
</details>

### CampaignUIHelper (+30 lines)

- Old: 3366 lines | New: 3396 lines
- Path: `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection\CampaignUIHelper.cs`

**Signature changes (public/protected API):**
```diff
-	private static readonly TextObject _partyInventoryCargoCapacityStr = new TextObject("{=*}Cargo Capacity");
+	private static readonly TextObject _partyInventoryCargoCapacityStr = new TextObject("{=fI7a7RoE}Cargo Capacity");
-	private static readonly TextObject _partyInventorySeaCapacityStr = new TextObject("{=*}Cargo Capacity at Sea");
+	private static readonly TextObject _partyInventorySeaCapacityStr = new TextObject("{=aAqMSU2d}Cargo Capacity at Sea");
-	private static readonly TextObject _partyInventoryCargoStr = new TextObject("{=*}Cargo");
+	private static readonly TextObject _partyInventoryCargoStr = new TextObject("{=cblOOivk}Cargo");
-	private static readonly TextObject _partyInventorySeaWeightStr = new TextObject("{=*}Cargo at Sea");
+	private static readonly TextObject _partyInventorySeaWeightStr = new TextObject("{=Tc5y7Tgd}Cargo at Sea");
+	private static readonly TextObject _partyRolesText = new TextObject("{=RrY2qMan}Party Roles");
+	private static readonly TextObject _governerEffectsText = new TextObject("{=J8ddrAOf}Governor Effects");
+	public static List<TooltipProperty> GetSmithingDifficultyTooltip()
```

<details>
<summary>Full diff (30 line delta)</summary>

```diff
@@ -233,19 +233,19 @@ public static class CampaignUIHelper
 
 	private static readonly TextObject _partyInventoryCapacityStr = new TextObject("{=fI7a7RoE}Inventory Capacity");
 
-	private static readonly TextObject _partyInventoryCargoCapacityStr = new TextObject("{=*}Cargo Capacity");
+	private static readonly TextObject _partyInventoryCargoCapacityStr = new TextObject("{=fI7a7RoE}Cargo Capacity");
 
 	private static readonly TextObject _partyInventoryLandCapacityStr = new TextObject("{=cBqjZjfJ}Inventory Capacity on Land");
 
-	private static readonly TextObject _partyInventorySeaCapacityStr = new TextObject("{=*}Cargo Capacity at Sea");
+	private static readonly TextObject _partyInventorySeaCapacityStr = new TextObject("{=aAqMSU2d}Cargo Capacity at Sea");
 
 	private static readonly TextObject _partyInventoryWeightStr = new TextObject("{=4Dd2xgPm}Weight");
 
-	private static readonly TextObject _partyInventoryCargoStr = new TextObject("{=*}Cargo");
+	private static readonly TextObject _partyInventoryCargoStr = new TextObject("{=cblOOivk}Cargo");
 
 	private static readonly TextObject _partyInventoryLandWeightStr = new TextObject("{=8d23bRmv}Weight on Land");
 
-	private static readonly TextObject _partyInventorySeaWeightStr = new TextObject("{=*}Cargo at Sea");
+	private static readonly TextObject _partyInventorySeaWeightStr = new TextObject("{=Tc5y7Tgd}Cargo at Sea");
 
 	private static readonly TextObject _partyTroopSizeLimitStr = new TextObject("{=2Cq3tViJ}Party Troop Size Limit");
 
@@ -279,6 +279,10 @@ public static class CampaignUIHelper
 
 	private static readonly TextObject _regroupingText = new TextObject("{=KxLoeSEO}Regrouping");
 
+	private static readonly TextObject _partyRolesText = new TextObject("{=RrY2qMan}Party Roles");
+
+	private static readonly TextObject _governerEffectsText = new TextObject("{=J8ddrAOf}Governor Effects");
+
 	public static readonly MobilePartyPrecedenceComparer MobilePartyPrecedenceComparerInstance = new MobilePartyPrecedenceComparer();
 
 	public static readonly SkillObjectComparer SkillObjectComparerInstance = new SkillObjectComparer();
@@ -1132,7 +1136,8 @@ public static class CampaignUIHelper
 		}
 		else
 		{
-			TooltipAddPropertyTitleWithValue(list, _partyInventoryCapacityStr.ToString(), party.InventoryCapacity);
+			TextObject textObject = (party.IsCurrentlyAtSea ? _partyInventoryCargoCapacityStr : _partyInventoryCapacityStr);
+			TooltipAddPropertyTitleWithValue(list, textObject.ToString(), party.InventoryCapacity);
 			TooltipAddSeperator(list);
 			ExplainedNumber explainedNumber3 = party.InventoryCapacityExplainedNumber;
 			TooltipAddExplanation(list, ref explainedNumber3);
@@ -1159,7 +1164,8 @@ public static class CampaignUIHelper
 		}
 		else
 		{
-			TooltipAddPropertyTitleWithValue(list, _partyInventoryWeightStr.ToString(), party.TotalWeightCarried);
+			TextObject textObject = (party.IsCurrentlyAtSea ? _partyInventoryCargoStr : _partyInventoryWeightStr);
+			TooltipAddPropertyTitleWithValue(list, textObject.ToString(), party.TotalWeightCarried);
 			TooltipAddSeperator(list);
 			ExplainedNumber explainedNumber3 = party.TotalWeightCarriedExplainedNumber;
 			TooltipAddExplanation(list, ref explainedNumber3);
@@ -1332,7 +1338,7 @@ public static class CampaignUIHelper
 		}
 		else
 		{
-			Debug.FailedAssert("Only towns' consumptions are tracked", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem.ViewModelCollection\\CampaignUIHelper.cs", "GetSettlementConsumptionTooltip", 1384);
+			Debug.FailedAssert("Only towns' consumptions are tracked", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem.ViewModelCollection\\CampaignUIHelper.cs", "GetSettlementConsumptionTooltip", 1388);
 		}
 		return list;
 	}
@@ -1483,7 +1489,7 @@ public static class CampaignUIHelper
 		{
 			return new StringItemWithHintVM("", TextObject.GetEmpty());
 		}
-		bool isMariner = character.IsMariner;
+		bool flag = !character.IsHero && character.IsMariner;
 		TextObject textObject = new TextObject("{=!}{TYPENAME}{MARINER}{BIG}");
 		TextObject textObject2;
 		if (character.IsRanged && character.IsMounted)
@@ -1494,7 +1500,7 @@ public static class CampaignUIHelper
 		else if (character.IsRanged)
 		{
 			textObject.SetTextVariable("TYPENAME", "bow");
-			string variation = (isMariner ? "Ranged_Mariner" : "Ranged");
+			string variation = (flag ? "Ranged_Mariner" : "Ranged");
 			textObject2 = GameTexts.FindText("str_troop_type_name", variation);
 		}
 		else if (character.IsMounted)
@@ -1509,10 +1515,10 @@ public static class CampaignUIHelper
 				return new StringItemWithHintVM("", TextObject.GetEmpty());
 			}
 			textObject.SetTextVariable("TYPENAME", "infantry");
-			string variation2 = (isMariner ? "Infantry_Mariner" : "Infantry");
+			string variation2 = (flag ? "Infantry_Mariner" : "Infantry");
 			textObject2 = GameTexts.FindText("str_troop_type_name", variation2);
 		}
-		textObject.SetTextVariable("MARINER", isMariner ? "_mar
... (truncated, 12331 chars total)
```
</details>

### DefaultArmyManagementCalculationModel (+27 lines)

- Old: 484 lines | New: 511 lines
- Path: `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultArmyManagementCalculationModel.cs`

**Signature changes (public/protected API):**
```diff
+	private const float MinimumInfluenceNeededToCreateArmy = 100f;
-	public override List<MobileParty> GetMobilePartiesToCallToArmy(MobileParty leaderParty)
+	public override bool CanLordCreateArmy(MobileParty mobileParty, out MBList<MobileParty> possibleArmyMembers)
+	private float GetInfluenceBudgetWhileCreatingArmy(MobileParty mobileParty)
+	private bool CanLordCreateArmyWithStrengthOf(MobileParty mobileParty, float possibleArmyStrength)
```

<details>
<summary>Full diff (27 line delta)</summary>

```diff
@@ -12,12 +12,15 @@ using TaleWorlds.CampaignSystem.Settlements;
 using TaleWorlds.CampaignSystem.Siege;
 using TaleWorlds.Core;
 using TaleWorlds.Library;
+using TaleWorlds.LinQuick;
 using TaleWorlds.Localization;
 
 namespace TaleWorlds.CampaignSystem.GameComponents;
 
 public class DefaultArmyManagementCalculationModel : ArmyManagementCalculationModel
 {
+	private const float MinimumInfluenceNeededToCreateArmy = 100f;
+
 	private readonly TextObject _numberOfPartiesText = GameTexts.FindText("str_number_of_parties");
 
 	private readonly TextObject _numberOfStarvingPartiesText = GameTexts.FindText("str_number_of_starving_parties");
@@ -113,96 +116,92 @@ public class DefaultArmyManagementCalculationModel : ArmyManagementCalculationMo
 		return (int)(0.65f * num3 * num4 * num7 * num6 * num5 * num8 * num9 * num10 * (float)AverageCallToArmyCost);
 	}
 
-	public override List<MobileParty> GetMobilePartiesToCallToArmy(MobileParty leaderParty)
+	public override bool CanLordCreateArmy(MobileParty mobileParty, out MBList<MobileParty> possibleArmyMembers)
 	{
-		List<MobileParty> list = new List<MobileParty>();
-		bool flag = false;
-		bool flag2 = false;
-		if (leaderParty.LeaderHero != null)
+		possibleArmyMembers = new MBList<MobileParty>();
+		if (!mobileParty.IsCurrentlyAtSea && mobileParty.LeaderHero.Clan.Influence > 100f && !mobileParty.LeaderHero.Clan.IsUnderMercenaryService && (float)mobileParty.GetNumDaysForFoodToLast() > Campaign.Current.Models.MobilePartyAIModel.NeededFoodsInDaysThresholdForSiege && (mobileParty.MapFaction as Kingdom).FactionsAtWarWith.AnyQ((IFaction x) => x.Fiefs.Any()) && mobileParty.PartySizeRatio > Campaign.Current.Models.ArmyManagementCalculationModel.AIMobilePartySizeRatioToCallToArmy && (mobileParty.LeaderHero.Clan.Leader == mobileParty.LeaderHero || (mobileParty.LeaderHero.Clan.Leader.PartyBelongedTo == null && mobileParty.LeaderHero.Clan.WarPartyComponents != null && mobileParty.LeaderHero.Clan.WarPartyComponents.FirstOrDefault() == mobileParty.WarPartyComponent)))
 		{
-			foreach (Settlement settlement in leaderParty.MapFaction.Settlements)
+			float num = GetInfluenceBudgetWhileCreatingArmy(mobileParty);
+			List<(MobileParty, float)> list = new List<(MobileParty, float)>();
+			foreach (WarPartyComponent warPartyComponent in mobileParty.MapFaction.WarPartyComponents)
 			{
-				if (settlement.IsFortification && settlement.SiegeEvent != null)
+				MobileParty mobileParty2 = warPartyComponent.MobileParty;
+				Hero leaderHero = mobileParty2.LeaderHero;
+				if (!mobileParty2.IsLordParty || mobileParty2.Army != null || mobileParty2 == mobileParty || leaderHero == null || mobileParty2.IsMainParty || leaderHero == leaderHero.MapFaction.Leader || mobileParty2.Ai.DoNotMakeNewDecisions || mobileParty2.CurrentSettlement?.SiegeEvent != null || mobileParty2.IsDisbanding || !((float)mobileParty2.GetNumDaysForFoodToLast() > Campaign.Current.Models.ArmyManagementCalculationModel.MinimumNeededFoodInDaysToCallToArmy) || !(mobileParty2.PartySizeRatio > Campaign.Current.Models.ArmyManagementCalculationModel.AIMobilePartySizeRatioToCallToArmy) || !leaderHero.CanLeadParty() || mobileParty2.IsInRaftState || mobileParty2.MapEvent != null || mobileParty2.BesiegedSettlement != null)
+				{
+					continue;
+				}
+				IDisbandPartyCampaignBehavior campaignBehavior = Campaign.Current.GetCampaignBehavior<IDisbandPartyCampaignBehavior>();
+				if (campaignBehavior != null && campaignBehavior.IsPartyWaitingForDisband(mobileParty2))
+				{
+					continue;
+				}
+				float maximumDistanceToCallToArmy = Campaign.Current.Models.ArmyManagementCalculationModel.MaximumDistanceToCallToArmy;
+				if (!(DistanceHelper.GetDistanceBetweenMobilePartyToMobileParty(mobileParty2, mobileParty, mobileParty2.NavigationCapability, out var _) < maximumDistanceToCallToArmy))
 				{
-					flag = true;
-					if (settlement.OwnerClan == leaderParty.LeaderHero.Clan)
+					continue;
+				}
+				bool flag = false;
+				foreach (var item3 in list)
+				{
+					if (item3.Item1 == mobileParty2)
 					{
-						flag2 = true;
+						flag = true;
+						break;
 					}
 				}
-			}
-		}
-		int b = ((leaderParty.MapFaction.IsKingdomFaction && (Kingdom)leaderParty.MapFaction != null) ? ((Kingdom)leaderParty.MapFaction).Armies.Count : 0);
-		float num = (1.5f - (float)MathF.Min(2, b) * 0.05f - ((Hero.MainHero.MapFaction == leaderParty.MapFaction) ? 0.05f : 0f)) * (1f - 0.5f * MathF.Sqrt(MathF.Min(leaderParty.LeaderHero.Clan.Influence, 900f)) * (1f / 30f));
-		num *= (flag2 ? 1.25f : 1f);
-		num *= (flag ? 1.125f : 1f);
-		num *= leaderParty.LeaderHero.RandomFloat(0.85f, 1f);
-		float num2 = MathF.Min(leaderParty.LeaderHero.Clan.Influence, 900f) * MathF.Min(1f, num);
-		List<(MobileParty, float)> list2 = new List<(MobileParty, float)>();
-		foreach (WarPartyComponent warPartyComponent in leaderParty.MapFaction.WarPartyComponents)
-		{
-			MobileParty mobileParty = warPartyComponent.MobileParty;
-			Hero leaderHero = mobileParty.LeaderHero;
... (truncated, 11382 chars total)
```
</details>

### DefaultBattleRewardModel (+26 lines)

- Old: 404 lines | New: 430 lines
- Path: `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultBattleRewardModel.cs`

**Signature changes (public/protected API):**
```diff
-	public override ExplainedNumber CalculateRenownGain(PartyBase party, float renownValueOfBattle, float contributionShare)
+	public override ExplainedNumber CalculateRenownGain(PartyBase winnerParty, float renownValueOfBattleForWinnerSide, float contributionShareOfWinnerParty, float renownMultiplierForWinnerSide, bool includeDescriptions)
-	public override ExplainedNumber CalculateInfluenceGain(PartyBase party, float influenceValueOfBattle, float contributionShare)
+	public override ExplainedNumber CalculateInfluenceGain(PartyBase winnerParty, float influenceValueOfBattleForWinnerSide, float contributionShareOfWinnerParty, float influenceMultiplierForWinnerSide, bool includeDescriptions)
-	public override ExplainedNumber CalculateMoraleGainVictory(PartyBase party, float renownValueOfBattle, float contributionShare, MapEvent battle)
+	public override ExplainedNumber CalculateMoraleGainVictory(PartyBase winnerParty, float renownValueOfBattleForWinnerSide, float contributionShareOfWinnerParty, bool includeDescriptions)
-	public override MBReadOnlyList<KeyValuePair<MapEventParty, float>> GetLootMemberChancesForWinnerParties(MBReadOnlyList<MapEventParty> winnerParties)
+	public override void GetCaptureMemberChancesForWinnerParties(MapEvent endedMapEvent, MBReadOnlyList<MapEventParty> winnerParties, out MBList<KeyValuePair<MapEventParty, float>> woundedMemberChances, out MBList<KeyValuePair<MapEventParty, float>> healthyMemberChances)
-	public override ExplainedNumber CalculateMoraleChangeOnRoundVictory(PartyBase party, MapEventSide partySide, BattleSideEnum roundWinner)
+	public override float CalculateMoraleChangeOnRoundVictory(PartyBase party, MapEventSide partySide, BattleSideEnum roundWinner)
+	public override bool CanTroopBeTakenPrisoner(CharacterObject troop)
```

<details>
<summary>Full diff (26 line delta)</summary>

```diff
@@ -1,5 +1,6 @@
 using System;
 using System.Collections.Generic;
+using System.Linq;
 using Helpers;
 using TaleWorlds.CampaignSystem.CharacterDevelopment;
 using TaleWorlds.CampaignSystem.ComponentInterfaces;
@@ -32,12 +33,11 @@ public class DefaultBattleRewardModel : BattleRewardModel
 
 	public override int GetPlayerGainedRelationAmount(MapEvent mapEvent, Hero hero)
 	{
-		MapEventSide mapEventSide = (mapEvent.AttackerSide.IsMainPartyAmongParties() ? mapEvent.AttackerSide : mapEvent.DefenderSide);
-		float playerPartyContributionRate = mapEventSide.GetPlayerPartyContributionRate();
+		float playerBattleContributionRate = mapEvent.GetPlayerBattleContributionRate();
 		float num = (mapEvent.StrengthOfSide[(int)PartyBase.MainParty.Side] - PlayerEncounter.Current.PlayerPartyInitialStrength) / (mapEvent.StrengthOfSide[(int)PartyBase.MainParty.OpponentSide] + 1f);
 		float num2 = ((num < 1f) ? (1f + (1f - num)) : ((num < 3f) ? (0.5f * (3f - num)) : 0f));
-		float renownValue = mapEvent.GetRenownValue((mapEventSide == mapEvent.AttackerSide) ? BattleSideEnum.Attacker : BattleSideEnum.Defender);
-		ExplainedNumber explainedNumber = new ExplainedNumber(0.75f + TaleWorlds.Library.MathF.Pow(playerPartyContributionRate * 1.3f * (num2 + renownValue), 0.67f));
+		float renownValue = (mapEvent.AttackerSide.IsMainPartyAmongParties() ? mapEvent.AttackerSide : mapEvent.DefenderSide).RenownValue;
+		ExplainedNumber explainedNumber = new ExplainedNumber(0.75f + TaleWorlds.Library.MathF.Pow(playerBattleContributionRate * 1.3f * (num2 + renownValue), 0.67f));
 		if (Hero.MainHero.GetPerkValue(DefaultPerks.Charm.Camaraderie))
 		{
 			explainedNumber.AddFactor(DefaultPerks.Charm.Camaraderie.PrimaryBonus, DefaultPerks.Charm.Camaraderie.Name);
@@ -45,24 +45,24 @@ public class DefaultBattleRewardModel : BattleRewardModel
 		return (int)explainedNumber.ResultNumber;
 	}
 
-	public override ExplainedNumber CalculateRenownGain(PartyBase party, float renownValueOfBattle, float contributionShare)
+	public override ExplainedNumber CalculateRenownGain(PartyBase winnerParty, float renownValueOfBattleForWinnerSide, float contributionShareOfWinnerParty, float renownMultiplierForWinnerSide, bool includeDescriptions)
 	{
-		ExplainedNumber stat = new ExplainedNumber(renownValueOfBattle * contributionShare, includeDescriptions: true);
-		if (party.IsMobile)
+		ExplainedNumber stat = new ExplainedNumber(contributionShareOfWinnerParty * renownValueOfBattleForWinnerSide * renownMultiplierForWinnerSide, includeDescriptions);
+		if (winnerParty.IsMobile)
 		{
-			if (party.MobileParty.HasPerk(DefaultPerks.Throwing.LongReach, checkSecondaryRole: true))
+			if (winnerParty.MobileParty.HasPerk(DefaultPerks.Throwing.LongReach, checkSecondaryRole: true))
 			{
-				PerkHelper.AddPerkBonusForParty(DefaultPerks.Throwing.LongReach, party.MobileParty, isPrimaryBonus: false, ref stat);
+				PerkHelper.AddPerkBonusForParty(DefaultPerks.Throwing.LongReach, winnerParty.MobileParty, isPrimaryBonus: false, ref stat);
 			}
-			if (party.MobileParty.HasPerk(DefaultPerks.Charm.PublicSpeaker))
+			if (winnerParty.MobileParty.HasPerk(DefaultPerks.Charm.PublicSpeaker))
 			{
 				stat.AddFactor(DefaultPerks.Charm.PublicSpeaker.PrimaryBonus, DefaultPerks.Charm.PublicSpeaker.Name);
 			}
-			if (party.LeaderHero != null)
+			if (winnerParty.LeaderHero != null)
 			{
-				PerkHelper.AddPerkBonusForCharacter(DefaultPerks.Leadership.FamousCommander, party.LeaderHero.CharacterObject, isPrimaryBonus: true, ref stat, party.MobileParty.IsCurrentlyAtSea);
+				PerkHelper.AddPerkBonusForCharacter(DefaultPerks.Leadership.FamousCommander, winnerParty.LeaderHero.CharacterObject, isPrimaryBonus: true, ref stat, winnerParty.MobileParty.IsCurrentlyAtSea);
 			}
-			if (PartyBaseHelper.HasFeat(party, DefaultCulturalFeats.VlandianRenownMercenaryFeat))
+			if (PartyBaseHelper.HasFeat(winnerParty, DefaultCulturalFeats.VlandianRenownMercenaryFeat))
 			{
 				stat.AddFactor(DefaultCulturalFeats.VlandianRenownMercenaryFeat.EffectBonus, GameTexts.FindText("str_culture"));
 			}
@@ -70,26 +70,33 @@ public class DefaultBattleRewardModel : BattleRewardModel
 		return stat;
 	}
 
-	public override ExplainedNumber CalculateInfluenceGain(PartyBase party, float influenceValueOfBattle, float contributionShare)
+	public override ExplainedNumber CalculateInfluenceGain(PartyBase winnerParty, float influenceValueOfBattleForWinnerSide, float contributionShareOfWinnerParty, float influenceMultiplierForWinnerSide, bool includeDescriptions)
 	{
-		ExplainedNumber bonuses = new ExplainedNumber(party.MapFaction.IsKingdomFaction ? (influenceValueOfBattle * contributionShare) : 0f, includeDescriptions: true);
-		if (party.LeaderHero != null)
+		ExplainedNumber bonuses = new ExplainedNumber(0f, includeDescriptions: false, null);
+		if (winnerParty.MapFaction.IsKingdomFaction)
 		{
-			PerkHelper.AddPerkBonusForCharacter(DefaultPerks.Charm.Warlord, party.LeaderHero.CharacterObject, isPrimaryBonus: true, ref bonu
... (truncated, 12652 chars total)
```
</details>

### DefaultDiplomacyModel (-24 lines)

- Old: 1316 lines | New: 1292 lines
- Path: `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultDiplomacyModel.cs`

**Signature changes (public/protected API):**
```diff
-	private const float FirstDegreeNeighborScore = 1f;
-	private const float SecondDegreeNeighborScore = 0.2f;
-	public override int MinNeutralRelationLimit => -25;
+	public override int MinNeutralRelationLimit => -50;
-	public override float WarDeclarationScorePenaltyAgainstAllies => 0.4f;
-	public override float WarDeclarationScoreBonusAgainstEnemiesOfAllies => 0.3f;
+	public override float WarDeclarationScorePenaltyAgainstTradePartners => 0.7f;
-	private static float GetAllianceFactor(IFaction factionDeclaresWar, IFaction factionDeclaredWar)
+	private static float GetTradeAgreementFactor(IFaction factionDeclaresWar, IFaction factionDeclaredWar)
```

<details>
<summary>Full diff (24 line delta)</summary>

```diff
@@ -4,6 +4,7 @@ using System.Linq;
 using Helpers;
 using TaleWorlds.CampaignSystem.Actions;
 using TaleWorlds.CampaignSystem.BarterSystem;
+using TaleWorlds.CampaignSystem.CampaignBehaviors;
 using TaleWorlds.CampaignSystem.CharacterDevelopment;
 using TaleWorlds.CampaignSystem.ComponentInterfaces;
 using TaleWorlds.CampaignSystem.Extensions;
@@ -46,10 +47,6 @@ public class DefaultDiplomacyModel : DiplomacyModel
 
 	private const float ClanRichnessEffectMultiplier = 0.15f;
 
-	private const float FirstDegreeNeighborScore = 1f;
-
-	private const float SecondDegreeNeighborScore = 0.2f;
-
 	private const float MaxBenefitValue = 10000000f;
 
 	private const float MeaningfulBenefitValue = 2000000f;
@@ -74,11 +71,9 @@ public class DefaultDiplomacyModel : DiplomacyModel
 
 	public override int MaxNeutralRelationLimit => 50;
 
-	public override int MinNeutralRelationLimit => -25;
+	public override int MinNeutralRelationLimit => -50;
 
-	public override float WarDeclarationScorePenaltyAgainstAllies => 0.4f;
-
-	public override float WarDeclarationScoreBonusAgainstEnemiesOfAllies => 0.3f;
+	public override float WarDeclarationScorePenaltyAgainstTradePartners => 0.7f;
 
 	public override float GetStrengthThresholdForNonMutualWarsToBeIgnoredToJoinKingdom(Kingdom kingdomToJoin)
 	{
@@ -276,7 +271,7 @@ public class DefaultDiplomacyModel : DiplomacyModel
 		}
 		float num7 = clan.CalculateTotalSettlementBaseValue();
 		float num8 = clan.CalculateTotalSettlementValueForFaction(kingdom);
-		int commanderLimit = clan.CommanderLimit;
+		int warPartyLimit = clan.WarPartyLimit;
 		float num9 = 0f;
 		float num10 = 0f;
 		if (!clan.IsMinorFaction)
@@ -291,13 +286,13 @@ public class DefaultDiplomacyModel : DiplomacyModel
 			{
 				if (!clan3.IsUnderMercenaryService && clan3 != clan)
 				{
-					num12 += clan3.CommanderLimit;
+					num12 += clan3.WarPartyLimit;
 				}
 			}
-			num9 = num11 / (float)(num12 + commanderLimit);
+			num9 = num11 / (float)(num12 + warPartyLimit);
 			num10 = 0f - (float)(num12 * num12) * 100f + 10000f;
 		}
-		float num13 = num9 * TaleWorlds.Library.MathF.Sqrt(commanderLimit) * 0.15f * 0.2f;
+		float num13 = num9 * TaleWorlds.Library.MathF.Sqrt(warPartyLimit) * 0.15f * 0.2f;
 		num13 *= num5 * num6;
 		num13 += (clan.MapFaction.IsAtWarWith(kingdom) ? (num8 - num7) : 0f);
 		num13 += num10;
@@ -325,7 +320,7 @@ public class DefaultDiplomacyModel : DiplomacyModel
 		float num6 = 1f + ((kingdom.Culture == clan.Culture) ? 0.15f : ((kingdom.Leader == Hero.MainHero) ? 0f : (-0.15f)));
 		float num7 = clan.CalculateTotalSettlementBaseValue();
 		float num8 = clan.CalculateTotalSettlementValueForFaction(kingdom);
-		int commanderLimit = clan.CommanderLimit;
+		int warPartyLimit = clan.WarPartyLimit;
 		float num9 = 0f;
 		if (!clan.IsMinorFaction)
 		{
@@ -339,10 +334,10 @@ public class DefaultDiplomacyModel : DiplomacyModel
 			{
 				if (!clan3.IsUnderMercenaryService && clan3 != clan)
 				{
-					num11 += clan3.CommanderLimit;
+					num11 += clan3.WarPartyLimit;
 				}
 			}
-			num9 = num10 / (float)(num11 + commanderLimit);
+			num9 = num10 / (float)(num11 + warPartyLimit);
 		}
 		float num12 = HeroHelper.CalculateReliabilityConstant(clan.Leader);
 		float b = (float)(CampaignTime.Now - clan.LastFactionChangeTime).ToDays;
@@ -362,7 +357,7 @@ public class DefaultDiplomacyModel : DiplomacyModel
 		}
 		float num16 = -70000f - (float)num15 * 10000f - (float)num14 * 30000f;
 		num16 /= 0.15f;
-		float num17 = (0f - num9) * TaleWorlds.Library.MathF.Sqrt(commanderLimit) * 0.15f * 0.2f + num16 * num12 + (0f - num13);
+		float num17 = (0f - num9) * TaleWorlds.Library.MathF.Sqrt(warPartyLimit) * 0.15f * 0.2f + num16 * num12 + (0f - num13);
 		num17 *= num5 * num6;
 		num17 = ((!(num5 < 1f) || !(num7 - num8 < 0f)) ? (num17 + (num7 - num8)) : (num17 + num5 * (num7 - num8)));
 		if (num5 < 1f)
@@ -380,8 +375,8 @@ public class DefaultDiplomacyModel : DiplomacyModel
 	{
 		float num = TaleWorlds.Library.MathF.Min(2f, TaleWorlds.Library.MathF.Max(0.33f, 1f + 0.02f * (float)FactionManager.GetRelationBetweenClans(kingdom.RulingClan, clan)));
 		float num2 = 1f + ((kingdom.Culture == clan.Culture) ? 1f : 0f);
-		int commanderLimit = clan.CommanderLimit;
-		float num3 = (clan.CurrentTotalStrength + 150f * (float)commanderLimit) * 20f;
+		int warPartyLimit = clan.WarPartyLimit;
+		float num3 = (clan.CurrentTotalStrength + 150f * (float)warPartyLimit) * 20f;
 		float powerRatioToEnemies = FactionHelper.GetPowerRatioToEnemies(kingdom);
 		float num4 = HeroHelper.CalculateReliabilityConstant(clan.Leader);
 		float num5 = 1f / TaleWorlds.Library.MathF.Max(0.4f, TaleWorlds.Library.MathF.Min(2.5f, TaleWorlds.Library.MathF.Sqrt(powerRatioToEnemies)));
@@ -393,8 +388,8 @@ public class DefaultDiplomacyModel : DiplomacyModel
 	{
 		float num = TaleWorlds.Library.MathF.Min(2f, TaleWorlds.Library.MathF.Max(0.33f, 1f + 0.02f * (float)FactionManager.GetRelationBetweenClans(kingdom.RulingClan, clan)));
 		float num2 = 1f + ((kingdom.Cu
... (truncated, 11864 chars total)
```
</details>

### DefaultTargetScoreCalculatingModel (-23 lines)

- Old: 415 lines | New: 392 lines
- Path: `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultTargetScoreCalculatingModel.cs`

**Signature changes (public/protected API):**
```diff
-	public override float GetPatrollingFactor(bool isNavalPatrolling)
+	public override float GetDefensivePatrollingFactor(bool isNavalPatrolling)
-	public override float CalculatePatrollingScoreForSettlement(Settlement settlement, bool isFromPort, MobileParty mobileParty)
+	public override float GetOffensivePatrollingFactor(bool isNavalPatrolling)
-	public override float CurrentObjectiveValue(MobileParty mobileParty)
-	private float CalculateNavalPatrollingScoreForSettlement(Settlement settlement, MobileParty mobileParty)
-	private float CalculateLandPatrollingScoreForSettlement(Settlement settlement, MobileParty mobileParty)
+	public override float CalculateDefensivePatrollingScoreForSettlement(Settlement settlement, bool isTargetingPort, MobileParty mobileParty)
+	public override float CalculateOffensivePatrollingScoreForSettlement(Settlement settlement, bool isTargetingPort, MobileParty mobileParty)
+	public override float CurrentObjectiveValue(MobileParty mobileParty)
```

<details>
<summary>Full diff (23 line delta)</summary>

```diff
@@ -3,12 +3,10 @@ using System.Linq;
 using Helpers;
 using TaleWorlds.CampaignSystem.ComponentInterfaces;
 using TaleWorlds.CampaignSystem.Map;
-using TaleWorlds.CampaignSystem.Naval;
 using TaleWorlds.CampaignSystem.Party;
 using TaleWorlds.CampaignSystem.Party.PartyComponents;
 using TaleWorlds.CampaignSystem.Settlements;
 using TaleWorlds.Library;
-using TaleWorlds.LinQuick;
 
 namespace TaleWorlds.CampaignSystem.GameComponents;
 
@@ -36,71 +34,17 @@ public class DefaultTargetScoreCalculatingModel : TargetScoreCalculatingModel
 
 	public override float DefendingFactor => 2f;
 
-	public override float GetPatrollingFactor(bool isNavalPatrolling)
+	public override float GetDefensivePatrollingFactor(bool isNavalPatrolling)
 	{
-		float num = 0.66f;
-		if (!isNavalPatrolling)
-		{
-			return num;
-		}
-		return num * 0.66f;
+		return 0.66f;
 	}
 
-	public override float CalculatePatrollingScoreForSettlement(Settlement settlement, bool isFromPort, MobileParty mobileParty)
+	public override float GetOffensivePatrollingFactor(bool isNavalPatrolling)
 	{
-		if (isFromPort)
-		{
-			return CalculateNavalPatrollingScoreForSettlement(settlement, mobileParty);
-		}
-		return CalculateLandPatrollingScoreForSettlement(settlement, mobileParty);
+		return 0f;
 	}
 
-	public override float CurrentObjectiveValue(MobileParty mobileParty)
-	{
-		float result = 0f;
-		if (mobileParty.TargetSettlement == null)
-		{
-			return 0f;
-		}
-		if (mobileParty.DefaultBehavior != AiBehavior.BesiegeSettlement && mobileParty.DefaultBehavior != AiBehavior.RaidSettlement && mobileParty.DefaultBehavior != AiBehavior.DefendSettlement)
-		{
-			return result;
-		}
-		float totalLandStrengthWithFollowers = mobileParty.GetTotalLandStrengthWithFollowers(includeNonAttachedArmyMembers: false);
-		result = GetTargetScoreForFaction(mobileParty.TargetSettlement, (mobileParty.DefaultBehavior != AiBehavior.BesiegeSettlement) ? ((mobileParty.DefaultBehavior == AiBehavior.RaidSettlement) ? Army.ArmyTypes.Raider : Army.ArmyTypes.Defender) : Army.ArmyTypes.Besieger, mobileParty, totalLandStrengthWithFollowers);
-		switch (mobileParty.DefaultBehavior)
-		{
-		case AiBehavior.BesiegeSettlement:
-			result *= ((mobileParty.Party.MapEvent == null && mobileParty.TargetSettlement.SiegeEvent != null && mobileParty.TargetSettlement.SiegeEvent.BesiegerCamp.HasInvolvedPartyForEventType(mobileParty.Party)) ? BesiegingFactor : ((mobileParty.Party.MapEvent != null && mobileParty.Party.MapEvent.MapEventSettlement == mobileParty.TargetSettlement) ? AssaultingTownFactor : TravelingToAssignmentFactor));
-			break;
-		case AiBehavior.RaidSettlement:
-			result *= ((mobileParty.Party.MapEvent != null && mobileParty.MapEvent.MapEventSettlement == mobileParty.TargetSettlement) ? RaidingFactor : TravelingToAssignmentFactor);
-			break;
-		case AiBehavior.DefendSettlement:
-			result *= ((mobileParty.Party.MapEvent != null && mobileParty.MapEvent.MapEventSettlement == mobileParty.TargetSettlement) ? DefendingFactor : TravelingToAssignmentFactor);
-			break;
-		}
-		return result;
-	}
-
-	private float CalculateNavalPatrollingScoreForSettlement(Settlement settlement, MobileParty mobileParty)
-	{
-		if (!mobileParty.HasNavalNavigationCapability || !settlement.HasPort || settlement.MapFaction != mobileParty.MapFaction)
-		{
-			return 0f;
-		}
-		float num = ((mobileParty.Food / (0f - mobileParty.FoodChange) > 5f) ? 1f : 0.2f);
-		float num2 = ((settlement.OwnerClan == mobileParty.LeaderHero?.Clan) ? 1f : 0.5f);
-		bool flag = mobileParty.DefaultBehavior == AiBehavior.PatrolAroundPoint && !mobileParty.TargetPosition.IsOnLand;
-		bool flag2 = mobileParty.DefaultBehavior == AiBehavior.PatrolAroundPoint && mobileParty.TargetPosition.IsOnLand;
-		float num3 = ((flag && mobileParty.TargetSettlement == settlement) ? 1.35f : 1f);
-		float num4 = (3f + settlement.NearbyNavalThreatIntensity - settlement.NearbyNavalAllyIntensity * 1.5f) * (flag ? 1.5f : 1f);
-		float num5 = mobileParty.Ships.SumQ((Ship x) => x.HitPoints / x.MaxHitPoints) / (float)mobileParty.Ships.Count;
-		float num6 = (flag2 ? 0.5f : 1f);
-		return num3 * num2 * num4 * num5 * num6 * num * Campaign.Current.Models.TargetScoreCalculatingModel.GetPatrollingFactor(isNavalPatrolling: true);
-	}
-
-	private float CalculateLandPatrollingScoreForSettlement(Settlement settlement, MobileParty mobileParty)
+	public override float CalculateDefensivePatrollingScoreForSettlement(Settlement settlement, bool isTargetingPort, MobileParty mobileParty)
 	{
 		bool flag = mobileParty.Army != null && mobileParty.Army.LeaderParty == mobileParty && !mobileParty.Army.IsWaitingForArmyMembers();
 		if (mobileParty.Army != null && !flag && mobileParty.Army.Cohesion > (float)mobileParty.Army.CohesionThresholdForDispersion && mobileParty.AttachedTo != null)
@@ -139,9 +83,42 @@ public class DefaultTargetScoreCalculatingModel : TargetScoreCalculatingModel
 		{
 			num7 = settlement.RandomFloatWithSeed((uint)CampaignTime.Now.ToWeeks, 0.2f, 1.8f);
 		}
-
... (truncated, 8973 chars total)
```
</details>

### TraitLevelingHelper (+17 lines)

- Old: 165 lines | New: 182 lines
- Path: `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\TraitLevelingHelper.cs`

**Signature changes (public/protected API):**
```diff
+	private const int TradeAgreementBrokenPenalty = -1000;
+	private const int AllianceBrokenHonorPenalty = -1000;
+	public static void OnTradeAgreementBroken()
+	public static void OnAllianceBrokenThroughHostility()
```

<details>
<summary>Full diff (17 line delta)</summary>

```diff
@@ -12,6 +12,10 @@ public static class TraitLevelingHelper
 {
 	private const int LordExecutedHonorPenalty = -1000;
 
+	private const int TradeAgreementBrokenPenalty = -1000;
+
+	private const int AllianceBrokenHonorPenalty = -1000;
+
 	private const int TroopsSacrificedValorPenalty = -30;
 
 	private const int VillageRaidedMercyPenalty = -30;
@@ -42,8 +46,11 @@ public static class TraitLevelingHelper
 		float strengthRatio = mapEvent.GetMapEventSide(PlayerEncounter.Current.PlayerSide).StrengthRatio;
 		if (strengthRatio > 9f)
 		{
-			int xpValue = (int)(MBMath.Map(strengthRatio, 9f, 10f, 5f, 20f) * contribution);
-			AddPlayerTraitXPAndLogEntry(DefaultTraits.Valor, xpValue, ActionNotes.BattleValor, null);
+			int num = (int)(MBMath.Map(strengthRatio, 9f, 10f, 5f, 20f) * contribution);
+			if (num > 0)
+			{
+				AddPlayerTraitXPAndLogEntry(DefaultTraits.Valor, num, ActionNotes.BattleValor, null);
+			}
 		}
 	}
 
@@ -57,6 +64,11 @@ public static class TraitLevelingHelper
 		AddPlayerTraitXPAndLogEntry(DefaultTraits.Honor, -1000, ActionNotes.SacrificedTroops, null);
 	}
 
+	public static void OnTradeAgreementBroken()
+	{
+		AddPlayerTraitXPAndLogEntry(DefaultTraits.Honor, -1000, ActionNotes.DishonestBusinessQuarrel, null);
+	}
+
 	public static void OnVillageRaided()
 	{
 		AddPlayerTraitXPAndLogEntry(DefaultTraits.Mercy, -30, ActionNotes.VillageRaid, null);
@@ -138,6 +150,11 @@ public static class TraitLevelingHelper
 		AddPlayerTraitXPAndLogEntry(trait, xpValue, ActionNotes.DefaultNote, Hero.MainHero);
 	}
 
+	public static void OnAllianceBrokenThroughHostility()
+	{
+		AddPlayerTraitXPAndLogEntry(DefaultTraits.Honor, -1000, ActionNotes.DishonestBusinessQuarrel, null);
+	}
+
 	private static void AddPlayerTraitXPAndLogEntry(TraitObject trait, int xpValue, ActionNotes context, Hero referenceHero)
 	{
 		int traitLevel = Hero.MainHero.GetTraitLevel(trait);
```
</details>

### MissionWeapon (+12 lines)

- Old: 593 lines | New: 605 lines
- Path: `E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MissionWeapon.cs`

**Signature changes (public/protected API):**
```diff
+	public bool HasAnyUsageWithItemUsageSetFlags(ItemObject.ItemUsageSetFlags flags)
```

<details>
<summary>Full diff (12 line delta)</summary>

```diff
@@ -465,6 +465,18 @@ public struct MissionWeapon
 		return false;
 	}
 
+	public bool HasAnyUsageWithItemUsageSetFlags(ItemObject.ItemUsageSetFlags flags)
+	{
+		foreach (WeaponComponentData weapon in _weapons)
+		{
+			if (MBItem.GetItemUsageSetFlags(weapon.ItemUsage).HasAllFlags(flags))
+			{
+				return true;
+			}
+		}
+		return false;
+	}
+
 	public void GatherInformationFromWeapon(out bool weaponHasMelee, out bool weaponHasShield, out bool weaponHasPolearm, out bool weaponHasNonConsumableRanged, out bool weaponHasThrown, out WeaponClass rangedAmmoClass)
 	{
 		weaponHasMelee = false;
```
</details>

### AiMilitaryBehavior (-10 lines)

- Old: 549 lines | New: 539 lines
- Path: `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors\AiMilitaryBehavior.cs`

**Signature changes (public/protected API):**
```diff
-	private const int MinimumInfluenceNeededToCreateArmy = 50;
-	private const float AverageSiegeDurationAsDays = 5.73f;
+	private const float AverageSiegeDurationAsDays = 8.02f;
-	public void FindBestTargetAndItsValueForFaction(Army.ArmyTypes missionType, PartyThinkParams p, float ourStrength, float newArmyCreatingAdditionalConstant = 1f)
+	public void FindBestTargetAndItsValueForFaction(Army.ArmyTypes missionType, PartyThinkParams p, float ourStrength)
-	private void CalculateMilitaryBehaviorForFactionSettlements(IFaction faction, PartyThinkParams p, Army.ArmyTypes missionType, AiBehavior aiBehavior, float ourStrength, float partySizeScore, float cohesionScore, float foodScore, float newArmyCreatingAdditionalConstant)
+	private void CalculateMilitaryBehaviorForFactionSettlements(IFaction faction, PartyThinkParams p, Army.ArmyTypes missionType, AiBehavior aiBehavior, float ourStrength, float partySizeScore, float cohesionScore, float foodScore)
-	private void CalculateMilitaryBehaviorForSettlement(Settlement settlement, Army.ArmyTypes missionType, AiBehavior aiBehavior, PartyThinkParams p, float ourStrength, float partySizeScore, float cohesionScore, float foodScore, float newArmyCreatingAdditionalConstant = 1f)
+	private void CalculateMilitaryBehaviorForSettlement(Settlement settlement, Army.ArmyTypes missionType, AiBehavior aiBehavior, PartyThinkParams p, float ourStrength, float partySizeScore, float cohesionScore, float foodScore)
```

<details>
<summary>Full diff (10 line delta)</summary>

```diff
@@ -1,26 +1,22 @@
 using System.Collections.Generic;
 using System.Linq;
 using Helpers;
-using TaleWorlds.CampaignSystem.CharacterDevelopment;
 using TaleWorlds.CampaignSystem.MapEvents;
 using TaleWorlds.CampaignSystem.Party;
 using TaleWorlds.CampaignSystem.Settlements;
 using TaleWorlds.CampaignSystem.Siege;
 using TaleWorlds.Core;
 using TaleWorlds.Library;
-using TaleWorlds.LinQuick;
 
 namespace TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
 
 public class AiMilitaryBehavior : CampaignBehaviorBase
 {
-	private const int MinimumInfluenceNeededToCreateArmy = 50;
-
 	private const float MeaningfulCohesionThresholdForArmy = 40f;
 
 	private const float MinimumCohesionScoreThreshold = 0.25f;
 
-	private const float AverageSiegeDurationAsDays = 5.73f;
+	private const float AverageSiegeDurationAsDays = 8.02f;
 
 	private IDisbandPartyCampaignBehavior _disbandPartyCampaignBehavior;
 
@@ -37,17 +33,34 @@ public class AiMilitaryBehavior : CampaignBehaviorBase
 
 	private void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
 	{
-		if (mapEvent.MapEventSettlement == null || !mapEvent.MapEventSettlement.IsFortification || !mapEvent.MapEventSettlement.HasPort || mapEvent.MapEventSettlement.SiegeEvent == null || !mapEvent.MapEventSettlement.SiegeEvent.IsBlockadeActive)
+		if (mapEvent.MapEventSettlement == null || !mapEvent.MapEventSettlement.HasPort)
 		{
 			return;
 		}
-		bool isNavalMapEvent = mapEvent.IsNavalMapEvent;
-		foreach (MobileParty allLordParty in MobileParty.AllLordParties)
+		if (mapEvent.MapEventSettlement.IsFortification && mapEvent.MapEventSettlement.SiegeEvent != null && mapEvent.MapEventSettlement.SiegeEvent.IsBlockadeActive)
 		{
-			bool flag = allLordParty.DefaultBehavior == AiBehavior.DefendSettlement && allLordParty.TargetSettlement == mapEvent.MapEventSettlement;
-			if (((allLordParty.ShortTermBehavior == AiBehavior.EngageParty && allLordParty.ShortTermTargetParty.SiegeEvent != null && allLordParty.ShortTermTargetParty.MapFaction.IsAtWarWith(allLordParty.MapFaction)) || flag) && isNavalMapEvent != allLordParty.IsTargetingPort)
+			bool isNavalMapEvent = mapEvent.IsNavalMapEvent;
 			{
-				allLordParty.SetMoveModeHold();
+				foreach (MobileParty allLordParty in MobileParty.AllLordParties)
+				{
+					bool flag = allLordParty.DefaultBehavior == AiBehavior.DefendSettlement && allLordParty.TargetSettlement == mapEvent.MapEventSettlement;
+					if (((allLordParty.ShortTermBehavior == AiBehavior.EngageParty && allLordParty.ShortTermTargetParty.SiegeEvent != null && allLordParty.ShortTermTargetParty.MapFaction.IsAtWarWith(allLordParty.MapFaction)) || flag) && isNavalMapEvent != allLordParty.IsTargetingPort)
+					{
+						allLordParty.SetMoveModeHold();
+					}
+				}
+				return;
+			}
+		}
+		if (!mapEvent.MapEventSettlement.IsVillage)
+		{
+			return;
+		}
+		foreach (MobileParty allLordParty2 in MobileParty.AllLordParties)
+		{
+			if (allLordParty2.DefaultBehavior == AiBehavior.GoToSettlement && allLordParty2.TargetSettlement == mapEvent.MapEventSettlement)
+			{
+				allLordParty2.SetMoveModeHold();
 			}
 		}
 	}
@@ -101,7 +114,7 @@ public class AiMilitaryBehavior : CampaignBehaviorBase
 	{
 	}
 
-	public void FindBestTargetAndItsValueForFaction(Army.ArmyTypes missionType, PartyThinkParams p, float ourStrength, float newArmyCreatingAdditionalConstant = 1f)
+	public void FindBestTargetAndItsValueForFaction(Army.ArmyTypes missionType, PartyThinkParams p, float ourStrength)
 	{
 		MobileParty mobilePartyOf = p.MobilePartyOf;
 		IFaction mapFaction = mobilePartyOf.MapFaction;
@@ -140,7 +153,7 @@ public class AiMilitaryBehavior : CampaignBehaviorBase
 		switch (missionType)
 		{
 		case Army.ArmyTypes.Defender:
-			CalculateMilitaryBehaviorForFactionSettlements(mapFaction, p, missionType, aiBehavior, ourStrength, partySizeScore, num, foodScoreForActionType, newArmyCreatingAdditionalConstant);
+			CalculateMilitaryBehaviorForFactionSettlements(mapFaction, p, missionType, aiBehavior, ourStrength, partySizeScore, num, foodScoreForActionType);
 			break;
 		case Army.ArmyTypes.Raider:
 			if (mobilePartyOf.Army != null || p.WillGatherAnArmy)
@@ -155,7 +168,7 @@ public class AiMilitaryBehavior : CampaignBehaviorBase
 				IFaction faction = mapFaction.FactionsAtWarWith[i];
 				if (faction.Leader != null && faction.IsMapFaction)
 				{
-					CalculateMilitaryBehaviorForFactionSettlements(faction, p, missionType, aiBehavior, ourStrength, partySizeScore, num, foodScoreForActionType, newArmyCreatingAdditionalConstant);
+					CalculateMilitaryBehaviorForFactionSettlements(faction, p, missionType, aiBehavior, ourStrength, partySizeScore, num, foodScoreForActionType);
 				}
 			}
 			break;
@@ -218,7 +231,7 @@ public class AiMilitaryBehavior : CampaignBehaviorBase
 		switch (missionType)
 		{
 		case Army.ArmyTypes.Defender:
-			num6 = MathF.Pow(num6, 0.75f);
+			num6 *= 0.5f;
 			break;
 		case Army.ArmyTypes.Raider:
 			num6 *= 0.75f;
@@ -227,7 +2
... (truncated, 13436 chars total)
```
</details>

### DefaultCombatSimulationModel (+9 lines)

- Old: 335 lines | New: 344 lines
- Path: `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultCombatSimulationModel.cs`

**Signature changes (public/protected API):**
```diff
+	public override CampaignTime GetSimulationTickInterval(MapEvent mapEvent)
```

<details>
<summary>Full diff (9 line delta)</summary>

```diff
@@ -332,4 +332,13 @@ public class DefaultCombatSimulationModel : CombatSimulationModel
 		}
 		return 0.1f;
 	}
+
+	public override CampaignTime GetSimulationTickInterval(MapEvent mapEvent)
+	{
+		if (mapEvent.IsSiegeAssault)
+		{
+			return CampaignTime.Minutes(60L);
+		}
+		return CampaignTime.Minutes(30L);
+	}
 }
```
</details>

### ClanPartyItemVM (-8 lines)

- Old: 1378 lines | New: 1370 lines
- Path: `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement\ClanPartyItemVM.cs`

**Signature changes (public/protected API):**
```diff
-	private IEnumerable<PartyRole> GetAssignablePartyRoles()
```

<details>
<summary>Full diff (8 line delta)</summary>

```diff
@@ -1240,7 +1240,7 @@ public class ClanPartyItemVM : ViewModel
 				x.OnFinalize();
 			});
 			Roles.Clear();
-			foreach (PartyRole assignablePartyRole in GetAssignablePartyRoles())
+			foreach (PartyRole assignablePartyRole in Campaign.Current.Models.ClanMemberPartyRoleModel.GetAssignablePartyRoles())
 			{
 				Roles.Add(new ClanRoleItemVM(Party.MobileParty, assignablePartyRole, HeroMembers, OnRoleSelectionToggled, OnRoleAssigned));
 			}
@@ -1323,14 +1323,6 @@ public class ClanPartyItemVM : ViewModel
 		}
 	}
 
-	private IEnumerable<PartyRole> GetAssignablePartyRoles()
-	{
-		yield return PartyRole.Quartermaster;
-		yield return PartyRole.Scout;
-		yield return PartyRole.Surgeon;
-		yield return PartyRole.Engineer;
-	}
-
 	private void OnRoleSelectionToggled(ClanRoleItemVM role)
 	{
 		LastOpenedRoleSelection = role;
```
</details>

### DefaultMilitaryPowerModel (+5 lines)

- Old: 279 lines | New: 284 lines
- Path: `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultMilitaryPowerModel.cs`

<details>
<summary>Full diff (5 line delta)</summary>

```diff
@@ -243,7 +243,12 @@ public class DefaultMilitaryPowerModel : MilitaryPowerModel
 	public override float GetDefaultTroopPower(CharacterObject troop)
 	{
 		int num = (troop.IsHero ? (troop.HeroObject.Level / 4 + 1) : troop.Tier);
-		return (float)((2 + num) * (10 + num)) * 0.02f * (troop.IsHero ? 1.5f : (troop.IsMounted ? 1.2f : 1f));
+		float num2 = (float)((2 + num) * (10 + num)) * 0.02f;
+		if (troop.IsHero)
+		{
+			num2 *= 1.5f;
+		}
+		return num2;
 	}
 
 	public override float GetContextModifier(Ship ship, BattleSideEnum battleSideEnum, MapEvent.PowerCalculationContext context)
```
</details>

### PartyBase (+4 lines)

- Old: 1194 lines | New: 1198 lines
- Path: `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs`

**Signature changes (public/protected API):**
```diff
-	public void SetCustomBanner(Banner banner)
+	public void SetCustomBanner(Banner banner)
+	public bool IsUnderPlayersCommand(BattleSideEnum playerSide)
```

<details>
<summary>Full diff (4 line delta)</summary>

```diff
@@ -300,7 +300,7 @@ public sealed class PartyBase : IBattleCombatant, IRandomOwner, IInteractablePoi
 			}
 			if (value != null && IsMobile && MapEvent != null && MapEvent.DefenderSide.LeaderParty == this)
 			{
-				Debug.FailedAssert($"Double MapEvent For {Name}", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Party\\PartyBase.cs", "MapEventSide", 257);
+				Debug.FailedAssert($"Double MapEvent For {Name}", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Party\\PartyBase.cs", "MapEventSide", 246);
 			}
 			if (_mapEventSide != null)
 			{
@@ -590,21 +590,6 @@ public sealed class PartyBase : IBattleCombatant, IRandomOwner, IInteractablePoi
 		SetVisualAsDirty();
 	}
 
-	public void SetCustomBanner(Banner banner)
-	{
-		CustomBanner = banner;
-		SetVisualAsDirty();
-	}
-
-	int IBattleCombatant.GetTacticsSkillAmount()
-	{
-		if (LeaderHero != null)
-		{
-			return LeaderHero.GetSkillValue(DefaultSkills.Tactics);
-		}
-		return 0;
-	}
-
 	CampaignVec2 IInteractablePoint.GetInteractionPosition(MobileParty interactingParty)
 	{
 		if (IsMobile)
@@ -657,13 +642,15 @@ public sealed class PartyBase : IBattleCombatant, IRandomOwner, IInteractablePoi
 	private static void GetEncounterTargetPoint(float dt, MobileParty mobileParty, out CampaignVec2 targetPoint, out float neededMaximumDistanceForEncountering)
 	{
 		EncounterModel encounterModel = Campaign.Current.Models.EncounterModel;
+		float num = (mobileParty.IsCurrentlyAtSea ? encounterModel.NeededMaximumNavalDistanceForEncounteringMobileParty : encounterModel.NeededMaximumLandDistanceForEncounteringMobileParty);
 		if (mobileParty.Army != null)
 		{
-			neededMaximumDistanceForEncountering = TaleWorlds.Library.MathF.Clamp(encounterModel.NeededMaximumDistanceForEncounteringMobileParty * TaleWorlds.Library.MathF.Sqrt(mobileParty.Army.LeaderParty.AttachedParties.Count + 1), TaleWorlds.Library.MathF.Max(encounterModel.NeededMaximumDistanceForEncounteringMobileParty, dt * Campaign.Current.EstimatedMaximumLordPartySpeedExceptPlayer), TaleWorlds.Library.MathF.Max(encounterModel.MaximumAllowedDistanceForEncounteringMobilePartyInArmy, dt * (Campaign.Current.EstimatedMaximumLordPartySpeedExceptPlayer + 0.01f)));
+			float a = (mobileParty.IsCurrentlyAtSea ? encounterModel.MaximumAllowedNavalDistanceForEncounteringMobilePartyInArmy : encounterModel.MaximumAllowedLandDistanceForEncounteringMobilePartyInArmy);
+			neededMaximumDistanceForEncountering = TaleWorlds.Library.MathF.Clamp(num * TaleWorlds.Library.MathF.Sqrt(mobileParty.Army.LeaderParty.AttachedParties.Count + 1), TaleWorlds.Library.MathF.Max(num, dt * Campaign.Current.EstimatedMaximumLordPartySpeedExceptPlayer), TaleWorlds.Library.MathF.Max(a, dt * (Campaign.Current.EstimatedMaximumLordPartySpeedExceptPlayer + 0.01f)));
 		}
 		else
 		{
-			neededMaximumDistanceForEncountering = TaleWorlds.Library.MathF.Max(encounterModel.NeededMaximumDistanceForEncounteringMobileParty, dt * Campaign.Current.EstimatedMaximumLordPartySpeedExceptPlayer);
+			neededMaximumDistanceForEncountering = TaleWorlds.Library.MathF.Max(num, dt * Campaign.Current.EstimatedMaximumLordPartySpeedExceptPlayer);
 		}
 		if (mobileParty.IsCurrentlyEngagingSettlement)
 		{
@@ -692,6 +679,30 @@ public sealed class PartyBase : IBattleCombatant, IRandomOwner, IInteractablePoi
 		}
 	}
 
+	public void SetCustomBanner(Banner banner)
+	{
+		CustomBanner = banner;
+		SetVisualAsDirty();
+	}
+
+	public bool IsUnderPlayersCommand(BattleSideEnum playerSide)
+	{
+		if (playerSide != Side)
+		{
+			return false;
+		}
+		return IsPartyUnderPlayerCommand(this);
+	}
+
+	int IBattleCombatant.GetTacticsSkillAmount()
+	{
+		if (LeaderHero != null)
+		{
+			return LeaderHero.GetSkillValue(DefaultSkills.Tactics);
+		}
+		return 0;
+	}
+
 	internal void AfterLoad()
 	{
 		if (!MBSaveLoad.IsUpdatingGameVersion)
@@ -784,7 +795,7 @@ public sealed class PartyBase : IBattleCombatant, IRandomOwner, IInteractablePoi
 	{
 		if (tier < 0)
 		{
-			Debug.FailedAssert("Requested men count for negative tier.", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Party\\PartyBase.cs", "GetNumberOfHealthyMenOfTier", 631);
+			Debug.FailedAssert("Requested men count for negative tier.", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Party\\PartyBase.cs", "GetNumberOfHealthyMenOfTier", 650);
 			return 0;
 		}
 		bool flag = false;
@@ -1046,6 +1057,7 @@ public sealed class PartyBase : IBattleCombatant, IRandomOwner, IInteractablePoi
 	private static void CalculateVisibilityAndInspected(Vec2 fromPosition, IMapPoint mapPoint, out bool isVisible, out bool isInspected, float mainPartySeeingRange = 0f)
 	{
 		isInspected = false;
+		isVisible = false;
 		MobileParty mobileParty = mapPoint as MobileParty;
 		if (mobileParty?.Army != null && mobileParty.Army.LeaderParty.AttachedParties.IndexOf(mobileParty) >= 0)
 		{
@@ -1058,41 +1070,33 @@ public sealed class PartyBase : IBattleCombatant
... (truncated, 6891 chars total)
```
</details>

### GuardsCampaignBehavior (-4 lines)

- Old: 1010 lines | New: 1006 lines
- Path: `E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.CampaignBehaviors\GuardsCampaignBehavior.cs`

<details>
<summary>Full diff (4 line delta)</summary>

```diff
@@ -702,12 +702,12 @@ public class GuardsCampaignBehavior : CampaignBehaviorBase
 		{
 			explanation = new TextObject("{=TP7rZTKs}You don't have {DENAR_AMOUNT}{GOLD_ICON} denars.", (Dictionary<string, object>)null);
 			explanation.SetTextVariable("DENAR_AMOUNT", bribeToEnterDungeon);
-			explanation.SetTextVariable("GOLD_ICON", "{=!}<img src=\"General\\Icons\\Coin@2x\" extend=\"8\">");
+			explanation.SetTextVariable("GOLD_ICON", "{=!}<img src=\"General\\Icons\\Coin@2x\" extend=\"6\">");
 			return false;
 		}
 		explanation = new TextObject("{=hCavIm4G}You will pay {AMOUNT}{GOLD_ICON} denars.", (Dictionary<string, object>)null);
 		explanation.SetTextVariable("AMOUNT", bribeToEnterDungeon);
-		explanation.SetTextVariable("GOLD_ICON", "{=!}<img src=\"General\\Icons\\Coin@2x\" extend=\"8\">");
+		explanation.SetTextVariable("GOLD_ICON", "{=!}<img src=\"General\\Icons\\Coin@2x\" extend=\"6\">");
 		return true;
 	}
 
@@ -808,12 +808,8 @@ public class GuardsCampaignBehavior : CampaignBehaviorBase
 	{
 		int bribeToEnterDungeon = Campaign.Current.Models.BribeCalculationModel.GetBribeToEnterDungeon(Settlement.CurrentSettlement);
 		MBTextManager.SetTextVariable("AMOUNT", bribeToEnterDungeon);
-		MBTextManager.SetTextVariable("GOLD_ICON", "{=!}<img src=\"General\\Icons\\Coin@2x\" extend=\"8\">", false);
-		if (Hero.MainHero.Gold >= bribeToEnterDungeon)
-		{
-			return !Campaign.Current.IsMainHeroDisguised;
-		}
-		return false;
+		MBTextManager.SetTextVariable("GOLD_ICON", "{=!}<img src=\"General\\Icons\\Coin@2x\" extend=\"6\">", false);
+		return !Campaign.Current.IsMainHeroDisguised;
 	}
 
 	private void conversation_prison_guard_visit_permission_bribe_on_consequence()
@@ -925,7 +921,7 @@ public class GuardsCampaignBehavior : CampaignBehaviorBase
 	{
 		int bribeToEnterLordsHall = Campaign.Current.Models.BribeCalculationModel.GetBribeToEnterLordsHall(Settlement.CurrentSettlement);
 		MBTextManager.SetTextVariable("AMOUNT", bribeToEnterLordsHall);
-		MBTextManager.SetTextVariable("GOLD_ICON", "{=!}<img src=\"General\\Icons\\Coin@2x\" extend=\"8\">", false);
+		MBTextManager.SetTextVariable("GOLD_ICON", "{=!}<img src=\"General\\Icons\\Coin@2x\" extend=\"6\">", false);
 		if (bribeToEnterLordsHall > 0 && !Campaign.Current.IsMainHeroDisguised)
 		{
 			return !conversation_castle_guard_nobody_inside_condition();
@@ -950,12 +946,12 @@ public class GuardsCampaignBehavior : CampaignBehaviorBase
 		{
 			explanation = new TextObject("{=TP7rZTKs}You don't have {DENAR_AMOUNT}{GOLD_ICON} denars.", (Dictionary<string, object>)null);
 			explanation.SetTextVariable("DENAR_AMOUNT", bribeToEnterLordsHall);
-			explanation.SetTextVariable("GOLD_ICON", "{=!}<img src=\"General\\Icons\\Coin@2x\" extend=\"8\">");
+			explanation.SetTextVariable("GOLD_ICON", "{=!}<img src=\"General\\Icons\\Coin@2x\" extend=\"6\">");
 			return false;
 		}
 		explanation = new TextObject("{=hCavIm4G}You will pay {AMOUNT}{GOLD_ICON} denars.", (Dictionary<string, object>)null);
 		explanation.SetTextVariable("AMOUNT", bribeToEnterLordsHall);
-		explanation.SetTextVariable("GOLD_ICON", "{=!}<img src=\"General\\Icons\\Coin@2x\" extend=\"8\">");
+		explanation.SetTextVariable("GOLD_ICON", "{=!}<img src=\"General\\Icons\\Coin@2x\" extend=\"6\">");
 		return true;
 	}
 
```
</details>

### CultureObject (+3 lines)

- Old: 533 lines | New: 536 lines
- Path: `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CultureObject.cs`

**Signature changes (public/protected API):**
```diff
+	public CharacterObject ShipyardWorker { get; private set; }
```

<details>
<summary>Full diff (3 line delta)</summary>

```diff
@@ -129,6 +129,8 @@ public sealed class CultureObject : BasicCultureObject
 
 	public CharacterObject Shipwright { get; private set; }
 
+	public CharacterObject ShipyardWorker { get; private set; }
+
 	public CharacterObject MilitiaVeteranArcher { get; private set; }
 
 	public CharacterObject GearDummy { get; private set; }
@@ -327,6 +329,7 @@ public sealed class CultureObject : BasicCultureObject
 		FemaleBeggar = objectManager.ReadObjectReferenceFromXml<CharacterObject>("female_beggar", node);
 		FemaleDancer = objectManager.ReadObjectReferenceFromXml<CharacterObject>("female_dancer", node);
 		Shipwright = objectManager.ReadObjectReferenceFromXml<CharacterObject>("shipwright", node);
+		ShipyardWorker = objectManager.ReadObjectReferenceFromXml<CharacterObject>("shipyard_worker", node);
 		MilitiaVeteranArcher = objectManager.ReadObjectReferenceFromXml<CharacterObject>("militia_veteran_archer", node);
 		GearDummy = objectManager.ReadObjectReferenceFromXml<CharacterObject>("gear_dummy", node);
 		BanditBandit = objectManager.ReadObjectReferenceFromXml<CharacterObject>("bandit_bandit", node);
```
</details>

### OrderOfBattleHeroItemVM (+3 lines)

- Old: 384 lines | New: 387 lines
- Path: `E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade.ViewModelCollection\TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle\OrderOfBattleHeroItemVM.cs`

<details>
<summary>Full diff (3 line delta)</summary>

```diff
@@ -317,7 +317,10 @@ public class OrderOfBattleHeroItemVM : ViewModel
 		{
 			Agent.Formation = InitialFormation;
 			InitialFormation.Refresh();
-			Agent.Team.DetachmentManager.RemoveScoresOfAgentFromDetachments(Agent);
+			if (Agent.IsDetachableFromFormation)
+			{
+				Agent.Team.DetachmentManager.RemoveScoresOfAgentFromDetachments(Agent);
+			}
 		}
 	}
 
```
</details>

### PartyVM (-2 lines)

- Old: 3591 lines | New: 3589 lines
- Path: `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.Party\PartyVM.cs`

**Signature changes (public/protected API):**
```diff
-	private List<string> _lockedTroopIDs;
+	private List<string> _lockedTroopIds;
-	private List<string> _lockedPrisonerIDs;
+	private List<string> _lockedPrisonerIds;
+	private PartyScreenLogic.TroopSortType _initialSortType;
+	private bool _initialSortAscending;
+	private List<string> _initialLockedTroopIds;
+	private List<string> _initialLockedPrisonerIds;
-	private void InitializePartyList(MBBindingList<PartyCharacterVM> partyList, TroopRoster currentTroopRoster, PartyScreenLogic.TroopType type, int side)
+	private void InitializePartyList(MBBindingList<PartyCharacterVM> partyList, PartyScreenLogic.PartyRosterSide side, PartyScreenLogic.TroopType type)
-	public void ExecuteResetAndCancel()
+	private void ExecuteCancelInternal()
-	public void ExecuteCancel()
+	public void ExecuteCancelWithoutInquiry()
+	public void ExecuteCancel(bool showCancelInquiry = false)
```

<details>
<summary>Full diff (2 line delta)</summary>

```diff
@@ -48,12 +48,20 @@ public class PartyVM : ViewModel
 
 	private PartyCharacterVM _currentCharacter;
 
-	private List<string> _lockedTroopIDs;
+	private List<string> _lockedTroopIds;
 
-	private List<string> _lockedPrisonerIDs;
+	private List<string> _lockedPrisonerIds;
 
 	private Func<string, TextObject> _getKeyTextFromKeyId;
 
+	private PartyScreenLogic.TroopSortType _initialSortType;
+
+	private bool _initialSortAscending;
+
+	private List<string> _initialLockedTroopIds;
+
+	private List<string> _initialLockedPrisonerIds;
+
 	public bool IsInConversation;
 
 	private List<Tuple<string, TextObject>> _formationNames;
@@ -1929,6 +1937,10 @@ public class PartyVM : ViewModel
 			IsDoneDisabled = !PartyScreenLogic.IsDoneActive();
 			DoneHint.HintText = new TextObject("{=!}" + PartyScreenLogic.DoneReasonString);
 			IsCancelDisabled = !PartyScreenLogic.IsCancelActive();
+			_initialSortType = (PartyScreenLogic.TroopSortType)_viewDataTracker.GetPartySortType();
+			_initialSortAscending = _viewDataTracker.GetIsPartySortAscending();
+			_initialLockedTroopIds = _viewDataTracker.GetPartyTroopLocks().ToList();
+			_initialLockedPrisonerIds = _viewDataTracker.GetPartyPrisonerLocks().ToList();
 			InitializeStaticInformation();
 			InitializeTroopLists();
 			RefreshPartyInformation();
@@ -1940,7 +1952,7 @@ public class PartyVM : ViewModel
 		IsAnyPopUpOpen = false;
 		OtherPartySortController = new PartySortControllerVM(PartyScreenLogic.PartyRosterSide.Left, OnSortTroops);
 		MainPartySortController = new PartySortControllerVM(PartyScreenLogic.PartyRosterSide.Right, OnSortTroops);
-		MainPartySortController.SortWith((PartyScreenLogic.TroopSortType)_viewDataTracker.GetPartySortType(), _viewDataTracker.GetIsPartySortAscending());
+		MainPartySortController.SortWith(_initialSortType, _initialSortAscending);
 		RefreshValues();
 	}
 
@@ -2021,7 +2033,7 @@ public class PartyVM : ViewModel
 	private void OnPartyMoraleChanged()
 	{
 		MBTextManager.SetTextVariable("PAY_OR_GET", (PartyScreenLogic.CurrentData.PartyMoraleChangeAmount > 0) ? 1 : 0);
-		MBTextManager.SetTextVariable("MORALE_ICON", "{=!}<img src=\"General\\Icons\\Morale@2x\" extend=\"8\">");
+		MBTextManager.SetTextVariable("MORALE_ICON", "{=!}<img src=\"General\\Icons\\Morale@2x\" extend=\"4\">");
 		MBTextManager.SetTextVariable("TRADE_AMOUNT", TaleWorlds.Library.MathF.Abs(PartyScreenLogic.CurrentData.PartyMoraleChangeAmount));
 		MoraleChangeText = ((PartyScreenLogic.CurrentData.PartyMoraleChangeAmount == 0) ? "" : GameTexts.FindText("str_party_morale_label").ToString());
 	}
@@ -2030,7 +2042,7 @@ public class PartyVM : ViewModel
 	{
 		int num = PartyScreenLogic.CurrentData.PartyInfluenceChangeAmount.Item1 + PartyScreenLogic.CurrentData.PartyInfluenceChangeAmount.Item2 + PartyScreenLogic.CurrentData.PartyInfluenceChangeAmount.Item3;
 		MBTextManager.SetTextVariable("PAY_OR_GET", (num > 0) ? 1 : 0);
-		MBTextManager.SetTextVariable("INFLUENCE_ICON", "{=!}<img src=\"General\\Icons\\Influence@2x\" extend=\"7\">");
+		MBTextManager.SetTextVariable("INFLUENCE_ICON", "{=!}<img src=\"General\\Icons\\Influence@2x\" extend=\"5\">");
 		MBTextManager.SetTextVariable("TRADE_AMOUNT", TaleWorlds.Library.MathF.Abs(num));
 		InfluenceChangeText = ((num == 0) ? "" : GameTexts.FindText("str_party_influence_label").ToString());
 	}
@@ -2046,12 +2058,12 @@ public class PartyVM : ViewModel
 	{
 		ArePrisonersRelevantOnCurrentMode = _currentMode != PartyScreenHelper.PartyScreenMode.TroopsManage && _currentMode != PartyScreenHelper.PartyScreenMode.QuestTroopManage;
 		AreMembersRelevantOnCurrentMode = _currentMode != PartyScreenHelper.PartyScreenMode.PrisonerManage && _currentMode != PartyScreenHelper.PartyScreenMode.Ransom;
-		_lockedTroopIDs = _viewDataTracker.GetPartyTroopLocks().ToList();
-		_lockedPrisonerIDs = _viewDataTracker.GetPartyPrisonerLocks().ToList();
-		InitializePartyList(MainPartyPrisoners, PartyScreenLogic.PrisonerRosters[1], PartyScreenLogic.TroopType.Prisoner, 1);
-		InitializePartyList(OtherPartyPrisoners, PartyScreenLogic.PrisonerRosters[0], PartyScreenLogic.TroopType.Prisoner, 0);
-		InitializePartyList(MainPartyTroops, PartyScreenLogic.MemberRosters[1], PartyScreenLogic.TroopType.Member, 1);
-		InitializePartyList(OtherPartyTroops, PartyScreenLogic.MemberRosters[0], PartyScreenLogic.TroopType.Member, 0);
+		_lockedTroopIds = _viewDataTracker.GetPartyTroopLocks().ToList();
+		_lockedPrisonerIds = _viewDataTracker.GetPartyPrisonerLocks().ToList();
+		InitializePartyList(MainPartyPrisoners, PartyScreenLogic.PartyRosterSide.Right, PartyScreenLogic.TroopType.Prisoner);
+		InitializePartyList(OtherPartyPrisoners, PartyScreenLogic.PartyRosterSide.Left, PartyScreenLogic.TroopType.Prisoner);
+		InitializePartyList(MainPartyTroops, PartyScreenLogic.PartyRosterSide.Right, PartyScreenLogic.TroopType.Member);
+		InitializePartyList(OtherPartyTroops, PartyScreenLogic.PartyRosterSide.Left, PartyScreenLogic.TroopType.Member);
 		if (MainPartyTroops.Count > 0)
 		{
 			
... (truncated, 19411 chars total)
```
</details>

### DefaultMapWeatherModel (+2 lines)

- Old: 678 lines | New: 680 lines
- Path: `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultMapWeatherModel.cs`

<details>
<summary>Full diff (2 line delta)</summary>

```diff
@@ -127,6 +127,7 @@ public class DefaultMapWeatherModel : MapWeatherModel
 		Campaign.Current.Models.MapWeatherModel.UpdateWeatherForPosition(position, CampaignTime.Now);
 		GetSeasonRainAndSnowDataForOpeningMission(position.ToVec2(), out var selectedSeason, out var isRaining, out var rainValue, out var snowFallDensity);
 		string selectedAtmosphereId = GetSelectedAtmosphereId(selectedSeason, isRaining, snowFallDensity, rainValue);
+		TerrainType terrainTypeAtPosition = Campaign.Current.MapSceneWrapper.GetTerrainTypeAtPosition(in position);
 		return new AtmosphereInfo
 		{
 			Seed = (uint)CampaignTime.Now.ToSeconds,
@@ -179,7 +180,8 @@ public class DefaultMapWeatherModel : MapWeatherModel
 				WindVector = Campaign.Current.Models.MapWeatherModel.GetWindForPosition(position),
 				CanUseLowAltitudeAtmosphere = 0,
 				UseSceneWindDirection = 1,
-				IsRiverBattle = ((Campaign.Current.MapSceneWrapper.GetTerrainTypeAtPosition(in position) == TerrainType.River) ? 1 : 0)
+				IsRiverBattle = ((terrainTypeAtPosition == TerrainType.River) ? 1 : 0),
+				UsesNavalSimulatedWater = ((terrainTypeAtPosition == TerrainType.River || terrainTypeAtPosition == TerrainType.Water || terrainTypeAtPosition == TerrainType.OpenSea || terrainTypeAtPosition == TerrainType.CoastalSea) ? 1 : 0)
 			},
 			AreaInfo = 
 			{
```
</details>

### MobilePartyVisual (+2 lines)

- Old: 1795 lines | New: 1797 lines
- Path: `E:\Decompiled_Bannerlord\Modules\SandBox.View\SandBox.View.Map.Visuals\MobilePartyVisual.cs`

**Signature changes (public/protected API):**
```diff
+	public override bool IsInSameFaction(IFaction faction)
```

<details>
<summary>Full diff (2 line delta)</summary>

```diff
@@ -98,12 +98,17 @@ public class MobilePartyVisual : MapEntityVisual<PartyBase>
 
 	public override bool IsEnemyOf(IFaction faction)
 	{
-		return FactionManager.IsAtWarAgainstFaction(base.MapEntity.MapFaction, Hero.MainHero.MapFaction);
+		return FactionManager.IsAtWarAgainstFaction(base.MapEntity.MapFaction, faction.MapFaction);
+	}
+
+	public override bool IsInSameFaction(IFaction faction)
+	{
+		return DiplomacyHelper.IsSameFactionAndNotEliminated(base.MapEntity.MapFaction, faction.MapFaction);
 	}
 
 	public override bool IsAllyOf(IFaction faction)
 	{
-		return DiplomacyHelper.IsSameFactionAndNotEliminated(base.MapEntity.MapFaction, Hero.MainHero.MapFaction);
+		return DiplomacyHelper.HasAllianceWithFaction(base.MapEntity.MapFaction, faction.MapFaction);
 	}
 
 	internal void OnPartyRemoved()
@@ -372,11 +377,10 @@ public class MobilePartyVisual : MapEntityVisual<PartyBase>
 		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
 		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
 		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
-		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
-		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
-		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
-		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
-		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
+		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
+		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
+		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
+		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
 		if (HumanAgentVisuals != null)
 		{
 			EquipmentElement val = HumanAgentVisuals.GetEquipment()[(EquipmentIndex)4];
@@ -388,8 +392,7 @@ public class MobilePartyVisual : MapEntityVisual<PartyBase>
 		ClothSimulatorComponent val2;
 		if ((NativeObject)(object)_cachedBannerComponent.Item2 != (NativeObject)null && (val2 = (ClothSimulatorComponent)/*isinst with value type is only supported in some contexts*/) != null)
 		{
-			float num = (IsPartOfBesiegerCamp(base.MapEntity) ? 6f : 1f);
-			val2.SetForcedWind(-StrategicEntity.GetGlobalFrame().rotation.f * num, false);
+			val2.SetForcedWind(-StrategicEntity.GetGlobalFrame().rotation.f, false);
 		}
 	}
 
@@ -1327,10 +1330,10 @@ public class MobilePartyVisual : MapEntityVisual<PartyBase>
 		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
 		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
 		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
-		//IL_0177: Unknown result type (might be due to invalid IL or missing references)
-		//IL_0183: Expected O, but got Unknown
-		//IL_0187: Unknown result type (might be due to invalid IL or missing references)
-		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
+		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
+		//IL_0172: Expected O, but got Unknown
+		//IL_0176: Unknown result type (might be due to invalid IL or missing references)
+		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
 		GameEntity val = GameEntity.CreateEmpty(strategicEntity.Scene, true, true, true);
 		val.AddMultiMesh(MetaMesh.GetCopy("map_icon_siege_camp_tent", true, false), true);
 		MatrixFrame identity = MatrixFrame.Identity;
@@ -1345,7 +1348,6 @@ public class MobilePartyVisual : MapEntityVisual<PartyBase>
 		bool flag = party.MobileParty.Army != null && party.MobileParty.Army.LeaderParty == party.MobileParty;
 		MatrixFrame identity2 = MatrixFrame.Identity;
 		identity2.origin.z += (flag ? 0.2f : 0.15f);
-		((Mat3)(ref identity2.rotation)).RotateAboutUp(MathF.PI / 2f);
 		float num = MBMath.Map(party.CalculateCurrentStrength() / 500f * ((party.MobileParty.Army != null && flag) ? 1f : 0.8f), 0f, 1f, 0.15f, 0.5f);
 		((Mat3)(ref identity2.rotation)).ApplyScaleLocal(num);
 		if (!string.IsNullOrEmpty(text))
@@ -1355,7 +1357,7 @@ public class MobilePartyVisual : MapEntityVisual<PartyBase>
 			if (_cachedBannerComponent.Item1 == text + text2)
 			{
 				_cachedBannerComponent.Item2.GetFirstMetaMesh().Frame = identity2;
-				strategicEntity.AddComponent(_cachedBannerComponent.Item2);
+				val.AddComponent(_cachedBannerComponent.Item2);
 			}
 			else
 			{
```
</details>

### PartyScreenLogic (+2 lines)

- Old: 1609 lines | New: 1611 lines
- Path: `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyScreenLogic.cs`

<details>
<summary>Full diff (2 line delta)</summary>

```diff
@@ -1036,8 +1036,10 @@ public class PartyScreenLogic
 		TroopSortType activeSortTypeForSide = GetActiveSortTypeForSide(side);
 		if (activeSortTypeForSide != TroopSortType.Custom)
 		{
-			TroopRoster roster2 = GetRoster(side, command.Type);
+			TroopRoster roster2 = GetRoster(side, TroopType.Member);
+			TroopRoster roster3 = GetRoster(side, TroopType.Prisoner);
 			SortRoster(roster2, activeSortTypeForSide);
+			SortRoster(roster3, activeSortTypeForSide);
 		}
 		UpdateDelegate?.Invoke(command);
 		this.Update?.Invoke(command);
@@ -1159,14 +1161,14 @@ public class PartyScreenLogic
 	{
 		if (roster.Count != list.Count)
 		{
-			TaleWorlds.Library.Debug.FailedAssert("Roster count is not synced with the list count", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Party\\PartyScreenLogic.cs", "EnsureRosterIsSyncedWithList", 1079);
+			TaleWorlds.Library.Debug.FailedAssert("Roster count is not synced with the list count", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Party\\PartyScreenLogic.cs", "EnsureRosterIsSyncedWithList", 1081);
 			return;
 		}
 		for (int i = 0; i < roster.Count; i++)
 		{
 			if (roster.GetCharacterAtIndex(i).StringId != list[i].Character.StringId)
 			{
-				TaleWorlds.Library.Debug.FailedAssert("Roster is not synced with the list at index: " + i, "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Party\\PartyScreenLogic.cs", "EnsureRosterIsSyncedWithList", 1089);
+				TaleWorlds.Library.Debug.FailedAssert("Roster is not synced with the list at index: " + i, "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Party\\PartyScreenLogic.cs", "EnsureRosterIsSyncedWithList", 1091);
 				break;
 			}
 		}
@@ -1515,7 +1517,7 @@ public class PartyScreenLogic
 		}
 		if (numOfItemsLeftToRemove > 0)
 		{
-			TaleWorlds.Library.Debug.FailedAssert("Couldn't find enough upgrade req items in the inventory.", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Party\\PartyScreenLogic.cs", "RemoveItemFromItemRoster", 1507);
+			TaleWorlds.Library.Debug.FailedAssert("Couldn't find enough upgrade req items in the inventory.", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Party\\PartyScreenLogic.cs", "RemoveItemFromItemRoster", 1509);
 		}
 		return list;
 	}
```
</details>

### DefaultMapVisibilityModel (-2 lines)

- Old: 112 lines | New: 110 lines
- Path: `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultMapVisibilityModel.cs`

**Signature changes (public/protected API):**
```diff
-	public override float GetPartySpottingRangeBase(MobileParty party)
+	public override float GetPartySeeingRangeBase(MobileParty party)
-	public override float GetPartyRelativeInspectionRange(IMapPoint party)
-	public override float GetPartySpottingDifficulty(MobileParty spottingParty, MobileParty party)
+	public override float GetPartySpottingRatioForMainPartySeeingRange(MobileParty party)
```

<details>
<summary>Full diff (2 line delta)</summary>

```diff
@@ -1,10 +1,11 @@
+using System;
 using Helpers;
 using TaleWorlds.CampaignSystem.CharacterDevelopment;
 using TaleWorlds.CampaignSystem.ComponentInterfaces;
-using TaleWorlds.CampaignSystem.Map;
 using TaleWorlds.CampaignSystem.Party;
 using TaleWorlds.Core;
 using TaleWorlds.Library;
+using TaleWorlds.Localization;
 
 namespace TaleWorlds.CampaignSystem.GameComponents;
 
@@ -17,7 +18,7 @@ public class DefaultMapVisibilityModel : MapVisibilityModel
 		return 60f;
 	}
 
-	public override float GetPartySpottingRangeBase(MobileParty party)
+	public override float GetPartySeeingRangeBase(MobileParty party)
 	{
 		if (!Campaign.Current.IsNight)
 		{
@@ -28,9 +29,8 @@ public class DefaultMapVisibilityModel : MapVisibilityModel
 
 	public override ExplainedNumber GetPartySpottingRange(MobileParty party, bool includeDescriptions = false)
 	{
-		float partySpottingRangeBase = Campaign.Current.Models.MapVisibilityModel.GetPartySpottingRangeBase(party);
-		ExplainedNumber explainedNumber = new ExplainedNumber(partySpottingRangeBase, includeDescriptions);
-		TerrainType faceTerrainType = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(party.CurrentNavigationFace);
+		float partySeeingRangeBase = Campaign.Current.Models.MapVisibilityModel.GetPartySeeingRangeBase(party);
+		ExplainedNumber explainedNumber = new ExplainedNumber(partySeeingRangeBase, includeDescriptions);
 		SkillHelper.AddSkillBonusForParty(DefaultSkillEffects.TrackingSpottingDistance, party, ref explainedNumber);
 		if (!party.IsCurrentlyAtSea)
 		{
@@ -39,6 +39,7 @@ public class DefaultMapVisibilityModel : MapVisibilityModel
 		Hero effectiveScout = party.EffectiveScout;
 		if (effectiveScout != null)
 		{
+			TerrainType faceTerrainType = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(party.CurrentNavigationFace);
 			if (faceTerrainType == TerrainType.Forest && PartyBaseHelper.HasFeat(party.Party, DefaultCulturalFeats.BattanianForestSpeedFeat))
 			{
 				explainedNumber.AddFactor(0.15f, GameTexts.FindText("str_culture"));
@@ -78,27 +79,24 @@ public class DefaultMapVisibilityModel : MapVisibilityModel
 				}
 			}
 		}
+		explainedNumber.LimitMax(Campaign.Current.Models.MapVisibilityModel.MaximumSeeingRange(), new TextObject("{=6qv6Hdww}Limit"));
 		return explainedNumber;
 	}
 
-	public override float GetPartyRelativeInspectionRange(IMapPoint party)
-	{
-		return 0.5f;
-	}
-
-	public override float GetPartySpottingDifficulty(MobileParty spottingParty, MobileParty party)
+	public override float GetPartySpottingRatioForMainPartySeeingRange(MobileParty party)
 	{
 		float num = 1f;
-		if (party != null && spottingParty != null && Campaign.Current.MapSceneWrapper.GetFaceTerrainType(party.CurrentNavigationFace) == TerrainType.Forest)
+		if (Campaign.Current.MapSceneWrapper.GetFaceTerrainType(party.CurrentNavigationFace) == TerrainType.Forest)
 		{
-			float num2 = 0.3f;
-			if (spottingParty.HasPerk(DefaultPerks.Scouting.KeenSight))
+			float num2 = -0.3f;
+			if (MobileParty.MainParty.HasPerk(DefaultPerks.Scouting.KeenSight))
 			{
 				num2 += num2 * DefaultPerks.Scouting.KeenSight.PrimaryBonus;
 			}
 			num += num2;
 		}
-		return (1f / MathF.Pow((float)(party.Party.NumberOfAllMembers + party.Party.NumberOfPrisoners + 2) * 0.2f, 0.6f) + 0.94f) * num;
+		int num3 = ((party.Army != null && party.Army.LeaderParty == party) ? party.Army.TotalManCount : party.MemberRoster.TotalManCount);
+		return MBMath.ClampFloat(1.1f - 0.5f * TaleWorlds.Library.MathF.Pow(System.MathF.E, (float)(-num3) / 200f), 0f, 1f) * num;
 	}
 
 	public override float GetHideoutSpottingDistance()
```
</details>

### CharacterObject (+2 lines)

- Old: 856 lines | New: 858 lines
- Path: `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CharacterObject.cs`

**Signature changes (public/protected API):**
```diff
+	private bool _isMariner;
-	public bool IsMariner { get; private set; }
+	public bool IsMariner => _isMariner;
```

<details>
<summary>Full diff (2 line delta)</summary>

```diff
@@ -31,6 +31,8 @@ public sealed class CharacterObject : BasicCharacterObject, ICharacterData
 
 	private CharacterObject _battleEquipmentTemplate;
 
+	private bool _isMariner;
+
 	private Occupation _occupation;
 
 	public override TextObject Name
@@ -251,7 +253,7 @@ public sealed class CharacterObject : BasicCharacterObject, ICharacterData
 
 	public static IEnumerable<CharacterObject> ConversationCharacters => Campaign.Current.ConversationManager.ConversationCharacters;
 
-	public bool IsMariner { get; private set; }
+	public bool IsMariner => _isMariner;
 
 	public new CultureObject Culture
 	{
@@ -429,7 +431,7 @@ public sealed class CharacterObject : BasicCharacterObject, ICharacterData
 		characterObject._occupation = character._occupation;
 		characterObject._persona = character._persona;
 		characterObject._characterTraits = new PropertyOwner<TraitObject>(character._characterTraits);
-		characterObject.IsMariner = character.IsMariner;
+		characterObject._isMariner = character.IsMariner;
 		characterObject._civilianEquipmentTemplate = character._civilianEquipmentTemplate;
 		characterObject._battleEquipmentTemplate = character._battleEquipmentTemplate;
 		characterObject.HiddenInEncyclopedia = character.HiddenInEncyclopedia;
@@ -524,7 +526,7 @@ public sealed class CharacterObject : BasicCharacterObject, ICharacterData
 		UpgradeRequiresItemFromCategory = _originCharacter.UpgradeRequiresItemFromCategory;
 		_civilianEquipmentTemplate = _originCharacter._civilianEquipmentTemplate;
 		_battleEquipmentTemplate = _originCharacter._battleEquipmentTemplate;
-		IsMariner = _originCharacter.IsMariner;
+		_isMariner = _originCharacter._isMariner;
 		_persona = _originCharacter._persona;
 		_characterTraits = _originCharacter._characterTraits;
 		DefaultCharacterSkills = _originCharacter.DefaultCharacterSkills;
@@ -592,7 +594,7 @@ public sealed class CharacterObject : BasicCharacterObject, ICharacterData
 		{
 			_battleEquipmentTemplate = objectManager.ReadObjectReferenceFromXml("battleTemplate", typeof(CharacterObject), node) as CharacterObject;
 		}
-		IsMariner = GetTraitLevel(DefaultTraits.NavalSoldier) != 0;
+		_isMariner = GetTraitLevel(DefaultTraits.NavalSoldier) != 0;
 		_originCharacter = null;
 	}
 
@@ -712,7 +714,7 @@ public sealed class CharacterObject : BasicCharacterObject, ICharacterData
 		case Equipment.EquipmentType.Stealth:
 			return FirstStealthEquipment;
 		default:
-			Debug.FailedAssert("Wanted EquipmentType doesn't exist", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\CharacterObject.cs", "GetEquipmentByType", 896);
+			Debug.FailedAssert("Wanted EquipmentType doesn't exist", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\CharacterObject.cs", "GetEquipmentByType", 906);
 			return null;
 		}
 	}
```
</details>

### Campaign (+1 lines)

- Old: 2117 lines | New: 2118 lines
- Path: `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Campaign.cs`

<details>
<summary>Full diff (1 line delta)</summary>

```diff
@@ -687,6 +687,7 @@ public class Campaign : GameType
 		base.ObjectManager.AfterLoad();
 		CampaignObjectManager.AfterLoad();
 		CharacterRelationManager.AfterLoad();
+		FactionManager.AfterLoad();
 		CampaignEventDispatcher.Instance.OnGameEarlyLoaded(starter);
 		CampaignEventDispatcher.Instance.OnGameLoaded(starter);
 		InitializeForSavedGame();
@@ -1503,7 +1504,7 @@ public class Campaign : GameType
 	private void InitializeCampaignObjectsOnAfterLoad()
 	{
 		CampaignObjectManager.InitializeOnLoad();
-		FactionManager.AfterLoad();
+		FactionManager.PreAfterLoad();
 		List<PerkObject> collection = AllPerks.Where((PerkObject x) => !x.IsTrash).ToList();
 		AllPerks = new MBReadOnlyList<PerkObject>(collection);
 		LogEntryHistory.OnAfterLoad();
@@ -1782,7 +1783,7 @@ public class Campaign : GameType
 			else
 			{
 				CampaignVec2 campaignPosition = Hero.MainHero.GetCampaignPosition();
-				position = ((campaignPosition.IsValid() && campaignPosition != CampaignVec2.Zero) ? campaignPosition : HeroHelper.FindASuitableSettlementToTeleportForHero(Hero.MainHero).GatePosition);
+				position = ((campaignPosition.IsValid() && campaignPosition != CampaignVec2.Zero) ? campaignPosition : SettlementHelper.GetBestSettlementToSpawnAround(Hero.MainHero).GatePosition);
 				MainParty.IsActive = true;
 				MainParty.MemberRoster.AddToCounts(Hero.MainHero.CharacterObject, 1, insertAtFront: true);
 			}
```
</details>

### ViewModel (+1 lines)

- Old: 635 lines | New: 636 lines
- Path: `E:\Decompiled_Bannerlord\Core\TaleWorlds.Library\TaleWorlds.Library\ViewModel.cs`

<details>
<summary>Full diff (1 line delta)</summary>

```diff
@@ -425,7 +425,8 @@ public abstract class ViewModel : IViewModel, INotifyPropertyChanged
 		}
 		if (bindingList.Count > 0)
 		{
-			int num = Convert.ToInt32(subPath.FirstNode);
+			int num = -1;
+			num = Convert.ToInt32(subPath.FirstNode);
 			if (num >= 0 && num < bindingList.Count)
 			{
 				object obj = bindingList[num];
```
</details>

### CharacterCreationCampaignBehavior (0 lines)

- Old: 3504 lines | New: 3504 lines
- Path: `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CharacterCreationCampaignBehavior.cs`

<details>
<summary>Full diff (0 line delta)</summary>

```diff
@@ -2214,9 +2214,9 @@ public class CharacterCreationCampaignBehavior : CampaignBehaviorBase, ICharacte
 		narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption16);
 		NarrativeMenuOption narrativeMenuOption17 = new NarrativeMenuOption("youth_camp_option", new TextObject("{=GFUggps8}marched with the camp followers."), new TextObject("{=64rWqBLN}You avoided service with one of the main forces of your realm's armies, but followed instead in the train - the troops' wives, lovers and servants, and those who make their living by caring for, entertaining, or cheating the soldiery."), GetYouthCampOptionArgs, YouthCampOptionOnCondition, YouthCampOptionOnSelect, null);
 		narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption17);
-		NarrativeMenuOption narrativeMenuOption18 = new NarrativeMenuOption("youth_envoys_guard_first_option", new TextObject("{=YmPlLGXb}served as an envoy's guard"), new TextObject("{=qPamcCkA}Your family arranged for you to accompany an envoy. You were not given major responsibilities - mostly carrying arms and trying to look imposing. - but it did give you a chance to travel a lot and socialise and see the world."), GetEnvoysGuardFirstOptionArgs, EnvoysGuardFirstOptionOnCondition, EnvoysGuardFirstOptionOnSelect, null);
+		NarrativeMenuOption narrativeMenuOption18 = new NarrativeMenuOption("youth_envoys_guard_first_option", new TextObject("{=YmPlLGXb}served in an envoy's entourage"), new TextObject("{=qPamcCkA}Your family arranged for you to accompany an envoy. You were not given major responsibilities - mostly carrying arms and trying to look imposing. - but it did give you a chance to travel a lot and socialise and see the world."), GetEnvoysGuardFirstOptionArgs, EnvoysGuardFirstOptionOnCondition, EnvoysGuardFirstOptionOnSelect, null);
 		narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption18);
-		NarrativeMenuOption narrativeMenuOption19 = new NarrativeMenuOption("youth_envoys_guard_second_option", new TextObject("{=YmPlLGXb}served as an envoy's guard"), new TextObject("{=VYU1nEHP}Your family arranged for you to accompany an envoy. You were not given major responsibilities but it did give you a chance to travel and socialise and see a bit of the world."), GetEnvoysGuardSecondOptionArgs, EnvoysGuardSecondOptionOnCondition, EnvoysGuardSecondOptionOnSelect, null);
+		NarrativeMenuOption narrativeMenuOption19 = new NarrativeMenuOption("youth_envoys_guard_second_option", new TextObject("{=YmPlLGXb}served in an envoy's entourage"), new TextObject("{=VYU1nEHP}Your family arranged for you to accompany an envoy. You were not given major responsibilities but it did give you a chance to travel and socialise and see a bit of the world."), GetEnvoysGuardSecondOptionArgs, EnvoysGuardSecondOptionOnCondition, EnvoysGuardSecondOptionOnSelect, null);
 		narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption19);
 	}
 
```
</details>

### DefaultSettlementLoyaltyModel (0 lines)

- Old: 296 lines | New: 296 lines
- Path: `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementLoyaltyModel.cs`

<details>
<summary>Full diff (0 line delta)</summary>

```diff
@@ -227,7 +227,7 @@ public class DefaultSettlementLoyaltyModel : SettlementLoyaltyModel
 		{
 			explainedNumber.Add(0.5f, DefaultPolicies.TrialByJury.Name);
 		}
-		if (kingdom.ActivePolicies.Contains(DefaultPolicies.ImperialTowns))
+		if (town.IsTown && kingdom.ActivePolicies.Contains(DefaultPolicies.ImperialTowns))
 		{
 			if (kingdom.RulingClan == town.Settlement.OwnerClan)
 			{
```
</details>

### PartyCharacterVM (0 lines)

- Old: 1540 lines | New: 1540 lines
- Path: `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.Party\PartyCharacterVM.cs`

<details>
<summary>Full diff (0 line delta)</summary>

```diff
@@ -1170,7 +1170,7 @@ public class PartyCharacterVM : ViewModel
 	{
 		base.RefreshValues();
 		Name = Troop.Character.Name.ToString();
-		LockHint = new HintViewModel(GameTexts.FindText("str_inventory_lock"));
+		LockHint = new HintViewModel(GameTexts.FindText("str_lock_in_party").SetTextVariable("TRANSFERABLE", IsPrisoner ? GameTexts.FindText("str_prisoners").ToString() : GameTexts.FindText("str_troops").ToString()));
 		Upgrades?.ApplyActionOnAllItems(delegate(UpgradeTargetVM x)
 		{
 			x.RefreshValues();
@@ -1186,7 +1186,7 @@ public class PartyCharacterVM : ViewModel
 	private string GetTransferHint()
 	{
 		string text = GameTexts.FindText("str_transfer").ToString();
-		string stackModifierString = CampaignUIHelper.GetStackModifierString(GameTexts.FindText("str_entire_stack_shortcut_transfer_troops"), GameTexts.FindText("str_five_stack_shortcut_transfer_troops"), Troop.Number >= 5);
+		string stackModifierString = CampaignUIHelper.GetStackModifierString(GameTexts.FindText("str_entire_stack_shortcut_transfer"), GameTexts.FindText("str_five_stack_shortcut_transfer"), Troop.Number >= 5);
 		if (string.IsNullOrEmpty(stackModifierString))
 		{
 			return text;
@@ -1302,7 +1302,7 @@ public class PartyCharacterVM : ViewModel
 				}
 				flag = flag && !_partyVm.PartyScreenLogic.IsTroopUpgradesDisabled;
 				string upgradeHint = CampaignUIHelper.GetUpgradeHint(i, numOfCategoryItemPartyHas, num, upgradeGoldCost, flag3, requiredPerk, Character, Troop, _partyScreenLogic.CurrentData.PartyGoldChangeAmount, _partyVm.PartyScreenLogic.IsTroopUpgradesDisabled);
-				Upgrades[i].Refresh(num, flag, flag2, flag4, flag3, upgradeHint, Character.IsMariner);
+				Upgrades[i].Refresh(num, flag, flag2, flag4, flag3, upgradeHint, !Character.IsHero && Character.IsMariner);
 				if (i == 0)
 				{
 					UpgradeCostText = upgradeGoldCost.ToString();
```
</details>

### Clan (0 lines)

- Old: 1428 lines | New: 1428 lines
- Path: `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs`

**Signature changes (public/protected API):**
```diff
-	public int CommanderLimit => Campaign.Current.Models.ClanTierModel.GetPartyLimitForTier(this, Tier);
+	public int WarPartyLimit => Campaign.Current.Models.ClanTierModel.GetPartyLimitForTier(this, Tier);
```

<details>
<summary>Full diff (0 line delta)</summary>

```diff
@@ -418,7 +418,7 @@ public sealed class Clan : MBObjectBase, IFaction
 		}
 	}
 
-	public int CommanderLimit => Campaign.Current.Models.ClanTierModel.GetPartyLimitForTier(this, Tier);
+	public int WarPartyLimit => Campaign.Current.Models.ClanTierModel.GetPartyLimitForTier(this, Tier);
 
 	public static MBReadOnlyList<Clan> All => Campaign.Current.Clans;
 
```
</details>

## New Types in v1.4.0

These are new in v1.4.0. Not breaking, but may offer new override/patch opportunities.

- **BodyGeneratorView** (at: `E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade.GauntletUI\TaleWorlds.MountAndBlade.GauntletUI.BodyGenerator\BodyGeneratorView.cs`)
- **CharacterSpawner** (at: `E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade.View\TaleWorlds.MountAndBlade.View.Scripts\CharacterSpawner.cs`)
- **CharacterTableau** (at: `E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade.View\TaleWorlds.MountAndBlade.View.Tableaus\CharacterTableau.cs`)
- **LoadingWindowViewModel** (at: `E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade.GauntletUI\TaleWorlds.MountAndBlade.GauntletUI\LoadingWindowViewModel.cs`)

## All TAOM-Referenced Types (108 total)

| Type | Status |
|------|--------|
| AbilityTemplateData | Unchanged |
| ActionIndexCache | Unchanged |
| ActionSetCode | Unchanged |
| Agent | Changed (133 lines) |
| AgentVisuals_Create_Patch | Unchanged |
| AgentVisualsData | Unchanged |
| AiMilitaryBehavior | Changed (-10 lines) |
| AllianceCampaignBehavior | Changed (95 lines) |
| AttackCollisionData | Unchanged |
| Banner | Unchanged |
| Banner_TryGetBannerDataFromCode_Transpiler | Unchanged |
| BannerEditorView | Unchanged |
| BannerlordMissions | Unchanged |
| BesiegerCamp | Unchanged |
| Blow | Unchanged |
| BodyGeneratorView | New |
| Campaign | Changed (1 lines) |
| CampaignSceneNotificationHelper | Unchanged |
| CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler | Unchanged |
| CampaignUIHelper | Changed (30 lines) |
| CharacterCreationCampaignBehavior | Changed (0 lines) |
| CharacterCreationManager | Unchanged |
| CharacterObject | Changed (2 lines) |
| CharacterSpawner | New |
| CharacterTableau | New |
| Clan | Changed (0 lines) |
| ClanPartyItemVM | Changed (-8 lines) |
| CombatLogData | Changed (51 lines) |
| CultureObject | Changed (3 lines) |
| CustomBattleData | Unchanged |
| CustomBattleHelper | Unchanged |
| CustomBattleSideVM | Unchanged |
| DeclareWarAction | Unchanged |
| DefaultAgeModel | Unchanged |
| DefaultAllianceModel | Changed (454 lines) |
| DefaultArmyManagementCalculationModel | Changed (27 lines) |
| DefaultBattleRewardModel | Changed (26 lines) |
| DefaultBuildingConstructionModel | Unchanged |
| DefaultCaravanModel | Unchanged |
| DefaultCharacterStatsModel | Unchanged |
| DefaultClanFinanceModel | Changed (68 lines) |
| DefaultCombatSimulationModel | Changed (9 lines) |
| DefaultDiplomacyModel | Changed (-24 lines) |
| DefaultExecutionRelationModel | Unchanged |
| DefaultHeroCreationModel | Unchanged |
| DefaultInformationRestrictionModel | Unchanged |
| DefaultInventoryCapacityModel | Unchanged |
| DefaultKingdomDecisionPermissionModel | Changed (48 lines) |
| DefaultMapVisibilityModel | Changed (-2 lines) |
| DefaultMapWeatherModel | Changed (2 lines) |
| DefaultMilitaryPowerModel | Changed (5 lines) |
| DefaultMobilePartyFoodConsumptionModel | Unchanged |
| DefaultPartyHealingModel | Unchanged |
| DefaultPartyMoraleModel | Unchanged |
| DefaultPartySizeLimitModel | Unchanged |
| DefaultPartySpeedCalculatingModel | Unchanged |
| DefaultPartyTroopUpgradeModel | Unchanged |
| DefaultPartyWageModel | Unchanged |
| DefaultPregnancyModel | Unchanged |
| DefaultRaidModel | Unchanged |
| DefaultSettlementLoyaltyModel | Changed (0 lines) |
| DefaultSettlementMilitiaModel | Unchanged |
| DefaultSettlementProsperityModel | Unchanged |
| DefaultSmithingModel | Unchanged |
| DefaultTargetScoreCalculatingModel | Changed (-23 lines) |
| DefaultTournamentModel | Unchanged |
| DefaultVillageProductionCalculatorModel | Unchanged |
| DefaultVolunteerModel | Unchanged |
| FaceGen | Unchanged |
| GauntletBannerEditorScreen | Unchanged |
| GuardsCampaignBehavior | Changed (-4 lines) |
| GuardsCampaignBehavior_GetSuitableSpear_Patch | Unchanged |
| GuardsCampaignBehavior_TakeGuardAgentData_Patch | Unchanged |
| Hero | Changed (91 lines) |
| HeroViewModel | Unchanged |
| KillCharacterAction | Unchanged |
| LoadingWindowViewModel | New |
| MakePeaceAction | Unchanged |
| MapConversationTableau_SpawnOpponentBodyguard_Patch | Unchanged |
| MapConversationTableau_SpawnOpponentLeader_Patch | Unchanged |
| MapScene | Unchanged |
| MBMapScene | Unchanged |
| MBTextManager | Unchanged |
| Mission | Changed (53 lines) |
| MissionWeapon | Changed (12 lines) |
| MobilePartyVisual | Changed (2 lines) |
| MobilePartyVisual_AddCharacterToPartyIcon_Patch | Unchanged |
| OrderOfBattleHeroItemVM | Changed (3 lines) |
| PartyBase | Changed (4 lines) |
| PartyCharacterVM | Changed (0 lines) |
| PartyCommand | Unchanged |
| PartyRosterSide | Unchanged |
| PartyScreenLogic | Changed (2 lines) |
| PartyVM | Changed (-2 lines) |
| RecruitmentVM | Unchanged |
| RefreshCharacterEntityAuxPatch | Unchanged |
| RegisterBlowDelegate | Unchanged |
| SandBoxUIHelper | Unchanged |
| SPInventoryVM | Changed (56 lines) |
| SubModule | Unchanged |
| TOwner | Unchanged |
| TraitLevelingHelper | Changed (17 lines) |
| TroopType | Unchanged |
| TroopTypeSelectionPopUpVM | Unchanged |
| TType | Unchanged |
| uint | Unchanged |
| ViewModel | Changed (1 lines) |
| WeakGameEntity | Changed (37 lines) |
