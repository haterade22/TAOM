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
| `AssetSources/` across TAOM modules (editor-only per `docs/reference/worldmap-battle-scene-grid.md:203`) | **51.8 GB** (re-measured 2026-08-10, incl. `Alliance.Wargs`) |
| `RuntimeDataCache/` across TAOM modules | **41.1 GB** (re-measured 2026-08-10, incl. `Alliance.Wargs`) |
| ~~`TAOM_Map/EmAssetPackages/` (editor-mode packs)~~ | 11.7 GB — **NOT an exclusion candidate, see correction below** |
| `LOTRLOME_Armory/Assets/Race Test/` | 987.8 MB |
| ~~FactionMap orphan sprites in install~~ | **56.7 MB**, not 545.7 MB — the L3a cap (2026-08-08) already shrank this. The old number predates the fix; do not quote it |
| Parked naval art (`TAOM_Map` ships/fishing-boat sources + geo tpacs) | ~185 MB |
| `TAOM_Map/Prefabs_Unused/` | 54.8 MB |
| `TAOM.NativeSkinFixes.pdb` ×3 + `.exp`/`.lib` (parked feature) | ~25 MB |
| ~~Stale `.bak` XMLs inside ModuleData (glob-loaded → duplicate Monster registrations)~~ | **RESOLVED 2026-09-01, and the premise was half wrong.** The engine globs `GetFiles("*.xml")`, so what decides the hazard is the LAST extension, not the presence of `.bak`: a sidecar named `foo.xml.bak-topic` is never loaded (the CHANGELOG lesson at the 2026-08-10 harness entry is right), while one named `foo.bak.xml` is parsed as real data and duplicates every id in it. All 781 sidecars across the three modules were swept to quarantine by `tools/sweep_module_backups.ps1`, which asserts no swept file ends in a real game extension. 937 MB recovered, 40 MB of it `ModuleData` |

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

#### Runbook — Phase 1, step by step

**P0. Prerequisites (once, before any run).**

```powershell
# Sysinternals: check first, install only if absent.
Get-Command vmmap64.exe, procmon64.exe, handle64.exe -ErrorAction SilentlyContinue
winget install --id Microsoft.Sysinternals.VMMap            --accept-source-agreements --accept-package-agreements
winget install --id Microsoft.Sysinternals.ProcessMonitor   --accept-source-agreements --accept-package-agreements
# Fallback if the winget ids drift: https://download.sysinternals.com/files/SysinternalsSuite.zip -> C:\Sysinternals
# CONFIRM the CLI form before trusting it — it is NOT verified here:
vmmap64.exe -?      # check the [-p <pid>] [outputfile] shape and which extensions it accepts
```

`cheat_mode = 1` must be set in `Configs\engine_config.txt` for `taom.print_memory`. Run the game
**windowed/borderless** for the whole matrix — VMMap and Procmon need alt-tab. Set the module set in
the **launcher UI** (never hand-edit; the launcher rewrites the file), then snapshot it per config:

```powershell
$cfg = "$env:USERPROFILE\OneDrive\Documents\Mount and Blade II Bannerlord\Configs"
Copy-Item "$cfg\LauncherData.xml" "$cfg\LauncherData.CONFIG_A.xml"   # after selecting config A
```

**Step 1.1 — Procmon capture (config B).**

1. `Procmon64.exe /AcceptEula`. **Ctrl+E** to stop capture, **Ctrl+X** to clear.
2. **Ctrl+L** → Filter. Add (all Include): `Process Name` begins with `Bannerlord`; `Path` contains
   `RuntimeDataCache`; `Operation` is `CreateFile`; `Operation` is `ReadFile`; `Operation` is `WriteFile`.
3. Toolbar: **File System Activity only** (Registry / Network / Process / Profiling off).
4. **Ctrl+E** to start. Launch config B → main menu → **one 250v250 custom battle** → quit.
5. **Ctrl+E** to stop. `File > Save… > All events > CSV` → `Desktop\rdc-procmon.csv`.

| Observation | Conclusion |
|---|---|
| `ReadFile` on any `*.rdc` | the shipping client **reads** RDC |
| `CreateFile` on `*.rtemp`, or any `WriteFile` under `RuntimeDataCache` | the client **regenerates** it |
| **Zero** operations | RDC is editor-only for this client |

Corroborate while the battle is loaded: `handle64.exe -accepteula -p Bannerlord RuntimeDataCache`.

**Step 1.2 — the decisive A/B rename (config B).** With the game **closed**:

```powershell
$m = "E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules"
foreach ($mod in 'TAOM','TAOM_Map','LOTRLOME_Armory') {
  if (Test-Path "$m\$mod\RuntimeDataCache") { Rename-Item "$m\$mod\RuntimeDataCache" 'RuntimeDataCache.OFF' }
}
```

Launch → menu (60 s) → the standard 250v250 custom battle → quit. Record **(a)** the newest
`…\Mount and Blade II Bannerlord\Logs\rgl_log_errors_*.txt`, grepped for `RDC cache path is not valid`,
`Unable to decompress data`, `partial read on compressed asset`; **(b)** menu→playable time from
`[BattleLoad] t=+` on `BattlePlayable`, **and which bucket moved** (see the phase-load correlation in
Phase 2); **(c)** whether any `RuntimeDataCache` folder regenerated; **(d)** `[MemSample]` at menu and
mid-battle plus `taom.print_memory rdcoff` mid-battle. Restore with the inverse rename.

| Result | Conclusion | Action |
|---|---|---|
| reads present, OFF run fine (no new rgl errors, load time within ~10 %) | client reads it but does not need it | exclude from release; measure first-run rebuild cost |
| reads present, OFF run broken (rgl errors, missing assets, or load time blows up) | client **depends** on it | escalate to repack; RDC stays in the release |
| zero reads, OFF run identical | editor-only | **exclude unconditionally — 42.2 GB off the install** |
| zero reads but OFF run breaks | contradiction — the reads went through a path the filter missed | re-run Procmon filtering on process name ONLY |

**Result — 2026-08-08, step 1.1 run. Q1 IS SETTLED: the shipping client READS RuntimeDataCache.**

Procmon capture (Mike's machine, config B, shipping client, launch → menu → one battle → quit),
exported with the `Path contains RuntimeDataCache` filter applied:

| Metric | Value |
|---|---:|
| RDC operations | **23,329** |
| `ReadFile` | **13,795** |
| `CreateFile` | 5,281 |
| `CloseFile` | 4,253 |
| `WriteFile` / `*.rtemp` | **0** |
| Distinct `.rdc` files touched | **5,036** |
| Result `SUCCESS` / `NAME NOT FOUND` | 22,386 / 943 |

Per module: TAOM_Map 13,577 · LOTRLOME_Armory 7,658 · TAOM 1,801 · **`Alliance.Wargs` 293**.

**So the row that applies is "reads present" — NOT the 42.2 GB free win.** RDC cannot simply be
excluded on the strength of "no vanilla module ships one". Three consequences:

1. **Step 1.2 (the OFF rename) is still required** to decide between "reads it but does not need it"
   and "depends on it". It has NOT been run.
2. **943 `NAME NOT FOUND` results mean the client already probes for `.rdc` entries that do not
   exist and carries on.** That is a graceful-fallback signature, so the OFF run will most likely
   boot and degrade rather than break — but degrade *by how much* is the open number, and it is a
   per-load cost paid by every player forever if the folder ships absent and never regenerates
   (zero writes were observed, so there is no evidence it rebuilds itself).
3. **`Alliance.Wargs` ships RDC too** and is not in the install-weight ledger above. Re-measure the
   42.2 GB total including it before quoting a download-size saving.

**Priority note:** RDC is a *disk* cache. The 2026-08-08 battle-load measurement (see
`docs/features/battle-load-diagnostics.md`) showed load time is bound by *physical memory* — 97 %
memLoad and page eviction, not disk. Removing RDC plausibly makes loading **worse** while doing
nothing for commit. Treat this as a release-packaging question, not a performance one.

**Method caveat worth repeating:** the first capture attempt produced zero RDC rows because the
capture was started and stopped without the game ever running. A mis-scoped or mis-timed capture is
indistinguishable from "editor-only" on the RDC row count alone. **Always corroborate that Bannerlord
appears in the capture** (or that a fresh `taom_debug_*.log` covers the window) before reading a zero
as an answer. `tools/`-adjacent helper used for this run: `phase1-rdc.ps1` / `phase1-analyze.py`
(session scratchpad; promote to `tools/` if Phase 1 is ever re-run).

**Update — 2026-08-10. Two facts from the installed binaries narrow the question further, and one
kills an exclusion candidate.**

*(a) The client can never rebuild RDC — the engine says so.* Diffing the RDC string surface between
`Win64_Shipping_Client\TaleWorlds.Native.dll` and `Win64_Shipping_wEditor\TaleWorlds.Native.dll`: the
client carries only `RuntimeDataCache`, `RDC0`, `RDC cache path is not valid`, `rglAsset_package_link_wrdc`
and the partial-read warning. **Every** write/generate string is editor-build-only — `Unable to write RDC
of texture %s`, `…for mesh %s`, `…for animation clip %s`, `Unable to delete cloth cook data from RDC`,
`Out of date RDC file found. RDC packing is disabled in test mode`, and the decisive one:

> `External .rdc file modification detected. RDC files cannot be updated outside the editor. Please restart the game`

So step 1.1's zero writes were not a warm-cache artifact, and the runbook's "does it regenerate?"
observation row drops to a **falsification check**: if a folder ever does reappear, this string
evidence is wrong and the verdict needs re-deriving. Corollary for release: ship without RDC and
whatever the fallback path costs is paid on every load, forever.

*(b) Vanilla is the control, and it ships none.* `Modules\Native` carries **1,188 tpacs / 44.71 GB and
zero `.rdc`**, and retail runs. `Native\flora.tpac` and `TAOM_Map\pack0.tpac` share an identical TPAC v2
header, so TAOM's editor-published packages are not a variant that hard-links to the cache. The no-RDC
path is the *normal* retail path; what step 1.2 still has to measure is its cost, not its existence.

*(c) Correction — `EmAssetPackages` is NOT editor-only, and was never evidence-checked.* The row above
called it "editor-mode packs". **Vanilla `Modules\Native` ships 26.36 GB of `EmAssetPackages`**
(measured 2026-08-10), which is not what an editor-only directory looks like. It is demoted from
exclusion to *candidate*: it needs its own Procmon/A-B pass before anyone drops 11.7 GB of it.
Same check cleared `SceneEditData` and `SceneObj` — vanilla ships both (Native: 0.19 GB / 1.1 GB),
so they belong in a public build.

*(c, resolved 2026-08-29 by the maintainer.)* The reasoning above was backwards. `AssetPackages` is
the cooked form given to **players**; `EmAssetPackages` is the cooked form given to **other modders**
who want to build against the module in the Modding Kit without receiving its `AssetSources`.
Vanilla shipping 26.36 GB of it is therefore evidence **for** the editor-distribution reading, not
against it: `Native` is the module every modder opens in the Kit. Neither folder is read on a dev
install, where the editor and the game both load `Assets/`. That makes `EmAssetPackages` a genuine
exclusion candidate for a **player** build, and the 11.74 GB is a real saving, but it is a
release-shape decision (a modder build would be a separate artifact) and `package_release.py` is
unchanged pending it. Folder semantics:
[bannerlord-engine-and-toolchain.md](../reference/bannerlord-engine-and-toolchain.md) section 6.1.

**Two more instruments shipped 2026-09-01, both aimed at Phase 2:**

- `tools/Invoke-CommitMatrix.ps1` stamps, via `-Station <label>`, one CSV row per station, `-Vmmap` snapshots at the same instant, `-Report` prints per-station deltas with FRESH/STALE stamps against a cutoff. It **refuses to write a row when no Bannerlord process is running**, recording `UNMEASURED`, because an invented zero cannot afterwards be told from a real reading; and its label grammar mirrors `MemoryProbeReportFormatter.IsValidLabel`, so a PowerShell row and a `taom.print_memory <label>` line join on one token.
- `[MemStation]` screen anchors (see [battle-load-diagnostics.md](../features/battle-load-diagnostics.md)): one line per screen open/close, so the UI stations below self-record without the operator stamping each one. This is what makes the pass/repeat/close-and-wait shape measurable at all.

**Tooling now exists for both halves** (2026-08-10):

- `tools/Invoke-RdcAbTest.ps1` — `-Status` / `-Off` / `-On` / `-Report`. Renames only, never deletes,
  refuses to run while Bannerlord is open, rolls back on partial failure, and `-Report` stamps every
  log artifact FRESH or STALE against a `-SinceMinutes` cutoff so the method caveat above cannot
  recur. It also extracts the four ON-vs-OFF comparison numbers directly.
- `tools/package_release.py` — the L4 packager (see Phase 3).

**RDC-ON baseline captured 2026-08-10** from `taom_debug_2026-08-10_10-23-55.log`, for the OFF run to
be compared against: load to `BattlePlayable` **67.4 s** on scene `battle_terrain_biome_040`;
`privMB` **12,799** at `FinishMissionLoadingDone`; peak `[MemSample]` `privMB` **16,979** over 78
samples; `memLoad` 56 %. **The OFF run must use this same scene or the comparison is not like-for-like.**

#### Step 1.2 — FIRST RUN, 2026-08-10. Partial. Verdict still OPEN.

Cache renamed `.OFF` on TAOM, TAOM_Map, LOTRLOME_Armory, Alliance.Wargs (41.13 GB absent).

| Observation | Result |
|---|---|
| Client boots to main menu | **yes** — TAOM loaded, `taom_debug_2026-08-10_13-23-05.log` written |
| `rgl_log_errors_*.txt` | **never created** — zero engine errors logged |
| Any `RuntimeDataCache` regenerated by the **client** | **no**, across every run |
| Campaign map station | **NOT RUN** |
| Battle station | **NOT RUN** |
| Load-time / commit comparison | **VOID** (see shader caveat) |

**Settled: the EDITOR requires RDC.** Launched against the renamed folders it asserts on
`C:\BuildAgent\work\mb3\TaleWorlds.Shared\Source\Base\FairyTale.Library\rglIntrusive_ptr.h:151`,
`Expression: px != nullptr`, and it regenerated **6 `.rdc` files into a fresh `RuntimeDataCache`
before dying** — the editor-only write surface demonstrating itself. This is the exact
"regeneration" event the falsification check was watching for, and it fired for the **editor**, never
the client, which is what the string evidence predicted. Developers keep the cache; only the public
build is in question.

**The confound that nearly produced a wrong verdict.** With the cache off, the main menu rendered
**black** — menu text and TAOM's font fine, module art gone, vanilla art fine, and *no log line*.
That matches the `ui_loading` failure chain in Phase 3's L2 note exactly (manifest resolves, texture
null, widget guards on the wrong thing), so it read as proof the client depends on RDC. **It was
not.** The cause was **stale compressed shader sacks**, invalidated when Steam moved the install to
v1.4.8 at **07:22 the same morning**; deleting them restored the art with the cache still absent.
Two variables were already flagged (missing cache; a `TAOM.dll` built minutes earlier from another
session's uncommitted `bool` prefix) and neither was it. **Rule for anyone re-running this: before
attributing a rendering fault to RDC, enumerate everything that changed on the machine that day —
an engine bump invalidates the shader cache, and that failure is silent too.**

**Blocking the timing half:** the shader sacks were deleted, so the next loads pay full shader
compilation. Comparing them to the 67.4 s baseline would charge that cost to RDC. Run
*Pre-compile Shaders*, or let two launches settle, **then** measure.

**Owed to close Phase 1:** campaign map (TAOM_Map is 13,577 of 23,329 ops — the menu only exercised
TAOM's 1,801), a 250v250 on `battle_terrain_biome_040` with warm shaders, and the cloth/animation
visual pass.

**Restore note.** `-On` refused because TAOM held both a regenerated `RuntimeDataCache` and the
`.OFF` original. All 6 regenerated GUIDs already existed among the original 98, so the partial was
renamed to `RuntimeDataCache.editor-partial-2026-08-10` rather than deleted, and the originals were
restored intact (98 / 2,817 / 4,061 / 166 files; 5.02 / 20.92 / 14.10 / 1.09 GB). `package_release.py`
now matches `RuntimeDataCache*` by prefix so any stray variant is excluded as cache rather than
blocking a release run as an unknown.

### Phase 2 — commit attribution matrix, ~2–3 h in-game on Mike's machine
Configs {A vanilla-only · B TAOM-full · C TAOM+lever-under-test} × stations {menu 60 s · campaign map (fixed save)
60 s · 250v250 custom battle, 2 min mid-fight} × 2 runs. Numbers from `[MemSample]` lines (menu/campaign fallback:
`Get-Process` + `typeperf "\Memory\Committed Bytes" -sc 1`). **One VMMap snapshot per station for A and B**
(`vmmap.exe -accepteula <pid>`, save .mmp + CSV) — the named-mapped-files split is the measurement that most
tightens attribution. Key derived number: the **A-vs-B menu delta** (isolates AlwaysLoad atlases + registration
surface from battle content).
**The instrument now exists: `taom.print_memory [label] [gpu]`** (Tier A, cheat gate; shipped
2026-08-07). It reads `GetApplicationMemoryStatistics()`, `GetNativeMemoryStatistics()`,
`GetCurrentEstimatedGPUMemoryCostMB()` and — only with the `gpu` keyword —
`DumpGPUMemoryStatistics(path)`. Its output is also mirrored into `taom_debug` under `[MemProbe]`, a
tag `tools/triage_battle_load.py` deliberately does not parse, so a matrix run is self-recording.

`GetMemoryUsageOfCategory(int)` is **deliberately NOT called.** There is no category-count API, no
category-name API, no enum, and no managed caller in either the shipping or the editor build; the
index goes straight to native with no validation, so a blind index walk is an access-violation risk
inside a diagnostic. Step 2.0 below settles empirically whether a numeric probe is needed at all.

#### Runbook — Phase 2, step by step

**Step 2.0 — settle the category question first (5 min, config B).** At the campaign map run
`taom.print_memory step0` and read the `application:` / `native:` blocks.

- They contain a category breakdown ⇒ **a numeric probe is not needed. Do not build it.** Record the
  category names here.
- They render `<unavailable>` (native reported failure) or carry no breakdown ⇒ **DO NOT build the
  numeric probe.** This branch previously prescribed an opt-in `taom.print_memory_categories <n>`
  walking a clamped index. **Corrected 2026-09-01:** in
  `_shipping_build_v1.4.8/TaleWorlds.Engine.cs:5760-5764`, `GetMemoryUsageOfCategory(int)` is
  declared immediately after `RegisterGPUAllocationGroup(string name) -> int`. The index space is
  therefore almost certainly **GPU allocation-group ids handed out by that registrar**, not a native
  heap taxonomy, so a walk would be both an unvalidated index into native (the AV risk this document
  already flags) *and* semantically wrong: it would attribute nothing that pre-existed our own
  registration. Two cheaper things instead:
    1. **Try the engine's own console command.** The shipping `TaleWorlds.Native.dll` string table
       carries `show_memory` beside a `%s Memory Usage: %s` / `OS Free Memory: %%%.3f` formatter,
       i.e. a name/value breakdown. Start typing `show_mem` and read what the console autocompletes.
       **Unverified: read out of the string table, never run.** Budget 60 seconds.
    2. **Read the GPU dump you already have.** `taom.print_memory <label> gpu` calls
       `DumpGPUMemoryStatistics`, whose schema (`gpu_total_memory`, `gpu_texture2d_render_target_memory`,
       `gpu_texture2d_shader_resource_memory`, `gpu_buffer_vertex_buffer_memory`, ...) *is* a real
       category breakdown. GPU-side, so it answers a different question than commit, but if the
       climb is GPU-resource-backed it names the class. Take it at three points, not more: it writes
       a file and is not free.

**Configs.** Everything not listed is **off** — this install also carries `ADOD_Beasts`,
`Alliance.Wargs`, `DOTS`, `ServeAsSoldier`, `BattleLinkMPClient`, `FastMode`, `Palantir.Debugger`,
`BirthAndDeath`, `NavalDLC`, `TAOM_Online`; any one of them left on invalidates the delta.

| Config | Modules ON |
|---|---|
| **A** vanilla | `Native`, `SandBoxCore`, `Sandbox`, `StoryMode`, `CustomBattle` |
| **A+** vanilla + armory | A **+** `LOTRLOME_Armory` — isolates the 3,297-item / 1,260-action-set / 140-skin registration surface |
| **B** TAOM-full | A **+** `TAOM.Dependencies`, `LOTRLOME_Armory`, `TAOM_Map`, `TAOM` (dependencies before TAOM) |

**Stations** — identical every run, or the deltas are noise. **menu:** cold launch → main menu →
wait 60 s untouched. **map:** load the same fixed save → sit unpaused 60 s, camera untouched.
**battle:** Custom Battle, same scene, 250v250, same two cultures, same season/time-of-day → 2 min
into the fight, camera on the melee.

Matrix: {A, A+, B} × {menu, map, battle} × 2 runs. Config A has no TAOM instrumentation, so use the
`Get-Station` fallback for every A cell; A+ and B use `[MemSample]` + `taom.print_memory <label>` as
primary with `Get-Station` as a cross-check (they should agree within a few MB — a large
disagreement means the wrong process was read).

```powershell
function Get-Station($label) {
  $p  = Get-Process Bannerlord -ErrorAction SilentlyContinue | Select-Object -First 1
  $os = Get-CimInstance Win32_OperatingSystem
  [pscustomobject]@{
    label = $label; ts = (Get-Date -Format 'HH:mm:ss'); pid = $p.Id
    privMB        = [int]($p.PrivateMemorySize64/1MB)
    wsMB          = [int]($p.WorkingSet64/1MB)
    commitUsedMB  = [int](($os.TotalVirtualMemorySize - $os.FreeVirtualMemory)/1KB)
    commitLimitMB = [int]($os.TotalVirtualMemorySize/1KB)
  }
}
# Get-Station 'B-battle-2min' | Tee-Object -Append -FilePath "$env:USERPROFILE\Desktop\commit-matrix.csv"
```

> **A's map and battle stations are NOT like-for-like with B's** — they need a vanilla save and the
> vanilla custom-battle scene (a TAOM save will not load). Record the vanilla scene/culture choices
> explicitly and label the A-vs-B battle comparison as different content. The **menu** delta *is*
> like-for-like, and it is the one this document calls decisive.

**VMMap — one snapshot per station for A and B (6 total).** Reach the station, let it settle,
alt-tab, then attach. If the CLI form differs from what `-?` reported, use the GUI
(`File > Attach to Process`, then `File > Save As` for both `.mmp` and `.csv`).

```powershell
$id = (Get-Process Bannerlord).Id
vmmap64.exe -accepteula -p $id "$env:USERPROFILE\Desktop\vmmap-B-battle.mmp"
vmmap64.exe -accepteula -p $id "$env:USERPROFILE\Desktop\vmmap-B-battle.csv"
```

From each snapshot record **Private Bytes**, **Mapped File** (committed), **Image**, **Shareable**,
**Heap**, **Managed Heap**, and the **top 15 rows of the Mapped-File view by size** — that last one
is the named-mapped-files split, and it names the offending tpac/atlas *by file*.

**Results table (fill in):**

| Config | Station | Run | privMB | wsMB | sysCommitUsedMB | VMMap privateMB | VMMap mappedMB | Top mapped file (MB) | GPU cost MB | notes |
|---|---|---|---|---|---|---|---|---|---|---|
| _pending_ | | | | | | | | | | |

**Derived numbers to compute and record:**

| Derived | Formula | What it proves / refutes |
|---|---|---|
| **A-vs-B menu delta** | `privMB(B,menu) − privMB(A,menu)` | isolates AlwaysLoad atlases + registration surface from all battle content. **≥ ~3 GB** confirms the banner-atlas hypothesis (L1/L2 are the right levers); **< ~1 GB** refutes it and re-points at battle content (L6/L7) |
| **A+-vs-A menu delta** | `privMB(A+,menu) − privMB(A,menu)` | the Armory's registration surface alone. Large ⇒ **L6 is a real lever**; near-zero ⇒ L6 is dead, drop it |
| **B-vs-A+ menu delta** | | what TAOM + TAOM_Map add on top of the Armory — the atlas/UI ledger specifically |
| **battle − menu, per config** | | per-battle content cost. Compare B's to the 20.3 GB #385 death: if `B,battle ≈ 15 GB` on a 32 GB machine, a 16 GB machine dies exactly as reported |
| **mapped vs private split** (VMMap, B, battle) | | mapped-dominant ⇒ **L7 pack splitting** (paging granularity, TAOM_Map's 6 monolithic tpacs); private-dominant ⇒ decoded/uncompressed residency ⇒ **L1/L2/L3**. This single number chooses between two very different multi-week workstreams |

**Phase-load correlation — the new load markers' payload.** For every config-B battle load, run
`python tools/triage_battle_load.py <newest taom_debug_*.log>` and record `bucket1Ms`
(MissionInitialize→MissionInitializeDone), `bucket2Ms` + `polls` + `waitMs`, `bucket3aMs`,
`bucket3bMs`, `bucket3cMs`, `bucket4Ms`, plus `privMB` at each of the four MemStats-bearing markers.

| Reading | Proves / refutes |
|---|---|
| bucket 1 dominant | the time is in native `InitializeMission` — scene/physics/terrain construction. Not agent equipment, not TAOM behaviors |
| bucket 2 dominant **and `polls=1`** | a **blocking native spin inside one frame** — the #352 `WaitForMeshesToBeLoaded` shape. Escalate to live stack sampling; it is a mesh/physics-body preload problem and it is TAOM-DATA driven |
| bucket 2 dominant **and `polls` ≈ `waitMs`/16** | genuine async streaming, main thread healthy. Correlate with the 1,163 "partial read on compressed asset data" lines ⇒ I/O / pack granularity ⇒ **L7**, and Phase 1's RDC answer becomes load-bearing |
| bucket 3b dominant | it is `Mission.AfterStart` — the AgentEquip burst. Already instrumented per-agent; read the existing equip stamps |
| bucket 3a or 3c dominant | warm-up ticks / `ResumeLoadingRenderings`. **Then and only then** split 3a further — deliberately not built now, because there is no managed seam between the two warm-up ticks and patching `Mission.Tick` would hook the hottest method in the game |
| **`privMB` rises steeply across the dominant bucket** | **memory and stall are ONE problem** — resource residency. L1/L2/L3 shrink load time as well as commit; give the umbrella issue a load-time acceptance criterion alongside the ≤ 14 GB one |
| **`privMB` flat across the dominant bucket** | **they are TWO problems.** The stall is I/O or CPU, not allocation. Do not expect L1/L2/L3 to fix load times — say so in the umbrella issue before anyone assumes otherwise |

> **Sanity gate before trusting any cell:** confirm the run's `taom_debug` contains a
> `[MemSample] session totalPhysMB=… sysCommitLimitMB=…` line **at the menu**. If it does not, the
> `MemoryPressureSampler.Start()` relocation did not ship and every menu cell is unmeasured.
>
> **Second gate:** confirm `polls=` on the `FinishMissionLoadingBegin` line is non-zero. `polls=0`
> means the `MissionState.TickLoading` binding failed — it does **not** mean there was no wait.

### Phase 3 — ranked levers (each becomes an issue AFTER Phases 1–2 put numbers behind it)

> **Status 2026-08-08: L1 and the FactionMap half of L3 are DONE and measured** — see the two rows
> below. They did not wait for Phase 2 because both were arithmetic on verified numbers (source
> pixels vs the widget's rendered size), not estimates needing a matrix. The rest still wait.

| # | Lever | Est. saving | Key risk / gate |
|---|---|---|---|
| L1 | Banner icons 1024²→256² (41 sheets → ~2) | **~2.4–2.5 GB commit** | Rebake via editor flow (`banner-icon-generation.md` — bare CLI silently no-ops); re-apply import flags; `tools/sync_sprite_bake.ps1`; restart law |
| **L1 — DONE 2026-08-08, in-game confirmed** | 369 sources 1024²→256², rebaked | **2,624 MB → 128 MB resident (−2.44 GB)** | Banner icons visually confirmed correct in game after the rebake + restart (Mike, 2026-08-08) — the manifest check below proves the bake landed, this proves the art survived it. Verified in BOTH manifests: `SpriteSheetCount` 41→**2**, every `SpritePart` **256×256**, 369 parts, `<AlwaysLoad/>` retained, `_tex.tpac` present. Target chosen from measurement, not guesswork: the widest consumer is 110 Gauntlet design units (`BannerEditor.xml`), Gauntlet scales by screen **height** vs a 1080 reference, so on the 5120×1440 test display it renders **147 px** and at 4K height 220 px — 256 covers both. Sigils are white-on-alpha (368/369 have RGB=255 everywhere), so a plain LANCZOS resize is exact; only `15009.png` carries colour and needed premultiplied resampling. **Left behind:** ~39 stale `ui_taom_bannericons_3..41` atlas PNGs + `_tex.tpac` in the install — harmless at runtime (`SpriteCategory.Load` loops `1..SpriteSheetCount`) but dead install weight; fold into L5. |
| **L3a — DONE 2026-08-08** | 35 oversized `faction_*.png` capped at 1024 on the long side | **2,474 MB → 78 MB decoded (−2.40 GB worst case)**; on disk 932 MB → 32 MB | These are **loose PNGs, NOT baked sprites** — `FactionImageWidget` builds the path itself and calls `EngineTexture.LoadTextureFromPath`, so no bake and no restart law applies (see `gui-sprite-system.md`). 34 files were 5504×3072 into a **429×240** design-unit widget (858×480 at 4K) — a 36× area oversample; 36 sibling files were already ≤340×200. Saving is a **worst case, not a flat win**: the textures load per faction viewed during character creation and are never released, so a player who loads a save pays 0 and one who browses all 72 paid the full 2.4 GB. |
| L2 | Drop `<AlwaysLoad/>`: bannericons+career OFF; fonts KEEP; **`ui_loading` DO NOT DROP** (see below); ui_taom after consumer enum | ~0.2 GB on top of L1 (overlaps) | Needs TAOM-side `UIResourceManager.LoadSpriteCategory`/`Unload` call sites; a missed consumer renders blank **silently** — in-game verify every touched screen |

> **`ui_loading` — REJECTED 2026-08-08, after a claim that it was the safest drop of the five.**
> A prior analysis held that `GauntletDefaultLoadingWindowManager` drives this category through
> `InitializePartialLoad`/`PartialLoadAtIndex`, so `<AlwaysLoad/>` defeated the engine's own
> one-image-at-a-time design and was actively harmful. **Both halves are false in the v1.4.7 dump:**
> `InitializePartialLoad` and `PartialLoadAtIndex` are referenced **only inside `SpriteCategory.cs`
> itself** — no caller exists anywhere — and the literal `"ui_loading"` appears **nowhere** in the
> decompile, so no engine code loads this category by name.
>
> Dropping `AlwaysLoad` here would therefore black out the loading screen, silently. The chain:
> `SpriteData.GetSprite` resolves from a flat dict built from the **manifest**, so it returns a
> non-null `Sprite` for an unloaded category; `SpritePart.Texture` returns **null** while
> `!category.IsLoaded`; and `LoadingWindowWidget.UpdateImage` guards on `sprite == null`, not on a
> null texture — so its `background_1` fallback never fires and it draws a textured-with-nothing
> sprite. No exception, no log line.
>
> **General rule this establishes for every remaining `AlwaysLoad` drop:** the failure mode is a
> silent blank, and the `Sprite`-level null check that looks like a guard is not one. A category may
> only lose `AlwaysLoad` once an explicit `LoadSpriteCategory` call is proven to run on every path
> that displays it — verified in game, not by inspection.
| L3b | FactionMap texture cache + dispose on CC exit | ~34 MB after L3a (was 0.3–1.2 GB) | The leak is real and verified — five `EngineTexture.LoadTextureFromPath` sites in `Main/Features/FactionMap/Widgets/`, and **zero** `Release`/`Dispose`/`Unload` calls anywhere in the feature, so every viewed faction stays resident for the process. L3a shrank the prize by ~97 %, so do this as correctness hygiene, not as a memory lever. `PolygonWidget`'s `_emblemSprite`/`_emblemLoaded` are **static** — reset them too or a second character creation in the same process draws from a released texture. |
| L3c | `region_*` map art sizing (47 files, ~640 MB decoded) | unquantified | **Deliberately excluded from L3a.** `PolygonWidget.cs:323` sizes these as `ScaledSuggestedWidth = _bboxW * parentW` against a `StretchToParent` container, so they scale with the whole map surface rather than a fixed panel, and `region_map_boundary.png` covers the entire map. Capping them at 1024 would visibly soften the culture-stage map. Needs its own per-region rendered-size analysis first. |
| **L4 — BUILT 2026-08-10** (`tools/package_release.py`, 27 unit tests) | Include-list packager: copies a dev install / publish output into a fresh destination, never deletes from source, so the editor keeps its RDC. Measured dry run over the 5-module release set: **147.72 GB → 54.73 GB, 92.99 GB dropped, 0 unrecognised entries.** Split: AssetSources 51.77 · RDC 41.12 · Prefabs_Unused 0.05 · NSF debug 0.03 · `.xml.bak` 0.01 · `.rtemp`/runtime-state ~0. | **92.99 GB** | Unknown top-level entries are reported and **fail the run** until `--allow-unknown` — so a new editor artifact cannot ride along silently and a needed folder cannot vanish silently. RDC exclusion prints a standing warning naming the unrun A/B; `--keep-rdc` is the pre-verdict safe default. `EmAssetPackages` (11.74 GB) and `Race Test` (0.96 GB) ship as **candidates** until proven. Still gate `.bak` with `validate_moduledata.py` + a load test of the packaged build. |
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

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/battle-load-diagnostics.md](../features/battle-load-diagnostics.md)
- [docs/features/gui-sprite-system.md](../features/gui-sprite-system.md)

<!-- backlinks-end -->
