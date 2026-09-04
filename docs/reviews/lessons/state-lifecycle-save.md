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

### A rate integrated over elapsed time must bound its catch-up: a skip path is a state, not a filter
Any feature that accumulates `rate x (now - lastAppliedTime)` and can be *skipped* (a master toggle, an eligibility gate, a stall, an alt-tab) freezes `lastAppliedTime` while the clock keeps running, and then delivers the whole skipped window as one application when it resumes. DreadAura's morale drain shipped this way: toggling the MCM off and back on mid-battle handed the scheduler a 40-second elapsed, and one pulse took every enemy in radius from full morale to zero, an instant army-wide rout from a settings click. The per-source elapsed integration itself was correct and deliberate (it keeps the drain rate-exact while sources are round-robined across frames); what was missing was an upper bound distinguishing "this source was rescheduled" from "this feature was stopped."
- **Why missed:** the design reasoned about the *reschedule* case, which is the one the elapsed integration exists for, and never enumerated the *skip* case. Both existing tests fed a plausible elapsed (0.25s, 6s) and asserted unbounded pass-through, so they pinned the defect as intended behaviour. Four of five deep-review agents were structurally blind to it: it is not a signature question, not an allocation question, and not a coverage question. The Data Flow agent reached it from its NaN-polarity check on engine-sourced floats, because `Mission.CurrentTime` is one.
- **Prevent:** for every `now - lastX` that feeds a magnitude, ask what the largest reachable gap is and clamp to `max(interval, <one sensible ceiling>)`. Enumerate the skip paths explicitly: every `return` above the point where the stamp is written is one. Test the clamp with a gap far larger than any real frame time (DreadAura uses 40s against a 1s ceiling), and keep one test asserting a *sub*-ceiling gap still passes through exactly, or the clamp silently becomes a fixed-step integrator. Companion to the latch rules below: that family is about flags that leak, this one is about time that accumulates.
- **Source:** docs/reviews/rca-dread-aura-2026-08-13.md finding 1 (HIGH, pre-commit deep review).

### "Toggles gate I/O, never state transitions" covers IDENTITY and ELIGIBILITY predicates too, not just latch flags
The existing latch rule (below, from the tournament-exit hang) is written in terms of window flags, so it does not pattern-match when the toggle is folded into a *lookup* whose answer happens to gate a state transition somewhere else. DreadAura's `IDreadRegistry.ResolveSource` folded the master toggle and answered "is this agent a dread source?"; `DreadSourceTracker` used that answer to decide whether to add the agent to its tracked list. Net effect: a wraith spawning while the feature was toggled off was never registered, and re-enabling did not rearm the one-shot mission scan, so it projected nothing for the rest of the battle: silent, no log line, and a unit test pinned it as intended.
- **Why missed:** the rule's text and both its worked examples are about latches (`_windowActive`, `_inflight`). The author folded the toggle into the registry for master-toggle-completeness reasons (the Data Flow agent's own "enumerate every member and confirm each folds `IsEnabled`" check *encourages* this), not realising the fold had moved onto the registration path. Two review checks pulled in opposite directions here.
- **Prevent:** when folding a master toggle into a service member, trace what the CALLER does with the answer. If the caller writes state on the strength of it, the fold belongs downstream of the write, not upstream. Identity ("is this X?") and eligibility ("does this qualify?") should be toggle-independent by construction; the cleanest form is for the class to hold no settings reference at all, so it cannot consult the toggle even by accident. Gate the EFFECT instead. Where a fold is genuinely needed on a state-writing path, pair it with a re-arm on the false-to-true edge.
- **Source:** docs/reviews/rca-dread-aura-2026-08-13.md finding 2 (MED-HIGH); first instance docs/reviews/rca-tournament-exit-hang-2026-07-06.md.

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

---

### Clearing cached agent-stat state is NOT an engine-side refresh — recompute every agent that baked it in, on EVERY clear path

**Why missed:** The #377 buff-lifecycle rework asked "when does the tracker ENTRY die?" and covered every clear path (expiry, death, agent delete, mission end). But the entry's side effect — buff values baked into `AgentDrivenProperties` — lives engine-side, and `Agent.UpdateAgentProperties()` is event-triggered (ammo/weapon/mount changes), never per-tick (decompile-verified). The expiry restores refreshed their agents; the hero-DEATH path cleared the dictionary AND `_activeContexts` (killing those restores) without refreshing anyone, so allies kept the buffed stats for the rest of the mission. A state matrix that only tracks the CACHE misses the CONSUMER.

**Prevent:** for every cached value that flows into `AgentDrivenProperties` (or any engine-side recomputed stat), every clear path must either (a) call `UpdateAgentProperties()` on each affected agent, or (b) provably leave the scheduled refresh alive. Snapshot the affected agent set BEFORE the clear (`GetBuffedAllyIndices()` pattern) — a cleared dictionary can't tell you who to refresh.

**Source:** `docs/reviews/rca-career-ux-arc-2026-08-05.md` finding #1 (deep-review data-flow agent, lifecycle trace).

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/LESSONS-LEARNED.md](../LESSONS-LEARNED.md)

<!-- backlinks-end -->
### Un-persisted state on a `Reuse.Singleton` service outlives the campaign — clear it at the session boundary

TAOM services are registered `Reuse.Singleton`, and `IoC.Configure()` runs once in `OnSubModuleLoad`,
so a service field lives for the whole Bannerlord PROCESS. Any field that is neither persisted by
`SyncData` nor cleared on a session boundary therefore survives quit-to-menu and reappears in the next
campaign loaded without restarting. FieldCommission's pending-offer queue did exactly that: an offer
earned in one save popped in the next, proposing a soldier the player did not have. Its sibling latch
(`IsShowingOffer`), lowered only from inside an inquiry callback, could stay raised for the rest of
the process and silently suppress every later offer.

**Why missed:** `ClearState()` was written against the fields that appear in `SyncData` — the mental
model was "reset what we persist". Transient state is invisible to that framing precisely because it
is not in the save file.

**Prevent:** enumerate EVERY instance field on a singleton service and classify each as save-scoped or
process-scoped. Save-scoped clears on a new campaign only; process-scoped clears on EVERY session
boundary, unconditionally and ahead of any `justLoadedFromSave` guard — gating it on the load flag is
what lets the previous campaign's state through. Naming the split in code (TAOM uses
`FieldCommissionSessionReset.ClearAll` vs `.ClearCarriedOverOffers`) makes the next edit pick a side.

**Source:** `docs/reviews/rca-field-commission-2026-08-07.md` findings 2 and 14.

### A hand-back that reads only the OTHER actor's position will strand the player (Enlistment, 2026-08-08)

`DischargeService.RestoreCampaignContext` decided where to put the player entirely from
`CommanderSnapshot.SettlementId` — and never asked where the **player** was. When the commander had
no settlement (dead, marching, in a hideout) while the player stood inside one, the whole settlement
branch was skipped and the wait menu was then closed. Result: `CurrentSettlement` set, no menu.

That is terminal, and the engine offers no way out:
`MobileParty.DoUpdatePosition` returns early for any party with `CurrentSettlement != null`;
`CheckExitingSettlementParallel` explicitly skips `IsMainParty`; the menu the engine re-pushes for a
fortification is `town_outside`. For a village it pushes nothing at all.

*(Correction, 2026-08-24, #510 Codex round: this entry used to add that `town_outside`'s Leave
option calls `PlayerEncounter.Finish()` and no-ops on a null `Current`. The player never reaches
that option. `game_menu_town_outside_on_init` opens with
`args.MenuTitle = PlayerEncounter.EncounterSettlement.Name`, and `EncounterSettlement` is
`Current?.EncounterSettlementAux`, so the menu NREs at init inside the unguarded
`GameMenu.RunOnInit`. The failure is a CTD, not a stuck menu. The lesson below is unchanged;
only the engine detail was wrong, and it had been copied into three other artifacts before
anyone decompiled the init.)* And it survives save/reload, because the record now reads `NotEnlisted` and
every recovery loop early-returns on that.

**The rule:** a placement/cleanup step must be driven by the state of the actor it is placing, not
by the state of whatever it is placing them relative to. Ask "where is the player *now*" before
"where should they go". Where a teardown can leave an actor in a container, exiting that container
belongs on EVERY path, not inside the success branch of the happy path.

### Coercing a transient state at save time silently breaks its own re-derivation contract (Enlistment, 2026-08-08)

`EnlistmentRecord.ToPersistedState` coerces `EnlistedBattle` to `EnlistedAttached`, on the stated
grounds that "battle reality is re-derived from the engine at load". That re-derivation was never
implemented: `Assess` returns `Attached` when both parties are already in the map event, and nothing
else produces `EnlistedBattle`.

The consequence was worse than a wrong enum. On reload the engine restores
`MapStateData.GameMenuId = "encounter"`, and the menu redirect — gated on `EnlistedAttached`, which
the coercion had just made true — swallowed it. `MapEventManager.Tick` deliberately SKIPS
`MainParty.MapEvent`; the player's own event advances only via `PlayerEncounter.Update`, driven from
that menu. The battle could never resolve, and the wait menu has no `isLeave` option to escape by.

**The rule:** if you coerce a state on save because "it will be re-derived on load", write the
re-derivation in the same change and test the round trip. A coercion whose counterpart does not
exist is a silent data-loss bug that only manifests through a third system.

### A latch check placed above its own reset can never recover (Enlistment, 2026-08-08)

`ServiceMaintenanceService.EnsureServiceMenu` returned early on `_menuFailures >= MaxMenuFailures`
**before** the two lines that reset the counter to zero. Three transient failures therefore disabled
the wait-menu invariant for the rest of the process — on a `Reuse.Singleton`, so across
re-enlistment and across campaigns. The existing test pinned the back-off and stopped there; it
never asserted recovery.

This is `harmony-patches.md` "Latches & Toggle Gates" rule 2 in a new costume: **state transitions
come first, gates second.** When you add a back-off, add the test that proves it un-backs-off.

### Enumerate what runs BEFORE an event dispatch, not only the teardown after it

When reading engine state inside an event handler, verifying "the teardown runs later, so the data
is intact" is a half-audit. Decompile the dispatch site and walk *upward* through every path that
reaches it.

**Why missed:** AutoResolveDiagnostics (2026-08-08) confirmed `MapEventSide.HandleMapEventEnd()`
runs at `MapEvent.cs:2147`, after the `:2068` dispatch, and concluded `Party.MemberRoster` was
intact. It is not: `CaptureDefeatedPartyMembers` (`:2018`) strips captured troops from the defeated
parties and `MapEventSide.Route()` (`:1250`) empties the roster on a rout — both reached from the
`BattleState` setter at `:301`, well before the dispatch. Result: every composition measurement was
taken on winners only, a silent survivorship bias in the one thing the feature existed to measure.

**Prevent:** confirming the absence of one mutation is not confirming the absence of all mutations —
and do not stop at the second one either. The first fix swapped `MemberRoster` for
`MapEventParty.Troops`, which only flips per-descriptor state in its own mutators and looked safe.
It was not: `MapEventSide.MakeReadyParty` calls `MapEventParty.Update()`, which does
`_roster.Clear()` and rebuilds from the already-stripped `MemberRoster`. Measured over 4,380 live
battles, losing sides read a median 55% short and winners 1%.

**The general rule: the only reliable record of a pre-event state is one captured before the event.**
If a measurement must reflect state at time T, snapshot at time T; do not hunt for a field that
survives to time T+1. Sibling of "GameModel Cross-Entity Propagation" in `csharp-architecture.md`:
open the engine code, do not reason from the shape of the API.

**Source:** `docs/reviews/rca-autoresolve-diagnostics-2026-08-08.md` (P1, found independently by
Codex and the data-flow agent).

### Resetting the sibling fields is not the same as resetting the field

`AutoResolveDiagnosticsBehavior`'s session-launch handler cleared `_pending` and `_sequence`, under a
comment that named the exact hazard: "A second campaign in the same process must not inherit the
first one's tracking." The third field on the same class, `_censusWritten`, was not cleared. So the
troop census ran once per **process** rather than once per session, and a player who returned to the
main menu and started a second campaign got a log with no engine ground truth at all — silently, and
the census is what validates every tier and power figure the offline analysis rests on.

The adjacent correct code is what makes this hard to see: a reviewer reading the handler sees a reset
block with a rationale and moves on. Two agents plus an adversarial pass all found it only by
enumerating the class's fields rather than by reading the handler.

Second defect in the same method, same shape: the latch was set **before** the work it guards, so one
exception mid-write foreclosed the census permanently instead of leaving it retryable.

**How to apply:** at a session/campaign boundary, enumerate every instance field on the type and
decide for each whether it is session-scoped — do not review the reset block, review the field list.
Set an idempotence latch *after* the successful pass, never before, unless the retry itself is the
hazard.

**Source:** review wave on #430, 2026-08-08.

### A singleton holding an ENGINE OBJECT REFERENCE needs a load-time reset, not just an id

TAOM's DryIoc container is process-scoped, so a `Reuse.Singleton` outlives the campaign. A cached
*id* that survives a reload is the familiar hazard (a lord-party StringId is identical across a
reload of the same campaign, so a stale handle HITS the cache test). A cached engine **object
reference** is worse in the opposite direction: after a reload it can never match anything again, so
every identity check against it silently fails forever.

- **Why missed:** `ArmyMembershipAdapter._createdArmy` holds a live `Army`. The load hook already
  called `_maintenance.ResetSessionCaches()`, and the presence of one reset made the load path look
 handled; nobody asked which OTHER singletons hold campaign-scoped state. Left stale, the identity
 test in `LeaveArmy` could never match, so the army raised for a battle would never be disbanded,
  and a bare-ctor army carries a null `AiBehaviorObject` that crashes the map UI permanently.
- **Prevent:** at a load/session boundary, enumerate every instance field on every singleton the
 feature registers and classify each as campaign-scoped or process-scoped, review the FIELD LIST,
  not the reset block. Put the reset in the ONE service that owns per-session cache lifetime rather
  than wiring each collaborator separately into the load hook; that keeps the enumeration in one
  place and keeps the thin lifecycle behavior from accreting a dependency per cache.
- **Source:** `docs/reviews/rca-enlistment-field-fixes-2026-08-11.md` finding #8.

### Enumerate the paths that end the SERVICE, not just the ones that end the EPISODE

A feature that acquires a resource for the duration of an episode (a battle, a mission, a quest) gets
its teardown reviewed in the vocabulary the feature is written in, "where does the battle end?" The
paths that end the whole SERVICE are a different question, they usually live in files the changeset
never touched, and they are where the leak is.

- **Why missed:** the transient army merge is acquired in `ServiceBattleService.TryJoin` and released
  in `OnCommanderBattleEnded`. Two review passes enumerated battle exits and one of them added a
  release to the reconciler's stale-battle self-heal. Nobody opened `DischargeService`, which is how
 service ENDS; it calls `ClearArmyAttachment()`, which detaches the player but knows nothing about
 the army, and discharge fires mid-battle whenever the MCM master switch is turned off or
  `CommanderDead` is raised from `EnlistedBattle`. Found by the Codex pass after ten agent-passes
  missed it.
- **Prevent:** for any resource acquired for an episode, list the exits in BOTH vocabularies before
 reviewing the teardown ("the episode ended" and "the feature stopped running") and include the
  MCM master switch, every discharge/cancel reason, captivity, death, and save/load in the second
  list. Then test the second list exhaustively (a `foreach` over the reason enum is cheap).
- **Second half of the same finding: a state-keyed guard is defeated by a state coercion.**
  `EnlistmentRecord.ToPersistedState` coerces `EnlistedBattle` to `EnlistedAttached` on save, so any
  `if (State == EnlistedBattle)` self-heal is structurally blind after a reload. Re-key such guards
  on the OBSERVABLE world (`IsInArmy`, "no map event anywhere") rather than on persisted state whose
  own serializer rewrites it. Grep the record's persist path for coercions before writing any guard
  that keys on a state value.
- **Source:** `docs/reviews/rca-enlistment-field-fixes-2026-08-11.md` finding #12.

### "Is the sequence right?" and "is it still the same actor?" are different review questions

A two-phase operation (acquire now, release later) gets its ORDERING reviewed carefully, because
ordering bugs are the famous ones. The identity of the actor performing each phase is a separate
question, and a passing ordering test makes the whole area feel covered.

- **Why missed:** TAOM's service-war mirror has a dedicated, well-commented test asserting the
 pre-oath enemy set is snapshotted BEFORE any declaration, the ordering safeguard, and it is
  correct. But both the declare and the unwind resolve `Hero.MapFaction` LIVE, and `MapFaction` is
  `Clan.Kingdom ?? Clan`, so a player whose clan joins a kingdom mid-service declares as his clan and
 makes peace as the KINGDOM, ending a war for every vassal in it because one soldier was
  discharged, with nothing on screen connecting the two.
- **Prevent:** for any acquire-then-release pair, ask who the actor is at each end and whether the
  engine can change it in between. Player faction, clan, kingdom, party, army and settlement
  ownership are all live-resolved in Bannerlord and all mutable mid-campaign. Pin the identity in the
 persisted record at acquire time and refuse to release under a different one, refusing is almost
  always safer than acting, because the release is a mutation someone else now owns.
- **Source:** `docs/reviews/rca-enlistment-field-fixes-2026-08-11.md` finding #14.

### A `static bool` diagnostic latch is process-scoped, and "once per session" is almost never what it delivers

`private static bool _faultReported` with a comment saying "once per session" is a process-lifetime
latch. Load campaign A, hit the fault, log once; quit to menu, load campaign B, hit a genuinely new
fault, and it is swallowed with no log at all. Campaign B then runs on the fallback path for its whole
life with nothing to explain why. Key the latch off `Campaign.Current` identity instead, or reset it on
a campaign-boundary event.

Second half, from the same finding: the latch was set BEFORE the log call, so a throwing logger would
have destroyed the one and only report. Latch AFTER the successful pass.

- **Why missed:** fourth instance of once-per-process where once-per-session was meant, and this file
  already contained both halves of the rule when the code was written. Writing a fault reporter feels
  like plumbing rather than state, so the state-lifecycle rules were not consulted.
- **Prevent:** for every `static` field in a Harmony patch or diagnostic helper, state which lifetime
  it is scoped to and how it is reset. If the answer is "it isn't", it is process-scoped, and say so in
  the comment rather than writing "session".
- **Source:** `docs/reviews/rca-fiefgranting-2026-08-14.md` finding #2.

### A patch class that caches a service in a static MUST be called from the unload sweep, and that is now gated
`SubModule.OnSubModuleUnloaded` disposes the IoC container, so any patch holding `_logger ??= IoC.Resolve<IModLogger>()` (or any other cached service) in a static field keeps a reference into a DISPOSED container across a reload-in-process, and silently drops everything it was meant to log. The .NET type stays loaded, so `_harmony.UnpatchAll` does not help: only an explicit `ResetForUnload()` clears it. Codex review #46 found this and fixed it by hand for four classes. Patch71 then shipped the fifth with the same omission (#486), because "add yourself to the sweep" lived only in those four files.
- **Why missed:** the sweep is four consecutive lines near the bottom of a 1,600-line single-owner file. Nothing connected "I added a static service cache" to "there is a list I must join", and no reviewer grepped for the pattern. Standards, compatibility and efficiency review all pass a class that simply omits a call.
- **Prevent:** `ResetForUnloadSweepTests.EveryResetForUnload_IsCalledFromOnSubModuleUnloaded` now scans `Main/` for every `public static void ResetForUnload()` and fails the build unless `OnSubModuleUnloaded` calls it. When you add a static service cache to a patch, add `ResetForUnload()` and the gate will tell you if you forget to wire it. **Verify a new gate actually fires** by breaking the thing it guards and watching it go red before trusting it (this one was: the Patch71 call was deleted, the test failed naming that class, and the call was restored).
- **Source:** docs/reviews/rca-field-commission-reset-equipments-2026-08-20.md (finding 8, second pass), #486; sibling Codex review #46.

### A process singleton holding per-campaign state needs a session-reset story, or campaign B inherits campaign A

SyncData only fires when a save RECORD exists; a brand-new campaign never calls LoadFrom, so a
process-lifetime service keeps the previous campaign's dictionaries and can then SAVE them into
the new campaign. Transient caches (party trackers, scan clocks, guard latches, visual shown-maps)
are worse: they survive a save LOAD too, and a loaded save has NEW engine objects under the old
ids, so a stale tracker drives a ghost party.

**Why missed:** the singleton-service precedent (RacePositionStore) is config, where process
lifetime is correct; the contracts specified LoadFrom/SaveInto and nobody asked the no-record
question. **Prevent:** ResetForNewSession() called from the behavior's OnSessionLaunched when no
SyncData load happened this session, PLUS every transient cache cleared inside LoadFrom; pin both
paths with SessionReset tests. Review lifecycle passes must walk a second campaign in one process
and a load-with-same-ids.

**Source:** docs/reviews/rca-yotthani-camps-2026-08-23.md Class 1 (2 CRITICAL, found independently
by the unbiased round-A review and Codex).

### A redirect list is a MASK over an invalid state, and every mask is one un-masking away from the crash

Enlistment walked the player into a settlement with `EnterSettlementAction.ApplyForParty` alone,
which creates no `PlayerEncounter` and no `LocationEncounter`. That state is unreachable in vanilla:
`EncounterManager.StartSettlementEncounter` always builds both. It looked safe only because
`RedirectMenuIds` swallowed `town`/`castle`/`village` and showed a TAOM menu instead. Then two
paths removed the mask, a month apart, and both crashed identically on
`game_menu_settlement_wait_on_init`'s unguarded `PlayerEncounter.EncounterSettlement.IsVillage`:
discharge, by clearing the record to `NotEnlisted` (which the redirect is gated on) before placing
the player, and shore leave, by releasing the settlement menus on purpose.

**Why missed:** the hazard WAS documented, twice, in exactly the right places. The follow path's own
doc comment named the crash, and the feature doc said letting the player use the town needed the
`LocationEncounter` work first. Shore leave was then written against that same feature doc and
shipped without it. Review passes read the mask as the mechanism and never asked what state the
mask was hiding.

**Prevent:** when you find yourself suppressing vanilla UI to keep a state survivable, name the
invalid state out loud and fix IT, or accept that you have set a trap for whoever removes the
suppression. Route the state change through one adapter chokepoint, and pin the chokepoint with an
IL-scanning ban test on the unsafe primitive (`SettlementEncounterInvariantTests`, built on the
shared `IlCallScanner`). Prose in a doc comment demonstrably does not hold the line. An allow-list
entry must state how that caller keeps the vanilla UI unreachable.

**Source:** issue #510, player crash bundle `d7d9f7d3` (TAOM v2.0.20 on Bannerlord v1.4.8); site B
shipped in v2.0.21 and v2.0.22. `docs/features/enlistment.md` "The settlement-encounter invariant".

### A flag that gates the UI must also gate the LOGIC the UI is describing, or the feature is inert

Shore leave set `record.OnTownLeave`, and that flag reached exactly two places: the menu redirect and
the wait-menu re-assertion. It never reached the attachment layer. `grep OnTownLeave` over
`EnlistmentReconciler.cs` and `ServiceAttachmentService.cs` returned nothing, so `Assess` went on
returning `SettlementExitRequired` the instant the commander's settlement differed and the hourly
reconciler dragged the player straight back out of the town the pass had just unlocked. A second
mechanism, the pump's 4 Hz position sync, pulled in the same direction. The feature shipped, read
correctly at every individual call site, and did nothing at all for two releases.

The tell is a state flag whose every consumer lives in the presentation or menu layer. A flag that
changes what the player may DO has to reach the code that decides what happens TO them.

**Why missed:** the feature was specified and reviewed as a menu-routing change, which is what it
literally was, and the menu routing worked. Nothing asked the second question: what else acts on the
player while the flag is set? Unit tests passed because they exercised the redirect, which was right.

**Prevent:** when adding a state flag, grep for it across the whole feature and list every layer it
reaches; if it reaches only presentation, say out loud why no service needs it. And measure the
feature end to end at least once, because a two second window is invisible to every test and obvious
in one log line. The live log here read `FOLLOW 08:22:59` / `EXIT 08:23:01`.

**Related but distinct** from the mask lesson above: that one is about suppressing vanilla UI to hide
an invalid state; this one is about a flag that changes the UI without changing the state at all.

**Source:** issue #512, live log 2026-08-25, `docs/features/enlistment.md` "Shore leave holds the
settlement (#512)".

### Enumerate every exit from the scope that SETS a latch, not just the one that clears it

A flag that gates other code's behaviour was set at the top of a `try` in `ApplyPreview` and cleared in a `finally` in the matching `RestoreDefault`. The pair looked symmetrical, so nobody enumerated the setter's own exits: a parse-failure `return` and a broad `catch` both left it set. The flag suppressed `Patch9_RaceFilter`, so a failed preview silently disabled the culture race filter for the rest of that face-generator visit, for a player who was by then editing their own face again. Codex found a third path the Claude agents missed, and it is the subtlest: `RestoreDefault` cleared the flag AFTER calling `SetBodyProperties`, and that call is the only thing that rebuilds the filtered selector, so the one refresh that would have restored the filter was itself suppressed.
- **Why missed:** reviewing the closer is not reviewing the latch. `.claude/rules/harmony-patches.md` "Latches & Toggle Gates" already states the closer-per-opener rule, but its stated scope is Harmony patch latches and this latch lives in a Hook class, so it never pattern-matched. Scope gap, not a missing rule.
- **Prevent:** acquire a cross-cutting flag in a `try`/`finally`, or clear it on every early return. Then ask separately whether the clear happens at the right MOMENT: if lifting the suppression is what allows some refresh to run, clear before that refresh, not after it.
- **Source:** docs/reviews/rca-player-switcher-2026-08-27.md findings 1 and 10 (#514).

### There is no rollback past `ChangePlayerCharacterAction`; report a post-commit failure as such

The handover wrapped its whole sequence in one catch that reported "continuing as the created character". That message is true only before `ChangePlayerCharacterAction.Apply` runs. After it, `Game.Current.PlayerTroop` has changed and the player-character-changed events have been dispatched to every listener; the engine offers no transaction and no rollback, so the player IS the new hero and the message is a lie about their own identity.
- **Why missed:** the test covering the catch threw at the ENTRY to `ApplyPlayerCharacter`, before any mutation. It validated the one scenario where the message is accurate and looked like it validated the general case. A test proves the scenario it constructs and nothing more.
- **Prevent:** when a sequence contains an irreversible step, track whether it ran and report post-commit failures with a distinct outcome and a distinct message. Write the failure test to throw AFTER the irreversible step, not before it.
- **Source:** docs/reviews/rca-player-switcher-2026-08-27.md finding 6 (#514; Codex P1).

### A guard that silently disables a feature needs a log line or a test that fails without it

`AiPartySizeSettingsWatcher.EnsureSubscribed(null)` returned quietly when MCM had not registered its settings yet. Correct and safe, and undiagnosable: the symptom of a real occurrence (an MCM change not taking effect until a reload) is exactly the symptom of the bug the watcher exists to fix, so it would have been misdiagnosed as the original defect and the guard never suspected. The same review found the paired shape in its own test, `EnsureSubscribed_RepeatedAndNullCalls_AreSafe`, whose stated subject was idempotence and whose assertion was `Assert.IsTrue(true)`: it passed with the `ReferenceEquals` guard deleted.
- **Why missed:** both were written as fail-safe and reviewed as correct. The question asked was "is this safe?", which they were. The question not asked was "if this safe path fires wrongly, how would anyone ever know?"
- **Prevent:** for every early-return that turns a feature OFF rather than throwing, require one of: a warning log naming the feature, or a unit test that fails when the guard is deleted. "No exception thrown" is not a passing condition for a test about behaviour. Applies hardest at optional-dependency attach points (MCM, a co-op host, an absent config file), where the guard is expected to fire in legitimate setups and so never looks suspicious.
- **Source:** docs/reviews/rca-ai-party-size-player-clan-2026-09-01.md findings 1 and 3.


### "Same shape as the sibling" is a design statement, not a correctness one -- audit the sibling before mirroring it

`MemoryStationSampler` was written as a deliberate mirror of `MemoryPressureSampler` ("Singleton like
its sibling") and inherited its un-reset state along with its shape: `_emitted` and `_capReported`
were never cleared, so a cap documented and named as per-SESSION was really per-PROCESS. A second
campaign in one process would inherit an exhausted budget, emit nothing at all, and leave its only
cap-reached warning sitting in a previous campaign's log.
- **Why missed:** mirroring an established sibling reads as the conservative choice, and the review
  attention went to what was NEW rather than to what was copied. The sibling's own latches
  (`_sessionLineEmitted`, `_warnLatched`, `_readFailureWarned`) still carry the same defect, so the
  pattern being copied was already an unfixed instance of this file's own rule.
- **Prevent:** before mirroring a class, run this file's checklist against the CLASS BEING COPIED and
  fix or explicitly inherit-with-reason. For any process-lifetime singleton, name the boundary that
  clears each field: the hook that fires on it (here `OnBeforeInitialModuleScreenSetAsRoot`, which
  re-fires on every main-menu return) and a test that a second session starts clean.
- **Source:** deep review of the memory-diagnostics changeset, 2026-09-01; RCA `docs/reviews/rca-memory-diagnostics-2026-09-01.md`.

### A "does the entity have X yet" test is not a new-game-vs-loaded-save discriminator
A legacy-data fallback gated on `!HasCareer(hero.StringId)` reads as "this is an old save", but on a
brand-new campaign it is equally true, because `OnSessionLaunched` fires before character creation
has even started: v1.4.8 `Campaign.DoLoadingForGameType` raises `OnSessionStart` at line 1695 and CC
is only pushed afterwards by `SandBoxGameManager.OnLoadFinished`. `Hero.MainHero` is still the
vanilla `main_hero` template there, culture `battania`, name Eren. So CareerSystem handed every new
player a Khand career plus its root choice about a minute before they picked their own, and because
`SetCareer` overwrites the career id without touching `ChoiceIds`, the ghost survived into the save
and permanently consumed the level-1 career point. Players reported it as "levelling grants no
career points"; the workaround they found, switching career at a lord and back, worked only because
`CareerSwitchService` is the single code path that calls `ClearCareer`.
- **Why missed:** the gate encodes an inference about lifecycle rather than reading it, and the
  comment beside it ("New games get career assigned during CC finalization") asserted the ordering
  the code depended on without anyone checking it. The test file had pasted a private copy of the
  fallback algorithm and asserted against that copy, so "should this run at all, and when" was not a
  question any test could fail. Two prior fixes in this same feature, the Phase 9b #128 ability-cache
  reset and #130 `RacePersistenceBehavior`, had already met the sibling half of this bug class.
- **Prevent:** when a handler must distinguish a new campaign from a loaded save, subscribe the event
  that IS that distinction. `OnGameLoadedEvent` fires only for saved campaigns and only before
  `OnSessionStart`; `OnNewGameCreatedEvent` fires only for new ones, and only after
  `CampaignBehaviorManager.RegisterEvents()` runs, so a listener added in `RegisterEvents` still
  receives it (`Campaign.cs`: 1583 calls `OnNewGameCreatedInternal`, which ends with
  `RegisterEvents()` at 1624; the dispatcher then fires at 1585). Never infer the phase from entity
  state. Companion rule for anything a wrongly-timed write already persisted: ship a repair pass that
  drops the bad rows on load, because the save keeps the defect long after the code stops making it.
  And when a test extracts an algorithm into its own copy to avoid an engine dependency, that copy
  can never cover WHEN the algorithm runs, which is the half that ships broken.
- **Source:** the 2026-09-02 career-points investigation; player logs `taom_debug_2026-09-02_09-50-33.log`
  and `_11-48-55.log` both show the fallback granting `blademaster_of_ren` 58 seconds before the real pick.

### A destructive repair deletes only on positive proof; a lookup's fallback is never that proof
A repair pass that prunes stored state must be written so that failing to prove an item BELONGS is
not the same as proving it is FOREIGN. The career-points repair originally built an allow-list from
the hero's career groups and deleted everything outside it. That reads identically on a healthy
install and is ruinous on a broken one: `CareerConfigProvider.EnsureLoaded` loads `taom_careers.xml`
and `taom_career_choices.xml` under SEPARATE try/catch blocks, so a malformed choices file leaves
every career resolvable and every group empty, the allow-list collapses to the root choice, and the
pass deletes the player's entire career tree, permanently, at the next save. Pre-fix, a broken
choices file was survivable and self-correcting.
- **Why missed:** the guard written was `if (career == null) return 0`, which covers the career being
  missing, not the CHOICES being missing, and every review question asked was about the career. This
  is a second instance of `csharp-architecture.md`'s "Lookup Functions With Fallbacks: Validate Before
  Lookup" (`GetChoicesForGroup` returns `EmptyChoices` as a survival fallback and that fallback was
  consumed as an acceptance criterion for a delete) and a set-shaped instance of the same file's
  NaN-gate polarity rule, which is scoped to floats and therefore did not fire. Both rules existed.
  An adversarial agent aimed squarely at this question read the provider, saw the collections were
  "possibly empty, never null", and concluded a load failure "degrades to keeps-everything", true
  only if both files fail together.
- **Prevent:** for any operation that DELETES persisted state, require an affirmative reason per
  item and treat every unresolvable answer as keep. Name the degenerate inputs explicitly (empty
  collection, null id, empty-string id) and assert each keeps everything. Ask of any allow-list: what
  does this contain when its data source failed to load, and what does the code then do? A repair
  that repairs nothing is always recoverable; one that deletes everything is not.
- **Source:** `docs/reviews/rca-career-points-2026-09-02.md` findings 1 and 4.

### A test that stubs the collaborator it is pinning behaviour against pins nothing
The career-points repair identified a choice's owning career through the choice's group. Every root
choice in `taom_career_choices.xml` carries `group_id=""`, so the real registry returns no owner for
any root, the ghost root would have survived, and the fix would not have fixed the reported bug. The
suite was green: the lifecycle tests stubbed `GetOwningCareerId("blademaster_of_ren_root")` to return
a career id that the real `CareerRegistry` never returns for a root choice.
- **Why missed:** the substitute encoded the author's belief about the collaborator rather than its
  behaviour, so the test asserted that belief back. All six review agents passed it, because the
  fiction was internally consistent and the production code matched it exactly.
- **Prevent:** when a test exists to pin how a collaborator RESOLVES something (ids, ownership,
  lookup semantics) rather than how the unit reacts to a resolution, drive it with the real
  collaborator over synthetic config. Keep substitutes for the unit's own branching. The tell:
  a `.Returns(...)` whose value you would have to check the data files to justify.
- **Source:** `docs/reviews/rca-career-points-2026-09-02.md` finding 2;
  `CareerRegistryTests.GetOwningCareerId_RootChoiceWithEmptyGroupId_ResolvesToItsCareer`.

### A comment asserting an invariant is not enforcement of it

`DeadMountDespawnService.CollectDue` returns `_dueBuffer`, its own reused `List<int>`, and the
caller's fade loop iterated it directly while calling `Agent.FadeOut`, which can drive
`Mission.OnAgentDeleted` synchronously. The behavior carried a comment saying the loop was safe
"precisely so" it never enumerated a mutable collection. The reasoning behind that was correct
(`Forget` touches `_deathTimes` alone, and nothing reachable re-enters `CollectDue`), but nothing
enforced it: a later `CollectDue` call anywhere in that call graph would hit `_dueBuffer.Clear()`
mid-iteration, and the comment would still read as true.
- **Why missed:** the comment recorded a conclusion the author had actually reached, so re-reading
  it confirmed the reasoning instead of testing whether anything held it in place. Only the
  data-flow agent, which opens both files at once and checks the identity of a returned collection,
  could see that the safety was a property of today's call graph rather than of the code.
- **Prevent:** when writing a comment that asserts a safety property, name what enforces it. If the
  honest answer is "nothing, but no current caller violates it", either write it that way ("safe
  today because X") or spend the few lines to make it structural. Here the fix was three lines: the
  caller copies into its own scratch list, so the invariant is local to one method instead of spread
  across two files. Watch for the words "structurally", "cannot", and "precisely so" in a comment
  about concurrency, re-entrancy, or ownership.
- **Source:** `docs/reviews/rca-mount-despawn-2026-09-03.md` finding 2.


### Two independent self-heals can become each other's precondition, and neither will ever fire
Enlistment had two recovery mechanisms written months apart. One was the only exit from a latched
battle state and refused to act while a `PlayerEncounter` was open; the other was the only thing that
closes a stranded `PlayerEncounter` and refused to act unless the state was already unlatched. Each
was the other's precondition, so the pair was a permanent deadlock: the player could not move, could
not open any encounter, and lost the feature's menu, with no path out short of a seven-day discharge
timer on a different code path.
- **Why missed:** each guard is locally correct and defensible, and neither file mentions the other. Unit tests covered each mechanism in isolation and both passed. The failure only exists in the product of the two, which nothing in the suite or the review checklist looked at.
- **Prevent:** when adding a recovery path, enumerate every OTHER recovery path for the same subsystem and ask what each one waits on. If A waits on a condition that only B clears, and B waits on a condition that only A clears, that is a deadlock regardless of how reasonable each guard reads alone. Prefer guards that name the real hazard (is this encounter part of a battle?) over guards that proxy it with a state or a flag that something else owns; a proxy is what lets two mechanisms disagree about what they are protecting.
- **Source:** docs/reviews/rca-enlistment-strand-2026-09-04.md (#538).

### A guard you delete because "it was never the real guard" may be load-bearing for a case you have not named
Removing a state gate from a self-heal, on the reasoning that the other guards expressed the real
condition, would have let the sweep run inside the battle loot/aftermath window: `MapEventSide.Clear()`
nulls `MainParty.MapEvent` BEFORE the encounter closes, so for a few moments every remaining guard
reads "no battle anywhere" while the player is looking at their siege results. The deleted state gate
had been the only thing keeping the sweep out of that window, by accident.
- **Why missed:** the reasoning was symmetrical and sounded principled ("the state is a proxy, the map-event checks are the real condition"), and two other places in the same feature already documented the aftermath window in comments the author had read for other purposes without connecting them.
- **Prevent:** before deleting a guard, state what it currently excludes and find at least one concrete case in that excluded set. If the codebase contains another predicate over the same subsystem, diff yours against it term by term and account for every term you do not have: here the sweep's guards were exactly `noBattleAnywhere` minus its `!HasCurrent` term, which is the whole finding in one line. Then replace the proxy with a guard that names the hazard directly, so the protection survives in states nobody enumerated.
- **Source:** docs/reviews/rca-enlistment-strand-2026-09-04.md (#538), caught by the deep-review data-flow pass, not by the author or the tests.
