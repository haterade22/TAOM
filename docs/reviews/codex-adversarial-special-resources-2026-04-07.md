# Codex Adversarial Review: SpecialResources vs TOR_Core CustomResources

**Date:** 2026-04-07
**Target:** working tree diff
**Verdict:** needs-attention

No-ship. TAOM's data-driven config is cleaner than TOR's hardcoded resource helpers, but the runtime semantics are materially weaker: troop spending is not transactional, resource enforcement is UI-only, raid income can fire for unrelated raids, and save/load does not migrate or sanitize balances.

## Findings

### [CRITICAL] Upgrade spending is applied outside the party-screen transaction and will survive Cancel

**File:** `PartyScreenLogic_UpgradeTroop_Patch.cs:15-29`

**TOR does:** Queues pending `_resourceChanges[]` during the party screen session. Applies on Done, discards on Cancel/Reset.

**TAOM does:** Spends resources in a Harmony postfix after every `UpgradeTroop` call. Never hooks screen close/reset. Resource deduction is written immediately to persistent storage and is not reverted on cancel.

**Impact:** Player upgrades troops, clicks Cancel, troop changes are reverted but resources are permanently lost.

**Fix:** Move resource spending to a screen-scoped pending transaction. Queue deltas during the party screen session, apply only on Done, discard on Cancel/Reset. Add explicit hooks for screen open/close or `PartyScreenLogic.Reset`.

### [CRITICAL] Resource affordability is advisory only — multi-upgrades can execute beyond budget, granting free upgrades

**File:** `PartyCharacterVM_InitializeUpgrades_Patch.cs:15-56`

**TOR does:** Uses `AddCommand` prefix plus precomputed mass budget to clamp upgrade count before execution.

**TAOM does:** Never validates or clamps `command.TotalNumber` against resource balance before `UpgradeTroop` runs. `InitializeUpgrades` postfix only edits visible UI counts/hints. If command count exceeds real budget (stale UI, popup mass-upgrade, other caller), upgrades execute and `StorageService.Add` clamps to zero.

**Impact:** Example: 3 scraps, cost 2 per upgrade, command for 2 upgrades → both execute, storage ends at 0, 1 upgrade is free.

**Fix:** Add an authoritative pre-execution guard on the command path. Clamp or reject upgrade commands before `PartyScreenLogic.UpgradeTroop` executes. Fail closed if queued spend exceeds remaining budget.

### [HIGH] Raid rewards can be granted for raids the player did not participate in

**File:** `SpecialResourcesBehavior.cs:108-116`

**TOR does:** Guards on player participation via `IsPlayerMapEvent` and victory checks.

**TAOM does:** `OnRaidCompleted` only checks `winnerSide == Attacker` then pays the main hero. Never checks `IsPlayerMapEvent`, player party, or whether the player was the victorious attacker.

**Impact:** Any successful attacker-side raid in the game awards scraps to the player as long as the player belongs to a configured kingdom.

**Fix:** Gate raid income on `component.IsPlayerMapEvent` and a player-victory check before awarding resources.

### [HIGH] Save/load bootstrap and kingdom-switch behavior are incomplete

**File:** `SpecialResourcesBehavior.cs:39-58`

**TOR does:** Stores resources by resource ID. Manages lifecycle around player's current culture/resource context.

**TAOM does:** Raw `heroId -> float` map. On load from pre-feature save, empty map — nothing seeds `starting_amount` (only happens in `OnNewGameCreated`). Hero can leave Mordor, hide balance while clanless, recover same scraps on rejoining.

**Impact:** Pre-feature saves start with zero resources and no migration. Kingdom-switching players can exploit stale balances.

**Fix:** Add load-time migration/bootstrap for legacy saves. Handle `OnHeroChangedClan`/kingdom transitions explicitly. Store by resource ID (or hero+resource) instead of single hero float.

### [HIGH] Loaded balances are trusted even when they exceed the configured cap

**File:** `SpecialResourceStorageService.cs:26-30`

**TOR does:** Clamps in stored-resource mutators. `AddCustomResource` enforces `[0, MaximumCustomResourceValue]` on every write.

**TAOM does:** Cap enforcement only exists on earning paths via `AddCapped`. `RestoreData` accepts arbitrary persisted values. `GetCurrentAmount` returns them unchanged.

**Impact:** Save-edited or stale pre-cap balances above max remain live after load. Only reduced if something later spends them.

**Fix:** Validate and clamp restored balances against current config during load/migration. Reject negative/NaN values.

### [LOW] Tests never exercise the risky behavior, patch, or config paths

**File:** `SpecialResourceServiceTests.cs:29-211`

**What's tested:** `SpecialResourceService` and `SpecialResourceStorageService` basic operations.

**What's NOT tested:** Cancel after upgrades, pre-execution clamping, raid participation gating, legacy-save bootstrap, kingdom changes, cap-on-load, malformed XML.

**Fix:** Add integration-style tests around the behavior and hook layer for all the above scenarios.

## What TAOM Does Better (Section 10)

1. **IoC vs static singletons** — TAOM uses DryIoc with `ISpecialResourceService`/`ISpecialResourceStorageService` interfaces. TOR uses `CustomResourceManager.Instance` static singleton. TAOM's approach is testable, mockable, and doesn't create hidden global state.

2. **Config-driven vs hardcoded** — TAOM defines resources and troop costs in XML (`special_resources_config.xml`, `troop_resource_costs.xml`). TOR hardcodes 10 resources in `CustomResourceManager.Initialize()` and per-resource helpers (`TeefHelper`, `OathGoldHelper`, etc.). TAOM can add new resources by editing XML; TOR requires new C# classes.

3. **Adapter pattern** — TAOM wraps TaleWorlds types behind `IHeroResourceAdapter`, `ISpecialResourceStorageService` interfaces. TOR accesses `Hero`, `CharacterObject`, `Settlement` directly throughout. TAOM's boundary is cleaner for v1.3.15 compatibility.

4. **Test coverage exists at all** — TAOM has 2 test files (~211 lines) covering service logic. TOR has zero unit tests for custom resources. The coverage is incomplete but the foundation exists.

5. **Thin entry points** — TAOM's Harmony patches delegate to hook interfaces which delegate to services. TOR's `CustomResourcePatches.cs` contains inline business logic. TAOM's pattern is more maintainable.

## Architecture Comparison Table (Section 11)

| Aspect | TOR Approach | TAOM Approach | Verdict |
|--------|-------------|---------------|---------|
| Persistence | `[SaveableField]` on `HeroExtendedInfo`, auto-serialized | `SyncData` with `Dictionary<string, float>` | **TOR better** — engine-native serialization, no migration code needed |
| Event coverage | 7 events (battle, hideout, prisoner, tournament, level-up, issue, mission start) | 3 events (battle, daily tick, raid) | **TOR better** — 4 missing income sources |
| Upgrade flow | Pending transaction with Done/Cancel, `AddCommand` prefix clamp | Immediate deduct in postfix, no cancel handling | **TOR better** — transactional correctness |
| UI depth | Tooltip resource costs, party header balance, Waaagh meter | Map bar mixin (basic), no upgrade tooltip integration | **TOR better** — more complete player feedback |
| Extensibility | New resource = new C# helper class + hardcoded registration | New resource = XML entry | **TAOM better** — data-driven, no recompile |
| Test coverage | Zero unit tests | 2 test files, ~211 lines | **TAOM better** — testable architecture |
| Save compat | `[SaveableField]` handles missing data gracefully via engine defaults | Raw dict, no legacy migration, no cap-on-load | **TOR better** — engine handles forward compat |
| Config pattern | Hardcoded constants scattered across helper classes | Centralized XML with `IConfigProvider` | **TAOM better** — single source of truth |

## Recommended Next Steps

1. **Fix upgrade transaction pattern** (CRITICAL) — implement Done/Cancel-aware pending spend
2. **Add pre-execution clamp** (CRITICAL) — guard `UpgradeTroop` command path against budget overrun
3. **Gate raid income on player participation** (HIGH)
4. **Add legacy save migration + cap-on-load** (HIGH)
5. **Expand event coverage** to match TOR's 7 events (tournaments, issues, level-ups, hideouts)
6. **Add upgrade tooltip resource costs** to match TOR's UI depth
7. **Add the missing test scenarios** for all 6 findings above
