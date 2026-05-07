# Codex Adversarial Review: SpecialResources vs TOR_Core CustomResources (v2)

**Date:** 2026-04-07
**Target:** branch diff against master
**Verdict:** needs-attention

No-ship. TAOM is cleaner and more data-driven than TOR, but the current port still miskeys Mordor against Bannerlord kingdom IDs, grants raid income from unrelated world events, and only enforces upgrade affordability in the UI after vanilla has already mutated the roster.

**Note:** This is v2 of the review. Codex could not clone TOR_Core (network restricted) but grounded analysis in local TAOM sources, `docs/research/tor-resource-system.md`, and decompiled Bannerlord v1.3.15 from `E:\Decompiled_Bannerlord\`.

## Known Suspects Verdict (Section 9)

1. **PENDING TRANSACTION GAP:** CONFIRMED — `PartyScreenLogic_UpgradeTroop_Patch.cs:15-29` deducts in postfix immediately. TOR uses `_resourceChanges[]` pending transaction with Done/Cancel handling. TAOM loses resources on Cancel.

2. **UPGRADE COST TARGET MISMATCH:** CONFIRMED CONSISTENT — Both TAOM patches use upgrade TARGET troop ID, and `troop_resource_costs.xml` keys by TARGET. However, the `kingdom_id="mordor"` mismatch (Finding 1) means the whole system no-ops for Mordor regardless.

3. **GAMEMODEL DEAD CODE:** CONFIRMED — `TaomSpecialResourceModel` exposes `GetCurrentResource()` and `CanAffordUpgrade()` but grep shows no callers outside SubModule GameModel registration. These methods are dead code.

4. **ORPHANED ADAPTERS:** CONFIRMED — `IHeroResourceAdapter`, `HeroResourceAdapter`, `ISettlementProductionAdapter`, `SettlementProductionAdapter` exist in `Main/Adapters/` but are not referenced by any service, behavior, or IoC registration.

5. **SPRITEWIDGET BRUSH CONFLICT:** CONFIRMED — `SpecialResourceSpriteWidget.cs:20-45` custom sprite branch is bypassed because `VisualId` override changes `IconID` away from the expected sentinel value. See Finding 5.

6. **NO CAP ON LOAD:** CONFIRMED — `SpecialResourceStorageService.cs:26-30` `RestoreData` accepts raw dictionary with no cap enforcement. TOR clamps in `HeroExtendedInfo.AddCustomResource` mutator. See Finding 4.

## Findings

### [CRITICAL] Configured resource never resolves for Mordor — kingdom ID vs culture ID mismatch

**File:** `special_resources_config.xml:3`

**TAOM code:** Runtime lookup uses `hero.Clan?.Kingdom?.StringId` which returns `"empire_s"` for Mordor.
**Config:** `kingdom_id="mordor"` — a culture ID, not a kingdom ID.

**Impact:** New-game init, daily income, battle/raid/siege rewards, UI display, and upgrade gating ALL miss for Mordor. The entire feature silently no-ops for the only configured kingdom. Tests encode the same wrong assumption.

**Fix:** Replace `kingdom_id="mordor"` with `kingdom_id="empire_s"` in `special_resources_config.xml:3`. Update test assertions to use `"empire_s"`. Add integration test asserting Mordor resolves through `Hero.MainHero.Clan.Kingdom.StringId`.

### [HIGH] Raid rewards granted for any attacker-side raid in the world, not just player participation

**File:** `SpecialResourcesBehavior.cs:108-116`

**TAOM code:** Handler checks `side == BattleSideEnum.Attacker` and player's kingdom. Never checks `component.IsPlayerMapEvent` or player party involvement.

**Vanilla evidence:** `ViewDataTrackerCampaignBehavior` guards on `raidEvent.IsPlayerMapEvent`. `CampaignEvents.RaidCompletedEvent` is global — fires for every raid in the world.

**Impact:** NPC raids silently feed the player's resource balance.

**Fix:** Add `component.IsPlayerMapEvent` check at `SpecialResourcesBehavior.cs:110` before awarding. Add test for NPC-only raid asserting no player reward.

### [HIGH] Upgrade affordability not enforced before vanilla mutates the roster

**File:** `PartyScreenLogic_UpgradeTroop_Patch.cs:15-29`

**TAOM code:** Deducts in postfix after vanilla `UpgradeTroop` executes. Never blocks or clamps the command before execution.

**Vanilla evidence:** `PartyScreenLogic.ValidateCommand` (decompiled) checks gold, XP, items, troop counts only — no custom resource concept. Stale or injected commands execute freely.

**TOR reference:** Uses `AddCommand` prefix + precomputed mass budget to clamp before execution.

**Impact:** Overspend degrades into free upgrades (storage clamps to 0 instead of rejecting). Example: 3 scraps, cost 2, 2 upgrades → both execute, 1 is free.

**Fix:** Add prefix on `PartyScreenLogic.AddCommand` or `UpgradeTroop` to reject/clamp command before roster mutation. Consider pending-transaction pattern for Cancel support.

### [HIGH] Save restore trusts raw values — no cap enforcement on load

**File:** `SpecialResourceStorageService.cs:26-30`

**TAOM code:** `RestoreData` accepts any values from save data. `Get` returns unchanged. Only `AddCapped` enforces limits.

**TOR reference:** `HeroExtendedInfo.AddCustomResource` clamps `[0, MaximumCustomResourceValue(5000)]` on every write.

**Impact:** Save-edited or legacy values above cap persist indefinitely. Negative/NaN values possible.

**Fix:** In `RestoreData` at `SpecialResourceStorageService.cs:26`, iterate restored dictionary and clamp each value to `[0, configuredCap]`. Reject NaN/Infinity.

### [MEDIUM] Map-bar icon resolution is internally inconsistent — custom sprite path never runs

**File:** `SpecialResourceSpriteWidget.cs:20-45`

**TAOM code:** Map-bar mixin creates `MapInfoItemVM("special_resource", ...)` then overrides `VisualId` with sprite name. Widget exits unless `IconID == "special_resource"`. After override, custom lookup branch is skipped. `IconBrushWidget` falls back to brush-layer lookup.

**Impact:** Resource icon is missing or blank on the campaign map bar.

**Fix:** Key custom branch off a stable property other than `IconID`, or pass sprite name through a separate bound property instead of overriding `VisualId`.

### [MEDIUM] Single-bucket storage — balances bleed across resources and kingdom switches

**File:** `TroopResourceCostEntry.cs:5-15`, `SpecialResourceStorageService.cs`

**TAOM code:** `TroopResourceCostEntry.ResourceId` is loaded from XML but never consulted. Storage is `Dictionary<string,float>` keyed by hero ID only. No resource ID dimension.

**TOR reference:** `HeroExtendedInfo.CustomResources` is `Dictionary<string,float>` keyed by resource StringId, supporting multiple resources per hero.

**Impact:** Cannot distinguish Scraps from future Gondor/Rohan resources. Player's balance follows current kingdom display context. Kingdom switch doesn't reset or separate balances.

**Fix:** Store balances per hero per resource ID. Require spend/upkeep paths to resolve specific `resource_id` from troop cost entry. Add test simulating two resources.

## What TAOM Does Better (Section 11)

1. **IoC vs static singletons** — TAOM uses DryIoc with interface contracts (`ISpecialResourceService`, `ISpecialResourceStorageService`). TOR uses `CustomResourceManager.Instance`. TAOM is testable and mockable; TOR requires static initialization order management.

2. **XML-driven config** — TAOM's `special_resources_config.xml` + `troop_resource_costs.xml` define everything declaratively. TOR hardcodes 10 resources in `CustomResourceManager.Initialize()` plus per-resource C# helper classes. Adding a TAOM resource = XML edit; adding a TOR resource = new C# class + registration.

3. **Adapter boundary** — TAOM wraps TaleWorlds types behind adapter interfaces. TOR accesses `Hero`, `CharacterObject`, `Settlement` directly throughout `CustomResourceManager`, `TeefBehavior`, etc. TAOM's boundary is cleaner for version migration.

4. **Test foundation** — TAOM has 2 test files (~24 test methods) covering service logic with NSubstitute mocks. TOR has zero unit tests for custom resources. TAOM's coverage is incomplete but the testable architecture exists.

5. **Thin entry points** — TAOM's Harmony patches delegate to `IOnPartyUpgradeResourceCheck` hook → service. TOR's `CustomResourcePatches.cs` contains inline business logic with direct `Hero.MainHero` access. TAOM's layering is more maintainable.

6. **Separation of concerns** — TAOM splits into Domain, Services, Hooks, UI, Config. TOR combines persistence, earning, spending, and UI concerns in `CustomResourceManager` (~800 lines). TAOM's decomposition follows Single Responsibility.

## Architecture Comparison (Section 12)

| Aspect | TOR | TAOM | Verdict |
|--------|-----|------|---------|
| Persistence | `[SaveableField]` engine-native | `SyncData` dictionary | **TOR** — auto-migration on field add/remove |
| Event coverage | 7 events | 3 events | **TOR** — 4 missing income sources |
| Upgrade flow | Pending transaction + AddCommand clamp | Postfix deduct, advisory UI only | **TOR** — transactional correctness |
| UI depth | Tooltip costs, party header, Waaagh meter | Map bar mixin (broken icon) | **TOR** — complete player feedback |
| Extensibility | New C# class per resource | XML entry | **TAOM** — data-driven, no recompile |
| Test coverage | Zero | 24+ test methods | **TAOM** — testable architecture |
| Save compat | Engine handles field versioning | Raw dict, no migration or cap | **TOR** — graceful forward compat |
| Config pattern | Hardcoded constants | Centralized XML + IConfigProvider | **TAOM** — single source of truth |
| v1.3.15 compat | Built for older BL versions | Built for v1.3.15 | **TAOM** — verified against decompiled 1.3.15 |
