Plan 036: a read-only, signature-guarded probe that logs how much on-demand animation clip data the
engine holds against its 12 MiB budget during a mission, so the maintainer can decide on the clip
residency levers with a number instead of a guess. Diagnostics only: it reads two engine globals and
writes nothing into the engine.

VERIFIED ENGINE FACTS (TAOM's Ghidra project on the installed v1.5.3 client, TaleWorlds.Native.dll
14,209,376 bytes, 2026-10-02; written up in docs/reference/engine/mission-frame-threads-and-native-costs.md
section 6, which this plan's branch inherits from perf/engine-performance):
- The on-demand clip eviction pass is FUN_18021dea0 (RVA 0x21DEA0). It reads the global count of loaded
  on-demand clip bytes with `mov eax, dword ptr [rip + disp32]` at RVA 0x21E00F, bytes
  `8B 05 2B DE B8 00`, target 0x21E00F + 6 + 0xB8DE2B = RVA 0xDABE40 (a 32-bit int), and compares it with
  the budget through `subss xmm0, dword ptr [rip + disp32]` at RVA 0x21E034, bytes
  `F3 0F 5C 05 A0 02 91 00`, target RVA 0xB2E2DC, a float holding 12582912.0 (12 MiB) that no other
  instruction in .text references. While the count exceeds the budget it frees clips nobody is reading.
- `MBAnimation.IsAnyAnimationLoadingFromDisk()` (managed, TaleWorlds.MountAndBlade) is true while any
  on-demand clip is in state 1 (loading) (native 0x6EAAE0).
- TAOM's rule for native work (.claude/rules/native-cpp-ports.md rule 3): never trust a fixed offset;
  locate code by an independent signature scan. So the probe must FIND the two instructions by scanning
  the loaded module's .text for a byte pattern with the rip displacements wildcarded (the pattern must
  include enough surrounding bytes of the eviction function to match exactly once; build it from the
  disassembly with `python tools/native_sig_author.py disasm 0x21DFFF --n 20` and check uniqueness with
  `python tools/native_sig_author.py scan "<pattern>"`), then compute each target from its displacement,
  and finally check the budget float equals 12582912.0. Any check failing (no match, two matches, a
  different float) disables the probe for the process with one INFO line saying why. It never guesses.

WHAT:
1. An adapter (Main/Adapters/, ADR-007) that gets the native module's base and size in the running
   process (GetModuleHandle plus the PE headers in memory, or the module list), exposes a byte span of
   its .text for scanning, and reads an aligned 32-bit int and a float at a computed address (aligned
   32-bit reads are atomic on x64, so a value the engine is writing cannot tear). Its interface is the
   test seam.
2. A pure service: pattern scan with wildcards over a byte array (unique match required), rip-relative
   target computation, validation, and the line format. Unit-tested on synthetic byte arrays.
3. A thin mission behaviour (or a slot in the existing MissionPerf heartbeat, whichever keeps ADR-002)
   that, when the new MCM toggle EnableAnimMemoryProbe (default TRUE per the maintainer's instruction that taom_debug is a critical, comprehensive log (plans/_audit/2026-10-02-perf/DECISIONS.md D6, D7): the probe is read-only, two aligned 32-bit reads a second after a one-time signature scan; Battle Load Diagnostics page;
   exclude it from the co-op settings fingerprint as EnableMissionPerfHeartbeat is excluded) is on,
   samples once per second: loaded bytes and IsAnyAnimationLoadingFromDisk (through an adapter), and
   logs `[AnimMem] t=+<s>s loadedKB=<n> budgetKB=<n> pctOfBudget=<n> loadingNow=<0|1> drops=<n>` every 5 s
   (drops = how many one-second samples fell below the previous one: a proxy for evictions), plus one
   summary line at mission end with the peak, the seconds at or above 90% of the budget, and the drop
   count. The signature work runs once per process, lazily, off the frame where possible (it scans about
   10 MB once; measure it and log its ms).
4. Docs: a section in the docs/features page plan 028 creates or extends (or docs/features/mission-perf-heartbeat.md),
   the registry if a patch is added (none should be needed), and a line in the engine reference page's
   section 6 pointing to the probe.

TESTS: the scan (unique match, no match, two matches, wildcards), the rip-relative computation against
the two real instruction encodings above, the float check, the disabled path, the 5 s and summary line
formats, the drop counter. Not testable offline: the live module read (Not-tested trailer).

OUT OF SCOPE: writing any engine memory (raising the budget is the maintainer's decision, FOR-MIKE),
Harmony patches, MinHook, anything under Dependencies/.

STOP conditions to include: the pattern cannot be made unique; the computed targets disagree with the
RVAs above on the installed binary; the managed P/Invoke surface needed is not available on net472.

LOGGING (D6): a configuration header when the probe arms (module base found, each signature matched at which RVA, the budget value read, the scan's cost in ms) or one reason line when it disables itself; the 5 s lines; the mission-end summary.
