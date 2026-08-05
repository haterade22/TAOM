# Native-commit audit — what TAOM actually needs resident, and where 20 GB went

> Hand this to a fresh session: "Read `docs/investigations/native-commit-audit-2026-08.md` and run the next unfinished phase."
> Trigger: issue #385 (facegen CTD at 20.3 GB process commit on a tester's 16 GB machine, 2026-08-05).
> Companion instrument: `[MemSample]` telemetry (#386) — periodic process+system memory lines in `Logs/taom_debug_*.log`.

Two ledgers, never conflated:

- **Runtime commit** — bytes the process actually commits. This is the crash-relevant axis.
- **Install weight** — bytes shipped to players. Bloat here costs download/disk, not stability.

## Grounding: the #385 numbers (from the tester's dump, parsed 2026-08-05)

| Fact | Value |
|---|---|
| Machine | 16 GB RAM, RTX 3070, 3440×1440 |
| Process commit at death | **20.3 GB** = 15.65 GB private + 4.04 GB mapped + 0.65 GB image (MemoryInfoList, 12,198 regions) |
| System commit at death | 23,081 MB used / 8,588 MB headroom |
| Managed heap | ~654 MB — the weight is native |
| Crash site | `TaleWorlds.Native.dll+0x58232c`, facegen static-morph, null morph-index array, engine worker thread |
| Streaming churn | 1,163 "partial read on compressed asset data" lines in the 10-min crash session; 28,863 in one longer session |

## Static inventory (measured 2026-08-05; game at `E:\Steam\steamapps\common\Mount & Blade II Bannerlord`)

### Runtime-commit suspects, ranked

1. **Banner-icon sprite atlases (eager, uncompressed).** `Main/_Module/GUI/SpriteParts/Config.xml` marks all 5 TAOM
   categories `<AlwaysLoad/>`: 52 sheets, of which **41 × 4096² serve `ui_taom_bannericons`** — 369 icons authored at
   1024×1024. Per `docs/reference/banner-icon-generation.md` step 4b.3 the atlases are imported **"Do Not Generate
   Mips" + "Do Not Compress"** → ~3.2 GB RGBA8 if resident uncompressed (~800 MB if BC). Vanilla baseline: 290 icons
   at 100×100 in ONE 2048² sheet, `AlwaysLoad=False`; vanilla's total AlwaysLoad surface is 162 MB.
   The 73 × exactly-64 MiB files in `Modules/TAOM/RuntimeDataCache` are the matching 4096² RGBA8 no-mip signature.
2. **RuntimeDataCache — open question Q1 (decisive, unresolved).** TAOM modules ship 42 GB of RDC
   (TAOM 5.2 / TAOM_Map 21.4 / Armory 14.4 GB); **no vanilla module ships any**. The shipping client's
   `TaleWorlds.Native.dll` contains the full RDC string surface (`RuntimeDataCache`, `rdc`, "RDC cache path is not
   valid", a "partial read on compressed asset **RDC** data" message variant) — the read path exists natively; the
   managed decompile has zero RDC references, so decompilation cannot settle whether the client uses it. The tester's
   log spam is the generic (non-RDC) variant, so his reads were tpac — but his install's RDC presence is unconfirmed.
   Armory/Map RDC entries are BC-compressed with mips; **the uncompressed-RGBA8 pathology is specific to TAOM's UI atlases.**
3. **FactionMap textures loaded but never freed.** `FactionImageWidget.cs:45-82`, `PolygonWidget.cs:779-911`,
   `BannerWidget.cs:253-284` — `EngineTexture.LoadTextureFromPath`, per-widget `_textureLoaded` flag, no cache, no
   dispose. One source PNG decodes to 281 MB (6227×11825); worst case ~1.2 GB resident after character creation.
   Plus **545.7 MB of orphan FactionMap sprites still in the game install** — deleted from the repo in `36880cb6`,
   never deleted from the install (`build.ps1` deploy is additive-only; no prune step exists).
4. **Registration surface (process-lifetime `MBObjectManager` residents):** 3,297 armory items · 1,260 action sets /
   37,708 actions (12× vanilla) · 140 skins / 14 races (14× vanilla) · 70 monsters. Only 6 distinct skeletons —
   skeleton count is cheap; the action-binding and skin surface is the unquantified part.
5. **Pack granularity:** TAOM_Map ships 6 monolithic tpacs (up to 4.4 GB) vs Native's 150 small ones — coarse paging.
   Caveat: the "one-package-at-a-time" claim in `docs/ai-includes/troll-race-arp-retargeting-workflow.md:100-111`
   describes **TpacTool's** AssetManager, not the engine — engine paging granularity is a Phase-2 measurement, not a known.

### Install-weight-only (release-packaging targets, pending Q1 for RDC)

| Item | Size |
|---|---|
| `AssetSources/` across TAOM modules (editor-only per `docs/reference/worldmap-battle-scene-grid.md:203`) | **53.0 GB** |
| `RuntimeDataCache/` across TAOM modules | **42.2 GB** |
| `TAOM_Map/EmAssetPackages/` (editor-mode packs) | 12.0 GB |
| `LOTRLOME_Armory/Assets/Race Test/` | 985.6 MB |
| FactionMap orphan sprites in install | 545.7 MB |
| Parked naval art (`TAOM_Map` ships/fishing-boat sources + geo tpacs) | ~185 MB |
| `TAOM_Map/Prefabs_Unused/` | 54.8 MB |
| `TAOM.NativeSkinFixes.pdb` ×3 + `.exp`/`.lib` (parked feature) | ~25 MB |
| Stale `.bak` XMLs inside ModuleData (glob-loaded → duplicate Monster registrations) | ~350 KB (load-surface hazard, not size) |

No release-packaging script exists — `build.ps1` deploys everything, deletes nothing.

## Runbook

### Phase 0 — ledger (this doc) — DONE 2026-08-05
Remaining Phase-0 action: **ask Levi** for `dir` listings of his `Modules\TAOM*\RuntimeDataCache` and `AssetSources`
folders (does a tester install even carry them?).

### Phase 1 — settle Q1 (RuntimeDataCache), ~1 h on Mike's machine, DECISIVE
1. Procmon: filter `Path contains RuntimeDataCache`, launch shipping client → menu → one custom battle.
   `ReadFile` on `.rdc` = client reads it; `.rtemp` creation = client writes it; zero ops = editor-only.
2. Rename A/B: `RuntimeDataCache` → `RuntimeDataCache.OFF` in TAOM, TAOM_Map, LOTRLOME_Armory; relaunch; record
   (a) rgl_log for "RDC cache path is not valid" / "Unable to decompress data", (b) load-time delta, (c) folder
   regeneration, (d) `[MemSample]` commit numbers at menu/battle. Restore after.
3. Decision rule: reads+fine → exclude from release, measure first-run rebuild cost · reads+broken → client depends
   on it, escalate to repack · no reads → editor-only, exclude unconditionally.

**Result (fill in):** _pending_

### Phase 2 — commit attribution matrix, ~2–3 h in-game on Mike's machine
Configs {A vanilla-only · B TAOM-full · C TAOM+lever-under-test} × stations {menu 60 s · campaign map (fixed save)
60 s · 250v250 custom battle, 2 min mid-fight} × 2 runs. Numbers from `[MemSample]` lines (menu/campaign fallback:
`Get-Process` + `typeperf "\Memory\Committed Bytes" -sc 1`). **One VMMap snapshot per station for A and B**
(`vmmap.exe -accepteula <pid>`, save .mmp + CSV) — the named-mapped-files split is the measurement that most
tightens attribution. Key derived number: the **A-vs-B menu delta** (isolates AlwaysLoad atlases + registration
surface from battle content).
Optional instrument: a `TaomConsole` command dumping `TaleWorlds.Engine.Utilities.GetApplicationMemoryStatistics()` /
`GetNativeMemoryStatistics()` / `GetMemoryUsageOfCategory(i)` / `DumpGPUMemoryStatistics(path)` — engine-category
attribution of the private commit (route through TaomConsole per the console-command trap in CLAUDE.md).

**Results table (fill in):**

| Config | Station | privMB | wsMB | sysCommitUsedMB | notes |
|---|---|---|---|---|---|
| _pending_ | | | | | |

### Phase 3 — ranked levers (each becomes an issue AFTER Phases 1–2 put numbers behind it)

| # | Lever | Est. saving | Key risk / gate |
|---|---|---|---|
| L1 | Banner icons 1024²→256² (41 sheets → ~2) | **~2.4–2.5 GB commit** | Rebake via editor flow (`banner-icon-generation.md` — bare CLI silently no-ops); re-apply import flags; `tools/sync_sprite_bake.ps1`; restart law |
| L2 | Drop `<AlwaysLoad/>`: bannericons+career OFF; fonts KEEP; ui_loading verify-first; ui_taom after consumer enum | ~0.2 GB on top of L1 (overlaps) | Needs TAOM-side `UIResourceManager.LoadSpriteCategory`/`Unload` call sites; a missed consumer renders blank **silently** — in-game verify every touched screen |
| L3 | FactionMap texture cache + dispose on CC exit | 0.3–1.2 GB session-dependent | Verify release API via `taom-src path TaleWorlds.Engine.Texture` |
| L4 | Author `tools/package_release.ps1` (include-list; exclude RDC pending P1, AssetSources, Race Test, naval art, Prefabs_Unused, `.bak`, NSF pdbs) | install ≤ ~44 GB lighter | `.bak` exclusion changes the loaded XML set — gate with `validate_moduledata.py` + a load test |
| L5 | Delete 61 orphan install sprites + scoped deploy-prune step | install 546 MB | Prune scoped to TAOM-owned dirs only |
| L6 | Action-set / skins rationalization | unknown (0.1–0.5 GB?) | ONLY after the Phase-2 menu delta isolates it; #385 says skin/morph data is hot |
| L7 | TAOM_Map pack splitting (6 monolithic → many) | targets the 4 GB mapped ledger | High effort; VMMap decides first |

### Phase 4 — reachability sweep (offline)
Extend `tools/validate_mesh_refs.py` with `--bytes` (per-TOC payload sizes; referenced vs unreferenced MB per
module/pack; top-30 unreferenced; JSON + markdown) + an RDC-orphan GUID joiner. Scene-referenced meshes classified
"referenced-by-scene (unaudited)", never falsely unreferenced.

### Phase 5 — deliverables
Umbrella issue (acceptance target: mid-battle commit ≤ 14 GB @ the standard 250v250 scenario — hold the final number
until Phase 2), per-lever issues, results recorded here, CHANGELOG.

## Bottom line (bounds as of 2026-08-05, before measurement)

Statically-verified levers (L1/L2/L3) address **~2.7–4.5 GB of the 15.65 GB private commit**. Optimistic case takes
the #385 scenario 20.3 → ~15.8 GB — likely clearing the 16 GB-machine CTD class (the tester died with 8.5 GB system
headroom). The remaining ~10–12 GB is battle content (armory textures are already BC-compressed — no easy
multiplier), engine working set, and .NET heaps; only content rationalization touches it, and only with Phase-2/4
numbers in hand. The two measurements that most tighten this: one VMMap of TAOM-full mid-battle + the A-vs-B menu
delta — under an hour combined.
