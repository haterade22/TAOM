# Enlistment — serve as a soldier in a lord's party

> **STATUS: FEATURE-COMPLETE, AWAITING IN-GAME VERIFICATION** (#375, checkpoints 1-2:
> `25a3340c` + `b1852a7a`). Core, dialogs, content systems, duties and equipment are all
> built and unit-tested (394 tests). Nothing has run in a live game yet — the in-game
> checklist at the bottom is the remaining gate, along with `/localize` for the new
> player-facing strings.

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
  (redirect-exempt) BEFORE any encounter/menu push. Failed joins roll back to
  parked-attached. Post-battle, the state stays `EnlistedBattle` while the loot encounter
  is open (so the guard can't eat aftermath menus); wait-menu init + reconciler close the
  loop.

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

**Duties.** 13 field duties collapse onto 5 mechanics — `HuntSpawnedParty` (4 flavors),
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

## Testing

394 tests in `TAOM.Tests/Features/Enlistment/` (full suite 5415 green): transition-table
matrix, discharge invariants, Entity-State-Matrix load rows, reconciler policy (grace,
captivity, prisoner-commander-with-live-party), record round-trip incl. NaN/forward-compat,
menu redirect policy + cap, battle ordering/rollback/loot-guard, binding pins, config
semantic validation (one test per rule), wage orchestration (solvency × arrears × channel,
incl. regression tests for the mint-mode double-payment and shortfall-overcount bugs),
promotion thresholds at both evaluation points, merit scoring/bands incl. NaN ratios, duty
gating/rotation/lifecycle per mechanic, equipment resolver fallback + payoff math.

**Owed at ship (in-game gates — nothing below has run in a live game):**
SetNextMenu timing vs EncounterGameMenuBehavior; encounter join from parked state; camera
handoff; captivity entry/exit mid-service; food/wage/morale ticks for the inactive
MainParty; save-load inside the wait menu; TimeAcceleration interplay; the duty
spawn→hunt→complete loop and its target-party cleanup; equipment visuals per race
(erebor, goblin, the four orc cultures); promotion/merit/incident popups; and the
FieldCommission offer flow with enlisted suppression.
