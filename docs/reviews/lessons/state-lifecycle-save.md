# Lessons — State, Lifecycle & Save

> Category file of the master lessons record — index + house shape: [LESSONS-LEARNED.md](../LESSONS-LEARNED.md). **Append new State, Lifecycle & Save lessons HERE** (`### rule` → `**Why missed:**` → `**Prevent:**` → `**Source:**`).

### A process-singleton per-campaign cache must clear on `OnSessionLaunchedEvent` — "no SyncData / ephemeral" ≠ "reset between campaigns"
A DryIoc `Reuse.Singleton` service is created once in `OnSubModuleLoad` and lives for the whole game process — it is shared by EVERY campaign in that session. "No SyncData — ephemeral, rebuilds as it goes" correctly means *not written to the save*, but it does NOT mean *reset between campaigns*. CaravanTrade's `CaravanVisitMemory` (a singleton keyed by `MobileParty.StringId`) was never cleared on new-game/load, so a stale ring from campaign A survived an in-process switch and — because `MobileParty.StringId` is reused across campaigns — mis-penalized a fresh caravan's first hops in campaign B (self-heals within 4 town visits; Codex MED, #335).
- **Why missed:** the design reasoned about the *within-campaign* rebuild and the *save-load one-hop* cost, but never enumerated the *new/loaded campaign in the SAME process* state. Two deep-review agents (Completeness, Data Flow) both read "no SyncData — ephemeral" and accepted it; the data-flow "Lifecycle State Matrix" check lists entity states (alive/killed/removed/session-end) for entity mutations but doesn't name "process-singleton survives a campaign switch" as a state to enumerate for a shared cache.
- **Prevent:** any behavior fronting a process-singleton runtime cache subscribes `CampaignEvents.OnSessionLaunchedEvent` (fires on both new game and load) → `ClearAll()`, so no state leaks from campaign A into campaign B. Whenever a cache is a `Reuse.Singleton` keyed by a campaign-reused id (`*.StringId`), enumerate the cross-campaign-same-process state, not only the within-campaign and save-load states. Distinct from the `OnGameLoaded` entity-mutation matrix — this is about a shared cache outliving the campaign, not about mutating a loaded entity. **Carve-out — cross-campaign hand-off is a legitimate use:** the rule targets caches keyed on a campaign-reused `StringId`, not every singleton. `PlayerPossession` registers both its services `Reuse.Singleton` precisely so the character-creation choices survive into the campaign that REPLACES the one that recorded them (a multiplayer join discards the CC hero), and its `ResetForNewCampaign` deliberately clears the baseline hero id while keeping `_choices`. Before applying the clear-on-session-launch reflex, ask whether the state is a per-campaign cache or a payload the next campaign is meant to consume.
- **Source:** docs/reviews/rca-caravan-trade-recency-2026-07-11.md (#335; Codex adversarial pass).

### An engine bump can regress a feature whose managed bindings are unchanged — behavior-only changes in an engine method the feature drives unusually
Binding-test-green (`ilspycmd` confirms the method still exists with the same signature) does NOT mean the method's *body* is unchanged. 1.4.7 added an **unconditional** `Mission.InitialPlayerAgent` deref inside `DeploymentMissionController.SetupTeams()`/`FinishDeployment()` — invisible to binding tests, invisible to the category-tree decompile ("unaffected"), and harmless to every *normal* battle (which always has a player-controlled agent → non-null). It only NREs TAOM's **headless** shader-precompile battle, the one place that opens a battle with no human. The bump had dispositioned shader-precompile "unaffected" on the strength of the passing binding tests; the regression was invisible until the feature ran in-game.
- **Why missed:** the engine-bump verification gates (binding tests, managed-diff, parity audits) all check *surface*, not *behavior*, and the affected path is a headless one no offline gate exercises. The line-level regression is only visible in a diff of `SetupTeams` against the preserved 1.4.6 baseline — which nobody diffs unless a symptom points at it.
- **Prevent:** an engine bump's "binding tests pass" covers signature drift, not behavior drift. **In-game-exercise every headless / unusual-code-path feature after a bump** (shader precompile, any `MBGameManager.StartNewGame` headless battle, editor-mode tools) — not just the campaign/battle happy paths already on the `/engine-bump` control-battle list. When a symptom does point at an engine method, line-diff its body against the preserved prior-version baseline (`E:\Decompiled_Bannerlord\_categories_v<prev>\`), don't just confirm the signature.
- **Source:** docs/reviews/rca-shader-precompile-1.4.7-2026-07-11.md (#336; 1.4.7 `DeploymentMissionController` NRE, user-caught in-game)

### Diagnostics latches: state transitions unconditional, closers enumerated per opener path
A latch/window flag in a toggleable diagnostics feature (e.g. `BattleLoadDiagnostics._exitWindowActive`) has two failure classes the happy path hides. (1) **Toggle-gated transitions:** copying the sibling methods' `if (!IsEnabled) return;` early-out onto the method that CLOSES the latch means a mid-window MCM toggle-off latches it forever; re-enabling later emits spurious stamps with huge `t=+` values — misleading forensics from a forensics feature. (2) **Closer-path coverage:** the window opened on ANY `Mission.EndMission` but every closer was campaign-only (`FirstMapTick` needs `MapState`; `ResetLifecycle`'s sole caller was the campaign-only `PlayerEncounter_Start_Patch`), so custom-battle exits and chained missions leaked it — while the feature doc asserted the opposite, a safety claim written from intention rather than a caller trace. (Tournament-exit-hang deep review, 2026-07-06 — both caught by the Data Flow agent, invisible to the four per-file agents.)
- **Why missed:** the `IsEnabled` gate was copied wholesale from sibling logging methods without classifying which lines are I/O (gate) vs state transitions (never gate); the closer set was designed from the one path under investigation (campaign tournament exit).
- **Prevent:** in toggleable features, toggles gate I/O only — state-machine transitions run unconditionally, **verified at the OUTERMOST gate**: after making a method's state transition unconditional, grep every CALLER for `IsEnabled`-style guards that re-condition it (Codex caught exactly this one review later — the service closes were fixed but two hooks still gated the calls; the regression tests exercised the service directly and were structurally blind to hook-level early-outs). For every latch: enumerate every path that OPENS it and verify a closer exists on each (or gate the opener to the paths the closers cover, e.g. `Campaign.Current != null`). Never write a doc safety claim naming a mechanism ("closes on X") without grepping X's actual callers. Companion to `.claude/rules/harmony-patches.md` "Static State Machines" (that rule covers sentinel collisions; this covers closer coverage + toggle gating).
- **Source:** docs/reviews/rca-tournament-exit-hang-2026-07-06.md, issue #331.

### A flag that gates a custom `"start"`-token dialog line must be cleared on `ConversationEnded`, not only in its own consequence
When a CampaignBehavior opens a conversation from a menu (`CampaignMapConversation.OpenConversation`) and gates a custom greeting line at the `"start"` token on a one-shot flag (set in the menu consequence, cleared in the greeting's consequence), the flag **leaks** whenever the greeting line does NOT win the `"start"` token — because a higher-priority vanilla `"start"` line (e.g. `issue_counter_offer` at `int.MaxValue`, gated on the *partner* notable having an active issue) won the conversation instead. The greeting's consequence never fires, the flag stays set, and the emissary greeting then leaks into the next *normal* chat with that same notable. Clearing only in the consequence assumes your line always wins; it doesn't. (EliteEmissary `_pendingEmissaryHeroId`, deep-review 2026-06-25.)
- **Why missed:** the original design reasoned "our greeting fires → consequence clears the flag" without enumerating the path where a higher-priority engine line preempts our greeting. The two adversarial verifiers split (one MED-real, one NOT_A_BUG-because-rare) — the trigger is rare + cosmetic + self-healing, but real.
- **Prevent:** any one-shot flag that gates a `"start"`-rooted dialog line must be cleared on `CampaignEvents.ConversationEnded` (fires after EVERY conversation regardless of which line won), in addition to the line's own consequence. The consequence is the fast path; `ConversationEnded` is the guarantee. More generally: a flag cleared "when my code runs" leaks whenever a higher-priority external handler runs instead — clear it on the lifecycle-end event, not the success path.
- **Source:** docs/features/elite-emissary.md (Design Decisions) + CHANGELOG 2026-06-25 EliteEmissary.

### Read the player's CULTURE/KINGDOM only AFTER `OnCharacterCreationIsOver` — `OnNewGameCreated` / `OnSessionLaunched` run on the CC PLACEHOLDER culture
At `OnNewGameCreatedEvent` and `OnSessionLaunchedEvent` (new game), `Hero.MainHero.Culture` is still the character-creation DEFAULT/placeholder culture, not the player's chosen one — TAOM's culture-setting (Patch29 `SetSelectedCulture` / `FactionMap.CultureSettingService`) applies the chosen culture DURING the culture stage, which only completes at `OnCharacterCreationIsOverEvent`. SpecialResources seeded the starting resource at `OnNewGameCreated`+`OnSessionLaunched`, so a Gondor pick was seeded the PLACEHOLDER culture's resource (`battania` → Tribal Relics=20) and the chosen culture's resource (Castar) stayed 0. Found 2026-06-25 by an in-game test of EliteEmissary — the emissary correctly displayed `balance=0`; the bug was upstream.
- **Why missed:** the seed was wired to the obvious "new game / session start" hooks; nobody verified `Hero.MainHero.Culture` was the FINAL culture at that instant. The log proved it: `[SpecRes] InitializeHero: main_hero → Tribal Relics = 20` at new-game, then `Resolved resource 'caster' via culture 'gondor'` ~30s later.
- **Prevent:** any feature that seeds / snapshots / branches on the player's CULTURE or KINGDOM at game start must do so at `OnCharacterCreationIsOverEvent` (new game) + `OnGameLoadedEvent` (existing/legacy save), NEVER at `OnNewGameCreatedEvent` / `OnSessionLaunchedEvent` — those fire while CC is still mutating the player's culture. `OnGameLoaded` fires only on load (not new game), so the pair covers both entry points exactly once, each after culture-finalize. Contains-gate the load path so it never re-seeds an earned/spent balance.
- **Source:** CHANGELOG 2026-06-25 SpecialResources seeding fix + `docs/features/special-resources.md`.

### Audit what vanilla auto-UNDOES the engine state you create
When a TAOM feature *creates* persistent engine state — an alliance, a `StanceLink`, a `Settlement.Culture` override, a relation, a kingdom decision — don't stop at the creation path. Audit the full lifecycle: enumerate every vanilla system that can auto-UNDO that state, and confirm whether an existing TAOM guard actually covers the NEW instances. A new mechanism inherits the durability of the **least-protected existing category**, NOT the one you mentally file it under — a player-formed alliance defaults to `Neutral` tier, so TAOM's `AllianceCampaignBehavior_EndAlliance_Patch` (which hardens only `Permanent`-tier) leaves it unprotected and vanilla `AllianceCampaignBehavior.OnWarDeclared` (`AllianceCampaignBehavior.cs:678-681`) silently dissolves it via `EndAlliance` the instant war is declared.
- **Why missed:** Neither `/deep-review` (5 agents) nor Codex caught it — both scoped to the *forming* path and never traced "alliance forms; now what ends it?" A change-scoped review can't reach the fix's path (`IsWarAllowed`/`OnWarDeclared`) because it wasn't in the feature's original diff.
- **Prevent:** After writing a state-creating feature, ask three questions before "done": (1) What vanilla events/ticks/decisions can remove or invalidate this state? — grep vanilla for `End*`/`Remove*`/`Reset*`/`Clear*` on the type plus its `OnXxx` handlers. (2) Does any TAOM guard already protect a SIBLING category, and is my new instance inside or outside that guard's scope (guards are usually scoped to one lore tier / culture / flag)? (3) Block the trigger or the sink? — block the trigger when the player still needs the sink.
- **Source:** memory/feedback_new_engine_state_audit_what_undoes_it.md + docs/reviews/rca-player-alliance-freedom-2026-06-16.md

### When you block a state transition to PROTECT state, confirm a deliberate exit survives
Blocking a transition to keep state alive can turn protection into a cage. The first-pass alliance fix ("block the involuntary war via `IsWarAllowed` → false for player+ally") was reverted because `/review-codex` proved it soft-locks the player: v1.4.6 has **no "break alliance" UI**, so the player's ONLY exit from an alliance is to declare war on the ally (which triggers `OnWarDeclared → EndAlliance`). Blocking that war removed the only exit → the player is trapped for ~100 years. Also: don't ship a behavioral fix for an UNCONFIRMED root cause — the war-block targeted a hypothesis (form-then-broken vs never-persists) that in-game diagnostics hadn't confirmed; outcome was reverted to diagnostics-only.
- **Why missed:** The deep-review data-flow agent *asserted* "the player can break the alliance via the vanilla Break Alliance UI" — false (`KingdomDiplomacyVM` had no break-alliance action); relaying that unverified is the evidence-over-claims §A.4 trap.
- **Prevent:** When you block a state transition to protect state, enumerate every exit the entity had and confirm at least one DELIBERATE exit survives — decompile the UI/VM to see what actions actually exist, don't assume one does. Diagnose the confirmed cause first (Iron Law), then fix.
- **Source:** memory/feedback_new_engine_state_audit_what_undoes_it.md + docs/reviews/rca-player-alliance-freedom-2026-06-16.md

### Decompile vanilla setters before deserialize-then-mutate
When a feature deserializes a vanilla cache/dict structure and then re-mutates it via vanilla "Set" APIs, decompile the setter body to confirm Add-only vs Set-or-replace semantics — vanilla `Set...` method names often hide `Dictionary.Add` (throws `ArgumentException` on duplicate key). EditorCacheRebuild's incremental rebuild deserialized the full prior `_settlementToSettlementDistanceWithLandRatio` dict, then Phase 1 called vanilla `SetSettlementToSettlementDistanceWithLandRatio` — which ends in `value.Add(key, ...)` — so every recomputed pair touching a changed settlement threw. Same pattern hit Phase 0's `SetClosestSettlementToFaceIndex` → `_closestSettlementsToFaceIndices.Add(faceId, settlement)`.
- **Why missed:** Codex review #38 (2026-05-12) caught two P1 bugs from this exact gap — the "Set" prefix in the method name was misleading.
- **Prevent:** Before shipping a feature that (1) calls `DeserializeCache(...)` to restore prior cache state AND (2) then calls any vanilla `Set/Add/Register` API onto that state, decompile EVERY write API used. If any ends in `Dictionary.Add`/`List.Add` without a prior `Remove`, you have a dup-key crash. Fix with a pre-clean `Remove` step (added `INavigationCacheAdapter.RemoveDistanceEntriesFor`) OR skip the deserialize and do a fresh full rebuild. Also: if your feature pre-populates a subcache that deserialize then replaces, either run that pre-pop ONLY when not deserializing or make it idempotent.
- **Source:** memory/feedback_decompile_vanilla_setter_before_deserialize_mutate.md

### Standalone QuestBase subclass needs a non-empty SpecialQuestType or it's auto-cancelled on save-load
A custom `TaleWorlds.CampaignSystem.QuestBase` subclass NOT created by an `IssueBase` (e.g. a career/story quest you `StartQuest()` directly) is silently `CompleteQuestWithCancel`'d by `QuestManager.OnGameLoaded` on the first save-load unless `IsSpecialQuest` is true. `IsSpecialQuest => !string.IsNullOrEmpty(SpecialQuestType)` and `SpecialQuestType` defaults to `string.Empty`, so you MUST override it: `public override string SpecialQuestType => "taom_my_quest";` (any non-empty string). Without it, `QuestManager.OnGameLoaded` finds no owning `IssueBase`, `IsSpecialQuest` is false, the quest is added to a cancel list, and `InitializeQuestOnLoadWithQuestManager()` (which runs `RegisterEvents()` + `InitializeQuestOnGameLoad()`) is never reached. The quest works perfectly until the player saves and reloads, then vanishes.
- **Why missed:** TAOM career-quest system, deep-review API-compat agent, 2026-06-01. The 4-cluster 1.4.5 verification pass confirmed every `QuestBase` member *signature* compiled — but "the API exists" ≠ "the engine keeps my object alive across save-load." The cancellation contract lives in `QuestManager.OnGameLoaded`, invisible from `QuestBase`'s own signatures; the bug compiled clean, passed all unit tests (entry point, not unit-tested), and surfaces only in-game after a save-load.
- **Prevent:** Add `public override string SpecialQuestType => "<unique>";` to every standalone `QuestBase` subclass. Generalise — engine-managed-subclass lifecycle check: when you subclass a TaleWorlds type owned/driven by a *manager* (`QuestBase`←`QuestManager`, `IssueBase`←`IssueManager`, `MissionBehavior`←`Mission`, `CampaignBehaviorBase`←`CampaignEventDispatcher`), decompile the manager's `OnGameLoaded`/`OnSessionLaunched`/cleanup path and verify how it treats YOUR subclass — not just the base type's member signatures. Add "trace the manager's load/cleanup path" to the engine-API verification checklist.
- **Source:** memory/feedback_questbase_subclass_special_or_issue.md + docs/reviews/rca-career-quest-system-2026-06-01.md + docs/features/career-quest-system.md

### SaveableTypeDefiner global id is base+localId — start localId at 101, not 1
The engine global type id for a `SaveableTypeDefiner` class is `_saveBaseId + saveId` (decompiled `SaveableTypeDefiner.AddClassDefinition`, verified 1.4.5 — `new TypeDefinition(type, _saveBaseId + saveId, ...)`; same `+ localId` math for `AddStructDefinition`/`AddEnumDefinition`/`AddBasicTypeDefinition`). TAOM's definer bases step by 100 (EquipPresets `726900501`, FormationPreset `726900601`, CareerQuest `726900701`) and register classes at localId **101+** so the computed id lands in the base+100 century block clear of the previous definer. The trap: base-step (100) < the conventional localId (101), so `baseN + 101 == base(N+100) + 1`. `CareerQuestSaveableTypeDefiner` used localId **1** → `726900701 + 1 = 726900702`, which collided with FormationPreset's `726900601 + 101 = 726900702` → `System.ArgumentException: An item with the same key has already been added` in `SaveableTypeDefiner.AddClassDefinition` → `SaveManager.InitializeGlobalDefinitionContext` → `Module.Initialize` hard crash, before any save is loaded.
- **Why missed:** It's a Module-init-time crash, not save-time — no unit test exercises it, and both the deep-review and Codex passes reviewed save *correctness* (field graph, special-quest lifecycle), not the *id arithmetic across definers*. The "next in the 7269007xx series" comment looked sufficient but ignored the base-step/localId interaction.
- **Prevent:** For every new `SaveableTypeDefiner`: (1) pick the next base in the `7269xxx` series stepping by 100 (`...801`, `...901`); (2) start per-class `localId` at **101** (not 1); (3) compute `base + localId` for every class and confirm it's not in the existing key set (current TAOM keys: `726900602`, `726900603` EquipPresets, `726900702` FormationPreset, `726900802` CareerQuest); (4) multiple classes in one definer use `101, 102, 103…` (stay < 200 so you don't bleed into the next base's block).
- **Source:** memory/feedback_saveable_typedefiner_localid_offset.md + docs/reviews/rca-career-quest-system-2026-06-01.md

### Singleton controllers spawned from a per-mission MissionBehavior have asymmetric lifetime
When refactoring a per-mission `MissionBehavior` by extracting state machines into `Reuse.Singleton` controllers, every controller field becomes cross-mission state with no automatic disposal at mission boundary. The entry-point `MissionBehavior` is constructed fresh per mission via `mission.AddMissionBehavior(new XBehavior(...))` (e.g. `SubModule.cs:683`) — all its instance fields are discarded with `this` at mission end (the implicit "fresh per mission" guarantee). Singleton controllers do NOT get that guarantee: their fields persist for the whole process lifetime, cleared only by explicit `Reset()`/`Cleanup()`/`ClearAll()`. If `OnEndMission` runs cleanup ops in straight-line order and one throws (e.g. `_hudController.Cleanup` → vanilla `ScreenBase.RemoveLayer` calls `HandleFinalize` unconditionally, which can throw on Gauntlet teardown), the remaining ops silently skip — the next mission starts with stale singleton state (most visibly a `_hudInitialized=true` Singleton whose `TryInitialize` early-returns, killing the HUD for the rest of the session).
- **Why missed:** Deep-review fan-out on issue #102 surfaced this as a MED-severity systemic finding ("Systemic singleton-controller-per-mission-behavior lifetime asymmetry — root cause of HIGH#7 not named in findings"); verdict confirmed, applied 2026-06-02. Rejected alternative: downgrading controllers to `Reuse.Transient` — rejected because existing CareerSystem services are also Singleton with an established `ClearAll()` pattern, and Transient would break any future cross-mission state (e.g. an achievement counter).
- **Prevent:** When you spawn `Reuse.Singleton` controllers from a per-mission entry point: (1) wrap each cleanup op in its own try/catch in `OnEndMission` (LogWarning with op name + exception) so a throw in one cannot abort the others — this per-step try/catch IS the compensating control, auditable in one place; (2) inside controllers owning engine resources (GauntletLayer, ScreenBase), wrap engine-touching teardown in `try {...} catch {...} finally { /* field-reset */ }` so a throw can't leave the flag stuck; (3) capture screen ownership at attach time (`_attachedScreen`) — `ScreenManager.TopScreen` at mission end may not be the screen you attached to. Audit Messengers, QuickActions, SmartCavalryAI for the same shape if symptoms surface.
- **Source:** memory/feedback_singleton_controller_per_mission_behavior_lifetime_asymmetry.md (issue #102, applies to `CareerPerkMissionBehavior` → `IAbilityActivationController`/`IAbilityHudController`/`IAbilityEffectExecutor`)

### Enumerate every clear path in a state matrix before writing the set path
For any system that sets timed/stateful data (buffs, tracked dictionaries, scheduled callbacks), enumerate ALL clear paths in a state matrix BEFORE writing the set code. Phase IV had 3 lifecycle bugs — hero buff expiry unconditionally cleared reactivated buffs, hero death orphaned ally buff restore callbacks, and ally buff restore didn't trigger stat refresh — all "set" paths written without tracing "clear" paths.
- **Prevent:** For every `SetBuff`/`ScheduleRestore`/dictionary write, verify these clear paths exist and work: (1) normal expiry (timer fires) — guards against replacement? refreshes stats? (2) reactivation (new buff overwrites old) — does the old timer's restore guard against clearing the new buff? (3) entity death (hero/agent dies) — are all tracked entries for that entity cleaned up? (4) mission end — are all static dictionaries and pending callbacks cleared? (5) screen/state change — are UI-bound references released? Each "set" line needs a traceable "clear" for every exit path; if the clear is delegated to a scheduled callback, verify the callback survives context clearing (e.g. `_activeContexts.Clear()` dropping pending restores).
- **Source:** memory/feedback_lifecycle_state_matrix.md

### CampaignBehaviorBase has no OnGameEnd — use OnGameOverEvent / OnNewGameCreatedEvent for singleton cleanup
`CampaignBehaviorBase` in v1.3.15 (verified via `ilspycmd ... -t TaleWorlds.CampaignSystem.CampaignBehaviorBase`) exposes only `RegisterEvents()` + `SyncData(IDataStore)` — there is no `OnFinalize` and no `OnGameEnd` virtual. An audit on `SpecialResources` (#133) recommended overriding `OnGameEnd()` to unsubscribe a static event; that method doesn't exist.
- **Why missed:** Codex audit (Phase 9b #133, 2026-05-13) recommended overriding `OnGameEnd()`; verified via `ilspycmd` that no such virtual exists. An inline comment in `SpecialResourcesBehavior.RegisterEvents` documents the constraint.
- **Prevent:** For singleton cleanup at campaign teardown: (1) best-effort hook `CampaignEvents.OnGameOverEvent.AddNonSerializedListener(this, UnsubscribeMethod)` — covers the death-of-character flow but NOT main-menu-exit (the orphan listener becomes GC-eligible once `CampaignGameStarter` releases). (2) For static event subscriptions on long-lived objects (e.g. `ScreenManager.OnPushScreen += handler`): same pattern, document the limitation inline. (3) For `Reuse.Singleton` service teardown needing campaign-2-in-same-process safety: use `OnNewGameCreatedEvent` on the ENTERING campaign (not OnGameOver on the exiting one) so reset happens just before fresh state is built — pattern shipped in #124 (BannerInjection), #128 (CareerSystem), #130 (HeroRace), #131 (RaceAge), #132 (Siege).
- **Source:** memory/feedback_campaignbehavior_no_ongameend.md (Phase 9b #133 SpecialResources ScreenManager event-leak fix)

### Pick a lifecycle hook by locating the engine call it must precede — never by copying a neighbour

Startup work with an ordering requirement ("this must run before the engine does X") gets its hook by
decompiling the engine and finding where X happens, not by matching whatever hook an adjacent guard
uses. Also establish whether the chosen hook is a one-shot: `OnBeforeInitialModuleScreenSetAsRoot`
fires on **every** return to the main menu (`Module.OnApplicationTick` → `SetInitialModuleScreenAsRootScreen`,
installed v1.4.7 line 509 → 758), which is why `_basicTableauGuardApplied` exists; `OnSubModuleLoad`
runs once per process.
- **Why missed:** the save-definer collision preflight was wired into `OnBeforeInitialModuleScreenSetAsRoot`
  by analogy with the `Patch55_BasicTableauRaceGuard` block 18 lines above it. But `Module.Initialize`
  calls `SaveManager.InitializeGlobalDefinitionContext()` at line 285 — right after `LoadSubModules`
  (267, which is where every `OnSubModuleLoad` fires) and long before line 758. On the one boot where
  a definer collision existed the engine had already thrown, so the preflight could only ever run on
  boots where it had nothing to report. It was also unguarded, so it re-walked every loaded assembly
  and re-instantiated every `SaveableTypeDefiner` on each quit-to-menu.
- **Prevent:** this is the non-Harmony sibling of the apply-timing rule the `/deep-review` skill
  already carries for `_harmony.PatchCategory` (issue #299, `rca-savetableau-2026-06-24.md`).
  Generalise it: for ANY startup work with an ordering requirement, cite the engine file:line of the
  call you must precede in a comment at the call site, and state whether the hook is re-entrant. Do
  not accept "it's registered early" as evidence it runs in time.
- **Source:** `docs/reviews/rca-coop-interop-2026-07-31.md` findings #1 + #10

### Persisted data is only as trustworthy as the ENVIRONMENT that captured it — validate the SHAPE of the source, not just each value

`RacePersistenceService.CaptureHeroRaces` used to snapshot every hero's FaceGen race index
unconditionally. A co-op host running WITHOUT TAOM's modules has exactly one race ("human") in its
FaceGen table, so on that host every hero reads back as **0** — and the capture wrote
`legend="human"` plus `{every hero: 0}`. That map rode the host→client save transfer, and
`RestoreHeroRaces` on a full 15-race client resolved "human" to a genuinely valid id 0 and force-set
every hero in the world to human. The guard now refuses to capture below
`MinimumTrustworthyRaceCount = 2` (`_raceManager.GetOrderedRaceNames().Count`), keeping the existing
map and legend rather than clearing them, so a good capture already in memory survives the bad host.
- **Why missed:** per-value validation is structurally incapable of catching this. Every entry was
  individually well-formed — a valid hero id mapped to a valid race index, translated through a
  legend naming a race that really exists — and the existing validators (`IsValidRaceId`,
  `IsValidRaceName`, the legend range check) all pass on it. The corruption lives in the
  RELATIONSHIP between the capture environment and the restore environment, which no single value
  encodes. Two is the smallest count that can express "human and something else", so the race COUNT
  is the only tell available.
- **Prevent:** when a snapshot is taken in one process and applied in another (save transfer, save
  file, config export), the capture side needs a plausibility check on the SHAPE of the source it is
  reading — table size, expected id set, module presence — not only per-field validation. Skip the
  capture rather than clearing on failure: an empty state at least degrades to "no saved data" and
  lets entities keep their authored values, whereas a written-but-degenerate state overwrites them.
- **Source:** Multiplayer field report 2026-08-03 (TAOM v2.0.16 co-op testing); commit 7cf5be28

### Two operations on one lifecycle event where one consumes the other's output: pin the ORDER with a test

`RacePersistenceBehavior.OnSessionLaunched()` runs `RestoreHeroRaces()` and then
`CaptureHeroRaces()`. The capture half is there because a host→client save transfer never raises
`OnBeforeSaveEvent`, which was the only capture trigger — so a joining player received a world with
no race data at all. But putting capture FIRST would snapshot the pre-restore state (every hero at
whatever race the raw XML gave them) and write it over the good map the restore is about to apply,
which is a worse bug than the one being fixed and leaves no trace. Two tests pin the ordering:
`OnSessionLaunched_RestoresThenCaptures` (NSubstitute `Received.InOrder`) and
`OnSessionLaunched_DoesNotCaptureBeforeRestoring` (explicit call-order list).
- **Why missed:** nothing about the ordering is visible in either method's own behaviour — each is
  correct in isolation and the wrong order still produces a green suite of per-method tests. The
  existing lifecycle rules in this file cover which EVENT to subscribe and which CLEAR paths exist;
  neither asks about the relative order of two handlers sharing one event.
- **Prevent:** whenever two operations run on a single lifecycle event and one consumes what the
  other produces, state the dependency in a comment at the call site and pin it with an order-asserting
  test. Ordering that only exists as the sequence of two statements is one careless reorder from
  silently inverting, and the failure mode is data loss rather than an exception.
- **Source:** Multiplayer field report 2026-08-03 (TAOM v2.0.16 co-op testing); commit 7cf5be28

---

---

### Never write a `Save-compat:` claim from memory — a bare public field may or may not persist
`[SaveableField]` / `[SaveableProperty]` is not the whole story: Bannerlord also persists members through `AutoGeneratedSaveManager`'s generated accessors. `Hero.Culture` and `Settlement.Culture` are BOTH bare `public CultureObject Culture;` fields with no attribute — and `Hero.Culture` persists (it has an `AutoGeneratedGetMemberValueCulture` entry) while `Settlement.Culture` does not (zero entries; `Settlement.cs:961` re-reads it from XML on every load). Reasoning from the declaration alone gives you a 50/50 answer that reads as certain.
- **Why missed:** #374's Khand retag (2026-08-04) shipped "Save-compat: `Settlement.Culture` is persisted, so existing saves keep Easterling Khand. New campaigns only" into a docstring, the CHANGELOG, the feature doc and an RCA. It is the exact reverse — the retag lands on every existing save. Worse, the correct fact was already in the repo twice (`SettlementConversionRecord.cs`, `ICultureConversionService.cs`): TAOM's whole CultureConversion re-apply-on-load mechanism exists *because* settlement culture is not engine-saved. `evidence-over-claims.md` §C did not fire because a `Save-compat:` line reads as boilerplate rather than as a factual assertion about engine behaviour.
- **Prevent:** before writing any `Save-compat:` trailer, docstring or CHANGELOG line about a field's persistence — (a) grep the repo for an existing statement about that field, since a feature that works around the behaviour is the strongest possible evidence; (b) confirm against BOTH the attribute AND `AutoGeneratedSaveManager`'s member list for that type. Treat a persistence claim as a verifiable API fact, not as a formality.
- **Source:** `docs/reviews/rca-landless-culture-spawn-2026-08-04.md` (deep-review M3, 2026-08-04)

---

### Name the driving event for every state-machine edge — or explicitly accept polling latency

**Why missed:** The Enlistment core (2026-08-04) designed its 8-state machine first-principles but inherited its *drivers* from the donor mod, which was 100% poll-shaped (4 Hz tick). The `EnlistedPlayerCaptive` state existed with legal edges and full reconciler handling — but nothing subscribed `HeroPrisonerTaken`, so the edge only fired on the hourly tick, leaving up to an hour where the wait menu ticked against a party vanilla considered captive. A state without a named driving event looks complete in every per-file review; only the data-flow trace ("which event moves EnlistedBattle→Captive?") exposed it.

**Prevent:** When adding a state or edge to any persisted state machine, write down in the same commit WHICH campaign/mission event drives the transition — or an explicit "polled hourly, latency accepted because X" note. An edge with neither is a finding. Grep `CampaignEvents` for an event before settling for polling; the engine usually has one (`HeroPrisonerTaken/Released` existed all along, already used by CareerQuest and SpecialResources).

**Source:** `docs/reviews/rca-enlistment-core-2026-08-04.md` finding #6 (deep-review data-flow agent, event-coverage trace).

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/LESSONS-LEARNED.md](../LESSONS-LEARNED.md)

<!-- backlinks-end -->
