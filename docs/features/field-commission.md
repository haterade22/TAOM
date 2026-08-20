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

- **Race allow-list** (`allowedRaceNames`, default `["human","dwarf","elf"]`) — validated via
  `IRaceManager.IsValidRaceId` BEFORE `GetRaceNameFromId` (validate-before-lookup rule: an
  unresolvable race id fails closed, never falls through to whatever fallback name the lookup
  returns). Reading the design brief, this allow-list is the SAME mechanism that keeps creature
  troops (cave trolls, berserkers) unpromotable — their race id fails the allow-list, not a
  separate hardcoded troop-id blacklist.
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
| `allowedRaceNames` | string[] | Race names (matched via `IRaceManager`) eligible for promotion. Blank/whitespace entries are sanitized out; a missing/null field defaults to `["human","dwarf","elf"]`. |

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
| `FieldCommissionMaxOffersPerBattle` | 1–20 | `2` | `maxOffersPerBattle` |
| `FieldCommissionRatioThreshold` | 0.1–3.0 | `1.3` | `ratioThreshold` |
| `FieldCommissionMeritPerKill` | 1–10 | `1` | `meritPerKill` |
| `FieldCommissionMeritThreshold` | 1–100 | `32` | `meritThreshold` |
| `FieldCommissionRetainerAllowance` | 0–10 | `0` | `retainerAllowance` |

`skillPointsPerLevel` and `allowedRaceNames` stay JSON-only (advanced / pack-author territory).

**Precedence, and its one sharp edge.** MCM has no "unset" state — once MCM is loaded, every property
reads back a value, so for the six exposed knobs the MCM value always wins over the JSON one, even
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
| `Main/Features/FieldCommission/FieldCommissionConfigProvider.cs` / `IFieldCommissionConfigProvider.cs` | JSON config load + validation |
| `Main/Features/FieldCommission/FieldCommissionSettingsProvider.cs` | MCM-over-JSON merge; decorates the above behind the same interface |
| `Main/Features/FieldCommission/Domain/FieldCommissionMcmSnapshot.cs` | Value-equality read of the six MCM knobs; the merged config's cache key |
| `Main/Features/FieldCommission/NullEnlistmentStateQuery.cs` | Null-object fallback for `IEnlistmentStateQuery` |
| `Main/Features/FieldCommission/FieldCommissionIoC.cs` | DryIoc registration |
| `Main/Features/FieldCommission/Domain/*.cs` | Pure POCOs + `CommissionSkillBudget` + `TroopUpgradeGraph` |
| `Main/Features/FieldCommission/Hooks/FieldCommissionBehavior.cs` | `CampaignBehaviorBase` entry point |
| `Main/Features/FieldCommission/Hooks/FieldCommissionMissionLogic.cs` | `MissionLogic` entry point (kill tracking) |
| `Main/Features/FieldCommission/Hooks/MapEventSideHelper.cs` | Pure `MapEventSide` boundary helper (keeps the behavior under the ADR-002 line budget) |
| `Main/Features/FieldCommission/Cheats/FieldCommissionCheats.cs` | `taom.fc_grant_merit`, `taom.fc_status` |
| `Main/Adapters/ITroopRosterQueryAdapter.cs` / `TroopRosterQueryAdapter.cs` | Wraps `MobileParty`/`CharacterObject`/`SkillObject` roster + troop-template queries (and the one roster-decrement write) |
| `Main/Adapters/IHeroCommissionAdapter.cs` / `HeroCommissionAdapter.cs` | Wraps `HeroCreator`/`Hero`/`HeroDeveloper`/`AddCompanionAction`/`AddHeroToPartyAction`/`ClanTierModel` |
| `Main/Adapters/IInquiryPresenterAdapter.cs` / `InquiryPresenterAdapter.cs` | Wraps `InformationManager`/`TextObject`/`InquiryData`/`TextInquiryData` |
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
- `TAOM.Tests/Features/FieldCommission/FieldCommissionOfferFlowServiceTests.cs` — full inquiry chain, no-room deferral + retainer allowance, hero-creation failure leaves state untouched.
- `TAOM.Tests/Features/FieldCommission/FieldCommissionSettingsProviderTests.cs` — the MCM-over-JSON
  merge (per-knob override, NaN/infinity revert, both clamp directions), snapshot value equality, the
  tick-path caching contract, and the compiled-default-vs-JSON-default pin.
- `TAOM.Tests/Features/FieldCommission/FieldCommissionCheatsTests.cs` — pure formatter output.
- `TAOM.Tests/Features/FieldCommission/NullEnlistmentStateQueryTests.cs` — null-object contract.
- `TAOM.Tests/Features/FieldCommission/FieldCommissionBindingTests.cs` (`TestCategory=BindingVerification`) — every TaleWorlds signature this feature depends on, most load-bearing being the `TextInquiryData` 12-parameter ctor order (bug fix (b)).

**194 tests total, all passing** (`dotnet test TAOM.Tests/TAOM.Tests.csproj --filter "FullyQualifiedName~FieldCommission"` → `Passed! - Failed: 0, Passed: 194, Skipped: 0, Total: 194`). The jump from 154 came with #486: an exhaustive truth table on `EquipmentResetPlan`, behavioural tests on `Patch71.Fill`, and 19 more binding drift-guards.

**Deliberately untested:** the three guards at the top of `FieldCommissionBehavior.OnTick`
(co-op authority / enlisted / master toggle; `PlayerEncounter.Current` and `MapEvent.PlayerMapEvent`;
`Hero.MainHero.IsPrisoner` and `MobileParty.MainParty`). They read TaleWorlds statics that the MSTest
host cannot construct, and ADR-008 does not require entry-point coverage for exactly this reason.
Everything they gate is tested on the service behind them. If one of these ever needs pinning, the
honest route is an adapter over the statics, not a mock of the engine.

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
- **#418** — `/localize` for the 10 `taom_fc_*` strings (with #375's 66 enlistment keys, 76 total).
  **Open**, blocked on `ANTHROPIC_API_KEY` not being set in the build environment.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
