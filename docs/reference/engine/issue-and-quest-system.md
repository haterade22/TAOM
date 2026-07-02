# Bannerlord issue & quest system — IssueBase / QuestBase / IssueManager / QuestManager

> **One subsystem, traced from the decompile** (v1.4.5): how the campaign generates the procedural "problems at a
> notable" the player solves (the `IssueBase` issues), how an issue becomes a `QuestBase`, and every seam a mod can use
> to add, tune, or remove them. This is the engine reference behind TAOM's [LOTR issue conversion](../../features/lotr-issues.md) (implemented 2026-06-20 — all 43 vanilla issues suppressed + replaced).
> Sibling of [campaignevents-and-campaignbehavior](./campaignevents-and-campaignbehavior.md) and
> [campaign-tick-time-and-party-ai](./campaign-tick-time-and-party-ai.md); for how custom quests persist, see
> [save-system](./save-system.md).
>
> **TAOM is sandbox-only** (no `StoryMode` dependency; `StoryModeNewGame` hidden — see
> [MainMenuCustomizer](../../../Main/Features/MainMenuCustomizer/MainMenuCustomizerService.cs)), so the StoryMode main
> storyline (Dragon Banner / conspiracy) never spawns. This doc covers the **procedural issue system**, which is the
> entire quest surface that actually runs in TAOM.

## WHAT it is — Issue vs Quest vs SpecialQuest

### IssueBase — the "problem at a notable"

An **`IssueBase`** (`IssueBase.cs:20`, `: MBObjectBase`) is a campaign-map problem owned by one `Hero` (`IssueOwner`,
`IssueBase.cs:221`) and tied to that notable's settlement (`IssueSettlement`, `IssueBase.cs:246`). It carries a due
time (`IssueDueTime`, `IssueBase.cs:101`), journal logs, and presentation text. Subclasses must implement `Title`
(`IssueBase.cs:241`) and `Description` (`IssueBase.cs:258`), the dialogue getters `IssueBriefByIssueGiver` /
`IssueAcceptByPlayer` / `IssueQuestSolutionExplanationByIssueGiver` / `IssueQuestSolutionAcceptByPlayer`
(`IssueBase.cs:128–142`), plus `GetFrequency`, `CanPlayerTakeQuestConditions`, `IssueStayAliveConditions`, `OnGameLoad`,
`HourlyTick`, and `GenerateIssueQuest` (`IssueBase.cs:521–533`).

**States** (internal `IssueState`, `IssueBase.cs:22`):

| State | Meaning | Predicate |
|-------|---------|-----------|
| Ongoing | exists, no path chosen | `IsOngoingWithoutQuest` (`IssueBase.cs:263`) |
| SolvingWithQuestSolution | player took the quest | `IssueBase.cs:265` |
| SolvingWithAlternativeSolution | player sent a companion + troops | `IssueBase.cs:267` |
| SolvingWithLordSolution | resolved via influence | `IssueBase.cs:269` |

**The three solution paths:**
1. **Quest** — `StartIssueWithQuest()` (`IssueBase.cs:584`) calls the abstract `GenerateIssueQuest(questId)`
   (`IssueBase.cs:525`) and stores the result in the saveable `IssueQuest` (`IssueBase.cs:243`).
2. **Alternative** — `StartIssueWithAlternativeSolution()` (`IssueBase.cs:595`) dispatches a companion + troops. Gated by
   `IsThereAlternativeSolution` (`IssueBase.cs:173`); `IsTroopTypeNeededByAlternativeSolution(CharacterObject)`
   (`IssueBase.cs:446`) filters eligible troops; `AlternativeSolutionBaseNeededMenCount` (`IssueBase.cs:166`) +
   `AlternativeSolutionScaleFlags` (`IssueBase.cs:285`) size it.
3. **Lord** — `StartIssueWithLordSolution()` (`IssueBase.cs:791`), gated by `IsThereLordSolution` (`IssueBase.cs:207`),
   spends `NeededInfluenceForLordSolution` (`IssueBase.cs:215`).

### QuestBase — created only when the quest path is taken

A **`QuestBase`** (`QuestBase.cs:14`) holds its own giver, due time, reward gold, task list, journal, and dialogue
flows (`OfferDialogFlow` etc., `QuestBase.cs:35–51`). Subclasses implement `Title` (`QuestBase.cs:81`),
`IsRemainingTimeHidden` (`QuestBase.cs:83`), `SetDialogs` (`QuestBase.cs:139`), and `InitializeQuestOnGameLoad`
(`QuestBase.cs:465`).

**`SpecialQuestType`** defaults to empty (`QuestBase.cs:89`); `IsSpecialQuest => !string.IsNullOrEmpty(SpecialQuestType)`
(`QuestBase.cs:87`). Issue-spawned quests leave it empty. A **non-empty override marks a standalone quest that is NOT
auto-cancelled on load** (the lifecycle rule below) — the lever a quest with no backing issue must use to survive
save/load. TAOM's [`CareerQuest`](../../../Main/Features/CareerSystem/Quests/CareerQuest.cs) is the working precedent.

### Packaging — one CampaignBehaviorBase per issue

Each vanilla issue is a `CampaignBehaviorBase` nesting its issue + quest + a `SaveableTypeDefiner`. Example:
`CaravanAmbushIssueBehavior` (`CaravanAmbushIssueBehavior.cs:21`) nests `CaravanAmbushIssue : IssueBase` (`:23`),
`CaravanAmbushIssueQuest : QuestBase` (`:272`), and a `SaveableTypeDefiner` registering both (`:838`). `RegisterEvents`
subscribes `OnCheckForIssue` (`:860`) → `AddPotentialIssueData` (`:867`); `GenerateIssueQuest` (`:196`) builds the quest
only when the player chooses that path.

## WHERE issues register — the authoritative sandbox set (43)

Issue behaviors are registered at **two** call sites, both during sandbox campaign initialization. **StoryMode
registers zero issue behaviors.**

| Site | Count | Location |
|------|-------|----------|
| `SandBoxManager.Initialize(CampaignGameStarter)` | **36** | `SandBoxManager.cs:27` (method); `gameStarter.AddBehavior(new …IssueBehavior())` at `:162–197` |
| `SandBoxSubModule.InitializeGameStarter` | **7** | `SandBoxSubModule.cs:29` (method); `:74–81` |

The 36 in `SandBoxManager` include the 3 complex `*IssueQuestBehavior` classes (`GangLeaderNeedsWeapons` `:170`,
`LordNeedsGarrisonTroops` `:181`, `MerchantNeedsHelpWithOutlaws` `:189`). The 7 SandBox-module issues live under
`E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Issues\` (`RivalGangMovingIn`, `RuralNotableInnAndOut`, `FamilyFeud`,
`NotableWantsDaughterFound`, `TheSpyParty` [quest], `ProdigalSon`, `SnareTheWealthy`).

`IssuesCampaignBehavior` itself (`SandBoxManager.cs:158`) is the **host spawner**, not an issue — keep it; remove only
the issue behaviors. The full per-issue inventory + LOTR disposition lives in
[lotr-issues.md](../../features/lotr-issues.md).

## HOW it works — registration + selection/scoring

### Daily spawn loop (settlement saturation)

`IssuesCampaignBehavior.OnSettlementDailyTick` (`IssuesCampaignBehavior.cs:59`) drives per-settlement spawning. It
counts notables already holding an issue (`:62-68`), then sets the floor/ceiling — **towns** min 1 / max 3, **villages**
min 1 / max 2 (`:69-70`, constants `:32-38`). A new issue is created only when below max AND (below min OR a roll
passes); `GetIssueGenerationChance = 0.3f * (1 - cur/max)²` (`:171-175`). Clans get an analogous path in `DailyTickClan`
(`:108`), gated at 10–20% of eligible lords (`:128-129`). New-game seeding (`OnNewGameCreatedPartialFollowUpEnd`,
`:86`) fills ~70% of villages, ~80% of towns, ~12% of non-bandit lords.

### Registration contract

`CreateAnIssueForSettlementNotables` (`:222`) calls `IssueManager.CheckForIssues(notable)` (`IssueManager.cs:225`). The
manager clears the hero's arg buffer and — only if the hero has no active issue — fires
`CampaignEventDispatcher.Instance.OnCheckForIssue(hero)` (`IssueManager.cs:228-231`). Each issue behavior's listener
responds by constructing `new PotentialIssueData(onSelected, typeof(SomeIssue), IssueFrequency)` and calling
`IssueManager.AddPotentialIssueData(hero, pid)` (`IssueManager.cs:215-218`), appending to `_issueArgs[hero]`.

### Selection (frequency-weighted + score)

Back in the behavior (`:231-244`): it sums `GetFrequencyScore` over valid PIDs, scores each via
`CalculateIssueScoreForNotable` → `CalculateIssueScoreInternal` (`:311-357`) — `score = (thisFreq/totalFreq) ×
adjustment`, the adjustment penalizing over-represented issue types (`:343-356`). PIDs with score > 0 and no active
cooldown (`HasIssueCoolDown`, `IssueManager.cs:616`) are cached; the winner is picked by `MBRandom.ChooseWeighted`
(`:253`), then `CreateNewIssue` (`IssueManager.cs:151`). The clan path uses argmax (`:281-285`).

**`GetFrequencyScore` lives in `IssuesCampaignBehavior` (`:359-375`), NOT in `IssueModel`** — VeryCommon = 6, Common =
3, Rare = 1. There is **no per-issue-type frequency knob anywhere in `IssueModel`** — frequency is hard-coded in each
behavior.

### The `Hero.CanHaveCampaignIssues()` gate — and its non-issue side effects

`Hero.cs:2026-2035` returns false if `Issue != null`, else dispatches `CanHaveCampaignIssues` (manager impl
`IssueManager.cs:634`, which also blocks heroes locked into another issue's roles). **Beyond gating issues, the same
predicate governs notable disappearance/retirement:** `NotablesCampaignBehavior.CheckAndMakeNotableDisappear`
(`NotablesCampaignBehavior.cs:278-289`) requires `notable.CanHaveCampaignIssues()` before a low-power notable can be
removed (`:280`). **Consequence for mods: suppressing issues by flipping this gate also suppresses natural notable
despawn.** (This is why the recommended TAOM suppression is `RemoveBehaviors<T>`, not a `CanHaveCampaignIssuesEvent`
veto — see [lotr-issues.md](../../features/lotr-issues.md).)

### Cooldowns

Stored in `IssueManager._issuesCoolDownData` keyed by issue-type name (`IssueManager.cs:28-29`, `:606-614`), queried by
`HasIssueCoolDown` (`:616-632`), pruned by `ExpireInvalidData` (`:351-368`), written on completion via
`AddIssueCoolDownData` with `IssueOwnerCoolDownInDays` from `IssueModel` (`IssuesCampaignBehavior.cs:423-426`).

## IssueModel surface + modding seams

`IssueModel : MBGameModel<IssueModel>` (`IssueModel.cs:7`) is a GameModel overridden by `DefaultIssueModel`
(`DefaultIssueModel.cs:11`). Every member is `abstract`, so a TAOM override (registered via `AddModel<IssueModel>`)
replaces vanilla wholesale.

| Member | Tunes | Vanilla default |
|---|---|---|
| `IssueOwnerCoolDownInDays` (`IssueModel.cs:9`) | Days before a hero hosts another issue | `30` (`DefaultIssueModel.cs:21`) |
| `GetIssueDifficultyMultiplier()` (`:11`) | Global difficulty scalar | `Clamp(PlayerProgress, 0.1, 1)` (`:23-26`) |
| `GetIssueEffectsOfSettlement` / `…OfHero` / `…OfClan` (`:13-17`) | Active-issue effects (prosperity/loyalty/…) | sum over hosts (`:28-77`) |
| `GetCausalityForHero` (`:19`) | Alt-solution casualty range | skill-ratio × needed-men (`:79-91`) |
| `GetFailureRiskForHero` (`:21`) | Alt-solution failure probability | `(skill_req − skill)·0.5/100`, clamp `[0,0.9]` (`:93-97`) |
| `GetDurationOfResolutionForHero` (`:23`) | How long the away party is gone | `base + 2·clamp(skill_req/skill,0,10)` (`:99-109`) |
| `GetTroopsRequiredForHero` (`:25`) | Men the player must send | `base × clamp(skill_req/skill,0.2,1.2)` (`:111-122`) |
| `CanTroopsReturnFromAlternativeSolution()` (`:27`) | Whether away troops can rejoin | prisoner/at-sea/MapEvent gated (`:138-145`) |
| `GetIssueAlternativeSolutionSkill` (`:29`) | Which skill the alt-solution checks | delegates to `issue.GetAlternativeSolutionSkill` (`:124-127`) |

### CampaignEvents hooks (subscribe in a behavior's `RegisterEvents`)

- **`OnCheckForIssueEvent`** — `IMbEvent<Hero>` (`CampaignEvents.cs:943`). Fired when the engine polls a hero; the hook
  to inject custom issues for that hero.
- **`OnNewIssueCreatedEvent`** — `IMbEvent<IssueBase>` (`:989`). React after any issue is created.
- **`OnIssueUpdatedEvent`** — `IMbEvent<IssueBase, IssueUpdateDetails, Hero>` (`:945`). React to state changes (started
  / solved / …).
- **`CanHaveCampaignIssuesEvent`** — `ReferenceIMBEvent<Hero, bool>` (`:1137`). The `ref bool` veto gate (TAOM uses it
  for castle notables) — but see the despawn side effect above.

### Un-registering vanilla behaviors

`CampaignGameStarter.RemoveBehaviors<T>()` is **public** (`CampaignGameStarter.cs:43`); `RemoveBehavior<T>(T)` removes
one instance (`:54`). This is the lever to drop a vanilla issue behavior so it never subscribes `OnCheckForIssue`. A
later-loading module whose `OnGameStart` runs after Sandbox's registration (the 36 via `SandBoxManager.Initialize`, the
7 via `SandBoxSubModule.InitializeGameStarter`) can call e.g. `gameStarter.RemoveBehaviors<FamilyFeudIssueBehavior>()`.
**Confirm the ordering at implementation** against the [submodule lifecycle](./submodule-lifecycle-and-harmony.md):
`InitializeGameStarter` fires for all modules before `OnGameStart`, and TAOM loads after Sandbox, so by TAOM's
`OnGameStart` ([SubModule.cs:294-307](../../../Main/SubModule.cs#L294)) the issue behaviors are present and removable.

## Lifecycle + save

### Issue → quest pipeline

The player accepting the classic ("quest") solution calls `IssueBase.StartIssueWithQuest()` (`IssueBase.cs:584-593`):
state → `SolvingWithQuestSolution`, then `GenerateIssueQuest(StringId + "_quest")` (`:588`, `protected abstract`
`:525`). The returned quest is stored in `[SaveableProperty(15)] IssueQuest` (`:243-244`), establishing the issue↔quest
link the load path checks. The quest goes live via `QuestBase.StartQuest()` (`QuestBase.cs:152-163`): sets
`_questState = Ongoing`, runs `OnStartQuest()` + `RegisterEvents()`, fires `OnQuestStarted` →
`QuestManager.OnQuestStarted` (`QuestManager.cs:85-88`) adds it to `_quests`.

### Completion paths

Each runs its `On…` hook, then private `FinalizeQuest()` (`QuestBase.cs:235-251`; state → `Finalized`,
`QuestManager.OnQuestFinalized` removes it), then dispatches `OnQuestCompleted` with a detail enum (`QuestBase.cs:22-30`):
`CompleteQuestWithSuccess()` (`:165`), `…WithFail()` (`:199`), `…WithTimeOut()` (`:173`, can divert to success via
`OnBeforeTimedOut`), `…WithBetrayal()` (`:211`), `…WithCancel()` (`:223`).

### The OnGameLoaded auto-cancel rule (critical for custom quests)

`QuestManager.OnGameLoaded` (`QuestManager.cs:129-177`) walks every non-finalized quest and scans `IssueManager.Issues`
for one whose `IssueQuest == questBase` (`:138-145`). The guard is **`if (flag || questBase.IsSpecialQuest)`** (`:146`)
→ re-initialize; otherwise the quest is added to a cancel list (`:160`, with a `Debug.FailedAssert` "There is not active
issue for quest…") and `CompleteQuestWithCancel()`'d (`:165-168`).

**An issue-less quest is silently cancelled on load unless it overrides `SpecialQuestType` to a non-empty string.**
TAOM's `CareerQuest` has no issue, so it sets `SpecialQuestType => "taom_career_quest"`
([CareerQuest.cs](../../../Main/Features/CareerSystem/Quests/CareerQuest.cs)) to survive save/load. Any future
TAOM standalone quest (e.g. a LOTR main-quest line) needs the same. (See memory
`feedback_questbase_subclass_special_or_issue`.)

### SaveableTypeDefiner id math

A custom `QuestBase`/`IssueBase` subclass plus its `[SaveableField]` progress members must be registered or the save
fails. The engine global type id is `_saveBaseId + localId`. **TAOM definer bases step by 100, so each `localId` starts
at 101, not 1.** `CareerQuestSaveableTypeDefiner` uses base `726900701` + localId `101` → `726900802`
([CareerQuestSaveableTypeDefiner.cs](../../../Main/Features/CareerSystem/Quests/CareerQuestSaveableTypeDefiner.cs)).
Using `1` produced `726900702`, colliding with FormationPreset → "An item with the same key has already been added" at
`Module.Initialize`. (See memory `feedback_saveable_typedefiner_localid_offset`.)

## Flow diagram

```
IssuesCampaignBehavior.OnSettlementDailyTick (saturation check, towns 1-3 / villages 1-2)
  → IssueManager.CheckForIssues(notable)
  → CampaignEventDispatcher.OnCheckForIssue(hero)
  → each *IssueBehavior listener: AddPotentialIssueData(hero, new PotentialIssueData(onSelected, typeof(Issue), Frequency))
  → score (freq-weighted + over-representation penalty, cooldown filter) → MBRandom.ChooseWeighted → CreateNewIssue
  → IssueBase (Ongoing)
       ├─ player accepts quest → StartIssueWithQuest → GenerateIssueQuest → QuestBase.StartQuest → QuestManager._quests
       │      → CompleteQuestWith{Success|Fail|TimeOut|Betrayal|Cancel} → FinalizeQuest → OnQuestCompleted
       ├─ player sends troops  → StartIssueWithAlternativeSolution (companion + men, IssueModel risk/duration/casualty)
       └─ player spends influence → StartIssueWithLordSolution
  on save/load: QuestManager.OnGameLoaded cancels any quest with no parent issue UNLESS IsSpecialQuest
```

## TAOM integration notes / gotchas

1. **Only the procedural issue system runs in TAOM** (sandbox-only). The StoryMode storyline is moot.
2. **Issues are culture-relative** — they derive troops/items from `IssueOwner.Culture`, which is already a LOTR culture
   in TAOM, so they pull LOTR content automatically. The immersion gap is hard-coded display *text*, not gameplay refs.
3. **Suppress via `RemoveBehaviors<T>`, not the `CanHaveCampaignIssues` veto** — the veto also kills notable despawn.
4. **A standalone (issue-less) quest must set `SpecialQuestType`** or `QuestManager.OnGameLoaded` cancels it.
5. **SaveableTypeDefiner localId ≥ 101**, base stepped by 100, no collision with existing bases (`726900501` /
   `726900601` / `726900701`).
6. **No per-issue-type frequency knob in `IssueModel`** — to change which issues spawn, add/remove behaviors, don't
   subclass the model.

---

*Engine reference for Bannerlord v1.4.5. Citations are decompiled `TaleWorlds.*` source under `E:\Decompiled_Bannerlord\`
(browse-only; verify signatures with `pwsh tools/taom-src.ps1 path <Type>`). See also:
[campaignevents-and-campaignbehavior](./campaignevents-and-campaignbehavior.md),
[submodule lifecycle](./submodule-lifecycle-and-harmony.md), [save-system](./save-system.md), and the TAOM
implementation [lotr-issues.md](../../features/lotr-issues.md) (shipped 2026-06-20).*

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/lotr-issues.md](../../features/lotr-issues.md)
- [docs/INDEX.md](../../INDEX.md)

<!-- backlinks-end -->
