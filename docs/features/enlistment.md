# Enlistment — serve as a soldier in a lord's party

> **STATUS: FEATURE-COMPLETE, AWAITING IN-GAME VERIFICATION** (#375; commits `25a3340c` →
> `b1852a7a` → `554b6993` → `d905cb36` → `6557a202`). Core, dialogs, content systems, duties
> and equipment are built and unit-tested. Nothing has run in a live game — the checklist at
> the bottom is the remaining gate, along with `/localize` for the 36 English keys in
> `taom_enlistment_strings.xml`. Reviews: two internal `/deep-review` cycles plus an
> independent Codex pass (`docs/reviews/rca-enlistment-core-2026-08-04.md`,
> `docs/reviews/rca-enlistment-content-2026-08-05.md`).

**Issue:** #375 · **Donor (reference only, never installed):**
`C:\Users\mikew\Downloads\TAOM-Enlistment-Promoted-Source\` (Realms Forgotten RF_Enlistment)
· **RCA for checkpoint 1 review:** `docs/reviews/rca-enlistment-core-2026-08-04.md`

## What it is

The player petitions a lord, swears an oath in conversation, and serves as a common
soldier: their party is hidden and parked at the commander's position, a wait menu renders
service, the commander's battles are joined automatically on the commander's side, and
service ends only through a single discharge pipeline.

## The state machine (the core design decision)

`EnlistmentState`, persisted: `NotEnlisted → PetitionPending → EnlistedAttached ⇄
{EnlistedBattle, EnlistedDetachedOnDuty*, EnlistedPlayerCaptive, CommanderUnavailable} →
Discharging → NotEnlisted` (*reserved for the content phase). Full legal-edge set:
`EnlistmentTransitionTable` (20 edges, pinned by an exhaustive 64-pair matrix test).

The donor mod used `MobileParty.IsActive=false + IsVisible=false` AS the enlisted state,
re-derived through five overlapping predicates — the root cause of most of its bugs. Here
presence flags are **outputs**: `ServiceAttachmentService.Assess()` is the single pure
attachment computation, `MobilePartyAttachmentAdapter` is the only presence writer, and
the hourly `EnlistmentReconciler` is the single terminal-decision authority (commander
death, grace start/expiry, captivity sync, re-parking). Menus and dialogs only render.

Key policies:
- **Discharge:** `DischargeService.Execute(reason)` — fixed order, restores party presence
  unconditionally BEFORE clearing the record. The donor's commission-exit softlock is
  structurally impossible; `DischargeServiceTests` pins restore-before-clear for every
  `DischargeReason`.
- **Commander captured/party-less:** `CommanderUnavailable` + 7-day grace (config), player
  freed to roam; recovery re-parks, expiry/death discharges honorably. Grace freezes while
  the player is captive.
- **Player captivity:** vanilla owns the party — we never touch presence while captive.
  Event-driven via `HeroPrisonerTaken`/`HeroPrisonerReleased` + hourly fallback.
- **Never touches `MobileParty.Army`** — parking is position-sync + hidden/inactive;
  battle join works through the encounter layer without army membership.
- **Identity guard:** `EnlistedHeroId != MainHero.StringId` at load → quiet discharge
  (co-op join, heir succession — PlayerPossession three-guard pattern).
- **Co-op:** every world-mutating handler is host-only (`ICoopSessionProvider.IsAuthority`).

## Save format

Primitive-dict SyncData under `_taom_enlistment` (no SaveableTypeDefiner): inner keys
`_taom_enlistment_v` (schema version, "1") + `_taom_enlistment_core` (`key=value;` pairs,
unknown-key tolerant for forward compat). Persists only: state, enlisted/commander hero
ids, petition id, enlisted/contract/grace day timers. `EnlistedBattle` and `Discharging`
never persist (coerced to `EnlistedAttached`). Non-finite day values are dropped
field-level on parse (NaN-gate rule); unfaithful records (missing identity ids, unknown
version) reset to NotEnlisted and the load normalizer's ownerless-parked rescue restores
the party — a hidden inactive MainParty with no service record never survives a load,
including foreign/corrupt saves.

## Menu + battle layers

- Wait menu `taom_enlistment_service_wait` (`EnlistmentMenuBehavior` +
  `EnlistmentWaitMenuPresenter` — text built once per menu init, position sync throttled
  to every 5th tick). Menu-guard policy in `EnlistmentMenuService`: redirect ONLY while
  `EnlistedAttached`, from a config-driven id list (11 defaults incl. the 1.4.x naval
  `port_menu`/`naval_town_outside`), fail-open with a once-per-id diagnostic funnel.
- Battle: `EnlistmentBattleBehavior` (MapEventStarted/Ended boundary) →
  `ServiceBattleService`. Ordering contract: state flips to `EnlistedBattle`
  (redirect-exempt) BEFORE any encounter/menu push, then presence → position →
  encounter → join → menu. Failed joins roll back to parked-attached, finishing any
  encounter they created first. Post-battle, the state stays `EnlistedBattle` while the loot
  encounter is open (so the guard can't eat aftermath menus); wait-menu init + reconciler
  close the loop.
- **Two join paths, both live.** The `MapEventStarted` edge, plus an hourly recovery:
  `IEnlistmentReconciler.BattleJoinRequested` → `ServiceBattleService.TryJoinCommanderBattle`,
  raised when the commander is in a map event and the player is not. The recovery path covers
  a missed edge (save-load mid-battle, a throw, enlisting into a running fight).
- **The player has NO battlefield command while enlisted** (#424, PR #426).
  `EnlistmentBattleRoleMissionBehavior` calls `Team.SetPlayerRole(false, false)` at `AfterStart`
  when the battle was entered as `EnlistedBattle` and the player does not lead the side;
  `BattleCommandPolicy` is the pure decision table. Detached-duty fights keep vanilla roles — the
  player really does lead their own force there.

### Battle-role facts that follow from Army being null (verified 1.4.7 — do not re-derive)

Everything below is a consequence of the SAME null `Army` that `ParkNear`/`RestorePresence` enforce.
Written down because two of them were re-derived wrongly during PR #426's review.

| Fact | Why |
|---|---|
| Without the correction, the enlisted player is the **general** of his side — not merely "not a sergeant" | `IsPlayerSergeant()` needs `Army != null`, so it is false; `SandBoxMissions` passes `!isPlayerSergeant` positionally as `isPlayerGeneral`; `Team.SetPlayerRole` then does `SetControlledByAI(this != PlayerTeam \|\| !IsPlayerGeneral)` across every formation |
| Neither-general-nor-sergeant is a **supported vanilla state**, not untested ground | `BehaviorComponent.cs:105` branches on exactly `!IsPlayerGeneral && !IsPlayerSergeant && IsPlayerTroopInFormation` — the soldier-receiving-orders path. The correction makes it reachable |
| The correction cannot be overwritten later | `SetPlayerRole` has exactly one engine call site (`Mission.cs:745`, team creation), and no `AddTeamAI` caller passes `forceNotAIControlled: true` |
| TAOM's `AfterStart` runs **after** vanilla's role assignment | `AddMissionBehavior` appends; the mission's own controllers enter via `InitializeStartingBehaviors` at construction; TAOM appends during `OnMissionBehaviorInitialize`; `Mission.AfterStart` then iterates the list in order |
| **The Order-of-Battle screen is unreachable while enlisted, and always was** | `SandboxBattleInitializationModel.CanPlayerSideDeployWithOrderOfBattleAux()` offers deployment only if the player leads the side, owns the besieged settlement, or `IsPlayerSergeant()`. All three are false while enlisted → `DeploymentMissionController` calls `FinishDeployment()` immediately. This predates the correction, which is why its ordering relative to deployment setup does not matter |

Do **not** adopt the reference mod's approach of rigging `GetCharacterSergeantScore`: that score also
feeds `DefaultEncounterModel.GetLeadingScore → GetLeaderOfMapEvent`, so it changes who leads the
battle at **campaign** level (sally-out menu, `PlayerEncounter.LeaveBattle`), not just in the mission.

### Why the player's party is NOT attached to the commander (settled 2026-08-07 — do not re-derive)

The obvious design — `MobileParty.MainParty.AttachedTo = commanderParty`, letting the engine carry
the player along — is **rejected**. It is not a better architecture we decline on cost; its naive
form is a guaranteed campaign crash. Verified against the installed 1.4.7 assemblies:

| Finding | Evidence |
|---|---|
| **Attaching without an Army is an unavoidable NRE** | `DefaultEncounterGameMenuModel.GetGenericStateMenu()` (`:230-232`) dereferences `mainParty.Army.LeaderParty` **unguarded**, immediately inside `if (mainParty.AttachedTo != null)`. `Campaign.Tick()` (`Campaign.cs:992-994`) calls it on every tick where the active state is `MapState` and not at a menu — i.e. every frame on the open map. |
| **Attaching WITH an Army means actually joining the army** | `Army.AddPartyInternal` is the engine's only writer of `AttachedTo`. That brings kingdom membership, faction/AI/cohesion semantics and the army UI, and undoes `LordConversationsJoinArmyConditionPatch`, which TAOM added deliberately. |
| **Even then, a parked attached party poisons everyone's battles** | `PartyBase.MapEventSide`'s setter cascades to `AttachedParties` (`PartyBase.cs:322-325`), and `MapEvent.CanPartyJoinBattle` (`:1436-1443`) requires `IsActive` for **every** party on **both** sides. One inactive attached party makes reinforcement joins fail for every AI lord on either side. |

The wins attachment appeared to offer are delivered elsewhere: settlement following through
`EnterSettlementAction`/`LeaveSettlementAction`; battle participation through the encounter path.
And it would **not** have fixed the ~1.8-unit position drift at all — leader-relative movement runs
through `IsAttachedArmyMember`, which requires `Army != null && AttachedTo != null`. That drift is a
tick-ORDERING problem, not a throttle or attachment one.

`ParkNear` and `RestorePresence` therefore clear `AttachedTo` rather than setting it.

### Service-lifecycle rules learned in live play (2026-08-07 — read before touching this path)

The first shipped build never joined a battle and left the player unable to interact with the world.
Four causes, all found by instrumenting a live session rather than by reading code:

| Rule | Why |
|---|---|
| **Close the conversation's `PlayerEncounter` when the oath is sworn** | The oath happens inside a conversation, which runs inside a `PlayerEncounter`. Parking without closing it left `PlayerEncounter.Current` live for the whole term — measured at `playerEncounter=True` on **93 of 93** ticks — and `EncounterManager` refuses EVERY main-party encounter while it is set. That single leak made the player unable to click any lord or settlement, and it survived into discharge. The donor closes it at the same point (`FinalizeEnlistmentConversation`: `Finish()` then attach). Discharge closes it too, and the hourly reconciler self-heals saves already stuck in that state. |
| **Every return to parked service owes the player a menu** | Re-parking alone leaves them on the open map with no menu and no way to act — reported as "after battle I was left behind and the option menu isn't here". The only re-assert was `OnConversationEnded`, which never fires on the battle path. Both battle paths (normal end, failed-join rollback) now call `ReassertServiceMenu`. |
| **Never anchor world spawns on the commander's CURRENT settlement** | `CommanderSnapshot.SettlementId` is empty whenever the column is marching — nearly always — so hunt duties could only start while the commander sat in a town. Every `recon_sweep` failed with `SpawnLooterParty: settlement=''`. Falls back to `FindNearestFriendlySettlement`. |
| **Calibrate diagnostic thresholds against a real session, not a guess** | The drift warning used `> 1f`; ordinary inter-tick drift while marching is ~1.8, so it fired on essentially every sync — **291 of one session's 299 warnings**. A per-world-map-event line produced another 3674. The threshold is now `15f`, and the routine lines sit behind the toggle described below. A diagnostic that fires constantly is indistinguishable from no diagnostic. |

**Verified in live play 2026-08-07:** field battle join (instant, via `MapEventStarted`), siege assault
join, hourly-recovery join when the immediate edge is missed, return to parked service after battle,
and config load without reverting. The measured steady-state drift while following is ~1.8 map units —
the player is teleport-chased rather than genuinely attached, which is what "dragged along" describes.

### The `[EnlistDiag]` volume toggle (MCM: Enlistment/Diagnostics)

`FileLogger` has **no level filter**. Every call builds its string caller-side, timestamps it, and the
writer thread puts it on disk — so a `LogDebug` downgrade changes durability, never volume. In one
32-minute session `[EnlistDiag]` was **4,856 of 6,001 lines (81%)**, 3,674 of them from the single
"map event started, commander NOT in it" line. The gate therefore had to go at the **call site**, and
`Main/Core/Logging/FileLogger.cs` is deliberately untouched.

**`TaomSettings.EnableEnlistmentDiagnostics`** (MCM group `Enlistment/Diagnostics`, `RequireRestart =
false`) reaches the call sites through `IEnlistmentDiagnosticsSettingsProvider`, whose singleton reads
the static on every call — which is what makes "takes effect immediately" true rather than aspirational.

**It ships OFF since 2026-08-09**, and the provider's MCM-absent fallback is `?? false` to match.
Those are two independent literals encoding one decision; `CompiledDefault_AndProviderFallback_Agree`
fails if either moves alone, so a player without MCM never gets different logging from a player at
the shipped default. The MCM-absent case is the one that decides the direction: a player with no
settings host cannot turn the trace off in game, so defaulting them into it is the only choice that
is unrecoverable.

It shipped **ON** for as long as the service loop was under diagnosis, and it earned that — the
trace is what found the battle-join defect (#406), the enlisted-general defect (#424) and the duty
exposure (#428). The flip came when #375 closed on a field-verified session. That session is also
the measurement: **950 of 3,452 log lines were enlistment, and five gated shapes accounted for 851
of them** — `TICK` (202), `SYNC ok` (199), `PARK ok` (23), `RESTORE ok` (12) and the per-map-event
line (450). The 52 ungated INFO sites produced the other ~99. A player now carries the events and
none of the trace; turn it on before reproducing a problem you intend to report.

**Gated lines emit at INFO, not DEBUG.** DEBUG is `FileLogger`'s async queue and a hard native CTD
discards whatever is still in it — under a DEBUG design the trace you switched on to catch a crash is
exactly what the crash destroys. Volume is the toggle's job; durability is the level's.

| Gated (routine, ~90% of measured volume) | Always-on (faults + forensic controls) |
|---|---|
| `TICK` (reconciler), `SYNC ok`, `PARK ok`, `RESTORE ok`, "map event started, commander NOT in it" | every `PARK/SYNC/RESTORE FAILED`/`THREW`, the drift WARNING above `15f`, the stranded-`PlayerEncounter` sweep + its failure, "verdict=Attached but the party is NOT parked", and every `DischargeService` line — including `LEFT THE PLAYER UNABLE TO START ENCOUNTERS`, which is never gated under any circumstance |

**The per-map-event line is additionally throttled to once per episode** (2026-08-09). Narrowing it
in August to "only when the commander's party does not resolve" was correct for the defect it was
chasing and wrong one level down: *unresolvable* is not rare, it is the **defining condition of the
`CommanderUnavailable` grace**, so the moment a commander loses his party the line reverted to the
every-world-event firehose it had been narrowed to escape — 450 lines in five minutes, measured
live, each a synchronous flush, describing bandit skirmishes the player has no part in.

It throttles on the **commander id**, not on `State != CommanderUnavailable`. The state gate would
leave the 33-second window between the party vanishing and the reconciler transitioning unthrottled,
and — the reason that matters — it would still spam in the one case the line exists to catch: a
commander who *is* present while resolution fails, which is #406 and which leaves the state at
`EnlistedAttached`.

`PARK skipped` (was `PARK FAILED`) is a **WARNING, not an ERROR**, for the same reason: a commander
with no active party is the grace's normal condition, not a fault. Live on 2026-08-09 it fired at
ERROR four seconds before `commander lord_4_1 lost their party — grace until day 26039.4`, and it
was the only ERROR in a 3,452-line session — the first line anyone would chase in a crash bundle,
and a dead end. The sibling `MainParty is null` branch stays ERROR; that one cannot be legitimate.

**"Toggle off" does NOT mean "zero `[EnlistDiag]` lines"** — the toggle gates volume, not the tag,
because `[EnlistDiag]` is the grep handle a bug reporter needs. Fault lines keep it. Expect a support
question about this.

**The trap, for anyone editing a gate:** in `EnlistmentReconciler.ReconcileAttached` the logging is
*interleaved* with `_encounter.Finish()`, `EnsureParked()` and `SyncPosition()`. A gate is
`if (_diag?.IsEnabled == true)` around **exactly one logging statement** — never a block, never
guarding a `return`, never above a mutation. `EnlistmentDiagnosticsGateTests` group B1 runs the
reconciler with the toggle off and asserts each mutation still happens; a source-scan guard in the
same file rejects `IsEnabled) return` and `if (!… IsEnabled` across all three gated files.
`DischargeService` takes no provider at all, which is what makes its lines structurally ungateable.

### Battle-join rules learned the hard way (2026-08-07 — read before touching this path)

The first shipped version never joined a single battle. Five separate defects, all on one path:

| Rule | Why |
|---|---|
| **Never gate the join on `MapEvent.CanPartyJoinBattle`** | It requires every party on the opposing side to be at war with the joining party's `MapFaction` — "may this free agent lawfully intervene?". An enlisted soldier keeps their own clan and is normally at war with nobody, so it is `false` for every battle. It also demands every party on both sides be `IsActive`. The donor mod hit this and explicitly tolerated a false result while parked. Use the mechanical `IsCommanderBattleJoinable` (event exists, not finalized, side resolves). |
| **Restore presence BEFORE any encounter work** | The engine skips inactive parties in encounter detection (`EncounterManager` line 38). Checking joinability while parked is a chicken-and-egg. |
| **Seed the encounter from `MapEvent.AttackerSide/DefenderSide.LeaderParty`** | Those are `PartyBase`, so a besieged settlement is representable. Resolving through the `MapEventStarted` arguments drops sieges silently — a settlement defender has no `MobileParty`, so its id is null. |
| **Verify the join actually landed** | `PlayerEncounter.JoinBattleInternal` silently calls `Finish()` when `EncounteredBattle` is null. A non-throwing call proves nothing; check `MainParty.MapEvent != null`. A false success left state at `EnlistedBattle`, which then blocked every later battle. |
| **Never leave an orphaned `PlayerEncounter`** | `EncounterManager` gates the main party on `PlayerEncounter.Current == null`. A leaked encounter stops the player entering any future one. Worse: a `MapEventSide` acquired without a live encounter + open `encounter` menu freezes that map event forever (`MapEventManager.Tick` skips the player's event; only `PlayerEncounter.Update` advances it), which leaves the commander permanently unable to start battles. |

## Patch66_Enlistment (see harmony-patch-registry.md)

`GameMenuManager.SetNextMenu` prefix (menu-id rewrite — the load-bearing guard; no event
alternative exists), `MapState.EnterMenuMode` postfix (recovery for ids written without
SetNextMenu), 4× `LordConversationsCampaignBehavior` condition prefixes (suppress
join-army/ally-thanks lines while enlisted; the clickable variant carries `out TextObject
hint`; ally-thanks pair is PUBLIC on 1.4.7). All fail open. All six targets pinned by
`Patch66EnlistmentBindingTests` against the installed DLLs. The donor's
`MapState.OnMapConversationOver` patch was replaced by `CampaignEvents.ConversationEnded`.

## The remediation arc (2026-08-07, batches 0-10)

The shipped feature had a defect that made the whole fantasy inert: **the enlisted player never
joined a single one of their commander's battles.** `ServiceBattleService` gated joining on
`MapEvent.CanPartyJoinBattle`, which is a *diplomacy* test — it requires every party on the
opposing side to be at war with the joiner's `MapFaction`. An enlisted player keeps their own clan
identity and is typically at war with nobody, so it returned false for every battle that has ever
existed. Unit tests mocked `IEncounterAdapter`, so all of them passed. Three rounds of code review
missed it; instrumenting the live game found it in one session.

### What each batch changed

| Batch | Change | Why it mattered |
|---|---|---|
| 0 | `Settlement.Find` replaces `CampaignObjectManager.Find<Settlement>` | `Find<Settlement>` returns null **unconditionally** — Settlement is never registered with it. Every duty settlement lookup was silently failing. |
| 1 | A granted release is never desertion | `ClassifyLeaveReason` returned Desertion before `ContractEndDay`, and the contract defaults to 365 days — so every realistic exit forfeited the player's arrears and called them a deserter for asking leave and being given it. |
| 2 | Cheap per-tick adapter surface | `GetSnapshot` renders `Name.ToString()` and walks Culture/Clan/Settlement; it is forbidden on a pump. `GetTickSnapshot` + `PlayerPresenceFlags` are the allocation-free reads. |
| 3 | Real-time service pump | The engine dispatches `OnMapEventStarted` exactly once, as the last statement of `MapEvent.Initialize` — so a commander joining an ALREADY-RUNNING fight is invisible to it. `OnPartyAddedToMapEventEvent` is the only edge that sees a late join. |
| 4 | One encounter-ownership policy | Five places finished a `PlayerEncounter` with their own ad-hoc reasoning. A settlement encounter has no encountered MOBILE party — that fact is what separates "visiting a town" from "meeting a lord". |
| 5 | Discharge hand-back invariant (INV-D1) | Every discharge must leave the player able to act. Ordering is load-bearing at three points; each is documented in place. |
| 6 | Informed leave choice | Asking to leave now states its price with a real day count. Desertion has exactly ONE producer: a branch the player picked after being told the cost. |
| 7 | Settlement following | The player enters the town with the column instead of standing invisibly outside the gate for the whole stop. |
| 8 | Dialog agency + MCM master switch | Reassignment, the quartermaster and in-person discharge were shipped, tested, and unreachable — nothing could open a conversation. |
| 9 | Live status board | The wait menu said one unchanging sentence from oath to discharge. |
| 10 | Two re-attach edges + re-entrancy guard | The reconciler calls `LeaveSettlementAction`, which dispatches the edge it now subscribes. |

### Design decisions worth not re-litigating

**Attaching the player to the commander's party (`MobileParty.AttachedTo`) was considered and
rejected.** `DefaultEncounterGameMenuModel.GetGenericStateMenu` dereferences
`mainParty.Army.LeaderParty` unguarded inside `if (mainParty.AttachedTo != null)`, and
`Campaign.cs` calls it every tick on the open map. `AttachedTo` set without an `Army` is therefore
a guaranteed CTD, not a style question.

**Settlement following places the player inside but holds them in the TAOM wait menu.** Letting
them actually *use* the town is a separate feature: every in-settlement affordance routes through
`PlayerEncounter.LocationEncounter`, and a live `PlayerEncounter` while `EnlistedAttached` is
precisely what the reconciler's stranded-encounter sweeper destroys. That needs its own
`EnlistedOnLeave` state with its own save-compat and transition work.

**The release refusal is keyed to `MinimumServiceDays` (21), deliberately NOT `ContractDays`
(365).** Keying it to the contract would refuse every realistic request and leave desertion as the
only exit — Batch 1's bug wearing a different hat.

**Four of the donor's six re-attach edges are DECLINED, on purpose.** Subscribed:
`OnSettlementLeftEvent`, `OnPartyLeftArmyEvent`. Declined: player-battle-end, army-joined,
siege-joined, siege-left, siege-completed. The pump re-derives everything within 250 ms, and the
declined edges only re-arm a flag whose own retry budget is an hour anyway — so they buy nothing
the hourly pass does not already give. **The honest caveat:** `CampaignEvents.TickEvent` does not
fire inside encounter/settlement menus or a map conversation, and the wait-menu tick only runs on
our own menu, so in those windows a declined edge does fall back to the hourly reconciler. The two
that WERE subscribed are exactly the ones that fire in that gap.

### The lesson

Mocking the adapter meant no test ever exercised the seam between adapter and engine — which is
the one place unit tests structurally cannot reach, and where this bug lived. `docs/features/`
listed "encounter join from parked state" under *owed at ship, never run in a live game*, and that
gate is precisely the one that failed. Recorded in `docs/reviews/lessons/testing-qa.md`.
## Player-facing surfaces (what exists, and where it lives)

| Surface | Owner | Notes |
|---|---|---|
| Service wait menu + its four options | `Presentation/EnlistmentWaitMenuOptions.cs` | **Every option is `isLeave: false`, and that is load-bearing.** `GameMenu.RunMenuOptionConsequence` calls `EndWait()` BEFORE the consequence for an `isLeave` option on a wait menu, which sets `IsWaitActive = false` and `TimeControlMode = Stop` — tearing down the tick that drives the position sync before the consequence runs. It also makes the option a candidate for the Escape slot. |
| Live status board | `ServiceStatusService` + `Presentation/ServiceStatusTextWriter.cs` | Rebuilt on the pump's 2 s budget, pushed only when the model differs. **The value equality IS the throttle**, so a field omitted from `Equals` is a status line that never updates — one `DataRow` per field guards it. |
| "Speak with your commander" | `EnlistmentPlayerActionService` + `IMapConversationAdapter` | The adapter carries the game-state guard itself: `ConversationManager.OpenMapConversation` opens with `(GameStateManager.Current?.ActiveState as MapState).OnMapConversationStarts(...)` — an `as` cast dereferenced with no null check, so it throws when the state manager is null, the state stack is empty, OR the active state is anything else. `CampaignMapConversation.OpenConversation` adds a fourth route via `Campaign.Current`. |
| "Ask your sergeant for work" | `DutyOrchestrationService.RequestDutyNow` | Shares ONE offer path with the daily tick and the same rotation cadence, so asking cannot conjure work the rotation would not have given. Host-only. |
| Release / desertion | `Presentation/EnlistmentWaitMenuPresenter` + `Hooks/EnlistmentReleaseDialogBehavior` | Keyed to `MinimumServiceDays` (21), deliberately NOT `ContractDays` (365) — see below. |
| Reassignment | `Hooks/EnlistmentAssignmentDialogBehavior` | The commander names your CURRENT section first. Each option hides when it is your current role; without the naming line a player already in the horse saw no cavalry option and reasonably concluded it was missing (reported in-game 2026-08-07). |
| Enum → player words | `Presentation/ServiceVocabulary.cs` | The single place a `ServiceRank`/`ServiceAssignment`/merit grade becomes text. Section names had been written twice over the same localization keys; two copies of one key set drift silently. |
| **MCM master switch** | `TaomSettings.EnableEnlistment` + `IEnlistmentFeatureSettingsProvider` | Fails open when MCM is absent. **Turning it off mid-service performs one honourable discharge** rather than halting: an enlisted player is parked hidden and inactive, and the code that restores them is the code being switched off — stopping in place would strand them invisible with no menu, a soft-lock produced by a settings toggle. |

### `Presentation/` vs `Hooks/`

`Hooks/` is Harmony patches and `CampaignBehaviorBase` entry points, and carries the ADR-002
150-line ceiling. `Presentation/` holds registered singleton services that legitimately own
`TextObject` / `InformationManager` (ADR-007 keeps those out of the service layer) but are not entry
points. The presenter and text writer were originally in `Hooks/`, which made them read as ceiling
breaches when they were really misfiled.

### Why the release refusal is not keyed to the contract

`ContractDays` is 365. Keying "days still owed" to it would refuse every realistic release request
and leave desertion as the only exit — which is the bug fixed in batch 1 (`ClassifyLeaveReason`
returning `Desertion` before `ContractEndDay`, forfeiting the player's arrears and calling them a
deserter for asking their lord's leave and being given it). `MinimumServiceDays` (21) is a term a
player can actually serve, which is what makes "leave now and forfeit your pay" a choice rather
than a trap. `ClassifyLeaveReason` still returns `PlayerRequest` unconditionally, pinned by a test.

**Desertion has exactly one producer** — the dialog branch the player picks after being told the
cost. Nothing classifies a leave as desertion behind their back.

---
## The review pass (2026-08-08) — four terminal defects the tests could not see

A five-agent deep review plus an adversarial Codex pass ran over batches 0-10, against a suite that
was already 668-green. **Twelve findings; the suite caught none of them.** Four were terminal or
invisible, and all four lived in the seam between our code and the engine — the seam a mock is
precisely a decision to stop testing at.

### 1. Discharge could strand the player inside a settlement, permanently

`RestoreCampaignContext` chose placement from `CommanderSnapshot.SettlementId` and never asked where
the PLAYER was. Commander dead / marching / in a hideout while the player stood in a town → the
settlement branch was skipped, then the wait menu was closed. `CurrentSettlement` set, no menu.

Why it is terminal rather than annoying — all verified on installed 1.4.7:

| Engine fact | Consequence |
|---|---|
| `MobileParty.DoUpdatePosition` returns early when `CurrentSettlement != null` | the party cannot move |
| `CheckExitingSettlementParallel` early-returns on `IsMainParty` | the engine never auto-exits you |
| `game_menu_castle_outside_leave_on_consequence` = `PlayerEncounter.Finish(); SetMoveModeHold();` and `Finish` returns immediately when `Current == null` | the Leave option is a no-op |
| `DefaultEncounterGameMenuModel.GetGenericStateMenu` returns `null` for a village | no menu appears at all |

It survives save/reload, because the record now reads `NotEnlisted` and every recovery loop in the
feature early-returns on that. **Fix:** the settlement exit is now on EVERY path that did not just
open a real settlement menu, driven by the player's own presence.

### 2. A save taken mid-battle froze that battle forever

`ToPersistedState` coerces `EnlistedBattle` → `EnlistedAttached` on the stated grounds that battle
reality is re-derived at load. **That re-derivation was never written.** On reload the engine
restores `MapStateData.GameMenuId = "encounter"`, and the redirect — gated on `EnlistedAttached`,
which the coercion had just made true — swallowed it. `MapEventManager.Tick` deliberately SKIPS
`MainParty.MapEvent`; the player's own event advances only through `PlayerEncounter.Update`, driven
from that menu. The wait menu has no `isLeave` option, so it never closes on its own either.

**Fix:** `encounter` / `join_encounter` are exempt from redirect whenever the player is genuinely in
a map event. The coercion stays (a transient state has no business in a save), but it no longer
depends on a re-derivation that does not exist.

### 3. A duty starting inside a settlement made the player invisible for days

The settlement exit ended in `ParkNear` — correct while following the column, catastrophic on a
duty: parking hides and deactivates the party, and NOTHING un-hides it while the state is
`EnlistedDetachedOnDuty` (`Assess` returns `Blocked/NotInAttachableState` for that state, so neither
the reconciler nor the pump ever restores presence). Invisible and immobile for the deadline — four
to six days — then the duty fails.

**Fixed at the time** by `ExitSettlementForDuty()` — leave, then restore presence — deliberately a
separate member from `ExitSettlementForService()` because the two looked identical and differed only
in how they ended. **Both the bug and its fix are gone (2026-08-09, #428):** a duty no longer leaves
the settlement, or anywhere else, so the exit it needed has no callers. The member was deleted
rather than kept as a spare — a second exit path that ends in `RestorePresence` is precisely what
the current design must not have, and leaving it available is an invitation.

### 4. The wait-menu guard could switch itself off for the rest of the process

`EnsureServiceMenu`'s `_menuFailures >= MaxMenuFailures` check sat ABOVE both reset sites, on a
`Reuse.Singleton`. Three transient failures disabled the invariant for the whole process — across
re-enlistment and across campaigns. **Fix:** state transition before gate; plus
`ResetSessionCaches()` on game load.

### Also fixed

- **A NaN `CommanderGraceDays` never expired.** `nowDays >= GraceEndsAtDay` is false forever against
  NaN, so the player sat in `CommanderUnavailable` permanently with no auto-discharge. Sixth
  instance of that bug class in this codebase; first one caught before shipping.
- **A commander-party handle cached across a game load** matched by `StringId` (lord-party ids are
  stable across a reload of the same campaign) and drove the position sync from a destroyed
  campaign's party, at frame rate.
- **The pump's real-time budget was frame-rate dependent.** The wait-menu tick is a FRAME tick and
  was fed a constant `1f/30f`, fabricating ~4.8s of budget per real second at 144 fps — running the
  "4 Hz" expensive tier at ~19 Hz and the status board (which uses the forbidden `GetSnapshot`) at
  ~2.4 Hz. It now measures real elapsed time, clamped so a stall cannot burst it.
- **Deferred duty callbacks** granted rewards and mutated the record without re-checking authority
  or enlistment — the same stale-authorisation shape as the desertion confirmation.
- **`RestorePresence`'s result was discarded** immediately before encounter work its own comment
  calls a hard precondition.
- **`StaleBeforeCommanderBattle` was defined but never called**, so a leftover encounter burned the
  immediate join and the retry budget then delayed the next attempt by an hour — long enough to
  miss a short battle.

### What this says about testing this feature

668 green tests proved none of the above. The technique that found all twelve was reading our code
against the decompiled engine and asking *what does the engine do if this value is what my code
allows*. Recorded in `docs/reviews/lessons/testing-qa.md`; the practical rules that came out of it:

1. A test that stubs an adapter proves the SERVICE, never the adapter contract. Any comment saying
   "the engine will X" needs a decompiled quote beside it — that quote is the only verification.
2. Every guard needs the test that proves it RELEASES: back-off/recovery, latch/reset,
   park/restore. A one-sided test on a two-sided mechanism is how #4 shipped.
3. A property no test ever stubs makes every branch behind it unreachable in the suite. That is how
   #3 shipped — `FieldDutyRuntimeTests` never stubbed `GetPresenceFlags()`.

---
## Engine facts verified on installed 1.4.7 (Phase 0.2 sweep)

`MobileParty.Position` is `CampaignVec2` (no `Position2D`; `GetPosition2D` is get-only);
camera-follow is `PartyBase.SetAsCameraFollowParty()` / `Campaign.CameraFollowParty`;
`GameMenu.MenuOverlayType` is nested in `GameMenu` (no `GameOverlays` on 1.4.x); no
`TextObject.Empty` (use `new TextObject(string.Empty)`); `PlayerCaptivity.IsCaptive` =
`_captorParty != null`; `HeroPrisonerTaken: IMbEvent<PartyBase, Hero>`,
`HeroPrisonerReleased: IMbEvent<Hero, PartyBase, IFaction, EndCaptivityDetail, bool>`;
spatial search = `MobileParty.StartFindingLocatablesAroundPosition(Vec2, float)` +
`FindNextLocatable` iterator; `EncounterGameMenuBehavior` pushes 43 distinct menu ids
(redirect-list seed). Compatibility review: 37 verified / 0 incompatible / 0 unverified.

## Content systems (checkpoint 2)

**The service day.** `EnlistmentDailyService` counts the day, pays the wage, grants daily
service XP + the assignment's signature skill + Leadership + one context skill
(priority-exclusive: siege > naval > blockade > army — the donor stacked all four), then
evaluates promotion. Config: `ModuleData/enlistment/enlistment_config.json`.

**Wages.** `WagePolicy` is pure: the commander pays from his own gold above a solvency
floor; the shortfall defers into arrears capped at `wagePolicy.maxDeferredWageDays` (default
**14**) days of the player's *current* rank wage — 70 gold at Recruit, 308 at Sergeant.
`ServiceRewardService.PayDailyWage` spends that plan through exactly ONE channel (commander
transfer or mint, per config) and derives the new debt by conservation — owed minus delivered
— rather than patching the plan's fields. Anything owed above the cap is forfeited, and the
service logs `deferred-wage cap reached — N gold of back pay forfeited` when that happens; it
used to be destroyed silently. An honorable discharge settles remaining arrears; desertion
forfeits them.

> **Config migration.** The key was `wagePolicy.maxDeferredWages`, a flat **60-gold** ceiling —
> a Sergeant on 22/day reached it in 2.7 days and lost roughly 600 gold over a 30-day insolvent
> stretch with nothing in the log. An existing install's `enlistment_config.json` still carrying
> the old key gets a load-time warning; the old value is ignored (the two units are not
> convertible), so re-set `maxDeferredWageDays` if you had retuned it. Valid range `[0, 365]`.

**Ranks + promotion.** Four ranks, thresholds in JSON (days / service XP / Leadership /
duty successes / trust), evaluated at exactly two points — the daily tick and the battle
payout — both through `IPromotionService`. The donor evaluated at twelve sites including
mid-mission per kill.

**Battle merit.** `EnlistmentMeritMissionBehavior` (`: MissionLogic`, registered
unconditionally, self-filtering) samples cohesion, commander proximity, engagement and
survival every 2s and counts your kills, submitting ONE sample at mission end.
`EnlistmentBattlePayoutService` scores it 0-100, resolves a reward band, and pays
everything at once — base win/loss XP, capped kill XP, band rewards — then re-evaluates
promotion. Role fit (`RoleFitEvaluator`) asks whether you fought the way your assignment
wants: archers hold an 18-50m shooting line, cavalry work the flanks at 10-28m, support
stays near the commander, infantry holds formation *and* stays in contact. Never-measured
and non-finite inputs fail closed — no free bonus.

**Leaving early is not surviving.** Survival was read as "never went down", which a player who
walked out at t=5s trivially satisfies — so quitting immediately banked the full 25-point survival
weight. `OnMissionResultReady` now latches whether the battle actually reached a verdict:
`Mission.MissionResult` is assigned in exactly one place (`Mission.CheckMissionEnded`, which calls
that hook in the same block), whereas the retreat inquiry, surrender, and walking out of the battle
boundary all reach `RetreatMission()`/`SurrenderMission()` → `EndMission()` and never produce one.
No verdict means `MeritSample.LeftTheField`, which zeroes the survival term and subtracts
`meritScoring.leftFieldPenalty` (30) — enough to sink even a maximally-engaged walkout into the
bottom band. No trust debit rides along: `MeritBand.Trust` already falls with the band, and a
second debit would charge the same failure twice. The geometry moved out to
`MeritGeometryAccumulator` (pure, unit-tested) with the engine scan isolated in
`MeritGeometryScanner`, which put the behavior back under the ADR-002 ceiling at 139 lines.

**Assignments.** Infantry / Archer / Cavalry / Support, changed by asking the commander;
costs a 7-day cooldown and a point of trust (the donor allowed free swaps in any
conversation, which made the choice weightless).

**Duties.** 13 field duties, plus 11 interactive skill-check duties and 3 camp incidents through
one presenter. Rows failing validation are SKIPPED with a warning (never silently defaulted).
`IArmyRhythmSnapshotService` caches the world read once per game hour.

A field duty is **camp work, not a journey** (reworked 2026-08-09, #428): you are given orders, they
occupy you for `durationHours` (4–8), and one `ISkillCheckService` roll against the row's
`difficulty` (45–76) decides the outcome. Success pays `reportReward`, failure pays
`failureReward` — both through the one `IServiceRewardService.Grant` chokepoint, so a duty cannot
pay through a side channel. Skills come from the row's `supportSkills`.

### The player is NEVER detached by a duty — do not re-add travel

This is the load-bearing property of the design, and it is pinned by
`FieldDutyRuntime_HasNoPresenceOrSpawnDependencies`, a source-scan test that reddens if
`RestorePresence`, `IServiceAttachmentService`, `SpawnLooterParty`, `DestroyParty` or
`EnlistedDetachedOnDuty` reappear in the runtime.

The previous model detached for **days**: `Start` called `RestorePresence()`, setting `IsActive`
and `IsVisible` true. An enlisted player's roster is one hero with no troops, so that produced a
fully targetable, escortless party in contested territory. A live session recorded the cost to the
second — duty started 22:02:38, player captured 22:03:19 — and the duty then outlived the
captivity and would have charged trust for a failure the player was physically prevented from
avoiding.

| Consequence | Detail |
|---|---|
| `EnlistedDetachedOnDuty` is **retired, not deleted** | The enum member and value 4 must survive: `TryParse` rejects any state failing `Enum.IsDefined`, which drops the WHOLE core record and silently un-enlists the player. `ToPersistedState` coerces it to `EnlistedAttached` **on parse** — that coercion IS the save migration |
| The inbound transition edge is gone; outbound edges stay | Nothing produces the state, but "unreachable" rests entirely on that one coercion. `EnlistmentReconciler.ReconcileRetiredDetachedDuty` is a 3-line recovery that returns the player to attached and logs loudly if it ever fires |
| A legacy duty with no `ShiftEndDay` self-heals | It would never satisfy the shift gate and would occupy the single duty slot forever, blocking every future offer. `HourlyUpdate` cancels it — no reward, no penalty |
| Duties cancel on captivity **and** on `CommanderUnavailable` | `IsEnlisted` spans five states including both. Without explicit guards a prisoner keeps ticking and is paid from a dungeon, and a duty resolves during the 7-day grace with no company to report to |
| Offers require `EnlistedAttached`, not `IsEnlisted` | Same reason: a prisoner was otherwise offered camp work |
| A duty day is now a **parked** day | So TAOM heals on it where vanilla used to. Intended — you are with the column — but it is a behaviour change from the detached model |
| Every start announces | The old model was self-announcing by accident (it made you visible and sent you travelling). This one is invisible, so `Start` shows an assignment toast — otherwise the first evidence of a duty is the result toast, after it has already moved trust |

The spawn/destroy path went with the travel model, which removes the **#375 stack-overflow surface**
entirely rather than guarding it: `DestroyPartyAction` dispatches `MobilePartyDestroyed` *before* it
deactivates the party, so the handler re-entered `Apply` and recursed. Nothing in the mod destroys a
party any more. Nine now-dead members were deleted from `IDutyWorldAdapter` (384 → 121 lines); the
four survivors are daily upkeep, used only by `EnlistmentDailyService`.

Known and accepted: a save made mid-duty under the old model may leave its spawned looter party on
the map with nothing to destroy it. They are ordinary bandit parties the engine already manages.

**Equipment.** `enlist_{runtimeCultureId}_{rank}` rosters in
`equipmentsets/taom_enlistment_equipment.xml` (68: 16 cultures × 4 ranks + 4
culture-neutral defaults), seeded from each culture's own troop tree by
`tools/generate_enlistment_rosters.py`, so kit is race-correct by construction. Drawn from
the quartermaster once per rank into party inventory (not auto-equipped). Fallback chain:
exact → lower rank → default → nothing-and-warn.
The issue-ledger is monotonic (covering a rank covers every rank below, so a demotion never
re-issues) and persists in the content record, so a full game restart cannot re-allow a
free draw.

## Interactions with the rest of TAOM

**Leader-keyed attribution is the recurring hazard.** For months of game time the player fights
inside someone else's army, so any system that asks "was the player the winning party's leader?"
silently pays them nothing. `SpecialResources` hit this before enlistment existed and moved to
`MapEvent.PlayerSide == WinningSide` participation. The #375 audit swept every remaining
`CareerPassiveHero.ResolveId` / `LeaderHero == MainHero` site and fixed three more: enlisted
sieges now advance the War of the Ring victory gate (`WarEventSnapshotAdapter.FromSiege`), and
commander-party captures credit the `DefeatEnemyLords` and `SettlementsCaptured` career-quest
objectives. **Before adding any new leader-keyed reward, ask what it does during someone else's
siege.** Consumers read enlisted state through `IEnlistmentStateQuery` (cross-feature seam;
FieldCommission and the Patch36 F6 gate use it too).

**Deliberately unpaid while parked.** A commander victory the player sits out pays nothing
anywhere — renown, special resources, momentum. That is participation-consistent and intended.

**Co-op.** Every world-mutating handler is host-only. The Codex pass found the one exception (the
wait-menu leave option) after our own review passed, which is worth remembering: a menu *option
consequence* is a world-mutating entry point even though it doesn't pattern-match like one.

## Testing

The Enlistment suite (`TAOM.Tests/Features/Enlistment/`, 668 enlistment tests; full repo suite 6052 green): transition-table
matrix, discharge invariants, Entity-State-Matrix load rows, reconciler policy (grace,
captivity, prisoner-commander-with-live-party), record round-trip incl. NaN/forward-compat,
menu redirect policy + cap, battle ordering/rollback/loot-guard, binding pins, config
semantic validation (one test per rule), wage orchestration (solvency × arrears × channel,
incl. regression tests for the mint-mode double-payment and shortfall-overcount bugs),
promotion thresholds at both evaluation points, merit scoring/bands incl. NaN ratios, duty
gating/rotation/lifecycle per mechanic, equipment resolver fallback + payoff math, the persisted
issue-ledger across a save round-trip, role-fit bands per assignment, and one regression test per
Codex finding (`CodexFindingRegressionTests`).

**Verified in live play (2026-08-07):** field-battle join with the commander solo (instant, via
`MapEventStarted`); siege-assault join; hourly-recovery join when the immediate edge is missed;
return to parked service after a battle; enlistment config loading without silently reverting;
the conversation encounter being closed at swear-in. Log lines that prove each:
`joined commander battle on side …`, `closed the enlistment conversation's PlayerEncounter`,
`service wait menu re-opened after …`.

**Still owed (in-game gates that have NOT run):**
A field battle with the commander **in an army** — the one case that could still fail, because
`FindCommanderPartyIdIn` matches only the commander's own party in `InvolvedParties`, and an
attached army member may not appear there. **Leaving service and immediately clicking a lord** —
the save-breaker fix is written but never exercised; the line that would indict it is
`DISCHARGE(…) LEFT THE PLAYER UNABLE TO START ENCOUNTERS`. **Save-load mid-service, then a
battle.** Also: SetNextMenu timing vs EncounterGameMenuBehavior; camera
handoff; captivity entry/exit mid-service; food/wage/morale ticks for the inactive
MainParty; save-load inside the wait menu; TimeAcceleration interplay; the duty
spawn→hunt→complete loop and its target-party cleanup; equipment visuals per race
(erebor, goblin, the four orc cultures); promotion/merit/incident popups; and the
FieldCommission offer flow with enlisted suppression.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->


**Verified in live play (2026-08-08):** settlement following — the player is INSIDE the
commander's settlement, not parked outside the gate; the live status board — the wait text changed
to *"The column rests inside Minas Tirith."*, naming the settlement, which is the proof line for
batch 9 (the old build showed one identical sentence from oath to discharge); rank and section
rendering through `ServiceVocabulary` rather than as raw enum names; **"Speak with your
commander"**, and through it the **quartermaster** issuing service gear — a shipped, tested
behaviour that had never once fired for a player, because nothing in the feature could open a
conversation. No token leakage (`{COMMANDER}`, `{SETTLEMENT}`, `{NEWLINE}` all resolved).

One defect found by that session and fixed: the wait menu listed *Ask to be released from service*
SECOND, directly above the two options a serving player uses constantly, carrying the back-arrow
icon that reads as "back" rather than "end my career". It is now last, as vanilla does it.

**Still NOT verified — and the review pass is why this list matters.** The rest of the 2026-08-08 batch
(the MCM switch, and every fix in the review section above) has run in a live game. The four terminal defects that pass found were all invisible
to 668 green tests; the in-game list is
[`docs/reviews/enlistment-morning-handoff-2026-08-08.md`](../reviews/enlistment-morning-handoff-2026-08-08.md).

Specifically owed, because each is a state a test structurally cannot reach:

1. Field battle with the commander **in an army** (the original report).
2. Commander riding into an **already-running** fight (`OnPartyAddedToMapEventEvent` edge).
3. A **settlement stop** — you inside it, leaving with the column.
4. **Discharge while inside a settlement the commander is not in** (defect 1 above).
5. **Save mid-battle, reload** (defect 2 above).
6. A **duty assigned while inside the commander's town** (defect 3 above).
7. The **MCM switch flipped off mid-service**.

### Still owed beyond testing

- **Batch 11 (content beats)** — not started.
- **12-language translation — DONE (2026-08-08).** `taom_enlistment_strings.xml` holds **178** keys
  and every one of the 12 `Languages/<L>/std_taom_enlistment_strings_<loc>.xml` files holds 178,
  verified by count this session. Nothing here ships English-only.

  The **97 runtime-built duty keys** are the reason
  `tools/generate_enlistment_duty_strings.py` exists: `InteractiveDutyPresenter` and
  `ServiceStatusTextWriter` assemble ids as `taom_enlist_duty_<row id>_<suffix>` at runtime, so a
  literal `{=key}` grep — the discovery mechanism `/localize` relies on — finds none of them. The
  generator enumerates them from the data rows instead. 84 are the 14 interactive-duty/incident
  rows × 6 suffixes; **13 are the field-duty `_title`s, added 2026-08-08** after the first pass
  derived its key set from `interactiveDuties` + `incidents` only and left all 13 field duties
  rendering as their raw snake_case id ("You have orders: recon_sweep") in every language.

  Two guards now stop that recurring: the generator **hard-fails** on a field-duty row with no
  authored title, and `ServiceStatusTextWriter`'s fallback is prose rather than the row id, so a
  future miss degrades quietly for the player instead of printing an internal symbol.
- **One entry point remains over the ADR-002 ceiling**, pre-dating this arc:
  `EnlistmentBattleBehavior` (157). `EnlistmentMeritMissionBehavior` was the other (163); the
  left-the-field fix paid that down to 139 by extracting its inline geometry into
  `MeritGeometryAccumulator` + `MeritGeometryScanner`.

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
