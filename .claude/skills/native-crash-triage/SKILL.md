---
name: native-crash-triage
description: Root-cause native Bannerlord CTDs (AccessViolation in TaleWorlds.Native.dll) and hangs, via Event Log offsets, hang dumps, Ghidra decompile and live debugger forensics. No symbols needed.
---

# Native Crash Triage

Names the crash site of a native CTD **without symbols** and drives it to a root cause. Proven
on the 2026-06-12 v1.4.6 spider campaign: three distinct native sites
(`Agent_ai::set_attack_entity`, the `monster_usage.cpp` jump map, the `Die`-path record
corruption) named and fixed in one day with exactly this protocol.

**When to use:** any `0xC0000005` / `System.AccessViolationException` whose stack dies in
`TaleWorlds.Native.dll` (or another native module). For managed TAOM bugs use `/investigate`
instead — this skill is the native-side complement (and `/investigate` may hand off here).

**Iron rule (from `/investigate`):** no fixes without a named site + root cause. Every fix this
protocol has produced was DATA (XML) or a routing patch — never a blind retry.

## Phase 1 — Collect (no debugger needed)

1. **Windows Event Log gives the faulting module + offset even after a CTD:**
   ```powershell
   Get-WinEvent -FilterHashtable @{LogName='Application'; ProviderName='Application Error'; StartTime=(Get-Date).AddHours(-6)} |
     Where-Object { $_.Message -match "Bannerlord" } |
     ForEach-Object { ($_.Message -split "`n" | Select-Object -First 8) -join "`n"; "---" }
   ```
   The `Fault offset` IS the RVA. **Compare offsets across runs** — identical offset = same
   site (discriminates "my fix didn't work" from "a different crash"); this is how Patch47 was
   exonerated. Caveat: a crash held by a debugger never reaches WER: no Event Log entry. That
   includes Visual Studio (Detach All at the exception, never Stop, or the process dies unrecorded;
   its `$exception._ip` still gives the offset) and TaleWorlds' own `Watchdog.exe -p <pid>`, which
   the launcher starts and which blocks procdump from attaching.
2. **Player crash bundles (no reporter Event Log):** TW's CrashDumper drops a minidump
   (`dump.dmp`) in the crash-report bundle. `python tools/native_crash_triage.py --dump
   <bundle>/dump.dmp` names the faulting module + RVA and the commit split (total / image /
   private / mapped) directly from the bundle — no Event Log needed — and chains into Phase 2's
   disassembly when the faulting module is the local `--dll`. **Build-version caveat:** RVAs
   transfer to a local disassembly only when the reporter's Build Source matches the installed
   build — compare the bundle's build line against the local install; the tool prints both the
   dump-side module path and the local `--dll` it disassembles, so a mismatch is visible.
3. Game-side timeline: newest `Logs/taom_debug_*.log` (game bin) + newest
   `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_*.txt` — last lines date the
   crash relative to gameplay events (charge orders, scene loads).
4. **Map an implicated patch to its owner.** If a `[BattleLoad]` / `[SaveLoad]` / `_PatchN`
   marker in those logs, or the last managed frame before the native transition, names a TAOM
   patch, grep it in [`docs/reference/harmony-patch-registry.md`](../../../docs/reference/harmony-patch-registry.md)
   — it maps the patch to its exact target method + status, so you know whether a TAOM hook sits
   on the crashing path before blaming the engine. (This is where CLAUDE.md's former "Harmony
   Patch Categories" table now lives.)
5. **Assertion dialogs (engine asserts, not AVs):** the dialog is a PAUSED pre-crash state —
   before clicking anything, copy the session's `rgl_log_<pid>`/`watchdog_log_<pid>` AND take a
   full dump (Task Manager → Create dump file, or `rundll32 comsvcs.dll, MiniDump <pid> <path> full`).
   Very early asserts (module scan) leave a 0-byte watchdog log and NO rgl_log — the dump is then
   the only artifact. Post-**Ignore** Event-Log offsets name the SECONDARY (abort-path) site, not
   the assert site. Never Ignore a queue/invariant assert to keep working — state is corrupted
   (2026-07-24 `rglConcurrentQueue`: Ignore → permanent loading-screen hang). Editor crashes:
   pass `--dll` pointing at the **Win64_Shipping_wEditor** `TaleWorlds.Native.dll` — offsets
   differ from the shipping client build.
6. **Any derived offset is valid only against the engine version its binary carries — check the
   wEditor version BEFORE trusting one.** `Win64_Shipping_wEditor` (the Modding Kit build) updates
   on its OWN Steam schedule and can sit at a different engine version than the shipping client the
   crash came from. It did: on 2026-08-10 it jumped `v1.4.5.114928` → `v1.4.8.119303` — three
   engine versions in one update — while the client went v1.4.7 → v1.4.8 (`TaleWorlds.Library`
   `GameVersion` in `_editor_build_v1.4.5` vs `_editor_build`). Every offset ever derived from that
   binary before then was against a v1.4.5 image, and it moved with no signal. Even at a matching
   version string the builds are not identical (v1.4.5 client `.115026` vs v1.4.5 editor `.114928`).
   **So: read `<game>/bin/Win64_Shipping_wEditor/Version.xml` and compare it against the crash
   report's `BannerlordVersion` before believing any RVA you derive from it.** A mismatch means the
   offset does not transfer — the same rule as step 2's `--dump` build-version caveat, applied to
   the editor binary.
7. **Steam overwrites in place, so old native images do not survive — archive one before each
   engine bump.** The only `TaleWorlds.Native.dll` copies on this machine are the two live ones
   under `bin/Win64_Shipping_{Client,wEditor}`, and both are now v1.5.2 (rewritten 2026-09-14; the archived `.dll` twins under `E:\Decompiled_Bannerlord\_native` are the exception); nothing v1.4.5 through v1.5.0
   is preserved in the repo or under `E:\Decompiled_Bannerlord\` (the decompile stack holds `.cs`
   for managed assemblies only — native modules are merely *listed* in `_native_dlls.txt`).
   Consequence: the open player report `crashz/report.json` (untracked; `git show b2e387db:crashz/report.json`; `BannerlordVersion v1.4.7.117484`)
   **cannot currently be triaged locally** — there is no v1.4.7 image to disassemble against.
   Copy both `TaleWorlds.Native.dll` files aside before the next update lands.

## Phase 2 — Name the site (offline, fully scripted)

```bash
python tools/native_crash_triage.py --rva 0x<fault_offset>
# or, from a live debugger IP + module base:
python tools/native_crash_triage.py --ip 0x<RIP> --base 0x<module_base> --callers 2
# or, from a player bundle's minidump (no Event Log needed):
python tools/native_crash_triage.py --dump <bundle>/dump.dmp
```

Output: exact `.pdata` function bounds, annotated hexdump, **every string the function
references** (shipping builds keep assert/trace text — functions frequently self-identify),
and the caller chain with each caller's strings. `--dump` first prints the exception
(code / thread / parameters), faulting module + RVA, the commit summary from MemoryInfoList,
and a return-address stack scan of the faulting thread (locating the stack through MemoryList
when the per-thread descriptor Rva is 0, which is what TW's CrashDumper writes), then chains
into this same pipeline when the faulting module matches `--dll` — otherwise it prints the
module + RVA and the rerun hint (`--dll` pointing at a local copy of that module). Then decompile
the function with `python tools/native_decompile.py --rva 0x<fault_offset>` (the triage output ends
with the exact line, also when the RVA is in a leaf function that has no `.pdata` entry, which
triage cannot bound; `--callers 1` adds the callers' C; the first run on a new binary analyses it
for minutes, once: [ghidra-native-decompile.md](../../../docs/features/ghidra-native-decompile.md)).
Read the crash row against the C, and hand-decode the instructions only when Ghidra is absent. When
the site implements a managed engine call, the output says which (`engine method: IMBAgent.X = x`):
that is the managed call TAOM can see, and `pwsh tools/taom-src.ps1 path <Type>` can follow it from there. The
common patterns:
- `cmp [reg+disp], imm` with reg=0 → **null + field-offset** (missing data surface)
- chain-walk loop (`cmp r10d,[rax]` / `mov rax,[rax+8]`) ending in a deref → **hash-map miss
  dereferencing its end-sentinel** (asserts compiled out of shipping) → a DATA TABLE is missing
  a key. Fix: make the table TOTAL (see `feedback_engine_lookup_total_key_coverage` memory)
  **The missing key is usually still in a register:** `--dump` prints the faulting thread's registers
  (the melee-table miss at +0x6590B9 keeps its clip index in `r9`). An in-game enumeration names it:
  log every action whose `MBActionSet.GetAnimationIndexOfAction` equals the key (the removed
  `TrollActionTrace.LogCrashKeys`, `git show 25995dc9:Main/Features/TrollBruteForce/TrollActionTrace.cs`,
  line 96). Then read the table's BUILDER before changing any data: xref the table global and name
  the owning class from its vtable's RTTI (`tools/native_sig_author.py xref` / `rtti`), then read the
  insert's condition in the builder's C (`native_decompile.py --rva <builder>`). It gives the data rule in one
  read, where crash-by-crash guessing does not (the melee table's rule, the Kit's "Blends with
  animation" box, TpacTool's `UnknownClipName`:
  [bannerlord-animation-system-map.md](../../../docs/reference/bannerlord-animation-system-map.md) section 3).
- faulting address ≈ heap, or an index register holding float bits → **corrupted record**
  consumed downstream; check binding targets (phantom-animation sweep) and route around if
  engine-internal (Patch47 pattern)

## Phase 2b: A hang (the game spins, nothing crashes)

No exception means no Event Log offset and no stream `--dump` can decode, so take the stack from a
full dump (proven on #599):

1. **Confirm the process is the game.** The Modding Kit also runs as
   `TaleWorlds.MountAndBlade.Launcher.exe`, from `bin\Win64_Shipping_wEditor`, and legitimate editor work (the
   settlement distance cache) sits at "Not Responding" for minutes, so a process name proves nothing. Read the
   image path, `(Get-CimInstance Win32_Process -Filter "ProcessId=<pid>").ExecutablePath`: the game runs from
   `bin\Win64_Shipping_Client`. Match the PID to the session's `rgl_log_<pid>.txt` in
   `C:\ProgramData\Mount and Blade II Bannerlord\logs`, whose last lines also show whether the game ended
   normally. Never tell the user to end a process you have not identified.
2. **Spot the spin.** `Get-Process Bannerlord` kept `Responding` true through a game-loop spin
   (#599), so do not wait for "Not Responding". Sample `Threads[].TotalProcessorTime` twice, 2 to 3 s apart: the game-loop thread holds
   nearly all the lifetime CPU and is still climbing. `[MemSample]` lines prove nothing (a timer).
3. **Dump.** `procdump -accepteula -ma <pid> E:\<dir>\hang.dmp` (Sysinternals, on PATH; about 11 GB).
4. **Stack.** `WinDbgX -z <dmp> -c '$$><E:\<dir>\stack.wds'`, where the script (written with the Write
   tool, never a heredoc) opens with `.logopen <log>`, runs `~~[0x<tid>]s; k 60; .loadby sos clr;
   !clrstack -a` and ends `.logclose; q`. Poll the log, then `Stop-Process DbgX.Shell` (the window
   outlives `q`). Never put a quoted `.printf` on the `-c` line: WinDbgX splits its own command line
   on the quotes. Set `_NT_SYMBOL_PATH=srv*E:\symcache*https://msdl.microsoft.com/download/symbols`
   for ntdll and kernel frames (caches live on E:, never C:).
5. **Read the native frames as C.** Each `TaleWorlds_Native+0x<off>` frame's offset is an RVA:
   `python tools/native_decompile.py --rva 0x<off>`. Read the stuck loop's exit condition in the C,
   then find what should have set it.
   Known on v1.5.4: a battle frozen with threads spinning at `TaleWorlds_Native+0x69DA4` to `+0x69DAA` or
   `+0x69D90` to `+0x69D96` is a full first skeleton pool; at `+0x6AFD7` to `+0x6AFDD` or `+0x6AFF0` to
   `+0x6AFF6`, a full second pool. A `movzx`/`test`/`jne` spin a little after another `lock xadd` can be one of
   the engine's nine other unbounded per-frame pools: section 10 of
   [mission-frame-threads-and-native-costs.md](../../../docs/reference/engine/mission-frame-threads-and-native-costs.md)
   lists them with their addresses. TAOM guards the first two: read the session's `[SkeletonBuffer] guard`
   lines (`guard ON pool N`, `guard OFF pool N: <reason>`, or one `guard OFF: <reason>` when the whole install
   stopped), since a hang in pool 1 or 2 means that pool's guard was off or another module's. A frame or fault address in an anonymous page
   just below `TaleWorlds.Native.dll` is one of the guard's code blocks (43 and 42 bytes), which have no unwind
   information.

Heap values from the same log: `!do <obj>` for fields, `!DumpArray -details -length 3` for a struct
array's layout, and `da poi(<address>)` to print the ASCII text a pointer stored at `<address>`
points to.

## Phase 3 — Live debugger forensics (when a repro is available)

1. **Attach mixed-mode:** VS → Debug → Attach to Process → select **`Bannerlord.exe`** (NEVER
   `TaleWorlds.MountAndBlade.Launcher.exe`) → Code type: **Managed (.NET Framework 4.x) +
   Native** both checked. Or set the launch profile's Debug engines to "Managed (.NET
   Framework) with native" and F5.
2. On break: **Call Stack** (copy entire — native frames now visible), **Registers**
   (double-click the TOP native frame first; the registers shown belong to the SELECTED frame),
   **Modules window** (Ctrl+Alt+U) for the module base → `RVA = RIP − base`.
3. **Managed probes** (select a MANAGED frame first — C# evaluation is blocked while a native
   frame is selected; `$exception` exists only in managed frames). The Immediate window accepts
   lambdas via explicit `System.Linq.Enumerable` calls:
   ```
   System.Linq.Enumerable.Count(System.Linq.Enumerable.Where(TaleWorlds.MountAndBlade.Mission.Current.AllAgents, a => a.Monster != null && a.Monster.StringId == "<id>"))
   this.GetCurrentAction(0).GetName()        // v1.4.6: GetCurrentAction, NOT GetCurrentActionValue
   ```
   Corrupted/interleaved action names or `act_none` on a moving agent's channel 0 = poisoned
   action records. Module base via probe:
   `...Cast<System.Diagnostics.ProcessModule>(System.Diagnostics.Process.GetCurrentProcess().Modules), m => m.ModuleName == "TaleWorlds.Native.dll")).BaseAddress`.
4. ASLR: module bases hold within a boot for repeated launches in practice, but ALWAYS
   re-derive the base per process — never reuse across launches.

## Phase 4 — Fix and verify

Data-table misses → make the table total (extra rows are inert; missing keys crash). Flag-driven
AI paths → match the proven baseline exactly (`tools/audit_mount_parity.py` for mounts).
Engine-internal corruption → route around (Patch47 dismount-before-death pattern). Then a
control battle that exercises the trigger, and compare the next Event Log offset if it still
crashes — a NEW offset is progress, not failure.

Full worked example with all three patterns: `docs/features/spider.md` ("The v1.4.6 engine-bump
campaign"); generalized lessons: memory `feedback_engine_lookup_total_key_coverage`.
