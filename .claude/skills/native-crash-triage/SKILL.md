---
name: native-crash-triage
description: Root-cause native Bannerlord CTDs (AccessViolation in TaleWorlds.Native.dll) via Event Log offsets, offline disassembly, and live debugger forensics. No symbols needed.
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
   exonerated. Caveat: a crash held by a debugger never reaches WER — no Event Log entry.
2. Game-side timeline: newest `Logs/taom_debug_*.log` (game bin) + newest
   `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_*.txt` — last lines date the
   crash relative to gameplay events (charge orders, scene loads).
3. **Map an implicated patch to its owner.** If a `[BattleLoad]` / `[SaveLoad]` / `_PatchN`
   marker in those logs, or the last managed frame before the native transition, names a TAOM
   patch, grep it in [`docs/reference/harmony-patch-registry.md`](../../../docs/reference/harmony-patch-registry.md)
   — it maps the patch to its exact target method + status, so you know whether a TAOM hook sits
   on the crashing path before blaming the engine. (This is where CLAUDE.md's former "Harmony
   Patch Categories" table now lives.)

## Phase 2 — Name the site (offline, fully scripted)

```bash
python tools/native_crash_triage.py --rva 0x<fault_offset>
# or, from a live debugger IP + module base:
python tools/native_crash_triage.py --ip 0x<RIP> --base 0x<module_base> --callers 2
```

Output: exact `.pdata` function bounds, annotated hexdump, **every string the function
references** (shipping builds keep assert/trace text — functions frequently self-identify),
and the caller chain with each caller's strings. Hand-decode the few instructions around the
crash row (the tool shows them); the common patterns:
- `cmp [reg+disp], imm` with reg=0 → **null + field-offset** (missing data surface)
- chain-walk loop (`cmp r10d,[rax]` / `mov rax,[rax+8]`) ending in a deref → **hash-map miss
  dereferencing its end-sentinel** (asserts compiled out of shipping) → a DATA TABLE is missing
  a key. Fix: make the table TOTAL (see `feedback_engine_lookup_total_key_coverage` memory)
- faulting address ≈ heap, or an index register holding float bits → **corrupted record**
  consumed downstream; check binding targets (phantom-animation sweep) and route around if
  engine-internal (Patch47 pattern)

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
