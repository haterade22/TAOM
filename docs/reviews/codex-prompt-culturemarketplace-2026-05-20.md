# Codex Adversarial Review — CultureMarketplace (2026-05-20)

You are reviewing a new TAOM feature called CultureMarketplace. It injects culture-tagged LOTRLOME items into town markets daily so the player can buy lore-correct equipment in lore-correct towns. The feature was just shipped on the bannerlord-1.3.15 branch (GitHub issue #207). Internal /deep-review already ran and found 3 LOW dead-code findings (CountPerInjection field, EnumerateTowns method, TownInjectionContext type) -- ALL FIXED before this review. Do not re-flag them. RCA at docs/reviews/rca-culturemarketplace-aspirational-scaffolding-2026-05-20.md.

Look for what /deep-review missed: logic bugs, vanilla-integration gaps, lifecycle holes, ID mismatches, fail-safe inversions, missing null guards, save-load issues, threading hazards.

## TAOM ID CHEATSHEET
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".
CONFIRMED: Main/_Module/ModuleData/taom_spcultures.xml declares "gondor" (line 2900) and "mordor" (line 3269) as culture IDs. empire_w / empire_s are KINGDOM ids only, not culture ids -- those appear only in spkingdoms.xslt, taom_careers.xml, and special_resources_config.xml.

## READ FIRST
- docs/features/culture-marketplace.md -- the feature overview
- docs/reviews/rca-culturemarketplace-aspirational-scaffolding-2026-05-20.md -- the 3 findings already fixed in Phase 1
- Main/_Module/ModuleData/culture_marketplace/culture_marketplace_config.xml -- empty override stub
- CLAUDE.md "CultureMarketplace" feature row + "CultureMarketplace config" row

## Known Suspects -- CONFIRM or DISPUTE

S1. Per-town cap counts the WHOLE roster, not just TAOM-injected items. CultureMarketplaceBehavior.cs:78 calls `_townAdapter.GetRosterDistinctItemCount(settlement)` which returns `settlement.ItemRoster.Count` -- the full count including vanilla items. CultureMarketplaceInjectionService.cs:32 then exits if `currentRosterCount >= _tuning.PerTownInjectedCap` (60). If a vanilla town already has 30+ vanilla items pre-feature, the headroom for injection is artificially small, and once total hits 60 NO further TAOM items inject. The field name "PerTownInjectedCap" suggests it should cap only OUR injections; the implementation caps total roster. Is this the right semantic? Decompile vanilla TownMerchantsCampaignBehavior and confirm typical post-vanilla-init roster size for a fresh town.

S2. EnsurePoolBuilt failure latch. CultureMarketplaceBehavior.cs:54-65 catches all exceptions during BuildPools, logs an error, and leaves `_poolBuilt = false`. OnDailyTickSettlement at line 68 then retries EnsurePoolBuilt every tick forever (200+ towns x daily) if the first build failed. Each retry re-scans MBObjectManager (the underlying IItemPoolAdapter has a `_cache` guard so it's cheap, but still) -- and continues spamming the error log forever. Should there be a permanent-failure latch after N attempts, or is the current retry-forever the intended behavior?

S3. PrefixMap ordering and over-match risk. ItemPoolAdapter.cs:13-42 maps ID prefixes to culture IDs in a fixed order. Several entries are sub-strings of others or share a stem. Audit for over-match -- e.g., `"sm_uruk_"` precedes other isengard prefixes, `"haradrim"` (no trailing underscore) could over-match into `haradrim01_anyname` AND `haradrim_specific_item`. Verify the iteration order produces the intended culture for every LOTRLOME item ID -- particularly cross-faction items where prefix overlap could route to the wrong culture pool.

S4. Item.Culture vs Clan.Culture type asymmetry. `ItemObject.Culture` is `BasicCultureObject` (line 150 of decompiled ItemObject.cs); `Clan.Culture` is `CultureObject : BasicCultureObject`. The code compares only `StringId` strings (TownRosterAdapter.cs `settlement?.OwnerClan?.Culture?.StringId` and ItemPoolAdapter.cs `item.Culture?.StringId`). Confirm this string-equality path is correct in all cases including when a Gondor item has `Culture.gondor` but a Gondor town owner-clan has `Culture` resolving to a CultureObject whose StringId might be derived differently. Cross-check LOTRLOME armor XML attribute `culture="Culture.gondor"` vs the actual loaded `BasicCultureObject.StringId` -- TaleWorlds may strip the "Culture." prefix during load, or it may not.

S5. WeightedDraw floating-point edge case. CultureMarketplaceInjectionService.cs:65-78 does `var roll = (float)(rng.NextDouble() * pool.TotalWeight)` then iterates and returns at `roll <= cumulative`. With pools of 1000+ entries (Rhun has 1211 items, all weight=1), TotalWeight=1211f, cumulative addition order is left-to-right. Float-summing 1211 1.0 values can drift; the final cumulative may end up < 1211 due to imprecision. If `rng.NextDouble()` returns very close to 1.0, `roll` could exceed the actual cumulative sum and fall through the for-loop -- then the code falls back to the last item (line 76). Verify the floating-point assumption: is the last-item fallback safe, or does it bias the distribution toward the last item?

S6. Save-load resilience. CultureMarketplaceBehavior has no SyncData (injected items live in vanilla Settlement.ItemRoster which is engine-serialized). But the IN-MEMORY pool is rebuilt on OnGameLoadedEvent. What happens if a save was made with EnableX modset Y, then loaded with modset Z that no longer has those item IDs? Vanilla Settlement.ItemRoster will have stale ItemObject references that fail to resolve. Confirm whether vanilla self-heals this (drops missing items on load) or if our cap-vs-headroom math breaks because settlement.ItemRoster.Count includes broken entries.

S7. Random instance shared across multiple settlements per tick. CultureMarketplaceBehavior.cs:19 has `private readonly Random _rng = new();` used in OnDailyTickSettlement. DailyTickSettlementEvent fires PER settlement -- if vanilla fires these calls in parallel on multiple threads (it likely doesn't, but verify), the shared Random instance is not thread-safe (NextDouble can return 0/0/0 under contention). Decompile CampaignEvents.DailyTickSettlementEvent's dispatch path and confirm it's main-thread only.

S8. Item availability per tier / quest reservation. The feature deliberately ships ALL ~6155 items including named/unique items (Anduril, witchking_sword, theoden_sword, faramir_armor, etc.) per the user-confirmed design. But are any of these items referenced as quest reward items elsewhere in TAOM's quest system? If Anduril is supposed to be a quest reward and the marketplace can also sell it, save-state may break or the quest UI may behave oddly. Grep TAOM source + LOTRLOME quest XML for `anduril`, `witchking_sword`, etc. and report whether any are quest-locked.

## Files to review (paths relative to repo root)

Feature services:
- Main/Features/CultureMarketplace/CultureMarketplaceBehavior.cs
- Main/Features/CultureMarketplace/CultureMarketplaceConfigProvider.cs
- Main/Features/CultureMarketplace/ICultureMarketplaceConfigProvider.cs
- Main/Features/CultureMarketplace/CultureItemPoolService.cs
- Main/Features/CultureMarketplace/ICultureItemPoolService.cs
- Main/Features/CultureMarketplace/CultureMarketplaceInjectionService.cs
- Main/Features/CultureMarketplace/ICultureMarketplaceInjectionService.cs
- Main/Features/CultureMarketplace/CultureMarketplaceIoC.cs

Domain types:
- Main/Features/CultureMarketplace/Domain/MarketplaceTuning.cs
- Main/Features/CultureMarketplace/Domain/CultureItemPool.cs
- Main/Features/CultureMarketplace/Domain/ItemPoolEntry.cs
- Main/Features/CultureMarketplace/Domain/ItemPoolItem.cs
- Main/Features/CultureMarketplace/Domain/MarketplaceConfigOverride.cs

Adapters:
- Main/Adapters/IItemPoolAdapter.cs
- Main/Adapters/ItemPoolAdapter.cs
- Main/Adapters/ITownRosterAdapter.cs
- Main/Adapters/TownRosterAdapter.cs

Wiring:
- Main/IoC.cs (CultureMarketplace lines only)
- Main/SubModule.cs (CultureMarketplaceBehavior registration block only)

Tests:
- TAOM.Tests/Features/CultureMarketplace/CultureMarketplaceConfigProviderTests.cs
- TAOM.Tests/Features/CultureMarketplace/CultureItemPoolServiceTests.cs
- TAOM.Tests/Features/CultureMarketplace/CultureMarketplaceInjectionServiceTests.cs

Config:
- Main/_Module/ModuleData/culture_marketplace/culture_marketplace_config.xml

## REQUIRED SECTIONS in your output

### 1. VANILLA CODE
For each Suspect that involves TaleWorlds vanilla integration, paste the relevant decompiled code as a fenced code block. Use the v1.3.15 INSTALLED DLLs at E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/ (NOT the v1.4 dump at E:/Decompiled_Bannerlord/ -- they drift). Targets:
- VillageGoodProductionCampaignBehavior.TickGoodProduction (the vanilla restock path)
- TownMerchantsCampaignBehavior.OnSessionLaunched (initial town inventory setup if it exists)
- CampaignEvents.DailyTickSettlementEvent dispatch path
- TaleWorlds.CampaignSystem.Settlements.Town.MarketData (relevant only if cap semantics are surprising)

### 2. DEEP ANALYSIS

#### 2a. Cap-vs-Headroom Semantics (S1)
Concrete scenario: at OnNewGameCreated, what does Settlement.ItemRoster.Count typically equal for a fresh Gondor town? 0, 5, 30? Walk through the first 3 daily ticks: roster count, headroom remaining, items injected. Show whether the player ever sees Gondor items in markets if the town starts with 50 vanilla items.

#### 2b. WeightedDraw Distribution (S5)
With Rhun pool of 1211 items all weight=1.0, what is the probability that the last item is selected vs any other item? Compute or simulate. If it's biased, propose the smallest fix.

#### 2c. Lifecycle Holes (S2, S6)
Walk through these scenarios and report any state-corruption or no-op latch:
- New game start, OnNewGameCreated fires, MBObjectManager not yet fully initialized
- Save in a Gondor town. Quit. Restart Bannerlord. Load the save. Does the pool rebuild?
- Mid-session: player captures Minas Tirith for Mordor. Confirm the NEXT daily tick on Minas Tirith pulls from the Mordor pool, not Gondor.

#### 2d. Prefix Map Audit (S3)
Run the iteration manually against a list of likely LOTRLOME item IDs spanning multiple cultures. Identify any item ID that maps to the WRONG culture due to prefix overlap. Quote the offending row in ItemPoolAdapter.cs.

#### 2e. Race / Threading Audit (S7)
Decompile CampaignEvents.DailyTickSettlementEvent's dispatcher and confirm whether DailyTickSettlement listeners run on the main campaign thread sequentially or possibly in parallel. If parallel, the shared `_rng` is a bug -- propose a fix (per-thread Random or lock).

### 3. CONFIG CROSS-REFERENCE

Verify against TAOM ID CHEATSHEET:
- Every culture ID in ItemPoolAdapter.cs PrefixMap (gondor, mordor, isengard, erebor, mirkwood, rivendell, aserai, khuzait, empire, vlandia)
- Every culture ID likely returned by `town.OwnerClan.Culture.StringId` for the 17 LOTRLOME folders (Gondor, Mordor, Rohan, Erebor, Isengard, Rivendell, Mirkwood, Rhun, Harad, Dunland, Gundabad, Dol Guldur, Arnor, Iron Hills, Mercenary, Thenn, Troll)

Flag any mismatch -- e.g., LOTRLOME "Rohan" folder items tagged Culture.vlandia but the prefix map routes them through `("rohan_", ?)` -- WAIT, there is no `rohan_` row, only `("whiterun_", "vlandia")` and `("cts_rohan_", "vlandia")` and `("theoden_", "vlandia")`. Do any Rohan items use prefix `rohan_<something>` and would they fall through to the unresolved bucket?

### 4. FINDINGS OR OBSERVATIONS

For each finding:
- CRITICAL / HIGH / MEDIUM / LOW
- Concrete bug description with file:line citation
- Why it matters (player-visible symptom or maintenance hazard)
- Smallest fix
- For each Known Suspect: CONFIRMED or DISPUTED with reason

If you find nothing, write OBSERVATIONS section anyway with patterns Codex sees that might bite later.

## QUALITY GATES (you MUST satisfy all 5)

1. Vanilla code blocks present for at least 2 of: VillageGoodProductionCampaignBehavior, CampaignEvents dispatch, ItemRoster.AddToCounts, BasicCultureObject vs CultureObject.
2. CONFIRMED / DISPUTED verdict on every numbered Known Suspect (S1-S8).
3. Concrete scenario walk-through for S1 cap-vs-headroom (with hypothetical roster counts).
4. Prefix map audited against a sample of 20+ real LOTRLOME item IDs (grep LOTRLOME_items/ if needed).
5. Config cross-reference verifies all 10 PrefixMap culture IDs against the TAOM ID CHEATSHEET above.

## Lessons From Prior Reviews

SUCCESSES: Config ID cross-ref has caught rohan/dol_guldur mismatches multiple times. Vanilla decompilation has caught missing gates that internal review missed. Lifecycle tracing has caught stale caches.

FAILURES TO AVOID:
- Codex has assumed empire=Rohan in the past (it is Dunland). Use the cheatsheet.
- Codex has flagged vanilla-matching code as bugs (the code is doing what vanilla does, intentionally).
- Codex has skipped hard sections when the prompt got long. Do not skip the Quality Gates.
- Codex has confused `gondor` with `empire_w` (the kingdom ID for Gondor) -- they are different namespaces.

## Output

Write to: docs/reviews/codex-adversarial-culturemarketplace-2026-05-20.md
