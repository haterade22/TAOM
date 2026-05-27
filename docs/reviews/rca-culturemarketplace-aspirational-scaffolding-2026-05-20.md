# RCA — CultureMarketplace (Deep-Review Phase 1, 2026-05-20)

## Top-line

`/deep-review` on the CultureMarketplace feature returned 0 findings on Standards, API Compatibility, Performance, and Completeness — but the Data Flow agent (Agent 5) found **3 LOW dead-code findings**, all in the same category: declared-but-never-used scaffolding. All 3 fixed (deletion) in the same session. Build clean (0 errors), 2254/2254 tests still pass.

**Important:** this is a **repeat-offender pattern**. The exact same rule fired on EquipPresets review #5 (2026-05-06, 14 days ago) and was codified as memory entry `feedback_no_aspirational_enum_values`. The rule existed at session start; I did not apply it during design.

## Findings + Root Cause Table

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|-----|-----|----------|-----------|-------------------|
| F1 | LOW | `MarketplaceTuning.CountPerInjection` field declared and initialized to `1` in `MarketplaceTuning.Default`, but never read. `CultureMarketplaceBehavior.cs:85` hardcodes `1` as the count passed to `_townAdapter.AddItem(...)` instead of consulting `_tuning.CountPerInjection`. | **Aspirational scaffolding** | Defended at design time as "knob for future tuning if we want bulk injection per slot." No producer in this PR. Exactly the pattern `feedback_no_aspirational_enum_values.md` forbids. | **Apply the existing rule.** Deleted the field. If a future session wants bulk-injection per slot, add the field AND the consumer in the same PR. |
| F2 | LOW | `ITownRosterAdapter.EnumerateTowns()` method declared and implemented (allocating a 256-capacity `List<Settlement>`, iterating `Campaign.Current?.Settlements`, filtering `IsTown`). Zero call sites within the feature. The behavior receives one settlement at a time via `CampaignEvents.DailyTickSettlementEvent` — `EnumerateTowns` is structurally redundant. | **Aspirational scaffolding** | Defended at design time as "could be useful if we ever do batch-mode injection." Same rule. | Deleted from both the interface and the implementation. If a batch-mode scenario emerges, restore at that time alongside the caller. |
| F3 | LOW | `Domain/TownInjectionContext.cs` declared a sealed POCO with three fields (`SettlementId`, `CultureId`, `CurrentRosterCount`). Zero instantiations. `ICultureMarketplaceInjectionService.SelectItems(string cultureId, int currentRosterCount, Random rng)` takes the fields as flat parameters; `Behavior.OnDailyTickSettlement` reads them in line and passes them flat. The context object is structurally dead. | **Aspirational scaffolding** | Created during initial domain modeling under the assumption "every cross-layer call should pass a context object." Then the actual signature ended up flat (3 params), and the context object was never removed. | Deleted the file. The flat 3-param signature is the right shape for this scope; if a context object becomes necessary, introduce it when the fourth parameter lands. |

## Root Cause Pattern: "Aspirational scaffolding (second occurrence in 14 days)"

All 3 findings share one shape: a name was introduced *because the design pattern wanted it* (knob for future use, batch-method symmetry, context-object convention), but the corresponding code path that produces or consumes it was never written. Phase 1 deep-review's Data Flow agent caught all 3 by enumerating "every declared field/method/type → who reads it?" and finding zero call sites.

This is a **repeat offender pattern in this codebase.** The same shape:
- EquipPresets review #5 (2026-05-06): `SlotApplyOutcome.SlotLocked`, `PresetLoadResult.SkippedLockedSlots` — both declared, both never produced. Both removed.
- Now CultureMarketplace (2026-05-20): three new aspirational artifacts. All removed.

The rule has existed for 14 days. The session author (me) did not consult `feedback_no_aspirational_enum_values.md` during design and so reintroduced the pattern.

## Why Each Deep-Review Agent Missed These (or Caught Them)

- **Agent 1 (Standards):** Not its scope. Dead code is not a standards violation — ADR-002/003/004/005/007 say nothing about unused declarations.
- **Agent 2 (Compatibility):** Not its scope. All declared APIs are syntactically valid; the issue is semantic (no consumer).
- **Agent 3 (Efficiency):** Not its scope. Dead code has zero per-tick cost; performance review is correctly indifferent.
- **Agent 4 (Completeness):** Not its scope. Completeness asks "did you ship the tests / docs / IoC wiring?" — not "are all declared fields read?"
- **Agent 5 (Data Flow): CAUGHT ALL 3.** This is the canonical Agent 5 finding shape — its rule set explicitly traces "declared field → consumer" and flags zero-call-site results. The agent prompt at `.claude/skills/deep-review/SKILL.md` rule 5 ("Enum Coverage") + rule 8 ("Vanilla Interaction Safety") + general rule 1 ("XML Config → C# Consumption") generalizes to ANY declaration that lacks a consumer, which is what fired here.

The lesson is *not* "improve the deep-review agent prompts" — Agent 5 worked correctly. The lesson is **apply the design-time rule earlier** so the post-implementation review doesn't have to catch it.

## Feedback Memories to Codify

**No new feedback memory.** The pattern is fully covered by the existing `feedback_no_aspirational_enum_values.md`. What's needed instead is the discipline to consult it during design.

Optional follow-up (not required for this RCA): a `think-before-coding.md` companion rule that requires enumerating "for every declared type/field/method in this PR, name its producer + consumer" before the first Edit. This would catch the pattern at design time rather than at review time. **Deferred** — `think-before-coding.md` already covers the broader case of surfacing load-bearing assumptions; the right move is for the session author to invoke its discipline, not to add yet another rule on top.

## Patch History

| Pre-fix | Post-fix |
|---------|----------|
| `MarketplaceTuning(itemsPerTownPerDay, perTownInjectedCap, countPerInjection)` | `MarketplaceTuning(itemsPerTownPerDay, perTownInjectedCap)` — third arg deleted |
| `ITownRosterAdapter.EnumerateTowns()` declared + implemented | Both interface method and impl deleted |
| `Domain/TownInjectionContext.cs` exists | File deleted |
| Feature doc lists `CountPerInjection` + `TownInjectionContext` | Doc updated to omit both |
| Test ctor `new MarketplaceTuning(..., countPerInjection: 1)` | Test ctor 2-arg `new MarketplaceTuning(...)` |

## Tests Affected

- `CultureMarketplaceInjectionServiceTests.cs:23` and `:111` — updated to 2-arg `MarketplaceTuning` ctor. No assertion changes. All 10 InjectionService tests still pass.
- Full suite: 2254/2254 passing post-fix.

## Verdict

Phase 1 (deep-review) RCA complete. Findings were design-time, not architecture-time. Proceeding to Phase 2 (Codex adversarial review).

---

## Addendum — Phase 2 (Codex adversarial review)

Codex review at [`docs/reviews/codex-adversarial-culturemarketplace-2026-05-20.md`](codex-adversarial-culturemarketplace-2026-05-20.md): **0 CRITICAL, 0 HIGH, 2 MEDIUM, 2 LOW**. All 4 confirmed and fixed in the same session. Disputed: S4 (culture type asymmetry), S5 (weighted-draw bias), S6 (save-load), S7 (threading), S8 (quest reservations).

### Findings + Root Cause Table (Phase 2)

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|-----|-----|----------|-----------|-------------------|
| C1 | MED | `PerTownInjectedCap` enforced against the WHOLE `Settlement.ItemRoster.Count`, not just our injected items. Vanilla `DistributeInitialItemsToTowns` runs 25 village-production passes; towns often start at 30-80 distinct entries. A town at 50+ distinct vanilla items had at most 10 CultureMarketplace draws before the cap blocked further injection forever. | **Semantic mismatch (name vs implementation)** | Design assumed "Settlement.ItemRoster will be empty or near-empty at OnNewGameCreated" and named the cap accordingly. Did not decompile `VillageGoodProductionCampaignBehavior` to confirm the assumption before naming the field. Agent 5 (Data Flow) had no rule for "trace the runtime value of `roster.Count` against the cap" — it only traced declarations to consumers. | Renamed `PerTownInjectedCap` → `PerTownTotalRosterCap`. Raised default 60 → 200 (gives 120-170 headroom even with full vanilla seeding). Field name now matches semantic; cap retains its bound-unbounded-growth purpose. **New rule (informal):** when a numeric cap is named after a thing the system controls (injected count, cached entries, etc.), verify by decompiling vanilla that nothing else writes into the same observed counter. |
| C2 | MED | Installed `LOTRAOM_horses.xml:231-330` declares Rohan harnesses with `culture="Culture.rohan"` — but `rohan` is NOT a valid TAOM culture ID (Rohan uses `vlandia`). Attribute-wins logic at `CultureItemPoolService.cs:48` routed those items into a `rohan` pool no town ever queries — Rohan markets never saw their own horse harnesses. | **Upstream data error masquerading as feature config** | Assumed every LOTRLOME `culture="..."` attribute would resolve to a valid game culture. Did not cross-reference against the TAOM ID CHEATSHEET at the design stage. The `feedback_battania_khand.md` memory documents the inverse case (battania=Khand, NOT Dunland) but doesn't generalize to "all LOTRLOME XML may use the wrong culture alias." | Added a `CultureAliases` dict in `CultureItemPoolService` mapping `rohan` → `vlandia`. Normalization runs after attribute lookup but before grouping. Two regression tests (Rohan-attribute → vlandia pool, case-insensitive). **Existing rule applied:** the `feedback_battania_khand.md` cheatsheet is now mechanically applied as a normalization map; future invalid aliases get one new dictionary entry. |
| C3 | LOW | `mirkwood_sword_a01`, `mirkwood_spear_a01/a02`, `mirkwood_glaive_a01`, `wm_harad_glaive_a01` — five real LOTRLOME crafted weapons (all `is_merchandise="true"`) had NO culture attribute AND no PrefixMap row. They were dropped as "unresolved" and never injected into any market. | **Prefix table coverage gap** | Designed the PrefixMap from a sampling agent's inventory scan but did not enumerate the no-attribute subset of LOTRLOME items. The original LOTRLOME inventory phase reported "78.6% of weapons have culture attribute" but did not list which 21.4% didn't. Did not grep `LOTRAOM_weapons.xml` for items missing the `culture=` attribute. | Added `("mirkwood_", "mirkwood")` and `("wm_harad_", "aserai")` rows. New `ItemPoolAdapterPrefixTests` class (7 tests, including the new rows + regression on existing prefixes via reflection on `ResolveByPrefix`). **Future:** if more no-attribute items surface, add prefix rows incrementally; the test class structure makes new entries trivial to verify. |
| C4 | LOW | `EnsurePoolBuilt` catches all exceptions and leaves `_poolBuilt=false`. `OnDailyTickSettlement` retries on every town tick (200+ towns × daily) FOREVER, log-spamming. | **Missing failure latch** | Followed the SpecialResources pattern (`EnsureLoaded` no-throw idempotent load), but SpecialResources is called once per hero from a service, not 200 times per game day from a tick handler. The retry-forever pattern is correct for a once-per-resolve call but pathological for a per-tick call. | Added `_failedAttempts` counter and `_gaveUp` latch. After 3 failed `BuildPools` attempts the feature flips inert for the rest of the session with one final error log. Tick handler now short-circuits on `_gaveUp` before doing any other work. |

### Root Cause Pattern (Phase 2): "Cross-system assumption blindness"

All 4 Phase 2 findings share one shape: a Phase-1 design assumption about an external system was not verified.

| Finding | Assumed | Actual |
|---------|---------|--------|
| C1 | Vanilla town roster starts near zero | 25-pass village-production seeding fills it to 30-80 |
| C2 | LOTRLOME `culture=` attributes map to valid TAOM IDs | At least 5 horse harnesses use invalid `Culture.rohan` |
| C3 | LOTRLOME item ID prefixes follow a single per-culture convention | Mirkwood and Harad have a no-prefix-attribute combo |
| C4 | Retry-on-failure was safe (SpecialResources precedent) | Per-tick retry-forever amplified ~1 failure into ~10k log lines per game day |

The pattern: deep-review's agents traced *intra-system* logic but did not validate *cross-system* assumptions about vanilla behavior, LOTRLOME data quality, or per-tick amplification. Codex caught all four because it explicitly decompiled vanilla (S1, S7) and grepped installed LOTRLOME XML (S3 prefix audit, C2 alias).

### Feedback Memories to Codify

**No new feedback memory.** Each of C1-C4 is well-covered by existing rules that the session author didn't apply:

- C1: the `csharp-architecture.md` "Config Providers MUST Validate" rule applies to MarketplaceTuning by analogy — the cap *is* config, even if hard-coded. Validation should include "verify the controlled-counter semantic matches the field name." Not codified separately; case-by-case design discipline.
- C2: `feedback_battania_khand.md` already documents the broader pattern. The new `CultureAliases` map is the mechanized form of that cheatsheet.
- C3: no specific rule; addressed by adding the prefix rows + tests.
- C4: no specific rule; addressed by the latch.

### Why Each Deep-Review Agent Missed Phase 2 Findings

- **Agent 1 (Standards):** All 4 findings are semantic/data, not architectural. Out of scope.
- **Agent 2 (Compatibility):** Verified all method signatures. Did not enumerate vanilla side-effects on `Settlement.ItemRoster` (would have caught C1) or grep installed LOTRLOME XML for invalid culture references (would have caught C2/C3).
- **Agent 3 (Efficiency):** C4 retry-forever IS a performance concern (log spam + repeated MBObjectManager scans), but Agent 3 saw the adapter `_cache` guard and concluded "one-time cost." It did not consider the FAILED-build path where `_poolBuilt=false` means no cache hit.
- **Agent 4 (Completeness):** Out of scope (focuses on tests/docs/IoC).
- **Agent 5 (Data Flow):** Caught the Phase-1 LOW findings. Did not catch C1-C4 because its rule set traces declarations-to-consumers, not vanilla-runtime-assumptions or upstream-data-quality.

**The lesson:** deep-review needs an Agent 6 for "vanilla-runtime-assumption verification" — pick the 3-5 most load-bearing assumptions the feature makes about vanilla behavior, decompile to confirm. This is what Codex does well and what Claude agents are not currently structured to do.

### Patch History (Phase 2)

| Pre-fix | Post-fix |
|---------|----------|
| `MarketplaceTuning(itemsPerTownPerDay, perTownInjectedCap)`, default 60 | `MarketplaceTuning(itemsPerTownPerDay, perTownTotalRosterCap)`, default 200 |
| `CultureItemPoolService.BuildPools` had no alias normalization | `CultureAliases` dict applied between attribute lookup and grouping |
| PrefixMap had 28 rows (no `mirkwood_`, no `wm_harad_`) | PrefixMap has 30 rows; LOTRLOME no-attribute Mirkwood/Harad weapons now route correctly |
| `EnsurePoolBuilt` retried on every daily tick on failure | 3-attempt failure latch; `_gaveUp` flag short-circuits the tick handler |

### Tests Added (Phase 2 regression coverage)

- `BuildPools_RohanCultureAttribute_NormalizesToVlandia` — confirms C2 fix
- `BuildPools_RohanAlias_IsCaseInsensitive` — confirms StringComparer.OrdinalIgnoreCase
- `ItemPoolAdapterPrefixTests` (new class, 7 tests):
  - `ResolveByPrefix_MirkwoodCraftedWeapon_ResolvesToMirkwood` — confirms C3 mirkwood_ row
  - `ResolveByPrefix_HaradCraftedWeapon_ResolvesToAserai` — confirms C3 wm_harad_ row
  - `ResolveByPrefix_GondorIdPrefixes_ResolveToGondor` — regression on existing prefixes
  - `ResolveByPrefix_MordorIdPrefixes_ResolveToMordor` — same
  - `ResolveByPrefix_RohanAndDunlandPrefixes_ResolveToVanillaCultureIds` — same
  - `ResolveByPrefix_NullOrEmptyId_ReturnsNull` — defensive
  - `ResolveByPrefix_UnknownPrefix_ReturnsNull` — defensive

Test suite after Phase 2 fixes: **2263 passed / 0 failed / 2 skipped** (+9 from Phase 1's 2254).

---

## Addendum — Phase 2.5 (in-game user findings, 2026-05-20)

After the Codex fixes landed, the user opened a fresh game and reported two additional observations:

### C5 — Wargs only show in Isengard markets

The Alliance.Wargs module ships [`lotr_warg.xml`](file:///E:/Steam/steamapps/common/Mount%20%26%20Blade%20II%20Bannerlord/Modules/Alliance.Wargs/ModuleData/Items/LOTR/lotr_warg.xml) with all four warg-related items (`warg_brown`, `warg_dark`, `warg_albino`, `warg_saddle`) tagged `culture="Culture.isengard"`. Lore-correctly wargs should also appear in Mordor, Gundabad, and Dol Guldur markets — but my single-culture-attribute model could only route them to Isengard.

**Why missed in earlier reviews:** the design discussion at plan time treated each item as having one canonical culture (consistent with the rest of LOTRLOME's per-culture folders). Wargs are the first item set we encountered that legitimately belong to multiple cultures. Neither deep-review nor Codex had any signal to flag this — it's only visible through in-game lore comparison.

**Fix:** added a top-level `<Routing>` XML section to `culture_marketplace_config.xml`:

```xml
<Routing>
  <Item id="warg_brown"  cultures="isengard,mordor,gundabad,dolguldur" />
  <Item id="warg_dark"   cultures="isengard,mordor,gundabad,dolguldur" />
  <Item id="warg_albino" cultures="isengard,mordor,gundabad,dolguldur" />
  <Item id="warg_saddle" cultures="isengard,mordor,gundabad,dolguldur" />
</Routing>
```

Listed items IGNORE their `culture=` attribute and ID-prefix fallback and appear ONLY in the listed cultures' pools. The mechanism is generic — future cross-culture items (universal merchant gear, mercenary equipment, etc.) need only one config entry.

Implementation: `ICultureMarketplaceConfigProvider.GetItemRouting()` returns a `Dictionary<string, IReadOnlyList<string>>` (item-id → culture-ids). `CultureItemPoolService.BuildPools` checks routing first; if present, the item is added to every listed culture's pool, honoring per-culture blacklists; if absent, the existing attribute → prefix-fallback chain runs.

Tests: 6 new `BuildPools_Routed*` cases + 6 new `GetItemRouting_*` config-parser cases = 12 new tests. All green.

### C6 — Rivendell market shows Harad/Rhun equipment (game just started, owned by Rivendell)

The user reported Harad and Rhun equipment appearing in a Rivendell-owned town's market on a fresh game. Investigation:

- Grep of LOTRLOME `Rivendell/` folder: zero items with `Culture.aserai` or `Culture.khuzait`. All Rivendell folder items correctly tagged `Culture.rivendell`. No data error there.
- PrefixMap audit: `("rivendell_", "rivendell")` precedes `("haradrim", "aserai")` and `("easterling", "khuzait")` in iteration order, and the longer-prefix-wins isn't an issue because none of those prefixes overlap. No misrouting.
- My injection logic: only adds items from `_poolService.GetPool(cultureId)` where `cultureId` is the owner clan's culture. For a Rivendell-owned town this is `rivendell`, and the rivendell pool contains only `Culture.rivendell` items.

**Most likely cause:** the items the user observed are coming from **vanilla's `DistributeInitialItemsToTowns`**, which runs 25 village-production passes per town at `OnNewGameCreatedPartialFollowUpEvent(i=1)` and writes the village output directly into the town's `ItemRoster` — with no culture filter. Village production lists can include items that happen to be Aserai/Khuzait-cultured (e.g., when a village type produces an item the engine has classified that way). This is independent of CultureMarketplace and pre-dates the feature.

**Why not flagged earlier:** no agent or Codex pass exercised "what does the town roster actually look like in-game when the player opens the market." This kind of bug is only visible through in-game observation.

**Fix:** added boot-time per-culture pool diagnostic logging in [`CultureItemPoolService.BuildPools`](../../Main/Features/CultureMarketplace/CultureItemPoolService.cs). The next session, the user can check the TAOM log for lines like:

```
[CultureMarketplace] Pool built: 14 cultures, 6155 items (attribute=5942, prefix-fallback=209, routed=4, unresolved=0)
[CultureMarketplace]   gondor: 503 items — sample: faramir_armor, imrahil_body, ithilien_jerkin_long, sk_gd_ano_inf_helmet_med_a
[CultureMarketplace]   rivendell: 363 items — sample: rivendell_body_gold_a, rivendell_body_gold_b, ...
...
```

This makes the per-culture pool composition transparent. If the Rivendell pool contains only Rivendell items (expected), the Harad/Rhun items the user sees are vanilla-seeded and out of scope for this feature. If the Rivendell pool actually contains Harad/Rhun items, there's a real bug we'd need to chase.

**Deferred to in-game retest:** without the diagnostic logs from a real run, I cannot conclusively distinguish "vanilla seeding" from "CultureMarketplace bug." The user can verify next session. If the issue persists after the diagnostic confirms a clean Rivendell pool, the next step would be a separate "filter vanilla items by town culture" feature — which is out of scope for #207 and would need its own design pass (it's a destructive op: removing items from the engine-managed roster).

### Tests after C5/C6 changes

- 12 new tests (6 routing pool-service + 6 routing config-parser)
- Full suite: **2276 passed / 0 failed / 2 skipped** (+13 since Phase 2 closeout)

### Patch History (Phase 2.5)

| Pre-fix | Post-fix |
|---------|----------|
| Single-culture-attribute model — items belong to exactly one pool | `<Routing>` XML section in `culture_marketplace_config.xml`; items can belong to multiple culture pools |
| Wargs (Culture.isengard) appear only in Isengard markets | Wargs appear in Isengard, Mordor, Gundabad, and Dol Guldur markets |
| Boot log: single line with aggregate counts | Boot log: aggregate line + per-culture pool size + 4-item sample for each culture |

---

## Addendum — Phase 3 (Codex self-review of fixes)

Codex re-reviewed the C1-C6 fix code at [`docs/reviews/codex-adversarial-culturemarketplace-fixes-2026-05-20.md`](codex-adversarial-culturemarketplace-fixes-2026-05-20.md). Verdict: **0 CRITICAL, 0 HIGH, 0 MEDIUM, 1 LOW**. Six of seven Known Suspects DISPUTED (no bug); one CONFIRMED. Regression check on C1-C4 was clean.

### Finding (Phase 3)

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|-----|-----|----------|-----------|-------------------|
| S2 | LOW | Routed cultures were NOT deduplicated after alias normalization. Two routing entries that collide post-alias — author typo `cultures="mordor,mordor"` OR alias-collision `cultures="rohan,vlandia"` (both alias to `vlandia`) — added the item to the same canonical pool TWICE, silently doubling its weight without a warning. | **Config validation gap (missing dedup)** | Built the routing mechanism in response to user finding C5 with happy-path tests only. Didn't enumerate the "list of culture IDs has duplicates" or "aliases collide" cases. The existing `feedback_no_aspirational_enum_values.md` and `feedback_config_providers_must_validate.md` patterns cover the validate-input rule, but I treated this as data dedup, not config validation. Both `MarketplaceConfigOverride.Blacklist` (HashSet) and `WeightBoosts` (Dictionary) inherently dedup by data structure; the routing target list was a `List<string>` with no dedup. | **Fix:** dedup in `BuildPools` routing branch using `HashSet<string>(StringComparer.OrdinalIgnoreCase)` per-item, AFTER alias normalization. Logs a warning when duplicates are observed so the author sees the redundancy. Two regression tests (exact duplicate, alias collision). |

### Root cause pattern (Phase 3): "container-type tells the validation story"

`Blacklist` is a `HashSet<string>` and `WeightBoosts` is a `Dictionary<string, float>` — both data structures inherently dedup. The routing target list is a `List<string>` — order-preserving but allowing duplicates. The shape chosen at design time IS the validation contract; I picked a list, so dedup needed to be enforced separately.

**Generalizable rule:** when a config field's downstream consumer treats it as a set (each entry triggers a side effect at most once), the parsed representation should be a `HashSet<>` from the start, OR the consumer must dedup before iterating. For ordered lists (e.g., PrefixMap where iteration order is intentional), keep `List<>` but enumerate the dedup expectations explicitly.

No new feedback memory codified — this rule is downstream of the existing config-validation guidance and adding another bullet would create churn. The fix + the two regression tests close the loop.

### Why each Known Suspect was DISPUTED (Codex's reasoning, confirmed by code re-read)

- **S1 (routing + alias):** routing branch calls `ApplyCultureAlias` on each culture ID before `AddToGroup`, so the alias is honored once per routed culture. The `continue` prevents subsequent attribute/prefix processing. Code verified at `CultureItemPoolService.cs:63-78`.
- **S3 (C4 latch counter):** if `BuildPools` succeeds on attempt 2, `_failedAttempts=1` but `_poolBuilt=true` short-circuits all future `EnsurePoolBuilt` calls. The stale counter is unobservable.
- **S4 (diagnostic cost):** `BuildPools` returns early on `_pools != null` so the per-culture log loop fires once. Retry-after-failure can't repeat the diagnostic because the failed branch never reaches the loop.
- **S5 (routing invariant):** the routing branch always `continue`s after processing, so a routed item never falls through to the attribute path. `warg_brown` is added once per listed culture, not twice in Isengard.
- **S6 (C1 leftover refs):** grep verified — no production source references the old `PerTownInjectedCap` name. Only review/RCA history docs mention it.
- **S7 (case-sensitivity):** routing uses `StringComparer.Ordinal` for the item-id key, consistent with TaleWorlds' `MBObjectManager.GetObject<T>(string)` which uses ordinal exact-match. Blacklist/Boost dicts also use ordinal. This is internally consistent.

### Tests after S2 fix

- 2 new tests: `BuildPools_RoutedItem_DuplicateCultureExact_DedupsToOneEntry`, `BuildPools_RoutedItem_AliasCollision_DedupsToOneEntry`
- Full suite: **2287 passed / 0 failed / 2 skipped**

### Patch History (Phase 3)

| Pre-fix | Post-fix |
|---------|----------|
| Routing branch iterated `routedCultures` and added each to `AddToGroup` with no dedup | Routing branch builds a `HashSet<string>(OrdinalIgnoreCase)` of post-alias targets; only first occurrence is added; subsequent duplicates increment a counter; one warn log per affected item if duplicates were seen |

---

## Addendum — Phase 4 (in-game findings post-commit `3167d08`, 2026-05-21)

After the `3167d08` ship, the user opened Orthanc (Isengard's capital) and reported:

1. **No wargs visible.** 2× `[Isengard] Warg Saddle` from random luck, 0 actual Warg mounts. K=6 random draws from a ~200-item Isengard pool give each warg ~3% per-day chance — insufficient for "always available."
2. **Foreign-culture LOTRLOME items appearing.** `[Gondor] Light Horse Armour — Pinnath Gelin` and `[Rohan] Horse Armour I` showing in Orthanc on day 1. Investigation confirmed the Rivendell folder was clean, my PrefixMap routes correctly, and `GetPool("isengard")` only contains isengard-tagged items. The leak came from vanilla's `VillageGoodProductionCampaignBehavior.DistributeInitialItemsToTowns` (25 production passes at `OnNewGameCreatedPartialFollowUpEvent(i=1)`, no culture filter).

### Findings + Root Cause Table (Phase 4)

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|-----|-----|----------|-----------|-------------------|
| F1 | HIGH (in-game) | Wargs unreliable — probabilistic K=6 draw from 200-item pool gives ~3%/day per warg variant, so the user-promised "wargs in evil-culture markets" was a coin flip. | **Probabilistic vs deterministic contract gap** | Original design treated routing as "this item is in this culture's pool" without a guaranteed-floor concept. Codex reviews #1 + #2 also missed it because the routing mechanism's user-facing contract was "they appear sometimes," which matches the implementation. No in-game testing of the actual frequency happened. | New `min_stock` attribute on `<Routing><Item>`. Listed items are kept above a floor by daily top-up; cap-bypassing. Documented in the feature doc as "Guaranteed Stock" with the explicit promise: ≥1 of each variant always available. 14 new tests across `GetRoutedItemsForCultureTests` (6) + `CultureMarketplaceBehaviorGuaranteedStockTests` (8). |
| F2 | HIGH (in-game) | Vanilla `DistributeInitialItemsToTowns` seeds foreign-culture LOTRLOME items into every town's roster at game start; my feature only ADDS, never FILTERS. Orthanc shows `[Gondor]` + `[Rohan]` items on day 1 because vanilla's 25-village-production passes put them there. | **Vanilla-flow blindspot** | The original design (#207) explicitly chose "additive only — don't filter vanilla" because we didn't want to disturb vanilla's behavior or risk removing player-sold items. But that left the cross-cultural seeding visible to the player. Codex review #1 disputed S8 ("no quest item reservations") but didn't surface that vanilla's village-production seeding distributes culture-tagged items with no filter — a different vanilla flow. | New `FilterForeignCultureItems` pass on every daily tick (capped at `MaxFilterRemovalsPerTick=6` to avoid surprise) PLUS an uncapped one-shot sweep on `OnNewGameCreatedPartialFollowUpEvent(i=2)` to clean the initial seed in one pass. Filter preserves routed items, vanilla universals (no culture attribute), and same-culture items. Net result: Orthanc no longer shows Gondor/Rohan items on day 1; vanilla universals (food, trade goods, base armour) remain untouched. 9 new filter tests via `CultureMarketplaceBehaviorFilterTests`. |
| F3 | NEW (mechanism) | Filter and pool-builder previously had their classification logic in different code paths; if the pool-builder's "attribute → prefix → alias" chain diverged from the filter's, the filter could remove items the pool was injecting. | **Logic drift risk** | The original `BuildPools` had the classification inline. Adding the filter would have duplicated the code. | Extracted `ICultureItemPoolService.ClassifyEffectiveCulture(attributeCultureId, prefixCultureId)` as a pure helper used by both `BuildPools` and the new filter pass. 6 new tests in `CultureItemPoolServiceClassifierTests` lock the contract. Refactor is behavior-preserving for the pool-builder (regression-tested via the existing `CultureItemPoolServiceTests` which still pass). |
| F4 | NEW (adapter) | The filter needs to inspect roster items (which culture, how many) and remove specific items — but the prior `ITownRosterAdapter` only exposed `AddItem` and a roster-size count. | **Adapter incompleteness** | The original adapter was designed for the inject-only flow. The filter needs read + delete operations. | Extended `ITownRosterAdapter` with `GetItemCount`, `RemoveItem` (via `AddToCounts(EquipmentElement, -N)` — confirmed in vanilla source as accepting negative counts and triggering `OnInventoryUpdated` → `TownMarketData` price recalc), and `EnumerateRoster` (returns `RosterItemSnapshot` DTOs to keep `ItemObject` out of services per ADR-007). Adapter is thin — tested via service tests that mock the adapter. |

### Root cause pattern (Phase 4): "user-promise gap between probabilistic + additive design and deterministic + visible expectations"

Both F1 and F2 are the same shape: the original design (#207) made implicit choices that the user-facing promise didn't reflect:

- F1: design said "items appear via weighted-random draws" → user heard "wargs are in the marketplace." Random meant unreliable; user thought "always at least one."
- F2: design said "we add items; vanilla decides what to seed" → user heard "the marketplace shows culture-appropriate items." Additive meant vanilla noise still leaks through; user thought "Orthanc shows Isengard items only."

**Generalizable rule (informal):** for any user-facing feature contract that uses verbs like "always," "every," "no [foreign category]," ensure the implementation is **deterministic** (guaranteed floor / explicit filter), not just **probabilistic** or **additive**. Audit feature docs for these verbs before shipping.

### Why each prior review missed Phase 4 findings

- **Phase 1 deep-review:** Caught dead code (3 LOW), not user-promise gaps.
- **Phase 2 Codex adversarial:** Found 4 real bugs (cap semantics, alias map, prefix gaps, retry latch) — all about implementation correctness. Didn't surface the "additive-only" or "probabilistic" gap because those WERE the design.
- **Phase 3 Codex self-review:** Found 1 routing dedup bug. Operating on the same design assumptions.

The lesson: **adversarial review catches implementation bugs, not user-promise gaps.** Those need in-game testing or an explicit "user-promise audit" review pass. Adding a future pre-ship check: read the feature doc's user-facing promises out loud against the code; if "always" / "every" / "no" appear, verify the implementation is deterministic.

### Patch History (Phase 4)

| Pre-fix | Post-fix |
|---------|----------|
| Routing dict was `Dictionary<string, IReadOnlyList<string>>` (item → cultures) | `Dictionary<string, RoutedItem>` (item → cultures + min_stock); old test suite updated mechanically |
| `MarketplaceTuning(itemsPerTownPerDay, perTownTotalRosterCap)` | `MarketplaceTuning(itemsPerTownPerDay, perTownTotalRosterCap, maxFilterRemovalsPerTick)` |
| `ITownRosterAdapter` had `AddItem` + `GetRosterDistinctItemCount` | Added `GetItemCount(settlement, itemId)`, `RemoveItem(settlement, itemId, count)`, `EnumerateRoster(settlement) -> IReadOnlyList<RosterItemSnapshot>` |
| `CultureItemPoolService.BuildPools` had inline classification | Extracted as `ClassifyEffectiveCulture(attributeCultureId, prefixCultureId)` — public on the interface; new `GetRoutedItemsForCulture(cultureId)` for the behavior's two new passes |
| `CultureMarketplaceBehavior.OnDailyTickSettlement` ran weighted-random injection only | Now runs three passes in order: guaranteed-stock (cap-bypass) → filter (capped) → weighted-random (capped). Plus uncapped one-shot filter sweep on `OnNewGameCreatedPartialFollowUpEvent(i≥2)`. |
| `culture_marketplace_config.xml` had `<Routing>` items without `min_stock` | 4 wargs gained `min_stock="1"` |
| `Main/SubModule.cs` registered the behavior with 4 ctor args | Now passes `MarketplaceTuning` as the 4th arg (5 total) |

### Tests after Phase 4 changes

- 34 new tests across 4 new classes + extended ConfigProvider tests (5 new `min_stock` cases)
- Full suite: **2321 passed / 0 failed / 2 skipped** (was 2287 after Phase 3)
- CultureMarketplace + adapter scope: **87/87** (was 53 after Phase 3)

---

## Addendum — Phase 4b (deep-review of Phase-4 fixes, 2026-05-21)

`/deep-review` on the Phase-4 changes returned 2 HIGH, 1 MEDIUM, 1 LOW. All 4 fixed in the same session.

### Findings + Root Cause Table (Phase 4b)

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|-----|-----|----------|-----------|-------------------|
| D1 | HIGH | `OnNewGameCreatedPartialFollowUpEvent` guard `if (i < 2) return` ran the uncapped initial filter sweep 98 times per new game (event fires for i ∈ [0, 99]). Functionally correct (subsequent sweeps no-op) but wasteful and produced 97 redundant log lines. | **Event-iteration count not enumerated** | Designed the hook assuming the event fires only for i values associated with documented vanilla phases (0=clear, 1=DistributeInitialItemsToTowns, 2=our cleanup). Didn't decompile to confirm the upper bound of i. The `feedback_observation_state_matrix.md` rule covers polled state machines but didn't fire here because this was an event with a discrete index parameter, not polling. | Added `_initialSweepDone` boolean; guard now `if (_initialSweepDone) return; if (i < 2) return;`. The flag is set after a successful sweep AND on exception (one-shot is the contract). Documented the i ∈ [0, 99] range inline. |
| D2 | HIGH | `CultureMarketplaceBehavior` grew to 194 lines with `EnsureGuaranteedStock` (17 lines) + `FilterForeignCultureItems` (33 lines) as inline business logic — both contain HashSet construction, decision logic, multi-call adapter sequences. Violates ADR-002 ("thin entry points <150 lines, no logic"). | **Behavior creep through additive private methods** | When adding the new passes, treated them as "just two more private helpers." The cumulative size + their internal complexity crossed the ADR-002 line. Standards review didn't fire on the prior commit because the behavior was 105 lines then; the new passes were each individually under any per-method line ceiling. | Extracted to new `ICultureMarketplaceMaintenanceService` + impl. Behavior now ~140 lines and delegates `EnsureGuaranteedStock` / `FilterForeignCultureItems` to the service. Service is constructor-injected, fully testable via public API (the prior private-method-reflection tests were renamed `CultureMarketplaceMaintenanceService{GuaranteedStock,Filter}Tests` and refactored to call the public methods directly, with the same coverage + 2 additional null-cultureId defenses). |
| D3 | MED | `ITownRosterAdapter.GetItemCount` only summed the FIRST `ItemRoster` stack matching the ItemObject (via `FindIndexOfItem`). Vanilla `ItemRoster` stores `(ItemObject × ItemModifier)` as distinct stacks — a town with "Sharp warg_brown ×3" + "Damaged warg_brown ×2" would report 3 (or 2, whichever stack is first in `_data[]`), not the total 5. Latent at MinStock=1 (any non-zero count satisfies the floor); real misfire at higher floors (the top-up would over-inject). | **Vanilla API semantic not verified for modifier-split inventory** | When implementing the adapter, treated `FindIndexOfItem` as "find the item" without considering that ItemRoster stacks split on modifier. The decompile verification confirmed the API exists but I didn't read the body to see it returns only the first matching index. | Rewrote `GetItemCount` to iterate `roster.Count` and sum `GetElementNumber(i)` for every index where `GetItemAtIndex(i) == itemObject`. O(n) where n is the town's distinct-item count (typically 30–200) — vanilla `FindIndexOfItem` was already O(n) so no regression. |
| D4 | LOW | Routing dict used `StringComparer.Ordinal` — flagged as "asymmetric vs `_byCulture` which uses `OrdinalIgnoreCase`." | **Self-audit revealed false positive** | The agent conflated two dict scopes: `_byCulture` is culture-id-keyed (case-insensitive is correct — defensive against author-typos), while `_routing`, `Blacklist`, and `WeightBoosts` are item-id-keyed (case-sensitive everywhere because `MBObjectManager.GetObject<ItemObject>(id)` is case-sensitive ordinal downstream — verified Phase 3). Item-id-keyed dicts are CONSISTENTLY Ordinal across the feature. | **Initially "fixed" to OrdinalIgnoreCase, then self-audit on the Codex Phase 4b prompt (S4 "case-insensitivity downstream") surfaced that uppercase author IDs would succeed the routing lookup but FAIL the `MBObjectManager` lookup → silent injection failure. Reverted to `Ordinal` with corrected comment explaining the key-scope distinction.** |

### Root cause pattern (Phase 4b): "scaling-up review surface area without scaling-up review attention"

D1, D2 share a shape: the previous review-and-ship cycles (3 of them) didn't notice these because each new piece was small in isolation. D1 was one new event hook; D2 was two ~20-line private methods. The deep-review caught both because Agent 1 measures lines per file (catches D2) and Agent 5 traces event iteration counts (catches D1). Both would have been caught earlier if a `/deep-review` had been run after Phase 2.5 (when the C5/C6 fixes added the partial-followup hook and pushed the behavior past the line ceiling).

**Generalizable rule (informal):** after any cluster of in-session fixes (Phase 2.5-style C5/C6 work), re-run `/deep-review` BEFORE the next ship event. Don't trust prior reviews on now-larger surface area. The cost of one extra review (~5 minutes wall-clock) is much less than the cost of shipping an architecture violation that the next review will catch anyway.

### Patch History (Phase 4b)

| Pre-fix | Post-fix |
|---------|----------|
| `if (i < 2) return;` allowed 98 sweep invocations per new game | `if (_initialSweepDone) return; if (i < 2) return;` — one-shot |
| `CultureMarketplaceBehavior.EnsureGuaranteedStock` + `FilterForeignCultureItems` private methods (194-line behavior) | Extracted to `ICultureMarketplaceMaintenanceService` + impl; behavior shrinks to ~140 lines, business logic in service |
| `GetItemCount` returned `FindIndexOfItem` → `GetElementNumber` of first matching stack | Iterates `roster.Count`, sums `GetElementNumber(i)` for every index where `GetItemAtIndex(i) == itemObject` |
| Routing dict comment claimed it was "for consistency" without specifying the scope | Kept `StringComparer.Ordinal` (item-id-keyed, consistent with `Blacklist`/`WeightBoosts`); comment now distinguishes the two key scopes and cites Phase 3's verification that `MBObjectManager` is case-sensitive |
| `Reflection`-based behavior tests on private methods (17 tests) | Public-API tests on `CultureMarketplaceMaintenanceService` (19 tests, +2 null defenses) |

### Tests after Phase 4b fixes

- 2 new null-cultureId defense tests on the maintenance service
- 17 tests migrated from reflection-private to public-API (same coverage, cleaner contract)
- Full suite: **2323 passed / 0 failed / 2 skipped** (was 2321 after Phase 4)
- CultureMarketplace + adapter scope: **89/89** (was 87 after Phase 4)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/rca-crash-report-2026-05-25.md](./rca-crash-report-2026-05-25.md)

<!-- backlinks-end -->
