# Codex Adversarial Review: Diplomacy + Execution

**Date:** 2026-04-05
**Target:** working tree diff
**Verdict:** needs-attention

No-ship. The change has config and state-handling holes that materially break the intended lore logic: some active kingdoms are outside both the diplomacy and execution matrices, the scripted phase-1 wars do not match the documented faction mapping, and full-war / honor-penalty enforcement has exploitable gaps.

## Section 1: Vanilla Code

### DefaultExecutionRelationModel.GetRelationChangeForExecutingHero (decompiled)

```csharp
if (hero.Clan == victim.Clan) result = ...ClanRelationPenalty;
else if (victim.IsFriend(hero)) result = ...FriendRelationPenalty;
else if (hero.MapFaction == victim.MapFaction && hero.CharacterObject.Occupation == Occupation.Lord) result = ...FactionRelationPenalty;
else if (hero.GetTraitLevel(DefaultTraits.Honor) > 0 && !victim.Clan.IsRebelClan) result = ...HonorableNobleRelationPenalty;
```

### DeclareWarAction.ApplyInternal (decompiled)

```csharp
FactionManager.DeclareWar(faction1, faction2);
...
CampaignEventDispatcher.Instance.OnWarDeclared(faction1, faction2, declareWarDetail);
```

### DefaultKingdomDecisionPermissionModel.IsPeaceDecisionAllowedBetweenKingdoms (decompiled)

```csharp
if (!Campaign.Current.Models.DiplomacyModel.IsAtConstantWar(kingdom1, kingdom2))
{
    ...
    return true;
}
reason = new TextObject("{=eNPupZOp}These kingdoms can not declare peace at this time.");
return false;
```

### TraitLevelingHelper.OnLordExecuted (decompiled)

```csharp
if ((actionDetail == KillCharacterActionDetail.Executed || actionDetail == KillCharacterActionDetail.ExecutionAfterMapEvent)
    && killer == Hero.MainHero && victim.Clan != null && victim.GetTraitLevel(DefaultTraits.Honor) >= 0)
{
    TraitLevelingHelper.OnLordExecuted();
}
```

## Section 2: Vanilla Analysis

- **GetScoreOfStartingAlliance:** TAOM's +1000/-10000 modifiers interact with the vanilla score as additive bonuses, which correctly overwhelms the base calculation when lore alignment demands it.
- **IsAtConstantWar:** Vanilla has no hardcoded factions. TAOM's override adds lore-based constant war via config.
- **GetRelationChangeForExecutingHero:** Vanilla applies penalties based on clan/friend/faction/honor relationships. TAOM replaces this with alignment-based classification.

## Findings

### [HIGH] Alignment config omits active Harad kingdoms — execution penalties silently fall back to Neutral

**File:** `alignment.json:1-18`, `AlignmentService.cs`

**TAOM code:**
```csharp
if (string.IsNullOrEmpty(kingdomId))
    return FactionSide.Neutral;
return _kingdomSides.TryGetValue(kingdomId, out var side) ? side : FactionSide.Neutral;
```

`alignment.json` has no `shaghana` or `abanissa` entries, but `TAOM_spkingdoms.xml` defines both kingdoms (id="shaghana" at line 717 and id="abanissa" at line 812) as Harad realms serving Sauron. Because unknown kingdoms become `Neutral`, executions of Shaghana/Abanissa lords are treated as cross-alignment/neutral cases instead of evil-side cases, suppressing allied approval/disapproval incorrectly.

**Remediation:** Add `shaghana` and `abanissa` with their intended side (`evil`), and validate the loaded map against the live kingdom roster during startup instead of defaulting missing IDs to `Neutral`.

### [HIGH] Diplomacy matrix incomplete for evil kingdoms — War of the Ring auto-war and peace blocking never apply

**File:** `diplomacy.json:1-76`, `DiplomacyService.cs`

**TAOM code:**
```csharp
return _relationships.TryGetValue(key, out var tier) ? tier : AllianceTier.Neutral;
```

`diplomacy.json` contains no entries for `battania`, `shaghana`, or `abanissa`. Since missing pairs default to `Neutral`, these factions are outside every lore guard: hostile-tier auto-war never fires, `ShouldBlockPeace` never blocks peace, hostile alliance scoring is never applied, and permanent/natural alliance rules are never enforced.

**Remediation:** Populate `diplomacy.json` for `battania`, `shaghana`, and `abanissa`, then validate that every active kingdom has the expected hostile/natural/permanent relationships before the campaign starts.

### [HIGH] Phase-1 war script does not match documented faction mapping — wrong opening wars

**File:** `war_of_the_ring.json:3-8`

Docs say Phase 1 is the IsengardWar where Isengard attacks Rohan. Config instead says:
```json
"phase1": {
  "wars": [
    { "attacker": "isengard", "defender": "vlandia" },
    { "attacker": "empire",   "defender": "vlandia" }
  ]
}
```

TAOM mapping: `empire` = Rohan, `vlandia` = Arthedain. So Phase 1 currently starts Isengard vs Arthedain plus Rohan vs Arthedain, not Isengard vs Rohan. User-visible lore break at the main scripted event.

**Remediation:** Correct the phase-1 kingdom IDs in `war_of_the_ring.json` and add an integration test asserting configured attacker/defender IDs match the documented kingdom mapping.

### [HIGH] Independent player executions bypass vanilla Honor loss — null executor kingdoms treated as enemy alignment

**File:** `TraitLevelingHelper_OnLordExecuted_Patch.cs:17-29`

**TAOM code:**
```csharp
if (!ExecutionContext.HasContext) return true;
var victimKingdomId = ExecutionContext.GetVictimKingdomId();
var executorKingdomId = ExecutionContext.GetExecutorKingdomId();
if (!_hook.ShouldApplyHonorPenalty(victimKingdomId, executorKingdomId))
    return false;
```

`AlignmentService` treats null/unknown kingdoms as `Neutral`, and `AreEnemyAlignments` returns `true` when either side is `Neutral`:
```csharp
if (string.IsNullOrEmpty(kingdomId)) return FactionSide.Neutral;
if (sideA == FactionSide.Neutral || sideB == FactionSide.Neutral) return true;
```

If the player is independent (`Hero.MainHero.Clan.Kingdom == null`), TAOM classifies the execution as cross-alignment and suppresses vanilla Honor loss. The feature docs say null kingdom IDs should preserve vanilla behavior.

**Remediation:** Add a null/unknown guard before calling `ShouldApplyHonorPenalty`. Fall through to vanilla whenever either kingdom ID is missing or unmapped.

### [MEDIUM] Full-war peace blocking is transient state only — save/load resets to Peace until next daily tick

**File:** `WarOfTheRingBehavior.cs:17-29`, `WarOfTheRingService.cs`

**TAOM code:**
```csharp
public WarPhase CurrentPhase { get; private set; } = WarPhase.Peace;
// WarOfTheRingBehavior.cs
public override void SyncData(IDataStore dataStore) { }
CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
```

`CurrentPhase` is not serialized and is only recomputed on `DailyTickEvent`. Loading a late-game save reopens a window where hostile kingdoms can make peace until the next day tick.

**Remediation:** Serialize the phase or call `CheckPhaseTransition` on session launch/map load so `IsWarOfTheRingActive` is restored before any diplomacy decisions can execute.

## Recommended Next Steps

1. Fix config holes first: complete `alignment.json`, `diplomacy.json`, and `war_of_the_ring.json` against the live kingdom roster
2. Add startup validation that fails loudly when any active kingdom ID is missing from either alignment or diplomacy config
3. Close the honor exploit by falling back to vanilla when execution context IDs are null/unknown
4. Persist or eagerly recompute War of the Ring phase on load before diplomacy logic runs
5. Fix phase-1 war pairs to match documented Isengard-vs-Rohan mapping
