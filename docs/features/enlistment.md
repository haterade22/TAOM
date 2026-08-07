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
| **Calibrate diagnostic thresholds against a real session, not a guess** | The drift warning used `> 1f`; ordinary inter-tick drift while marching is ~1.8, so it fired on essentially every sync — **291 of one session's 299 warnings**. A per-world-map-event INFO line produced another 3674. Both are now DEBUG / properly thresholded. A diagnostic that fires constantly is indistinguishable from no diagnostic. |

**Verified in live play 2026-08-07:** field battle join (instant, via `MapEventStarted`), siege assault
join, hourly-recovery join when the immediate edge is missed, return to parked service after battle,
and config load without reverting. The measured steady-state drift while following is ~1.8 map units —
the player is teleport-chased rather than genuinely attached, which is what "dragged along" describes.

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
floor; the shortfall defers into arrears capped at 60. `ServiceRewardService.PayDailyWage`
spends that plan through exactly ONE channel (commander transfer or mint, per config) and
derives the new debt by conservation — owed minus delivered — rather than patching the
plan's fields. An honorable discharge settles remaining arrears; desertion forfeits them.

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

**Assignments.** Infantry / Archer / Cavalry / Support, changed by asking the commander;
costs a 7-day cooldown and a point of trust (the donor allowed free swaps in any
conversation, which made the choice weightless).

**Duties.** 13 field duties collapse onto 5 mechanics — `HuntSpawnedParty` (5 flavors),
`VisitSettlement` (5), `DeliverFood`, `CollectFood`, `WaitHours` — with the flavor,
targets, deadlines, gates and rewards as data rows in `enlistment_duties.json`, plus 11
interactive skill-check duties and 3 camp incidents through one presenter. Rows failing
validation are SKIPPED with a warning (never silently defaulted). `IArmyRhythmSnapshotService`
caches the world read once per game hour.

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

The Enlistment suite (`TAOM.Tests/Features/Enlistment/`, full suite 5448 green): transition-table
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

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
