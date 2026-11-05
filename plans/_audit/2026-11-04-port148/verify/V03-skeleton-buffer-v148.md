# V03: the ecaa67d26 stability guards on Bannerlord v1.4.8 (skeleton buffer, mission start, nameplate cull, map view release) plus the native pin gate (15e52d7af, c286e2393)

Verifier: read-only, adversarial. Run 2026-11-04-port148. Nothing was built, tested, checked out or written except this file.

## 0. Verdict

**PARTLY-CONFIRMED, high confidence.**

- The native half of the classifier's claim holds in full, and more strongly than it was stated. Every
  skeleton-buffer pattern matches exactly once in the installed v1.4.8 `TaleWorlds.Native.dll`, and no foreign
  shape matches. The KnownBuilds row the classifier gave is exact. Every intra-function distance equals all four
  1.5.x rows, and the host function is 980 bytes, the size the 1.5.x doc gives for v1.5.4. The instruction-level
  semantics the two caves depend on hold on v1.4.8.
- MissionStartGuard and MapViewRelease bind nothing that drifted on v1.4.8. For MissionStartGuard this was
  checked down to the installed IL.
- NameplateCull does NOT port unchanged. v1.4.8's `SettlementNameplateVM.RefreshDynamicProperties` computes a
  hideout's range from `ISpottable.IsSpotted`; the adapter mirrors v1.5.4's `Settlement.IsVisible`, and on v1.4.8
  the two can disagree. The pinned call-sequence test also names `Settlement.get_FerryTarget`, which v1.4.8 lacks.
- Three prerequisite notes from the classifier are wrong or incomplete, and two hidden prerequisites were missed
  (section 5).

## 1. Refs, tools, method

| Item | Value |
|---|---|
| 1.5.x ref | `origin/bannerlord-1.5.x` = `e45f6df2c` |
| 1.4.x ref | `origin/bannerlord-1.4.5` = `1b00d1acd` |
| Commits | `ecaa67d26` (2026-10-08, 133 files), `15e52d7af` and `c286e2393` (2026-10-15) |
| Later 1.5.x edits to the four guards' code | only `6d7e67f3f` (adds `[ClientOnly]` to the Patch104 class). `git diff ecaa67d26 origin/bannerlord-1.5.x` over the four feature folders and eight adapters: 3 files, 10 lines (that attribute, three KnownBuilds rows plus a tag, and a `RepoPaths.CSharpFiles` rewrite in one wiring test) |
| Native scan | `C:\Python314\python.exe -I` with pefile 2024.8.26 and capstone 5.0.7, scripts on stdin, the DLL opened read-only |
| Engine members | taom-src, which resolved every type below into `C:\Users\mikew\.taom-src\v1.4.8\`. The v1.4.8 dump `C:\Decompiled_Bannerlord\_categories_v1.4.8` (manifest: version v1.4.8, decompiled 2026-09-06 from this install). `ilspycmd 9.1.0.7988 -il` on the installed `TaleWorlds.MountAndBlade.dll`, to stdout |

## 2. Claims checked

| # | Claim (source) | Verdict | Where |
|---|---|---|---|
| C1 | Every SkeletonBufferSignature pattern matches once in the installed v1.4.8 DLL; no foreign shape (classifier on ecaa67d26 and 15e52d7af; FREEZE-THREADING-07; LOADING-HANG-06) | CONFIRMED (these are true counts over all of `.text`; the scan does not stop at 2) | 3.2 |
| C2 | `[14185944] = ("v1.4.8", 0x88681, 0xD96D18, 0x886B4, 0x88475, 0x898F0)` | CONFIRMED | 3.2 |
| C3 | Guard 0x33 after the watch site; pool-2 call at load + 0x29 enters 0x20 before the site | CONFIRMED; all distances equal every 1.5.x row | 3.3 |
| C4 | "byte-identical on v1.4.8" (LOADING-HANG-06), "engine code CONFIRMED identical" (FREEZE-THREADING-07) | PARTLY: matched bytes, struct layout, block sizes, function size, every offset and the caller count all match. No v1.5.x DLL exists on this laptop, so whole-function bytes were not compared | 3.3 |
| C5 | ClipBudget pattern matches once, load site 0x23AADF, "other row fields not derived" | CONFIRMED; the full row is derived in 3.5 | 3.5 |
| C6 | "The ClipBudget class needs the AnimMemory port" | REFUTED: it needs only `ClipBudgetSignature.cs` and `PeSectionTable.cs`, plus `SyntheticNativeImage.cs` for the unit tests | 5.3 |
| C7 | RepoPaths: "first added on 1.5.x in 35007dbb8; take that file only" | REFUTED: `35007dbb8:TAOM.Tests/Infrastructure/RepoPaths.cs` is 22 lines with `RepoPath` only. The ported tests call `RepoPaths.ReadSource(path, stripComments: true)`, which came in `a0db0558b` | 5.4 |
| C8 | 15e52d7af: the BindingVerification tag makes an unlisted build FAIL the gate | NOT TRUE on 1.4.x yet: mapping Inconclusive to a failure needs plan 008's `TAOM.Tests/binding-gate.runsettings` (`MapInconclusiveToFailed`), absent on 1.4.x (group in WP 133, wave 6) | 5.5 |
| C9 | The v1.4.8 reload loop: MissionState.cs:89-95, :221-233, `_missionInitializing = false` (:335) before `AfterStart` (:345); the six wrapped calls once each (Mission.cs:3807-3829) | CONFIRMED in the installed-DLL decompile and in the IL | 4.1 |
| C10 | NameplateCull: `SettlementNameplatesVM.Update`, `_mapCamera`, `_bindIsVisibleOnMap`, `_worldPos` exist on v1.4.8, so port it | Members CONFIRMED; port REFUTED as "unchanged" (vanilla body drift) | 4.2 |
| C11 | MapViewRelease: `ScreenLayer.OnLayerActiveStateChanged` and `SceneLayer.ClearRuntimeGPUMemory` exist | CONFIRMED, no drift | 4.3 |
| C12 | 1.4.x closer at the end of `OnMissionBehaviorInitialize`, no try (SubModule.cs:1757-1889) | CONFIRMED: the closer is at :1888-1890 and the method runs to :1911 | 4.1 |
| C13 | Patch103 and Patch104 are free on 1.4.x (highest is 88) | CONFIRMED: the highest `"PatchNN_` literal in 1.4.x `Main` is 88 | 6 |
| C14 | The AfterStart throw is swallowed by the Native2Managed shim finalizer (LOADING-HANG-03) | PARTLY: the innermost swallow is SaveShield's finalizer on `MissionState.FinishMissionLoading`. The batch-15 note named SaveShield, and sibling report V04 reached the same result independently | 4.1 |

## 3. Skeleton buffer: code inventory and the v1.4.8 scan

### 3.1 What the code uses (identical at ecaa67d26 and the 1.5.x tip)

Patterns, from `ecaa67d26:Main/Features/SkeletonBuffer/SkeletonBufferSignature.cs`:

| Name | Line | Bytes (`?` wildcard) |
|---|---|---|
| `WatchPattern` (load length 7, disp at +3) | :24, :26 | `48 8B 0D ? ? ? ? 48 81 C1 D0 09 00 00 48 63 81 50 02 00 00 48 69 E8 28 01 00 00` |
| `Pool2Pattern` (33 bytes, disp at +3) | :33, :35, :37 | `48 8B 15 ? ? ? ? 48 81 C2 28 0C 00 00 48 89 5C 24 50 48 63 82 50 02 00 00 48 69 D8 28 01 00 00` |
| `GuardPattern` (13 site bytes plus tail) | :40 | `41 8B F4 4C 89 7C 24 20 F0 0F C1 75 00 45 8D 7C 24 FF 8B C6 C1 E8 0B` |
| `ForeignGuardPattern` | :43 | `E9 ? ? ? ? 90 90 90 90 90 90 90 90 45 8D 7C 24 FF 8B C6 C1 E8 0B` |
| `Pool2GuardPattern` | :50 | `4C 89 7C 24 20 44 8B FA F0 44 0F C1 39 8D 7A FF 41 8B C7 C1 E8 0D` |
| `Pool2ForeignGuardPattern` | :53 | `E9 ? ? ? ? 90 90 90 90 90 90 90 90 8D 7A FF 41 8B C7 C1 E8 0D` |
| `ClipBudgetSignature.Pattern` (`ecaa67d26:Main/Features/MissionPerf/AnimMemory/ClipBudgetSignature.cs:16`) | :16-19 | `8B 05 ? ? ? ? 41 8B EC 48 8B 3D ? ? ? ? 48 2B 3D ? ? ? ? 48 C1 FF 04 83 EF 01 66 0F 6E C0 0F 5B C0 F3 0F 5C 05 ? ? ? ? F3 44 0F 2C F8`; load +0 (6 bytes), budget +37 (8 bytes), expected 12582912f |

Constants:

- Link window: `Pool2CallWindow = Pool2CalleeSpan = 0x80` (SkeletonBufferSignature.cs:60).
- Guard service: `HeaderBytes 4096`, `MaxSiteDistance 0x80`, `ReachBytes 0x7FF00000` (SkeletonBufferGuardService.cs:31, :34, :37).
- Pages and caves: 2 pages of 0x1000, pool-1 cave 0x2B bytes comparing fill-after with `0x10000`, pool-2 cave 0x2A bytes at page+0x40 comparing with `0x40000` (SkeletonBufferCave.cs:19-24, :42-54, :74-76, :104-116).
- Watch offsets: `BufferOffset 0x9D0`, `BufferStride 0x128`, `Pool2Offset 0xC28` (SkeletonBufferWatchService.cs:20-24).
- Capacities: 65,536 and 262,144; `EntriesPerSkeleton 28`, used for display only (SkeletonBufferWatchState.cs:13, :16, :19).
- Test originals: `OriginalSite` and `OriginalSite2` (`origin/bannerlord-1.5.x:TAOM.Tests/Features/SkeletonBuffer/SkeletonBufferInstalledBinaryTests.cs:33-41`).

v1.5.4 RVAs named only in comments and docs:

- Host `FUN_180069aa0`, "980-byte"; pool-2 function `FUN_18006af30`, called from the host at `0x69AFE`. The reservations' `lock xadd` sit at `0x69D1C` (pool 1) and `0x6AF58` (pool 2) (`origin/bannerlord-1.5.x:docs/features/skeleton-buffer-guard.md:24, :73`).
- `0x6AF60` and `0x6AF63` (SkeletonBufferCave.cs:95).

KnownBuilds rows on the 1.5.x tip (SkeletonBufferInstalledBinaryTests.cs:24-31; ecaa67d26 had only the v1.5.4 row):

| Build | DLL bytes | WatchSite | Global | GuardSite | Pool2Load | Pool2GuardSite | Added by |
|---|---|---|---|---|---|---|---|
| v1.5.4 | 14,209,888 | 0x69CE1 | 0xD9D160 | 0x69D14 | 0x69AD5 | 0x6AF50 | ecaa67d26 |
| v1.5.5 | 14,212,960 | 0x6A1A1 | 0xD9E160 | 0x6A1D4 | 0x69F95 | 0x6B410 | 15e52d7af |
| v1.5.6 | 14,215,008 | 0x6A1A1 | 0xD9F160 | 0x6A1D4 | 0x69F95 | 0x6B410 | 12b010626 |
| v1.5.6.125270 | 14,218,592 | 0x6A181 | 0xDA0120 | 0x6A1B4 | 0x69F75 | 0x6B3F0 | f39b399b4 |
| **v1.4.8 (this scan)** | **14,185,944** | **0x88681** | **0xD96D18** | **0x886B4** | **0x88475** | **0x898F0** | (port) |

ClipBudget rows (`origin/bannerlord-1.5.x:TAOM.Tests/Features/MissionPerf/AnimMemory/ClipBudgetSignatureInstalledBinaryTests.cs:21-29`), shown as LoadSite, BudgetSite, Counter, Budget:

- v1.5.3, 14,209,376 bytes: 0x21E00F, 0x21E034, 0xDABE40, 0xB2E2DC
- v1.5.4: 0x21E00F, 0x21E034, 0xDABE40, 0xB2E2CC
- v1.5.5: 0x21E50F, 0x21E534, 0xDACE40, 0xB2E3EC
- v1.5.6: 0x21E50F, 0x21E534, 0xDADE40, 0xB2F3A4
- v1.5.6.125270: 0x21E42F, 0x21E454, 0xDAEE00, 0xB30434
- **v1.4.8, 14,185,944 bytes (this scan): 0x23AADF, 0x23AB04, 0xDA59B0, 0xB2BC9C**

### 3.2 Installed DLL identity and pattern results

| Field | Value |
|---|---|
| Path | `%BANNERLORD_GAME_DIR%\bin\Win64_Shipping_Client\TaleWorlds.Native.dll` (`C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord`) |
| Size | 14,185,944 bytes |
| SHA-256 | `2a5e0e0b15513ebb7052d747a621b82c50ee834553eba283b33bbbc33766e12a` |
| PE TimeDateStamp | `0x6A732505` (2026-08-05 11:56:53 UTC); the CodeView entry has the same stamp |
| CodeView | `TaleWorlds.Native.pdb`, GUID `2029BA6E-176D-498A-903C-B56D1AF1370C`, age 1 |
| File mtime | 2026-09-06 06:57:49 -0500 |
| Version | No VS_VERSIONINFO (only a manifest resource). `bin\Win64_Shipping_Client\Version.xml` says `Singleplayer v1.4.8`. "Build 119303" comes from the brief; the DLL does not carry it |
| PE | AMD64, PE32+, ImageBase 0x180000000, SizeOfImage 0xECA000, DllCharacteristics 0x160 (DYNAMIC_BASE, NX_COMPAT, HIGH_ENTROPY_VA; no GUARD_CF) |
| `.text` | VA 0x1000, VSize 0xA2B5DC, raw 0x400, raw size 0xA2B600 |
| `.data` | VA 0xCEF000, VSize 0x16FAC8, raw size 0x2DE00. The global lies in the zero-filled tail; the C# check uses VirtualSize, so it passes |

The scan read `.text` exactly as `SkeletonBufferInstalledBinaryTests.Resolve` does (`min(VirtualSize, SizeOfRawData)` from `PointerToRawData`).

| Pattern | Matches | RVA | Derived |
|---|---|---|---|
| Watch | 1 | 0x88681 | `mov rcx,[rip+0xd0e690]` resolves to global 0xD96D18 |
| Pool2 | 1 | 0x88475 | `mov rdx,[rip+0xd0e89c]` resolves to 0xD96D18 (the same global) |
| Guard | 1 | 0x886B4 | resume 0x886C1; 13 bytes `41 8b f4 4c 89 7c 24 20 f0 0f c1 75 00` equal `OriginalSite` |
| ForeignGuard | 0 | | |
| Pool2Guard | 1 | 0x898F0 | resume 0x898FD; 13 bytes `4c 89 7c 24 20 44 8b fa f0 44 0f c1 39` equal `OriginalSite2` |
| Pool2ForeignGuard | 0 | | |
| ClipBudget | 1 | 0x23AADF | see 3.5 |

The global is 8-aligned (0xD96D18 mod 8 = 0) and inside `.data` by the VirtualSize rule. The link check (`CalledFromLoad`) tries every E8 byte in the window:

- 0x8849E (load + 0x29) targets 0x898D0, which is 0x20 before the site: **linked**.
- 0x884B1 (+0x3C) targets 0x89830 (0xC0 before the site): not linked.
- 0x884EE (+0x79) targets 0x74E270: not linked.

**Base relocations.** None of the 19,180 entries overlaps any matched range (watch, pool 2, guard, pool-2 guard, the 0x80 call window, ClipBudget). So the in-memory bytes that `SkeletonBufferGuardService.InstallCore` scans (`moduleBase + text.VirtualAddress`, SkeletonBufferGuardService.cs:127) equal the file bytes under ASLR, unless another module patched them. The foreign-shape patterns cover that case.

### 3.3 Distances against every 1.5.x row

| Build | guard - watch | watch - pool2Load | pool2Guard - watch | global % 8 |
|---|---|---|---|---|
| v1.5.4 | 0x33 | 0x20C | 0x126F | 0 |
| v1.5.5 | 0x33 | 0x20C | 0x126F | 0 |
| v1.5.6 | 0x33 | 0x20C | 0x126F | 0 |
| v1.5.6.125270 | 0x33 | 0x20C | 0x126F | 0 |
| **v1.4.8** | **0x33** | **0x20C** | **0x126F** | **0** |

**`.pdata`.** One logical function: primary RUNTIME_FUNCTION [0x88440, 0x8845F), with chained fragments (UNW_FLAG_CHAININFO) at 0x8845F, 0x88483, 0x884C6, 0x884D3 and 0x886AF, up to 0x88814. That makes 980 bytes, the size the 1.5.x doc gives `FUN_180069aa0`. Offsets into it:

| | v1.4.8 | v1.5.4 |
|---|---|---|
| pool-2 load | +0x35 | 0x69AD5 - 0x69AA0 = +0x35 |
| call | +0x5E | 0x69AFE - 0x69AA0 = +0x5E |
| watch | +0x241 | 0x69CE1 - 0x69AA0 = +0x241 |

The pool-2 function is [0x898D0, 0x89918) plus a chained fragment to 0x899B0. Exactly 4 E8 call sites target 0x898D0 (0x8849E, 0x35CB08, 0x35CB97, 0x4B4AF8), matching "the first calls at 0x69AFE and three other functions call for the same pool". The caller at 0x35CB08 loads the same global plus 0xC28.

### 3.4 Instruction semantics on v1.4.8 (capstone)

**Pool 1 (host FUN_180088440).**

- `0x88681 mov rcx,[global]; add rcx,0x9d0; movsxd rax,[rcx+0x250]; imul rbp,rax,0x128; add rbp,rcx`: the current buffer of a double-buffered pair.
- The site at 0x886B4 is `mov esi,r12d; mov [rsp+0x20],r15; lock xadd [rbp],esi`, so `[rbp]` is the fill counter.
- The resume at 0x886C1 is `lea r15d,[r12-1]; mov eax,esi; shr eax,0xb`: 2,048 entries per block. eax and the flags are dead at resume.
- Block pointers are read at `[rbp+8+idx*8]`; per-block ready bytes are at `[rbp+0x108+idx]`.
- Blocks are allocated as 0x2000 bytes (`mov edx,0x2000; call 0x729b40`).
- Nothing compares the block index with 32.
- The per-skeleton count is the byte at `[rdi+0x44]` (0x884C1). Pool 1 is skipped when `[rdi+0x10]` is null (0x884B6).

**Pool 2 (FUN_1800898D0).** rcx is the buffer, edx the count.

- The site at 0x898F0 is `mov [rsp+0x20],r15; mov r15d,edx; lock xadd [rcx],r15d`.
- The resume at 0x898FD is `lea edi,[rdx-1]; mov eax,r15d (0x89900); shr eax,0xd (0x89903)`: 8,192 entries per block. eax and the flags are dead at resume.
- Blocks are 0x80000 bytes; the same +8 / +0x108 layout; no bound against 32.

**Layout.** 4-byte fill, 4 bytes of padding, 32 block pointers, then 32 ready bytes: 0x128 bytes, the stride. Capacities are therefore exactly 32 x 2,048 = 65,536 and 32 x 8,192 = 262,144, matching the caves' `0x10000` and `0x40000`. The pool-2 bound follows from the callee's own block size, so it is right for all four callers.

**The four spin loops** (each is `movzx; test; jnz` back to itself; the RVAs cited in FREEZE-THREADING-06/07 are correct):

| Pool | Loop at |
|---|---|
| 1 | 0x88730-0x88735 |
| 1 | 0x88744-0x88749 |
| 2 | 0x89977-0x8997C |
| 2 | 0x89990-0x89995 |

**Unwind.** In both functions the fragment holding the site saves r15 to `[rsp+0x20]` inside its unwind prolog (prolog 13 for [0x886AF, ...) and 37 for 0x898D0). Each cave performs that same save before jumping back. So at every resume point the frame matches the unwind data. Inside a cave there is no unwind data, the limit the 1.5.x doc already records. There is no CFG, and the jumps are direct `E9` anyway.

**Freeze mechanism.** An index of 32 reads the ready-byte area as a block pointer. The cmpxchg path then writes a heap pointer over those bytes, and waiters spin. This is the mechanism ecaa67d26 describes, and it is present on v1.4.8.

### 3.5 The rows the port needs

```csharp
// SkeletonBufferInstalledBinaryTests.KnownBuilds (1.4.x: this row only)
[14185944] = ("v1.4.8", 0x88681, 0xD96D18, 0x886B4, 0x88475, 0x898F0),
// ClipBudgetSignatureInstalledBinaryTests.KnownBuilds (if that class is ported; see 5.3)
[14185944] = ("v1.4.8", 0x23AADF, 0x23AB04, 0xDA59B0, 0xB2BC9C),
```

ClipBudget checks on this DLL:

- The counter 0xDA59B0 is in `.data` and 4-aligned.
- The budget 0xB2BC9C is in `.rdata` and 4-aligned.
- The float read from the file there is 12582912.0.

These meet every assertion of the class's signature test (ClipBudgetSignatureInstalledBinaryTests.cs:31-44).

## 4. The other three guards: engine members against v1.4.8

All paths in this section are under `C:\Users\mikew\.taom-src\v1.4.8\` unless they say "dump".

### 4.1 MissionStartGuard (Patch103)

**Harmony target.** `public void Mission.AfterStart()`, non-virtual, no parameters (TaleWorlds.MountAndBlade.Mission.cs:3799-3836). The body matches the dump line for line. Calls sit at :3807, :3811, :3815, :3819, :3825 and :3829; `CurrentState = State.Continuing` is at :3835.

**`AccessTools.Method` sites** (`ecaa67d26:Main/Features/MissionStartGuard/Hooks/MissionStartGuardSwaps.cs:21-29`). All six are introducing `public virtual` declarations on the named type, so `TickProfilerTranspiler.Fits` (DeclaringType equals the helper's first parameter) passes:

| Target | Declared at |
|---|---|
| `MBSubModuleBase.OnBeforeMissionBehaviorInitialize(Mission)` | MBSubModuleBase.cs:105 |
| `MissionBehavior.OnBehaviorInitialize()` | MissionBehavior.cs:21 |
| `MBSubModuleBase.OnMissionBehaviorInitialize(Mission)` | MBSubModuleBase.cs:101 |
| `MissionBehavior.EarlyStart()` | MissionBehavior.cs:29 |
| `MissionBehavior.AfterStart()` | MissionBehavior.cs:33 |
| `MissionObject.AfterMissionStart()` | MissionObject.cs:170 (`MissionObject : ScriptComponentBehavior`, which does not declare it) |

**Installed IL** (`ilspycmd -il`). Exactly one `callvirt` each: IL_0047, IL_0075, IL_00a2, IL_00d5, IL_011e and IL_0151. The transpiler would swap 6 of 6, which is `MissionStartGuardService.ExpectedSites`.

**The reload loop** (TaleWorlds.MountAndBlade.MissionState.cs):

- `OnTick` ticks loading while the state is NewlyCreated or Initializing (:89, :95).
- `TickLoading` calls `LoadMission` whenever `!_missionInitializing` (:221-232).
- `LoadMission` sets `_missionInitializing = true` and calls `Mission.Initialize` again (:235-244).
- `FinishMissionLoading` clears the flag (:335) before `CurrentMission.AfterStart()` (:345).

**Who swallows on 1.4.x.** `origin/bannerlord-1.4.5:Dependencies/Foundation/SaveShield.cs:83-85` finalizes `MissionState.FinishMissionLoading`, `Mission.Initialize` and others under MISSION-INIT. `SaveShieldPolicy.ShouldSwallow` returns true for every category when co-op is absent, unless `saveshield-swallow-disabled.flag` exists (SaveShieldPolicy.cs:30-35; SaveShield.cs:159-165, :283-286). That is the forever-reload. The Native2Managed finalizers that LOADING-HANG-03 names are an outer net, not the first catcher.

**Other bindings.**

- `TextObject`, `InformationManager.DisplayMessage` and `Color.FromUint` are standard on 1.4.x.
- 1.4.x already patches `Mission.AfterStart` with Patch43, a prefix and postfix (`origin/bannerlord-1.4.5:Main/Features/BattleLoadDiagnostics/Hooks/Mission_AfterStart_BattleLoad_Patch.cs:14-15`). No conflict.
- 1.4.x PatchShield swallows only MissingMethod, MissingField and TypeLoad, at Harmony's default priority. Its exclusion list covers three GauntletUI/TwoDimension namespace prefixes (`origin/bannerlord-1.4.5:Dependencies/Foundation/PatchShield.cs:60-69, :261-300`), so `Priority.First` still orders the guard's finalizer first.
- Both lines pin Lib.Harmony 2.4.2 (Main/TAOM.csproj).

**No drift.**

### 4.2 NameplateCull (Patch104)

| Bound member | v1.4.8 | State |
|---|---|---|
| `SettlementNameplatesVM.Update()` (target) | SandBox.ViewModelCollection.Nameplate.SettlementNameplatesVM.cs:238-248: camera, `TWParallel.For(..., UpdateNameplateAuxMTPredicate, 16)`, `RefreshBindValues` loop | same shape |
| `_mapCamera` (`private readonly Camera`) | :18 | ok |
| `_cachedCameraPosition` | :20; read only in `UpdateNameplateAuxMT` (:229-235), written only in `Update` (:242) | ok |
| `AllNameplates` | :42 | ok |
| `SettlementNameplateVM._worldPos`, `_bindIsVisibleOnMap` | SettlementNameplateVM.cs:72, :108 | ok |
| `Settlement`, `IsTracked`, `IsInRange`, `UpdateNameplateMT(Vec3)`, `RefreshBindValues()` | :148, :296, :332, :681, :744 | ok |
| `NameplateVM.Position` (Vec2), `IsVisibleOnMap`, `IsTargetedByTutorial` | base class (dump `_modules_build\SandBox__SandBox.ViewModelCollection.cs:7055, :7071, :7089`) | ok |
| `TWParallel.For(int,int,ParallelForAuxPredicate,int grainSize = 16)`, nested delegate | TaleWorlds.Library.TWParallel.cs:36, :11 | ok; the default grain equals vanilla's explicit 16 |
| `VisualTrackerManager.CheckTracked(ITrackableBase)`; `Settlement : ITrackableBase` | VisualTrackerManager.cs:84; Settlement.cs:27 | ok |
| `Settlement.IsVisible/IsInspected/IsTown/IsFortification/IsHideout/Party`, `PartyBase.IsVisualDirty` | Settlement.cs:130, :147, :370, :394, :408, :97; PartyBase.cs:454 | ok |
| Visibility constants 400 / 200 / +100 | SettlementNameplateVM.cs:772-793 | same |
| Parked point (-1000, -1000) | SettlementNameplateVM.cs:570 | same |
| Range source in `RefreshDynamicProperties` | SettlementNameplateVM.cs:500-509: hideout uses `SettlementComponent is ISpottable && IsSpotted`, otherwise `IsInspected` | **DRIFT** |

**What drifted.** The adapter reads `settlement.IsHideout ? settlement.IsVisible : settlement.IsInspected` (`ecaa67d26:Main/Adapters/NameplateCullAdapter.cs:128`). On v1.4.8 `IsSpotted` and `Settlement.IsVisible` are separate fields (dump `...\TaleWorlds.CampaignSystem.Settlements\Hideout.cs:49-59`). One vanilla path sets them apart: `IncidentEffect.cs:699-700` does `hideout.IsSpotted = true; hideout.Settlement.IsVisible = false;`.

**Effect.** A hidden plate of such a hideout is culled although vanilla would flip `IsInRange` to true. That flip runs `RegisterEvents` (SettlementNameplateVM.cs:332-355), so the cull's "a skipped plate's update changes nothing" invariant breaks for that case. It self-corrects once the plate can be visible again.

**Fix.** Use v1.4.8's own expression. `ISpottable` lives in `TaleWorlds.CampaignSystem.Settlements`, already imported by the adapter.

**Test re-pin.** `ecaa67d26:TAOM.Tests/Features/NameplateCull/NameplateCullBindingTests.cs:302-326` pins v1.5.4's `RefreshDynamicProperties` call list, which contains `Settlement.get_IsVisible` (:322) and `Settlement.get_FerryTarget` (:325). v1.4.8 has no `FerryTarget` (absent from Settlement.cs) and reaches buildings through `Settlement?.Town?.Buildings`. That test fails on v1.4.8 and must be re-pinned from the v1.4.8 IL. This is the gate working, not a reason to delete it.

**Notification ticks.** `RefreshBindValues` ticks `SettlementNotifications` and `SettlementEvents` only when their events are registered (:762-768). Registration follows `IsInRange`, so skipping an out-of-range plate skips no live tick.

**No TAOM conflict.** The 1.4.x nameplate fade and relation code is identical to ecaa67d26's: `git diff ecaa67d26 origin/bannerlord-1.4.5 -- Main/Features/SettlementNameplateRelation Main/Features/SettlementNameplateFade` is empty. Neither patches a VM method.

### 4.3 MapViewRelease (no Harmony patch)

| Bound member | v1.4.8 |
|---|---|
| `public static event Action<ScreenLayer> OnLayerActiveStateChanged`; `IsActive` | TaleWorlds.ScreenSystem.ScreenLayer.cs:39, :25 |
| Raised after `IsActive = true; OnActivate()` and after `OnDeactivate(); IsActive = false; TryLoseFocus` | ScreenLayer.cs:110-129 |
| `SceneLayer.ClearRuntimeGPUMemory(bool remove_terrain)`, which is `_sceneView.ClearAll(clearScene: false, remove_terrain)` | TaleWorlds.Engine.Screens.SceneLayer.cs:145-148. The named argument `remove_terrain:` compiles |
| `MapScreen.Instance`, `MapScreen.SceneLayer`; `ClearGPUMemory` calls `ClearRuntimeGPUMemory(true)` | SandBox.View.Map.MapScreen.cs:257, :294, :507-512 |
| `ScreenBase` activates layers before `OnActivate`/`OnResume`, deactivates before `OnPause` | dump `Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenBase.cs:71-145` |

**No drift.**

## 5. Prerequisites: what ecaa67d26 uses, and the minimal 1.4.x answer

### 5.1 9ed3ee308, the feature-module runner (absent on 1.4.x)

**What ecaa67d26 uses.**

- `TaomFeatureModule` (`Id`, `RegisterServices(IRegistrator)`, `InitializeStatics(IResolver)`, `PatchCategories`, `MissionBehaviors`, `OnPhase`).
- `PatchCategoryDecl(name, ApplyPhase.GameInit)` and `MissionBehaviorDecl.Of(...)`.
- `ApplyPhase.MainMenu` (the first `OnBeforeInitialModuleScreenSetAsRoot`) and `ApplyPhase.GameInit` (the first `OnGameInitializationFinished`).
- Four `FeatureModules.All` entries (`ecaa67d26:Main/Composition/FeatureModules.cs:32-35`).
- The runner's per-module fault isolation (`ecaa67d26:Main/Composition/FeatureModuleHooks.cs:39-123`).

**Minimal 1.4.x answer: hand wiring, no Composition.** Keep each `*Module` class as an `internal static class` with the same full name, holding only `PatchCategory` constants. Two reasons: the patch attributes reference `MissionStartGuardModule.PatchCategory` and `NameplateCullModule.PatchCategory`, and VanillaTuning v0.1.1 stands aside on the type `TAOM.Features.SkeletonBuffer.SkeletonBufferModule` (`origin/bannerlord-1.5.x:docs/features/skeleton-buffer-guard.md`, foreign-guard row, added in 7eac30153; how it looks the type up is UNVERIFIED).

Registrations move to per-feature `<Feature>IoC.Register<Feature>Feature(IContainer)` files, called from `IoC.Configure`. That is the 1.4.x idiom (`origin/bannerlord-1.4.5:Main/IoC.cs:88-99`; `Main/Features/CoopInterop/CoopInteropIoC.cs:7-16`). The phases map to SubModule.cs as in 6.2.

### 5.2 6eb2e1b90, TickProfilerTranspiler (absent on 1.4.x)

**What ecaa67d26 uses.** `TickProfilerTranspiler.Rewrite(instructions, swaps, "Mission.AfterStart", null, out swapped)` and the one-helper `CallSwap` constructor (`ecaa67d26:Main/Features/MissionPerf/TickProfilerTranspiler.cs:15-27, :37-97`). The file is pure: System, Reflection.Emit, HarmonyLib, IModLogger. It calls three `TickProfileLines` builders, which never run here because the logger is null, but they must compile.

**Do not copy `TickProfileLines.cs` whole.** It references `TickWindow`, `PerfContext` and `MissionTickProfiler.MaxHitchLinesPerMission` (`ecaa67d26:Main/Features/MissionPerf/TickProfileLines.cs:36, :89, :164`).

**Minimal answer.**

1. Copy `TickProfilerTranspiler.cs` verbatim at its 1.5.x path.
2. Add a trimmed `Main/Features/MissionPerf/TickProfileLines.cs` with `StatusTag`, `BuildSiteCountWarning` (:121-124), `BuildHelperMismatchWarning` (:126-127), `BuildRewriteFault` (:188-190), `LeftVanilla` (:203-207) and `Quote` (:258-259), byte for byte. WP 112's full file later replaces it.
3. Bring `TAOM.Tests/Features/MissionPerf/TickProfilerTranspilerTests.cs` (192 lines) and `TickProfilerTranspilerOccurrenceTests.cs` (160 lines). They touch only `Rewrite` and `TickProfileLines.BuildSiteCountWarning`, plus members present on v1.4.8: `Mission.WaitTickCompletion` (Mission.cs:3585), `MissionBehavior.OnPreDisplayMissionTick` (:142) and `OnMissionTick` (:146).

### 5.3 a9f10688f, ClipBudgetSignature (absent on 1.4.x)

**What ecaa67d26 uses.**

- `ClipBudgetSignature.Parse`, `Find` and `RipTarget` (`ecaa67d26:Main/Features/MissionPerf/AnimMemory/ClipBudgetSignature.cs:22-50`).
- `PeSectionTable.Parse`/`Find` and `PeSection.Contains`/`VirtualAddress`/`VirtualSize`; the tests also use `PointerToRawData`/`SizeOfRawData` (`PeSectionTable.cs:8-88`).
- A doc-comment `<see cref="INativeModuleMemoryAdapter"/>` in `ecaa67d26:Main/Adapters/ISkeletonBufferMemoryAdapter.cs:21`. That type is absent on 1.4.x. It is harmless, since 1.4.x has no `GenerateDocumentationFile` or warnings-as-errors (Main/TAOM.csproj), but the comment should be reworded.

Both Main files are pure System code and unchanged since a9f10688f on 1.5.x.

**Minimal answer.**

- Copy `ClipBudgetSignature.cs` and `PeSectionTable.cs` at their 1.5.x paths.
- Bring `TAOM.Tests/Features/MissionPerf/AnimMemory/SyntheticNativeImage.cs` (80 lines, System only), `ClipBudgetSignatureTests.cs` and `PeSectionTableTests.cs`.
- Bring `ClipBudgetSignatureInstalledBinaryTests.cs` with the v1.4.8 row (3.5). This keeps `NativePinGateTests` unchanged, since it pins that class by name (`origin/bannerlord-1.5.x:TAOM.Tests/Migration/NativePinGateTests.cs:42-44`).
- Do not port the AnimMemory probe, `INativeModuleMemoryAdapter` or the 12 MiB probe; they belong to WP 112.

**Docs-gap side effect.** A new `Main/Features/MissionPerf/` folder has no `docs/features/mission-perf*.md` on 1.4.x, so `detect-docs-gaps.sh` will name it. The hook only reports, and accepts an alias doc.

### 5.4 Hidden prerequisites the classifier did not list

| Need | Used by | 1.4.x state | Minimal answer |
|---|---|---|---|
| `TAOM.Tests/Infrastructure/RepoPaths.cs` with `ReadSource(path, stripComments)` | BattleLoadCloserWiringTests, SkeletonBufferWiringTests, NativePinGateTests | absent | Use `4f27b3bde:TAOM.Tests/Infrastructure/RepoPaths.cs` (72 lines: RepoPath, ReadSource, StripComments, MentionsOutsideComments; no `Client` root). WP 111 needs the same file; whichever lands first adds it. Do NOT use the tip version (`CodeRoots = { "Main", "Client" }`) or 35007dbb8's |
| `GameAssemblies.ResolveGameDir(string,string,string)` and `BuiltGameFolder` | both InstalledBinaryTests `LoadInstalled` (tip test :96-99; ClipBudget :63-66) | 1.4.x has only a private parameterless `ResolveGameDir` (`origin/bannerlord-1.4.5:TAOM.Tests/Migration/GameAssemblies.cs:111-122`); `EnsureLoaded()` and `GameDir` are public (:36, :28) | Use `GameAssemblies.EnsureLoaded() ? GameAssemblies.GameDir : null` in both `LoadInstalled` methods, or land plan 008 (WP 133) first |
| `RecordingLogger` | SkeletonBufferGuardServiceTests, ...WatchServiceTests, ...WatchMissionBehaviorTests (namespace `TAOM.Tests.Features.LoadTimeStamps`, a 1.5.x-only feature) | absent | Copy `ecaa67d26:TAOM.Tests/Features/LoadTimeStamps/RecordingLogger.cs` (35 lines) into `TAOM.Tests/Features/SkeletonBuffer/` and change the three `using` lines. `IModLogger` is identical on both lines (Main/Core/Logging/IModLogger.cs:5-15) |
| `PatchShieldPolicy.IsExcludedTargetMethod/IsExcludedTargetNamespace` | `MissionStartGuardBindingTests.PatchShield_StillWrapsMissionAfterStart_ByNameAndByNamespace` (ecaa67d26 :276-281) | absent (from 30abf550b) | Rewrite that one test against 1.4.x's private `ExcludedTargetNamespacePrefixes` (PatchShield.cs:60-69) by reflection, or drop it |
| Composition-shaped tests | `MissionStartGuardModuleTests`, `NameplateCullModuleTests`, `MapViewReleaseModuleTests`, the module parts of `SkeletonBufferWiringTests` (ecaa67d26 :12, :32, :47-49, :89-94) and `MissionStartGuardBindingTests` :263-270 | runner absent | Rewrite as 1.4.x source-text wiring tests. Precedent: `origin/bannerlord-1.4.5:TAOM.Tests/Features/WandererAllegiance/WandererAllegianceWiringTests.cs`, `MountDespawnWiringTests.cs`, `HeroRaceWiringTests.cs` |
| `[ClientOnly]` (6d7e67f3f) | tip Patch104 class | absent | Take ecaa67d26's version of the patch file |
| `CampaignHotPathSettingsProvidersTests`, `HotPathSettingsProvidersTests` | the two NameplateCull provider tests | both absent | Put `NameplateCull_ReadsThroughTheCachedSettings` and `..._NoMcm_CullDefaultsOn` in a new `NameplateCullSettingsProviderTests` |

### 5.5 The native pin gate (15e52d7af, c286e2393) on 1.4.x

**What to take.**

- Both class tags (`[TestCategory("LiveInstall")]` and `[TestCategory("BindingVerification")]`).
- `NativePinGateTests.cs` in its tip form (81 lines; c286e2393 plus fdee18f02's comment). It uses `AccessTools.GetTypesFromAssembly`, compiled `TestCategory` attributes and `RepoPaths.ReadSource(".claude/skills/engine-bump/SKILL.md")` (:66-71).
- A native-pin paragraph in 1.4.x's engine-bump skill. The skill (143 lines on `origin/bannerlord-1.4.5`) names neither class, so `KnownBuildsTables_EachClass_IsNamedInTheEngineBumpSkill` fails until it does. Adapt the tip's bullet and Phase 5 lines (`origin/bannerlord-1.5.x:.claude/skills/engine-bump/SKILL.md:212-222, :248-250`). Drop the `PINS`, `native_decompile.py` and `native_build_match.py` sentences: those tools are absent on 1.4.x. `native_crash_triage.py` and `native_sig_author.py` are present.

**What the tag does not do on 1.4.x.** It makes an unlisted build fail only once plan 008's `TAOM.Tests/binding-gate.runsettings` exists and the gate passes it (`origin/bannerlord-1.5.x:TAOM.Tests/binding-gate.runsettings`: `MapInconclusiveToFailed`, `TreatNoTestsAsError`). Without it an unlisted build reads Skipped with exit 0. Either bring that file plus the verify-bindings skill line into this WP, or record in the plan that the fail-not-skip property arrives with WP 133.

## 6. Port spec for WP 106 (all on the 1.4.x line)

### 6.1 Commit order

Each commit is subjected `...: v2.0.28 - ...` (the hook enforces it), stages its own CHANGELOG.md hunk (the 1.4.x hooks hard-block a `.claude/` commit without one), and is staged path by path.

1. **Shared helpers, no behaviour change.**
   - Main: `Main/Features/MissionPerf/TickProfilerTranspiler.cs`, the trimmed `TickProfileLines.cs`, `Main/Features/MissionPerf/AnimMemory/{ClipBudgetSignature,PeSectionTable}.cs`.
   - Tests: `TAOM.Tests/Infrastructure/RepoPaths.cs` (4f27b3bde, unless WP 111 has landed it), `SyntheticNativeImage.cs`, `ClipBudgetSignatureTests.cs`, `PeSectionTableTests.cs`, `ClipBudgetSignatureInstalledBinaryTests.cs` (v1.4.8 row only, 1.4.x `LoadInstalled`), `TickProfilerTranspilerTests.cs`, `TickProfilerTranspilerOccurrenceTests.cs`.
2. **BattleLoad closer in a finally (P0, independent).**
   - In `Main/SubModule.cs`, move the body of `OnMissionBehaviorInitialize` from :1765 (the `_missionTimePatchesApplied` prelude) to :1910 (`LogTaomBehaviorsDone`) into `private void AddTaomMissionBehaviors(Mission mission)`. Call it inside `try { } finally { }` with no catch.
   - Remove the closer at :1888-1890. Re-add it in the finally as ecaa67d26 does (its SubModule hunk at `@@ -2045`): `mission.AddMissionBehavior(...)` guarded by `GetMissionBehavior<BattleLoadPhaseBehavior>() == null`.
   - Port `TAOM.Tests/Features/BattleLoadDiagnostics/BattleLoadCloserWiringTests.cs` unchanged. Its anchors `if (!_missionTimePatchesApplied)` and `ShaderPrecompileRunner.TryClaimMission(mission)` exist at SubModule.cs:1765 and :1795.
3. **MissionStartGuard (Patch103).**
   - Main: `Main/Features/MissionStartGuard/**` (from ecaa67d26, with the module reduced to a static constants class), `Main/Adapters/{I,}MissionStartGuardAdapter.cs`, and a new `MissionStartGuardIoC.cs`.
   - Setting: `CrashReportSettings.SurviveMissionStartFailures` (Master group, Order 3, default true, `RequireRestart = false`).
   - String: `taom_mission_start_guard_notice` in `Main/_Module/ModuleData/taom_module_strings.xml`.
   - Tests: `TAOM.Tests/Features/MissionStartGuard/**`, with the module test rewritten as a wiring test and the PatchShield test adapted (5.4).
   - Optional, recommended: 649cc30ff's "Throw On Next Mission AfterStart" dev trigger (CrashReportSettings plus `CrashReportDevTrigger.AfterStart`), the only built-in repro for the in-game check. It adds one more CrashReportSettings property.
4. **SkeletonBuffer guard and watch.**
   - Main: `Main/Features/SkeletonBuffer/**`, the four `Main/Adapters/*SkeletonBuffer*` files (reword the `INativeModuleMemoryAdapter` cref), and `SkeletonBufferIoC.cs`.
   - Settings: `SkeletonBufferGuard` (Order 4, default true, `RequireRestart = true`) and `SkeletonBufferWatch` (Order 5, default true).
   - String: `taom_skeleton_buffer_warning`.
   - Tests: the 12 `TAOM.Tests/Features/SkeletonBuffer/*` classes, with the ecaa67d26 version of `SkeletonBufferWiringTests` (it uses `Directory.GetFiles`, not `CSharpFiles`) and its module parts rewritten, `RecordingLogger` relocated, and `SkeletonBufferInstalledBinaryTests` with the v1.4.8 row and the 1.4.x `LoadInstalled`.
   - Then the 15e52d7af and c286e2393 pieces from 5.5.
5. **NameplateCull (Patch104), with the v1.4.8 changes from 4.2.**
   - In `NameplateCullAdapter.MaySkip`, set `SettlementInRange = settlement.IsHideout ? settlement.SettlementComponent is ISpottable s && s.IsSpotted : settlement.IsInspected`.
   - Re-pin `NameplateCullBindingTests.VanillaDynamicProperties_CallExactlyTheSettlementFactsTheRuleMirrors` and every other pinned sequence from the v1.4.8 IL. Run the RequiresGameIL tests to read the real lists.
   - Fix the comments that say v1.5.4.
   - Settings: `BattleLoadDiagnosticsSettings.CullHiddenNameplates`, in a new "Map Performance" group, default true. Its HintText mentions the `[MapProfile]` lines of the map profiler, which is absent on 1.4.x: reword it.
   - Add the Patch104 `CoopVetoClassificationTests` entry (bool prefix: `ReviewedSafe`). Patch103's void prefix needs none (CoopVetoClassificationTests.cs:15-23).
6. **MapViewRelease.**
   - Main: `Main/Features/MapViewRelease/**`, `Main/Adapters/{I,}MapViewReleaseAdapter.cs`, and `MapViewReleaseIoC.cs`.
   - Settings: `ReleaseMapViewMemory` (default true) and `MapViewReleaseInterval` (1-1000, default 20).
   - Tests: `TAOM.Tests/Features/MapViewRelease/**`, with the module test rewritten.
7. **Bookkeeping and docs, in the same commits or one more.**
   - `Main/_Module/THIRD-PARTY-LICENSES.txt`: a VanillaTuning section that carries the FULL MIT permission notice. The 1.5.x section points at "full text above, under RUNTIME LIBRARIES", and the 1.4.x file (117 lines) has no MIT text and no such section.
   - `docs/reference/provenance-register.md`: the VanillaTuning row.
   - `docs/reference/harmony-patch-registry.md`: Patch103 and Patch104 sections, with the v1.4.8 targets.
   - `docs/features/{skeleton-buffer-guard,mission-start-guard,nameplate-cull,map-view-release}.md`: v1.4.8 RVAs from section 3; drop the `native_decompile.py` instructions.
   - The native layout (1.5.x engine doc section 10) goes into the skeleton-buffer feature doc: `docs/reference/engine/mission-frame-threads-and-native-costs.md` does not exist on 1.4.x (WP 136).
   - `docs/reference/feature-map.md`, `docs/features/mcm.md`, `crash-report.md` and `battle-load-diagnostics.md`.
   - The two harness edits: the `.claude/rules/harmony-patches.md` lesson bullet and the `.ai/review-reference.md` wording. 83ad1eab5's `__state` bullet is in the same group.
   - Regenerate the API snapshot (`pwsh tools/snapshot_api_surface.ps1`, then `-Check`).
   - Leave out the adoption records, the Codex prompts and `plans/_audit`.

### 6.2 Wiring (single-owner files)

**`Main/IoC.cs`.** Four `Features.X.XIoC.RegisterXFeature(container)` calls beside :88-99. Registrations as in each `*Module.RegisterServices`:

- MissionStartGuard: 3 singletons.
- SkeletonBuffer: 5, including `SkeletonBufferWatchService` as a concrete singleton.
- NameplateCull: 3.
- MapViewRelease: 3.

`IDedicatedServerProvider` (CoopInteropIoC.cs:13) and `IModLogger` (IoC.cs:225) are already registered.

**`Main/SubModule.cs`:**

- `OnSubModuleLoad`, after `IoC.Configure()` (:114), beside the existing static handshakes (:298-303): `MissionStartGuardCalls.Initialize(...)`, `NameplateCullCalls.Initialize(...)`, `MapViewReleaseCalls.Initialize(...)`.
- `OnBeforeInitialModuleScreenSetAsRoot` (:525): `IoC.Resolve<ISkeletonBufferGuardService>().Install()` in a try/catch. `Install` latches with `_ran` and never throws; this hook runs on every return to the menu.
- `OnGameInitializationFinished`, inside the once-per-process block after `_gameInitPatchesApplied = true;` (:1324-1325):
  - Apply `"Patch103_MissionStartGuard"`, then `LogInstall(MissionStartGuardSwaps.LastSwapped)`.
  - Apply `"Patch104_NameplateCull"`, then `INameplateCullService.Install()`.
  - Call `IMapViewReleaseService.LogInstall()`, then `MapViewReleaseLayerEvents.SubscribeOnce()`.
  - Give each its own try/catch, like the preview-category loop (:1338-1361), or use `TryPatchCategory` if WP 104 (plan 009) landed first.
- `AddTaomMissionBehaviors`: `AddTaomBehavior(new SkeletonBufferWatchMissionBehavior(IoC.Resolve<SkeletonBufferWatchService>()))` after `BattleActionBarMissionView` (:1853) and before `MissionDiagnostic`.

### 6.3 MCM and json2

**Keys are new.** None of the six keys, the dev trigger or the "Map Performance" group exists on 1.4.x (git grep), so their defaults reach existing installs: no rename is needed.

**Keys are shared across lines.** Both lines use the same settings Ids (`TAOM.CrashReport`, `TAOM.BattleLoadDiagnostics`), folder `TAOM` and `json2` (`origin/bannerlord-1.4.5:Main/Features/CrashReport/CrashReportSettings.cs:12-15`, `BattleLoadDiagnosticsSettings.cs:13-16`, the same on 1.5.x). A machine that ran a 1.5.x v2.0.34+ build already has persisted values under these exact names. Keep the 1.5.x names and defaults. If 1.4.x wants a different default (for example NameplateCull off until it is measured), give the key a new name instead of a new default. That is the rename-never-flip rule from CLAUDE.md applied at port time.

**Count updates.**

- `SettingsFingerprintTests.AssertSplit`: `BattleLoadDiagnosticsSettings` 7 to 10, `CrashReportSettings` 6 to 9, or to 10 with 649cc30ff (`origin/bannerlord-1.4.5:TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs:211, :213`).
- `SettingRequireRestartPostureTests`: add the `CrashReportSettings.SkeletonBufferGuard` row and update the "every setting but N" sentence from 1.4.x's own count.
- `CoopSettingsRelevance`: the six keys, with ecaa67d26's classification.
- `ReflectionSiteBindingTests`: the 9 DataRows from ecaa67d26 (same 5-column shape on 1.4.x, :106).

### 6.4 Gates (local only; the 1.4.x line has no CI)

Run every command with `-p:DisableModuleCopy=true -p:ModuleId=`.

1. **Focused tests:** `dotnet test TAOM.Tests ... --filter "FullyQualifiedName~SkeletonBuffer|FullyQualifiedName~MissionStartGuard|FullyQualifiedName~NameplateCull|FullyQualifiedName~MapViewRelease|FullyQualifiedName~NativePinGate|FullyQualifiedName~BattleLoadCloser|FullyQualifiedName~TickProfilerTranspiler|FullyQualifiedName~ClipBudget|FullyQualifiedName~PeSectionTable|FullyQualifiedName~SettingsFingerprint|FullyQualifiedName~SettingRequireRestart|FullyQualifiedName~CoopVeto|FullyQualifiedName~ReflectionSite"`.
2. **Binding gate:** `--filter TestCategory=BindingVerification`, with the game environment set. Read the skip count; on 1.4.x skips do not fail.
3. **Full suite.** The 1b00d1acd baseline is 9150 passed, 3 skipped.
4. `pwsh tools/snapshot_api_surface.ps1 -Check`.

### 6.5 In-game checks on v1.4.8 (owed; none done on either line per the 1.5.x docs)

1. **First start**, in `taom_debug_*.log`:
   - `[SkeletonBuffer] guard ON pool 1: site=0x886B4 resume=0x886C1 cave=0x... counter=0x... scanMs=...`
   - `[SkeletonBuffer] guard ON pool 2: site=0x898F0 resume=0x898FD ...`
   - Then turn the guard off, restart, and confirm `guard OFF: setting off (MCM Skeleton Buffer Guard)`. This proves MCM is readable at that hook on 1.4.x (UNVERIFIED; the provider fails open).
2. **Large battle and siege** (Unlimited corpses): one `[SkeletonBuffer] mission peak:` line per mission, covering both pools. If overflows are above 0: wrong bones for a frame, never a freeze.
3. **Patch103 install line** names 6 swapped sites at GameInit. With the dev trigger and Enable Crash Capture on, the mission starts, one notice shows, and an ERROR line names the behaviour and its assembly. With the toggle off, the old reload loop returns.
4. **Nameplates.** Zoom and pan; a tracked settlement; a spotted hideout (the drift case); tutorial highlights; siege and raid events on plates; fast forward. A map frame-time A/B with the cull on and off.
5. **Map view.** Twenty covers (inventory, party, encyclopedia): the twentieth return shows the short loading screen, with no black map. The load-game preview still renders after the GameInit subscription (the upstream module moved its install for that reason).
6. **Closer.** A throw in TAOM wiring (the dev trigger) leaves no 300 s BattleLoad watchdog bundle.

### 6.6 STOP conditions

- **The DLL is not the one scanned.** `TaleWorlds.Native.dll` is not 14,185,944 bytes or its SHA-256 is not `2a5e0e0b...6e12a`: STOP and run `/engine-bump`. Re-derive the rows on the desktop too; this scan is the laptop's install only.
- **The pattern scan fails.** Any pattern count differs from 1/1/0/1/1/0, the two loads read different globals, or the link check fails: STOP. Do not hand-edit RVAs.
- **The transpiler finds a different site count.** `MissionStartGuardBindingTests` reports a swap count other than 6 on v1.4.8: STOP.
- **The cull needs more than the hideout change.** The re-pinned NameplateCull sequences show a vanilla step the rule does not mirror beyond the hideout source and FerryTarget: STOP and re-review the cull, or ship WP 106 without it.
- **The license text is missing.** The MIT notice cannot be included in full in THIRD-PARTY-LICENSES.txt: STOP before release.

## 7. Corrections to the lens and classifier text

- **LOADING-HANG-06, FREEZE-THREADING-07.** "Byte-identical" overstates the evidence. Write instead: "the same instruction bytes at every matched site, the same struct layout and block sizes, the same 980-byte host function and every intra-function distance, and the same 4 callers". A whole-function comparison needs a 1.5.x DLL.
- **RUNTIME-PERF-11 and the ecaa67d26 classifier row.** The nameplate cull is not a straight port: v1.4.8's hideout range source differs (4.2), and the pinned sequence names a member v1.4.8 lacks.
- **The 15e52d7af classifier row.**
  - Its RepoPaths source commit is wrong (5.4).
  - The ClipBudget class needs two helper files, not the AnimMemory port.
  - The gate-fails-on-unlisted-build property needs plan 008's runsettings.
  - The full ClipBudget row is now derived (3.5).
- **LOADING-HANG-03.** SaveShield's MISSION-INIT finalizer on `FinishMissionLoading` swallows first; the Native2Managed finalizers are the outer net.

## 8. Open questions (UNVERIFIED)

- **Do 1.4.8 players actually reach the pool limit?** The watch writes its peak line at mission end, so a battle that froze for good writes no line. Only the guard's overflow counter on later missions, or a dump with a thread inside one of the four spin loops (3.4), would show it. A link to E3 is not established: the #612 lesson in brief2 points to a different wedge.
- **Entries per human skeleton on v1.4.8.** The value 28 is yotthani's v1.5.4 measurement and is display only. On v1.4.8 the count is the byte at `[rdi+0x44]` of the draw record.
- **How VanillaTuning detects TAOM**, and whether VanillaTuning runs on 1.4.8 at all.
- **Whether `CrashReportSettings.Instance` is non-null** at the first `OnBeforeInitialModuleScreenSetAsRoot` on 1.4.x.
- **The other two vanilla call lists** pinned by `NameplateCullBindingTests` (`RefreshBindValues`, `UpdateNameplateMT`). The decompile matches them by eye; the IL test must run.

## 9. Skills to recommend (the orchestrator runs them)

- `/research` on `SettlementNameplateVM` before porting the cull.
- `/verify-bindings` after the port, with `snapshot_api_surface.ps1 -Check`.
- `/native-crash-triage` for any frozen-battle dump: check the four spin RVAs.
- `/localize` for the two new strings (translations are optional; English renders inline).
- `/deep-review`, then `/ship`, per package.
- `/engine-bump` if the DLL hash changes.
