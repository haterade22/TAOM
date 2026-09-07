# Alignment-Aware Execution System

## Overview

The Alignment-Aware Execution system replaces vanilla Bannerlord's one-size-fits-all lord execution penalties with a LOTR-thematic system that considers the moral alignment of the executor, victim, and every evaluating clan leader. When a Free Peoples lord executes a servant of Sauron, there is no dishonor — only justice. When a lord slays one of their own allies, it is kinslaying, punished far more harshly than vanilla.

## Why This Exists

Vanilla Bannerlord applies the same massive penalties to every execution regardless of context:
- **-1000 Honor XP** to the player's Honor trait
- **-60 relation** with the victim's clan
- **-30 relation** with friends of each evaluating clan leader
- **-10 relation** with all same-faction lords and all honorable nobles worldwide

This breaks LOTR immersion. Aragorn executing the Mouth of Sauron shouldn't make him dishonorable. Theoden executing a captured Uruk-hai warlord shouldn't turn Gondor against him. Meanwhile, Denethor executing a Rohan lord should be considered kinslaying — a far graver act than the vanilla system recognizes.

### Design Decisions

These decisions were explicitly confirmed during design:

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Cross-alignment execution penalties | **Zero** for allies of executor | Free/evil factions approve of killing their enemies |
| Neutral kingdoms | **Treated as enemy by both sides** | Umbar, mercenary factions — everyone can execute them freely |
| Same-alignment execution | **50% harsher than vanilla (kinslaying)** | Killing your own side is treachery in Middle-earth |
| AI execution behavior | **Player only** | No AI execution logic; vanilla AI already never executes |

## Vanilla Execution Flow (What We Override)

Understanding the vanilla flow is essential to understanding what this feature changes. This was first determined by decompiling the Bannerlord v1.3.12 DLLs, and re-verified against v1.4.8 on 2026-09-06 while fixing #556. Line numbers below are v1.4.8.

### Entry Points

There are two ways a player triggers an execution:

**1. Party Screen** — `PartyCharacterVM.ExecuteExecuteTroop()` in `TaleWorlds.CampaignSystem.ViewModelCollection.Party`. The player right-clicks a prisoner hero in their party and selects Execute. Eligibility is checked by `PartyScreenLogic.IsExecutable()`:
- Must be `TroopType.Prisoner` in the player's party roster
- Must be a named Hero (not a regular troop)
- Must be of age (`AgeModel.HeroComesOfAge`)
- Must not be during an active `PlayerEncounter`
- Must pass visual maturity check (not Tween or younger)

**2. Post-Battle Conversation** — `LordConversationsCampaignBehavior` offers an execution option during the post-defeat lord conversation dialogue, which triggers the same execution scene.

### Execution Scene

`HeroExecutionSceneNotificationData` plays a cinematic scene (`scn_execution_notification`) where the victim is shown in battle armor (weapons stripped) and the executor holds an `execution_axe` item. Before confirmation, the UI previews relation changes by iterating ALL clan leaders and calling `ExecutionRelationModel.GetRelationChangeForExecutingHero()`.

### Core Kill Chain — `KillCharacterAction.ApplyInternal()`

This is the central method in `TaleWorlds.CampaignSystem.Actions.KillCharacterAction`. It is a `private static` method on a `public static class`. The two execution entry points both call it:
- `ApplyByExecution(victim, executer)` — detail = `KillCharacterActionDetail.Executed` (value 6)
- `ApplyByExecutionAfterMapEvent(victim, executer)` — detail = `KillCharacterActionDetail.ExecutionAfterMapEvent` (value 7)

**Step-by-step flow of `ApplyInternal`:**

1. **CanDie check** — `Hero.CanDie()` fires `CanHeroDie` event, letting active quests block the death. The party screen path calls with `isForced: false`; the conversation path with `isForced: true`.

2. **Deferred death** — If the victim is in an active map event or siege, death is deferred via `Hero.AddDeathMark()`. The actual kill runs later on `AgingCampaignBehavior`'s daily tick.

3. **Pre-kill event** — `OnBeforeHeroKilled` fires. `CommentOnCharacterKilledBehavior` creates a `CharacterKilledLogEntry` and `DeathMapNotification`.

4. **Obituary** — Encyclopedia text: *"{S/He} was executed in {YEAR} at the age of {AGE}."*

5. **Clan leader succession** — If the victim was clan leader, `ChangeClanLeaderAction.ApplyWithoutSelectedNewLeader()` selects an heir via `HeirSelectionCalculationModel`. The new leader **inherits 70% of the dead leader's relations** with all alive heroes. If the victim was also kingdom ruler, a `KingSelectionKingdomDecision` vote is triggered (or the kingdom is destroyed if no eligible clans remain).

6. **Army disbandment** — If the victim was army leader, the army is disbanded.

7. **MakeDead** — State = `Dead`, death day recorded. If the victim was a prisoner, `EndCaptivityAction.ApplyByDeath()`. If they led a party, leadership is transferred or the party is disbanded.

8. **Governor removal** — `ChangeGovernorAction.RemoveGovernorOf()`.

9. **Honor trait penalty** — **THIS IS WHAT WE PATCH.** The vanilla code:
   ```
   if (detail is Executed or ExecutionAfterMapEvent
       && killer == Hero.MainHero
       && victim.Clan != null
       && victim.GetTraitLevel(DefaultTraits.Honor) >= 0)
   {
       TraitLevelingHelper.OnLordExecuted();  // -1000 Honor XP
   }
   ```
   `TraitLevelingHelper.OnLordExecuted()` is a `public static` method on a `public static class` with **no parameters** — it always applies -1000 XP to `Hero.MainHero`. It has a likely vanilla bug: it uses `ActionNotes.SacrificedTroops` instead of a dedicated execution note.

10. **Clan destruction** — If the victim was clan leader and no successor was found, the entire clan is eliminated (all remaining heroes killed, all fiefs redistributed, removed from all wars).

11. **Post-kill event** — `OnHeroKilled` fires, which triggers `CharacterRelationCampaignBehavior` to apply relation changes.

> **Steps 10 and 11 are in that order (L133-143 then L144), and it bites.** `DestroyClanAction.ApplyInternal` calls
> `ChangeKingdomAction.ApplyByLeaveKingdomByClanDestruction`, which nulls `Clan.Kingdom`. So by the
> time the relation pass runs, an executed lord who was the last adult of his clan has **no kingdom
> left to read**. Anything that re-derives the victim's identity during step 11 gets `null`. This is
> what `ExecutionContext` exists to defeat: it snapshots victim and executor at the top of
> `ApplyInternal`, before either step can run. The tell that this is happening in a live game is that
> the pre-confirm preview shows one set of numbers and the applied result is different, because the
> preview runs the same model before the kill. Cost the project #556.

12. **Cleanup** — Spouse nulled (widowed), companion removed, removed from location.

### Vanilla Relation Change Model — `DefaultExecutionRelationModel`

**Class:** `TaleWorlds.CampaignSystem.GameComponents.DefaultExecutionRelationModel` (NOT sealed, inherits from `ExecutionRelationModel`)

The abstract base class `ExecutionRelationModel` (in `TaleWorlds.CampaignSystem.ComponentInterfaces`) defines 10 abstract `int` properties and 1 abstract method:

```
Properties (all abstract int):
  PlayerExecutingHeroClanRelationPenalty           = -60
  PlayerExecutingHeroFriendRelationPenalty          = -30
  PlayerExecutingHeroFactionRelationPenalty         = -10
  PlayerExecutingHeroHonorableNobleRelationPenalty  = -10
  PlayerExecutingHeroClanRelationPenaltyDishonorable    = -30
  PlayerExecutingHeroFriendRelationPenaltyDishonorable  = -15
  PlayerExecutingHeroFactionRelationPenaltyDishonorable = -5
  PlayerExecutingHeroHonorPenalty                       = -1000
  HeroKillingHeroClanRelationPenalty                    = -40
  HeroKillingHeroFriendRelationPenalty                  = -10

Method:
  abstract int GetRelationChangeForExecutingHero(Hero victim, Hero hero, out bool showQuickNotification)
```

`DefaultExecutionRelationModel.GetRelationChangeForExecutingHero()` evaluates each clan leader with this priority chain (first match wins):

**If victim has Honor < 0 (dishonorable):**

| Evaluator relationship | Penalty |
|----------------------|---------|
| Same clan as victim | -30 |
| Friend of evaluating leader | -15 |
| Same faction lord | -5 |

**If victim has Honor >= 0 (honorable/neutral):**

| Evaluator relationship | Penalty |
|----------------------|---------|
| Same clan as victim | -60 |
| Friend of evaluating leader | -30 |
| Same faction lord | -10 |
| Honorable noble (Honor > 0, not rebel clan) | -10 |

**Important quirk:** The method reads penalty values from `Campaign.Current.Models.ExecutionRelationModel.*` (i.e., via the GameModel accessor, resolving to the currently registered model), not from `this.*`. This means if you register a custom model, your overridden property values will be used even when `base.GetRelationChangeForExecutingHero()` is called.

The relation changes are applied by `CharacterRelationCampaignBehavior.OnHeroKilled()` — it only fires for player executions, iterates ALL non-eliminated non-bandit clan leaders, calls the model, and applies changes via `ChangeRelationAction.ApplyPlayerRelation(leader, change, affectRelatives: true)`.

### AI Execution Behavior

**AI lords NEVER proactively execute prisoners** in vanilla. The only AI execution path is in `RebellionsCampaignBehavior` which force-executes rebel clan heroes as cleanup (90+ days after losing all settlements). Execution is effectively a player-only strategic decision.

### Fief Redistribution on Clan Destruction

When a clan is destroyed via `DestroyClanAction`, fiefs are redistributed by `FactionHelper.ChooseHeirClanForFiefs()`:
1. If clan has a kingdom and is NOT the ruling clan → fiefs go to the ruling clan
2. If clan IS the ruling clan → random eligible clan in the kingdom
3. If no eligible kingdom clan → geographically nearest non-eliminated clan with adult lords
4. Last resort → PlayerClan

## How We Override Vanilla

We intercept two independent points in the vanilla execution flow:

### Override 1: Relation Penalties — GameModel Override

**`TaomExecutionRelationModel : DefaultExecutionRelationModel`**

Registered via `campaignStarter.AddModel()` in `SubModule.OnGameStart`. Bannerlord's `GameModels` system resolves the last-registered model, so our model replaces the vanilla one.

We override `GetRelationChangeForExecutingHero()`:
1. Call `base.GetRelationChangeForExecutingHero()` to get the vanilla penalty
2. Build three `ExecutionParticipant` values (kingdom id + culture id). Executor and victim come from `ExecutionContext.ResolveExecutor` / `ResolveVictim`, which return the snapshot when a kill is in flight and the live values otherwise. The evaluator is always live, because the kill does not touch him
3. Delegate to `IExecutionRelationService.GetRelationModifier()`, which resolves each side and applies the alignment logic
4. Return its delta and notification flag

The model is thin and contains **no branching at all**, per `.claude/rules/gamemodels.md` rule 4: the snapshot-or-live choice lives in `ExecutionContext`, and every decision lives in the service.

The live fallback is load-bearing rather than defensive. `HeroExecutionSceneNotificationData` calls this same model to build the pre-confirm relation preview, when no kill is in flight and nothing has been destroyed. Routing preview and applied value through one model is what keeps them agreeing; a divergence between the two is the symptom to look for when this feature misbehaves.

### Override 2: Honor Penalty — Two Harmony Patches

The honor penalty is applied deep inside `KillCharacterAction.ApplyInternal()` via `TraitLevelingHelper.OnLordExecuted()` — a parameterless static method. We can't override it via GameModel, and we can't know the victim/killer inside `OnLordExecuted()` since it takes no arguments.

**Solution: Thread-local execution context.**

**Patch A — `KillCharacterAction_ApplyInternal_Patch`:**
- **Target:** `KillCharacterAction.ApplyInternal` (private static, accessed via `AccessTools.Method`)
- **Prefix:** Before `ApplyInternal` runs, if the action is `Executed` or `ExecutionAfterMapEvent`, store the victim's and killer's kingdom StringIds into `ExecutionContext` (thread-local static)
- **Finalizer:** Always clears the context, even if `ApplyInternal` throws

**Patch B — `TraitLevelingHelper_OnLordExecuted_Patch`:**
- **Target:** `TraitLevelingHelper.OnLordExecuted` (public static, patched via `[HarmonyPatch]` attribute)
- **Prefix:** Reads the thread-local `ExecutionContext`. If the execution is cross-alignment (enemy kill), returns `false` to skip the original method (no -1000 Honor XP). If context is missing, falls through to vanilla.

Both patches share `[HarmonyPatchCategory("Patch14_Execution")]` and are applied in `SubModule.OnGameInitializationFinished`.

### Thread-Local Context Pattern

`ExecutionContext` uses `System.Threading.ThreadLocal<string>` for two values: `VictimKingdomId` and `ExecutorKingdomId`. This is necessary because:
- `KillCharacterAction.ApplyInternal` is the only place that has both `victim` and `killer`
- `TraitLevelingHelper.OnLordExecuted` has neither — it just applies -1000 XP to MainHero
- The call from `ApplyInternal` → `OnLordExecuted` is synchronous and on the same thread
- The Finalizer guarantees cleanup even on exceptions

## Alignment System

### Data Model

Each Bannerlord kingdom is assigned one of three sides:

```
enum FactionSide { Free, Evil, Neutral }
```

The mapping is stored in `Main/_Module/ModuleData/execution/alignment.json`. It is a single flat
table read in two roles: as kingdom ids by `GetKingdomSide`, and as culture ids by `GetCultureSide`.
`ResolveSide(kingdomId, cultureId)` tries the kingdom first and falls back to the culture, which is
what places a hero whose clan has no kingdom.

The authoritative id list is the file itself; the table below is the reading of it. In-game names
come from `Main/_Module/ModuleData/spkingdoms.xslt`, which renames eight vanilla kingdoms in place,
and `taom_spkingdoms.xml`, which adds the rest. **The id almost never matches the LOTR name**, so
never infer one from the other.

### Kingdom ID Reference

| Kingdom StringId | LOTR name | Side | Vanilla origin |
|---|---|---|---|
| `empire_w` | Gondor | Free | Western Empire |
| `vlandia` | Rohan | Free | Vlandia |
| `sturgia` | Dale | Free | Sturgia |
| `erebor` | Erebor | Free | Custom kingdom |
| `rivendell` | Rivendell | Free | Custom kingdom |
| `lothlorien` | Lothlorien | Free | Custom kingdom |
| `mirkwood` | Mirkwood | Free | Custom kingdom |
| `lindon` | Lindon | Free | Custom kingdom |
| `empire` | Dunland | Evil | Empire (main) |
| `empire_s` | Mordor | Evil | Southern Empire |
| `aserai` | Harad | Evil | Aserai |
| `khuzait` | Rhun (Easterlings) | Evil | Khuzait |
| `isengard` | Isengard | Evil | Custom kingdom |
| `gundabad` | Gundabad | Evil | Custom kingdom |
| `dolguldur` | Dol Guldur | Evil | Custom kingdom |
| `goblin` | Goblin-town | Evil | Custom kingdom |
| `mistymountainorcs` | Misty Mountain orcs | Evil | Custom kingdom |
| `bluecraig` | Blue Craig | Evil | Custom kingdom |
| `battania` | Khand | Neutral | Battania |
| `umbar` | Umbar (Corsairs) | Neutral | Custom kingdom |
| `shaghana` | Shaghana | Neutral | Custom kingdom |
| `abanissa` | Abanissa | Neutral | Custom kingdom |

That is every kingdom the game actually loads, verified against the live install.

### Culture ID Reference

Playable cultures nearly all share an id with their kingdom, so the same table answers both
questions. Gondor and Mordor are the two that do not, and they carry their own entries so the
culture fallback can place a kingdom-less hero:

| Culture StringId | Kingdom | Side |
|---|---|---|
| `gondor` | `empire_w` | Free |
| `mordor` | `empire_s` | Evil |

`ShippedMainCultureAlignmentCoverageTests` fails the build if a playable culture in
`taom_spcultures.xml` has no entry here, because an unlisted culture resolves Neutral and silently
switches the fallback off for anyone playing it.

Not to be confused with `Main/_Module/ModuleData/factionmap/factions.json`, which is UI-level data
for the FactionMap and uses LOTR names as `game_faction` values.

### Alignment Logic

`AlignmentService` answers four questions:

**`GetKingdomSide(string kingdomId)`** and **`GetCultureSide(string cultureId)`** return `FactionSide.Neutral` for null, empty, or unknown ids. Lookup is case-insensitive and both read the same table.

**`ResolveSide(string kingdomId, string cultureId)`** takes the kingdom side first; when that reads Neutral, the culture side; Neutral when neither classifies. **This is what execution resolves every participant through, and it is not optional.** A kingdom id alone reads Neutral for an independent, mercenary or enlisted player (enlistment deliberately does not join the commander's kingdom), for a minor or mercenary clan leader, and for a victim whose clan was destroyed by the very kill being evaluated. The three sibling alignment features (CaravanTrade, WarOfTheRingMomentum, PrisonerRecruitment) each carried a private copy of this fallback; execution did not, which is #556.

**`AreEnemyAlignments(string A, string B)`** — Returns `true` if the sides differ OR if either side is Neutral. Neutral kingdoms are treated as enemies of everyone, including other neutrals. This means:
- Free vs Evil = enemies
- Free vs Neutral = enemies
- Evil vs Neutral = enemies
- Neutral vs Neutral = enemies
- Free vs Free = NOT enemies
- Evil vs Evil = NOT enemies

**`AreSameAlignment(string A, string B)`** — Returns `true` only if both are the same non-Neutral side. Neutral+Neutral returns `false` — neutrals are never considered allies.

Both predicates also have `FactionSide` overloads for callers that have already resolved their sides. The string overloads delegate to them, so the Neutral semantics above have exactly one definition.

**There is deliberately no "unknown, defer to vanilla" escape hatch.** Falling back to vanilla looks conservative and is not: vanilla charges -10 to every clan leader in the world whose Honor is above 0, which in TAOM is the entire Free Peoples. One unresolved id used to hand the whole calculation to that rule. A participant that classifies on neither id now resolves Neutral and is simply nobody's ally.

## Penalty Matrix

### Cross-Alignment Execution (Good kills Evil, Evil kills Good, anyone kills Neutral)

The executor kills someone from an opposing alignment. This is a "righteous" or "expected" kill.

| Who evaluates the kill | Relation change | Honor XP | Rationale |
|----------------------|----------------|----------|-----------|
| Clan leader aligned with **executor** | **0** | — | "Good riddance" |
| Clan leader aligned with **victim** | **Vanilla** (-60/-30/-10) | — | Enemies still mourn their own |
| Neutral clan leader | **0** | — | Neutrals don't care |
| Player (Honor trait) | — | **0** | No dishonor in killing your enemy |

### Same-Alignment Execution — Kinslaying (Good kills Good, Evil kills Evil)

The executor betrays their own side. This is kinslaying.

| Who evaluates the kill | Relation change | Honor XP | Rationale |
|----------------------|----------------|----------|-----------|
| Same clan as victim | **-90** (vanilla -60 x 1.5) | — | Kinslaying penalty |
| Friend of evaluating leader | **-45** (vanilla -30 x 1.5) | — | Kinslaying penalty |
| Same faction lord | **-15** (vanilla -10 x 1.5) | — | Kinslaying penalty |
| Honorable noble | **-15** (vanilla -10 x 1.5) | — | Kinslaying penalty |
| Player (Honor trait) | — | **-1000** (vanilla) | Standard dishonor applies |

The 1.5x multiplier is defined as `KinslayingMultiplier` in `ExecutionActionHook`.

### Unclassified Factions

There is no vanilla-passthrough fallback. A participant that classifies on neither its kingdom id nor
its culture id resolves `Neutral`, and Neutral is nobody's ally and everybody's enemy, so a Neutral
executor's kill costs only the victim's own side and a Neutral evaluator never charges anything.

A stated consequence, not an accident of the predicates: **executing a Neutral-aligned lord costs
nothing from anyone, including that lord's own faction, and docks no Honor.** Khand, Umbar, Shaghana
and Abanissa are all Neutral, so a Corsair captain can be executed with no political cost at all.
That follows from "Neutral is nobody's ally", it was reviewed and kept deliberately on 2026-09-06,
and it is written down here so a future reader does not re-flag it as a gap. Making a Neutral
victim's own faction react would mean giving Neutral a same-side relation of its own, which changes
the meaning of Neutral everywhere the alignment table is used, not just in execution.

This used to be a passthrough, and that was the #556 bug: vanilla's penalty chain ends in -10 to
every clan leader in the world whose Honor is above 0, so "defer to vanilla" meant "charge the entire
Free Peoples". A missing classification must degrade to indifference, never to the vanilla rule.

## Architecture

### Component Diagram

```
alignment.json
      |
AlignmentConfigProvider ── IPathService
      |
AlignmentService (IAlignmentService)
      |
ExecutionActionHook (IOnExecutionAction)
     / \
    /   \
   /     \
TaomExecution         TraitLevelingHelper
RelationModel         _OnLordExecuted_Patch
(GameModel override)      |
   |               ExecutionContext (ThreadLocal)
   |                      |
   |               KillCharacterAction
   |               _ApplyInternal_Patch
   |                      |
   +---------- both read from IOnExecutionAction
```

### Data Flow for a Cross-Alignment Execution

```
Player clicks Execute on Mordor lord (party screen)
    |
    v
[Vanilla] PartyScreenLogic.IsExecutable() → true
    |
    v
[Vanilla] HeroExecutionSceneNotificationData — shows relation preview
    |  (calls TaomExecutionRelationModel.GetRelationChangeForExecutingHero
    |   for each clan leader — cross-alignment allies see 0)
    v
Player confirms → KillCharacterAction.ApplyByExecution(victim, Hero.MainHero)
    |
    v
KillCharacterAction.ApplyInternal(victim, killer, Executed, ...)
    |
    +--[Patch A Prefix]-- ExecutionContext.Set(mordor, gondor)
    |
    v
[Vanilla] ... obituary, succession, army, MakeDead ...
    |
    v
[Vanilla] Checks: is Executed? killer == MainHero? victim.Honor >= 0?
    |
    v
TraitLevelingHelper.OnLordExecuted()
    |
    +--[Patch B Prefix]-- Reads ExecutionContext → mordor vs gondor
    |                      → AreEnemyAlignments = true
    |                      → ShouldApplyHonorPenalty = false
    |                      → return false (SKIP vanilla -1000 XP)
    v
[Vanilla] CampaignEventDispatcher.OnHeroKilled
    |
    v
CharacterRelationCampaignBehavior.OnHeroKilled
    |  For each clan leader:
    |    calls TaomExecutionRelationModel.GetRelationChangeForExecutingHero
    |      → base returns vanilla -60 (same clan)
    |      → GetRelationModifier(executor, victim, evaluator, -60)
    |        → each participant: ResolveSide(kingdomId, cultureId)
    |          (victim's kingdom is already null here if his clan was
    |           just destroyed — the snapshot is what saves this)
    |        → executor Free vs victim Evil: cross-alignment
    |        → evaluator Free?    return 0
    |        → evaluator Evil?    return -60 (vanilla)
    |        → evaluator Neutral? return 0
    |      → applies modified value via ChangeRelationAction
    v
[Patch A Finalizer] ExecutionContext.Clear()
```

### Dependency Chain

```
ExecutionIoC registers:
  IAlignmentConfigProvider → AlignmentConfigProvider (loads alignment.json)
  IAlignmentService → AlignmentService (kingdom side lookups)
  IOnExecutionAction → ExecutionActionHook (penalty calculations)

SubModule.OnSubModuleLoad:
  ExecutionIoC.InitializeHooks(executionHook)
    → TraitLevelingHelper_OnLordExecuted_Patch.Initialize(hook)

SubModule.OnGameStart:
  campaignStarter.AddModel(new TaomExecutionRelationModel(executionAction))

SubModule.OnGameInitializationFinished:
  _harmony.PatchCategory("Patch14_Execution")
    → Applies KillCharacterAction_ApplyInternal_Patch
    → Applies TraitLevelingHelper_OnLordExecuted_Patch
```

## Key Files

| File | Purpose | Lines |
|------|---------|-------|
| `Main/Features/Execution/FactionSide.cs` | Enum: Free, Evil, Neutral | ~8 |
| `Main/Features/Execution/IAlignmentService.cs` | Alignment query interface, incl. `ResolveSide` | ~35 |
| `Main/Features/Execution/AlignmentService.cs` | Kingdom/culture side lookups, `ResolveSide`, side predicates | ~70 |
| `Main/Features/Execution/IAlignmentConfigProvider.cs` | Config loader interface | ~8 |
| `Main/Features/Execution/AlignmentConfigProvider.cs` | Reads alignment.json via IPathService | ~40 |
| `Main/Features/Execution/ExecutionIoC.cs` | IoC registration + hook init | ~18 |
| `Main/Features/Execution/Models/TaomExecutionRelationModel.cs` | GameModel override: boundary conversion + delegate | ~44 |
| `Main/Features/Execution/IExecutionRelationService.cs` | `ExecutionParticipant`, `ExecutionRelationResult`, service contract | ~66 |
| `Main/Features/Execution/ExecutionRelationService.cs` | Relation decision: side resolution, kinslaying, notification | ~62 |
| `Main/Features/Execution/Hooks/IOnExecutionAction.cs` | Hook interface (honor penalty only) | ~7 |
| `Main/Features/Execution/Hooks/ExecutionActionHook.cs` | Honor-penalty decision | ~25 |
| `Main/Features/Execution/Hooks/ExecutionContext.cs` | Thread-local victim/killer snapshot (kingdom + culture) | ~78 |
| `Main/Features/Execution/Hooks/KillCharacterAction_ApplyInternal_Patch.cs` | Harmony prefix/finalizer on ApplyInternal | ~30 |
| `Main/Features/Execution/Hooks/TraitLevelingHelper_OnLordExecuted_Patch.cs` | Harmony prefix — skips honor penalty | ~30 |
| `Main/_Module/ModuleData/execution/alignment.json` | Kingdom → side mapping (16 entries) | ~18 |

### Modified Files

| File | Change |
|------|--------|
| `Main/IoC.cs` | Added `ExecutionIoC.RegisterExecutionFeature(container)` |
| `Main/SubModule.cs` | Added using statements, hook init in `OnSubModuleLoad`, model registration in `OnGameStart`, `Patch14_Execution` category in `OnGameInitializationFinished` |

## Tests

### `TAOM.Tests/Features/Execution/AlignmentServiceTests.cs` — 15 tests

Tests the `AlignmentService` with mocked `IAlignmentConfigProvider`:
- `GetKingdomSide` — Free, Evil, Neutral, Unknown (→ Neutral), Null (→ Neutral)
- `AreEnemyAlignments` — Free vs Evil, Evil vs Free, Free vs Neutral, Evil vs Neutral, Free vs Free (false), Evil vs Evil (false), Neutral vs Neutral (true)
- `AreSameAlignment` — Free vs Free (true), Evil vs Evil (true), Free vs Evil (false), Neutral vs Neutral (false), Free vs Neutral (false), Null (false)

### `TAOM.Tests/Features/Execution/ExecutionActionHookTests.cs` — 13 tests

Tests the `ExecutionActionHook` with mocked `IAlignmentService`:
- `ShouldApplyHonorPenalty` — Cross-alignment (false), Same-alignment (true)
- `IsKinslaying` — Same (true), Cross (false)
- `GetRelationModifier` — Cross-alignment with executor's ally evaluator (→ 0), victim's ally evaluator (→ vanilla), neutral evaluator (→ 0); Kinslaying with -60 (→ -90), -30 (→ -45), -10 (→ -15)

## Vanilla Classes Referenced

All re-verified against decompiled Bannerlord v1.4.8 DLLs (2026-09-06, #556):

| Class | Namespace | Why Referenced |
|-------|-----------|---------------|
| `KillCharacterAction` | `TaleWorlds.CampaignSystem.Actions` | Harmony patch on `ApplyInternal` — the core kill method |
| `KillCharacterAction.KillCharacterActionDetail` | (nested enum) | Values `Executed` (6) and `ExecutionAfterMapEvent` (7) |
| `TraitLevelingHelper` | `TaleWorlds.CampaignSystem.CharacterDevelopment` | Harmony patch on `OnLordExecuted()` — the honor penalty method |
| `ExecutionRelationModel` | `TaleWorlds.CampaignSystem.ComponentInterfaces` | Abstract base class for our GameModel override |
| `DefaultExecutionRelationModel` | `TaleWorlds.CampaignSystem.GameComponents` | Concrete base class we inherit from |
| `CharacterRelationCampaignBehavior` | `TaleWorlds.CampaignSystem.CampaignBehaviors` | Calls our model for each clan leader on `OnHeroKilled` |
| `PartyScreenLogic` | `TaleWorlds.CampaignSystem.Party` | `IsExecutable()` — eligibility check (not patched) |
| `HeroExecutionSceneNotificationData` | `TaleWorlds.CampaignSystem.SceneInformationPopupTypes` | Execution cutscene + relation preview UI (not patched) |
| `Hero` | `TaleWorlds.CampaignSystem` | `Clan.Kingdom.StringId` — how we determine alignment |
| `ChangeClanLeaderAction` | `TaleWorlds.CampaignSystem.Actions` | Successor selection + 70% relation inheritance (not patched) |
| `DestroyClanAction` | `TaleWorlds.CampaignSystem.Actions` | Clan elimination + fief redistribution (not patched) |

## How to Add a New Kingdom

1. Add a new entry to `Main/_Module/ModuleData/execution/alignment.json` with the kingdom's StringId and side
2. No code changes needed — `AlignmentService` reads the JSON at startup

## How to Change the Kinslaying Multiplier

Edit `KinslayingMultiplier` in `Main/Features/Execution/Hooks/ExecutionActionHook.cs`. Currently `1.5f` (50% harsher than vanilla). Setting to `1.0f` would match vanilla for kinslaying; `2.0f` would double vanilla penalties.

## How to Test In-Game

1. Start a campaign as Gondor (empire_w)
2. Capture a Mordor (empire_s) lord in battle
3. Open party screen → right-click the prisoner → Execute
4. **Expected:** Relation preview shows 0 changes for Free Peoples clans, normal penalties only for Evil clans. After execution, no Honor trait penalty in the character screen.
5. Capture a Rohan (empire/vlandia) lord
6. Execute them
7. **Expected:** Relation preview shows -90 for same-clan (kinslaying). Honor penalty applies normally.

## Related Documentation

- [War of the Ring](war-of-the-ring.md) — The diplomacy system that defines hostile kingdoms
- [Architecture](../ai-includes/architecture.md) — TAOM architecture patterns
- [Harmony Patches](../../.claude/rules/harmony-patches.md) — Patch conventions
- `Main/_Module/ModuleData/diplomacy/diplomacy.json` — Kingdom relationship tiers
- `Main/_Module/ModuleData/factionmap/factions.json` — UI-level faction data (uses LOTR names, NOT kingdom StringIds)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/execution.md](./execution.md)
- [docs/features/prisoner-recruitment.md](./prisoner-recruitment.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/configs-factions-and-world.md](../modding/configs-factions-and-world.md)

<!-- backlinks-end -->
## Changelog

- 2026-09-06 (#556): fixed the fall-through that made this whole feature skippable. `ExecutionRelationService` and the honor patch both handed the calculation back to vanilla whenever any one kingdom id was null or empty, and vanilla charges -10 to every honourable clan leader in the world. Reachable two ways: a kingdom-less executor (independent, mercenary, or enlisted player), and a victim whose `Clan.Kingdom` was nulled by the clan destruction that step 10 performs before step 11 fires the relation pass. Added `IAlignmentService.ResolveSide(kingdomId, cultureId)` plus `FactionSide` overloads of the two predicates, moved the relation service onto `ExecutionParticipant`, extended `ExecutionContext` to snapshot culture as well as kingdom for both victim and executor, added `IPlayerContextAdapter.GetPlayerCultureId()`, and deleted the dead duplicate relation logic (`ExecutionActionHook.GetRelationModifier` / `IsKinslaying`). New build-time gate `ShippedMainCultureAlignmentCoverageTests`. Corrected the kingdom mapping tables above, which had `vlandia` as Arthedain and `empire` as a free Rohan; the XSLT says `vlandia` is Rohan and `empire` is Dunland. The deep review then found that the Harmony finalizer cleared the snapshot unconditionally, so a nested kill during clan destruction wiped it mid-flight; ownership now runs through `ExecutionContext.TrySet` / `ClearIfOwned` and Harmony's `__state`. RCA: `docs/reviews/rca-execution-alignment-fallthrough-2026-09-06.md`.
- 2026-05-14 — Phase 9b extraction (#147): pulled `IExecutionRelationService` out of `TaomExecutionRelationModel` (returns `ExecutionRelationResult { RelationDelta, ShowNotification }`), reduced the model body to a single-call delegate, and replaced direct `Hero.MainHero.MapFaction.StringId` access with injected `IPlayerContextAdapter.GetPlayerKingdomId()`.
- 2026-03-25 — Introduced the Alignment-Aware Execution System: `Main/Features/Execution/` with `TaomExecutionRelationModel` GameModel override + `KillCharacterAction.ApplyInternal` / `TraitLevelingHelper.OnLordExecuted` Harmony patches (`Patch14_Execution`), `alignment.json` (16 kingdoms → Free/Evil/Neutral), zero-penalty cross-alignment kills, 1.5x kinslaying penalties, and 28 tests.
