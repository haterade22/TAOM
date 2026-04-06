# Codex Adversarial Review: Infrastructure (Adapters, Core, SubModule, IoC)

**Date:** 2026-04-05
**Target:** branch diff against master
**Verdict:** needs-attention

No-ship. The infrastructure layer has two high-risk state/lifecycle defects and one observability failure: ruling-clan banner sync reinitializes the whole kingdom object, mission agent adapters are cached past mission boundaries, and logger shutdown can drop or fault queued writes during teardown.

## Section 1: Initialization Order

### Init Sequence (from SubModule.cs + IoC.cs)

1. `OnSubModuleLoaded`: IoC container created, core services registered, all feature IoC calls executed
2. `OnGameStart`: Harmony patches applied by category
3. `OnCampaignStart`: CampaignBehaviors added, GameModels registered
4. `OnGameEnd`: Container disposal

No features initialized before their dependencies. No circular dependencies found in the DryIoc container — all registrations are either Singleton or Transient with clear dependency chains.

## Section 2: Adapter Safety

### Null-conditional on TaleWorlds properties

Adapters generally use null-conditional on adapter method entry, before accessing sealed type properties. No direct property-access-before-null-check patterns found.

### Stale cached references — See Finding 2

`MissionAdapterFactory` caches agents by `Agent.Index` as a process-lifetime singleton. Agent indices are mission-scoped and reused across missions.

### Boundary leaks

`ClanColorInfo` is a `record struct` — a value type that copies TaleWorlds data rather than exposing the sealed type. Other adapters follow the same pattern. No direct sealed type exposure found in adapter interfaces.

## Section 3: Core Services

### FileLogger — See Finding 3

Concurrent writes are handled via a `ConcurrentQueue<string>` with a background writer thread. Disposal races the writer thread with a 2-second timeout.

### RaceManager

Maps race names to IDs via `FaceGen.GetRaceOrDefault(raceName)` at runtime. The mapping is stable across save/load because it derives from the game's registered monster list, which is loaded from XML before any save data.

### ReflectionHelper

Used by `AtmospherePersistenceService` and `BannerColorService`. Field/property names are hardcoded strings (e.g., `"InitializerRecord"`, `"_primaryBannerColor"`). These are version-sensitive — already flagged in the AtmospherePersistence review.

## Findings

### [HIGH] Ruling-clan banner sync reinitializes the entire kingdom, not just colors

**File:** `BannerHeroAdapter.cs:20-47`

**TAOM code:** `SyncKingdomColors` calls `Kingdom.InitializeKingdom(...)` after every `Clan.UpdateBannerColor` on the ruling clan.

**Vanilla code (decompiled):** `Kingdom.InitializeKingdom` resets non-color state including `Banner`, `PrimaryBannerColor`, `SecondaryBannerColor`, and `PoliticalStagnation = 100` (Kingdom.cs:563-575). Vanilla `Clan.UpdateBannerColor` only updates clan banner color fields (Clan.cs:1405-1410).

**Evidence:** A normal banner-color change silently mutates kingdom-level campaign state far beyond visuals. Political stagnation reset can affect diplomacy decisions.

**Remediation:** Replace `InitializeKingdom` with a narrowly scoped kingdom color/banner update. Only touch the fields required for banner persistence — do not reset political stagnation or other unrelated kingdom state.

### [HIGH] Mission adapter cache is module-singleton state keyed by mission-scoped Agent.Index

**File:** `MissionAdapterFactory.cs:11-24`

**TAOM code:** `ConcurrentDictionary<int, IAgentAdapter>` lives for the singleton factory's lifetime. Registered as `Reuse.Singleton` in `IoC.cs`. No cache clear on mission end.

**Vanilla code:** `Mission.Current.FindAgentWithIndex(...)` is mission-scoped (Mission.cs:385-388). Agent indices are not globally unique — they are reused across missions.

**Evidence:** Once a later mission reuses an index, `GetAgentAdapter` returns an adapter wrapping an `Agent` from an earlier mission. Classic stale-reference bug in a foundation service used by Warg and AdvancedCombat.

**Remediation:** Scope `MissionAdapterFactory` to mission lifetime (register per-mission or clear cache on mission end). Or key the cache by mission-unique identity.

### [MEDIUM] Logger teardown can race the writer thread and lose final diagnostics

**File:** `FileLogger.cs:38-59`

**TAOM code:** `Dispose` sets `_stopping`, waits 2 seconds via `Thread.Join(2000)`, then disposes `_logFile` unconditionally. `ProcessQueue` continues while `!_stopping || !_queue.IsEmpty` and writes via `_logFile?.WriteLine(...)` without synchronization.

**Evidence:** If the queue is still draining after the 2-second join timeout, the background thread can write to a disposed `StreamWriter` or drop remaining lines. `IoC.Dispose()` runs during `SubModule.OnSubModuleUnloaded` — exactly when shutdown diagnostics are most valuable.

**Remediation:** Wait until the queue is fully drained before disposing the stream, or use a synchronized producer/consumer with explicit completion semantics.

## Observations

- No circular dependencies in IoC container
- Initialization order is clean: core -> features -> Harmony -> GameModels -> Behaviors
- `ClanColorInfo` record struct pattern correctly avoids boundary leaks
- `RaceManager` mapping is stable because it derives from game's monster XML, loaded before saves
- `ReflectionHelper` hardcoded strings are the same version-sensitive targets flagged in AtmospherePersistence and BannerColorPersistence reviews
- No adapter interfaces expose sealed TaleWorlds types directly

## Recommended Next Steps

1. **Fix `BannerHeroAdapter.SyncKingdomColors`** — replace `InitializeKingdom` with targeted color-only update (highest impact: campaign state corruption from common UI action)
2. **Fix `MissionAdapterFactory` lifecycle** — scope to mission or clear on mission end (stale agents affect Warg + AdvancedCombat)
3. **Fix `FileLogger` teardown** — drain queue before disposing stream
4. Add integration test for adapter cache invalidation across mission boundaries
