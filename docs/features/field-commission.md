# Field Commission (Battlefield Promotions)

> **STATUS: CODE-COMPLETE AND WIRED, AWAITING IN-GAME VERIFICATION** (#376, committed in
> `b1852a7a`). Registered in `Main/IoC.cs` (after Enlistment — the `NullEnlistmentStateQuery`
> fallback uses `IfAlreadyRegistered.Keep`, so the real query must already be in the container)
> and in `Main/SubModule.cs` (campaign behaviour + `FieldCommissionMissionLogic` in the
> unconditional `AddTaomBehavior` block). Nothing has run in a live game. Reviews:
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
| `meritThreshold` | int | Merit required before a promotion offer is queued. Must be ≥ 1. |
| `retainerAllowance` | int | Extra companions allowed beyond the clan-tier limit before offers defer. Must be ≥ 0. |
| `skillPointsPerLevel` | int | Skill-value budget granted per hero level (see Commission Skill Budget above). Must be ≥ 1. |
| `allowedRaceNames` | string[] | Race names (matched via `IRaceManager`) eligible for promotion. Blank/whitespace entries are sanitized out; a missing/null field defaults to `["human","dwarf","elf"]`. |

### Current Values

Defaults mirror the donor mod's tuning (`RatioThreshold=1.3`, `MeritPerKill=1`, `MeritThreshold=8`)
except `AllowMultiplePromotions`/`PromotedCompanionsIncreaseLimit`/`EnableBonusCompanionLimit`
(donor-only knobs) — dropped as YAGIN in favor of the single `retainerAllowance` int, and
`AlwaysPromote` (donor's manual-testing flag) — dropped in favor of the `taom.fc_grant_merit` cheat.

**Reload scope:** `Reuse.Singleton` — changes require a full game restart, not a save-load.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/FieldCommission/FieldCommissionMeritService.cs` / `IFieldCommissionMeritService.cs` | Eligibility, kill-tracking, merit banking, orphan-merit consolidation, promotability gate, offer queue |
| `Main/Features/FieldCommission/FieldCommissionOfferFlowService.cs` / `IFieldCommissionOfferFlowService.cs` | Inquiry chain orchestration: promote? → companion-room → rename → hero creation → completion |
| `Main/Features/FieldCommission/FieldCommissionConfigProvider.cs` / `IFieldCommissionConfigProvider.cs` | JSON config load + validation |
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
- `TAOM.Tests/Features/FieldCommission/FieldCommissionCheatsTests.cs` — pure formatter output.
- `TAOM.Tests/Features/FieldCommission/NullEnlistmentStateQueryTests.cs` — null-object contract.
- `TAOM.Tests/Features/FieldCommission/FieldCommissionBindingTests.cs` (`TestCategory=BindingVerification`) — every TaleWorlds signature this feature depends on, most load-bearing being the `TextInquiryData` 12-parameter ctor order (bug fix (b)).

**111 tests total, all passing** (`dotnet test TAOM.Tests/TAOM.Tests.csproj --filter "FullyQualifiedName~FieldCommission"` → `Passed! - Failed: 0, Passed: 111, Skipped: 0, Total: 111`).

## How to Add a New Config Knob

1. Add the property to `FieldCommissionConfig` (`Main/Features/FieldCommission/Domain/`).
2. Add per-field validation in `FieldCommissionConfigProvider.Validate` (revert-to-default + warn on invalid).
3. Add the default to `field_commission_config.json`.
4. Add a test per validation rule (valid value, invalid value, NaN if it's a float) in `FieldCommissionConfigProviderTests`.
5. If the value should also be MCM-editable, see "Proposed MCM Properties" below — wiring that in is deferred to the orchestrator (`TaomSettings.cs` is single-owner).

## Proposed MCM Properties (not yet wired — `TaomSettings.cs` is single-owner)

```csharp
[SettingPropertyGroup("Battlefield Promotions")]
[SettingPropertyBool("Enable Battlefield Promotions", Order = 0, ...)]
public bool EnableFieldCommission { get; set; } = true;

[SettingPropertyGroup("Battlefield Promotions")]
[SettingPropertyFloatingInteger("Fair-Fight Ratio Threshold", 0.5f, 3.0f, "0.00", Order = 1, ...)]
public float FieldCommissionRatioThreshold { get; set; } = 1.3f;

[SettingPropertyGroup("Battlefield Promotions")]
[SettingPropertyInteger("Merit Per Kill", 1, 10, Order = 2, ...)]
public int FieldCommissionMeritPerKill { get; set; } = 1;

[SettingPropertyGroup("Battlefield Promotions")]
[SettingPropertyInteger("Merit Threshold", 1, 100, Order = 3, ...)]
public int FieldCommissionMeritThreshold { get; set; } = 8;

[SettingPropertyGroup("Battlefield Promotions")]
[SettingPropertyInteger("Retainer Allowance", 0, 10, Order = 4, ...)]
public int FieldCommissionRetainerAllowance { get; set; } = 0;
```

Wiring these requires a `FieldCommissionSettingsProvider` bridging class (mirroring
`Main/Features/Diplomacy/TaomSettingsProvider.cs`) that the JSON `FieldCommissionConfigProvider`
would need to consult — deferred, since the JSON config alone is a complete, working config surface
today.

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

## GitHub Issue

- **Issue:** #376 — Battlefield Promotions (Field Commission native rewrite)
- **Status:** Open (code complete + wired; in-game smoke and `/localize` for the 13 strings pending)
