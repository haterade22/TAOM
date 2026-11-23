# Field Commission (Battlefield Promotions)

> **STATUS: SHIPPED TO PLAYERS, STILL AWAITING IN-GAME VERIFICATION** (#376). Registered in
> `Main/IoC.cs` (after Enlistment — the `NullEnlistmentStateQuery` fallback uses
> `IfAlreadyRegistered.Keep`, so the real query must already be in the container) and in
> `Main/SubModule.cs` (campaign behaviour + `FieldCommissionMissionLogic` in the unconditional
> `AddTaomBehavior` block).
>
> **Nothing here has ever run in a live game** — and it reached players in that state, which is how
> #415 (promoted companions reported un-interactable) arrived. Treat every behavioural claim below as
> code-verified, not play-verified.
>
> Commits: `b1852a7a` (build + wiring) → `5fa48f95` (15 defects, MCM group, diagnostics) →
> `14160f0a` (promotion bar raised). Reviews: `docs/reviews/rca-field-commission-2026-08-07.md`,
> `docs/reviews/rca-enlistment-content-2026-08-05.md`.

## Overview

Troops that rack up kills in fair-fight battles the player WINS can be promoted into named
companions. TAOM native rewrite of the `TAOM_Promoted` ("RF_Promoted") donor mod — kept mechanics,
fixed 8 concrete bugs, and added TAOM-specific gates (race allow-list, enlisted suppression, co-op
authority, companion-limit awareness) on top. Issue #376.

A promoted companion can also be sent back to the ranks (#540): the hero is removed and one soldier of
the troop they came from rejoins the party. See "Dismissing a promoted companion" below.

**Open: #625.** `HeroCommissionAdapter` fills the companion from the troop's first battle set, so a
commissioned archer keeps its generated `ladder_*` bow or crossbow as hero equipment, and twelve of
those cells hit above the spec's `hero_ceiling` (Bow 90, Crossbow 105). Mike's decision (2026-09-18):
swap a ladder launcher for its line's donor at commission.
[ranged-ladders.md](ranged-ladders.md) lists the cells.

## Why This Exists

- **Vanilla behavior:** troops never individually distinguish themselves — a soldier who racks up
  20 kills across a campaign is mechanically identical to one who never swung a weapon.
- **TAOM requirement:** LOTR-flavored "battlefield promotion" — a soldier who proves themselves
  becomes a named companion, mirroring how real armies field-promote.
- **Without this feature:** no mechanical payoff for keeping troops alive and fighting well; every
  named companion has to come from the tavern/wanderer pool.

## Architecture

### Design Challenge

The donor mod got the mechanic mostly right but shipped 8 concrete defects (see the table below) —
most severe: it deducted a troop's banked merit the moment an offer was QUEUED, before the player
ever saw or responded to the inquiry, so declining a promotion (or the game crashing/being closed
before the inquiry was answered) silently destroyed the merit anyway. TAOM's rewrite fixes all 8
while keeping the kept mechanics (fair-fight eligibility, per-troop-type kill merit, merit
inheritance up the troop-upgrade tree, the promote→companion-path→rename offer chain, hero-from-
troop construction, companion-limit awareness) and adds TAOM's own gates on top (race allow-list,
enlisted suppression, co-op host authority).

### Solution Approach

Two thin entry points (ADR-002) convert sealed TaleWorlds types to primitives at the boundary and
delegate everything else to two services:

- **`FieldCommissionBehavior`** (`CampaignBehaviorBase`) — `MapEventStarted`/`MapEventEnded` (fair-
  fight eligibility + merit banking), `TickEvent` (offer pump), save/load (`SyncData`,
  `_justLoadedFromSave` pattern mirrored from `CultureConversionBehavior`).
- **`FieldCommissionMissionLogic`** (`MissionLogic`, never `MissionBehavior` — see the
  `BehaviorTreeMissionLogic` regression rule) — `OnAgentRemoved` kill tracking. Registered
  unconditionally into `SubModule.OnMissionBehaviorInitialize`; self-filters in `AfterStart` on
  `Campaign.Current != null` and per-kill on `ICoopSessionProvider.IsAuthority`.
- **`FieldCommissionMeritService`** — all merit/eligibility/promotability/offer-queue bookkeeping.
  Pure primitives in and out; depends on `ITroopRosterQueryAdapter` + `IRaceManager` (existing,
  reused) + `IFieldCommissionConfigProvider`.
- **`FieldCommissionOfferFlowService`** — orchestrates one offer's inquiry chain (promote? →
  companion-room check → rename → hero creation → merit completion).
- **`CommissionSkillBudget`** / **`TroopUpgradeGraph`** — pure domain logic (100% unit-testable, no
  TaleWorlds references at all).

### Component Diagram

```
field_commission_config.json
        |
FieldCommissionConfigProvider
        |
FieldCommissionMeritService ---- ITroopRosterQueryAdapter --- MobileParty/CharacterObject/SkillObject
        |    \
        |     TroopUpgradeGraph (pure BFS)
        |
FieldCommissionOfferFlowService ---- IHeroCommissionAdapter --- HeroCreator/Hero/HeroDeveloper/AddCompanionAction
        |    \                  \
        |     CommissionSkillBudget (pure)
        |                        IInquiryPresenterAdapter --- InformationManager/TextObject
        |
FieldCommissionBehavior (CampaignBehaviorBase)   FieldCommissionMissionLogic (MissionLogic)
```

## Donor Bug Fixes (mandatory, all 8 addressed)

| # | Bug | Donor behavior | TAOM fix |
|---|-----|-----------------|----------|
| (a) | Merit deducted at QUEUE time | `QueuePromotionOffers` subtracted `offersToQueue * threshold` from the bank the moment an offer was built, before the player ever saw the inquiry — decline/cancel/save-quit/full-retinue all destroyed the merit anyway. | `FieldCommissionMeritService.EndBattle` NEVER deducts. `CompleteOffer(troopId)` — called ONLY after a hero is actually created — is the sole deduction point. |
| (b) | `TextInquiryData` param mis-order | The rename box's default text landed in `soundEventPath` (position 11), leaving `defaultInputText` (position 12) empty — the rename prompt never pre-filled the soldier's name. | `InquiryPresenterAdapter.ShowRenamePrompt` uses the verified 1.4.7 ctor with named args; `defaultInputText: troopName`. Pinned by `FieldCommissionBindingTests.TextInquiryData_CtorBindingResolves_WithVerifiedParameterOrder`. |
| (c) | `MapEventStarted`/`Ended` didn't gate on the player's own event first | `ResetBattleState()` ran unconditionally at the top of `OnMapEventStarted`/inside `OnMapEventEnded`'s `finally`, so ANY map event anywhere in the world (AI vs AI) reset the player's in-progress battle tracking. | `FieldCommissionBehavior` gates `IsPlayerMapEvent` FIRST, before any state touch, and tracks the SPECIFIC `MapEvent` instance (`_trackedMapEvent`) — `OnMapEventEnded` compares by reference before doing anything. |
| (d) | Mutated cached `GameTexts` | `GameTexts.FindText(id)` returns a shared/cached `TextObject`; calling `SetTextVariable` on it corrupts that shared instance for every other call site expecting the untouched default. | `InquiryPresenterAdapter` builds a FRESH `new TextObject("{=taom_fc_*}...")` per call, never `GameTexts.FindText`. |
| (e) | Raw `Clan.Heroes` mutation | The dropped Occupation.Lord/Family path did `Clan.PlayerClan.Heroes.Add(hero)` directly. | TAOM is companion-only; `HeroCommissionAdapter` uses `AddCompanionAction.Apply(Clan, Hero)` exclusively — never touches `Clan.Heroes` for writes. |
| (f) | Mission behavior had no self-filter | `PromotedMissionBehavior.AfterStart` unconditionally fetched the campaign behavior with no guard. | `FieldCommissionMissionLogic.AfterStart` checks `Campaign.Current != null`; every kill additionally gates on `ICoopSessionProvider.IsAuthority`. Registered unconditionally in `SubModule`. |
| (g) | No config validation | `PromotedSettings.Load()` swallowed any parse exception and kept prior defaults, with no per-field semantic validation (a negative `RatioThreshold` or zero `MeritThreshold` would pass through). | `FieldCommissionConfigProvider` mirrors `CultureConversionConfigProvider`: per-field revert-to-default + warning, `FiniteFloatValidator` on the float field, `ObjectCreationHandling.Replace` on the race-name list (Json.NET's default append-merge would duplicate entries onto the compiled default). |
| (h) | Unbounded upgrade-tree BFS | `FindUpgradedDescendantInParty` capped depth at 3 but had NO visited set — a cyclic upgrade graph (data error or future-proofing) would loop forever. | `TroopUpgradeGraph.FindDescendantInRoster` adds every visited id to a `HashSet<string>` before enqueueing; `TroopUpgradeGraphTests` includes 2-node and self-referencing cycle fixtures asserting termination. |

## TAOM-Specific Gates (new, not from the donor)

- **Race allow-list** (`allowedRaceNames`, default: every soldier race, `human`, `dwarf`, `elf`,
  `orc`, `uruk`, `uruk_hai`, `pale_uruk`, `dg_uruk`, `goblin`, `berserker`). Validated via
  `IRaceManager.IsValidRaceId` BEFORE `GetRaceNameFromId` (validate-before-lookup rule: an
  unresolvable race id fails closed, never falls through to whatever fallback name the lookup
  returns). The five races left out (`cave_troll`, `hill_troll`, `nazghul`, `saruman`, `sauron`) are
  the creatures and unique heroes: their race fails the allow-list, so there is no separate
  troop-id blacklist. Until #612 (2026-09-17) the default was `human`/`dwarf`/`elf`, written in
  #376 on the premise of a Free Peoples player. Character creation offers seven evil races, and
  for every one of those campaigns the feature was inert: merit banked, no offer ever queued. A
  Free Peoples player who fields recruited orcs can promote one; TAOM's own companion minting is
  not alignment-gated (decided for [wanderer allegiance](wanderer-allegiance.md), #575). The
  default is copied in the JSON, `FieldCommissionConfigProvider.DefaultAllowedRaceNames` and the
  `FieldCommissionConfig` constructor; `ShippedJson_AllowedRaceNames_MatchCompiledDefault` pins
  the three together and `ShippedJson_AllowedRaceNames_AreAllKnownRaces` pins every name to
  `raceage/race_age_config.json`. A player's or pack author's typo in the list is caught at
  runtime instead: `FieldCommissionMeritService` checks each configured name against
  `IRaceManager.IsValidRaceName` once per process and logs a `[FieldCommission]` warning naming
  the entry, because an unknown name never matches a troop and the race is otherwise silently
  unpromotable. Known limitation: `BasicTableauRaceGuard` has verified only `uruk` as safe for the
  agentless `BasicCharacterTableau` renderer, so a promoted orc, goblin, Uruk-hai or berserker
  companion shows a human-headed thumbnail on the clan screen and in the encyclopedia until that
  race passes the per-race render test ([hero-race](hero-race.md)); not a crash, and the same is
  already true of every orc lord.
- **Enlisted suppression** — while `IEnlistmentStateQuery.IsEnlisted`, battle eligibility is forced
  `false` (the player's "own party" health count isn't a trustworthy fair-fight signal while
  enlisted) and the offer pump defers. No merit accrues from enlisted battles at all.
- **Co-op authority** — merit accrual, the offer pump, and hero creation all gate on
  `ICoopSessionProvider.IsAuthority`; a co-op client never tracks a battle, never queues an offer,
  never creates a hero.
- **Companion-limit awareness with a retainer allowance** — `IHeroCommissionAdapter.GetCompanionRoomInfo()`
  reports current companions vs `Campaign.Current.Models.ClanTierModel.GetCompanionLimit(Clan)`
  (perk bonuses already folded in). `retainerAllowance` (default 0) lets the player accept N over
  the tier limit before an offer defers with a "no room" message — merit stays banked either way.
- **Commission skill budget** (TAOM-original, not ported) — the donor copied a troop template's
  skill values verbatim, so a high-tier elite troop instantly became a master-skilled companion.
  `CommissionSkillBudget` caps every skill to `min(maxSkillValue, heroLevel * skillPointsPerLevel)`
  (default 5 points/level, 300 max) and grants 1 focus point per non-zero skill + a flat +2 to every
  core attribute — applied via the verified `HeroDeveloper.SetInitialLevel`/`SetInitialSkillLevel`/
  `AddFocus`/`AddAttribute` (`checkUnspentFocusPoints:false`/`checkUnspentPoints:false` — fresh
  initialization, not spending from an existing pool). `Hero.Level` is set directly (it is a plain
  public field, not a property) so hero level always equals troop level.

## Configuration

### Config File: `Main/_Module/ModuleData/field_commission/field_commission_config.json`

| Field | Type | Description |
|-------|------|-------------|
| `enabled` | bool | Master toggle. |
| `ratioThreshold` | float | Player-healthy / enemy-healthy must be BELOW this for merit to accrue. Default 1.3. 0 is a valid (if extreme — "never eligible") choice; negative/NaN/Infinity revert to default. |
| `meritPerKill` | int | Merit banked per kill of a troop type in an eligible, won battle. Must be ≥ 1. |
| `meritThreshold` | int | Merit required before a promotion offer is queued. Must be ≥ 1. Default 32. |
| `retainerAllowance` | int | Extra companions allowed beyond the clan-tier limit before offers defer. Must be ≥ 0. |
| `maxOffersPerBattle` | int | Hard ceiling on promotion offers queued by ONE won battle, across all troop types. Must be ≥ 1. Default 2. Merit above the cap is kept and re-queues after the next won battle. |
| `skillPointsPerLevel` | int | Skill-value budget granted per hero level (see Commission Skill Budget above). Must be ≥ 1. |
| `diagnostics` | bool | Writes the `[FieldCommission]` trace to the TAOM debug log. Default false. |
| `allowedRaceNames` | string[] | Race names (matched via `IRaceManager`) eligible for promotion. Blank/whitespace entries are sanitized out; a missing/null field defaults to the ten soldier races listed under "Race allow-list" above. |

### Current Values

`RatioThreshold=1.3` and `MeritPerKill=1` still mirror the donor mod. **`MeritThreshold` and
`MaxOffersPerBattle` no longer do** — retuned 2026-08-08 because promotions landed too easily in play
(see the note below). `MaxOffersPerBattle=1` was the donor's `AllowMultiplePromotions=false`
behaviour; it is now 2.

> **Why the bar moved, and what it does not fix.** Merit pools per troop **TYPE**, not per soldier —
> `_merits` is keyed on the `CharacterObject` StringId, so a 30-strong stack shares one counter. At
> the old threshold of 8 that was well under a kill each, inside a single battle. Raising it to 32
> makes a 20-stack take roughly three to four battles instead of one.
>
> Two structural causes are deliberately **not** addressed and remain true: merit still never decays
> (it is a permanently rising ratchet, persisted across saves, reduced only by an accepted
> promotion), and a 40-stack still accrues ~5× faster than an 8-stack of the same tier. The raise
> changes the slope of the curve, not its shape. Also unchanged: `ratioThreshold` stays 1.3, so
> battles you comfortably win still count — at 1.3 you can outnumber the enemy 5:4 and qualify, and
> because the numerator counts only your own party, an army battle where allies do most of the work
> is structurally always eligible. If the bar still feels low after play, per-stack scaling and a
> minimum troop level are the levers that change the shape.

Dropped donor knobs: `PromotedCompanionsIncreaseLimit`/`EnableBonusCompanionLimit`/
`BonusCompanionLimitValue` — all three were **dead code in the donor itself**
(`PromotedHelper.GetAdditionalCompanionLimit` has no caller anywhere in the donor tree), replaced by
the single `retainerAllowance` int; and `AlwaysPromote` (the donor's manual-testing flag) — replaced
by the `taom.fc_grant_merit` cheat.

**Reload scope:** the JSON file is read by a `Reuse.Singleton` `Lazy<T>` provider, so **JSON edits
need a full application restart**. The MCM knobs below do not — they are re-read on every access.

### MCM: "Battlefield Promotions" (GroupOrder 43)

| Property | Range | Default | JSON field it overrides |
|----------|-------|---------|-------------------------|
| `EnableFieldCommission` | bool | `true` | `enabled` |
| `FieldCommissionOffersPerBattle` | 1 to 20 | `2` | `maxOffersPerBattle` |
| `FieldCommissionRatioThreshold` | 0.1 to 3.0 | `1.3` | `ratioThreshold` |
| `FieldCommissionMeritPerKill` | 1 to 10 | `1` | `meritPerKill` |
| `FieldCommissionMeritToPromote` | 1 to 100 | `32` | `meritThreshold` |
| `FieldCommissionRetainerAllowance` | 0 to 10 | `0` | `retainerAllowance` |
| `EnableFieldCommissionDiagnostics` ("Promotion Diagnostics") | bool | `false` | `diagnostics` |

`skillPointsPerLevel` and `allowedRaceNames` stay JSON-only (advanced / pack-author territory).

**Precedence, and its one sharp edge.** MCM has no "unset" state — once MCM is loaded, every property
reads back a value, so for the seven exposed knobs the MCM value always wins over the JSON one, even
when the player has never touched the slider. That is TAOM's house behaviour (every
`*SettingsProvider` works this way) and it is the right default: the player outranks the pack author
on tuning they can see. The consequence a pack author needs to know is that customising an
MCM-exposed field in `field_commission_config.json` only takes effect for players running without
MCM. Fields that must hold regardless belong in the JSON-only set above.

**The clamp bounds the player's input, not the pack author's.** `Merge` applies the slider ranges only
when the MCM value is present; a JSON value passes through untouched, because the JSON provider has
already validated it on its own, stricter terms. Without that split, `ratioThreshold: 0` — explicitly
legal in the JSON provider, meaning "never eligible" — came back out of the 0.1-floored slider clamp
as `0.1` and quietly re-enabled the thing the pack author had turned off. Two tests pin both
directions.

**Turning the master switch off is fully inert and reversible.** `enabled` is read at
`OnMapEventStarted` (no battle is tracked, so no kill is ever registered) and at `OnTick` (no offer
is pumped). Banked merit stays in the save, and companions already promoted are ordinary companions —
nothing is taken back.

**Wiring.** `FieldCommissionSettingsProvider` implements `IFieldCommissionConfigProvider` and
*decorates* the JSON `FieldCommissionConfigProvider`, rather than exposing a parallel scalar surface
like TAOM's other `*SettingsProvider` classes. Two reasons: every consumer already reads several
fields off one `FieldCommissionConfig`, so no constructor changes; and `GetConfig()` is called from
`CampaignEvents.TickEvent`, so the merged config is cached against a
`FieldCommissionMcmSnapshot` value-equality key and allocates nothing while the sliders are still.
IoC registers the JSON provider as a concrete type and the interface via `RegisterDelegate`, so the
decorator cannot resolve to itself.

Every compiled MCM default must equal its JSON counterpart —
`FieldCommissionSettingsProviderTests.CompiledMcmDefaults_MatchShippedJsonDefaults` fails the build
if they drift, because a player without MCM reads the JSON and a player with MCM at default reads the
literal, and those two must describe the same game.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/FieldCommission/FieldCommissionMeritService.cs` / `IFieldCommissionMeritService.cs` | Eligibility, kill-tracking, merit banking, orphan-merit consolidation, promotability gate, offer queue |
| `Main/Features/FieldCommission/FieldCommissionOfferFlowService.cs` / `IFieldCommissionOfferFlowService.cs` | Inquiry chain orchestration: promote? → companion-room → rename → hero creation → completion |
| `Main/Features/FieldCommission/FieldCommissionDismissService.cs` / `IFieldCommissionDismissService.cs` | Dismissal back to the ranks (#540): the verdict, the remove-then-refund order, the picker and confirm chain |
| `Main/Features/FieldCommission/FieldCommissionConfigProvider.cs` / `IFieldCommissionConfigProvider.cs` | JSON config load + validation |
| `Main/Features/FieldCommission/FieldCommissionSettingsProvider.cs` | MCM-over-JSON merge; decorates the above behind the same interface |
| `Main/Features/FieldCommission/Domain/FieldCommissionMcmSnapshot.cs` | Value-equality read of the seven MCM knobs; the merged config's cache key |
| `Main/Features/FieldCommission/NullEnlistmentStateQuery.cs` | Null-object fallback for `IEnlistmentStateQuery` |
| `Main/Features/FieldCommission/FieldCommissionIoC.cs` | DryIoc registration |
| `Main/Features/FieldCommission/Domain/*.cs` | Pure POCOs + `CommissionSkillBudget` + `TroopUpgradeGraph` + the dismissal verdict types (`DismissOutcome`, `DismissCandidate`, `PromotedHeroSnapshot`) |
| `Main/Features/FieldCommission/Hooks/FieldCommissionBehavior.cs` | `CampaignBehaviorBase` entry point |
| `Main/Features/FieldCommission/Hooks/FieldCommissionMissionLogic.cs` | `MissionLogic` entry point (kill tracking) |
| `Main/Features/FieldCommission/Hooks/FieldCommissionDismissDialogBehavior.cs` | `CampaignBehaviorBase` entry point: the dismissal dialogue line, removal armed on `ConversationEnded` (#540) |
| `Main/Features/FieldCommission/Hooks/FieldCommissionDismissMenuBehavior.cs` | `CampaignBehaviorBase` entry point: the settlement-menu picker option (#540) |
| `Main/Features/FieldCommission/Hooks/MapEventSideHelper.cs` | Pure `MapEventSide` boundary helper (keeps the behavior under the ADR-002 line budget) |
| `Main/Features/FieldCommission/Cheats/FieldCommissionCheats.cs` | `taom.fc_grant_merit`, `taom.fc_add_companion`, `taom.fc_status` |
| `Main/Adapters/ITroopRosterQueryAdapter.cs` / `TroopRosterQueryAdapter.cs` | Wraps `MobileParty`/`CharacterObject`/`SkillObject` roster + troop-template queries (and the two roster-count writes: the promotion decrement, the dismissal refund) |
| `Main/Adapters/IHeroCommissionAdapter.cs` / `HeroCommissionAdapter.cs` | Wraps `HeroCreator`/`Hero`/`HeroDeveloper`/`AddCompanionAction`/`AddHeroToPartyAction`/`ClanTierModel`; for dismissal, the hero snapshot and `KillCharacterAction.ApplyByRemove` |
| `Main/Adapters/IInquiryPresenterAdapter.cs` / `InquiryPresenterAdapter.cs` | Wraps `InformationManager`/`TextObject`/`InquiryData`/`TextInquiryData`, and for the dismissal picker `MBInformationManager`/`MultiSelectionInquiryData` |
| `Main/_Module/ModuleData/field_commission/field_commission_config.json` | Configuration data |

## Dependencies

- `TAOM.Features.Enlistment.IEnlistmentStateQuery` (cross-feature, existing) — enlisted suppression.
- `TAOM.Features.CoopInterop.ICoopSessionProvider` (existing) — host-authority gating.
- `TAOM.Core.Domain.IRaceManager` (existing) — race allow-list validation.
- `ITroopRosterQueryAdapter` / `IHeroCommissionAdapter` / `IInquiryPresenterAdapter` (new, this feature).

## Tests

- `TAOM.Tests/Features/FieldCommission/CommissionSkillBudgetTests.cs` — level clamping, per-skill cap, max-skill-value cap, negative-value clamp, config-clamp defense.
- `TAOM.Tests/Features/FieldCommission/TroopUpgradeGraphTests.cs` — depth cap, 2-node and self-referencing cycles, branch-order BFS, null-entry tolerance.
- `TAOM.Tests/Features/FieldCommission/FieldCommissionConfigProviderTests.cs` — valid parse, missing file, malformed JSON, every field's validation rule (incl. NaN), race-name sanitization, append-merge defense.
- `TAOM.Tests/Features/FieldCommission/FieldCommissionMeritServiceTests.cs` — ratio/NaN gate, deduct-on-completion, decline-keeps-merit, orphan-merit consolidation, promotability gate (fail-closed), promoted-hero pruning, SyncData round-trip.
- `FieldCommissionMeritServiceTests.RegisterKill_FromManyThreadsAtOnce_CountsEveryKill` pins the #634 lock: `RegisterKill` is called from `OnAgentRemoved`, which native can raise off the main thread, so two removals at once must not lose a kill (unsynchronised, 20,000 kills of one troop from four threads counted 9,709). `EndBattle_TiedKills_OffersFollowFirstKillOrder` pins why it is a lock and not a concurrent map: `EndBattle` breaks kill ties in the map's insertion order.
- `TAOM.Tests/Features/FieldCommission/FieldCommissionOfferFlowServiceTests.cs` — full inquiry chain, no-room deferral + retainer allowance, hero-creation failure leaves state untouched.
- `TAOM.Tests/Features/FieldCommission/FieldCommissionDismissServiceTests.cs`: one test per entity state in the
  dismissal matrix, remove-before-refund ordering, nothing partially applied on any refusal, the picker and
  confirm chain (#540).
- `TAOM.Tests/Features/FieldCommission/InquiryPresenterPauseTests.cs`: every popup in
  `InquiryPresenterAdapter` that waits for an answer is raised with `pauseGameActiveState: true`
  (source-text pin: the file's inquiry calls and its `pauseGameActiveState: true` arguments must be
  equal in number, and at least five, so a new popup with the default flag fails and an empty scan fails).
- `TAOM.Tests/Features/FieldCommission/FieldCommissionSettingsProviderTests.cs` — the MCM-over-JSON
  merge (per-knob override, NaN/infinity revert, both clamp directions), snapshot value equality, the
  tick-path caching contract, and the compiled-default-vs-JSON-default pin.
- `TAOM.Tests/Features/FieldCommission/FieldCommissionCheatsTests.cs` — pure formatter output.
- `TAOM.Tests/Features/FieldCommission/NullEnlistmentStateQueryTests.cs` — null-object contract.
- `TAOM.Tests/Features/FieldCommission/FieldCommissionBindingTests.cs` (`TestCategory=BindingVerification`): every TaleWorlds signature this feature depends on, most load-bearing being the `TextInquiryData` 12-parameter ctor order (bug fix (b)). Since #540 also the
  dismissal set: `CharacterObject.OriginalCharacter`, the seven-parameter `TroopRoster.AddToCounts`,
  `KillCharacterAction.ApplyByRemove` and its `isForced` default, and the `MultiSelectionInquiryData`
  parameter names, because the picker binds them by name.

**266 tests total, all passing** (`dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~FieldCommission"` → `Passed! - Failed: 0, Passed: 266, Skipped: 0, Total: 266`, 2026-09-04). The jump from 194 came with #540: 51 dismissal tests and 21 binding drift-guards. The jump from 154 to 194 came with #486: an exhaustive truth table on `EquipmentResetPlan`, behavioural tests on `Patch71.Fill`, and 19 more binding drift-guards.

**Deliberately untested:** the three guards at the top of `FieldCommissionBehavior.OnTick`
(co-op authority / enlisted / master toggle; `PlayerEncounter.Current` and `MapEvent.PlayerMapEvent`;
`Hero.MainHero.IsPrisoner` and `MobileParty.MainParty`). They read TaleWorlds statics that the MSTest
host cannot construct, and ADR-008 does not require entry-point coverage for exactly this reason.
Everything they gate is tested on the service behind them. If one of these ever needs pinning, the
honest route is an adapter over the statics, not a mock of the engine. The two #540 entry points sit
under the same rule (`Hero.OneToOneConversationHero`, `MBTextManager.SetTextVariable`, the
`CampaignGameStarter` line and menu registrations, `MenuCallbackArgs`); everything they decide is tested
on `FieldCommissionDismissService`.

## How to Add a New Config Knob

1. Add the property to `FieldCommissionConfig` (`Main/Features/FieldCommission/Domain/`).
2. Add per-field validation in `FieldCommissionConfigProvider.Validate` (revert-to-default + warn on invalid).
3. Add the default to `field_commission_config.json`.
4. Add a test per validation rule (valid value, invalid value, NaN if it's a float) in `FieldCommissionConfigProviderTests`.
5. If the value should also be MCM-editable, add the property to `TaomSettings.cs` under the
   "Battlefield Promotions" group, add a nullable field to `FieldCommissionMcmSnapshot` (equality
   AND hash — a field left out of equality makes its slider look dead until a restart), read it in
   `FieldCommissionSettingsProvider.Capture`, clamp it in `Merge`, and extend
   `CompiledMcmDefaults_MatchShippedJsonDefaults` with the new default pair.

## Performance

No per-frame allocation beyond the existing `TickEvent` pump (which no-ops immediately unless a
promotion offer is actually pending). Merit/roster lookups are dictionary/list operations against
an in-memory `Dictionary<string,int>` capped by the number of DISTINCT troop types the player has
ever fielded — not a concern at any realistic party size.

## Changelog

- 2026-08-04 — Initial build (#376): merit service, offer-flow service, 3 adapters, JSON config,
  cheats, 111 tests.
- 2026-08-05 — Wired into `Main/IoC.cs` + `Main/SubModule.cs` and committed (`b1852a7a`).
  Registering the four `LordConversations` prefixes of the sibling Enlistment feature also
  required dispositions in `CoopVetoClassificationTests` — that suite fails the build on any
  skip-original prefix with no recorded co-op stance, and it caught them.
- 2026-08-07 — **15 defects fixed** (`5fa48f95`) after a player report of un-interactable promoted
  companions (#415). A 28-agent adversarial pass plus an independent Codex pass could **not**
  reproduce the reported symptoms and disproved the attractive hypotheses; what they did find was an
  uncapped prompt storm, an offer queue that outlived its campaign, a promotion that could create a
  companion with no soldier consumed, a promoted-hero list that emptied itself on every load, and a
  decline that recorded nothing. Four of the fifteen were introduced by the fix pass and caught by
  review. Shipped alongside: the **MCM "Battlefield Promotions" group** and the `[FieldCommission]`
  diagnostics trace. Full write-up: `docs/reviews/rca-field-commission-2026-08-07.md`.
- 2026-08-08 — **Promotion bar raised** (`14160f0a`): `meritThreshold` 8 → 32,
  `maxOffersPerBattle` 1 → 2. Promotions were landing too easily because merit pools per troop TYPE
  rather than per soldier. See "Current Values" for what this does and does not fix.
- 2026-09-04: **Dismissal back to the ranks** (#540). A promoted companion can be sent back through
  their own dialogue line or a settlement-menu picker; the hero is removed and one origin soldier
  rejoins the party. See the section below.
- 2026-09-17: **Race allow-list widened to every soldier race** (#612). A player running Isengard
  reported no promotions and found the `human`/`dwarf`/`elf` list; the seven evil races from
  character creation join it. An existing evil-faction save already holds banked merit, so it
  starts offering on its next won fair battle, capped by `maxOffersPerBattle`.
- 2026-09-22: `RegisterKill` takes a lock (#634). Its caller, `FieldCommissionMissionLogic.OnAgentRemoved`, runs on whichever thread native raised the removal, and a v1.4.8 player log caught one off the main thread. `BeginBattle` and `EndBattle` run on the campaign map, never beside a mission's removals. Not a `ConcurrentDictionary`: `EndBattle` breaks kill ties in the map's insertion order, which a hash-ordered map would scramble.
- 2026-11-14: every Battlefield Promotion popup pauses the campaign. The offer itself already did,
  but the rename prompt that follows "Promote" was raised with `pauseGameActiveState: false` and the
  discharge picker with the default (false), so the party kept moving on the map while the player
  typed a name (player report). Closing a paused popup resumes the speed the map had (v1.5.7
  `GauntletQueryManager.HandleQueryCreated` :244-259 and `CloseQuery` :261-294, read on the v1.5.7
  install); no `TimeControlMode` is touched. Pinned by `InquiryPresenterPauseTests`. Not verified in
  game. Owed at play or fast-forward speed: wait for an offer and click Promote, type a new name in
  the rename box and confirm (typing under the engine pause is unproven: every vanilla text inquiry
  is raised unpaused), then check that the companion carries that name, that the map stays still
  while the box is open, and that it resumes at the old speed when the box closes. If typing is
  blocked, put the rename prompt back to unpaused and hold the clock another way.
- 2026-11-17: a promoted dwarf wears its own tunic in the civilian slots (#1431). The Erebor,
  Ered Luin and Fords of War dwarf troops ended with a reference to a vanilla Battanian civilian
  template, which the engine loader turns into a civilian set for the troop
  (`BasicCharacterObject.Deserialize`, v1.5.7 lines 387-412), so `CreateCompanionFromTroop` copied
  the Highland Tartan and never reached its battle-kit fallback. Each reference is now two inline
  dwarf tunic rosters (`Realm.civilian_kits` for the generated Ered Luin file). Gate:
  `DwarfCivilianKitTests`. A companion promoted before this change keeps the clothes stored on
  the hero. Other races and the human realms still carry the reference (#1433); dwarf tunics
  carry no `Civilian` item flag, so civilian mode refuses to re-equip them (#1432).
- 2026-11-22: a promoted goblin, orc, uruk or elf wears its own culture's clothes in the civilian
  slots (#1433). 131 non-human troops (Misty Mountains goblins, Dol Guldur, Gundabad, Isengard, Mordor,
  Mirkwood and the Fords of War copies) ended with a reference to a vanilla Aserai or Battanian civilian
  template, which the loader turns into a civilian set. Each reference is now inline civilian rosters
  (one per elf) with Body and Leg only: the vanilla sets' sword, headwear, gloves and cape are gone.
  The orc and uruk kits are their culture's light pieces. The elves, all of level 36 and above, wear the
  source `mirkwood_body` and `mirkwood_boots`, the elite Palace Guard armour (body 48), because a band
  clone resolves back to its source at that level; the two militia elves already had inline
  `taom_band_lt_mirkwood_*` kits. Dol Guldur goblins wear `sk_dg_uruk_chest_light_a` and `_b` with
  `sk_dg_uruk_boots_light_a`. Gate:
  `NonHumanCivilianKitTests`. None of the new civilian items carries the `Civilian` item flag (an
  Armory-side gap tracked by #1432), so a piece taken off cannot be put back in the civilian tab. Human
  troops keep vanilla civilian templates for now.

## Firing a promoted companion (#486)

Firing a promoted companion threw `NullReferenceException` inside `Hero.ResetEquipments` until
`Patch71_HeroResetEquipmentsGuard` landed. Worth understanding before touching companion creation,
because the cause is structural rather than a slip.

`HeroCreator.CreateSpecialHero(troop, ...)` routes through `CreateHero(useCharacterAsTemplate:
true)`, so a promoted companion's `Hero.Template` is **the line troop it was promoted from**, not a
wanderer template. `RemoveCompanionAction.ApplyInternal` calls `ResetEquipments()` when a wanderer
is fired, and that method clones `Template.FirstBattleEquipment`, `FirstCivilianEquipment` and
`FirstStealthEquipment` with no null checks. On a troop, `FirstCivilianEquipment` is
`AllEquipments.FirstOrDefaultQ(e => e.IsCivilian)`, which is null for **743 of 895 TAOM troop
blocks** (every Dale, Dunland, Gondor, Harad and Rhûn troop; Rivendell, Lindon, Umbar and Erebor
are the exceptions). Vanilla never hits this because `spnpccharacters.xml` wanderers always declare
a civilian set.

The state left behind is what makes it worse than a normal caught exception: the throw lands after
`CompanionOf = null`, the roster decrement and `MakeHeroFugitiveAction`, and **before**
`OnCompanionRemoved` dispatches, so every listener is skipped while the companion is already gone.
PatchShield catches it, which means the session keeps running on that state. A player who hit this
pre-fix should reload rather than play on.

Two things this establishes for future work here:

- **`Occupation.Wanderer` is what makes the fire path reachable** (`HeroCommissionAdapter`
  sets it). Anything that changes a promoted companion's occupation changes which engine paths
  apply to them.
- **Guarding at creation cannot fix a method that re-derives from the template.**
  `CreateCompanionFromTroop` already fell back to battle gear for the civilian slot, and it made no
  difference: `ResetEquipments` reads the troop again rather than the hero's stored kit. The same
  commit also fixed that call passing `useSourceEquipmentType` as true on the fallback, which
  retyped the hero's civilian equipment `EquipmentType.Battle`.

Full mechanism, the campaign-wide-singleton trap in `Hero.BattleEquipment` and friends, and the
bandit-culture stealth variant: [harmony-patch-registry.md](../reference/harmony-patch-registry.md)
under `Patch71_HeroResetEquipmentsGuard`.

## The civilian set's type, and lost gear (#1544)

A player reported that a promoted companion's gear went back to the troop's default kit after some
time, and the items they had given him were gone. The player's exact trigger is not known. The
mechanism below produces that symptom, and a Codex probe reproduced it by calling the engine's own
`ResetEquipment` on test objects; it was not reproduced in a game.

1. For a troop with no civilian set, `Hero.SetInitialValuesFromCharacter` (v1.5.7 Hero.cs:2321-2350)
   clones a set from `neutral_culture`'s `default_civilian_equipment_roster_neutral`. That roster
   (SandBoxCore `sandboxcore_equipment_sets.xml:16198`) declares `<EquipmentSet>` with no `civilian`
   or `equipmentType` attribute, and `MBEquipmentRoster.InitEquipment` (:91) types such a set
   `Battle`. The #486 fix to the `FillFrom` flag kept that type; it did not cause it.
2. Opening the inventory saves a copy of each hero's three sets
   (`InventoryLogic.PartyEquipment.InitializeCopyFrom`, :56-72). Cancel or Reset runs
   `PartyEquipment.ResetEquipment` (:74-99), which writes each copy back to the set of the same type.
   The civilian copy is typed `Battle`, so it lands in the battle set after the real battle copy, and
   the battle kit becomes the civilian contents: the troop's kit. The item roster goes back to the
   session's backup, so gear equipped in an earlier, committed session is not returned.

`HeroCommissionAdapter.MakeCivilian` retypes the set at promotion. On load,
`FieldCommissionBehavior` runs `RepairCivilianKitType` for every promoted id that survives the prune,
which retypes older companions and writes one `[FieldCommission] ... retyped Civilian (#1544)` line
for each. Gear already lost cannot be recovered. `taom.fc_add_companion` promotes a soldier at once
for this check; it refuses any troop `CanPromote` refuses, so a hero character (the player's own
included) cannot be passed to it.

## Dismissing a promoted companion (return to the ranks, #540)

Nothing in the feature took a commission back until #540. Vanilla's own dismissal exists, but players
could not find it: "I no longer have need of your services" sits under "About your position in the
clan", and its condition (`CompanionRolesCampaignBehavior.companion_fire_condition`) hides the line
whenever `Settlement.CurrentSettlement` is set. It is reachable only through Party screen > Talk on the
world map. Talk to the companion inside a town and the line is absent.

### Two entry points, one service

- **In person.** `FieldCommissionDismissDialogBehavior` adds "Your commission is ended. Return to the
  ranks." under `hero_main_options` for a promoted companion who qualifies, with an are-you-sure
  exchange. No settlement gate, so it works in a keep or tavern scene as well as from the Party screen.
  The removal runs on `ConversationEnded`, not in the farewell line's consequence: vanilla never removes
  a conversation partner from inside a scene conversation (its fire is map-only), and this behaviour
  does not start. The hand-off is a one-shot field consumed on every conversation end, whichever line
  won, so it cannot leak into the next chat.
- **From the settlement menu.** `FieldCommissionDismissMenuBehavior` adds "Discharge a promoted
  companion" to the town, castle and village menus while at least one promoted companion qualifies. It
  opens a picker (`MultiSelectionInquiryData`, one row per candidate, "name (was troop)"), then a
  confirm inquiry that names the returning troop and says the gear is lost. This path needs no
  conversation, which is the point while #415 is open. It is deliberately not gated on the MCM master
  switch: an already-promoted companion is an ordinary companion, and turning promotions off must not
  strand one.

Both end in `FieldCommissionDismissService.DismissAndReport`, so the verdict, the ordering and the
feedback line live in one place. Co-op clients see neither entry point; the behaviours gate on
`ICoopSessionProvider.IsAuthority`.

### What happens

The hero is removed with one engine call, `KillCharacterAction.ApplyByRemove`, and one soldier of the
troop the hero was promoted from is added to the main party roster, wounded if the companion was
wounded. The origin troop is `Hero.Template`, which is `CharacterObject.OriginalCharacter`, a saveable
field, so it survives a load. Merit is untouched. The companion's gear is lost, as it is when vanilla
fires a wanderer; the confirm text says so. The 250 denar stipend goes back to the clan leader through
the engine's own kill path (`GiveGoldAction.ApplyBetweenCharacters` for a clan member who is not the
leader).

Inside a settlement scene the hero also has a live `Agent`, and nothing on the engine's removal path
touches it: `KillCharacterAction` only drops the `LocationCharacter`, which is the spawn list for the
NEXT scene entry, so without more the dismissed companion keeps standing in the tavern as a ghost the
player can still click (`MissionConversationLogic.IsThereAgentAction` never asks whether the hero is
alive). Vanilla never meets this because its fire line is map-only. `RemoveCompanionFromGame` therefore
removes the agent first, the way `MissionAgentHandler.FadeoutExitingLocationCharacter` removes a
character leaving through a passage, with the same refusal on a mission that is already ending; on the
map there is no mission and nothing to do (deep-review data-flow finding, 2026-09-04). It uses the
instant form, `Agent.FadeOut(hideInstantly: true, hideMount: true)`, rather than the visible fade: a
fading agent stays `Active` for the length of the fade, `IsThereAgentAction` never checks
`IsFadingOut`, and a click in those frames would open a conversation with a hero that is already dead
and un-clanned, which the wanderer-hire lines would treat as hireable. The instant form is what vanilla
uses for a departing multiplayer peer (second review round, lifecycle agent). The picker path never
meets a live agent at all: `Mission.Current` is cleared in `Mission.OnMissionStateFinalize`, called from
`MissionState.OnFinalize` when the scene is left, and the town, castle and village menus belong to
`MapState`, so the removal is a no-op there and only the map and scene conversation paths differ.

Order inside `Dismiss`: evaluate, remove, refund, forget the promoted id. Remove first because the
engine step is the only one with a runtime failure mode (a deferral behind a DeathMark, a throw) and
it is detectable in-frame, so a refusal there leaves nothing to roll back. The refund's own
preconditions were checked a moment earlier in the same paused frame; if the engine still refuses it,
the hero is already gone, so the service logs a warning rather than pretending, and still forgets the
id.

### Why one call and not vanilla's two

Vanilla's fire line runs `RemoveCompanionAction.ApplyByFire` and then `KillCharacterAction.ApplyByRemove`.
Eight vanilla listeners subscribe to `CampaignEvents.CompanionRemoved` on the installed v1.4.8,
enumerated by the subscription call rather than by handler name (five in
`TaleWorlds.CampaignSystem`: `LordsNeedsTutorIssueBehavior`, `CompanionRolesCampaignBehavior`,
`HeroSpawnCampaignBehavior`, `PartyRolesCampaignBehavior`, `PlayerTrackCompanionBehavior`; three in
`SandBox`, which the decompile dump does not carry: `CompanionDismissCampaignBehavior`,
`DefaultNotificationsCampaignBehavior`, `FamilyFeudIssueBehavior`). Six behave the same for the Fire
and Death details on a wanderer in the main party; the spawn teleport skips both. Two differ, and both
differences favour Death:
`DefaultNotificationsCampaignBehavior` shows "left your clan" only for Fire, and this feature shows its
own line instead; `CompanionDismissCampaignBehavior`, for Fire only and whenever the player stands in a
settlement, dereferences `ConversationMission.OneToOneConversationAgent` with no null check to stop the
fired companion following the player. That is a vanilla NRE for a Fire applied inside a settlement
outside a conversation, which is exactly the picker path, and it is the likely reason vanilla's own fire
line is map-only. The Fire path also adds a fugitive interlude, runs `Hero.ResetEquipments` (the #486
crash site, guarded by Patch71) on a hero that is about to be removed, and cuts `CompanionOf` first,
which is what the stipend hand-back keys on (`Hero.Clan` is `CompanionOf ?? _clan`). `ApplyByRemove`
alone reaches `RemoveCompanionAction.ApplyByDeath` from `KillCharacterAction.ApplyInternal`, after
`MakeDead` has taken the hero out of the roster, and is the call the Refuge warden rollback already
ships. The first draft of this section counted four listeners because it was written from the dump,
and the second counted seven by grepping for a handler name; the compatibility review corrected both
against the installed DLLs, by listing subscribers to the event.

### Entity state matrix

| State | Verdict | Detected by |
|---|---|---|
| In the main party, healthy | Ok | every gate passes |
| In the main party, wounded | Ok, the soldier comes back wounded | `PromotedHeroSnapshot.IsWounded` |
| Main party in a field battle, assault or siege | `PartyInBattle` | `PartyBelongedTo.MapEvent` or `SiegeEvent`, the predicate `KillCharacterAction` defers on |
| Governor, own party leader, caravan leader, refuge warden, prisoner (enemy or own party), fugitive | `NotInMainParty` | `PartyBelongedTo?.IsMainParty != true` |
| Dead or disabled | `HeroGone` | absent from `Hero.AllAliveHeroes` |
| Alive but no longer the clan's companion | `NotACompanion` | `Hero.IsPlayerCompanion` |
| Ordinary (unpromoted) companion | `NotPromoted` | not in `_promotedHeroIds`; vanilla's fire line remains their path |
| Origin troop removed from the XML, or a hero template | `TroopUnresolved` | `GetTroopInfo` missing or `IsHero` |
| Player enlisted | `PlayerEnlisted` | `IEnlistmentStateQuery.IsEnlisted`, symmetric with the offer pump |
| Co-op client | hidden and no-op | `ICoopSessionProvider.IsAuthority` in the behaviours |

`RemovalFailed` is not a verdict of `Evaluate`; it is what `Dismiss` returns when the adapter reports the
engine did not remove the hero, and nothing else has been touched at that point.

### Owed in-game smoke

None of this has run in a live game. In order: dismiss from the Party screen talk on the map; dismiss
through the town menu picker; dismiss from a keep or tavern scene conversation (vanilla never exercises
a removal after a scene conversation, so this is the path to watch); confirm the option and the line
are absent while besieging; save, reload, then dismiss (the origin troop must survive the load);
confirm the stipend returns and no death notification appears; `taom.fc_status` shows one fewer
promoted companion.

## How to verify in game

Owed for #1431. Preconditions: a restart after the build, the full desktop module set, a save with
dwarf soldiers in the party.

1. Promote an Erebor noble-line soldier (for example `erebor_noble_guard`) with Field Commission.
   Open the companion's inventory and switch to the civilian tab. Pass: Body is an [Erebor] Noble
   Tunic, Leg is [Erebor] Leather Boots I, and no Highland Tartan shows.
2. Promote an Ered Luin soldier and an `erebor_oathsworn` soldier. Pass: Body is an [Erebor]
   Civilian Tunic.
3. In one companion's civilian tab, take the tunic off and put it back. This checks #1432: record
   whether "cannot be equipped with civilian equipment" appears.

Owed for #1433, same preconditions, with orc, goblin and elf soldiers in the party.

1. Promote a `dg_goblin_crawler` soldier. Open the civilian tab. Pass: Body is a Dol Guldur light
   chest and Leg a Dol Guldur light boot, with no Aserai or Battanian clothes.
2. Promote a `mirkwood_woodsman` soldier. Pass: Body and Leg are the Mirkwood Palace Guard body and
   boots (`mirkwood_body`, `mirkwood_boots`).
3. Promote a Mordor or Gundabad uruk. Pass: the civilian tab shows that culture's own light kit.
4. Take one civilian piece off and try to put it back. Expected today: the game refuses, because the
   new items carry no `Civilian` flag (#1432). This is not a #1433 failure.

Companions promoted before the change keep their stored tartan; that is expected.

## GitHub Issue

- **#376** — Battlefield Promotions (Field Commission native rewrite). **Open**: code complete,
  wired and shipped; in-game verification still owed.
- **#415** — promoted companions reported with no dialogue / crash on interaction. **Open**: the
  symptoms were **not** reproduced and remain unexplained. Reproduce with **Promotion Diagnostics**
  on and attach the log.
- **#486**: firing a promoted companion NREd in `Hero.ResetEquipments`. **Closed 2026-08-20** on
  `Patch71_HeroResetEquipmentsGuard`, code-verified rather than play-verified: the in-game smoke
  (fire a promoted Dale troop, then a bandit-culture one for the stealth branch) was not run.
  Reopen if it recurs. Two review passes, RCA in `docs/reviews/`.
- **#418**: `/localize` for the 27 `taom_fc_*` strings (17 of them from #540, with #375's 66 enlistment keys).
  **Open**, blocked on `ANTHROPIC_API_KEY` not being set in the build environment.
- **#540**: dismiss a promoted companion back to the ranks. **Open**: code complete, wired and
  reviewed; every in-game path is still owed (see "Owed in-game smoke" above).
- **#1431**: a promoted dwarf wore the Highland Tartan in the civilian slots. Code complete; the in-game
  check is under "How to verify in game". Follow-ups: #1432 (no `Civilian` item flag on dwarf tunics),
  #1433 (other races and human realms).
- **#1544**: a promoted companion lost given gear to the troop kit after an inventory Cancel. Code
  complete with unit tests; the in-game check is owed: `taom.fc_add_companion <troopId>`, give the
  companion new battle gear and press Done, open the inventory again and press Cancel. Pass: the new
  gear stays. On an older save, the load writes one "retyped Civilian" line for each promoted companion.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/dev-console.md](./dev-console.md)
- [docs/features/wanderer-allegiance.md](./wanderer-allegiance.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/configs-balance.md](../modding/configs-balance.md)
- [docs/modding/file-catalogue.md](../modding/file-catalogue.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reference/harmony-patch-registry.md](../reference/harmony-patch-registry.md)
- [docs/reference/provenance-register.md](../reference/provenance-register.md)

<!-- backlinks-end -->
