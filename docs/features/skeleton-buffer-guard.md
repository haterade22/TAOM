# Skeleton buffer guard and watch

## Overview

The engine's skeleton draw path reserves per-frame room in two pools and never checks that the next reservation
fits. About 2,340 skeletons in view fill the first pool (65,536 entries); the second (262,144 entries of 64 bytes)
takes a reservation from every skeleton drawn, horses and wargs included (the call comes before the branch that lets
some skeletons skip the first pool). One reservation past the end of either freezes the battle for good. This feature does
two things, both on by default (Mike, 2026-10-08). The **guard** rewrites 13 bytes at each pool's reservation in
`TaleWorlds.Native.dll`, in memory, at the first main menu, so a skeleton that would not fit is given its pool's first
slots instead of running past the end. The **watch** reads both pools' fill during every mission and writes their peaks
and the guard's refusals to the log when the mission ends; when nothing guards the first pool it also shows one
on-screen warning at 90 percent. MCM: CrashReport page, group "Master", "Skeleton Buffer Guard" (restart needed) and
"Skeleton Buffer Watch".

Adapted from yotthani's VanillaTuning `frame-buffer-guard` and `frame-buffer-watch` (MIT, (c) 2026 yotthani), which
guard and watch the first pool. The second pool's guard is TAOM's own, after the 2026-10-08 review found the pool.
Provenance: [provenance-register.md](../reference/provenance-register.md), adoption record
[adopt-yotthani-2026-10-08.md](../reviews/adopt-yotthani-2026-10-08.md).

## Why This Exists

- **Vanilla behavior:** each skeleton drawn reserves entries in pool 2, and each one with a remap table in pool 1 as
  well, with one locked add on the buffer's fill counter (pool 1 at RVA `0x69D1C`, pool 2 at `0x6AF58` in v1.5.4; the
  pool-1 count is read per skeleton from a byte,
  28 for a vanilla human by yotthani's measurement). The block index is `fill >> 11` (pool 1) or `fill >> 13`
  (pool 2), and nothing compares it with 32, so the pointer slot of a 33rd block overlaps the buffer's own "block ready"
  bytes. Threads that wait on those bytes spin for ever. The battle stands still, the engine logs nothing, and the game
  does not crash. The layout and the evidence are in
  [mission-frame-threads-and-native-costs.md](../reference/engine/mission-frame-threads-and-native-costs.md) section 10.
- **TAOM requirement:** battles above the menu's 1,000 soldiers (another mod's battle size) and large sieges with melee
  near the camera can reach the limit, and TAOM adds skeletons of its own (a spider mount is one more, a mumak brings
  eight crew, and every horse, warg and creature mount fills the second pool).
- **Without this feature:** a freeze with no log line and no crash report, which a player can only end by killing the game.
- **Other pools:** the engine's per-frame allocator has at least eleven pools of this design, two more of them on the
  same draw path, and none checks that a reservation fits. TAOM guards the two that skeletons fill. yotthani's
  2,001-agent battle ran through with only the first pool guarded, so none of the others reached its end there.
  Section 10 of the engine doc lists them; a guard for another pool waits for a measurement (issue draft 9 of
  [adopt-yotthani-2026-10-08.md](../reviews/adopt-yotthani-2026-10-08.md)).

## The price

When the guard refuses a reservation, that skeleton is given its pool's first slots, which another skeleton of the same
frame already owns. Every frame in which the battle stays above a limit refuses the skeletons that do not fit, so
several figures can show wrong bones for as long as the battle stays that crowded (read from the decompile; the look in
game is unverified). This happens only where the battle would otherwise have frozen for good. Each refusal is counted
per pool, and the mission-end line reports the counts.

Two more effects come with a refusal, both inside the pool and both only in a frame that overflows. A reservation that
would fit can be refused too, while a bigger refused one has not yet taken its entries back. And slots near the end can
be given twice: when two refused reservations take their entries back in the other order, a later reservation can start
inside one already granted. yotthani's block for the first pool behaves the same; a compare-and-swap loop would close
both, at the cost of a longer block. Not verified: if the engine reset a buffer while a refused reservation was between
its add and its take-back, the take-back would leave the fill below zero, and the next reservation could pass the check
with an index past the end. The static reading found no such reset, and the first pool's upstream block has the same
exposure.

This is TAOM's first write to engine code, and it is byte-verified: nothing is written at a site unless its byte pattern
matches exactly once in the loaded module's code, and the 13 bytes at the site equal the engine's own (compared again,
inside the write). A build that only moves the functions is still patched; a build whose bytes changed gets nothing
written. The two code blocks have no unwind information, so a stack walk sampled while a thread is inside one of them
cannot unwind past it; the window is a few instructions long.

## Architecture

### Design Challenge

Each reservation sits inside a native function TAOM does not own (pool 1 in the 980-byte `FUN_180069aa0`, pool 2 in
`FUN_18006af30`, which the first calls at `0x69AFE` and three other functions call for the same pool), so there is no
Harmony target: each site is 13 bytes inside a
function, and the offsets move with every engine build. The fix has to find the bytes by pattern, prove they are the
expected ones, and leave the process exactly as it was on any failure.

### Solution Approach

`SkeletonBufferModule` (a `TaomFeatureModule`, no Harmony patch) installs the guard in `OnPhase(MainMenu)` and declares
the watch as a mission behavior.

**Why MainMenu.** It is the first point where MCM settings are readable (they are null during `OnSubModuleLoad`, see the
comment in `SubModule.cs`), and nothing that draws skeletons is running yet, so no thread can be inside the 13 bytes
while they change. The writes happen on the main thread, once.

**The install** (`SkeletonBufferGuardService.Install`). Global steps end in one `[SkeletonBuffer] guard OFF: <reason>`
line; each pool then installs or reports on its own, as `guard ON pool N: ...` or `guard OFF pool N: <reason>`:

| Step | Stands aside when |
|---|---|
| Dedicated server | always: nothing is drawn |
| Read the module's headers and its in-memory `.text` | the module is not loaded, or a read fails |
| Find the watch pattern | it matches zero or two times; the watch is then off too |
| The global it names | the target is not an aligned 8-byte slot inside `.data` |
| Per pool: the site already holds `E9 rel32` plus eight NOPs | another module guards it, in practice yotthani's VanillaTuning for pool 1, which patches at `OnSubModuleLoad`, before TAOM's main-menu phase (reported as such, even with the setting off; TAOM still guards the other pool) |
| MCM "Skeleton Buffer Guard" | the setting is off |
| Pool 1: find its site pattern | zero or two matches, or it is not 1 to 128 bytes after the watch site (the same function) |
| Pool 2: find its site pattern | zero or two matches, the pool-2 load is not resolved, or no `E8 rel32` within `0x80` bytes after the pool-2 load enters the function at most `0x80` bytes before the site |
| Allocate two pages below the module, within a 32-bit jump of both resume points | none is free, or the address is out of reach |
| Write both code blocks, then make their page read-execute | either fails: pages freed, engine code untouched |
| Write site 1, then site 2 (13 bytes each), last | a site's bytes changed since the scan: that site is not written |

The pages are freed only when neither site was written. If a site write cannot be confirmed (the write failed and the
original bytes could not be read back, or the write threw), the install stops there, keeps the pages (the site may
already jump into them) and writes an ERROR line for that pool; a later site gets a `not attempted` line.

**The code blocks** (`SkeletonBufferCave`), both on the first page; the overflow counters on the second (pool 1 at +0,
pool 2 at +0x40), so the page with the code is made read-execute and never writable again. TAOM's change from
upstream: upstream keeps its counter in the code page, which therefore stays read-write-execute for the life of the
process.

- **Pool 1** (code page + 0, 43 bytes, yotthani's): repeats the engine's three instructions, compares the fill after
  with 65,536, and either jumps back or takes the reservation back (`lock sub`), counts the overflow and gives the
  skeleton the first slots (`esi = 0`).
- **Pool 2** (code page + 0x40, 42 bytes, TAOM's, the same shape): repeats `mov [rsp+20h], r15; mov r15d, edx;
  lock xadd [rcx], r15d`, then `lea eax, [rdx+r15]; cmp eax, 40000h; jbe` back; on an overflow `lock sub [rcx], edx`,
  counts it and sets `r15d = 0`, so `FUN_18006af30` returns start index 0 and the caller stores it.

**The watch** (`SkeletonBufferWatchService`, behavior `SkeletonBufferWatchMissionBehavior`) reads, each
`OnMissionTick`, the pointer in the engine global and the fill counters behind it (the two buffers of each pool, the
fuller one counts), through `ReadProcessMemory` (which fails instead of crashing). It keeps each pool's peak, with the
agent count and the second into the mission at the first pool's peak. At `OnEndMission` or `OnRemoveBehavior`, once, it
writes one line, for example:

```
[SkeletonBuffer] mission peak: 41230 of 65536 entries (62.9 %), about 1472 skeletons, 1650 agents, 38.2 s into the mission; guard overflows this mission: 0; pool 2 peak: 120400 of 262144 entries (45.9 %); guard overflows this mission: 0
```

Each pool's note is `guard overflows this mission: N`, `guard overflow count unreadable`, `no guard installed` or
`guarded by another module`. A pool at or above 90 percent carries `, over 90 %`. A nonzero overflow count in either
pool, or a pool at or above 90 percent that nothing guards, makes the line a WARNING. The warning on screen (`taom_skeleton_buffer_warning`, once per mission, at 90 percent of the first
pool) is shown only when the first pool is not guarded, and never on a dedicated server.

### Component Diagram

```
SkeletonBufferModule
  MainMenu  -> ISkeletonBufferGuardService.Install
  missions  -> SkeletonBufferWatchMissionBehavior (thin)
        |                          |
SkeletonBufferGuardService   SkeletonBufferWatchService
  Signature (pure)             WatchState (pure) + Lines (pure)
  Cave (pure, both blocks)            |
        \                            /
  ISkeletonBufferMemoryAdapter   ISkeletonBufferEngineAdapter   ISkeletonBufferSettingsProvider
  (kernel32: the only native      (the one on-screen line)        (CrashReportSettings.Instance)
   write in TAOM)
```

## Configuration

MCM, CrashReport page, group "Master", after "Survive Mission Start Failures":

| Setting | Default | Restart | Read |
|---|---|---|---|
| Skeleton Buffer Guard | on | yes | once, at the first main menu; covers both pools |
| Skeleton Buffer Watch | on | no | at the start of each mission |

Both fall back to on when MCM has no instance. Both are excluded from the co-op settings fingerprint in
`CoopSettingsRelevance`, the guard as presentation (where the engine writes skeleton data) and the watch as
instrumentation: neither changes what is simulated.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/SkeletonBuffer/SkeletonBufferModule.cs` | Registrations, install at MainMenu, the watch behavior declaration |
| `Main/Features/SkeletonBuffer/SkeletonBufferSignature.cs` | The byte patterns (the watch load, the pool-2 load, both sites, both foreign shapes) and their resolution (parse and search reuse `ClipBudgetSignature`) |
| `Main/Features/SkeletonBuffer/SkeletonBufferCave.cs` | Both code blocks and both site jumps, as bytes for given addresses |
| `Main/Features/SkeletonBuffer/SkeletonBufferGuardService.cs` | The install decisions and their order, per pool |
| `Main/Features/SkeletonBuffer/SkeletonBufferWatchService.cs` | Per-mission reading, peak line, warning |
| `Main/Features/SkeletonBuffer/SkeletonBufferWatchState.cs` | Peak tracking and the 90 percent level |
| `Main/Features/SkeletonBuffer/SkeletonBufferLines.cs` | Every log line's wording |
| `Main/Features/SkeletonBuffer/Hooks/SkeletonBufferWatchMissionBehavior.cs` | Start, tick, end |
| `Main/Adapters/SkeletonBufferMemoryAdapter.cs` | kernel32 reads, allocation, the code writes (the only file that may call `VirtualProtect`) |
| `Main/Adapters/SkeletonBufferEngineAdapter.cs` | The localized warning |
| `Main/Features/CrashReport/CrashReportSettings.cs` | The two toggles |

## Dependencies

- `ISkeletonBufferMemoryAdapter` and `ISkeletonBufferEngineAdapter` (Adapters).
- `ClipBudgetSignature` and `PeSectionTable` (AnimMemory): pattern parsing and search, and the PE section table.
- `IDedicatedServerProvider` (CoopInterop): no warning and no install on a dedicated server.
- `IModLogger`: `taom_debug.log`.

## Tests

`TAOM.Tests/Features/SkeletonBuffer/` (235 tests):

- `SkeletonBufferSignatureTests` (30): zero, one and two matches for every pattern, both foreign shapes, wildcards, the
  pool-2 load's same-global check, the pool-2 site's call link, and a call whose 32-bit target would wrap.
- `SkeletonBufferCaveTests` (19): golden bytes of both blocks at fixed addresses, jump and counter range edges.
- `SkeletonBufferGuardServiceTests` (58): every reason line, every combination of the two sites' outcomes (a write
  that throws included, reported per pool), the order (blocks, protect, site 1, site 2), page release only when neither
  site was written.
- `SkeletonBufferWatchStateTests` (17), `SkeletonBufferWatchServiceTests` (58) and `SkeletonBufferLinesTests` (16): both
  pools' peaks and 90 percent markers, both overflow deltas, the WARNING level (a pool at 90 percent warns only while
  nothing guards it), the on-screen warning, the read-failure lines.
- `SkeletonBufferWatchMissionBehaviorTests` (5): `AfterStart` begins the watch, `OnMissionTick` reads, the end paths.
- `SkeletonBufferMemoryAdapterTests` (17): the real adapter against this process, including both code blocks EXECUTED
  through a thunk: a reservation that fits returns the old fill, one ending exactly at the capacity is granted, one past
  it is taken back, counted and given slot 0, and a smaller one still fits after a refusal; for pool 2 also that `r15`
  is saved to `[rsp+20h]` and `rcx` and `edx` are unchanged.
- `SkeletonBufferInstalledBinaryTests` (2, `LiveInstall`): the patterns against the installed `TaleWorlds.Native.dll`
  file, exactly one match each (pool 1's site at RVA `0x69D14`, pool 2's at `0x6AF50`, the pool-2 load at `0x69AD5`),
  no foreign shape, and the global at RVA `0xD9D160`.
- `SkeletonBufferWiringTests` (13): module list, behavior shape, settings, and that native write calls exist in one file
  only (with a floor: the scan must match the adapter itself).

The first pool's RED step was recovered by mutation on 2026-10-08 (those tests were written first but first run green):
a changed `jbe` byte and a page release on the unknown write state each failed the expected tests (the same pass's two
nameplate-cull mutations are in [nameplate-cull.md](nameplate-cull.md)). The second pool's tests ran RED against
declaration-only stubs first (68 of 226 failing), and seven mutations of its code each failed tests.

## After an engine bump

`SkeletonBufferInstalledBinaryTests` goes Inconclusive for a build missing from its `KnownBuilds` table (a test-only
pin; the game itself scans by pattern). Read the `[SkeletonBuffer] guard` lines of the first start: `guard ON pool 1`
and `guard ON pool 2` mean both patterns still match exactly once; `guard OFF` with a pattern reason means players lost
the freeze protection for that pool with this build. Re-derive with `python tools/native_decompile.py --rva 0x69D1C` and
`--rva 0x6AF58` (the reservations' addresses in v1.5.4) and check section 10 of the engine doc still holds (layout,
entries per skeleton, 32 blocks, both pools), then add the build's file length and RVAs to `KnownBuilds`. Do not use
`tools/native_sig_author.py xref` to count references: its single capstone sweep stops at the first byte it cannot
decode (2.8 % of `.text` on v1.5.4).

## Performance

- **The watch:** one 8-byte `ReadProcessMemory` of the engine pointer plus two 4-byte counter reads per pool, per
  mission tick, no allocation; 0.80 microseconds per tick for the first pool, measured with the adapter's P/Invoke
  shapes on the desktop (2026-10-08 review), about 0.26 more per read for the second pool.
- **The guard:** each code block adds two jumps and a compare to its pool's reservations. The counters are on their own
  page and written only on an overflow, so render threads do not contend on them.
- **The install:** six pattern scans of the 10.6 MB `.text` once per launch at the first main menu, 152 to 173 ms in
  all in TAOM's Debug build (the 2026-10-08 review's bench of the same loop; a first-byte prefilter would cut it to
  about 50 ms, issue draft 10 of the adoption record).

## In-game check

Not yet done in game. Start the game with the default settings and read `taom_debug.log`: two lines at the main menu,
`[SkeletonBuffer] guard ON pool 1: site=0x69D14 resume=0x69D21 cave=0x... counter=0x... scanMs=...` and
`[SkeletonBuffer] guard ON pool 2: site=0x6AF50 resume=0x6AF5D ...`. Run a large custom battle and leave it: one
`[SkeletonBuffer] mission peak:` line per mission, with both pools. To see a refusal, raise the battle size with a mod
until the peak line reports overflows above zero in either pool.

## Changelog

- 2026-10-08: added, adapted from yotthani's VanillaTuning, with TAOM's two-page layout and the executed-code-block
  test; the second pool's guard added the same day (TAOM's own block, Mike's decision after the review found the pool).

## GitHub Issue

- **Issue:** #776 (draft item 2 in [adopt-yotthani-2026-10-08.md](../reviews/adopt-yotthani-2026-10-08.md)).
- **Status:** built and unit-tested 2026-10-08; in-game check owed.
