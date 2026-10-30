# Enlistment — serve as a soldier in a lord's party

> **STATUS: SHIPPED AND FIELD-VERIFIED** (#375 closed 2026-08-09). A live session that day exercised
> oath, parking, ten battle joins on both sides, two field duties, a promotion, a camp incident,
> battle merit, rations and the morale floor, and a commander-loss grace — **one `[ERROR]` line in
> 3,452, and that one was a mislabelled diagnostic** since downgraded. 225 localization keys, all 12
> languages id-identical to English.
>
> **Not exercised in game:** discharge (any reason), player captivity, the commander-loss modal, the
> contract waiver, and the formation placement. Those are the remaining gates, tracked on their own
> issues rather than here.
>
> Reviews: three `/deep-review` cycles and three independent Codex passes. RCAs:
> `rca-enlistment-core-2026-08-04.md`, `rca-enlistment-content-2026-08-05.md`,
> `rca-enlistment-survivors-2026-08-08.md`, `rca-duty-autoresolve-2026-08-09.md`.

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
{EnlistedBattle, EnlistedPlayerCaptive, CommanderUnavailable} → Discharging → NotEnlisted`, plus
`EnlistedDetachedOnDuty`* carrying **outbound edges only**. Full legal-edge set:
`EnlistmentTransitionTable` (**19** edges, pinned by an exhaustive 64-pair matrix test — the enum
still has 8 members, so the matrix is still 64 pairs).

*\* RETIRED 2026-08-09 (#428): nothing produces it and the inbound edge is deleted. The enum member
and its numeric value survive because `TryParse` rejects any state failing `Enum.IsDefined`, which
would drop the whole record and silently un-enlist the player; it coerces to `EnlistedAttached` on
parse, and that coercion IS the save migration. The four outbound edges stay so a legacy save can
still leave the state.*

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
- **Only discharge ends service.** Several paths decide that it ends (the hourly
  `EnlistmentReconciler`, `EnlistmentLoadNormalizer` at load, the player through
  `EnlistmentService.RequestDischarge`); every one of them calls `DischargeService.Execute`, the
  only code that ends it. Two presence states are
  legitimate while enlisted: parked (hidden and inactive), and active and visible inside the
  commander's settlement. Neither is a fault to repair.
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

The content record (`_taom_enlistment_content`) reads by key name and defaults a missing key, so
saves written before the #844 cleanup still load. Their retired keys (`repF`, `repL`, `repC`,
`repS`, `wins`, `losses`, `tourneys`, `dutyFood`, `dutyFails`) are ignored on read and not written back
(`ContentRecord_SaveWithRetiredKeys_StillLoads`). The same goes for `trainingSessionXp`,
`reputationMax`, `repDomain` and `repAmount` in the JSON configs: a leftover key loads quietly.

## Menu + battle layers

**Fixed (#828, closes #573; found 2026-09-12 by the Codex pass on #567):** the wait menu's
condition delegate used to be `IsEnlisted`, and a wait menu's condition hides EVERY option when
false (`GameMenu.GetMenuOptionConditionsHold`; the camp-menu incident of #567). A discharge that
runs during load (Enable Enlistment turned off before loading, or the identity guard) fires from
`OnGameLoaded`, before `MapState` exists, so `DischargeService.ExitServiceMenuIfOpen` sees no
menu context and the persisted service-menu id re-entered with zero options. The Patch66 redirects
do not repair it: `TryRedirectMenu` returns false for the service id and for any non-attached state.

The menu condition is now `args => true` and each option gates itself on
`IEnlistmentPlayerActionService.IsServing`. While nobody serves, the body reads "Your service has
ended." (`ServiceStatusTextWriter.WriteNotServing`) and one option, "Go on your way"
(`taom_enlist_menu_discharged_leave`, the only isLeave option on this menu, never disabled), calls
`IEnlistmentPlayerActionService.LeaveAfterDischarge`. EndWait has already run before an isLeave
consequence. The branches: still serving, re-enter the service menu, which restarts the wait;
inside a settlement and not in a map event, establish the settlement encounter and open the town
menu; if the encounter cannot be made, walk out of the settlement and exit; if
the encounter was made but the town menu will not open, finish the encounter, walk out and exit;
if the walk-out fails, the service menu stays so an exit remains on screen; in a map event, exit
and let vanilla push `encounter`. Everything else exits to the last menu, never leaving the player
in a town with no menu (the #510 shape). `EnlistmentServiceMenuWiringTests` pins the source facts.

**Why the persisted menu id is NOT cleared on discharge (decision A):** an empty id skips
`PlayerEncounter.OnLoad()` in `SandBoxGameManager.OnLoadFinished`, and a surviving settlement
encounter would then leave the player in a town with no menu. Keeping the id and giving the menu
an exit is the safer side. The Patch66 `EnterMenuMode` postfix is untouched.

- Wait menu `taom_enlistment_service_wait` (`EnlistmentMenuBehavior` +
  `EnlistmentWaitMenuPresenter`; text built once per menu init; the wait-menu tick feeds the
  pump (whose cheap tier syncs the parked position) only while campaign time is stopped; the campaign tick
  feeds it otherwise). Menu-guard policy in `EnlistmentMenuService`: redirect ONLY while
  `EnlistedAttached`, from a config-driven id list (11 defaults incl. the 1.4.x naval
  `port_menu`/`naval_town_outside`), fail-open with a once-per-id diagnostic funnel.
- Battle: `EnlistmentBattleBehavior` (MapEventStarted/Ended boundary) →
  `ServiceBattleService`. Ordering contract: state flips to `EnlistedBattle`
  (redirect-exempt) BEFORE any encounter/menu push, then presence → position →
  encounter → join → menu. A join that never landed rolls back to parked-attached, finishing any
  encounter it created first. A landed join whose `encounter` menu failed to open stays in the battle
  (state `EnlistedBattle`, service menu popped) and the game opens `encounter` itself (#1021). Post-battle, the state stays `EnlistedBattle` while the loot
  encounter is open (so the guard can't eat aftermath menus); wait-menu init + reconciler
  close the loop.
- **Two join paths, both live.** The `MapEventStarted` edge, plus an hourly recovery:
  `IEnlistmentReconciler.BattleJoinRequested` → `ServiceBattleService.TryJoinCommanderBattle`,
  raised when the commander is in a map event and the player is not. The recovery path covers
  a missed edge (save-load mid-battle, a throw, enlisting into a running fight).
- **The player has NO battlefield command while enlisted, at any rank** (#424, #576). One pure
  predicate, `BattleCommandPolicy.ShouldStripPlayerCommand`, whose whole body is
  `state == EnlistedBattle && !playerLeadsBattleSide`, and three consumers that must never gate
  apart: `TaomBattleInitializationModel` keeps the Order of Battle deployment screen shut (so there
  is no captain slot to take), `EnlistmentBattleRoleMissionBehavior` calls
  `Team.SetPlayerRole(false, false)` at `AfterStart` (so the order UI stays closed and every
  formation is AI-controlled), and `EnlistmentBattleFormationMissionBehavior` stands him in his
  assignment's formation and puts him back there after vanilla's captain assignment moves him.
  There is no duty branch, because since #428 a duty never detaches the player and so can never
  produce a battle of their own, and there is no rank branch, because the rank-3 Sergeant
  carve-out shipped 2026-08-12 is what opened the screen to every rank (see "No rank holds a
  command" below).

### Standing in the line — and why the soldier is still standing alone (#441, #442, #443)

`EnlistmentBattleFormationMissionBehavior` assigns the enlisted player's agent, at `OnAgentBuild`, to
the formation matching their `ServiceAssignment` (`BattleFormationPolicy`: Infantry → Infantry,
Archer → Ranged, Cavalry → Cavalry, **Support deliberately unmapped** — the rear-echelon fantasy has
no line to stand in), and repositions them onto it when that formation already has non-player units
and a valid order position. It shares the #424 gate via `BattleCommandPolicy.ShouldStripPlayerCommand`
so the two corrections cannot gate apart.

**Two corrections to how this was originally described, both verified against 1.4.7:**

1. **It relocates `IsPlayerTroopInFormation`; it does not deliver it.** `Agent.Build` already assigns
   `Formation = agentBuildData?.AgentFormation` (`Agent.cs:5173`), and `Mission.SpawnAgent` calls
   `BuildAgent` **before** the `OnAgentBuild` dispatch loop (`Mission.cs:4348` then `:4360`). The
   player was always in a formation and the flag was always true. What this actually buys is the
   formation matching the assignment the player *chose* rather than their equipment. (The mechanism
   itself checks out: the `Agent.Formation` setter routes through `Formation.AddUnit` at
   `Agent.cs:1143`, and `AddUnit` sets the flag at `Formation.cs:2117-2119`.)
2. **`BehaviorComponent:105` has FOUR conjuncts, not two** — `!IsPlayerGeneral && !IsPlayerSergeant &&
   IsPlayerTroopInFormation && Mission.Current.MainAgent != null` — and the branch is a two-second
   `AddQuickInformation` toast, not an orders pipeline.

**The formation had nobody else in it (#443, closed 2026-08-20 by the transient army join).**
`Mission.GetAgentTeam` (`Mission.cs:5183-5189`) routes a party to `PlayerTeam` when
`IsUnderPlayersCommand || IsInSameArmyAsPlayer`, else to `PlayerAllyTeam`. The player's own party
takes the first arm; the commander's party took neither while enlistment kept `MainParty.Army`
null, so the soldier joined a formation on his own one-man team and was manoeuvred by that team's
own `TeamAIGeneral`. The #424 field test ("F1 to F8 dead and the AI fought the line") was consistent
with that because the line being fought was the **ally** team's. The fix chosen was the second of
#443's two options: `ArmyMembershipAdapter.JoinCommanderArmy` gives the player a non-null `Army`
for the duration of the battle (see "The field-test arc" below), which collapses both parties onto
`PlayerTeam`. `Army` is still cleared in `ParkNear` and `RestorePresence`, so it is null everywhere
except inside a battle. The merge-failed path (a kingdomless commander, #495) still lands on the
one-man team.

**Hardening applied post-merge** (`ab5d3cfe`): a try/catch, because `Mission.SpawnAgent` dispatches
`OnAgentBuild` in a bare `foreach` (`Mission.cs:4357-4360`) and a throw there aborts the whole spawn
wave and skips every later TAOM behavior for that agent; a finiteness gate on the teleport target,
since `OrderPositionIsValid` checks only the 2D position and the scene pointer while the Z comes from
`GetGroundZ()`, which returns NaN when it cannot validate; `CountOfDetachableNonPlayerUnits` instead
of `CountOfUnits`, which counts the player himself so a formation of one read as a line to join; and
removal of a dead `?? Mission.PlayerTeam` fallback that would have placed the agent on a team he is
not on.

### Battle-role facts (re-derived 2026-09-12 on installed 1.4.8)

This table was first written when `Army` was null in every battle and carried a "do not re-derive"
banner. The transient army join (2026-08-12) changed the premise of three rows and the banner kept
the stale conclusions alive for a month, which is how #576 shipped. Re-derived, no banner.

| Fact | Why |
|---|---|
| What vanilla hands the enlisted player depends on the merge | With the merge (`Army != null`) `IsPlayerSergeant()` is true and `SandBoxMissions` wires `(isPlayerGeneral: false, isPlayerSergeant: true)`. Without it (#495) `IsPlayerSergeant()` is false, `SandBoxMissions` passes `!isPlayerSergeant` positionally as `isPlayerGeneral`, and he is the **general** of his side |
| Neither-general-nor-sergeant is a **supported vanilla state**, not untested ground | `BehaviorComponent.cs:103-110` branches on exactly `!IsPlayerGeneral && !IsPlayerSergeant && IsPlayerTroopInFormation`, the soldier-receiving-orders path. `Team.SetPlayerRole` sets `SetControlledByAI(true)` on every formation for it, and `MissionOrderVM.CheckCanBeOpened` refuses the order UI on it |
| One strip at `AfterStart` is final | `SetPlayerRole` has exactly TWO engine call sites, `Mission.cs:741-746` (team creation) and `AssignPlayerRoleInTeamMissionController.cs:43` (`AfterStart`), both before TAOM's `AfterStart`. Nothing re-derives the flags around deployment; an `OnDeploymentFinished` belt that claimed otherwise was dead code and is gone |
| TAOM's `AfterStart` runs **after** vanilla's role assignment | `AddMissionBehavior` appends; the mission's own controllers enter via `InitializeStartingBehaviors` at construction; TAOM appends during `OnMissionBehaviorInitialize`; `Mission.AfterStart` then iterates the list in order |
| **The Order of Battle screen IS reachable through `IsPlayerSergeant()` once `Army` is non-null, and the role strip cannot close it** | `SandboxBattleInitializationModel.CanPlayerSideDeployWithOrderOfBattleAux()` (`:65-83`) offers deployment when the player leads the side, owns the besieged settlement, or `IsPlayerSergeant()`, with 20 or more controllable troops. It reads campaign state only, and the non-virtual `CanPlayerSideDeployWithOrderOfBattle()` caches the answer once per mission, first read at `DeploymentMissionController.SetupTeams:181` on the first tick, after every `AfterStart`. The doc's earlier "unreachable, and always was" row was wrong from 2026-08-12 to 2026-09-12. `TaomBattleInitializationModel` closes it (#576) |

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
| **Never anchor world spawns on the commander's CURRENT settlement** *(HISTORICAL — the duty spawn path was deleted with the travel model, #428)* | The durable half, still true: `CommanderSnapshot.SettlementId` is empty whenever the column is marching — nearly always — and the live status board reads it. The rest describes a design that no longer exists: hunt duties could only start while the commander sat in a town, and every `recon_sweep` failed with `SpawnLooterParty: settlement=''`. Nothing in the mod spawns or destroys a party any more; `SpawnLooterParty` survives only as a banned symbol in `FieldDutyRuntimeTests`. |
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

**`TaomSettings.EnableEnlistmentTrace`** (MCM group `Enlistment/Diagnostics`, label "Enable Enlistment
Trace", `RequireRestart = false`) reaches the call sites through `IEnlistmentDiagnosticsSettingsProvider`, whose singleton reads
the static on every call — which is what makes "takes effect immediately" true rather than aspirational.

**It ships OFF (again, #840).** It was off from 2026-08-09, went back on temporarily on 2026-08-28 for an
in-game smoke and was never switched off, so every release from v2.0.24 (2026-09-01) through v2.0.35
(2026-10-19) shipped it on. The setting was then RENAMED from `EnableEnlistmentDiagnostics` to
`EnableEnlistmentTrace` rather than just flipped: MCM keeps a saved value per property name, and any player who
confirmed a change of any TAOM setting while the default was on has `true` saved under the old name, so a
flipped default would not have reached them. The orphaned old key is ignored on load and dropped by the next save. To record the trace for
a bug report, switch it on in MCM (Enlistment, Diagnostics); real faults always log regardless, so
`[EnlistDiag]` lines still appear for them with the trace off. The provider's MCM-absent fallback is `?? false` to match.
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
| **Never leave an orphaned `PlayerEncounter`** | `EncounterManager` gates the main party on `PlayerEncounter.Current == null`. A leaked encounter stops the player entering any future one. Worse: a `MapEventSide` acquired without a live encounter + open `encounter` menu freezes that map event forever (`MapEventManager.Tick` skips the player's event; only `PlayerEncounter.Update` advances it), which leaves the commander permanently unable to start battles. The rollback cannot pull an unmerged soldier out of his event, so when a landed join's menu fails the menu is left to the game (`ServiceBattleService.TryJoin`, #1021). |

## Patch66_Enlistment (see harmony-patch-registry.md)

`GameMenuManager.SetNextMenu` prefix (menu-id rewrite — the load-bearing guard; no event
alternative exists), `MapState.EnterMenuMode` postfix (recovery for ids written without
SetNextMenu), 4× `LordConversationsCampaignBehavior` condition prefixes (suppress
join-army/ally-thanks lines while enlisted; the clickable variant carries `out TextObject
hint`; ally-thanks pair is PUBLIC on 1.4.7), `MapEventHelper.CanMainPartyLeaveBattleCommonCondition`
postfix (#827: encounter menu only; hides vanilla 'Leave' and 'Abandon army' while the soldier serves in his
commander's battle, gated by `IEnlistmentMenuService.ShouldBlockBattleExit`, and released under a safe-passage
truce so the menu never loses its last option), and the private
`EncounterGameMenuBehavior.game_menu_encounter_order_attack_on_condition` postfix (#830:
`EncounterGameMenuOrderAttackConditionPatch`, narrows 'Send troops' / 'Leave it to the others'; see
"Leave it to the others" below). All fail open. All eight targets pinned by
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
| 6 | Informed leave choice | Asking to leave now states its price with a real day count. Desertion has exactly TWO producers (the in-person dialog and the wait-menu confirmation), each a branch the player picked after being told the cost. |
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

**Settlement following places the player inside but holds them in the TAOM wait menu.** The
redirect list is what holds him there. Note carefully what that means: he is inside the settlement
with NO `PlayerEncounter`, because the follow path uses `EnterSettlementAction.ApplyForParty`
alone. The redirect is a mask over an invalid state, not a fix for it, and the crash is one
un-masking away. See "The settlement-encounter invariant" below, which is what shore leave and the
discharge release both needed and neither originally had.

*This paragraph used to end here, claiming letting the player use the town was future work needing
its own `EnlistedOnLeave` state. Shore leave (2026-08-12) then shipped that feature without the
`PlayerEncounter` work the paragraph named, which is how issue #510 happened. The requirement was
real; only the "separate state" framing was wrong.*

**The release refusal is keyed to `MinimumServiceDays` (21), deliberately NOT `ContractDays`
(365).** Keying it to the contract would refuse every realistic request and leave desertion as the
only exit — Batch 1's bug wearing a different hat.

**Four of the donor's six re-attach edges are DECLINED, on purpose.** Subscribed:
`OnSettlementLeftEvent`, `OnPartyLeftArmyEvent`. Declined: player-battle-end, army-joined,
siege-joined, siege-left, siege-completed. The pump re-derives everything every 18 campaign minutes (about 250 ms at the menu's default fast forward), and the
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
| Service wait menu + its six options | `Presentation/EnlistmentWaitMenuOptions.cs` | **Every service option is `isLeave: false`, and that is load-bearing; the discharged exit ("Go on your way", #828) is the one `isLeave` option and shows only while nobody serves.** `GameMenu.RunMenuOptionConsequence` calls `EndWait()` BEFORE the consequence for an `isLeave` option on a wait menu, which sets `IsWaitActive = false` and `TimeControlMode = Stop`, tearing down the tick that drives the position sync before the consequence runs. It also makes the option a candidate for the Escape slot. |
| Live status board | `ServiceStatusService` + `Presentation/ServiceStatusTextWriter.cs` | Rebuilt on the pump's 2 s budget, pushed only when the model differs, or when the board was rendered in another language than the active one (#1019). **The value equality IS the throttle**, so a field omitted from `Equals` is a status line that never updates, one `DataRow` per field guards it. The board is rendered to a plain string once per push (`SetTextVariable(name, text.ToString())`), not handed to the menu as a nested `TextObject`. |
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

**What desertion costs (#833).** Two producers reach `DischargeReason.Desertion`: the in-person
dialog (`EnlistmentReleaseDialogBehavior.OnDesertionConfirmed`) and the wait-menu prompt
(`EnlistmentWaitMenuPresenter.ConfirmDesertion`), both only after the commander refused release inside
the minimum term. The cost is relation -15 with the commander and Honor trait XP -100: no war, no
crime, no renown. The values are `desertionRelationPenalty` and `desertionHonorXpPenalty` in
`enlistment_config.json` (compiled defaults equal the shipped file; integers, relation in [-100, 0]
and Honor XP in [-1000, 0], a value outside that range reverts to the default with a warning; 0 skips
that part of the cost while the confirmation text still names it). `DischargeService.Execute` applies them at
step 7c through `DesertionPenaltyService`, with the commander id captured at step 3 (the record reset
wipes it, so `ServiceRewardService.Grant` could not carry it). Host only: both producers check the
co-op authority before requesting the discharge. A throwing penalty is logged and the discharge still
completes. Skipped entirely, both
parts, if the commander is dead: the dishonour is toward him. Deferred wages are still forfeited and
the deserter keeps his kit, as before; an honourable release (after the minimum term) is free. Both
confirmations name the cost through two new keys that hold only the cost sentence and are composed onto
the unchanged, already translated rows: `taom_enlist_desert_cost` (appended to the wait-menu body
through the inquiry adapter's `bodyAppendKey`) and `taom_enlist_desert_cost_reply` (appended to the
in-person player line, composed with a `{LINE} {COST}` text variable). Both are gender-neutral
("your commander's favor"). Since #837 both also name what the history now
costs through a third key, `taom_enlist_desert_memory` ("Your commander will not take your oath again
for a time, and this term will count for nothing if you return."), appended after the cost sentence (the inquiry
adapter's `secondAppendKey`, and a `{MEMORY}` variable on the in-person line, which uses the
first-person key `taom_enlist_desert_memory_reply`). The relation change is silent (no notification). The Honor change: the
engine sets a player's Honor XP exactly at his level's threshold at character creation and on a
player-character change, so a player at Honor +1 or +2 who has gained no Honor since then drops one
level on his first desertion (a map notice); from level 0 it takes ten desertions to reach -1. XP is
visible with cheats via `campaign.print_player_traits`. Tests: `DesertionPenaltyServiceTests`,
`DischargeServiceTests` (desertion only, captured id, dead commander),
`EnlistmentContentConfigProviderTests` (defaults, shipped parity, invalid values) and
`DesertionCostTextTests`.

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
from that menu. The wait menu shows no `isLeave` option while anyone serves, so it never closes on its own either.

**Fix:** `encounter` / `join_encounter` are exempt from redirect whenever the player is genuinely in
a map event. The coercion stays (a transient state has no business in a save). The re-derivation
it was always supposed to rest on was finally written on 2026-09-12 (#577):
`EnlistmentLoadNormalizer` restores `EnlistedBattle` at load when the record reads
`EnlistedAttached` but the party is still in a map event, and leaves an encounter with no map event
behind it alone. A settlement encounter is the reconciler's; a leftover whose encountered party is a
mobile opposing leader outside a settlement is skipped by the sweep (R4 returns `SkipNotOurs`), and since
#1021 the stale-latch recovery closes it: it treats an attached soldier with a joined-battle encounter
(`IEncounterAdapter.IsJoinedBattleEncounter`) outside a settlement as a timed episode too.
Without it every gate keyed on `EnlistedBattle` read the wrong state for the battle the player
saved in: the deployment-screen model and the role strip deferred to vanilla, and the presence
hold did not fire.

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
- **The pump's two feeders used different clocks and double-fed a running wait menu (#841).** The campaign
  tick (`CampaignEvents.TickEvent`) passes the campaign delta (0.25 of real time at play speed, times the
  fast-forward multiplier), and the wait-menu tick passed real seconds; on a wait menu with time running both
  fire in the same frame, so the budget filled twice and the cheap position sync ran twice per frame. The
  wait-menu tick now pumps only while campaign time is stopped (the delta the engine passes it is zero,
  with its real-time clock kept for that case; no clamp is needed, because both budgets reset to zero when
  a tier fires, so a post-stall step fires each tier at most once). The cadence is in budget seconds. While campaign time runs, the budget is the campaign tick's delta,
  so the expensive tier (0.25 budget seconds) runs every 18 campaign minutes and the status board (2
  budget seconds) every 2.4 campaign hours at any speed: on the service menu's own fast forward at the
  default multiplier of 4 that is about every 250 ms and every 2 real seconds, at extra fast forward
  (8) every 125 ms and every second, and at play speed, which the player can choose on the menu, every
  second and every 8 seconds. On a stopped wait menu (paused, or just after loading a save) the budget
  is real seconds: about every 250 ms and every 2 seconds. The board rebuild exceeds 4 Hz from a fast-forward multiplier above 32 (equal at 32), which is accepted;
  before this change the running menu ran both tiers twice as often, so the change halves the rate only at
  the default x4. Since #1019 each of those pushes renders the board once to a flat string
  (`ServiceStatusTextWriter` passes `text.ToString()` to `MBTextManager.SetTextVariable`, whose string overload wraps
  it in a plain `TextObject`), so the per-frame re-resolve of the menu text, which `GameMenuVM.IsMenuTextChanged`
  forces for a menu text with no attributes, costs one flat pass instead of a nested render of the board lines.
  The price is the language: the flat string no longer follows a language switch, so `IServiceStatusTextWriter.IsStale`
  (the rendered language differs from `MBTextManager.ActiveTextLanguage`) makes `RefreshIfChanged` write again, within
  one status tick of the switch. The wait-menu frame is also the one place a deferred raid join is performed (see "#954 Open
  risk 1"): `ServiceMaintenanceService.OnWaitMenuFrame` flushes it first, then pumps only when the campaign
  delta is exactly zero; the hook makes that one call and restarts its Stopwatch every frame.
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
**14**) days of the player's *current* rank wage: 70 gold at Private, 560 at Sergeant Major.
`ServiceRewardService.PayDailyWage` spends that plan through exactly ONE channel (commander
transfer or mint, per config) and derives the new debt by conservation (owed minus delivered)
rather than patching the plan's fields. Today's wage is paid first: only what the commander
delivers beyond it counts against the debt already banked, so a debt above the cap (after a mid-term
change of the wage table or the cap) stays whole while the commander pays each day's wage. Anything
owed above the cap is forfeited, and the
service logs `deferred-wage cap reached — N gold of back pay forfeited` when that happens; it
used to be destroyed silently. An honorable discharge settles remaining arrears; desertion
forfeits them.

> **Config migration.** The key was `wagePolicy.maxDeferredWages`, a flat **60-gold** ceiling —
> a Staff Sergeant (then simply Sergeant) on 22/day reached it in 2.7 days and lost roughly 600 gold over a 30-day insolvent
> stretch with nothing in the log. An existing install's `enlistment_config.json` still carrying
> the old key gets a load-time warning; the old value is ignored (the two units are not
> convertible), so re-set `maxDeferredWageDays` if you had retuned it. Valid range `[0, 365]`.
>
> An install that keeps its own pre-#936 `enlistment_config.json` or `enlistment_duties.json` (rank
> names Recruit, Soldier, Veteran) logs an error with a rename hint and loads the compiled defaults
> (an empty duty list for the duties file); take the shipped file.

**Ranks + promotion (#936).** Seven ranks, the US Army enlisted ladder. Thresholds are in JSON
(days / service XP / Leadership / duty successes / trust), evaluated at exactly two points, the
daily tick and the battle payout, both through `IPromotionService`. The donor evaluated at twelve
sites including mid-mission per kill. Sections further down were written against the earlier
four-rank ladder (Recruit, Soldier, Veteran, Sergeant) and keep those names where they quote logs
or describe what a player saw then; the mapping is below.

| Rank (ordinal) | Was | Days | Service XP | Leadership | Duties | Trust | Daily wage | Service XP per day |
|---|---|---|---|---|---|---|---|---|
| Private (0) | Recruit | start | | | | | 5 | 10 |
| Specialist (1) | Soldier | 7 | 100 | 0 | 0 | -10 | 8 | 12 |
| Sergeant (2) | Veteran | 25 | 350 | 0 | 2 | 0 | 14 | 16 |
| Staff Sergeant (3) | Sergeant | 60 | 800 | 5 | 5 | 6 | 22 | 20 |
| Sergeant First Class (4) | new | 120 | 1800 | 10 | 8 | 10 | 28 | 22 |
| Master Sergeant (5) | new | 200 | 3200 | 10 | 12 | 14 | 34 | 24 |
| Sergeant Major (6) | new | 300 | 5000 | 15 | 16 | 18 | 40 | 26 |

The Days through Trust columns are the thresholds to reach that rank; the wage and service XP
columns are what that rank earns. The validators count against the enum (six promotion rows, seven
wage and service-XP entries), so a future rank needs no validator edit.

**Saves (D1).** `ServiceRank` is saved as an int in the content record (`rank=`), and `TryParse`
rejects an undefined ordinal. Saved ranks 0 to 3 keep their position, pay and kit and only their
names change (their thresholds too, except the Leadership gates into Sergeant and Staff Sergeant,
which D2 lowered), so a saved Sergeant loads as a Staff Sergeant on the same 22 a day with the
same kit. Ranks 4 to 6 are new. There is no migration code and no new save key. `enlistment_duties.json`
parses `minRank` by enum NAME, so every row was rewritten by ordinal (Soldier to Specialist, Veteran
to Sergeant, Sergeant to StaffSergeant); `strategic_planning` is the silent trap, because "Sergeant"
still parses but now means ordinal 2, one rank below the row's tuning. `ServiceRankLadderTests` pins every shipped
duty's `minRank` ordinal.

**Kits (D3).** The roster data has four tiers, so `EnlistmentKitTiers.For(rank)` maps ranks 0 to 3
to their own tier and ranks 4 to 6 to the best tier (`EnlistmentKitTiers.Best`, which is `EnlistmentKitTier.Sergeant`). The issue ledger
stays keyed by tier. At the best tier a second request answers "You already carry the best kit my
stores can issue." (`QuartermasterAnswer.AlreadyDrawnBest`) instead of promising a stripe that does
not exist.

**Duty rank bonus (D4).** `SkillCheckService.RankBonus(rank)` returns `min(rank * 4, 12)` and both
duty call sites use it. The duty difficulties were tuned against a ceiling of +12, reached at Staff
Sergeant, so the three new ranks add nothing to a check.

**Leadership spread (D2).** The old ladder asked for Leadership 50 at the last step alone. The engine
curve (`DefaultCharacterDevelopmentModel.InitializeXpRequiredForSkillLevel`, installed v1.5.5: the
first requirement is 30 and each step adds `10 + i` to the increment, so level 50 costs 34,575 XP)
set against the daily grant (`EnlistmentDailyService` adds `progression.dailyLeadershipXp`, 10, to
Leadership each day) makes that about 3,460 days. Each step now asks for the highest level reachable
from zero by its own day threshold at 10 XP a day, rounded down to a multiple of 5:

| Step | Days | XP at 10/day | Level reachable | Gate |
|---|---|---|---|---|
| Specialist | 7 | 70 | 1 | 0 |
| Sergeant | 25 | 250 | 4 | 0 (was 20) |
| Staff Sergeant | 60 | 600 | 7 | 5 (was 50) |
| Sergeant First Class | 120 | 1200 | 11 | 10 |
| Master Sergeant | 200 | 2000 | 14 | 10 |
| Sergeant Major | 300 | 3000 | 17 | 15 |

The real rate is not the nominal one. `HeroDeveloper.AddSkillXp` multiplies each grant by the
hero's learning rate at the current skill: `1.25 * (1 + 0.4 * Social + focus)`, minus
`1 + 0.1 * (skill - limit)` inside the bracket once the skill passes the learning limit
`max(0, (Social - 1) * 10) + 30 * focus`, floored at 0
(`DefaultCharacterDevelopmentModel.CalculateLearningRate` and `CalculateLearningLimit`). A new
character has Social 2 and no Leadership focus, so the 10 XP grant is worth 22.5 XP a day up to
Leadership 10, then less, and it stops at 18. Simulated from zero, that hero reaches Leadership 5 on
day 12, 10 on day 41 and 15 on day 198, so every gate opens before its day threshold. The extra army
context XP and `GameAccelerationMode.Fast` (the curve times 0.3) only make a gate easier. Grace days
count toward days served but grant no Leadership. `ShippedLeadershipGates_AreReachedByEachStepsDayThreshold`
(in `EnlistmentContentConfigProviderTests`) reproduces the curve and the rate, reads the shipped
`enlistment_config.json`, and fails if a retune makes a gate unreachable by its day threshold.
Part of the earlier gating on this ladder is addressed here (#521).

Seasoned Hosts' "Learning Never Stops" (#890, default on) keeps the learning rate at 0.05 or more instead of letting it fall
to 0, so a hero past the learning limit keeps training slowly where this section says the grant stops. Skill gates only
get easier with it; the simulation above is the setting-off case. See [seasoned-hosts.md](seasoned-hosts.md).

Rank still grants no battlefield command (#576).

**Known limitation.** A Social 1 hero with no Leadership focus has a learning limit of 0 and stops
at Leadership 4, one short of Staff Sergeant's gate of 5. A Leadership focus point fixes it (that
hero then reaches 5 on day 9). The gate is a real Leadership ask, not a formality.
With "Learning Never Stops" on, that hero is slowed past the limit rather than stopped, so the gate is reachable, slowly.

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
`IArmyRhythmSnapshotService` probes the commander's column fresh on every call (the daily service
tick, the daily duty offer and a duty request); it has no cache. The probe counts the column as
under siege when `MobileParty.SiegeEvent` is set (the besieger's camp event) **or** when the
commander's current settlement is under siege (`Settlement.SiegeEvent`), so a soldier whose
commander defends a besieged town gets the siege duties and the siege skill context, not the quiet-day
`night_patrol`.

A field duty is **camp work, not a journey** (reworked 2026-08-09, #428): you are given orders, they
occupy you for `durationHours` (4–8), and one `ISkillCheckService` roll against the row's
`difficulty` (40 to 76) decides the outcome. Success pays `reportReward`, failure pays
`failureReward` — both through the one `IServiceRewardService.Grant` chokepoint, so a duty cannot
pay through a side channel. Skills come from the row's `supportSkills`, best of the two, and no
failure costs standing (see "Duty gates must admit someone who can pass" below).

### When the commander stops having a command

Three outcomes wear one state. `CommanderUnavailable` is entered whenever `Assess` returns
`Blocked(CommanderPartyMissing)`, which covers a lord who was captured and a lord whose party was
simply destroyed; a dead lord discharges immediately instead.

Until 2026-08-09 the transition was **completely silent** — zero `ShowMessage`/`_inquiry` calls in
either `EnlistmentReconciler` or `DischargeService`. The player went from invisible soldier to lone
visible hero with no explanation, standing exactly where their company had been annihilated, because
`RestorePresence` restores presence without moving the party. A live session measured 73 real
seconds from that transition to the same lone hero being inside a 1,544-combatant sally-out.

**The case split is in the TEXT, not the clock.** One 7-day window serves both, and that is a
measured decision rather than a shrug:

| Case | Recovery odds inside 7 days | What can be said |
|---|---|---|
| Captured | ~25% on escape alone — `PrisonerReleaseCampaignBehavior.DailyHeroTick:206` is a flat `0.04f`/day for a prisoner held in a settlement (the ×5 field multiplier applies only to prisoners held by a *mobile* party). Plus the peace and ransom release paths the same behavior runs | Captor **and** settlement — the only loss case with a location |
| Party destroyed, lord free | Waits on a respawn tick, then a clan-tier gate decides whether he ever re-arms | Nothing locational. He has no position at all until the engine places him |

Neither is a certainty and neither is a no-op, so two timers would be two numbers to tune and two
behaviours to explain for no measured gain. `CommanderSnapshot` gained `CaptorName` and
`CaptivitySettlementName` for the split, read from `PartyBelongedToAsPrisoner` — **not** from the
four existing settlement fields, every one of which resolves through `PartyBelongedTo` and is
therefore null for a prisoner.

**A modal, not a toast, and prioritized.** It fires in the tick after a battle, which is when the
player is accelerating — and #436 measured what a toast is worth at speed. It also asks a question,
unlike a duty assignment. `InformationManager.ShowInquiry(data, pauseGameActiveState, prioritize)`
enqueues a non-prioritized inquiry behind whatever is on screen, and the tick after a battle is when
vanilla raises its own ransom and peace popups, so it passes `prioritize: true`.

It deliberately does **not** also force `TimeControlMode = Stop`, which was the original plan.
`pauseGameActiveState: true` already holds the clock for the window that matters, and Stop is a
setter BannerlordTogether prefixes and rewrites outright in a co-op session (see
`CoopSuppressedUiAttribute`) — a control that lies is worse than no control.

Latched on the commander id, so the hourly reconciler asks once per episode rather than once per
game hour.

**Two consequences of the state, both of which used to be wrong:**

- **The term is waived.** `EvaluateReleaseRequest` now grants release outright while the commander
  is unavailable. Not a kindness — the term cannot be *served*: no duties (the orchestrator requires
  attached), no battles, no trust, nobody to earn it from. Refusing left exactly one interaction,
  stand still for N days or take a desertion penalty for leaving a company that no longer exists.
- **No wage and no promotion.** `RunDailyTick` gates on `IsEnlisted`, which spans this state, so a
  lord in an enemy dungeon went on paying a daily wage and could fire *"You have been promoted to
  {RANK}."* Rations, the morale floor and the surgeon stay — they are the difference between waiting
  and starving, and the player did not choose to be left behind. Pay and rank are the parts that
  require someone to award them.
  The battle payout also skips the promotion in this state (#835).

### Two toasts, one of which is conditional (#436)

A duty announces at start and reports at resolve. At time acceleration those land closer together
than a message stays on screen: measured live 2026-08-09 at 4x, a 4-hour shift started 09:29:38 and
resolved 09:29:42, and the player reported being given **one** duty when the log records two. A
Bannerlord quick-info toast lives about three seconds, so the assignment had faded as the result
arrived — and trust had already moved off a notification nobody could read.

Two changes, and the second is the load-bearing one:

- **The result toast is self-contained.** `taom_enlist_duty_result` = `Orders: {DUTY}. {RESULT}`,
  where `RESULT` is the row's own success/failure line. The player who reads exactly one message now
  gets the whole story. The two halves stay separate variables rather than a concatenated template
  so a translator can reorder them.
- **The assignment toast is skipped when the shift is too fast to read two messages.**
  `FieldDutyRuntime` samples real seconds per campaign hour from the gap between hourly ticks — that
  gap *is* the time-acceleration multiplier — and predicts `durationHours × rate`. Under ten real
  seconds, it does not announce.

The prediction fails toward announcing, deliberately: an unknown rate (first duty of a session, or
straight after a save/load, since the estimate is in-memory and not persisted) and a non-finite
clock both resolve to "announce". A redundant toast is cosmetic; a missing one is the bug. The gate
is written as a positive requirement for the same reason NaN gates are elsewhere in TAOM.

`IRealTimeProvider` exists for this and nothing else. It is a seam for the same reason
`IRandomProvider` is one — a service that reads the clock directly cannot be tested — and it is
deliberately **not** campaign time. Everything else in this feature measures campaign days, because
that is what the fiction runs on; this measures what the player experienced, and the two diverge by
exactly the multiplier.

### Duty gates must admit someone who can pass

`SkillCheckService.Passes` is `skill + max(0,trust)×2 + rank×4 + Next(0..50) >= difficulty`. The
roll caps at 50, so a row whose `difficulty` exceeds a gated-in player's reachable total by more
than 50 is not *hard* — it is **impossible**, and every offer is a guaranteed trust loss the player
could neither foresee nor avoid.

`hideout_strike` shipped that way: difficulty 76, gated at Veteran with **no** `minTrust`, while its
two Veteran siblings both had one (`trusted_dispatch` 70/trust 15, `relief_dispatch` 72/trust 8). An
untrained Veteran needed 58 on a d50. Fixed by completing the pattern the author had already
established — `minTrust: 15`, the value already in the file for a comparable duty — rather than by
inventing a new difficulty.

`FieldDutyReachabilityTests` now pins two floors: no row may be unpassable by the weakest player its
own gates admit, and the difficulty ceiling must **rise** with the rank required, so a promotion
cannot hand the player easier work than it just unlocked. Both are floors, not balance opinions —
whether a 6% duty is *good* belongs to whoever plays it; whether a 0% duty is a *bug* does not.

#### The floor rested on `UntrainedSkill = 10`, and the field log said 0 (#438, answered by #520)

The reachability test cannot read the player's skill; there is no player at test time. It assumed
one, `UntrainedSkill = 10`, documented as "roughly a fresh hero's untrained value". That constant
was the whole floor, and every row passed against it, which is why the suite stayed green while
eight rows were unwinnable in play.

Two live sessions produced the counter-example. The gating skill is **0**, not 10:

```
[Enlistment.Duties] duty 'recruitment_errand' failed — skill 0 trust -1 rank Recruit vs difficulty 54
[Enlistment.Duties] duty 'service_shift' failed — check 6 (skill 6, trust +0, rank Recruit +0)
                    + roll 0-50 vs difficulty 45
```

A Bannerlord hero has 0 in any skill they never invested in, and Charm on an orc warrior is the
ordinary case rather than a corner one. `service_shift` is the easiest row in the file.

The constant is now **0** and the difficulties were retuned to meet it. The Recruit band was
rescaled into [40, 48] and the Soldier band into [52, 52], preserving the author's relative
ordering, so no row needs better than a natural maximum from a player with nothing trained:

| Row | Was | Now | | Row | Was | Now |
|---|---|---|---|---|---|---|
| `service_shift` | 45 | **40** | | `scout_route` | 56 | **48** |
| `forage` | 48 | **42** | | `bandit_hunt` | 58 | **48** |
| `road_patrol` | 52 | **44** | | `mounted_pursuit` | 62 | **52** |
| `supply_delivery` | 52 | **44** | | `deserter_sweep` | 64 | **52** |
| `recruitment_errand` | 54 | **46** | | `recon_sweep` | 55 | **47** |

`bandit_hunt` and `deserter_sweep` first landed at 50 and 54, which is `needed == 50` for the weakest
player each admits: legal under the floor, and one roll in fifty-one in play. A fourth assertion,
`NoFieldDuty_IsPassableOnlyOnANearMaximumRoll`, now rejects anything at two rolls or fewer of
headroom, because the two floors above leave exactly that gap. Passable-at-all is satisfied by a 2%
row, and trust-positive-in-expectation is satisfied by any row that charges nothing for failure,
which since this change is all thirteen.

The three Veteran rows keep 70 / 72 / 76 untouched: their `minTrust` 8 to 15 already carries them
past their difficulty at skill 0, which is the `hideout_strike` fix above still holding.

#### Every row also carries a second support skill, and failure no longer costs standing

Retuning difficulty alone would have left the real defect in place, because the difficulty was never
the interesting half. `EffectiveSkill` takes the **best** of `supportSkills[0]` and `[1]`, and ten
rows named only a specialist skill (Scouting, Charm, Steward, Tactics) that a warrior hero has at
zero for the whole campaign. Each row now names a second skill an enlisted soldier actually trains,
so the same duty gets easier as service accumulates rather than staying flat forever:

| Row | Specialist | Second |
|---|---|---|
| `recon_sweep`, `mounted_pursuit`, `road_patrol`, `scout_route` | Scouting | Riding |
| `bandit_hunt` | Tactics | Athletics |
| `deserter_sweep` | Leadership | Athletics |
| `forage` | Steward | Athletics |
| `recruitment_errand` | Charm | Leadership |
| `supply_delivery` | Steward | Leadership |
| `service_shift` | Athletics | Leadership |
| `relief_dispatch` | Riding | Leadership |

Leadership is the deliberate default for company work: `RunDailyTick` grants it 10 XP a day to every
enlisted player regardless of assignment, so it is the one skill service itself guarantees.

**`failureReward.trust` is 0 on all thirteen rows.** A field duty is handed to you, the offer states
no odds, and the check happens off screen, so charging standing for a roll the player could not see
and often could not win is a trap rather than a cost. Failure still forfeits the success reward, and
that is the cost. The interactive duties and incidents keep their negative outcomes, because those
are opt-in: an incident happens TO you but you pick the option, where a field duty is simply handed
over.

Be precise about what that does and does not justify, because the first version of this paragraph
said the popup "stated the stakes" and it does not. `InteractiveDutyPresenter.PresentIncident` passes
`ShowTwoOptionInquiry` exactly four strings: the row's title and body, and each option's label. No difficulty, no skill name, no trust delta reaches the
player. The exemption rests on the choice being the player's, nothing more. Making the stakes legible
would be a real improvement and is not in this change.

One incident row is worse than that exemption can carry, and it predates this change: `press_claim`
(pay_delay) is Charm 65 with `rankBonusApplies` unset, so a Recruit at Charm 0 needs 65 on a d50 and
cannot pass, and it charges 1 trust on **both** outcomes. Its sibling `wait_it_out` is Steward 50,
pays +1 on success, costs nothing on failure, and triggers the same `ReleaseDeferredPay` effect,
because the effect is keyed on the incident and fires for whichever option passes. `press_claim` is
therefore dominated in practice: harder, always costs standing, and pays only 4 more service XP on
success (20 against 16); the reputation it also paid is no longer tracked. Left alone here
deliberately and tracked as #521, because retuning an incident belongs in its own change rather
than folded into the field-duty economy.

`FieldDutyReachabilityTests` now pins a third floor beside the two above: at skill 0, for the
weakest player a row's own gates admit, the expectation `p·success + (1-p)·failure` must be `>= 0`.
Every row satisfies it today through the zeroed failure cost, which makes it a guard rather than a
live constraint. That is the point. Re-add a trust cost without lowering the difficulty to match and
it reddens.

### Standing had no reachable source, which is what pinned the ladder (#520)

The two sections above are one bug seen from the duty side. From the ladder side it reads
differently, and worse. `AdjustTrust` has three call sites and **none of them is a passive gain for
serving**: standing rises only from a duty success or from a merit band that pays trust. With the
duty side unwinnable, and the merit bands paying trust only at score 60 and above, a soldier who
fought steadily and took camp work he could not pass drifted downward with no floor above
`trustMin`.

A live service reached day 73 with 2903 service XP, eight times the Veteran requirement, still a
Soldier, reading "badly thought of" with the ladder line stuck on *your commander's confidence*.
Veteran gates on `minTrust: 0` and `minDutySuccesses: 2`, so both halves of that gate sat downstream
of the same closed loop: the duties that grant trust needed a skill the player did not have, and the
duties that are winnable at high trust need trust he could not reach.

The fix is two independent earners, neither depending on the other:

- the duty loop, repaired above;
- the merit path, reopened by the cohesion fix below. `strong` (`minScore: 60`) already paid 1 trust
  and `distinguished` 2; what was missing was any way for an ordinary battle to reach them, because
  cohesion scored a flat zero.

**The band that pays was NOT moved down, and the first attempt at this fix moved it.** Paying trust
from `solid` (`minScore: 40`) looked like the obvious second earner and quietly paid two shapes that
never fought. `BattleMeritScorer` zeroes only the survival term on `LeftTheField`, so kills, cohesion,
proximity, engagement and role fit all survive a walkout: the ceiling for quitting with a full kill
count is `30 + 15 + 10 + 10 + 10 − 30 = 45`, which is inside `solid`. And a soldier who simply stands
in his own line banks survival + cohesion = 40 with no kills and no engagement at all, which is also
inside `solid`. Both were reachable through ordinary play with no guard anywhere in the payout chain.

So the boundary stays where it was, and two things now hold it there:

- `MeritTrustFloorTests` pins the invariant against the shipped config from both sides: no score
  attainable **without fighting** may reach a band that pays trust, and a soldier who held formation,
  stayed engaged and took a kill still must. A future tuner cannot move a `minScore` past either.
- `EnlistmentBattlePayoutService` withholds band trust outright when the sample says `LeftTheField`,
  independent of where the boundaries sit. XP and gold still reflect what the player did before he
  left, which is the same reasoning that made `LeftTheField` zero the survival term alone. When that
  walkout was also a sat-out battle (#830), the sit-out rule then governs the base pay and renown. The
  comment on `MeritScoringConfig.LeftFieldPenalty` used to claim 30 was "sized to sink the best
  possible walkout into the bottom band"; it arrived at that by leaving kills out of the sum, and it
  has been corrected.

`MeritBandTrust_MatchesBetweenTheShippedFileAndTheCompiledDefaults` pins the JSON against
`DefaultMeritBands()`, and it reads the **file**, not `GetConfig()`. Four paths inside the provider
replace `MeritBands` wholesale with the compiled defaults, one of them (`meritBands` null or empty)
logging nothing at all, so a test that went through the provider would compare the defaults against
themselves and report green on exactly the divergence it exists to catch.

The rank thresholds themselves were not touched. They were never the bug.

#### Cohesion had a blind spot that held the merit score under the band

`MeritGeometryScanner` measured cohesion against the player's formation **captain**, and reported
`Absent` when the only candidate was the player himself. That is the ordinary case, not an edge one:
per #443 an enlisted player is routed to a one-man `PlayerTeam`, so his formation contains nobody
else and cohesion scored a flat zero for entire battles. The cost is 15 points of the 100 outright,
plus the 10-point infantry role-fit bonus, which requires `CohesionRatio >= 0.5`. Twenty-five points
is what stood between an ordinary infantry battle and the `strong` band.

`MeritGeometryAccumulator.CohesionDistanceSq` now takes the captain reading when there is one and
the **nearest ally** when there is not, and the scanner tracks that distance in the loop it was
already running (the allied branch previously skipped every non-hero). Absent still means absent
when neither is observable: alone on the field is a real state and it must keep failing the gate,
never collapse to a distance of zero and score a free hit.

The fallback is measured against the same `cohesionDistance` of 25 m, which was calibrated for
distance-to-captain and is a looser bar for distance-to-anybody: inside a friendly line it is a hit
on essentially every sample. That is deliberate given #443 (there is no captain to measure against
and the alternative is a permanent zero), and it is why the trust boundary is held by
`MeritTrustFloorTests` rather than by the score distribution. It is also unmeasured: nobody has
observed the resulting `CohesionRatio` in a live battle.

Three consequences worth naming, because the score feeds more than trust.
`EnlistmentBattlePayoutService` grants the band's `ServiceXp`, `Gold` and `Trust` and folds
`band.Renown` into `BattleRenownPolicy`:

- **`distinguished` becomes reachable for Infantry for the first time.** With cohesion pinned at 0
  the Infantry ceiling was `30 + 25 + 10 + 10 = 75`, under the band's `minScore: 80`. It is now 100.
  That unlocks 30 XP, 20 gold, 2 trust, and 3 renown at the top of the ladder.
- **Reputation is gone (#844).** The four domains (`FieldRep` and its siblings) were written by
  merit bands and the `repDomain` rows in the duty and config JSON, and read only by
  `TrustLedger.DominantDomain`, which no production path called. The fields, the `repDomain` and
  `repAmount` keys, `reputationMax` and the rhythm snapshot's `HighScrutiny` flag (#843) were
  deleted together. Battle counters, tournament wins, the duty food requirement,
  `trainingSessionXp`, `EnlistmentStarted`, `EnlistmentStateMachine.Transitioned`,
  `EquipmentPayoffCalculator`, `WithdrawPetition`, the duty failure counter (`DutyFailures`) and the
  `TrustLedger` helper (its one clamp is now inline in `ServiceRewardService`) went the same way:
  nothing read them or they had a single caller.
- **Renown is the one payout that reaches the wider campaign**, through `GainRenownAction` to the
  player's own clan, so a better merit distribution is mild clan-tier inflation.

This does not touch #443's own design call about which team the enlisted player belongs on. It fixes
the merit-scoring fallout only.

#### Confirmed in play 2026-08-28, and the same run reproduced the defect that was reverted

`taom_debug_2026-08-28_20-58-46.log`, diagnostics on, one fought-through Infantry battle, won:

```
[Enlistment] battle merit 46 -> band 'solid' (kills=0, won=True)
```

That line had never appeared in any log before. The merit path is observed working end to end for the
first time, and the flat win renown of 2 plus the band's 1 arrived as `renown=3`.

**The 46 is worth keeping, because it proves the fallback without needing the ratios logged.** The
player was in the Infantry formation, and infantry role fit requires `CohesionRatio >= 0.5`, so
before this change cohesion contributed nothing AND role fit was unreachable. A zero-kill battle
therefore topped out at survival 25 + commander 10 + engagement 10 = **45**. Anything above 45 at
zero kills is only reachable with the fallback running.

Decomposing further: role fit must still have been off, because switching it on needs cohesion and
engagement both at 0.5 or better, which is 12.5 points against the 11 available above survival. So
cohesion was saturated near 1.0 with engagement near 0.2. **That is the stander shape**, in the line
all battle, rarely near an enemy, nothing killed, landing at 46 and inside `solid`. Paying trust from
`solid`, which is what the first version of this fix did, would have paid standing for hanging back.
The review caught it on arithmetic; play reproduced it within three campaign hours of shipping.

It also answers the question the cohesion section above flags as unmeasured: an ally really is inside
25 m on essentially every sample, so cohesion does saturate for anyone standing in a formation. Read
the 25 m fallback as generous by design, not as a threshold that discriminates.

**The duty half is still unexercised.** Zero `[Enlistment.Duties]` lines: the oath was sworn and the
battle fought inside about three campaign hours, against `minDaysBeforeFirstOffer: 3`. Nothing in
that run touches the eight rows that were impossible or the retune that fixed them, which is the
larger half of #520. It needs 3+ days of service and "Ask your sergeant for work"; the line that
settles it is `duty '<id>' completed`.

Calibration, from the same run: 46 sat 14 short of `strong`, which is where standing is earned. Three
kills would have made 61, and holding engagement above 0.5 unlocks the 10-point role fit on its own.
Participation pays and presence does not, which is the intent, but it does mean the duty loop carries
most of the standing economy.

#### What the economy looks like afterwards, including its one-way half

Standing is now a **ratchet on the field-duty track**. `record.Trust` has one writer
(`ServiceRewardService.AdjustTrust`), no field duty charges it, no merit band pays it negative, and
there is no daily decay, defeat penalty or discharge penalty. Every remaining sink is opt-in: the
assignment swap (1, behind a 7-day cooldown), and the `press_claim`, `forage_extra` and
`break_it_up` incident options. A player who declines those walks monotonically to `trustMax: 20` and
pins there. That is defensible (standing is meant to be earned and kept, and the ladder tops out at
`minTrust: 6` for Sergeant) but it was not a decision anyone wrote down, so it is written down here.

Two knock-on effects follow from trust actually climbing, neither of which is a bug:

- The three Veteran duty rows gated at `minTrust` 8 and 15 go from near-unreachable to routine, which
  is what those gates were for.
- Duty offers get more frequent. `DutyRotationPolicy` computes
  `baseOfferChance + (pressure ? 0.15 : 0) + max(0, trust) * 0.01`, so the quiet chance moves from the
  0.06 floor toward 0.26 as trust saturates, clamped by `maxOfferChance: 0.4`. Read `0.06` as the
  starting rate, not the operative one. The provider's attainable-ceiling warning is unaffected:
  `0.06 + 0.15 + 20 x 0.01 = 0.41`, and `0.4 > 0.41` is false either way.

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
| Every start announces — **unless the shift is too fast to read two messages** (#436) | The old model was self-announcing by accident (it made you visible and sent you travelling). This one is invisible, so `Start` shows an assignment toast — skipped only when `durationHours × real-seconds-per-campaign-hour` falls inside the 10 s window, where the self-contained result toast carries both halves instead. Pinned by `Start_ShiftFasterThanTheToastWindow_SkipsTheAssignmentToast` |

The spawn/destroy path went with the travel model, which removes the **#375 stack-overflow surface**
entirely rather than guarding it: `DestroyPartyAction` dispatches `MobilePartyDestroyed` *before* it
deactivates the party, so the handler re-entered `Apply` and recursed. Nothing in the mod destroys a
party any more. Nine now-dead members were deleted from `IDutyWorldAdapter` (384 → 121 lines); the
four survivors are daily upkeep, used only by `EnlistmentDailyService`.

Known and accepted: a save made mid-duty under the old model may leave its spawned looter party on
the map with nothing to destroy it. They are ordinary bandit parties the engine already manages.

**Equipment.** `enlist_{runtimeCultureId}_{assignment}_{rank}` rosters in
`equipmentsets/taom_enlistment_equipment.xml` (359: 343 culture cells out of a possible 416, over
26 cultures × 4 assignments × 4 kit tiers, Cardolan, Rhudaur and Ered Luin among them, plus 16
culture-neutral defaults, counts from `tools/audit_enlistment_roster_coverage.py` on 2026-10-27), seeded from each
culture's own troop tree by `tools/generate_enlistment_rosters.py`, so kit is race-correct by
construction. Drawn from the quartermaster once per kit tier into party inventory (not
auto-equipped). The quartermaster's reply is chosen by `QuartermasterAnswerPolicy` (#829), except
the host-only reply for a co-op client (when this peer is not the authority), which the authority
check in the behavior sets. Besides the issued line there are five refusals (already drawn for the
rank, already carrying the best kit at the top tier, empty stores, cannot load into the baggage, host only), each with its own text. "Cannot load"
is the line with no condition: it sits below the others and also answers any answer without a line
of its own, and a new issuance result maps to it through the policy default. Before #829 every
refusal read "already drawn". `QuartermasterAnswerPolicyTests` pins the mapping and the wiring.

The kit carried **no weapon at all** until 2026-09-01 (#525). That was the shipped contract, not
a regression: the generator filtered every weapon slot out and the auditor failed the build on
one. Players reported drawing armour and never a sword, and they were reading the data
correctly. The C# was never involved: `EquipmentRosterCatalogAdapter` has always read all 12
slots, so the fix was data plus tooling. The assignment entered the id at the same time, because
a weapon is only right if it matches the role the player chose.

**Fallback chain: culture, then assignment, then rank**, each descending. Culture outranks
assignment because issuing another faction's kit is the defect players actually report: #427 was
"the quartermaster gives me gondor gloves and I'm enlisted under Theoden", and #431 was the same
complaint arriving through the neutral fallback, which is tagged `Culture.neutral_culture` while
being Rohan militia in Dunland boots. Rank is innermost, so the right role at a lower rank beats
the wrong role at the right rank. (An earlier draft justified the ordering as a *rendering*
invariant. It is not: the roster is keyed on the commander's culture, so it cannot know the
player's race under either ordering. See the known limitation below.)

**69 of the 368 cells are absent by design.** A cell is omitted when its donor pool holds nothing
within one band of the rank, when the resulting kit would repeat one the player already drew at a
lower rank, or when it would give less protection than he is already wearing, measured per hit
zone rather than by a single stat (a body piece contributes body AND leg armour, a cape body AND
arm, so a single-stat proxy misses most of what a kit protects). The band is a hard cap, not a
sort key: filtering donors by
`default_group` removed the guarantee that an in-band donor exists, and without the cap a
Mirkwood cavalry recruit drew a level-41 kit. Where a culture would otherwise own no roster at a
rank at all (bluecraig and mistymountainorcs each ship a single level-36 troop), the cap yields
rather than let the player fall out to human militia gear.

**Shaghana and Abanissa have their own kits (2026-10-15).** They were #431's last two documented
exceptions, falling out to the neutral default; shaghana gained 14 generated kits and abanissa 15,
abanissa through the `TREE_ALIASES` entry `'abanissa': 'aserai'` in the generator (it owns no troops,
its tree is the aserai one). `DocumentedExceptions` in `EnlistmentRosterCultureCoverageTests` is now
empty. The Harad-sphere archer kits (the aserai, shaghana and abanissa chains) carry the
vanilla steppe bows (`composite_bow` 30/51, `steppe_heavy_bow` 30/52, `composite_steppe_bow` 30/52,
`steppe_war_bow` 50/54 as difficulty/damage), because every Harad ladder bow needs Bow 100 and the
player equips the kit by hand; `EnlistmentArcherLauncherDataTests` pins it. The same day KEYforce's
Harad regular weapons (#805) changed their donor troops, so the 29 shaghana and abanissa kits were
removed and reseeded with `--seed-missing` (they now issue his spears, sword, shields and arrows), and
the steppe bows were put back on their archer chains. KEYforce's javelins and maces (lotraom-assets
`03320a65`) changed the donors again on 2026-10-16, and the same reseed now issues 28 kits: his maces in
place of the aserai swords (not on the abanissa archer recruit, whose donor `harad_militia_archer` still
carries one), his javelins on the shaghana infantry from soldier up, and his sword on both cavalry
recruits. Since the steppe bows are swapped in before duplicate suppression (decision D7), the shaghana
archer soldier has its own kit with `steppe_heavy_bow` instead of re-drawing the recruit's. Neither culture has a `support_veteran` kit any more: it would equal the soldier's, so the
resolver descends to the soldier kit and `EnlistmentEquipmentService` issues it a second time at
veteran (its ledger checks the requested kit tier, not the roster). The aserai kits, KEYforce's hand edits
of 2026-10-10 kept by decision D3, were reseeded the same day on Mike's word (decision D5 in
[rca-keyforce-harad-javelins-2026-10-16.md](../reviews/rca-keyforce-harad-javelins-2026-10-16.md)): 14 kits,
identical to abanissa's, which share the tree. The steppe bows now come from the generator's
`ISSUE_LAUNCHER` table, so a reseed is one command per culture:
`python tools/generate_enlistment_rosters.py --apply --culture <c> --reseed`. The generator has no
`--verify`, so a later donor change goes unnoticed until someone reseeds.

**A cell is never emitted weaponless.** That is not a nicety: the resolver probes *existence*, so
an armour-only roster would end the fallback walk and shadow the armed kit the player would
otherwise descend to. The first cut of #525 shipped 15 such rosters, all Support, and reproduced
the reported bug inside its own fix. Support now takes any melee sidearm (one weapon, still no
shield) and a cell with no usable weapon is suppressed instead of emitted.

**No mounts, at any assignment including cavalry.** The cavalry donor pools mount `taom_chariot_a`
(the generator's `drop_creature_riders` keeps the spider, war elephant and Mumakil riders out of
every pool, judging each troop by every battle set the engine builds for it, `<Equipments>`-level
overrides included); the roster is keyed on the *commander's* culture, so it
cannot know the player is a dwarf who would spawn inside a horse; and `MOUNTED_DWARF` cannot see
these rosters at all, because it walks data an `NPCCharacter` names and these are applied at
runtime by id. The reassign line was reworded off "Give me a horse" in the same change so the
shipped text does not outlive the behaviour.

The issue-ledger is monotonic (covering a kit tier covers every tier below, so a demotion never
re-issues), keyed on the **kit tier alone** (ranks 4 to 6 share the best tier), and persists in the content record. Two consequences worth
stating: swapping assignment does not re-open a spent draw, and **a character already serving at
the top tier when #525 shipped has spent every draw and will never see weapons** without a fresh
enlistment. There is no save migration for that, deliberately.

**Known limitations.**

- The kit is keyed on the commander's culture, not the player's race, so a cross-race enlistment
  draws gear rigged for another skeleton, and cross-race armour clips or floats on a custom
  skeleton. That predates #525 for armour; weapons make it visible in-hand as well.
- **Honourable discharge reclaims the issued kit by item id**, one per ledger entry, and the
  ledger has held weapons and ammunition since #525. `IPartyItemRosterAdapter.RemoveItem` drains
  the unmodified stack with no notion of provenance, so a player who drew an archer kit, shot it
  dry and refilled from loot has one of *his* arrow stacks taken at discharge. Arrows are the most
  common post-battle loot in the game, so this is not hypothetical. Desertion keeps the kit and is
  unaffected. Tracked as a follow-up rather than fixed here: excluding ammunition from the ledger
  needs an item-class surface `IItemPoolAdapter` does not currently expose.

## The field-test arc (2026-08-11), seven reports, one root cause

A live playtest produced seven complaints. **Six trace to one decision:** `MobileParty.MainParty.Army`
was kept permanently null (`ClearArmyAttachment()` in both `ParkNear` and `RestorePresence`).

| Report | Actual cause | Fix |
|---|---|---|
| 1. Can't enter towns / can't buy anything | The player was ALREADY inside; `"town"`/`"castle"`/`"village"` were in `RedirectMenuIds` | Shore-leave pass (`TownLeavePolicy`) suspends those three while the column rests there |
| 2. Not paid, or it doesn't show in the wallet | Both true: the 500g commander reserve silently defers the wage, and the gold that did arrive used `disableNotification: true` with `DailySummary.Wage` read by nobody | `WageReportPolicy` + three messages; display-only line in `TaomClanFinanceModel` |
| 3. Not enough renown | TAOM granted none; vanilla's share is contribution-scaled and a party of one hero rounds to zero | `BattleRenownPolicy` through the `Grant` chokepoint, via `GainRenownAction` |
| 4. Spawn far behind everyone | `Army == null` → different TEAM → own deployment block, sorted last, 20-unit gap | Transient army join |
| 5. Clan declares war individually | Already happening via `BeHostileAction` on the vanilla `encounter` menu | Mirror the commander's wars; unwind only what the mirror created |
| 6. Commands show wrong | **Vanilla bug** at `BehaviorComponent.cs:107` | One-instruction transpiler |
| 7a. Lord's army fought without me | `FindCommanderPartyIdIn` matched only his OWN party; an army-attached lord never enters the `MapEvent` himself | Match the army leader too |
| 7b. Jumped immediately after defeat | Still `AttachedTo` when the encounter finished → forfeits vanilla's escape | Detach above every gate in `OnCommanderBattleEnded` (one precondition now sits above it, see #551) |

### The three engine facts this rests on (installed v1.4.8)

**`PartyAgentOrigin.IsInSameArmyAsPlayer` needs BOTH halves.** It requires
`army == MobileParty.MainParty.Army`, membership, which only the `Army` setter gives, because
`OnAddPartyInternal` does the `_parties.Add`, AND, when the leader is not the main party,
`MobileParty.MainParty.AttachedTo == army.LeaderParty`, which only `AddPartyToMergedParties` sets.
Either call alone leaves the property false and the player back on his own team.

**`PlayerEncounter.FinishEncounterInternal` grants the post-defeat escape only when
`MainParty.AttachedTo == null`.** `TeleportPartyToOutSideOfEncounterRadius()` plus
`SetDoNotAttackMainParty(2)` sit behind that check, and `AddPartyToMergedParties` sets `AttachedTo`.
**So the leave must run before the encounter finishes, and since #557 it runs INSIDE it.**
`PlayerEncounter.Finish` runs `FinalizeBattle()` (which dispatches `MapEventEnded`) and
`FinishEncounterInternal()` (which grants the escape) as consecutive statements, and campaign-event
listeners are LIFO, so a detach on `MapEventEnded` ran BEFORE vanilla's siege-aftermath handler and
corrupted its read of the side's parties. `Patch85_EnlistedDetachDeferral` postfixes `FinalizeBattle`
and calls `ServiceBattleService.FlushArmyLeaveAfterBattle`, stateless, guarded on
`!IsMainPartyInMapEvent` (the #551 guard in its cheapest form); `OnCommanderBattleEnded` no longer
detaches at all. ServeAsSoldier ships with the original hole.
`BattleEnded_DoesNotDetachInsideTheMapEventEndedDispatch` and
`FlushArmyLeaveAfterBattle_DetachesOnceTheBattleIsOver` pin the seam. The flush's own `LeaveArmy`
disbands the army we raised, which raises the commander's "army left" edge from inside `Finish`;
the reconciler now refuses that pass while the adapter is mutating (#577, "The presence hold").

**Exactly one precondition sits ABOVE the leave, and it is not a relaxation of the rule** (#551).
The detach is destructive when the ending event is not the player's: clearing `AttachedTo` runs
`Party.MapEventSide = null`, which pulls him out of whatever map event he IS in. So the leave is
skipped when `!mainPartyWasInEndingEvent && _encounter.IsMainPartyInMapEvent`, and logs at ERROR when
it fires, because reaching that state means the behaviour's gate leaked. That condition can only
hold for a FOREIGN event; the player's own event reaches this method with `mainPartyWasInEndingEvent`
true and leaves the detach to the deferred seam above.
`BattleEnded_ForeignEventWhileInAnotherLiveBattle_DoesNotLeaveTheArmy` and
`BattleEnded_OurOwnEventEnding_LeavesTheDetachToTheDeferredSeam` pin both halves. Do not "simplify"
this to an unconditional detach.

**`Kingdom.CreateArmy` moves the commander.** It calls `army.Gather()`, whose non-player branch runs
`FindBestGatheringSettlementAndMoveTheLeader` and dispatches `OnArmyCreated`. So
`ArmyMembershipAdapter` uses the bare `Army(kingdom, party, type)` constructor instead: it sets
`LeaderParty`, assigns `LeaderParty.Army = this`, and the `Kingdom` setter self-registers through
`AddArmyInternal`, complete, without the march or the "has formed an army" notification.

`AiBehaviorObject` therefore stays null for that army's whole life. **That is a liability, not a
feature**, see the review-pass section below. It does keep the siege and owner-change handlers inert
(they gate on `AiBehaviorObject is Settlement`), but five cases of
`Army.GetLongTermBehaviorTextForAILeadedParty` dereference the same field with no guard, so the army
must never outlive the battle.

### No rank holds a command (#576, replaces the 2026-08-12 rank gate)

With `Army != null`, `IsPlayerSergeant()` is true, so vanilla stops promoting the player to GENERAL
and wires him as a sergeant. The 2026-08-12 change let a rank-3 Sergeant keep the one formation
vanilla then offered (`BattleCommandPolicy.ShouldKeepSergeantCommand`) and noted, as a "consequence
to watch in-game", that `CanPlayerSideDeployWithOrderOfBattleAux()` also keys on
`IsPlayerSergeant()`. #443 then closed without that in-game look, and the consequence shipped:
the Order of Battle deployment screen opened at EVERY rank (its gate reads campaign state, not the
TAOM rank), the sergeant-choice view listed the player as the only assignable captain and
auto-selected him, and dropping himself on a formation ran `AssignSergeant`, which sets
`Formation.PlayerOwner`. That setter turns `IsAIControlled` off (`Formation.cs:271-282`); with TAOM's
`(false, false)` roles the formation AI never ticks (`Formation.cs:2283`) and the order UI is refused
(`MissionOrderVM.cs:796-803`). Nobody could command that formation. Players reported it as
"assign yourself captain and no orders are given".

Fixed on 2026-09-12 by removing the carve-out (no rank holds a command; #521 says rank 3 is
effectively unreachable anyway) and adding the two seams the role strip could not supply:

- **`TaomBattleInitializationModel : SandboxBattleInitializationModel`** overrides
  `CanPlayerSideDeployWithOrderOfBattleAux()` as `_service.CanPlayerSideDeployWithOrderOfBattle() ??
  base.…()`. `EnlistmentDeploymentService` answers `false` exactly when
  `BattleCommandPolicy.ShouldStripPlayerCommand` is true and `null` otherwise, never `true`, so the
  disabled path is literally `base`. Registered in `SubModule.OnGameStart` after SandBox's model,
  last registered wins. The engine caches the answer once per mission, and every deployment reader
  (`DeploymentMissionController.SetupTeams`, `AssignPlayerRoleInTeamMissionController.OnPlayerTeamDeployed`,
  `GeneralsAndCaptainsAssignmentLogic.OnTeamDeployed`) goes through that one cached bool, so the
  screen never opens and `AssignSergeant` is unreachable. The commander's lords also regain the AI
  captain path the open screen used to skip.
- **`EnlistedSoldierPlacement.ReclaimAfterDeployment`**, from
  `EnlistmentBattleFormationMissionBehavior.OnAfterDeploymentFinished`. With the screen shut,
  vanilla's `GeneralsAndCaptainsAssignmentLogic.OnTeamDeployed` runs `AssignBestCaptainsForTeam`
  over every hero on the player team, the player included, and moves the captain into the largest
  formation matching his mount state; `OnDeploymentFinished` then moves him into the general's
  formation whenever the team has 50 or more members. Neither sets `PlayerOwner`, so orders keep
  flowing, but he is captain of, and standing in, the wrong formation. The correction runs after
  every `OnDeploymentFinished` handler (`FinishDeployment:48` then `:78`), clears any captaincy
  handed to the player, and re-runs the placement. The explicit clear is load-bearing:
  `Formation.RemoveUnit` nulls `Captain` only for a unit that cannot lead formations remotely
  (`Formation.cs:2193`), and vanilla's `OnDeploymentFinished` sets that flag true on the player
  before moving him, so the move alone would leave him captain. Known cosmetic leftover: the
  formation banner set for the brief captaincy stays.

Proof lines, with the Enlistment Trace off: `[Enlistment] Order of Battle deployment
suppressed: enlisted soldier in the commander's battle`, `[Enlistment] battle command stripped at
AfterStart`, and `[Enlistment] after deployment: cleared N captaincy vanilla handed the soldier,
formation X -> Y` when vanilla had moved him. Pinned by `EnlistmentDeploymentServiceTests` (every
state x side equals the shared policy, never true), `TaomBattleInitializationModelInvariantsTests`
(the engine's gate stays non-virtual and cached, the Aux stays protected virtual) and
`EnlistmentAfterDeploymentBindingTests`. Not yet seen in game; smoke items in "Testing".

Host Review ([host-review.md](host-review.md)) relies on the `null` fall-through above: with no map event `EnlistmentDeploymentService` returns `null`, so the model defers to `SandboxBattleInitializationModel` and no Order of Battle screen opens.

### The presence hold: nothing moves the party while its battle encounter is open (#577)

A player report at x64 campaign speed: the party attached and immediately detached during the
battle join, the log showed `TICK trigger=army left` while the `PlayerEncounter` was still active,
and "Send troops" then threw `IndexOutOfRangeException` in `BattleSimulation`. The index is the
same one #551 named, one path earlier: `BattleSimulation.cs:62` writes
`SelectedTroops[(int)PlayerSide]`, `PlayerSide => PartyBase.MainParty.Side`, and `Side` is
`MapEventSide?.MissionSide ?? None` where `None` is -1. Anything that clears `AttachedTo` on an
active party nulls its `MapEventSide` (`MobileParty.SetAttachedToInternal:1779-1782`), and every
park and every presence restore clears `AttachedTo` (`ClearArmyAttachment()` runs unconditionally
inside `ParkNear` and `RestorePresence`).

Two defects, both fixed 2026-09-12:

- **`Assess` admitted `EnlistedBattle` and fell through to `AttachRequired` inside
  `PlayerEncounter.Finish`.** Inside `PlayerEncounter.Finish`, after `FinalizeEvent` has taken the party out of its map
  event and before the encounter is cleared, the state reads as "no battle anywhere" to every rule in
  `Assess`. A reconcile raised there by TAOM's own army leave reached the reconciler's `AttachRequired`
  arm, which parked the party with no encounter guard (the `noBattleAnywhere`
  guard above it covered `LeaveArmy` and the state demotion, never the `Assess` switch). `Assess`
  now returns `Blocked(BattleEncounterOpen)` whenever `state == EnlistedBattle &&
  player.HasPlayerEncounter`, placed right after captivity and ABOVE commander fitness and the
  settlement exit: the engine owns the party until the encounter closes, exactly as in captivity,
  and a commander killed in that battle is discharged one tick later rather than from inside
  `PlayerEncounter.Finish`. Keyed on `EnlistedBattle`, not on the encounter alone, because since
  #510 every settlement placement opens an encounter deliberately. `BreakStaleBattleLatch` runs
  before the assessment and stays the only deliberate exit from the shape (#551).
- **TAOM's own army disband raised the edge the reconciler subscribes.** `LeaveArmy` and
  `CreateArmyLedBy` call `DisbandCreatedArmy`; `Army.DisperseInternal` sets `Army = null` on the
  commander's party, whose setter dispatches `OnPartyLeftArmy`, which
  `EnlistmentMaintenanceBehavior` turns into `ReconcileNow("army left")`. That pass ran from inside
  the join (`TryJoin:100`) or from inside `PlayerEncounter.Finish` (Patch85 ->
  `FlushArmyLeaveAfterBattle` -> `LeaveArmy`), on half-mutated adapter state and with the encounter
  open by construction; `_reconcileInFlight` only covers re-entry from a pass already running.
  `IArmyMembershipAdapter.IsMutating` is now true for the duration of `JoinCommanderArmy` and
  `LeaveArmy`, and `EnlistmentReconciler.Reconcile` returns early while it is set. A commander
  leaving a vanilla army still reconciles.
- **The save coercion defeated every `EnlistedBattle` gate after a reload** (found by the deep
  review's data-flow pass). `EnlistmentRecord.ToPersistedState` writes `EnlistedBattle` as
  `EnlistedAttached`, so a save at the encounter menu or in the aftermath reloaded as an active,
  unparked Attached soldier: the deployment-screen model and the role strip deferred to vanilla
  for that battle, and `Assess` returned `AttachRequired`, parking the party out of its live
  encounter. `EnlistmentLoadNormalizer` now restores `EnlistedBattle` at load when the party is in
  a map event, before the reconcile runs
  (`EnlistmentLoadNormalizerTests`, three rows).

Two paths still detach with an open encounter, both pre-existing, both recorded on #577 rather
than fixed here: a discharge that runs mid-battle (the heir-mismatch discharge at load, tracked in
#1030) runs RestorePresence, LeaveArmy and ClearArmyAttachment before the ownership policy closes the
encounter, by the documented "discharge outranks ownership" rule (the MCM master switch no longer takes
this path: with the feature off, the release waits until the player's battle ends and discharges on the
next hourly tick, #1021); and a failed join's rollback can leave the encounter open
when the encounter's party is the opposing leader (R4 skips it) while `ReassertServiceMenu` re-inits the wait
menu, whose init parks unconditionally on `EnlistedAttached`.

Why x64 exposed it: `Campaign.TickMapTime` yields 1080 x multiplier campaign seconds per real
second, so at 64 a campaign hour is about 52 ms of wall clock, `MBCampaignEvent.CheckUpdate` fires
one `HourlyTick` per crossed hour in a `while` loop, and the service wait menu's `StartWait`
restores `UnstoppableFastForward` at the retained multiplier every time it is re-asserted. The
reconciler ran about 16 times more often than at 4x. The feature's campaign-time discriminators
(`StaleBattleLatchDays` = 1 hour, `AttachRetryIntervalHours` = 1, `SettlementDwellHours` = 6) all
collapse to tens of milliseconds at that speed; that is #578, not this fix.

Proof lines, with the Enlistment Trace ON: `[EnlistDiag] holding presence: EnlistedBattle with a
live encounter` and `[EnlistDiag] reconcile skipped (trigger=army left): raised from inside our own
army join/leave`. A `TICK trigger=army left ... verdict=AttachRequired ... hasPlayerEncounter=True`
line followed by `PARK ok` is the defect. Pinned by `ServiceAttachmentServiceTests` (the hold and
its ordering against captivity, commander loss and the settlement exit) and
`EnlistmentReconcilerTests` (no park on the aftermath shape, no discharge from inside it, the
own-mutation guard both ways). Not yet seen in game; smoke items in "Testing".

### Where we deliberately diverge from ServeAsSoldier

- **Town access:** SAS force-evicts the player from every settlement each tick (`Test.cs:2424-2440`)
  and substitutes a gear-picker conversation for shopping. TAOM already has him inside a real town.
- **Post-defeat escape:** SAS has no mitigation for the `AttachedTo` window above.
- **Order banner:** SAS leaves the broken one and adds its own, so a sub-sergeant player reads both.
- **Renown:** SAS writes `Clan.Renown` directly, bypassing `OnRenownGained` and every listener.
- **Discharge peace:** SAS peaces out of EVERY war including pre-enlistment ones, a free universal
 peace button, and its changelog admits it ignores minor factions. Both fixed here.
- **Army leadership:** SAS rips a lord out of someone else's army to make him a leader
  (`Test.cs:2465-2476`). We join whatever army he is already in.

### The review pass (2026-08-12), read this before touching the army merge

**A bare-ctor army's `AiBehaviorObject` is null for its whole life, and vanilla dereferences that
field unguarded in five separate places.** Vanilla never hits them because `Gather()` always seeds
the field first. This is the authoritative reader list, verified on installed 1.4.8, **do not
re-derive it**:

| Reader | Guarded? | What it dereferences | Reached from |
|---|---|---|---|
| `GetLongTermBehaviorTextForAILeadedParty`, cases `Hold` and `GoToPoint` | **yes** (`IsWaitingForArmyMembers() && AiBehaviorObject != null`) | n/a | n/a |
| …`GoToSettlement` | no | `AiBehaviorObject.Name` | `MobileParty.GetBehaviorText()` (map party tooltip), `KingdomArmyItemVM` (kingdom Armies tab) |
| …`BesiegeSettlement` | no | `((Settlement)AiBehaviorObject).IsVillage` | as above |
| …`RaidSettlement` | no | `.EncyclopediaLinkWithName` / `.Name` | as above |
| …`DefendSettlement` | no | `((Settlement)AiBehaviorObject).Position` | as above |
| …`PatrolAroundPoint` | no | `.EncyclopediaLinkWithName` / `.Name` | as above, **and the only case reachable with a genuinely unset objective in vanilla too**, because `SetPartyAiAction`'s `PatrolAroundPoint` branch sets `DefaultBehavior` WITHOUT writing `AiBehaviorObject` (every other settlement-bound case writes both in the same block) |
| `Army.GetNotificationText` | no | `AiBehaviorObject.Name` | whenever the leader is not the main party |
| `LordConversationsCampaignBehavior.conversation_lord_tell_objective_gathering_on_condition` | no | `Army.AiBehaviorObject.Name` | **any conversation with any lord in the army** |
| `MobileParty.CheckAiForMapChangeAndUpdateIfNeeded`, case `GoToPoint` | **no, despite testing for null** | branches on `aiBehaviorObject == null`, then reads `Army.AiBehaviorObject.Position` on that same branch | `Campaign.CheckMapUpdate()` on save load, when the scene's navmesh CRC differs from the save's |
| `OnSiegeStarted`, `OnSettlementOwnerChanged`, `CheckAndSetArmyGatheringTime`, `MoveLeaderToGatheringLocationIfNeeded`, `StartTrackingTargetSettlement` | **yes** (`is Settlement` or an explicit null test) | n/a | n/a |
| `IsAnotherEnemyBesiegingTarget` | safe **only** because `ArmyType == ArmyTypes.Besieger` short-circuits first | `settlement.IsUnderSiege` | n/a |

Two of those deserve emphasis. **The conversation one is the worst**, and it is the reason the field
is seeded rather than merely cleaned up: it is gated only on `Army != null &&
Army.IsWaitingForArmyMembers()`, with no `ArmyType` check (unlike its three sibling conditions), and
`IsWaitingForArmyMembers()` returns **true forever** for a bare-ctor army, `_armyGatheringStartTime`
stays 0, and the only thing that sets it (`CheckAndSetArmyGatheringTime`) itself requires
`AiBehaviorObject is Settlement`. So with a null objective, talking to the commander is an
unconditional CTD, and *this feature's own wait menu offers exactly that action*. And
`IsAnotherEnemyBesiegingTarget` is why **`ArmyTypes.Patrolling` is load-bearing, not cosmetic**,
switching it to `Besieger` would defeat the short-circuit and add a per-tick NRE.

**The invariant is closed twice over:**

1. **Seed it.** `CreateArmyLedBy` sets `AiBehaviorObject` to the commander's `CurrentSettlement ??
   HomeSettlement ?? LeaderHero.HomeSettlement` immediately after construction. This is inert for
 the army's real lifetime, the only two behaviours the objective drives
   (`MoveLeaderToGatheringLocationIfNeeded`, `CheckAndSetArmyGatheringTime`) both require
   `LeaderParty.MapEvent == null`, and the army exists only while the commander IS in a map event.
   They can fire solely for an army that leaked, where being walked toward a settlement beats a
   crash. Set before the main party joins, so the setter's tracking branch
   (`Parties.Contains(MobileParty.MainParty)`) cannot fire.
2. **Disband it unconditionally.** `LeaveArmy()` ends what it raised whether or not other lords
   joined. The first revision kept it standing when another lord had attached, reasoning it had
   become a real army; it had not. `DisbandArmyAction.ApplyByObjectiveFinished` is an ordinary
 vanilla dispersion; every party is detached, repositioned around the leader and set to hold.

Neither alone is sufficient, and the disband could not be conditional even on `left`: `DischargeService`
calls `RestorePresence()` (which nulls `main.Army`) *before* `LeaveArmy()`, so a disband keyed on the
army the player just left would silently skip on every mid-battle discharge.

**Nothing cleans a leaked army up on its own.** `Army.CheckInactivity` *decrements*
`_inactivityCounter` for `Besiege`/`Raid`/`Defend`/`AssaultSettlement`, so an army around a lord who
goes besieging never hits the inactivity disband, and `_aiBehaviorObject` is `[SaveableField(16)]`,
so whatever it holds survives every reload.

**Three places the membership could leak, all now closed:**

- `EnlistmentReconciler`'s stale-battle self-heal is the ONLY code that notices a battle resolved
  without a `MapEventEnded` edge (save/load across the end, a throw, a co-op host handoff). It now
 calls `LeaveArmy()` before the transition, without it, the player stayed merged into peacetime and
  the next unrelated ambush re-created report 7b with no army fight to explain it.
- `ServiceMaintenanceService.ResetSessionCaches` drops the adapter's `_createdArmy` handle. That
  handle is a live `Army` reference on a `Reuse.Singleton` whose container is process-scoped, so
  after a reload it names a dead object and the identity test in `LeaveArmy` could never match again.
  It also drops `IEnlistmentReconciler`'s stale-battle-latch anchor for the same reason (#551): an
  absolute campaign day left behind by a campaign that ended while latched makes the recovery fire
  instantly on the next later save.
  It lives there, not in `EnlistmentBehavior`, because that method is the one place that knows the
 lifetime of the feature's per-session state, the same reason the adapter's cached commander party
  is dropped from it (through `IServiceAttachmentService.ResetForNewSession`).
- `CreateArmyLedBy` disbands any prior created army before raising another, so a missed `LeaveArmy`
  cannot orphan one by overwriting the handle.

**`MeritBand.Renown` was dead config**, and the shape of the miss is worth keeping. The field
existed, `BattleRenownPolicy` added it, and six tests covered that policy exhaustively; every one
passing `bandRenown` in as a literal. No default band and no shipped JSON key ever set it, so the
live value was always 0, every battle paid the same flat base, and the policy's doc comment asserting
that "the band figure does the differentiating" was false for the feature's whole life. 100% coverage
of a pure function proves nothing about the values that reach it. Bands now pay 3/2/1/0 against a
base of 2 (win) / 1 (loss); `Renown` is in `IsValidBandLadder`'s non-negative set because it is
directional; and a test reads the shipped `enlistment_config.json` rather than the compiled defaults.

**Enlisting during days 30 to 44 can pull the liege into a war early (decided: allowed).** The war mirror declares the commander's wars for `Hero.MainHero.MapFaction` through `DeclareWarAction.ApplyByPlayerHostility`, and the War of the Ring rule treats that as a player-caused war, which lifts the phase vetoes. A vassal who enlists under a lord of another kingdom between the Isengard War day and Full War therefore declares those wars for the player's liege kingdom, which can bring it into the Rohan war before the script would. The daily sweep keeps the war, because it belongs to the player's kingdom. A Permanent or same-side pair is still refused, and the adapter then reports no war landed, so the service records nothing to unwind ([war-of-the-ring.md](war-of-the-ring.md), "Player-caused wars").

**The war mirror declares as one faction and could make peace as another.** `Hero.MapFaction` is
`Clan.Kingdom ?? Clan` (verified 1.4.8) and the enlist gate deliberately admits a player whose clan
is already a vassal, so the identity is not stable across a term of service. A player independent
at oath declared as his own CLAN; if that clan joined a kingdom before discharge, `UnwindServiceWars`
resolved `MapFaction` live and would have called `MakePeaceAction.Apply` on the **kingdom**, ending a
war for every vassal in it because one soldier left service, with nothing on screen connecting the
two. The reverse strands the kingdom in wars the oath created. `EnlistmentRecord.OathFactionId` now
pins the declaring identity and the unwind refuses to act under a different one, clearing the mirror
either way, since those wars are neither ours to unwind nor ours to keep. An absent pin (a save from
before the field) unwinds as previous builds did, so nobody mid-service is stranded.

**#832: wars during service.** The oath mirrored the commander's wars once and the discharge unwound
them; nothing followed the commander in between. `ServiceDiplomacyService.FollowCommanderWars` now
does, from the hourly tick only (`EnlistmentBehavior.OnHourlyTick`, host-only, delegating to
`EnlistmentReconciler.OnHourlyTick`: `ReconcileHourly`, then the crown-war release, then the sync). An earlier design listened to `WarDeclared`,
`MakePeace` and `OnClanChangedKingdomEvent`; running inside those engine actions re-entered
`ChangeKingdomAction` mid-change (a peace flip-flop when a commander's realm lost its last fief, an
`InvalidOperationException` in its loops) and could act in the load window before the map events were
rebuilt. The cost of the tick is a lag of up to one in-game hour, bridged by the join-time top-up. The
sync is not run from the load normalizer or from the captivity-released path, although both call
`ReconcileHourly`.

- **The arithmetic is pure.** `ServiceWarPolicy.Diff(commanderEnemies, enemiesAtOath, mirrored)`
  declares the commander's wars minus the pre-oath enemies minus the wars already mirrored, and makes
  peace only with mirrored wars the commander no longer fights (never a pre-oath enemy). Deduped,
  null-safe, ordinal.
- **Own clan only (Mike, 2026-10-20).** `ShouldMirrorMidTerm(oathFactionId, currentFactionId,
  playerClanId)` is true only when the pin is set, is still the player's faction, and is his clan. A
  vassal keeps the oath mirror and the discharge unwind, nothing in between: every declaration made
  for him is his whole kingdom's. The gate runs before any hero lookup. A vassal instead sits out a
  battle whose opposing faction his kingdom is not at war with (#960):
  `IServiceDiplomacyService.ShouldSitOutAsVassal(foe)` is true when the record is enlisted, the
  player has a kingdom, mid-term mirroring does not apply, the player-enemy read is non-empty (an
  empty read is a failure and fails open) and the foe is not among the player's enemies.
  `ServiceBattleService.TryJoin` asks it right before the top-up, with the same "already in a map
  event" carve-out as the other floors, and returns before any transition or join (INFO log). The own
  clan is unchanged: the top-up takes the war on first. A soldier with no kingdom whose oath
  identity is not his clan (he swore as a vassal and his clan later left the kingdom) gets neither
  the top-up nor the sit-out, so Attack can still declare an untracked clan war (old behaviour,
  rare).
- **Prune first, then follow.** Each pass drops every `MirroredWars` entry the player is no longer at
  war with (a faction eliminated, or a peace his own side made), logged once each, without calling
  `MakePeaceWith`. A peace the player made himself while the commander still fights that faction is
  therefore re-declared at the next hour: the mirror follows the commander.
- **Only acts that landed are recorded.** `FollowWarOn` and `DeclareWarOn` report whether the war
  exists afterwards, and `MakePeaceWith` reports whether the peace landed. A refused peace stays
  mirrored. Followed wars use the engine's default reason (`DeclareWarAction.ApplyByDefault`): a war
  the commander's realm chose is not the player's hostility, and the hostility reason fails open
  quests from that faction where the default one cancels them. The oath mirror and the join top-up
  keep the hostility reason. TAOM's war and peace vetoes act only between two kingdoms, so a
  clan-identity mirror is never vetoed by them; another mod's prefix still can be. At the oath the
  mirror is cleared first and each landed declaration is recorded, so a stale entry never survives.
- **The unwind is unchanged.** Mid-term mirrors go into the same `MirroredWars` list, so the existing
  discharge unwind ends them too, and pre-oath wars still survive it.
- **Join-time top-up.** `ServiceBattleService.TryJoin` reads the opposing side leader's faction
  (`IEncounterAdapter.GetOpposingLeaderFactionId`, through `MapEventSide.OtherSide`) after the raid
  floor and calls `MirrorWarAtJoin` before the state transition. "Already at war" is true under any
  identity (so a vassal already at war logs no refusal). Every refusal is logged and the join
  continues: a war check never refuses a join and `CanPartyJoinBattle` stays unused. The top-up follows
  with the engine's default reason (open quests are cancelled, not failed). For a raid the
  opposing leader is the village, whose faction is the one vanilla's Attack and Send troops would
  declare on (`MenuHelper.cs:265-269`; `BeHostileAction.cs:32-35` returns early when already at war,
  `:192-195` declares otherwise), so for a clan player the top-up replaces an untracked private war.
- **Crown-war release.** When the commander is at war with the player's faction
  (`IServiceDiplomacyService.IsServingAgainstOwnCrown`: his kingdom, or his clan when independent),
  `EnlistmentReconciler.ReleaseIfServingAgainstOwnCrown` shows a one-button modal (`ShowNotice`, title
  `taom_enlist_lost_title`, body `taom_enlist_crown_war_release`, button `taom_enlist_ok`) and
  discharges with `DischargeReason.CommanderAtWarWithYourRealm` (value 8): honourable, wages settled
  and kit reclaimed. It is a tick-only step, derived from world state every hour with no saved
  field. It waits only on a battle: captivity, a map event for the player or the commander, a battle
  encounter, or a live conversation. A settlement encounter does not wait it (a soldier held in the
  commander's town always has one; the discharge finishes it and the walk-out leaves the
  settlement). The player is released on the map, not into the commander's settlement, which now
  belongs to his crown's enemy. A war that predates the oath is ignored (an independent clan already
  at war with the lord's realm can swear, and is not released for it). While the war lasts and before the release, `TryJoin` skips the join
  (INFO line, the same carve-out as the raid floor), so the soldier sits out battles against his own
  crown.

Tests: `ServiceWarPolicyTests` (Diff, ShouldMirrorMidTerm), `ServiceDiplomacyServiceTests` (the
hourly sync, top-up, crown war, oath, the #960 vassal sit-out predicate), `EnlistmentHourlyTickTests` (the behavior only delegates; that
the load normalizer and the captivity-released path never reach the tick-only steps is pinned by
`EnlistmentReconcilerTests.ReconcileHourly_NeverReleasesForACrownWar`, because they call
`ReconcileHourly` alone), `ServiceBattleServiceTests` (top-up order, a refusal joins, every
refusal logged, the crown-war join skip, the #960 vassal sit-out), `EnlistmentReconcilerTests` (the tick order, the release and its waits) and
`DischargeServiceTests` (released on the map, walk-out once).

**The commander-loss modal only fired once per commander per process.** `_lossAnnouncedFor` exists to
stop the "Word from the column" inquiry repeating every hour *within* one grace episode, but it was
never cleared when the commander recovered, so a lord who was captured, ransomed, and later lost his
party again took the player into a second silent grace: visible and alone on the map, with the
message that explains it suppressed. It is now re-armed alongside `GraceEndsAtDay = null`, and
(plan 014, #656) also on every discharge, through a `DischargeService.EnlistmentEnded`
subscription in the reconciler's constructor, and on a session reset
(`EnlistmentReconciler.ResetForNewSession`). Without those two, a later term or a loaded save under
the same lord lost him in silence.

**Two gates naming one condition.** `GetDailyWage()` (the wallet projection) gated on `IsEnlisted`,
five states; `EnlistmentDailyService.RunDailyTick` skips `PayDailyWage` in `CommanderUnavailable`.
The projection promised income on exactly the days none arrived. When you add a preview for an
existing action, copy the action's guard, do not re-derive one from the same intent. Same class:
`TaomClanFinanceModel` overrode `CalculateClanGoldChange` but not `CalculateClanIncome`, which calls
`CalculateClanIncomeInternal` directly and never routes through it, so the clan screen's Income tile
and the expected-change tooltip beside it disagreed. Both now share `AddServiceWageLine`. The career `DailyGold` count (#769 E11) is added by a second shared helper, `AddCareerDailyGold`, and deliberately sits OUTSIDE that guard: the wage is paid by TAOM's own code, the career gold by the daily clan tick through `applyWithdrawals: true`.

Full RCA with a "why missed" for each finding:
[`docs/reviews/rca-enlistment-field-fixes-2026-08-11.md`](../reviews/rca-enlistment-field-fixes-2026-08-11.md).

### Owed

In-game verification of all seven, in one session: spawn WITH the line; banner reads "Men! Wait!";
wallet tooltip shows the wage; renown moves; enter a town and buy something; lose a battle and confirm
you are not immediately re-engaged; discharge and confirm pre-enlistment wars are exactly as they were.
**Add to that list:** raise an army for a battle, let a real lord join it, end the battle, then open
the kingdom Armies tab and hover the commander's party on the map; both were the crash surface.
Translation of the 5 new keys (#434).

## Shore leave holds the settlement (#512)

The pass suspends service following. It ends when the PLAYER walks out, not when the commander does.

### What was wrong

Shore leave shipped 2026-08-12 and never once worked, for three independent reasons:

| # | Cause | Evidence |
|---|---|---|
| 1 | `OnTownLeave` appeared nowhere in the attachment layer, so `Assess` returned `SettlementExitRequired` the moment the commander's settlement differed and the hourly reconciler dragged the player out mid-shop | `grep OnTownLeave` over `EnlistmentReconciler.cs` and `ServiceAttachmentService.cs` returned nothing |
| 2 | `SyncPositionCached` ended with an unconditional `main.Position = party.Position`, called every cheap pump pass while `EnlistedAttached`, including inside a settlement | pump cheap tier, 4 Hz |
| 3 | The window to click the option was about two real seconds | live log: `FOLLOW 08:22:59` then `EXIT 08:23:01` |

A lord's town stop is a few campaign hours, and the wait menu runs at `UnstoppableFastForward`
(`GameMenu.StartWait`), which is what turns "a few hours" into two seconds.

### The three fixes

**`Assess` takes `onTownLeave`.** Two ordering constraints, both pinned by tests because a future
reorder is silent: it sits BELOW the commander-fitness block, so a dead or captured commander still
reaches `Blocked` and a pass never outranks losing your commander; and the exit clause stays ABOVE
the battle branch, so falling through on a pass still reaches `PartyIsInMapEvent` and a commander
battle still raises `BattleJoinRequired`. A soldier on leave is still enlisted.

**`SyncPositionCached` never drags a party that is inside a settlement.** Correct independent of
shore leave: the engine pins such a party to its gate.

**The arrival offer is a modal, and the modal IS the pause.**
`ServiceAttachmentService` raises `ColumnEnteredSettlement` after the follow transaction lands;
`EnlistmentMenuBehavior` subscribes and the presenter shows a two-option inquiry.
`InformationManager.ShowInquiry` halts the game while it is up, so this needs **no**
`TimeControlMode` edit. That is deliberate: hand-editing time control on a wait-menu path is the
#506 freeze class, which is why `taom.time_status` and `taom.rescue_time` exist. Do not go near it.

Gating: host-only, once per settlement stop (in-memory, deliberately not persisted, since a reload
re-asking is harmless and persisting it would buy save-compat surface for nothing), only when
`CanTakeTownLeave()` already agrees, and behind the MCM toggle `OfferLeaveOnArrival` (default ON,
classified `PlayerLocal` for co-op because the pass it offers is host-gated anyway).

"Once per stop" (plan 014, #656): the settlement-id latch is cleared when the stop ends, which is
the commander leaving the town. `EnlistmentMaintenanceBehavior` routes the commander's
settlement-left edge to `IEnlistmentWaitMenuPresenter.OnStopEnded` before its re-attach pass, so
the latch clears however the player leaves: walked out by the exit sweep, or on foot later from a
shore-leave pass. A pass suspends the exit sweep, so an accepted offer never reached it; the first
cut cleared the latch only there (`ServiceAttachmentService.ExitSettlementForService` raising
`ColumnLeftSettlement`, which `EnlistmentMenuBehavior` still routes to the same handler), and the
review of that cut found the pass route uncovered. Before plan 014 the latch held until a different
settlement was offered or the session ended, so a later stop in the same town was not offered
until then. The 24-hour cooldown is not cleared by the stop's end, so a commander dipping straight
back in still gets one modal a day at most. A discharge inside the town still leaves the latch
set until the next stop elsewhere or a session reset.

`TownLeavePolicy.ShouldRevokeLeave` is unchanged: it already revokes exactly when the player is no
longer inside the settlement, which is the wanted behaviour. Only the exit sweep needed teaching.

### Still open

Crash bundle `d7d9f7d3` shows the same FOLLOW/EXIT pair oscillating on a 6 to 7 second period for
fifteen minutes while the commander sat in `town_EW1`. If that survives this fix with no pass held,
the commander's settlement is being read unstably and that is a separate defect. Check the log
before assuming it is gone.

## The settlement-encounter invariant (#510)

**Whenever TAOM puts the main party inside a settlement and a vanilla settlement menu can reach the
player, a `PlayerEncounter` and its `LocationEncounter` must exist.** One chokepoint enforces it:
`IEncounterAdapter.EnsureSettlementEncounter`. `IMobilePartyAttachmentAdapter.MoveIntoSettlement`
does NOT, and `SettlementEncounterInvariantTests` bans every caller of it outside a tiny allow-list.

### Why the engine leaves no room here

Vanilla has exactly one way for the main party to be inside a settlement, and it always builds both
objects. `EncounterManager.StartSettlementEncounter` calls `PlayerEncounter.Start()` then
`PlayerEncounter.Init(...)`, and `Init` calls `PlayerEncounter.EnterSettlement()`, which creates the
`TownEncounter` / `CastleEncounter` / `VillageEncounter` and only then runs
`EnterSettlementAction.ApplyForParty`. Verified on installed v1.4.8:

| Engine fact | Consequence when the encounter is missing |
|---|---|
| `game_menu_settlement_wait_on_init` opens on `PlayerEncounter.EncounterSettlement.IsVillage`, guarding `PlayerEncounter.Current` only on the NEXT line | "Wait here for some time" is a CTD |
| `EncounterSettlement => Current?.EncounterSettlementAux` | that deref is on a null |
| That one `on_init` backs `town_wait_menus` AND `village_wait_menus`, and the castle menu's `town_wait` routes into `town_wait_menus` | towns, castles and villages fail identically |
| `game_menu_wait_here_on_condition` consults only `SettlementAccessModel` | the option is drawn ENABLED, so nothing warns the player |
| `PlayerTownVisitCampaignBehavior` derefs `PlayerEncounter.LocationEncounter` unguarded in about a dozen places | tavern, arena, keep and the town-centre walk fail too, so guarding one menu is not a fix |
| `EnterSettlementAction.ApplyForParty` has no already-inside guard and re-dispatches `OnSettlementEntered` | the chokepoint sets `LocationEncounter` directly when the party is already inside, rather than calling `EnterSettlement()` a second time and completing a cancelled duty |

The recipe the chokepoint follows is vanilla's own, from
`EncounterGameMenuBehavior.game_menu_siege_attacker_left_return_to_settlement_on_consequence`.
It uses `PlayerEncounter.Start()` + the public `SetupFields(MainParty, settlement.Party)` because
`Init` is `internal`, and `SetupFields` sets `EncounterSettlementAux` from a settlement defender
party. `MessengerCampaignBehavior` already used it this way.

### How it shipped twice

Discharge released the player into the commander's town with the raw vanilla menu (v2.0.20; crash
bundle `d7d9f7d3`, "Wait here for some time" 14 seconds after the release). The redirect list could
not cover it: it is gated on `EnlistedAttached`, and the pipeline sets the record to `NotEnlisted`
before placing the player. Shore leave then handed the same menu to a still-enlisted player
(v2.0.21, v2.0.22), by design, without building the encounter first.

`ServiceAttachmentService.FollowCommanderIntoSettlement`'s doc comment named this crash the whole
time. So did the design note above. **A comment did not stop it; the ban test is there because
prose cannot enforce an invariant.**

### Save and reload: vanilla rebuilds the half that is not saved

`Campaign.LocationEncounter` is `[CachedData]`, NOT `[SaveableProperty]`, so it genuinely does not
survive a reload. The encounter itself does: `Campaign.PlayerEncounter` is `[SaveableProperty(54)]`
and `EncounterSettlementAux` is `[SaveableProperty(28)]`.

The missing half is rebuilt by vanilla, not by TAOM. `SandBoxGameManager.OnLoadFinished` calls
`PlayerEncounter.Current.OnLoad()` when `MapState.GameMenuId` names a resolvable menu, and that does:

```csharp
if (InsideSettlement && Battle == null)
    CreateLocationEncounter(MobileParty.MainParty.CurrentSettlement);
```

`InsideSettlement` is `MainParty.IsActive && CurrentSettlement != null`, which holds for every state
this feature creates (the follow path restores presence before moving in, so the party inside a
settlement is always active). In each of those states the player is sitting on a resolvable menu:
`town` after a discharge release, `town`/`castle`/`village` on shore leave, the TAOM wait menu
otherwise. So a save taken during shore leave reloads safely.

Worth re-verifying on the next engine bump, because it rests on a vanilla mechanism TAOM does not
own: if `OnLoad` stops being called, or its `InsideSettlement` gate changes, the reload path silently
reverts to the #510 shape.

### Teardown

Shore leave's encounter is settlement-shaped, and `EncounterOwnershipPolicy` R3 deliberately never
closes those (that rule is what stops the oath destroying a town visit the player owns). Nothing
else would ever take it down, and a leaked `PlayerEncounter` blocks every main-party encounter for
the rest of the term. So the revoke owns it, through the policy under
`EncounterFinishIntent.ShoreLeaveEnd` (R2b), which inverts R3 and only R3. It goes through the
policy rather than a bare `Finish` because leave is revoked for every reason including "a battle
started", and at that moment the live encounter may be the one `ServiceBattleService` seeded.

Discharge needs no special case: `EncounterFinishIntent.Discharge` already outranks R3, so a
discharge taken while on leave closes that encounter at step 7 and step 10 builds a fresh one.

The revoke passes `forcePlayerOutFromSettlement: presence.IsInSettlement`, not a bare `false`. Leave
is revoked for every reason that is not attached-and-in-a-settlement, so it also fires on a STATE
change while the player is still standing in the town, and vanilla `Finish` always stops time and
calls `GameMenu.ExitToLast()`. Leaving them inside with no menu is the terminal shape from the 2026
review pass, because `MobileParty.DoUpdatePosition` will not move a party with `CurrentSettlement`
set. Out on the map with time stopped is merely paused.

## A stranded PlayerEncounter could latch service permanently (#538, 2026-09-04)

Player report: *"the army left me behind after sieging East Osgiliath making me unable to move and do
anything, even the Enlist UI is gone."* No log exists: every enlistment-bearing log on the dev machine
is from #520. This was found by reading code.

### Two self-heals were each other's precondition

`ServiceMaintenanceService.TryBreakBattleLatch` is the only exit from `EnlistedBattle` when no battle
is running, and it returns early while `presence.HasPlayerEncounter`. The reconciler's
stranded-encounter sweep was the only thing that closes a stale encounter, and it required
`State == EnlistedAttached`. So `EnlistedBattle` plus a stranded encounter was a **permanent mutual
block**: the encounter stopped the return to Attached, and not being Attached stopped the sweep.

Every symptom is that one flag. An open `PlayerEncounter` holds the map ("unable to move"), blocks
every future main-party encounter, and the service menu disappears because the pump gates its menu
work on `EnlistedAttached` too. The reconciler's own `noBattleAnywhere` includes `!HasCurrent`, so it
was blocked as well: three mechanisms, one cause.

### Where the encounter comes from, and why it is not a corner case

`LeaveSettlementAction.ApplyForParty` (installed v1.4.8) calls `PlayerEncounter.Finish()` in exactly
one branch: when the LEAVING party leads its army **and** the main party is attached to it. An
enlisted player is the main party and leads nothing, so the branch never runs. `CurrentSettlement` is
cleared unconditionally at the end, so the party does leave and the encounter stays open.

Since #510 every TAOM settlement placement opens an encounter deliberately, so this is the ordinary
path. A siege supplies the long map event that holds the state in `EnlistedBattle`.

### The state gate, the map event and #796

The first version of this fix removed the `EnlistedAttached` gate on the reasoning that "the state was
never the real guard", and a review argued the gate was load-bearing for the loot window. That premise was wrong (#796, 2026-10-26). The main party stays in its map event through the
loot and aftermath screens; on that path vanilla removes it inside `PlayerEncounter.Finish` (v1.5.5
`MapEvent.cs:1743`, `MapEventSide.cs:434`), so the sweep's `presence.IsInMapEvent` guard already keeps it
out of that window. Other exits remove it with the encounter still open: a defender's repulsed assault
(`SiegeAssaultEventComponent.OnPlayerEncounterContinueBattle`), an army leader leaving the event
(`PartyBase.cs:323-326`), and `LeaveBattle`. The R1b rule and the `IsBattleEncounter` snapshot field written for it never ran in a
game, because the adapter never passed the flag, and both were deleted in #796. Behaviour for players is
unchanged: trunk never passed the flag, so every consumer already read false.

### The rules that changed

| Rule | Change |
|---|---|
| **R2c** (new) | `StrandedOutsideSettlement` inverts R3 the way `ShoreLeaveEnd` does, but from the other direction: the encounter outlived the settlement. It requires `!PlayerInsideSettlement` **itself**, read fresh by the adapter, rather than trusting the caller. |
| `ShoreLeaveEnd` | No longer the only intent that treats a settlement-shaped encounter as ours. Its doc said so and is corrected. |

R2c enforcing its own precondition is the second review correction. The first version documented it as
"the caller owes the precondition", and the caller read that fact from a snapshot captured earlier in
the tick, while the ownership snapshot had carried a freshly-read `PlayerInsideSettlement` all along
that the policy simply never looked at. Worse, the committed test asserted `Finish` for a snapshot
built with `playerInsideSettlement: true`, so it encoded as correct the exact state R3 exists to
protect.

### The sweep is shared, and the grace window is why

`SweepStrandedEncounter` is called from `ReconcileAttached` and `ReconcileGrace`. The grace path had
no sweep at all, so a player who arrived in `CommanderUnavailable` holding an encounter waited out up
to seven campaign days, unable to move, until discharge finally closed it.

Precision, because a first draft of this got it wrong: a commander lost DURING an assault does not
land in grace. `EnlistedBattle` to `CommanderUnavailable` is a deliberately illegal edge, so that
player stays latched in `EnlistedBattle` and is freed by the sweep on the attached path. The grace
call is an independent backstop for the states that do reach it.

### `taom.service_status` / `taom.rescue_service`

A code fix reaches the next campaign; a stranded player is stuck in the save they already have, with
no menu and therefore no discharge dialog. The rescue closes the encounter, walks the party out of any
settlement, re-parks on the commander (or restores presence and defers to the grace path if he is
unfit), and reopens the wait menu. It refuses while a conversation or a live battle is running, which
are the two states where the player is demonstrably not stuck.

**`PlayerEncounter.InsideSettlement` is a trap for exactly this caller**, and the first version fell
into it. It reads `if (MobileParty.MainParty.IsActive) return CurrentSettlement != null; return
false;` and a parked enlisted player has `IsActive == false`, so it returns **false even with
`CurrentSettlement` set**. Passing that as `forcePlayerOutFromSettlement` means the engine's own
walk-out inside `Finish` (guarded by the same property) never runs, and the rescue leaves the party
inside the settlement: immobile, which is the thing it promises to fix, with the #510 menu crash
armed. Read `MainParty.CurrentSettlement` directly and call `LeaveSettlement()` explicitly, as
`DischargeService.RestoreCampaignContext` does.

### The column leaving a town without you is (probably) not a bug

A second, vaguer report. Two mechanisms produce it deliberately and both self-correct by re-parking
the player on the commander:

- `ServiceAttachmentService.SettlementDwellHours` is **6 campaign hours**. It exists because following
  an AI lord straight back out of a town produced ten transitions across three towns in three real
  minutes (measured 2026-08-25), twice in and out within the same second. The commander can walk off
  during that hold.
- Shore leave (#512) suspends the exit sweep until the PLAYER leaves, so the column leaving without
  you is the pass working.

Nothing was changed for either. Recorded so the next report can be matched against them, and so any
decision to shorten the dwell is made against a repro rather than a hunch.

### Known gap: the mask still disarms outside `EnlistedAttached` (#511)

The invariant above is enforced at the two sites that hand over a vanilla menu. It does NOT cover
the state the follow path leaves behind. `TryRedirectMenu` and `EnsureServiceMenu` are both gated on
`EnlistedAttached` alone, so in `CommanderUnavailable` the mask is off while the player is inside a
settlement with no encounter, and `Campaign.Tick` force-pushes the generic state menu on every tick
the player is not at one. For an encounter-less party inside a fortification that resolves to
`town_outside`.

**Corrected 2026-08-24, after the Codex round.** This section first claimed the failure was a
soft-lock, on the grounds that `town_outside`'s Leave option calls `PlayerEncounter.Finish()`, which
no-ops when `Current` is null. The player never reaches that option:
`game_menu_town_outside_on_init` opens with `args.MenuTitle = PlayerEncounter.EncounterSettlement.Name`
and `EncounterSettlement` is `Current?.EncounterSettlementAux`, so the menu NREs at init inside
`GameMenu.RunOnInit`, which has no try/catch. The hazard is a CTD of the #510 class, not a soft-lock.

**And the chain is harder to reach than first written.** `Campaign.Tick`'s push is gated on
`MapState.AtMenu == false`, and nothing found in the `CommanderUnavailable` path closes the TAOM wait
menu: every service option gates on `IsServing` (true across that state, as `IsEnlisted` is) and is
registered `isLeave: false` so there is no Escape slot; the only isLeave option is hidden while anyone serves; and `EnsureServiceMenu`'s gate only stops the menu being
RE-asserted, it never tears one down. So the smoke that would settle #511 is not "sit in a town with
a dead commander", it is "find something that closes the wait menu in that state", plus the separate
case of a vanilla `SwitchToMenu` swapping it, which needs no `AtMenu == false` at all.

Tracked in #511 rather than fixed here because both candidate fixes need an in-game smoke, and the
first collides with the battle path's R3 handling.

## A battle ending on the far side of the map tore the player out of his own (#551, 2026-09-06)

Crash bundle `31942985`, from a live game on v2.0.27.0. The stack is pure vanilla with no TAOM frame
on it, which is why this one is worth reading in full: the crash is four and a half minutes and one
feature away from where it surfaces.

```
System.NullReferenceException @ MapEventSide.AllocateTroops
  <- MapEventSide.MakeReadyForSimulation  <- MapEvent.SimulateBattleSetup
  <- MapEvent.SimulateBattleSessionForMapEvent  <- MapEvent.Update
  <- MapEventManager.Tick  <- Campaign.Tick
```

### The gate was our state, and it should have been the event's identity

`EnlistmentBattleBehavior.OnMapEventEnded` read `State == EnlistedBattle || commander-in-event`. The
state disjunct alone was enough, so while the player sat in `EnlistedBattle` **every** map event
resolving anywhere in the world counted as "the commander's battle ended". The reporting session
logged around 650 AI battles, several per second.

At 20:23:57 `TryJoin` set `EnlistedBattle` and landed a verified join. In the same second a
`sturgia lord_2_1` against `umbar_corsairs` field battle finished on the far side of the map (id
`91082.413`, read from the `[AutoResolve]` record emitted by the same dispatch), and
`OnCommanderBattleEnded` tore down the army it had raised 0.3 seconds earlier.

The gate is now `commander-in-event || main-party-in-event`. The main-party disjunct is what the
state clause was reaching for: when the commander is wiped mid-event his party is gone from
`InvolvedParties` but the player is still in it, and that end is genuinely ours.
`CampaignEventDispatcher.OnMapEventEnded` is dispatched from `MapEvent.FinalizeEventAux` :2079,
**before** the side teardown, so `InvolvedParties` is still populated at that point;
`FindCommanderPartyIdIn` already depended on that fact.

### Why the teardown is fatal rather than merely wrong

`ArmyMembershipAdapter.LeaveArmy` clears `MobileParty.MainParty.AttachedTo`, and the engine answers
that with more than a detach. `MobileParty.SetAttachedToInternal` :1780-1783 runs
`Party.MapEventSide.HandleMapEventEndForPartyInternal(Party); Party.MapEventSide = null;`, so the
main party is pulled out of whatever map event it is in. Two things follow, and the crash needs both:

| Consequence | Engine site |
|---|---|
| `MapEvent.TroopUpgradeTracker` is nulled permanently, because the removed party is the MAIN party | `RemoveInvolvedPartyInternal` :855-858 |
| The event becomes engine-tickable again, because `MapEventManager.Tick` skips only `MobileParty.MainParty.MapEvent` | `MapEventManager.Tick` :59 |

`MapEventSide.AllocateTroops` :552 then dereferences that tracker with no null check, gated only on
`BattleObserver != null`. The observer was attached at 20:28:25 by a `BattleSimulation` constructor
that assigned it and then threw `IndexOutOfRangeException` on
`SelectedTroops[(int)_mapEvent.PlayerSide]`, because `BattleSideEnum.None` is `-1` and the player had
no `MapEventSide` any more. Its one clearer, `PlayerEncounter.LeaveBattle` :1990, never ran.
`ServiceBattleService.OnCommanderBattleEnded` now refuses the detach outright when the main party is
in a map event other than the one ending, and logs at ERROR when it does, because reaching that state
means the gate leaked.

### The four and a half minutes before the crash

The teardown returned early at `if (_encounter.HasCurrent) return;`, leaving `EnlistedBattle` with a
live `PlayerEncounter` and the commander still fighting. Nothing in the feature could move it:

| Guard | Why it held |
|---|---|
| `TryBreakBattleLatch` returns on `HasPlayerEncounter` or `commander.PartyIsInMapEvent` | Both true |
| `SweepStrandedEncounter` returns on `commanderInMapEvent` | The commander was still fighting |
| The ownership policy skips the sweep's intents | The encountered party is the opposing leader, not the commander (R4) |
| `ServiceBattleService.TryJoin` refuses every rejoin | The state is not `EnlistedAttached` |

**#538's fix does not cover this shape.** Its sweep is gated on `!commanderInMapEvent`, which is
exactly the leg this crash sets. Four guards, and no exit between
them.

The discriminator is time: the loot screens do not have this shape, so time is a
conservative margin. `EnlistmentReconciler.BreakStaleBattleLatch` times one continuous episode of
(`EnlistedBattle`, encounter open, player in no map event) and, past
`EnlistmentCoreConfig.StaleBattleLatchDays` (one in-game hour), takes the new `StaleBattleLatch`
intent that R1c lets through. It runs before the assessment so the recovery lands in the same pass,
and it hands R1c the decision rather than calling `Finish` itself, so R1 still refuses outright if the
player turns out to be in a map event after all. Like R2c, R1c enforces its own
`!PlayerInsideSettlement` precondition rather than trusting the caller.

**Widened for #1021.** The same timer now covers two more orphans. (1) `EnlistedAttached`, parked
outside a settlement, encounter open against the opposing leader, `IsJoinedBattle` true, no map event:
a save taken while latched in `EnlistedBattle` loads as attached, and the policy skips it because the
encountered party is the opposing leader. The recovery finishes it through R1c and
re-parks him; no state transition is made, because attached to attached is not a legal edge.
`FinalizeBattle` leaves the battle with no winner and the finalized event no-ops, so the finish is safe
here. (2) `EnlistedBattle` INSIDE the commander's town after a defender's repulsed assault
(`OnPlayerEncounterContinueBattle` keeps the encounter). R1c refuses inside a settlement and `Finish`
would escape-teleport an unmerged soldier and walk him out (`FinishEncounterInternal` only teleports,
and the walk-out is `PlayerEncounter.Finish`'s own `LeaveSettlement()`; both need `AttachedTo` null), and for a merged one neither runs but he is left inside with no encounter, the
#510 crash shape. So the recovery only moves the state to `EnlistedAttached` after the window, logs a
warning, and leaves the encounter. The next assault reuses it when the party it was joined against is
in that assault; otherwise the join is refused and rolls back. Reuse clears the last battle's result
(`IEncounterAdapter.ForgetEndedBattleResult`, only when the encounter has no battle and a result is
still set). The repulsed assault leaves the result at `EnemyRetreated`; the siege component's continue
check (`CheckIfBattleShouldContinueAfterBattleMission`) returns false on it only when the other side is not
the attacker, and for a defender it is, so the continue handler would finish the new assault
(`FinishBattleAndKeepSiegeEvent`) and switch to `menu_siege_strategies`. Every component's continue check
returns false on a null result, hence the reset (the saved continue flag resets at the first `DoWait`).
Before the fix every later assault on that town ended the moment he joined. A conversation keeps the
latch.

**The anchor is not persisted, and that is not the same as safe.** The reconciler is
`Reuse.Singleton`, so it outlives the campaign, and the anchor is an ABSOLUTE campaign day. A campaign
that ends while latched leaves a finite value behind; load a later save and the elapsed time is
enormous, so the recovery fires on the very first latched tick and finishes an encounter with no real
waiting at all. That defeats the margin, committed by the safety net written to provide it. Two guards. `IEnlistmentReconciler.ResetForNewSession` is
called from `ServiceMaintenanceService.ResetSessionCaches` (the feature's one place that knows this
lifetime, which is also why the army handle is dropped there rather than from the load hook), and
that reset now runs on a load and on a new campaign. A backwards-clock re-anchor inside
`BreakStaleBattleLatch` is the second, self-contained guard. When it was written,
`ResetSessionCaches` ran from `OnGameLoaded` only, so a brand-new campaign never reached it; a new
campaign starts at a low day count, so the leftover anchor sat in its future, and a clock that ran
backwards cannot be one continuous episode. Found by the `/deep-review` data-flow agent, not by the
tests, which all passed. `EnlistmentBehavior.OnNewGameCreated` now runs the reset too, the load hook
runs it on every peer (above the co-op authority gate), and the re-anchor stays as a self-contained
guard for any path that skips the reset.

**Every campaign-clock latch on an Enlistment singleton is reset on every peer's load, on a new
campaign and at game end.** `EnlistmentBehavior.OnGameLoaded` calls `ResetSessionCaches` before its
co-op authority gate, so a co-op client drops its session state too; only the normalization, which
discharges and parks, stays host-only. Every callee is an in-memory field clear, which is what makes
the ungated call safe. `SubModule.OnGameEnd` calls it as well, so the cached commander
`MobileParty` and `Army` handles no longer point into the finished campaign (plan 014, #656). Other
roots can still hold that campaign at the main menu (`CommanderLordAdapter._lastSeenMapEvent`, and
the singleton behaviors' `_lastSessionStarter`), so the heap benefit is unmeasured and not claimed. `ResetSessionCaches` also clears the settlement-dwell anchor and the adapter's cached
commander party (`IServiceAttachmentService.ResetForNewSession`), the arrival-offer settlement id
and 24-hour cooldown (`IEnlistmentWaitMenuPresenter.ResetForNewSession`). It also drops the
sit-out ledger's verdict (`SitOutLedger.Consume`), the pending raid join, the pending battle-merit
sample (`BattleMeritAccumulator._pending`, cleared by `Consume` and by `ResetForNewSession`) and the
contract-renewal state (`ContractRenewalService.ResetForNewSession`). The army-rhythm snapshot
used to have an hourly cache and a reset here; both are gone, because it now probes fresh on every
call. The dwell anchor and the
offer cooldown held an absolute campaign hour. Before this, loading an earlier save left those
stamps in the future, which the code read as "a moment ago": the exit sweep held the player in a
town the commander had left until the new clock passed the old stamp plus 6 hours, and the
shore-leave offer stayed silent until it passed the old stamp plus a day. The reconciler's reset also clears the commander-loss modal's shown-once latch
(`_lossAnnouncedFor`), which discharge clears as well.

**A save with no Enlistment data loads with no record.** The engine calls a behavior's `SyncData`
only when the save holds an entry for it (`CampaignBehaviorDataStore.LoadBehaviorData`), and the
store is otherwise cleared only in `OnSessionLaunched`, which runs after `OnGameLoaded`. So a save
made without this feature used to reach the load normalizer still holding the previous session's
term, which could discharge or park a player this save never enlisted. `OnGameLoaded` now clears
the store first when no loading `SyncData` ran this load (`_justLoadedFromSave` is false), on every
peer, and the host then normalizes the empty record (the ownerless-parked rescue still applies).

The tests are `EnlistmentSessionResetTests` (the load hook's routing and order, the co-op client's
reset, the no-data clear, and a source check that `SubModule.OnGameEnd` reaches the reset), the
`ResetSessionCaches_*` tests in `ServiceMaintenanceServiceTests`, the latch tests in
`CommanderLossAnnouncementTests`, and the stop-end tests in `EnlistmentWaitMenuPresenterTests`,
`SettlementFollowingTests` and `EnlistmentStopEndTests`.

Not every per-session value is reset yet. `FieldDutyRuntime`'s real-time pace estimate survives a session
change (cosmetic: the first duty after a load can fold its assignment toast into the result).
`CommanderLordAdapter`'s one-slot `MapEvent`
cache (`_lastSeenMapEvent`) is never cleared, so it keeps one finished battle referenced. These are
follow-ups, not part of this reset, and the list comes from the review's field sweep, so it may
not be complete.

### The engine backstop, and the bundle that was suppressed

`Patch82_MapEventObserverInvariant` restores the `BattleObserver`/`TroopUpgradeTracker` pairing before
`SimulateBattleSetup` reads it, for every path including ones that do not exist yet:
[map-event-guard.md](map-event-guard.md), registry entry in
[harmony-patch-registry.md](../reference/harmony-patch-registry.md).

Separately, this crash was nearly unsolvable. `CrashSignatureCalculator` hashed only the outer
exception type, and every Gauntlet-dispatched crash arrives as `TargetInvocationException @
ScreenManager.Update` over the same eight frames — so the 20:28:25 `IndexOutOfRangeException`, the
bundle that would have named the corruption, was suppressed as a duplicate of the 20:23:58
`NullReferenceException`. The chain was reconstructable only because the raw `rgl_log.txt` rode along
in the surviving bundle. The signature now includes the inner chain (#552).

## Leave it to the others: the sit-out gate (#830)

An enlisted soldier with no troops of his own could pick "Leave it to the others" in his
commander's battle, let it auto-resolve and be paid as if he had fought. Vanilla's
`MenuHelper.EncounterOrderAttackCondition` counts healthy men across every party on the player's
side, so the option shows even when his own party is empty. The fix is one Postfix,
`EncounterGameMenuOrderAttackConditionPatch` on the private
`EncounterGameMenuBehavior.game_menu_encounter_order_attack_on_condition` (`Patch66_Enlistment`),
that asks `IEnlistmentMenuService.EvaluateSitOut()`. It only narrows: it returns at once unless
vanilla showed the option and left it enabled, it writes only `args.IsEnabled = false` and
`args.Tooltip`, and it never hides the option or adds a second one. Vanilla's other path to the same
consequence, the town assault init, stays.

**With or without men of his own.** The rule applies either way. A soldier who does have men of his
own sees the label "Send troops" instead of "Leave it to the others", and both are the same option
with the same verdicts.

**The gate** is one private method, `ServesInCommandersBattle()`, shared with `ShouldBlockBattleExit`:
state `EnlistedBattle`, and `BattleCommandPolicy.ShouldStripPlayerCommand` with the player not leading
his side. Outside it the snapshot is never read and the verdict is `Unchanged`. The patch asks only
when vanilla showed and enabled the option. The winner check
(`IEncounterAdapter.PlayerMapEventHasWinner`) is read before the gate on purpose: inside the gate, a
leadership handover in the aftermath would let a later refresh overwrite the verdict taken when the
player clicked. Every call that passes it records its verdict in `SitOutLedger`, the closed gate
included, so each click is preceded by a fresh record. A battle that ends without a payout can leave
its last verdict behind; that is harmless, because the simulation flag is set only after a click on an
enabled option whose refresh recorded a fresh verdict.

**The decision** is the pure `SitOutPolicy.Decide(SitOutSnapshot, threshold)`, in this order:

1. A needed field the adapter could not read: `Unchanged` (vanilla stands).
2. Wounded: `WoundedFree`.
3. Vanilla's Attack unavailable: `ForcedOpen`.
4. Siege assault or sally-out: `RefusedSiege`. The lord's hall phase is part of the assault map
   event, so it needs no rule of its own.
5. Player side share at or above `sitOut.overwhelmingShare` (0.85), and at most 1: `OverwhelmingCost`.
6. Otherwise: `RefusedField`.

Every `Refused*` verdict implies Attack is available, so the player always keeps an enabled way out.
`IsSiege` is `MapEvent.IsSiegeAssault || IsSallyOut` only. Every other fortification battle (a relief
battle outside the walls, a siege ambush, a naval blockade) follows the field rules.

The share is `mine / (mine + theirs)` from `MapEvent.GetStrengthsRelativeToParty(PlayerSide, ...)`,
which is read-only (it sums `GetCustomStrength` over the involved parties and starts each side at 0.1,
so the total is never zero). Never `MapEvent.RecalculateStrengthOfSides()`, which writes the saved
`StrengthOfSide`. Attack availability comes from a probe that builds its throwaway `MenuCallbackArgs` the way
`IsAttackBarredByTruce` does, but mirrors vanilla's whole Attack condition (the honour check, then
`EncounterAttackCondition`), while the truce probe mirrors only the honour check. A binding test pins
that vanilla's `game_menu_encounter_attack_on_condition` still calls exactly those two methods.

**Tooltips.** A refused option replaces vanilla's tooltip with TAOM's line. The two allowed verdicts
(`WoundedFree`, `OverwhelmingCost`) keep vanilla's power-context tooltip below TAOM's line. The odds
line says "your commander will remember it", which is only true while `sitOut.trustCost` is above 0
(the shipped value is 1); a tuner who sets 0 should reword `taom_enlist_sitout_odds_tip`.

**The payout.** `EnlistmentContentBehavior.OnMapEventEnded` passes `mapEvent.IsPlayerSimulation` to
`EnlistmentBattlePayoutService.PayOutBattle(won, playerSimulated)`; the parameter is required, so the
call site cannot forget it. `BattleSimulation` sets that flag, and two IL pins (TaleWorlds.CampaignSystem
only, SandBox and the other official modules are not scanned) hold that `PlayerEncounter.InitSimulation`
is called only from `MenuHelper.EncounterOrderAttack`, the `BattleSimulation` constructor only from
`InitSimulation`, and `MapEvent.IsPlayerSimulation` is set only by that constructor.

The payout consumes the ledger on every battle. A battle counts as sat out when the player simulated it,
the recorded verdict is one of `WoundedFree`, `ForcedOpen` or `OverwhelmingCost`, and the mission never
ran for him: no merit sample, or a walkout (`LeftTheField`) from a mission he was never downed in
(`SurvivalRatio` at 1). A recorded `Unchanged` or `Refused*` verdict means the simulation did not come
from an option the gate allowed, and the battle pays as normal. For a sat-out battle:

| Reason | Base XP | Renown | Trust |
|---|---|---|---|
| `WoundedFree` | full | withheld | unchanged |
| `ForcedOpen` | full | withheld | unchanged |
| `OverwhelmingCost` | times `sitOut.baseXpFactor` (0.5) | withheld | minus `sitOut.trustCost` (1) |

A walkout that sat out still receives the band XP and gold its sample earned; the sit-out rule governs
only the base pay and renown. Win and loss counters still count the battle. The announcer says which line applies
(`AnnounceSatOut`): `taom_enlist_sat_out_wounded_line`, `taom_enlist_sat_out_line` (the cut share) or
`taom_enlist_sat_out_forced_line`. `SitOutLedger` is a never-saved singleton, consumed by the payout and
by `ServiceMaintenanceService.ResetSessionCaches`, which also drops an unconsumed merit sample.

| Situation | Send troops / Leave it to the others | Attack |
|---|---|---|
| Field battle, healthy, side share below 0.85 (with or without an army) | Disabled, "Take your place in the fight" | Enabled |
| Field battle, healthy, side share at or above 0.85 | Enabled, trust cost and cut base XP | Enabled |
| Safe-passage truce | Disabled by vanilla | Disabled by vanilla; #827 releases 'Leave' |
| Wounded | Enabled, full base XP, no renown | Disabled by vanilla |
| Party morale 0, healthy | Disabled by vanilla | Enabled |
| Wounded and morale 0 | Disabled by vanilla | Disabled by vanilla; 'Surrender' only (pre-existing) |
| Siege assault or sally-out, prep complete | Disabled, "A siege needs every soldier" | Enabled |
| Siege assault, prep incomplete | Enabled (`ForcedOpen`) | Hidden by vanilla |
| Fortification battle that is not an assault (relief battle outside the walls, siege ambush, naval blockade) | Follows the field rows | Enabled |
| Raid on a village | Normally not in it when the commander raids (#831; the army-attached edge and a failed read are the exceptions); a raid a lord relieves (#954) puts the soldier in it, and it follows the field rows (`IsSiege` covers only an assault or sally-out); as defender, follows the field rows | Enabled |
| Enemy on a raft | Hidden by vanilla | Disabled by vanilla (pre-existing) |
| Own party on a raft | Enabled (`ForcedOpen`) | Disabled by vanilla |
| Naval raid at sea with too few men | Disabled by vanilla | Disabled by vanilla (pre-existing gap) |

Fails open: any error leaves vanilla's answer and logs once. Co-op: a postfix on a local menu
predicate that skips no replicated mutation; a client never holds `EnlistedBattle`, so the gate is
inert there. Text keys: `taom_enlist_sitout_*_tip` and the three `taom_enlist_sat_out_*_line` keys.
Config: the `sitOut` section of `enlistment_config.json`.

Tests: `SitOutPolicyTests` (every rule, the threshold boundary, NaN and out-of-range shares, an
exhaustive invariant loop), `EnlistmentMenuServiceTests` (the gate, the ledger, the aftermath skip),
`EncounterGameMenuOrderAttackConditionPatchTests` (narrow-only, fail-open, log once),
`EnlistmentBattlePayoutServiceTests` (every sat-out row), `ServiceDailyAnnouncerSatOutTests` (line
choice), `ServiceMaintenanceServiceTests` (the session reset), `EnlistmentContentConfigProviderTests`
(the `sitOut` validation and the shipped-file parity) and `Patch66EnlistmentBindingTests` (the target,
the category, `IsPlayerSimulation` and the IL pins).

## The commander's village raids are not battles to join (#831)

A raid, forced supplies and forced volunteers are village hostile actions. Joined like a battle,
they held the soldier in `EnlistedBattle` for many in-game hours, refused release
(`RefusedInBattle`) for that time, and ended in vanilla's `raiding_village` menu with its 'Abandon
Army' (costs influence) and 'Leave Army' exits, which nothing guarded.

**What is skipped.** A commander who is the aggressor in a raid, forced supplies or forced volunteers
(`VillageRaidReader.IsRaiding(party)`: in a map event, `Party.Side` is the attacker, and
`IsRaid || IsForcingSupplies || IsForcingVolunteers`, and no lord party with healthy troops defends it,
#954). The set matches `StuckBattleEventAdapter`.
Forced supplies and forced volunteers are player-only events in vanilla; they are in the set to match
vanilla's grouping and cannot occur for an AI commander. The adapter snapshots (`CommanderSnapshot`,
`CommanderTickSnapshot`) carry one `PartyIsRaiding` bool, and `IEncounterAdapter.IsPartyRaiding` reads
the same function for the join floor. The side check uses the party's own side, which also holds for
an army follower because setting a leader's side copies it to the attached parties. A failed read is
false, which joins.

**Where the raid flag applies** (the join and latch sites through
`VillageActionJoinPolicy.IsJoinableBattle(inMapEvent, raiding)`; the floor and the status line read the
flag directly):

| Site | Effect |
|---|---|
| `ServiceAttachmentService.Assess` | A raid falls through to the march rules: no `BattleJoinRequired`, no retry budget. "No pending flag" holds for Assess and the pump: `OnPartyJoinedRunningMapEvent` still arms `PendingCommanderAttachment` when the commander starts a raid, and that flag is inert only while the raid stays uncontested, because its only reader (`ServiceMaintenanceService.TryRequestBattleJoin`) sits behind the joinable gate |
| `ServiceMaintenanceService.TryRequestBattleJoin` | No join request is raised |
| `ServiceMaintenanceService.TryBreakBattleLatch` and the reconciler's latch predicate | Both demote a stale `EnlistedBattle` while the commander only raids (pinned together by `EnlistmentPumpAuthorityTests`) |
| `ServiceBattleService.TryJoin` (floor) | After the side check, before the transition: one INFO line "commander raiding: not a battle to join", no rollback, no failure log. A player already in any map event still runs the existing join path |
| `ServiceStatusService.ResolveActivity` | `CommanderActivity.Raiding` (value 5, checked before `InBattle`; a raid a lord contests shows `InBattle`); the board reads "The column is raiding." (`taom_enlist_act_raid`) |

**Deliberately broad.** These still read `PartyIsInMapEvent`: the reconciler's army-leave sweep; the
stranded-encounter sweep (`EnlistmentReconciler.cs` `SweepStrandedEncounter`, which delays cleanup for
the length of a raid); the settlement-exit dwell carve-out; and `EnlistmentRescueCheats.cs`. The TICK
diagnostic line now also prints `raiding=`.

**Defender side joins.** A commander fighting off a raid on his own village is not the aggressor, so
`IsRaiding` is false and the soldier joins. **Militia fights inside a raid are not joined.** The
militia sits on the defender side of the raid event. When it resists, that event ends as the militia
falls, and the raider opens a second raid event that does the looting; both are raids the commander
leads, so neither is joined. A player who is neither the raider nor in the event stays parked.

**Consequence for the exits.** The `raiding_village` menu's 'Leave Army', 'Abandon Army' and 'End
Raiding' options are now reachable only in the army-attached edge case (the player already inside the
event through the propagation), when the raid read fails open, and possibly after a relief battle
(#954, see the timing risk below). `MapEventHelperCanMainPartyLeaveBattlePatch` does not close them.
In the attached edge, 'Abandon Army' ends the commander's whole raid (`PlayerEncounter.Finish` runs
`RaidEventComponent.OnPlayerEncounterFinalizeBattle`).

Sieges, sally-outs, hideout and field battles are never village actions and always join.

**A lord who relieves the raid is joined (#954).** A relief lord attacks the raiding commander
inside the same Raid event (`StartBattleAction.cs:116-118`, `:30`), so the commander stays the
attacker in a Raid. `VillageRaidReader.IsRaiding` therefore returns false while the raid's defender
side holds a lord party with healthy troops (vanilla's own test, `EncounterManager.cs:158`: mobile,
`IsLordParty`, `TotalHealthyCount > 0`). Militia, patrols and other non-lord defenders do not count,
so a raid they defend stays skipped. No other code changed: every site above reads the same flag. No
`MapEventStarted` fires when the lord joins, so the pump picks the flip up on the raid's event token
and raises the join. The fight ends the whole raid event once a side wins (`MapEvent.cs:897-899`), so
the soldier returns to service through the existing flush, army leave and latch demote; the status
board shows the commander engaged while it runs. With the soldier inside, the end comes through
`PlayerEncounter.Finish` -> `FinalizeBattle` -> `RaidEventComponent.OnPlayerEncounterFinalizeBattle`
(:220-225), and a lord retreat ends it too (`EndByRunAway` sets a winner, `MapEvent.cs:1153-1156`).

A soldier already inside the raid through the army-attached edge is restarted by vanilla when the lord
engages (`EncounterManager.StartPartyEncounter` :58, :75-77, `RestartPlayerEncounter` :317-339), which
ends the raid when an encounter is open. That path is unchanged by #954.

Open risks:

1. One-frame timing. A join made from `TickEvent` or from the hourly tick runs before
   `MapEventManager.Tick` in the same frame (`Campaign.cs:980-982`), and raids update even when they
   are the player's own event (`MapEventManager.cs:59`), so one simulation session can run with the
   soldier inside. If it decides the fight he is left in a decided raid, where vanilla offers the
   village hostile actions (`EncounterGameMenuBehavior.cs:2192-2217`) or the on_init jump
   (`:1494-1521`) leads to `raiding_village`. Guarded for raids since #841. The engine skips
   `MapEvent.Update` for the player's own event unless it is a raid (`MapEventManager.cs:59-61`). It runs at
   `Campaign.cs:982`, after the `TickEvent` dispatch (`:980`) and the periodic (hourly, daily) events (`:981`),
   and a session can hurt and decide it: the party's troops take casualties with no difficulty reduction
   (`MapEvent.cs:760-763`), and the fight can finish because the defender side holds more than one party
   (`:889-900`); the main hero is never a simulation troop (`MapEventSide.cs:765`). Field battles and siege
   assaults are skipped from `JoinBattleInternal` (`PlayerEncounter.cs:912`) on, so they join at once. A join
   into a RAID (`CommanderTickSnapshot.PartyIsInRaidEvent`, `MapEvent.IsRaid`) that is requested outside the
   wait-menu frame while the service wait menu is current is held as one pending join (the commander and his
   map event token) by `ServiceMaintenanceService.RequestJoin`, and performed by `OnWaitMenuFrame` before it
   pumps. The wait-menu tick runs before `Campaign.RealTick` (`MapState.OnTick:156-160`), and the join's
   switch to the encounter menu sets Stop (`GameMenu.cs:370`) before `Campaign.RealTick` computes this frame's
   delta, so `Campaign.Tick` skips `MapEventManager.Tick` (`:978-982`), except in a session's first two ticks
   (`CurrentTickCount < 3`). That post-load case predates #841, needs a save taken between a relief's arrival
   and the next update, and has no code guard (UNVERIFIED). The pending join is dropped when the event ends or
   changes, the commander leaves or is no longer followable, the service ends or the soldier is no longer
   attached, the player is already inside, or the session resets. Off the service wait menu nothing is
   deferred, so a soldier is never stranded; a raid joined there keeps this risk, the intended trade-off. The
   battle-start edge (`MapEventStarted`) also joins at once, by design: the event was created that instant and
   its first session is 30 campaign minutes away (`MapEvent.cs:787`), and AI raids start after the update
   (`EncounterManager.Tick`, `Campaign.cs:994`). The reconciler's hourly request goes through
   `IServiceMaintenanceService.RequestBattleJoin`, so both sources share the guard. UNVERIFIED in game.
2. The encounter's Attack and Send troops apply the hostile action against the village
   (`MenuHelper.cs:265-269` for Attack, `:566-572` for Send troops). Closed for an own-clan identity
   by the #832 join-time top-up, which mirrors and records the war first. For a vassal identity
   (no top-up, by decision) it is closed by the #960 rule: a vassal sits out a battle whose opposing
   faction his kingdom is not at war with, so Attack never declares a kingdom war on the commander's
   behalf (`BeHostileAction.cs:192-195`, with the -24 relation with the village owner and notables at
   `:36-43`, crime and trait loss at `:105-115`, relation and influence at `:138-147`). It fails open
   on a failed read.
3. Any `PlayerEncounter.Finish` with no winner while he is inside ends the commander's whole fight
   (`RaidEventComponent.cs:222`): Leave when the army merge was unavailable, or a `TryJoin` rollback.
4. The pre-existing join-rollback `LeaveArmy` mid-event; Patch82 is the floor.

**Owed in-game check.** See the #831/#954 bullet under "What actually remains". Control: a raid
fought off by the commander as defender still joins (he joins the running raid, so the pump makes the
join, not `MapEventStarted`).

Tests: `VillageActionJoinPolicyTests` (the truth table), `VillageRaidReaderTests` (the five shapes
against a staged raid: village alone, plus a healthy lord, plus a lord with no healthy troops, plus a
non-lord mobile party, and the commander on the defender side), `ServiceAttachmentServiceTests`,
`ServiceMaintenanceServiceTests`, `EnlistmentPumpAuthorityTests`, `ServiceBattleServiceTests` and
`ServiceStatusServiceTests` (the raid rows, each with a not-raiding control; they see only the bool),
and `CommanderSettlementIdentityTests` (both snapshots fill the flag through `VillageRaidReader`, and
the comment-stripped source pin: the reader tests `IsMobile`, `IsLordParty` and `TotalHealthyCount`
with no LINQ and no `Find`).

## Inside the raided village (#1019)

A raid can reach the village the soldier stands in. Read from the v1.5.5 decompile, not seen in a game:
`EncounterManager.StartSettlementEncounter` (:222-243) finishes the player's encounter and starts the raid; TAOM's
battle-start join (`EnlistmentBattleBehavior.OnMapEventStarted` -> `ServiceBattleService.TryJoin`) then seeds an
encounter through `IEncounterAdapter.EnsureEncounterAgainst`; `StartBattleAction.ApplyInternal` (:34-36) interrupts that
encounter unless the event is a sally-out: `PlayerEncounter.InterruptEncounter` (:1671-1679) leaves the battle and
opens vanilla's `encounter_interrupted` menu (Help / Leave). For the raid itself, `StartSettlementEncounter` then,
when he shares the village's faction, calls `PlayerEncounter.Start()`, which only creates the encounter, and
`Current.Init(...)`, which joins the defender side (`Init`, :655-661); otherwise it walks him out of the village with
`LeaveSettlementAction.ApplyForParty` (:232-242). He is not stranded in either path, so no fix is made; the question
that stays open is only which menu the game shows and what Help or Leave does.

`EnsureEncounterAgainst` therefore writes one INFO line when it seeds a join while the main party's
`CurrentSettlement` is the map event's settlement, and not behind the trace toggle (it is rare):
`[EnlistDiag] seeding the join from inside '<settlement id>' (event=<type>, sameFactionAsSettlement=<True|False>); ...`.
`sameFactionAsSettlement` is the engine's own test on the raid path: True means the engine re-joined him on the defender
side, False means it walked him out of the village.
Reproduce twice (same faction, then his own clan's village) with "Enable Enlistment Trace" on, and report that line,
which menu appeared, and what happened after Help or Leave. `InsideSettlementJoinDiagnosticTests` pins the line's
place (after the settlement read, before `RestartPlayerEncounter`, ungated). The timing questions of the same issue
(the menu text redraw, the link colour rewrite, the board rate above x32) are measured by the map profiler's menu
probes: [map-perf-profiler.md](map-perf-profiler.md), "Menu probes (#1019)".

## Personal spoils (#834)

A soldier who wins a battle keeps a few items from the enemies he killed himself, on top of
vanilla's loot share (which is kept as it is; no `DoLootInventory` patch).

- **Kill list, hero by character.** `EnlistmentMeritMissionBehavior.OnAgentRemoved` now counts a kill
  only when the killer's `Character` is `CharacterObject.PlayerCharacter`, not when the killer is the
  controlled agent. A soldier the player takes over after falling is never a hero
  (`Mission.CanTakeControlOfAgent` requires `!Character.IsHero`), so his kills are not the player's. This
  also changes kill XP and the merit score's kill term (the `BattleMeritScorer` kill weight): both now
  count only the hero's own kills. The merit sampler's fall and position readings also treat a
  controlled soldier as "hero down" after a takeover, so the fall is recorded. Each human enemy kill also queues the
  `Character.StringId` into a `ConcurrentQueue<string>` (callbacks run off-thread; an `Agent` is never
  stored), copied at mission end into `MeritSample.KilledCharacterIds`.
- **Win-only payout.** `EnlistmentBattlePayoutService.PayOutBattle` runs the spoils only when the
  battle was won and not sat out, from the same consumed sample, at `MapEventEnded` on the host. A loss,
  a sat-out battle, a missing sample or a kill-less battle gives nothing.
- **Rank, then try.** `PersonalSpoilsPolicy.Rank` orders the kills by expected value, highest first
  (ties keep kill order), and gives each a target value = expected x (1 + `spoilsValueScalePerRank` x
  rank ordinal) x a random factor from `spoilsRandomMin` to `spoilsRandomMax`. Empty ids and unreadable,
  non-finite and non-positive expected values are skipped. There is no cap in the policy: the payout stops
  once `spoilsMaxItemsPerBattle` items have LANDED, because vanilla's roll often returns nothing and an
  empty roll must not use a slot. With the cap at 0 no engine read happens at all.
- **One adapter call rolls and grants.** `IBattleLootAdapter.GrantLootFrom` wraps `BattleRewardModel`
  (`GetLootedItemFromTroop`), adds the element with `ItemRoster.AddToCounts(element, 1)` so the rolled
  modifier is kept, and returns the display name, or null for a missing character, a troop with no battle
  set (vanilla's `GetRandomItem` dereferences a null `Equipment` there), an empty roll, a banner, or a
  failure. Vanilla's roll walks the troop's merchandise items from most to least valuable, accepts each
  with a chance that drops once the item is worth more than the target, skips non-merchandise items, and
  often returns nothing. So no gated armour piece can be a spoil (see
  [armour-acquisition.md](armour-acquisition.md)).
- **Not in the ledger.** Spoils are never recorded in the issue ledger; like any loot, an unmodified
  spoil of the same item as a piece he was issued can be taken back at an honourable discharge when no
  unmodified copy of that issued piece is left in the inventory (sold, lost or worn) (the known limitation under "Honourable discharge reclaims the issued kit by item id"
  in this doc). One summary toast (`taom_enlist_spoils`, "Spoils of your own blade: {ITEMS}") lists what
  landed; none when nothing did.
- **Config** (`enlistment_config.json`, validated, out of range reverts that key with a warning):
  `spoilsMaxItemsPerBattle` 3 (0 to 10), `spoilsValueScalePerRank` 0.25 (finite, 0 to 2),
  `spoilsRandomMin` 0.1 and `spoilsRandomMax` 0.9 (finite, 0 <= min <= max <= 2).
- **Session reset.** `IBattleMeritAccumulator.ResetForNewSession` drops a pending sample, called from
  `ServiceMaintenanceService.ResetSessionCaches`, so a sample cannot pay into another campaign. A save
  taken mid-battle loses the sample, as it already loses the merit.

Tests: `PersonalSpoilsPolicyTests`, `BattleMeritAccumulatorTests`, `EnlistmentBattlePayoutServiceTests`
(the spoils rows, the cap counting landed items, a zero cap reading nothing), `EnlistmentContentConfigProviderTests`
(spoils defaults, shipped parity, range), `PersonalSpoilsWiringTests` (the kill-queue, hero-by-character
and adapter source shapes, and the engine binding) and `EnlistmentContainerWiringTests` (the payout
service resolves).

## The promotion notice and the kit line (#835)

A promotion used to be a one-line message in the log. It is now a quick-information notice with the
commander's portrait, and the status board says when the quartermaster has gear to hand over.

- **The notice.** `ServiceDailyAnnouncer.AnnouncePromotion` builds "{COMMANDER} names you {RANK}."
  (`taom_enlist_promoted_notice`) and, when a kit waits, appends "The quartermaster has kit for your new
  rank." (`taom_enlist_promoted_kit`), then shows it through `IQuickNoticeAdapter` /
  `QuickNoticeAdapter` (`MBInformationManager.AddQuickInformation`, 3000 ms extra, with the commander's
  `CharacterObject`). If showing the notice throws, the old plain `taom_enlist_promoted` message is shown
  instead; a notice the UI does not display cannot be detected. A commander who is missing, dead or a
  prisoner (`ServiceDailyAnnouncer.UsePortraitNotice`) gets that neutral text instead of a portrait. Both
  callers go through `AnnouncePromotion`: the battle payout and the daily check. Every promotion is
  exactly one rank up, so two promotions in a day are two ranks and show two notices.
- **No promotion in the grace state.** `EnlistmentBattlePayoutService` skips the promotion evaluation
  while the commander is unavailable, as the daily tick already does: there is no chain of command left
  to promote anyone.
- **A kit waits** when `EnlistmentKitTiers.KitWaits(rank, ledger)`: the quartermaster's own ledger
  (`IEquipmentIssueLedger.HasIssuedForTier`) has not covered the rank's kit tier, so the board, the
  notice and the quartermaster's answer share one rule (an undefined stored tier counts as nothing
  drawn). Ranks 4 to 6 share the top tier, so a promotion inside it promises no kit once the Staff
  Sergeant kit is drawn.
- **Status board.** `ServiceStatusModel.KitWaits` (in `Equals` and `GetHashCode`, so a change reaches the
  screen) adds "Kit to draw: the quartermaster holds gear for your rank." (`taom_enlist_board_kit`, with
  its own leading newline like the other board lines) under the same condition. The writer appends it to
  the board's last variable, so the registered board template keeps its shape and its translations. The
  line also shows before the first kit is drawn.
- **Not built:** naming the next rank's gate inside the promotion notice (the status board already shows it), and automatic issue of gear (drawing in conversation
  is deliberate).

Tests: `PromotionNoticeTests` (the kit rule against the real ledger, the portrait-or-neutral choice, source
shapes), `ServiceStatusServiceTests` (`KitWaits` on the board and in equality),
`EnlistmentBattlePayoutServiceTests` (no promotion in the grace state) and `EnlistmentContainerWiringTests`
(the announcer resolves).

## End of the term (#836)

When the term set at the oath ends (`ContractEndDay`), the player used to get a toast and nothing else.
Now a modal two-option inquiry opens and forces a choice. It replaces the old toast: the
`taom_enlist_term_served` key is retired (its rows are gone from the English file, the 12 language files
and the translation caches).

- **Offer.** `ContractRenewalService.OfferIfDue(nowDays)`, called from `EnlistmentContentBehavior`'s daily
  tick after the daily work, on the host only (the service checks the co-op authority itself): "{COMMANDER}
  offers you {BONUS} denars and renewed trust to sign on for another term, or you may leave the service
  with honor."
- **Option B is Escape.** The engine runs the negative option on Escape and the gamepad B button, whatever
  the button state. So option A is "Leave with honor" and option B is "Sign on again": Escape (and gamepad B) runs
  option B and keeps the soldier in service, while Enter (and gamepad A) runs option A, the deliberate
  confirm, as in the desertion prompt. Renewing holds him to nothing: he may still ask for release at once, and the bonus is
  bounded (at most `renewalBonusWageDays` of wage, once per year served).
- **Renew.** Adds `renewalTrust` through `IServiceRewardService.Grant`, and pays the bonus
  (`renewalBonusWageDays` x the current rank's daily wage, computed in a long and clamped) through
  `IServiceRewardService.PayBonusFromCommander`. The bonus is a gift, never a debt: with commander-funded
  wages it comes from the commander's purse and is at most what he holds above the same gold floor wages
  keep (`wagePolicy.commanderGoldFloor`, 500), so a poor commander pays less or nothing; it is minted in
  full only when wages are minted. The unpaid part of the bonus is not owed and arrears are never touched.
  A notice (`taom_enlist_term_bonus_paid`) states the amount actually received, and none is shown for 0.
  Then `ContractEndDay = now + ContractDays`, the oath's own expression.
- **Leave.** Only while `EnlistedAttached` (a battle can start in the same frame after the offer shows):
  `DischargeService.Execute(DischargeReason.ContractNotRenewed)`, the first producer of that reason, with
  the existing honourable consequences. If refused because he is no longer attached, the guard is cleared
  and the offer returns on the next daily tick. `Commission` still has no producer.
- **Gates.** `ContractRenewalPolicy.IsDue` is the cheap gate (attached, end day reached, no offer already
  made for this end day) and runs before any adapter read. Then the service waits unless the commander
  exists, is alive and is not a prisoner, and waits while the commander is in a map event, the player is in
  a map event, the ownership read failed or a conversation is in progress. A
  settlement encounter alone does not block, because a soldier held in the commander's town always has
  one. A blocked offer retries on the next daily tick. The choice lands on a later frame, so the callbacks
  re-check the authority and that the soldier is still enlisted.
- **Once per term, in memory only.** `ContractRenewalService` keeps the end day it already offered for in a
  field, set before the inquiry shows, cleared on a refused Leave and on a new campaign session
  (`ServiceMaintenanceService.ResetSessionCaches`). The reason: the engine can run the daily tick twice in
  one frame (`MBCampaignEvent.CheckUpdate` loops while the trigger time is past). It is not saved on
  purpose, since a saved stamp could lose the offer when Save And Exit hides the inquiry without an
  answer. Renewing
  moves the end day, so the next term offers again. Co-op: the co-op layer can call `HideInquiry` and
  dismiss the offer without an answer, which leaves the guard set for the session (UNVERIFIED; release
  still works).
- **Console.** `taom.set_term_end` (host only, campaign gate) sets `ContractEndDay` to today while
  enlisted, so the next daily tick makes the offer. See [dev-console.md](dev-console.md).
- **Config** (`enlistment_config.json`, validated, out of range reverts that key with a warning):
  `renewalBonusWageDays` 30 (0 to 365) and `renewalTrust` 10 (0 to 100).
- **Keys:** `taom_enlist_term_title`, `taom_enlist_term_body`, `taom_enlist_term_renew`,
  `taom_enlist_term_leave`, `taom_enlist_term_bonus_paid`.

Tests: `ContractRenewalTests` (the cheap gate, each service gate, the offer once, the session reset, which
option is Escape, renew, leave and its refusal, the callbacks' re-checks, the console command, the retired
key),
`ServiceRewardServiceTests` (the bonus paid like wages), `EnlistmentRecordTests` (an old save carrying the retired stamp, including garbage values), `EnlistmentContentConfigProviderTests` (renewal defaults, shipped parity, range) and
`EnlistmentContainerWiringTests` (the content behavior resolves).

## Past service (#837)

A soldier who served before is remembered. A discharge used to wipe the content record, so a veteran who
took the oath again began as a Private.

- **History.** `ServiceHistory` is a saved list of past contracts (`ServiceHistoryEntry`: the enlisted
  hero id, commander id, clan id, kingdom id (empty for an independent clan; a mercenary clan records the
  kingdom that hired it), rank, discharge reason, end day, and the `EnlistmentKitTier?` that stayed with
  the player). It lives in `EnlistmentContentStore.History` and is saved in the content section as one more
  `history=` key (rows joined by `~`, fields by `|`; the kit tier field is empty or the tier ordinal). The
  parse is hardened like the other records: a bad row (unknown rank, reason or tier, non-finite day, wrong
  field count, empty commander or hero id) is dropped and the rest survive, an old save without the key
  loads empty, and a malformed progression record does not cost the history.
- **What clears it.** A discharge resets only the content record (`DischargeConsequenceService` calls the
  record's own `Reset`), so the history survives it. `EnlistmentContentStore.Clear()` also drops the history,
  and its two callers in `EnlistmentContentBehavior` (a new game, and a session launch that did not follow
  a save load) are the new-campaign reset. A save with no enlistment data does not call `SyncData` (the
  engine skips behaviors that have no saved data), so that case is reset by the session-launch `Clear()`,
  not by `Deserialize`. The history belongs to its campaign.
- **Pruning.** On write, `Prune(now, expiry, max, cooldown)` drops entries older than
  `serviceHistoryExpiryDays` (730 DAYS: a game year is 84 days at normal pace (24 in Fast mode), and 730 days is two 365-day contracts, so
  never write "2 years") and entries dated after now, then the oldest beyond `serviceHistoryMaxEntries`.
  A desertion still inside its cooldown is never removed, by expiry or by the cap (only the oldest other
  rows go, so the cap can be passed by cooling rows). The enlist gate and the prune read one predicate,
  `ServiceHistoryEntry.IsCoolingDownDesertion`. `Restore` and the cooldown ignore future-dated entries too.
- **Whose entries count.** An entry belongs to the hero who served (`Record.EnlistedHeroId` at the
  discharge). The policy and the cooldown consider only entries of the current main hero, so a Player
  Switcher swap or an heir starts fresh. The oath passes its `enlistedHeroId`; the gate's cooldown check
  reads the main hero id through `IPlayerPartyAdapter`.
- **When an entry is written.** `DischargeService.Execute` notes the commander and the enlisted hero at
  step 3 (`IServiceHistoryService.NoteCommander`, with the clan and kingdom ids from the snapshot), and
  `DischargeConsequenceService.ApplyConsequences` calls `RecordEnd(reason, rank, kitTier)` for every
  reason, before the content record is reset, on the host. Only `RecordEnd` filters the quiet reasons: heir
  succession and save normalization write nothing.
- **One kit rule.** `ReclaimIssuedKit` returns the number of items it took back (0 on every early exit:
  feature off, nothing issued, main party unavailable), and a desertion reclaims nothing. The kit stayed
  with him when `issuedCount > reclaimed`, or when a tier is recorded with no item ids (only a restore
  seed leaves that). The entry stores `ledger.HighestIssuedTier` when the kit stayed, else nothing.
- **The restored rank** (`ReenlistmentPolicy.Restore`, pure): the best of the same commander's old rank
  minus one, the same clan's minus one, and the same kingdom's minus two, never below Private. Ties go to
  the most recent entry, whose kit tier is the one carried. A desertion gives no credit. Expired entries
  give nothing. `CompleteOath` applies it through `IServiceHistoryService.RestoreOnOath` (no return
  value): rank only, so service XP and days served restart and the next promotion's gates count from this
  oath. It never lowers a rank already held. The oath line (`taom_enlist_oath_remembered`, "{COMMANDER}
  remembers your service: you begin as {RANK}.") shows only when a rank was restored.
- **Kit ledger.** The oath seeds the ledger with the tier he really kept (`RecordIssue(tier, no items)`),
  and only when the entry has one. After a full reclaim it stays empty so the veteran can draw kit for
  his rank again. A seeded tier lower than the new rank's own still leaves a kit waiting after the next
  promotion (`KitWaits`).
- **Desertion cooldown.** `ReenlistmentPolicy.InDesertionCooldown`: this hero deserted from this
  commander less than `desertionCooldownDays` ago (0 turns it off).
  `EnlistmentDialogGateService.CanEnlistWith` returns `EnlistGateResult.DesertedFromHim`, shown greyed
  with `taom_enlist_refused_deserter` ("{COMMANDER} remembers your desertion and will not take your
  oath."). It sits below the commander checks, so a lord with no party gives `CommanderUnavailable`
  first. Another lord is not affected. All four consumers of `IServiceHistoryService` (`DischargeService`,
  `DischargeConsequenceService`, `EnlistmentDialogGateService`, `EnlistmentService`) take it as a required
  constructor argument.
- **The warning names it.** The wait-menu prompt appends `taom_enlist_desert_memory` ("Your commander
  will not take your oath again for a time, and this term will count for nothing if you return.") after
  the cost sentence (see "What desertion costs (#833)"). The in-person line is the player's own speech, so
  it composes the first-person twin `taom_enlist_desert_memory_reply` ("(My commander will not take my oath
  again for a time, and this term will count for nothing.)"). An older honourable entry still gives credit
  after a desertion; only the desertion's own term counts for nothing.
- **Config** (`enlistment_config.json`, validated, out of range reverts that key with a warning):
  `serviceHistoryExpiryDays` 730 (1 to 3650), `serviceHistoryMaxEntries` 10 (1 to 50),
  `desertionCooldownDays` 60 (0 to 365).
- **Keys:** `taom_enlist_oath_remembered`, `taom_enlist_refused_deserter`, `taom_enlist_desert_memory`, `taom_enlist_desert_memory_reply`.

Tests: `PastServiceTests` (the list, pruning, the store and its new-campaign reset, the policy, the
cooldown edges, hero identity, the service), `EnlistOfferVisibilityTests` (the gate ladder and the
greyed verdict), `ServiceVocabularyTests` (the refusal text), `DischargeConsequenceServiceTests` (the kit
rule per path, every reason), `DischargeServiceTests` (the commander is noted before the event),
`EnlistmentServiceTests` and `EnlistmentDialogGateServiceTests` (the wiring), `DesertionCostTextTests`
and `EnlistmentWaitMenuPresenterTests` (the warning), `EnlistmentContentConfigProviderTests` (defaults,
shipped parity, range).

## The column line (#838)

The status board shows what the commander's column is doing, in the engine's own words.

- **Source.** `ICommanderLordAdapter.GetPartyBehaviorText(heroId)` returns
  `hero.PartyBelongedTo.GetBehaviorText()` rendered to a string (the text the vanilla party tooltip uses:
  "Besieging X.", "Defending Z.", "Travelling to Y.", "Running from W.", an army's long-term text, and so
  on). It is the engine's localized string, so only the label needed a new key. It returns null when the
  hero does not resolve or has no party, or the read throws (logged once until a read succeeds). The engine returns
  a non-empty text on v1.5.5, and its phrases are shown as-is, including ones such as "Unknown Behavior.".
- **A separate read, not part of a snapshot.** The text is not on `CommanderSnapshot` or
  `CommanderTickSnapshot`: the full snapshot is also read by the reconciler and the enlist gate, and the
  tick snapshot is the pump's read, which must not render text. Only `ServiceStatusService.Build`
  asks for it, and only when the commander has a party and is not inside a settlement.
- **When it shows.** `ServiceStatusModel.ColumnText` is part of the model's value equality, so a changed
  activity reaches the screen on the board's next refresh (the same throttle as every other field, about
  every 2 seconds on the wait menu). The line is left out when the text is null or empty, and
  while the commander's party is in a settlement (the engine blanks this text there, and the activity line
  already says where he is).
- **Rendering.** `ServiceStatusTextWriter.WithColumnLine` appends `taom_enlist_board_column`
  ("{NEWLINE}Column: {COLUMN}") under the activity sentence (the `ACTIVITY` variable) through the same
  composition as the kit line, so the translated board template `taom_enlist_wait_board_v2` does not change.
- **Key:** `taom_enlist_board_column`.

Tests: `ServiceStatusServiceTests` (the model's equality and hash, the service fill, the writer source pin
including the `ACTIVITY` placement, no ask when there is no party or the commander is in a settlement, that
neither snapshot body renders the text, and the log-once pin).

## The cost of your own men (#839)

A soldier who keeps his own paid people is told what they cost next to what service pays, where he decides.

- **N, the cost.** `IPlayerPartyAdapter.GetMainPartyDailyWage()` returns `Hero.MainHero.PartyBelongedTo.TotalWage`
  (v1.5.5 `MobileParty.TotalWage`, which asks the installed `PartyWageModel`). That is the value
  `DefaultClanFinanceModel.CalculatePartyWage` charges the player's clan, so it is the real daily bill and
  includes any rule a model override adds; it is not recomputed. Companions draw a wage too, so N counts them
  as well as troops. It returns 0 when there is no party or the read throws (logged once until a read
  succeeds).
- **M, what service pays.** On the board it is `IEnlistmentWagePreview.GetDailyWage()`, the rank's daily
  wage when a wage is due today and otherwise 0 (not enlisted, or the column is lost,
  `CommanderUnavailable`), so the board's "Daily pay" line and this line both say 0 then. In the oath
  question it is the table wage of the rank the oath would start at: `IServiceHistoryService.WageAtOath`
  shares one start-rank rule with `RestoreOnOath`, so a returning veteran is quoted his restored rank's pay,
  and the oath grants exactly that rank.
- **Where it shows.** In the commander's oath question: the line `taom_enlist_oath_ask` is composed as
  `{=!}{OATH_ASK}{OATH_RETINUE}` (the ask text and its key are unchanged), and the line's condition,
  `RetinueCostText.SetOathLine(IEnlistmentDialogGateService.OathRetinueCost(partnerId))`, sets the retinue
  sentence after a space, or an empty text when N is 0, so the question reads as before. The gate method
  never throws (it catches, logs once, and answers (0, 0)), because the question is the only path to the
  oath. Declining is still free. On the status board: `ServiceStatusModel.RetinueWage` (part of its value
  equality; M is the model's `DailyWage`) and `ServiceStatusTextWriter.WithRetinueLine` append the sentence
  on its own line after the pay-owed line (`{=!}{PREVIOUS_LINES}{NEWLINE}{RETINUE_LINE}`), so the translated
  board template `taom_enlist_wait_board_v2` does not change. Both are left out when N is 0. The board
  reads one wage on each board refresh.
- **One key.** `taom_enlist_retinue_cost` ("Your own men cost {COST}{COIN} a day; service pays {PAY}{COIN}."),
  built by the one `RetinueCostText.Build` for both places, with the coin icon the "Daily pay" line uses.
- **No relief, by decision.** The retinue keeps costing its full wage. A discount would let a parked army
  cost little, which changes balance; it is a possible follow-up on the maintainer's word.

Tests: `RetinueCostTextTests` (the builder, the single key, the oath-question composition),
`EnlistmentDialogGateServiceTests` (`OathRetinueCost`), `PastServiceTests` (`WageAtOath` agrees with
`RestoreOnOath`), `ServiceStatusServiceTests` (the model's equality and hash, the service fill, M as the
preview's wage, the writer and adapter source pins).

## Interactions with the rest of TAOM

**Leader-keyed attribution is the recurring hazard.** For months of game time the player fights
inside someone else's army, so any system that asks "was the player the winning party's leader?"
silently pays them nothing. `SpecialResources` hit this before enlistment existed and moved to
`MapEvent.PlayerSide == WinningSide` participation. The #375 audit swept every remaining
`CareerPassiveHero.ResolveId` / `LeaderHero == MainHero` site and fixed three more: enlisted
sieges now advance the War of the Ring victory gate (`WarEventSnapshotAdapter.FromSiege`), and
captures credit the `DefeatEnemyLords` and `SettlementsCaptured` career-quest objectives.
Since #1015 the career quest credits side membership for every player, enlisted or not;
momentum adds the side rule to its kingdom test, so a vassal is still credited for any capture by
his own realm. The capturer is
the leader of the side that won (`AttackerSide.LeaderParty` after an assault,
`DefenderSide.LeaderParty` after a failed sally-out), so a soldier whose commander fights inside
another lord's army is credited, and one who sat out a commander-led siege is not. A prisoner
is drawn by weighted chance among the winning parties, so `DefeatEnemyLords` credits the player
when his party is on the captor's side of that map event, and falls back to "the captor is the
player or the commander's party" for a capture outside a battle. The sides are still populated
when these fire (`SiegeCompleted` comes from `OnBeforeMapEventFinalize`, before
`MapEvent.FinalizeEventAux` clears them). `KingdomManager.SiegeCompleted` runs before the momentum
listener and none of its steps clears the sides, so a future listener that moves the player out
of the event would zero the credit. **Before adding any new leader-keyed reward, ask what it does during someone else's
siege.** Consumers read enlisted state through `IEnlistmentStateQuery` (cross-feature seam;
FieldCommission and the Patch36 F6 gate use it too).

**Deliberately unpaid while parked.** A commander victory the player sits out pays nothing
anywhere — renown, special resources, momentum. That is participation-consistent and intended.

**Co-op.** Every world-mutating handler is host-only. The Codex pass found the one exception (the
wait-menu leave option) after our own review passed, which is worth remembering: a menu *option
consequence* is a world-mutating entry point even though it doesn't pattern-match like one.

**Not through a messenger (#898).** `EnlistmentDialogBehavior.OfferIsTakeable(verdict, throughMessenger)` greys the offer while `IMessengerConversationState.IsOpen`, with `taom_enlist_not_by_messenger`. Swearing finishes the live encounter and `MobilePartyAttachmentAdapter.ParkNear` sets the player's position to the commander's, which through a messenger would be a teleport. The line stays visible, so the player learns why. See [messengers.md](messengers.md).

**Co-op guests see no enlist offer (#966).** `EnlistmentDialogBehavior.ShowsOfferFor(verdict, deferToHost)`
(`EnlistmentDialogBehavior.cs:113`, read at `:120-121` with `ICoopSessionProvider.ShouldDeferToHost`)
hides the line for a guest, because enlistment is applied host-only and on a server the host is the
placeholder hero. A player host and a solo player keep the offer. Cost: under BannerlordTogether or
another co-op mod TAOM cannot probe, or after a failed Coop role bind, `ShouldDeferToHost` is true on
every peer, so the offer hides in solo play there too. See [coop-interop.md](coop-interop.md).

## Testing

The Enlistment suite (`TAOM.Tests/Features/Enlistment/`, 1,042 tests as of 2026-09-06, measured with `--filter FullyQualifiedName~TAOM.Tests.Features.Enlistment`; the repo-wide total is not quoted here because it moves with every other feature): transition-table
matrix, discharge invariants, Entity-State-Matrix load rows, reconciler policy (grace,
captivity, prisoner-commander-with-live-party), record round-trip incl. NaN/forward-compat,
menu redirect policy + cap, battle ordering/rollback/loot-guard, binding pins, config
semantic validation (one test per rule), wage orchestration (solvency × arrears × channel,
incl. regression tests for the mint-mode double-payment and shortfall-overcount bugs),
promotion thresholds at both evaluation points, merit scoring/bands incl. NaN ratios, duty
gating/rotation/lifecycle per mechanic, equipment resolver fallback, the persisted
issue-ledger across a save round-trip, role-fit bands per assignment, and one regression test per
Codex finding (`CodexFindingRegressionTests`). Since #551 the suite also pins the battle-end gate's event identity, the detach precondition, R1c and the stale-latch timer, and the cross-campaign reset of that timer's anchor.

**Verified in live play (2026-08-07):** field-battle join with the commander solo (instant, via
`MapEventStarted`); siege-assault join; hourly-recovery join when the immediate edge is missed;
return to parked service after a battle; enlistment config loading without silently reverting;
the conversation encounter being closed at swear-in. Log lines that prove each:
`joined commander battle on side …`, `closed the enlistment conversation's PlayerEncounter`,
`service wait menu re-opened after …`.

**Still owed (in-game gates that have NOT run) — rewritten 2026-08-09 after the live session.**

Struck from this list because the 2026-08-09 session exercised them: **battle joins** (ten, both
sides, four via `MapEventStarted` and six via the hourly recovery), **field duties** (two, one passed
one failed), **promotion** (to Soldier on day 7), **a camp incident** (`short_rations`), **battle
merit** banding, and the **food/wage/morale ticks for the inactive MainParty** (rations topped twice,
morale lifted to the floor). Also struck: *the duty spawn→hunt→complete loop and its target-party
cleanup* — **that model no longer exists.** Duties never spawn or destroy anything (#428).

What actually remains, each a state no test can reach:

- **Encounter after a battle (#796).** (1) Enlisted, join and win a field battle with loot and
  prisoners and read every screen to the end: back on the service menu, and no "closing" line from
  `[EnlistDiag]` until the screens close. (2) The same during a siege assault the commander wins.
  (3) If an assault is repulsed with your commander ATTACKING: within three in-game hours (the
  first hourly tick only sets the anchor, and the elapsed check against 1/24 day falls short by float
  rounding on about half of the hours, so recovery fires on the second or third tick) the service menu is back, and the log shows "breaking a
  stale battle latch (state EnlistedBattle)" or "a PlayerEncounter is open with
  no battle in progress". A defending commander's repulse inside his town (#1021): enlist under a lord
  holding a town, follow him inside, defend an assault until the attackers retreat; within three
  in-game hours (the second or third hourly tick) `taom.service_status` shows `EnlistedAttached`, still inside, the log shows "releasing a stale
  EnlistedBattle latch inside a settlement", the next assault logs "cleared the last battle's result from the
  reused encounter (#1021)" and then "joined commander battle on side Defender", and that assault reaches
  its mission or simulation instead of ending at once; when the lord marches out the soldier follows. The saved-while-latched field orphan is
  unit-tested only.
  (4) Right after a battle, discharge from the service menu, then click a settlement and a lord:
  encounters start.
- **Quartermaster answers (#829).** Draw a kit (expect the issued line), ask again at the same rank
  (expect "already drawn"), and if reachable trigger empty stores and a co-op client (expect the
  stores and host-only lines). Check each text; the three new keys show English until the paid
  translation run. The cannot-load case (the party cannot take items) is effectively unreachable by
  hand and is covered by unit tests.
- **Discharge during load (#828).** (a) MCM switch off, saved on the open map with the service menu:
  load, expect the body "Your service has ended." and one enabled option "Go on your way", landing
  on the map. (b) MCM switch off, column in a town: the discharge itself finishes the settlement
  encounter during load, clearing the saved menu and walking him out; expect the plain map beside
  the town, no service menu, and the expected ERROR line `DISCHARGE(PlayerRequest) could not open
  'town' for '<settlement id>'`. (c) The town route ("Go on your way" into the town menu) is reached
  only through the identity guard from a save with no live settlement encounter; there is no
  in-game recipe, unit tests cover it. (d) Repeat (a) once in a non-English language (the English
  seed text shows until the paid translation). (e) #1021: save at the assault's encounter menu as a
  defender, switch enlistment off in MCM, load: the log shows "feature disabled: waiting for the
  battle to end before releasing", the battle plays out normally, and the release then follows (a) or
  (b) above. The discharge's own "left the player in his own battle" path (step 10 skipped) is the floor
  for an unmerged soldier and has no in-game recipe; unit tests cover it.
- **Discharge, any reason.** Zero occurrences in the live log. The save-breaker fix is written and
  never exercised; the line that would indict it is
  `DISCHARGE(…) LEFT THE PLAYER UNABLE TO START ENCOUNTERS`. Leaving service and immediately
  clicking a lord is the sharpest version.
- **Player captivity mid-service** — entry and exit. Every captivity guard across the reconciler, the
  duty runtime and the grace freeze is unit-tested only. The duty guard's proof line is
  `duty '<id>' cancelled (captive)`.
- **The commander-loss modal, the contract waiver, and the wage/promotion gate.** All written
  2026-08-09, none seen. The live session reached `CommanderUnavailable` but before the modal existed.
- **Formation placement and no captaincy (#443, #576).** Enlist at any rank, join a field battle
  with 20 or more troops on the side: NO deployment screen; the log carries `Order of Battle
  deployment suppressed`, `battle command stripped at AfterStart`, `joined ... army for the
  battle` and `soldier placed in the <class> formation`; F1 to F8 answer "There isn't any unit
  under command"; the commander's formations move; no formation banner is the player's. Repeat with
  50 or more on the side (the player must stand in his assigned formation, not the general's) and
  as a siege assault and a siege defence. Control: leave service and fight a battle of the player's
  own with 20 or more troops; the deployment screen MUST still open. Merge-failed path (#495, a
  kingdomless commander): `army merge unavailable` and still no screen.
- **Battle exits hidden (#827).** Join a commander's battle through an army: the encounter menu
  shows neither 'Abandon army' nor 'Leave'. Merge-failed path (a kingdomless commander): no
  'Leave'. Under a safe-passage truce with the enemy faction, a soldier with no army sees
  'Leave' again, because vanilla greys out both 'Attack' and 'Send troops' and the menu must
  keep an exit. After that 'Leave', expect the state to return to `EnlistedAttached` once the
  commander's battle ends (the reconciler frees it when no battle remains).
- **The presence hold at x64 (#577).** Extra Fast Forward multiplier 64, Enable Enlistment Trace on: ride with
  the commander through three or more battles. In each encounter menu hover 'Leave it to the
  others' (it is greyed with the TAOM tooltip while Attack is enabled). Click it where it is
  enabled (wounded, or odds at or above 0.85), otherwise click Attack. Every x64 run needs at least
  one enabled 'Leave it to the others' click, because only that click builds a `BattleSimulation`,
  which is the path #577 hardened. Recipe: enter the battle wounded, so the option is enabled whatever
  the odds. Expect no exception and no
  `TICK trigger=army left ... verdict=AttachRequired` line; expect `reconcile
  skipped (trigger=army left)` or `holding presence` lines instead. The reporting player's log is
  still wanted: the confirming sequence is `RESTORE ok`, `joined ... army for the battle`, `TICK
  trigger=army left state=EnlistedBattle verdict=AttachRequired ... hasPlayerEncounter=True`,
  `PARK ok ... before: mapEvent=True ... after: mapEvent=False`.
- **Leave it to the others (#830).** Empty party, healthy, field battle below 0.85: the option
  is greyed with "Take your place in the fight". Same at a siege assault and a sally-out. A relief
  battle outside the walls, a siege ambush and a naval blockade follow the field rows. With men of
  his own the option reads "Send troops" and follows the same rows. Wounded: the option is enabled
  with the wounded line over vanilla's tooltip; click it and expect the wounded line, full base XP and
  no renown. Side at 0.85 or more: enabled with the odds line; click it and expect the "share was cut"
  line, cut base XP, no renown and one trust point docked. A forced-open case (an own raft, siege
  preparation incomplete) pays full base XP with no renown and the "no way into the line" line. The
  `[Enlistment] sat out a simulated battle` log line names the reason. Repeat one enabled click at
  Extra Fast Forward 64 (see the #577 step). If the game allows a save in the battle aftermath, save
  there after a sat-out click and reload: the click-time verdict is not saved, so check whether that
  battle is paid as sat out or in full. Not yet seen in game.
- **A field battle with the commander in an army.** Still the case most likely to fail:
  `FindCommanderPartyIdIn` matches only the commander's own party in `InvolvedParties`, and an
  attached army member may not appear there.
- **Cost of your own men (#839).** Keep paid troops or companions in your party and ask a lord to enlist: his
  oath question ends with "Your own men cost N a day; service pays M." (declining is still free), and the
  wait-menu board shows the same line, with coin icons, after the pay lines. N should match the main party
  wage line of the detailed gold tooltip (or the map bar's troop wage figure); M is the pay of the rank the
  oath starts at (a returning veteran sees his restored rank's pay, and gets that rank), and on the board
  what service pays today (0 while the column is lost). For the line to go, dismiss every troop and leave
  the companions in a town; with companions only, N falls to their wages. Release some troops and the number
  follows on the next board refresh.
- **Raid join from the running service menu (#841, #954 risk 1).** Join a relief battle against a village
  raid from the running service menu: the encounter opens on the next frame with no casualties taken before
  it. Joins into field battles and sieges still happen at once, and a raid joined off the service wait menu
  is not delayed.
- **Pump rates (#841) and the trace default (#840).** After a board input changes (the Column line, a day
  passing) the board follows within about 2 seconds on the default running service menu and about 8 seconds
  at play speed; the pump keeps running while
  paused and right after loading onto the menu; a commander joining a running battle is still followed (the
  pump fallback now waits up to 18 campaign minutes). A `TAOM.json` carrying `"EnableEnlistmentDiagnostics": true`
  ends up with the trace off (the old key is ignored), and switching "Enable Enlistment Trace" on records it.
- **Duty words in English and one translation (#845).** With English text, ask for work until a field duty
  is assigned: the order toast and the board name the duty itself (for example "a reconnaissance sweep"), not
  "a task for the company", and its result toast is the duty's own line, not "It went well.". Night patrol,
  an incident and their choices read as before. Switch to Deutsch and repeat: the same duties in German. The
  debug log shows the duties loaded with no "duty '...' skipped" line.
- **Cleanups (#842, #843, #844).** Nothing should change: an existing save with an enlisted soldier loads,
  duties are still offered by the column's state (march, siege, garrison), and battle pay, trust and
  promotion work as before.
- **Column line (#838).** On the wait menu the board shows, under the activity line, "Column: ..." with the commander party's
  current activity (for example besieging or travelling to a settlement). Let the column change what it is
  doing and the line follows within a few seconds; with the commander in a battle, note what the engine
  says. No line when he has no party or is inside a settlement. After a battle the soldier joined, the Column line
  must not name the commander's home settlement (a leftover battle army names its seeded objective).
- **Past service (#837).** Reach Sergeant under one lord (25 days, 350 XP, 2 duty wins), leave
  honorably, and take the oath with him again: you begin as Specialist and the kit can be drawn. A lord of
  ANOTHER clan of the same kingdom gives two ranks lower, so it takes a Staff Sergeant to show a gain.
  Desert and try him again: the offer is greyed with his refusal for 60 days, and the desertion warning
  names that cost. The oath line names the restored rank. Service XP and days served restart at zero.
- **End of the term (#836).** Run `taom.set_term_end` while enlisted (cheat mode on), then let a day
  tick: the inquiry shows once. Press Escape on it: he signs on again (the bonus, if the commander can
  pay it, and the trust arrive, and the term restarts). Run it again and choose "Leave with honor": an
  honourable discharge (wages settled, kit reclaimed). A battle or conversation at that moment delays the
  offer to the next daily tick.
- **Promotion notice (#835).** A promotion shows the notice with the commander's portrait. The kit line
  appears on the board when a kit waits and disappears after drawing it from the quartermaster. A
  promotion inside the top tier (Sergeant First Class and above) promises no kit after drawing the Staff
  Sergeant kit.
- **Personal spoils (#834).** Win a field battle in service with several kills: up to 3 items land in
  the inventory with their modifiers kept, and one "Spoils of your own blade" toast lists them. Lose a
  battle: nothing. Fall, take control of a soldier, kill an enemy: no spoil, no kill XP and no kill credit in the merit score; the fall is still recorded. Discharge
  afterwards: the spoils stay, with the one known exception that an unmodified spoil of the same item as a
  piece he was issued can be taken back at an honourable discharge when no unmodified copy of that
  issued piece is left in the inventory (sold, lost or worn); spoils are never recorded in the ledger.
- **Desertion cost (#833).** Desert during the minimum term: relation with the commander drops by 15,
  Honor XP by 100, wages are forfeited, the kit is kept, and both confirmations name the cost.
  Control: an honourable release after the term costs nothing.
- **Wars during service (#832).** (1) As a clan player, the commander declares a war mid-term (the
  mirror follows within the hour), makes peace (the mirror ends), and his clan changes kingdom (the
  mirror recomputes); then discharge leaves only the pre-oath wars. (2) A relieved raid in a war
  begun after the oath: no relation loss and no private war. (3) As a vassal, his crown and the
  commander's realm go to war: the soldier sits out battles, then the release modal appears after the
  battle, on the map; note whether the commander's party attacks him. (4) A War of the Ring scripted
  war while enlisted: one declaration, no phantom mirror entry. (5) A release while held in the
  commander's town (the settlement encounter must not block it and he must walk out). (6) An
  independent-clan crown war. (7) The commander's realm losing its last fief while serving as a clan:
  no peace flip-flop. (8) #960: as a vassal, the lord's realm declares war on a third faction after the oath: the soldier
  sits out that battle and his kingdom gains no war, and he joins the next battle against a faction
  his kingdom does fight.
- **Village raids (#831).** The in-game check (state stays `EnlistedAttached`,
  status line, release allowed, no raid menu; siege, sally-out and field battle still join; a raid
  the commander fights off as defender still joins). Also (#954): a relief lord attacks the raiding
  commander: the soldier joins within a pump pass, the encounter menu (reads "encountered <village>",
  vanilla parity) shows Attack, and Attack opens a village battle. After a win: no raid menu, he
  leaves the army and returns to `EnlistedAttached`. Commander loses: the normal defeat path. Lord
  retreats: the raid ends, he returns to service, and the commander's next raid is skipped. After a
  mission: no raid menu. Plain and militia-defended raids are still skipped. PROBE: a relief at the
  highest fast-forward speed, then check whether the battle was already decided when he joined
  (timing risk 1). Undecided: talking to the commander mid-raid.
- **Save-load mid-service, then a battle**, and save-load inside the wait menu.
- Remaining smaller unknowns: SetNextMenu timing vs `EncounterGameMenuBehavior`; camera handoff;
  TimeAcceleration interplay; equipment visuals per race (erebor, goblin, the four orc cultures); and
  the FieldCommission offer flow with enlisted suppression.

### The 2026-08-08 live session and what it left owed

> Rescued 2026-08-09 from **inside** the auto-generated backlinks region, where it had been sitting
> below the `backlinks-start` marker. `build_backlinks.py`'s `splice_footer` keeps only
> `content[:start]` + the regenerated footer + `content[end:]`, so every line here would have been
> silently deleted by the next run — and that run was already armed: today's handoff doc became this
> file's 4th inbound reference while the footer still listed 3.

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

**Still NOT verified — and the review pass is why this list matters.** Nothing else in the 2026-08-08 batch
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
8. **Seven-rank ladder (#936).** Load an old save with a Sergeant: he reads as Staff Sergeant with the
   same pay and kit. Promote through the new ranks (Sergeant First Class, Master Sergeant, Sergeant
   Major) and read each promotion toast and the board line ("Toward Sergeant First Class" at Staff
   Sergeant). Ask the quartermaster for kit at the top tier and read the answer ("You already carry
   the best kit my stores can issue."). Rank names and that answer show in English until the paid
   translation run.

### Still owed beyond testing

- **Batch 11 (content beats)** — not started.
- **12-language translation — DONE, and re-counted 2026-08-09.** `taom_enlistment_strings.xml` holds
  **225** keys and all 12 of the 12 `Languages/<L>/std_taom_enlistment_strings_<loc>.xml` files are
  **id-identical to English**, verified by set comparison rather than by count — a matching count
  with a differing key would pass the weaker check. It read "178" until today, which was true on
  2026-08-08 and then absorbed the 26 duty-result toasts (#428), the 2 duty toast keys the adapter
  composes, the 6 commander-loss strings, and the earlier status-board work. That was true on
  2026-08-09; the #828, #829 and #936 strings (the seven rank names and the best-kit answer among
  them) ship English until the next paid translation run, tracked in #939.

  **Duty text has one source (#845).** `enlistment_duties.json` carries the English of every duty
  row as `{=taom_enlist_duty_<id>_<suffix>}English` strings: `title` on every row, `success` and
  `failure` result lines on a field duty, and `body` plus a `label` on each of `optionA` and
  `optionB` (keys `_opta` and `_optb`) on an interactive duty or incident. The code shows the row's
  own strings, because for an English game `MBTextManager.GetLocalizedText` returns the inline text
  after `{=key}` and never reads the registered row: `InteractiveDutyPresenter` passes the popup
  strings to the inquiry, `FieldDutyRuntime` renders the row's title and result line in its two
  toasts, and the status board gets the active duty's title through `ServiceStatusModel.ActiveDutyTitle`.
  All keys come from one formula, `DutyText.KeyFor`. A generic text applies only to a duty whose row
  is no longer loaded.
  `EnlistmentContentConfigProvider` skips a row whose text is missing, is not keyed exactly
  `taom_enlist_duty_<id>_<suffix>`, or has no English, and logs a warning naming the row. The keys
  are composed at runtime, so a grep for a literal `{=key}` cannot find them:
  `tools/harvest_duty_strings.py` reads the JSON and adds or updates the matching rows in
  `taom_enlistment_strings.xml`, which stays the translation registry (`--check` exits 1 on drift,
  `--apply` writes). `DutyTextTests.EveryShippedDutyString_MatchesTheRegisteredRowByteForByte`
  pins the two files together, and `UnregisteredLocalizationKeyBaselineTests` covers the rest.
  An interactive duty or incident has no result strings of its own: its `_success` and `_failure`
  rows hold the two shared sentences ("It went well.", "It didn't go as planned."), which the
  presenter shows with the same inline text, and they stay hand-registered rows that the harvester
  only checks for.

  **To add a duty:** add the row to `enlistment_duties.json` with its strings (`title`, plus
  `success` and `failure` on a field duty, or `body` and both option `label`s on an interactive duty
  or incident), run `python tools/harvest_duty_strings.py --apply`, then seed or translate the new
  keys (`translate_with_claude.py --sync-ids`). A new interactive duty or incident needs its
  shared `_success` and `_failure` rows registered by hand FIRST (`--apply` refuses, and `--check`
  fails, until both exist under their own keys), then run the harvester.
  Changing the English of an existing string rewrites its registered row and makes the 12
  translations of it stale: the harvester lists every changed key, and the follow-up is the
  checklist in the translator guide, "The checklist when you edit an existing English string".
- **One entry point remains over the ADR-002 ceiling**, pre-dating this arc:
  `EnlistmentBattleBehavior` (157). `EnlistmentMeritMissionBehavior` was the other (163); the
  left-the-field fix paid that down to 139 by extracting its inline geometry into
  `MeritGeometryAccumulator` + `MeritGeometryScanner`.

## Board activities: sit-out and the grace state (#1016)

`ServiceStatusService.ResolveActivity` picks the board's activity line, first match wins:

| Activity | When | Line |
|---|---|---|
| `Unavailable` | no commander, dead, captive, no party, or a party that is not active | `taom_enlist_act_lost` |
| `Raiding` | the commander's party is raiding | `taom_enlist_act_raid` |
| `SittingOut` | in a map event, and `OathSitOutRule` says the oath keeps the soldier out of that battle | `taom_enlist_act_sitout` |
| `InBattle` | in a map event otherwise | `taom_enlist_act_battle` |
| `Besieging`, `InSettlement`, `Marching` | as before | their own keys |

- **One rule.** `OathSitOutRule.Decide` is the single place that decides a battle the oath keeps him out of: the commander's realm at war with the
  player's own faction (#832), or a vassal whose kingdom is not at war with the opposing leader's faction (#960). Both
  `ServiceBattleService.TryJoin` and the board call it, so the board cannot promise a place in the line for such a battle. The board does not mirror the join's other refusals (state, no side, not joinable). It
  never sits out a player who is already in a map event, the same carve-out the join has.
- **Inactive party.** An inactive commander party counts as unavailable, matching the attach check and the grace logic, and the "Column:" line is left out.
  In v1.5.5 the engine clears the hero's party whenever it deactivates one (`MobileParty.RemoveParty` sets `Hero.PartyBelongedTo = null`), so nothing visible changes today.
- **Retired:** `taom_enlist_wait_text` had no reader since 3164fb291 and is gone from the English file, the 12 language files
  and the translation caches.

**Owed in-game checks.** (1) Sit-out: as a vassal, with the commander fighting a faction your kingdom is not at war with, the
board reads "Your commander is fighting a battle your oath keeps you out of." and not "Hold your place in the line"; at war
with that faction it still reads the battle line. (2) Regression check, grace: after the commander is captured or loses his party, the board
reads "You have lost the column" with no "Column:" line underneath.

## Co-op (#967)

Two enlistment mission behaviors have the co-op role `Unsafe` in `CoopMissionRoles`, so `AddTaomBehavior` does not add them to a mission while a co-op session is live: `EnlistmentMeritMissionBehavior` (it scores the enlisted player's battle merit into this save's enlistment state) and `FieldCommissionMissionLogic` (it already required `IsAuthority`, which is false on every peer that runs a mission). In a session, battle merit is not scored and field commission logic does not run; outside a session nothing changes. `EnlistmentBattleRoleMissionBehavior` and `EnlistmentBattleFormationMissionBehavior` shape only this peer's own player slot and stay `Local`. The table and the in-game list are in [coop-interop.md](coop-interop.md#mission-behaviors-967-c7); the offer-hiding half for guests (#966) is the ledger's row C7 in [coop-compat-gap-ledger.md](../research/coop-compat-gap-ledger.md).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/orientation.md](../ai-includes/orientation.md)
- [docs/features/coop-interop.md](./coop-interop.md)
- [docs/features/diplomacy.md](./diplomacy.md)
- [docs/features/host-review.md](./host-review.md)
- [docs/features/map-event-guard.md](./map-event-guard.md)
- [docs/features/return-to-army.md](./return-to-army.md)
- [docs/features/wanderer-allegiance.md](./wanderer-allegiance.md)
- [docs/features/war-of-the-ring.md](./war-of-the-ring.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/configs-balance.md](../modding/configs-balance.md)
- [docs/modding/equipment-rosters.md](../modding/equipment-rosters.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reviews/enlistment-morning-handoff-2026-08-09.md](../reviews/enlistment-morning-handoff-2026-08-09.md)
- [docs/reviews/rca-map-event-observer-2026-09-06.md](../reviews/rca-map-event-observer-2026-09-06.md)

<!-- backlinks-end -->
