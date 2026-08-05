# War of the Ring Momentum

## Overview

Tracks Evil-vs-Good war progress on a signed "momentum" meter fed by battles, sieges, raids, army-gatherings, and a daily strength differential. A persistent on-map slider shows who is winning; clicking it opens a detail popup (faction banners, leaders/allies, per-type breakdown, total stats).

**By default the war is endless** — momentum is tracked but no side ever wins. The victory mechanic (a side reaching the victory threshold, or eliminating the other, gated on player participation → an end-of-war inquiry + peace) is fully wired but **opt-in via `victoryEnabled` / the MCM "Enable Victory" toggle** (default off). See "Victory" and "Known limitations".

Ported from LOTRAOM 1.2.12's "Momentum" system onto TAOM 1.4.6 (issue #327).

## Why This Exists

TAOM already had the *war-triggering* half — the `WarOfTheRingService` phase machine (`Peace → IsengardWar → FullWar`, see [war-of-the-ring.md](war-of-the-ring.md)) — but no way for the player to see who is winning and no campaign arc that resolves the war. Players wanted visible Evil-vs-Good progress. This feature is the *progress + victory* half.

## Architecture

### Layer stack (ADR-002/007)

```
CampaignEvents  →  WarOfTheRingMomentumBehavior (thin entry point)
                        │ snapshots engine args via
                   IWarEventSnapshotAdapter  (MapEvent/Siege/Raid/Army → flat DTOs)
                        │ delegates to
                   IMomentumEventService     (scoring + decay, pure)
                   IMomentumEnrollmentService (side membership from IAlignmentService)
                   IMomentumVictoryService   (victory decision + peace-out)
                        │ mutate
                   IMomentumStateStore → MomentumWarState (pure domain math)
                        │ read by
                   IMomentumQueryService  →  UI (MapView + popup VMs)
```

Every service is pure and unit-tested; the behavior holds no logic beyond routing. Sealed TaleWorlds types never cross into a service — the snapshot adapter flattens `MapEvent`/`Army`/etc. to plain DTOs at the boundary.

### Momentum model

- Per-side momentum is stored as an **int on a ×100 internal scale** (`MomentumWarState.MomentumScale`) for battle-scaling precision. `InternalMomentum = (Free − Evil) / 100`, positive = Free ahead.
- Each side keeps `Dictionary<MomentumActionType, Queue<MomentumEvent>>`, capped 100 per type. Events **decay**: their value is subtracted back out when their `EndTimeHours` passes (battles/sieges/raids 504h, army 168h, strength 12h).
- Config event values are **internal-scale units** and are ×100'd into the store on the way in (`MomentumEventService`). This is a deliberate fix of a donor bug — LOTRAOM added config values raw while comparing `raw/100` against the threshold, so its own tuning comments ("~2 sieges for victory") were 100× off. Here `siege 250 + siege 250 ≥ threshold 500` as intended.

### Enrollment (deviation from LOTRAOM, justified)

LOTRAOM enrolled kingdoms from its own war-event scripting. TAOM's phase machine already owns war declaration, so momentum **enrolls dynamically**: on session launch and each daily tick while `CurrentPhase == FullWar`, every kingdom whose side is Free or Evil is swept into that side (Neutral excluded). Enrollment is bookkeeping only — it never declares wars or alliances (the donor's `StanceType.Alliance` reflection is impossible on 1.4.6 anyway; the enum has no `Alliance` value).

Side resolution is `IAlignmentService.GetKingdomSide(kingdomId)` (keyed on kingdom StringId via `execution/alignment.json`), **falling back to `GetCultureSide(kingdom.Culture.StringId)` when the kingdom id is Neutral/absent**. The culture fallback is what covers **player-founded and revolt kingdoms** — their runtime StringId (`new_kingdom*`) isn't in `alignment.json`, so without it the player's own kingdom would resolve Neutral and never appear on the meter (Codex #327 HIGH).

Each sweep also **reconciles** the enrolled sets against the current world: it drops any enrolled kingdom that is no longer live (a `KingdomDestroyed` missed while the feature was toggled off) OR whose current side no longer matches where it's enrolled (an `alignment.json` edit — e.g. Khand/`battania` flipped to Neutral — or a runtime culture change). The enroll loop then re-adds it to the correct side if it moved Free↔Evil. Without this, a kingdom enrolled before its alignment changed stayed stuck on the old side on an existing save.

### Victory ↔ phase-machine integration

`WarPhase` gained a terminal `WarEnded` value and `IWarOfTheRingService` gained `EndWar(WarOutcome)` / `Outcome`. All three peace-block layers (`TaomDiplomacyModel.IsAtConstantWar`, `TaomKingdomDecisionPermissionModel`, the `MakePeaceAction` prefix) route through `IsWarOfTheRingActive` / `ShouldBlockPeace`, which key off `CurrentPhase == FullWar` — so flipping the phase to `WarEnded` lifts all three at once with **zero patch changes**.

Victory is **opt-in**: `MomentumVictoryService.CheckAndApplyVictory` returns `None` immediately when `IMomentumSettingsProvider.VictoryEnabled` is false (the default — endless war), so nothing ever ends. When enabled, the sequence order is load-bearing (pinned by a `Received.InOrder` test): **freeze the state → `EndWar` → peace-out cross-side pairs**. `MakePeaceAction` is blocked until the phase leaves `FullWar`, so peace must come after `EndWar`. Both victories are gated on the player-participation requirement (LOTRAOM parity — the donor gates the Evil branch too).

Load reconcile in `OnSessionLaunched`: with victory **disabled**, a war that ended under a prior victory-enabled build is **un-frozen** (its momentum/kingdoms/stats kept, only the ended flags reset) so the meter and tracking resume — this is how an already-ended save becomes endless again. With victory **enabled**, it instead calls `EndWar` if the save says the momentum war ended but the phase machine hasn't caught up (idempotent).

### What counts as player participation

Both the ×1.5 `participationMultiplier` and the victory gate's event count key off one flag per event: `PlayerInvolved`, computed on the snapshot the adapter builds. The test is **the player's main party, or any party in the player's kingdom** (`WarEventSnapshotAdapter.IsPlayerRelated`) — plus `MapEvent.IsPlayerMapEvent` for battles.

Sieges used to be narrower. `PlayerInvolved` was `capturerParty?.IsMainParty == true`, so the player's own party had to *be* the captor. Capturing a fief inside an ally's army — the normal way a vassal takes anything — recorded no player event at all, costing both the multiplier and a credit toward `minimumPlayerEventsForVictory`, so the victory requirement quietly failed to advance. Sieges now use the identical `IsPlayerRelated` test as battles. **This was a single-player gap that predates any co-op work**; the 2026-08-03 field report found the multiplayer half of the same shortfall (see Known limitations).

That report also stated the victory requirement "can only be satisfied by the authority's MainHero". That was already inaccurate before the siege fix — battles were satisfiable by any party in the authority MainHero's *kingdom*. Worth knowing so the wrong premise isn't re-derived from the report.

## UI & display

**On-map bar** (`MomentumMapIndicator.xml` + `MomentumIndicatorMapView` — a `MapView` hosting a `GauntletLayer`): a persistent "War of the Ring" slider on the campaign map, added at FullWar and removed on victory (or when either MCM toggle is off). Its value is `IMomentumQueryService.SliderValue` — a **relative-balance ratio** `(free − evil) / (free + evil)` mapped to −100..+100, **positive = Free ahead** (the handle fills rightward toward the green end; negative = Evil, toward red). It is deliberately NOT a victory-threshold fraction: in a long war the accumulated momentum grows many times past the threshold, so a threshold-normalized value clamped to one end and the bar never moved. The ratio stays readable at any magnitude.

**Custom themed art** (`GUI/SpriteParts/ui_taom/WarOfTheRing/`, category `ui_taom` = `AlwaysLoad`): the bar dropped the native `Kingdom.Support.*` / `SPKingdom\progress_bar_frame` widgets for three LOTR-themed sprites (Imagine-generated source; the fill + Ring cut to transparent PNGs locally with PIL, the obsidian frame supplied pre-cut with a clean anti-aliased alpha):

| Sprite | File | Role in prefab |
|--------|------|----------------|
| Obsidian+gold frame | `wotr_frame.png` (700×164) | opaque **background** — Eye of Sauron (Evil) at the LEFT end, White Tree of Gondor (Free) at the RIGHT end, with a recessed channel between them (matches `positive = Free = right`) |
| Red\|green track | `wotr_fill.png` (360×57) | `WarOfTheRing.Bar.Fill` brush — the `SliderWidget` background, sized to the channel |
| The One Ring | `wotr_ring.png` (150×151) | `WarOfTheRing.Bar.Handle` brush — the sliding handle; travels toward the Eye (Evil winning) or the Tree (Free winning) |

Layering (Option B): the frame is the opaque background `Widget`; the `SliderWidget` (fill + Ring handle) is a sibling **drawn on top**, sized + centered to the channel (measured **55.4% × 34.2%** of the frame, centered both axes — so `HorizontalAlignment`/`VerticalAlignment="Center"` aligns it without margins). Container 400×94, channel-slider 222×32, Ring handle 44×44 (overhangs the channel for a bead look). Brushes live in `Main/_Module/GUI/Brushes/BalanceOfPower.xml`. **Bake pending:** the three loose PNGs must be packed by the editor sprite-generation (`SpriteSheetGenerator.exe` + the texture-compile pass that writes `ui_taom_*_tex.tpac`) before they render — a loose PNG is blank until baked (see [gui-sprite-system.md](./gui-sprite-system.md)). Sizes/alignment are first estimates; expect one in-game tuning pass (baked ≠ visible).

**Detail popup** (`MomentumView.xml` + `MomentumPopupController`/`MomentumPopupVM`): laid out to **mirror the bar — Evil on the LEFT column, Free on the RIGHT** (the prefab swaps the `Good*`/`Evil*` bindings + the `Number1`/`Number2` columns; the VM stays semantically Free=`Good*`). Faction banners (Gondor/Mordor leaders), Leaders/Allies rows (the enrolled kingdoms, banners rendered from each kingdom's `Banner` via `BannerImageIdentifierVM`), a per-`MomentumActionType` breakdown table with accumulating tooltips, and a Total Stats table (kills/settlements captured/villages raided). It ctor-computes the **banner/roster VMs once** and live-recomputes only the **numbers** (total/color/breakdown/stats) on `MomentumChanged` while open (unsubscribes on close). The banners are deliberately NOT rebuilt on change: banner textures render asynchronously, so re-creating the `BannerImageIdentifierVM` on every event replaced each VM before its texture finished and the banners flashed in and vanished (the popup is a non-pausing map layer, so events keep firing while it is open). The **"Total:" line shows the bounded balance magnitude (0–100), colored green when the Free Peoples lead, red when Evil leads, parchment when even** — direction is carried by colour so the sign isn't needed (and it fixes the near-invisible dark default text). Opened by clicking the bar; closed by the button or Escape (`GenericPanelGameKeyCategory` "Exit").

Kingdoms are resolved by StringId with `Kingdom.All.FirstOrDefault(k => k.StringId == id)` (the vanilla idiom) — **not** `MBObjectManager.GetObject<Kingdom>`, which does not resolve campaign kingdoms and returned null (blank banners + zero strength).

## Configuration

`Main/_Module/ModuleData/momentum/momentum.json` (validated; invalid values revert to default + warn; `Reuse.Singleton` — edits need a full game restart):

| Field | Default | Meaning |
|-------|---------|---------|
| `enabled` | true | JSON fallback for the master toggle |
| `victoryEnabled` | **false** | when off, the war is endless (tracked, never resolves); on = a side can win |
| `victoryThreshold` | 500 | internal momentum lead to win (only when `victoryEnabled`) |
| `events.maxBattleMomentum` | 350 | battle-won cap (scaled by casualties ÷ loser strength — stays small; that's why `killMomentumPerHundred` exists). Was 300, bumped 2026-07-05 |
| `events.siegeMomentum` | 400 | fixed per captured settlement (was 250 — raised 2026-07-05: taking a fief is the war's real objective, now the highest per-event weight) |
| `events.raidMomentum` | 50 | fixed per raided village (was 200 → 100 → 50 over 2026-07-04/05: Good factions rarely raid, so raids structurally over-fed Evil) |
| `events.armyMomentum` | 50 | fixed per army gathered (was 200 — lowered 2026-07-06 to match raids: gathering an army is a routine move, not a war outcome) |
| `events.maxStrengthMomentum` | **0** | daily strength-differential cap — **retired 2026-07-05** (was 300): Evil out-strengths Free for most of a campaign, so it just fed Evil free daily momentum. At 0 the award is skipped and the `RelativeStrength` row is dropped from the breakdown; set > 0 to bring it back |
| `events.killMomentumPerHundred` | 10 | **new 2026-07-05** — momentum per 100 enemies killed in battle, RAW attrition (not strength-normalized like battle-won, which stays tiny). Displayed = kills × this ÷ 100. Accrues for both sides on every war battle (mirrors the kill stat). Shows as an "Enemies Killed" breakdown row. 0 disables |
| `durationsHours.*` | 504/504/504/168/12/504 | decay windows (battle/siege/raid/army/strength/**enemiesKilled**) |
| `player.requireParticipationForVictory` | true | war can't end until the player has fought enough |
| `player.participationMultiplier` | 1.5 | momentum ×1.5 when the player takes part — see [What counts as player participation](#what-counts-as-player-participation) |
| `player.minimumPlayerEventsForVictory` | 5 | player events needed before either side can win |
| `strengthRatioForMaxMomentum` | 4.0 | how many × stronger a side must be for the max daily award |

**MCM** ("War of the Ring/Momentum" group, `TaomSettings`): `MomentumEnabled`, `ShowWarOfTheRingMapMeter`, **`MomentumVictoryEnabled`** (default off = endless), `MomentumVictoryThreshold` (100–2000), `MomentumParticipationMultiplier` (1.0–3.0), `MomentumRequirePlayerForVictory`, `MomentumMinPlayerEvents` (0–20). MCM values win over JSON; both surfaces clamp to the same ranges.

Defaults equal LOTRAOM's shipped `momentum_config.xml`, so the out-of-box balance matches the donor (minus the 100× scale bug).

## Persistence

`MomentumStateStore` serializes the whole war state to a `Dictionary<string,string>` (war flags, victor, per-side momentum/kingdoms/stats, every event queue, the player-event list). The behavior JSON-encodes that dictionary to a string, then **splits it across N `SyncData` strings** (`MomentumSyncChunker`) — a count key `_taom_wotr_momentum_v3_count` plus `_taom_wotr_momentum_v3_{i}` chunk keys, each capped at 10,000 UTF-16 chars.

**Why chunked, not one string (the v2.0.9 save-corruption fix — 2026-07-07):** the engine's `ArchiveSerializer.SerializeEntry` writes each save-archive entry's byte length as `(short)Data.Length` — a signed int16 truncation — but writes the data in full. So **any single `SyncData` string whose UTF-8 payload exceeds 32,767 bytes is written with a wrong length and CORRUPTS THE SAVE at write time** (unloadable next launch: `ArgumentException: Source array was not long enough` inside `ArchiveDeserializer.LoadFrom`, or `OverflowException` in the 32,768–65,535 range). The momentum log — up to 100 events/type × 6 types × 2 sides, each carrying its full localized description — crosses ~32 KB as one string around day ~50, so every save after that point was bricked. Chunking sizes each string so its worst-case UTF-8 expansion (3 bytes/char × 10,000 = 30,000 B) stays safely under the 32,763-byte entry limit, regardless of how the log grows — **no events or descriptions dropped, no gameplay change** (this is why the earlier single-string transport, which itself replaced a `Dictionary<string,string>` container that didn't round-trip the `IDataStore` at scale, was NOT enough — the container fix traded one scale failure for a worse one). On load the count is read first, then that many chunk keys are joined and parsed. A corrupt/absent count loads as fresh state (logged); the `_v3` keys mean an old single-string `_v2` (or dict `_v1`) save loads as absent → a **one-time momentum reset** on the first load after the update — kingdoms re-enroll and momentum re-accrues on the next daily tick; the campaign itself is untouched. **Event descriptions are stored resolved** (in the write-time language — a documented limitation; a save made in English shows English event tooltips after switching to French). The Diplomacy behavior persists the phase + outcome as ints. This also fixes a real LOTRAOM bug: the donor never persisted the player victory-gate events, so victory progress silently reset on reload.

**Already-bricked saves are recoverable** with `tools/repair_sav_strings.py` (offline, zero campaign-data loss — resets only the oversized momentum string; the war-meter history clears, everything else is byte-identical). See `docs/features/save-load-diagnostics.md` and the RCA below. Player how-to: `docs/SAVE-REPAIR-GUIDE.md`. Full write-time-corruption RCA: `docs/reviews/rca-momentum-save-corruption-2026-07-07.md`.

## Key Files

| File | Purpose |
|------|---------|
| `Domain/MomentumWarState.cs` + `MomentumSideData.cs` | pure momentum math (scale, decay, queue caps) |
| `Domain/MomentumEvent.cs`, `MomentumTotalStats.cs`, `MomentumActionType.cs`, `MomentumSide.cs` | domain POCOs |
| `Snapshots/*.cs` | flat DTOs (battle/siege/raid/army, incl. `TroopCasualties`) built at the adapter boundary |
| `MomentumEventService.cs` | scoring + decay |
| `MomentumEnrollmentService.cs` | side membership sweep |
| `MomentumVictoryService.cs` | victory decision + ordered peace-out |
| `PlayerMomentumService.cs` | participation multiplier + victory gate |
| `MomentumStateStore.cs` | serialize/deserialize state ↔ flat dict (JSON-stringified for SyncData by the behavior) + `MomentumChanged` event |
| `MomentumSyncChunker.cs` | pure `Split`/`Join` of the serialized JSON across `SyncData` strings ≤10,000 chars — keeps every entry under the engine's 32,767-byte limit (the v2.0.9 save-corruption fix) |
| `MomentumQueryService.cs` | UI read facade (pins the ratio `SliderValue`, `positive = Free`) |
| `MomentumConfigProvider.cs` + `MomentumSettingsProvider.cs` | validated JSON + MCM-over-JSON |
| `MomentumTextService.cs` | localized event-description composer |
| `WarOfTheRingMomentumBehavior.cs` | thin campaign-event entry point + SyncData |
| `WarOfTheRingMomentumIoC.cs` | registrations |
| `UI/MomentumIndicatorMapView.cs` | on-map slider MapView (adds/removes via `IMomentumUIService`) |
| `UI/MomentumPopupController.cs` + `MomentumPopupVM.cs` | detail popup |
| `UI/MomentumIndicatorItemVM.cs`, `MomentumIndicatorVM.cs`, `FactionRelationshipVM.cs`, `BreakdownVM.cs` | VMs |
| `Main/Adapters/WarEventSnapshotAdapter.cs` + `KingdomStrengthAdapter.cs` | engine boundary |
| `Main/_Module/GUI/PreFabs/MomentumView/*.xml` | prefabs (fork residue, 1.4.x-migrated); `MomentumMapIndicator.xml` rewired to the custom bar art |
| `Main/_Module/GUI/SpriteParts/ui_taom/WarOfTheRing/wotr_{frame,fill,ring}.png` | custom bar sprites (obsidian frame / red-green track / One Ring handle) — bake via editor `SpriteSheetGenerator` |
| `Main/_Module/GUI/Brushes/BalanceOfPower.xml` | `WarOfTheRing.Bar.Fill` / `.Handle` brushes + the momentum text brushes |
| `Main/_Module/ModuleData/taom_wotr_strings.xml` | 24 localization keys |

## Dependencies

`IWarOfTheRingService` (Diplomacy — the phase machine), `IAlignmentService` (Execution — Free/Evil/Neutral classification, `execution/alignment.json`), `IAllianceAdapter` + `IPlayerContextAdapter` (shared adapters), MCM (`TaomSettings`).

## Tests

`TAOM.Tests/Features/WarOfTheRingMomentum/` — ~155 tests across 9 files: domain math (scale, queue-cap trim), config validation (one per rule), state-store round-trip (NaN reject, malformed skip, pipe-in-description, re-cap), player service (gate, multiplier, tie), event service (scoring, decay, filters, battle-ratio clamp, donor-parity quirks), enrollment (never-Neutral, dedup, elimination + alignment-change reconciliation, player-founded culture fallback), victory (threshold/elimination, player gate, `EndWar`-before-`MakePeace` ordering, idempotence), query facade (ratio slider sign, runaway-doesn't-pin, zero/equal). Plus the extended `WarOfTheRingServiceTests` for the `WarEnded`/`EndWar` phase transition. The adapters (`KingdomStrengthAdapter`, `WarEventSnapshotAdapter`) and the behavior/VMs are boundary code (mocked in service tests; live-only per ADR-008) — note this is why the `MBObjectManager` kingdom-resolution regression passed the whole suite yet broke in-game.

## How-To

**Retune balance:** edit `momentum/momentum.json`, restart the game (singleton cache). For live in-game tuning use the MCM group.

**Reach FullWar fast for testing:** the existing WotR `testMode` (`diplomacy/war_of_the_ring.json`, `phase2Day: 3`) triggers FullWar on day 3 — the meter appears there.

**Add a new momentum source:** add a `MomentumActionType`, a snapshot DTO + adapter method, a `Process*` in `MomentumEventService`, a duration in `MomentumDurationsConfig.GetHours`, a label in `MomentumPopupVM.ActionTypeLabel`, and a `{=taom_wotr_*}` string. The store's per-type serialization and the breakdown UI pick it up automatically.

## Deliberate deviations from LOTRAOM

- **Scale fix**: config values are internal-scale units ×100'd into the store (donor was 100× off its own tuning).
- **Raids require an enrolled kingdom**: the donor sided raids by culture with no enrollment check, so every looter raid fed Evil +200.
- **No alliance-stance setting**: `StanceType.Alliance` doesn't exist on 1.4.6; TAOM Diplomacy owns stances anyway.
- **Raid/army don't apply the participation multiplier or victory-gate credit**: donor parity — LOTRAOM applied both only to battles and sieges. Documented on the snapshot DTOs.
- **Dropped the dead tanh soft-cap** (`softCapDivisor`/`DisplayMomentum`): never wired into the donor's UI either; removed per the simplicity criterion.
- **Slider is a relative-balance ratio, positive = Free** (donor + first cut used a threshold fraction, positive = Evil): the threshold fraction pinned to one end in a long war, and positive = Evil clashed with the green-good colour. See UI & display.
- **Khand (`battania`) is Neutral, not Evil** (2026-07-04 balance decision): changed in the shared `execution/alignment.json`, so it applies to all alignment features, not just the meter.

## Known limitations

- **The war is endless by default (`victoryEnabled = false`, user decision 2026-07-04).** Victory is turned OFF at the toggle, so no side ever wins — the momentum is tracked open-endedly. This is the chosen behaviour; **do NOT "fix" it as an oversight.** The victory machinery is fully wired and tested — flip `victoryEnabled` / the MCM "Enable Victory" toggle to turn it on.
- **Momentum accumulates without bound.** Events trimmed at the per-type cap (100) never subtract back out (LOTRAOM parity), and the player-participation gate can hold the war open, so `InternalMomentum` grows far past the victory threshold in a long campaign. This is *why* victory was made opt-in: with runaway momentum an enabled threshold-victory fires almost immediately once the player has ~5 events (anticlimactic). Before enabling victory for a real playthrough, pair it with a bounded-momentum rebalance — make cap-trim subtract (bounding momentum to the decaying event window) so the threshold is meaningful. The map bar and popup Total use the bounded balance *ratio*, not the raw magnitude, so the runaway value is never shown to the player.
- **A remote co-op player in another kingdom earns no participation credit.** `OnSiegeCompleted` and `OnMapEventEnded` are `IsAuthority`-gated, and `IPlayerContextAdapter.GetPlayerKingdomId()` resolves the LOCAL peer's clan — so on a client the event is never scored at all, and on the host the participation test runs against the *host's* kingdom. Closing that needs a co-op seam TAOM does not have. The 2026-08-03 siege-participation fix is the single-player half only and is **not** a multiplayer fix.
- Event descriptions freeze in the write-time language (stored resolved in the save).
- A battle resolving earlier in the campaign day than the daily enrollment sweep on the exact day FullWar first triggers is dropped (war "begins" that tick).
- **Momentum persistence is bounded per synced string, not per total** — the chunker caps each `SyncData` string, but the total serialized log still grows with the campaign (bounded by the 100/type event cap). This is fine (~9 chunks at the observed day-52 max) and cannot corrupt a save; it just means the momentum section of a very long save is a handful of KB.

## Status

Built + ~155 tests green + deep-reviewed (5 agents, 6 findings fixed) + Codex-reviewed (1 HIGH + 4 lower, all fixed) — RCA `docs/reviews/rca-wotr-momentum-2026-07-03.md`. **In-game confirmed:** map bar renders + moves, popup renders with faction banners, Relative-Strength award works, Khand dropped from Evil. **Still owed:** save/reload persistence round-trip and the full victory flow (inquiry → wars end → meter freezes) under a live game. Not yet merged to trunk (`/finish-branch`); AI localization pass pending (`ANTHROPIC_API_KEY`).

## Play-test fix history (2026-07-03 → 07-04)

The feature was play-tested iteratively after the reviews; the notable live-only fixes (all on `feature/wotr-momentum`, detailed in the RCA + CHANGELOG):

| Symptom | Root cause | Fix |
|---------|-----------|-----|
| Blank Leaders/Allies banners + Relative-Strength 0/0 | deep-review "efficiency fix" swapped `Kingdom.All.FirstOrDefault` → `MBObjectManager.GetObject<Kingdom>`, which returns null for campaign kingdoms | reverted to `Kingdom.All` |
| Total stats + momentum reset every reload | store synced as a `Dictionary<string,string>` (~1000 entries) didn't round-trip the engine `IDataStore` at scale | JSON-encode the dict to one string, sync the string (`_taom_wotr_momentum_v2`) |
| **Saves unloadable past ~day 50** ("A problem occured while trying to load the saved game.") | the single momentum string crossed 32,767 B; the engine's `ArchiveSerializer` writes entry length as `(short)Data.Length` (int16 truncation) → corrupt on WRITE (2026-07-07) | **split the JSON across chunk keys ≤10,000 chars each (`_v3`)** — `MomentumSyncChunker`; already-bricked saves recovered offline via `tools/repair_sav_strings.py` |
| Popup number columns wrapped (`12200`→`200-`/`00`) | value columns pinned at `SuggestedWidth=50` | widened to 120 |
| Player-founded kingdom never enrolled | enrollment keyed on kingdom-id via `alignment.json` (dynamic ids absent) | culture fallback |
| Khand shown as Evil | data: `battania` was `evil` | set Neutral in `alignment.json` + sweep reconciliation |
| Map bar pinned to one end / Total an ever-growing negative | threshold-normalized slider + runaway momentum | ratio slider + colored bounded Total |

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/war-of-the-ring.md](./war-of-the-ring.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
