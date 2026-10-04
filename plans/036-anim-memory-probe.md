# Plan 036: Log on-demand animation clip memory against the engine's 12 MiB budget

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Work in the worktree and on the branch you were given. This plan's work
> lands on its own branch: never commit it onto a branch that carries another plan's work. The
> orchestrator keeps `plans/README.md`; do not edit it.
>
> **Drift check (run first)**:
> `git diff --stat 0d1e91f0..HEAD -- Main/Features/MissionPerf Main/Composition/FeatureModules.cs Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs Main/Features/CoopInterop/CoopSettingsRelevance.cs Main/Adapters/INativeModuleMemoryAdapter.cs Main/Adapters/NativeModuleMemoryAdapter.cs Main/Adapters/IAnimationLoadingAdapter.cs Main/Adapters/AnimationLoadingAdapter.cs TAOM.Tests/Features/MissionPerf TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs docs/features/mission-perf-heartbeat.md docs/features/coop-interop.md docs/features/bannerlord-together-compat.md docs/reference/feature-map.md docs/reference/engine/mission-frame-threads-and-native-costs.md`
> If an in-scope file changed since this plan was written, compare the "Current state" excerpts with
> the live code; a mismatch is a STOP condition, with one exception spelled out in Step 9: if plan
> 028 (tick profiler) has landed under you, the settings counts quoted below are higher, and you use
> the numbers the failing test reports instead. One hit is expected and is not a mismatch: commit
> `4a909f20` edited `docs/reference/engine/mission-frame-threads-and-native-costs.md` in section 7
> only; section 6, the part this plan quotes and extends, is unchanged.
> Also run `cat .claude/pinned-game-version.txt` (must print `v1.5.3`) and
> `ls -l "$BANNERLORD_GAME_DIR/bin/Win64_Shipping_Client/TaleWorlds.Native.dll"` (size must be
> `14209376`; if `BANNERLORD_GAME_DIR` is unset, use the `DEFAULT_DLL` path in
> `tools/native_sig_author.py`). Either differing is a STOP: every native fact below was read from
> that binary.

## Status

- **Priority**: P1
- **Effort**: M (new pure code, two small adapters, one mission behaviour, wiring, docs; one commit)
- **Risk**: LOW (read-only: after a one-time signature check and one budget read, one aligned 32-bit
  read and one `IsAnyAnimationLoadingFromDisk` call per second; it writes nothing into the engine and
  disables itself on any mismatch)
- **Depends on**: none
- **Category**: perf (a measurement that decides a perf lever)
- **Planned at**: commit `0d1e91f0`, 2026-10-02
- **Baseline at that commit**: dotnet `Failed! - Failed: 1, Passed: 12345, Skipped: 2, Total: 12348`
  (net472), measured at `dffdf879` and unchanged at `0d1e91f0` (docs and plans only since). Failing:
  `EveryLanguage_DeclaresARowForEveryEnglishKey` (English keys without rows in the other languages; the
  paid translator run waits on the maintainer). Python suite: not needed (this plan touches no
  `tools/` file). RefAsm unit step (Commands): not measured by the writer; Step 1 records it.
- **Issue**: filed by the orchestrator before execution

## Why this matters

Bannerlord keeps animation clips marked Loading Type 1 or 2 "on demand": a worker that samples an
unloaded clip blocks until it loads, which delays the parallel agent tick and shows up as a main-thread
frame spike. The engine holds only **12 MiB** of such clip data and evicts clips past that, so a battle
whose working set is larger keeps evicting and reloading. Vanilla ships hundreds of these clips that
TAOM battles play, and TAOM's hill troll and elephant attack clips are type 2. Two levers exist (make
TAOM's hot clips resident, or raise the 12 MiB budget engine-wide), and both are the maintainer's call;
neither should be pulled on a guess. This plan adds a read-only probe that logs, once a second, how
many bytes of on-demand clip data the engine holds against that budget, whether a clip is loading
right now, and how often the total fell (clips evicted), so one troll-heavy battle and one large
vanilla battle give the number the decision needs.

## Current state

### Engine facts (TAOM-verified on the installed v1.5.3 client, re-derived by this plan's writer)

Re-derived on 2026-10-02 against `TaleWorlds.Native.dll`, 14,209,376 bytes, image base `0x180000000`,
with `python tools/native_sig_author.py` and a brute-force disp32 scan of `.text`.

- **The eviction pass** is the function at RVA `0x21DEA0`. Disassembly (`python -B
  tools/native_sig_author.py disasm 0x21DFFF --n 30`; the first two lines are a misaligned decode and
  are not instructions):
  ```
  0x0021E00A  E8 71 01 00 00               call 0x18021e180
  0x0021E00F  8B 05 2B DE B8 00            mov eax, dword ptr [rip + 0xb8de2b]
  0x0021E015  41 8B EC                     mov ebp, r12d
  0x0021E018  48 8B 3D 09 DE B8 00         mov rdi, qword ptr [rip + 0xb8de09]
  0x0021E01F  48 2B 3D FA DD B8 00         sub rdi, qword ptr [rip + 0xb8ddfa]
  0x0021E026  48 C1 FF 04                  sar rdi, 4
  0x0021E02A  83 EF 01                     sub edi, 1
  0x0021E02D  66 0F 6E C0                  movd xmm0, eax
  0x0021E031  0F 5B C0                     cvtdq2ps xmm0, xmm0
  0x0021E034  F3 0F 5C 05 A0 02 91 00      subss xmm0, dword ptr [rip + 0x9102a0]
  0x0021E03C  F3 44 0F 2C F8               cvttss2si r15d, xmm0
  0x0021E041  0F 88 1E 01 00 00            js 0x18021e165
  ```
  The `mov eax` at `0x21E00F` (6 bytes, disp32 at instruction offset 2) loads the global loaded-bytes
  counter: target `0x21E00F + 6 + 0xB8DE2B = 0xDABE40`, a 32-bit signed int in `.data` (in the
  zero-filled tail past the section's raw data, so it has no value on disk). The `subss` at `0x21E034`
  (8 bytes, disp32 at instruction offset 4) subtracts the budget: target `0x21E034 + 8 + 0x9102A0 =
  0xB2E2DC`, a float in `.rdata` whose bytes are `00 00 40 4B` = `12582912.0` (12 MiB). While the
  counter exceeds the budget the pass frees clips nobody is reading.
- **The signature.** This 50-byte pattern (the four rip displacements wildcarded) matches **exactly
  once** in `.text`, at RVA `0x21E00F`:
  ```
  8B 05 ? ? ? ? 41 8B EC 48 8B 3D ? ? ? ? 48 2B 3D ? ? ? ? 48 C1 FF 04 83 EF 01 66 0F 6E C0 0F 5B C0 F3 0F 5C 05 ? ? ? ? F3 44 0F 2C F8
  ```
  The load instruction is at pattern offset 0 (disp32 at offset 2); the `subss` is at pattern offset 37
  (`0x21E034 - 0x21E00F = 0x25`; disp32 at offset 41). The real bytes at that site are:
  ```
  8B 05 2B DE B8 00 41 8B EC 48 8B 3D 09 DE B8 00 48 2B 3D FA DD B8 00 48 C1 FF 04 83 EF 01 66 0F 6E C0 0F 5B C0 F3 0F 5C 05 A0 02 91 00 F3 44 0F 2C F8
  ```
  Shorter variants are also unique today (for example `8B 05 ? ? ? ? 41 8B EC`), but the full 50 bytes
  keep a future build from matching an unrelated `mov eax` by accident.
- **Every rip-relative reference to the two targets** (brute-force disp32 scan of all of `.text`):
  budget float `0xB2E2DC`: only the `subss` at `0x21E034`. Counter `0xDABE40`: reads at `0x21E00F`
  (eviction) and `0x830A3` (function `0x82BE0`, converts it to float); writes are `lock xadd` at
  `0x591319` (function `0x5911A0`: adds the clip's size field `+0x80` after a load) and `lock xadd`
  at `0x21E0EF` (eviction: adds the negated freed bytes). So the counter is a byte total kept with
  atomic adds; a falling sample means the eviction pass ran. A write through a register pointer cannot
  be ruled out by this scan. Also observed, not relied on: after its add, `0x5911A0` compares the new
  total with `0xF00000` (15 MiB, at `0x591327`) and above it sets a once-flag with `lock cmpxchg` and
  hands an object to `0x44400`; that this schedules the eviction pass is an inference, not verified. So
  **`pctOfBudget` above 100 is expected**, not an error.
- **PE layout of that DLL** (from its headers): `e_lfanew = 0x198`, Machine `0x8664`, 8 sections,
  `SizeOfOptionalHeader = 0xF0` (PE32+, magic `0x20B`), `SizeOfHeaders = 0x400`, section table at
  `0x198 + 24 + 0xF0 = 0x2A0`, 40 bytes per entry (Name 8 bytes, then VirtualSize at +8, VirtualAddress
  +12, SizeOfRawData +16, PointerToRawData +20, all little-endian uint32):
  | Section | VirtualAddress | VirtualSize | PointerToRawData | SizeOfRawData |
  |---|---|---|---|---|
  | `.text` | `0x1000` | `0xA240CC` | `0x400` | `0xA24200` |
  | `.rdata` | `0xA27000` | `0x2CDD9E` | `0xA24E00` | `0x2CDE00` |
  | `.data` | `0xCF5000` | `0x16FD68` | `0xCF2C00` | `0x2E000` |
  (`.rodata`, `.pdata`, `_RDATA`, `.rsrc`, `.reloc` also exist.) Inside a section the raw and the
  mapped layouts agree, so offset `i` into the raw `.text` bytes is RVA `0x1000 + i`.
- **Managed probe of loading** (`pwsh tools/taom-src.ps1 path TaleWorlds.MountAndBlade.MBAnimation`,
  v1.5.3, lines 143-146):
  ```csharp
  public static bool IsAnyAnimationLoadingFromDisk()
  {
      return MBAPI.IMBAnimation.IsAnyAnimationLoadingFromDisk();
  }
  ```
  `MBAnimation` is a `public struct` in namespace `TaleWorlds.MountAndBlade`. Natively (`0x6EAAE0`) it
  walks the on-demand clip records and returns true while any is in state 1 (loading).
- **When `AfterStart` runs** (`pwsh tools/taom-src.ps1 path TaleWorlds.MountAndBlade.MissionState`,
  `FinishMissionLoading`, lines 333-351): `CurrentMission.AfterStart()` runs between
  `Utilities.SetLoadingScreenPercentage(0.48f)` and `(0.56f)`, under the loading screen and before the
  first `Mission.OnTick`. A one-time scan there costs load time, not a battle frame. `MissionBehavior`
  (same decompile): `public virtual void AfterStart()`, `public virtual void OnMissionTick(float dt)`,
  `protected virtual void OnEndMission()`.
- **Reading safely.** On .NET Framework 4.x an `AccessViolationException` is a corrupted-state
  exception that a plain `catch` does not catch: a read of an unmapped address kills the game. So every
  address the probe reads must first be proven inside a mapped section of the module (the `.text`
  range it copies, the `.data` and `.rdata` targets). An aligned 32-bit read is one load on x64, so a
  value the engine is writing cannot tear. Net472 surface used: `[DllImport("kernel32")]
  GetModuleHandleW`, `Marshal.Copy(IntPtr, byte[], int, int)`, `Marshal.ReadInt32(IntPtr)`,
  `BitConverter.ToInt32`, `BitConverter.ToSingle(BitConverter.GetBytes(int), 0)`.
  `BitConverter.Int32BitsToSingle` does **not** exist on net472; do not use it.

### TAOM code at `0d1e91f0`

- `Main/Features/MissionPerf/Hooks/MissionPerfHeartbeatBehavior.cs` (119 lines): the exemplar this
  plan's behaviour mirrors. A `public sealed class ... : MissionLogic` taking `IModLogger`, timing with
  `Stopwatch.GetTimestamp()`, reading its toggle as
  `BattleLoadDiagnosticsSettings.Instance?.EnableMissionPerfHeartbeat ?? true` (line 57), and
  self-disabling after one exception (lines 83-88):
  ```csharp
  catch (Exception ex)
  {
      _disabled = true;
      try { _logger.LogError($"[MissionPerf] heartbeat disabled for this mission after {ex.GetType().Name}: {ex.Message}"); }
      catch { /* diagnostic only */ }
  }
  ```
  It is hand-wired at `Main/SubModule.cs:2112`. This plan does **not** touch `SubModule.cs`.
- `Main/Features/MissionPerf/MissionPerfLine.cs`: the exemplar for a pure line builder
  (`string.Format(CultureInfo.InvariantCulture, "[MissionPerf] t=+{0:0}s frames={1} ...", ...)`).
- `Main/Composition/FeatureModules.cs` lines 14-25: the feature-module list. New features append a
  module here instead of editing `IoC.cs` and `SubModule.cs`:
  ```csharp
  internal static readonly TaomFeatureModule[] All =
  {
      new Features.WandererAllegiance.WandererAllegianceModule(),
      new Features.CreatureBandits.CreatureBanditsModule(),
      new Features.ArmourAcquisition.ArmourAcquisitionModule(),
      new Features.RealmBorders.RealmBordersModule(),
      new Features.BattleCorpses.BattleCorpsesModule(),
      new Features.TournamentRewards.TournamentRewardsModule(),
  };
  ```
  Modules' mission behaviours are added at every mission start by
  `FeatureModuleHooks.AddMissionBehaviors` (`Main/SubModule.cs:2121`), after the hand-wired ones.
- `Main/Features/BattleCorpses/BattleCorpsesModule.cs`: the module exemplar to copy:
  ```csharp
  internal sealed class BattleCorpsesModule : TaomFeatureModule
  {
      private static readonly MissionBehaviorDecl[] Missions =
      {
          MissionBehaviorDecl.Of((_, r) => new BattleCorpseMissionBehavior(
              r.Resolve<BattleCorpsePolicy>(),
              r.Resolve<IGraphicsOptionsAdapter>(),
              r.Resolve<IModLogger>())),
      };

      public override string Id => "BattleCorpses";

      public override void RegisterServices(IRegistrator registrator)
      {
          registrator.Register<IBattleCorpseSettingsProvider, BattleCorpseSettingsProvider>(Reuse.Singleton);
          ...
      }

      public override IReadOnlyList<MissionBehaviorDecl> MissionBehaviors => Missions;
  ```
  Its usings (`BattleCorpsesModule.cs:1-6`): `System.Collections.Generic`, `DryIoc`, `TAOM.Adapters`,
  `TAOM.Composition`, `TAOM.Core.Logging`, and its own `Hooks` namespace. Its tests,
  `TAOM.Tests/Features/BattleCorpses/BattleCorpsesWiringTests.cs`, are the wiring-test exemplar
  (`FeatureModules.All.OfType<BattleCorpsesModule>().Count()`, `decls[0].BehaviorType`, a DryIoc
  `new Container()` with `container.RegisterInstance(Substitute.For<IModLogger>())`); their usings
  (lines 1-10) are `System.Linq`, `DryIoc`, `Microsoft.VisualStudio.TestTools.UnitTesting`,
  `NSubstitute`, `TAOM.Adapters`, `TAOM.Composition`, `TAOM.Core.Logging`, the feature's two
  namespaces, and `TAOM.Tests.Infrastructure` (for `RepoPaths.ReadSource(string relativePath, bool
  stripComments = false)`, `RepoPaths.cs:34`).
- Namespaces at `0d1e91f0`: `TAOM.Features.MissionPerf` and `TAOM.Features.MissionPerf.Hooks` exist;
  `TAOM.Features.MissionPerf.AnimMemory` does not until Step 4 creates it, and
  `TAOM.Features.MissionPerf.AnimMemory.Hooks` not until Step 9. Other namespaces the new tests need:
  `TAOM.Features.BattleLoadDiagnostics`, `TAOM.Features.CoopInterop`, `TAOM.Tests.Migration`.
- **Compiler behaviour the RED gates rely on** (proven by a scratch compile with the SDK on this
  machine): a `using` that names a namespace that does not exist yet fails with CS0234 ("'AnimMemory'
  does not exist in the namespace 'TAOM.Features.MissionPerf'"), and while such a declaration error
  stands the compiler reports no method-body errors at all, so a RED build may show only the CS0234
  lines. Once the namespace exists, a missing class used by name gives CS0103, a missing type CS0246,
  a missing property on an instance CS1061, and a missing property inside `nameof(Type.Member)` CS0117.
- `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs` (62 lines), the MCM page
  "Battle Load Diagnostics", `FormatType => "json2"`, every toggle default ON ("diagnose now"). Its last
  group, lines 58-61:
  ```csharp
  [SettingPropertyGroup("Mission Performance")]
  [SettingPropertyBool("Enable Mission Frame-Time Heartbeat", Order = 0, RequireRestart = false,
      HintText = "Writes a [MissionPerf] line to the TAOM debug log every 5 seconds ...")]
  public bool EnableMissionPerfHeartbeat { get; set; } = true;
  ```
  MCM labels on this page are plain strings (no `{=KEY}`), as on every TAOM MCM page, so no
  localization step applies.
- `Main/Features/CoopInterop/CoopSettingsRelevance.cs`, `Instrumentation` set, lines 70-72:
  ```csharp
  // The doctrine status line and the [MissionPerf] heartbeat; EnableCultureDoctrine itself
  // changes which tactics an AI team can pick and stays relevant.
  "CultureDoctrineDebug", "EnableMissionPerfHeartbeat",
  ```
- `TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs`, `EverySettingsClass_HasItsSplitPinned`
  (lines 202-214): `AssertSplit(typeof(BattleLoadDiagnosticsSettings), reflected: 9, covered: 0);` at
  line 211. Its comment says the pinned count is the guard that forces a new setting to be classified,
  so moving it together with the classification is the designed procedure, not a loosened gate.
  `EveryDocQuotingTheSettingsCounts_AgreesWithReflection` (lines 216-252) requires
  `docs/features/coop-interop.md` and `docs/features/bannerlord-together-compat.md` to contain
  `**<total>**` or ` <total> MCM settings`, and `**<relevant> are simulation-relevant**`. Today:
  `docs/features/coop-interop.md:310` `TAOM ships **337** settings across`, `:312` `the split is 320 in
  \`TaomSettings\`, 9 in \`BattleLoadDiagnosticsSettings\`, 7 in`, `:316` `\`TaomSettings\`. The 120
  excluded (67 counted 2026-09-22, ..., and the thirteen Menus & Loading Screens settings added
  2026-10-01, #704) are instrumentation, ...`; `docs/features/bannerlord-together-compat.md:291`
  `TAOM's 337 MCM settings`. The simulation-relevant count (217) does not change.
- `TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs` fails on any value setting with
  `RequireRestart = true` unless allowlisted. The new toggle is read at each mission start, so it takes
  `RequireRestart = false` and needs no allowlist entry.
- `Main/Core/Logging/FileLogger.cs:85-88`: `LogInfo`, `LogWarning`, `LogError` are durable (flushed
  synchronously on the calling thread); `LogDebug` is queued. `IModLogger`
  (`Main/Core/Logging/IModLogger.cs`) has `LogInfo`, `LogDebug`, `LogWarning`, `LogError`.
- `Main/TAOM.csproj` is SDK-style (new `.cs` files compile without a csproj edit) and grants
  `InternalsVisibleTo` to `TAOM.Tests` and `TAOM_Online` only (lines 112-120), **not** to
  `DynamicProxyGenAssembly2`. So any interface NSubstitute fakes must be `public`.
- `TAOM.Tests/Migration/GameAssemblies.cs`: `internal static string? ResolveGameDir(string? overrideDir,
  string? gameDir, string? builtGameFolder)` (line 133) and `internal static string? BuiltGameFolder`
  (line 123) locate the install without loading assemblies.
- `docs/features/mission-perf-heartbeat.md` (68 lines): the MissionPerf feature doc, sections Overview,
  Why This Exists, Architecture, Configuration, Log line, Key Files, Tests, Reading an A/B.
  `docs/reference/feature-map.md:72` is its row. `docs/reference/engine/mission-frame-threads-and-native-costs.md`
  section 6 (lines 130-165) documents the clip residency chain; its last bullet (lines 159-165) ends
  "...or raise the 12 MiB budget for the whole process (a guarded four-byte native patch)."

### Conventions that bind this change

- **ADR-002**: entry points (the mission behaviour, the module) stay under 150 lines and only
  delegate; all logic lives in plain classes.
- **ADR-007**: services never touch TaleWorlds types or raw native memory directly; each engine or OS
  surface sits behind an `I...Adapter` in `Main/Adapters/` (`.claude/rules/adapters.md`: interface plus
  implementation, only the members the feature needs).
- **ADR-008**: services and pure engines 100% covered; adapters covered through service tests with
  NSubstitute fakes; entry points not required.
- **ADR-003 / ADR-005**: no `#region`; no `#if DEBUG`.
- `.claude/rules/csharp-architecture.md`: constructor injection; `Reuse.Singleton` for services; an
  interface only when a test fakes it (both new adapters and the probe interface are faked); NaN must
  fail every gate on an engine-sourced float (the budget check is written `!(budget == Expected)`, and a
  NaN test pins it); `float.IsFinite` does not exist on net472.
- `.claude/rules/native-cpp-ports.md` rule 3 (written for C++ ports, applied here by the brief): never
  trust a fixed offset into the native binary; locate code by an independent signature scan. The
  probe therefore carries no RVA constant; the RVAs above appear only in tests and docs.
- `.claude/rules/tests.md`: MSTest plus NSubstitute, names `Method_State_Expected`, AAA; a test that
  reads the vanilla install is tagged `[TestCategory("LiveInstall")]` (hosted CI runs
  `TestCategory!=RequiresGame&TestCategory!=LiveInstall&TestCategory!=BindingVerification`,
  `.github/workflows/csharp.yml:67`).
- **Logging (the maintainer's instruction D6, binding)**: `taom_debug.log` is TAOM's critical record.
  The probe logs a configuration header when it arms (module base, each matched site's RVA, the budget
  read, the scan's cost in ms) or one reason line when it disables itself; a per-mission start line; a
  line every 5 s carrying every measured field (aggregated over the window, so no sample's information
  is dropped); a summary at mission end; and one line, once, whenever it skips or stops. All of these
  are INFO (ERROR for a caught exception): none is per frame. Tests pin every line's format literally,
  and the feature doc lists every line with its fields and an example.

### Blast radius

`python tools/graphify_taom.py refresh --if-stale` then `affected "<Type>" --depth 2`, at `0d1e91f0`:

- `BattleLoadDiagnosticsSettings`: "No affected nodes found." (it is read by name through the MCM static
  and by reflection in tests; `git grep` adds `SettingsFingerprintTests`,
  `SettingRequireRestartPostureTests`, `MissionPerfHeartbeatBehavior` and the BattleLoadDiagnostics
  services, none of which changes behaviour when a property is added).
- `CoopSettingsRelevance`: `.AssertSplit()` (SettingsFingerprintTests.cs:L268),
  `.NoExcludedName_IsDead()` (L288), `.EverySettingsClass_HasItsSplitPinned()` (L210).
- `FeatureModules`: "No affected nodes found." (`git grep` adds `Main/IoC.cs:218`,
  `Main/Composition/FeatureModuleHooks.cs`, `TAOM.Tests/Composition/FeatureModulesTests.cs` and the
  five `*WiringTests.cs`; appending a module changes none of their results).

Every other type is new.

## Step 0: the maintainer's edit

None. This plan edits no protected file (`.claude/settings*.json`, `Directory.Build.props`,
`docs/adrs/*.md`) and no single-owner file (`Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`).

## Commands you will need

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | the baseline's totals plus the new tests |
| One test class | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<ClassName>"` | the named tests run; a filter matching nothing proves nothing |
| Docs | `python tools/lint_docs.py --fail-on-drift` | exit 0 |
| Data | `python tools/validate_moduledata.py` | not needed: this plan changes no ModuleData |
| Signature tool | `python -B tools/native_sig_author.py disasm 0x21DFFF --n 30` and `python -B tools/native_sig_author.py scan "<pattern>"` | as quoted in Current state |
| RefAsm unit step (hosted CI's build and unit test, `.github/workflows/csharp.yml`) | `env -u BANNERLORD_GAME_DIR -u BANNERLORD_OVERRIDE_DIR dotnet build TAOM.Tests -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId=`, then `env -u BANNERLORD_GAME_DIR -u BANNERLORD_OVERRIDE_DIR dotnet test TAOM.Tests --no-build -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId= --filter "TestCategory!=RequiresGame&TestCategory!=LiveInstall&TestCategory!=BindingVerification"` | the failure set Step 1 recorded, no new names |

Both MSBuild flags go on build AND test, and prefix dotnet with the TEMP and TMP your dispatch rules
give. Never `./build.ps1`: it deploys into the game install. Run `python` with `-B`; never `python3`.
The RefAsm build overwrites `TAOM.Tests/bin`, so run the plain `Tests` command (it rebuilds against
the installed game) after it, never `--no-build`. If the RefAsm restore cannot download its reference
packages, report the step as not run with the error; that is not a STOP.

## Scope

**In scope** (the only files you create or modify):

- New, `Main/Adapters/`: `INativeModuleMemoryAdapter.cs`, `NativeModuleMemoryAdapter.cs`,
  `IAnimationLoadingAdapter.cs`, `AnimationLoadingAdapter.cs`
- New, `Main/Features/MissionPerf/AnimMemory/`: `ClipBudgetSignature.cs`, `PeSectionTable.cs`,
  `AnimMemLine.cs`, `IAnimClipMemoryProbe.cs`, `AnimMemoryProbe.cs`, `AnimMemorySession.cs`,
  `AnimMemoryProbeModule.cs`, `Hooks/AnimMemoryProbeMissionBehavior.cs`
- New, `TAOM.Tests/Features/MissionPerf/AnimMemory/`: `ClipBudgetSignatureTests.cs`,
  `PeSectionTableTests.cs`, `ClipBudgetSignatureInstalledBinaryTests.cs`, `AnimMemLineTests.cs`,
  `AnimMemoryProbeTests.cs`, `AnimMemorySessionTests.cs`, `AnimMemoryProbeWiringTests.cs`,
  `SyntheticNativeImage.cs` (test helper)
- `Main/Composition/FeatureModules.cs` (append one line)
- `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs` (one property)
- `Main/Features/CoopInterop/CoopSettingsRelevance.cs` (one name on `Instrumentation`)
- `TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs` (one pinned count)
- `docs/features/mission-perf-heartbeat.md`, `docs/features/coop-interop.md`,
  `docs/features/bannerlord-together-compat.md`, `docs/reference/feature-map.md` (row 72),
  `docs/reference/engine/mission-frame-threads-and-native-costs.md` (section 6 only)

**Out of scope** (do NOT touch, even though they look related):

- Writing any engine memory (no `Marshal.Write*`, no `VirtualProtect`, no pointer store). Raising the
  12 MiB budget is the maintainer's decision.
- Harmony patches, MinHook, anything under `Dependencies/`, `docs/reference/harmony-patch-registry.md`
  (no patch is added).
- `Main/SubModule.cs`, `Main/IoC.cs`, `Main/TAOM.csproj`: the feature-module seam makes them
  unnecessary. If you find you need one, STOP.
- `MissionPerfHeartbeatBehavior.cs`, `FrameStats.cs`, `MissionPerfLine.cs` and the BattleLoadDiagnostics
  settings provider (`IBattleLoadDiagnosticsSettingsProvider.cs`): plan 028 owns those changes.
- Any Armory or ModuleData file, any clip's Loading Type.
- `docs/features/battle-load-diagnostics.md`: its Configuration table (about lines 586-595) lists the
  settings page but already omits `EnableMissionPerfHeartbeat`; the Mission Performance group is
  documented in `mission-perf-heartbeat.md`. Bringing that table up to date for both toggles is a
  follow-up (Maintenance notes), kept out to avoid a conflict with plan 028.
- The gates themselves. Never turn a gate green by editing it: deleting or `[Ignore]`-ing a test,
  loosening an assertion, or adding to `RestartAllowlist`. The one pinned count this plan moves
  (`reflected: 9` to `10`) is the classification procedure that test's own comment prescribes, done
  together with the `Instrumentation` entry.

## Git workflow

- Commit on the branch you were given; never push or open a PR.
- Subject `<type>(<scope>): <version> - <description>`, at most 72 characters, `<version>` being the
  `<Version value=...>` in `Main/_Module/SubModule.xml` when you commit (`v2.0.32` at `0d1e91f0`; a hook
  refuses any other). Use: `feat(mission-perf): <version> - log clip memory against the 12 MiB budget`
  (71 characters with `v2.0.32`).
- The body is the changelog entry: what changed and why, for a reader of the release note, wrapped at
  72. Never edit `CHANGELOG.md`. No AI attribution trailer. Add
  `Not-tested: the live module read (GetModuleHandleW, Marshal.Copy/ReadInt32 against the running
  game), MBAnimation.IsAnyAnimationLoadingFromDisk, and the mission behaviour's engine callbacks; all
  logic behind them is unit-tested.`
- Stage explicit paths only; write the message to a file and `git commit -F <file>`.

## Steps

### Step 1: record the base

Run the drift check and the two binary checks at the top. Then, before any edit, run the RefAsm unit
step (Commands) and record its totals and failing test names, then the full `Tests` command, and write
both into your report.

**Verify**: the `Tests` command prints `Failed! - Failed: 1, Passed: 12345, Skipped: 2, Total: 12348`,
the one failure being `EveryLanguage_DeclaresARowForEveryEnglishKey`, or the difference is explained
(a landed plan's tests). The RefAsm totals and failure names are recorded (no expected value: the
writer did not measure them).

### Step 2: re-derive the signature on the installed binary

Run `python -B tools/native_sig_author.py disasm 0x21DFFF --n 30` and
`python -B tools/native_sig_author.py scan "8B 05 ? ? ? ? 41 8B EC 48 8B 3D ? ? ? ? 48 2B 3D ? ? ? ? 48 C1 FF 04 83 EF 01 66 0F 6E C0 0F 5B C0 F3 0F 5C 05 ? ? ? ? F3 44 0F 2C F8"`.

**Verify**: the disassembly shows `8B 05 2B DE B8 00` at `0x0021E00F` and `F3 0F 5C 05 A0 02 91 00` at
`0x0021E034`, and the scan prints `matches: 1` and `RVA=0x21E00F`. Anything else is a STOP.

### Step 3: RED, the pure signature and PE tests

Create the test helper and three test classes. Namespace `TAOM.Tests.Features.MissionPerf.AnimMemory`;
production namespace `TAOM.Features.MissionPerf.AnimMemory`. The test namespace does not enclose the
production one, so each test class file carries `using TAOM.Features.MissionPerf.AnimMemory;` (plus
`System`, `Microsoft.VisualStudio.TestTools.UnitTesting`, and in the installed-binary class `System.IO`
and `TAOM.Tests.Migration`).

`SyntheticNativeImage.cs` (internal static test helper), building a fake loaded image:

- `const long Base = 0x180000000L;`
- `static byte[] Headers(params (string Name, int Va, int Size)[] sections)`: a 4,096-byte array with
  `M Z` at 0, `e_lfanew = 0x80` (int32 at `0x3C`), `P E 0 0` at `0x80`, Machine `0x8664` (uint16 at
  `0x84`), NumberOfSections (uint16 at `0x86`), SizeOfOptionalHeader `0xF0` (uint16 at `0x94`), magic
  `0x20B` (uint16 at `0x98`), and the section table at `0x80 + 24 + 0xF0 = 0x188`, 40 bytes per entry:
  name (8 bytes, zero-padded), VirtualSize at +8, VirtualAddress at +12, SizeOfRawData at +16 (= size),
  PointerToRawData at +20 (= va).
- `static readonly (string, int, int)[] Standard = { (".text", 0x1000, 0x200), (".rdata", 0x2000, 0x100), (".data", 0x3000, 0x100) };`
- `static byte[] TextWithSiteAt(int textOffset, int counterRva, int budgetRva, int textRva = 0x1000, int textSize = 0x200)`:
  zero bytes with the 50-byte real site (Current state) copied in at `textOffset`, then the load disp32
  (offset +2) set to `counterRva - (textRva + textOffset + 6)` and the budget disp32 (offset +41) set
  to `budgetRva - (textRva + textOffset + 37 + 8)`, little-endian. For the standard case
  (`textOffset 0x40`, counter `0x3010`, budget `0x2020`) the two disps are `0x1FCA` and `0xFB3`.

`ClipBudgetSignatureTests.cs`:

- `Parse_ThePattern_Has50BytesAnd16Wildcards`
- `Parse_QuestionMarkAndDoubleQuestionMark_AreWildcards` (`"8B ? ?? 05"` gives `{0x8B, -1, -1, 0x05}`)
- `Find_UniqueMatch_ReturnsItsOffset`, `Find_NoMatch_ReturnsEmpty`,
  `Find_TwoMatches_StopsAtMaxHitsTwo`, `Find_WildcardBytesDiffer_StillMatches`,
  `Find_MatchAtVeryEnd_IsFound`, `Find_HaystackShorterThanPattern_ReturnsEmpty`
- `RipTarget_RealLoadEncoding_IsDABE40`: `RipTarget(0x21E00F, 6, BitConverter.ToInt32(new byte[]{0x8B,0x05,0x2B,0xDE,0xB8,0x00}, 2))` is `0xDABE40`
- `RipTarget_RealSubssEncoding_IsB2E2DC`: `RipTarget(0x21E034, 8, BitConverter.ToInt32(new byte[]{0xF3,0x0F,0x5C,0x05,0xA0,0x02,0x91,0x00}, 4))` is `0xB2E2DC`
- `RipTarget_NegativeDisplacement_PointsBackwards`
- `Resolve_RealSiteBytesAtTextRva21E00F_GivesTheVerifiedRvas`: the 50 real bytes as the whole text,
  `textRva 0x21E00F`; expect `MatchCount 1`, `LoadSiteRva 0x21E00F`, `BudgetSiteRva 0x21E034`,
  `CounterRva 0xDABE40`, `BudgetRva 0xB2E2DC`
- `Resolve_NoSite_MatchCountZero`, `Resolve_TwoSites_MatchCountTwo`

`PeSectionTableTests.cs`:

- `Parse_SyntheticHeaders_ReturnsEachSection` (names, VA, sizes, raw pointer)
- `Parse_NoMzSignature_ReturnsNull`, `Parse_ELfanewOutsideBuffer_ReturnsNull`,
  `Parse_NoPeSignature_ReturnsNull`, `Parse_SectionTablePastBuffer_ReturnsNull`
- `Parse_MachineNotX64_ReturnsNull` (Machine at `0x84` set to `0x014C`) and
  `Parse_MagicNotPe32Plus_ReturnsNull` (magic at `0x98` set to `0x010B`)
- `Find_MissingName_ReturnsNull`
- `Contains_FirstAndLastAlignedInt_True_OnePastEnd_False` (for `.data` at `0x3000`, size `0x100`:
  `Contains(0x3000, 4)` and `Contains(0x30FC, 4)` true, `Contains(0x30FD, 4)` and `Contains(0x2FFC, 4)` false)

`ClipBudgetSignatureInstalledBinaryTests.cs`, class tagged `[TestCategory("LiveInstall")]` (it reads
the vanilla install): resolve the folder with
`GameAssemblies.ResolveGameDir(Environment.GetEnvironmentVariable("BANNERLORD_OVERRIDE_DIR"), Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR"), GameAssemblies.BuiltGameFolder)`
(`using TAOM.Tests.Migration;`), then `<dir>\bin\Win64_Shipping_Client\TaleWorlds.Native.dll`;
`Assert.Inconclusive` with the reason when the folder or file is missing. Read the file, parse the
first 4,096 bytes with `PeSectionTable.Parse`, slice the raw `.text` (`PointerToRawData`,
`min(VirtualSize, SizeOfRawData)` bytes) and call `ClipBudgetSignature.Resolve(rawText, text.VirtualAddress)`.

- `InstalledNativeDll_Signature_MatchesOnceWithTargetsInDataAndRdataAndA12MiBBudget`: `MatchCount 1`;
  `.data` contains `(CounterRva, 4)`; `.rdata` contains `(BudgetRva, 4)`; both RVAs divisible by 4; the
  float at raw offset `rdata.PointerToRawData + (BudgetRva - rdata.VirtualAddress)` is `12582912f`.
- `InstalledNativeDll_V153Binary_TargetsAreTheVerifiedRvas`: `Assert.Inconclusive` unless the file
  length is `14209376`; then the four RVAs equal `0x21E00F`, `0x21E034`, `0xDABE40`, `0xB2E2DC`.

Build the test project (`dotnet build TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`).

**Verify**: the build fails with at least one error, and every error is one of: CS0234 saying
`AnimMemory` does not exist in the namespace `TAOM.Features.MissionPerf` (from the `using` lines), or
CS0103 or CS0246 naming `ClipBudgetSignature`, `PeSectionTable`, `PeSection` or `SignatureMatch`.
Expect to see only the CS0234 lines (one per test class file): while a `using` fails, the compiler
skips method-body errors (Current state). Any other diagnostic is a defect in your tests: fix it. Quote
the error count in your report.

### Step 4: GREEN, `ClipBudgetSignature` and `PeSectionTable`

`Main/Features/MissionPerf/AnimMemory/PeSectionTable.cs`:

```csharp
internal sealed class PeSection
{
    // Name, VirtualAddress, VirtualSize, PointerToRawData, SizeOfRawData (all int)
    internal bool Contains(int rva, int length) =>
        rva >= VirtualAddress && (long)rva + length <= (long)VirtualAddress + VirtualSize;
}

internal static class PeSectionTable
{
    /// <summary>The section table of an x64 PE32+ header block, or null when it is not one.</summary>
    internal static IReadOnlyList<PeSection>? Parse(byte[] headers) { ... }
    internal static PeSection? Find(IReadOnlyList<PeSection> sections, string name) { ... }
}
```

Parse checks, each returning null on failure: length at least `0x40`; bytes 0-1 `M Z`; `e_lfanew`
(int32 at `0x3C`) positive and `e_lfanew + 26` within the buffer; `P E 0 0` at `e_lfanew`; Machine
(uint16 at `e_lfanew + 4`) is `0x8664`; the optional header's magic (uint16 at `e_lfanew + 24`) is
`0x20B`; the whole table (`e_lfanew + 24 + SizeOfOptionalHeader + 40 * NumberOfSections`, with
NumberOfSections the uint16 at `e_lfanew + 6` and SizeOfOptionalHeader the uint16 at `e_lfanew + 20`)
within the buffer. Names are the 8 name bytes up to the first zero, ASCII.

`Main/Features/MissionPerf/AnimMemory/ClipBudgetSignature.cs` (`internal static class`), with a short
summary naming the eviction pass and the two instructions in words (the clip eviction pass loads the
loaded-bytes counter with `mov eax` and subtracts the 12 MiB budget float with `subss`). No RVA or
other hex address anywhere in production code, comments included: the Done criteria grep `Main` for
the four RVAs and match comments too.

```csharp
internal const string ModuleName = "TaleWorlds.Native.dll";
internal const string Pattern = "8B 05 ? ? ? ? 41 8B EC 48 8B 3D ? ? ? ? 48 2B 3D ? ? ? ? 48 C1 FF 04 83 EF 01 66 0F 6E C0 0F 5B C0 F3 0F 5C 05 ? ? ? ? F3 44 0F 2C F8";
internal const int LoadOffset = 0, LoadLength = 6, LoadDispOffset = 2;      // mov eax, dword ptr [rip+disp32]
internal const int BudgetOffset = 37, BudgetLength = 8, BudgetDispOffset = 4; // subss xmm0, dword ptr [rip+disp32]
internal const float ExpectedBudgetBytes = 12582912f;

internal static int[] Parse(string pattern);                       // -1 = wildcard
internal static List<int> Find(byte[] haystack, int[] pattern, int maxHits);
internal static int RipTarget(int instructionRva, int instructionLength, int disp32) =>
    instructionRva + instructionLength + disp32;
internal static SignatureMatch Resolve(byte[] text, int textRva);  // Find with maxHits 2
```

`Find` is a plain loop: for each start `i` from 0 to `haystack.Length - pattern.Length`, compare until
the first mismatch (a `-1` matches anything); on a full match add `i` and stop once `maxHits` are found.
`SignatureMatch` (internal sealed class, same file) carries `MatchCount`, `LoadSiteRva`,
`BudgetSiteRva`, `CounterRva`, `BudgetRva`. Displacements are read with `BitConverter.ToInt32` (x64 is
little-endian).

**Verify**: `dotnet test ... --filter "FullyQualifiedName~ClipBudgetSignature|FullyQualifiedName~PeSectionTable"`
(MSTest accepts `|`) runs all three classes and passes; the two `ClipBudgetSignatureInstalledBinaryTests`
show as **Passed**, not Skipped (on this machine the install is present). If the installed-binary test
fails, STOP: the code and the binary disagree.

### Step 5: RED then GREEN, the line formats

`TAOM.Tests/Features/MissionPerf/AnimMemory/AnimMemLineTests.cs` pins each line literally (InvariantCulture):

| Test | Call | Expected string |
|---|---|---|
| `Armed_FormatsHeader` | `AnimMemLine.Armed(0x7FFB12340000L, 0x1000, 0xA240CC, 0x21E00F, 0x21E034, 0xDABE40, 0xB2E2DC, 12582912, 23.44)` | `[AnimMem] armed: TaleWorlds.Native.dll base=0x7FFB12340000 text=0x1000+0xA240CC loadSite=0x21E00F budgetSite=0x21E034 counter=0xDABE40 budget=0xB2E2DC budgetBytes=12582912 scanMs=23.4` |
| `Disabled_FormatsReason` | `AnimMemLine.Disabled("TaleWorlds.Native.dll is not loaded in this process")` | `[AnimMem] disabled for this process: TaleWorlds.Native.dll is not loaded in this process. No [AnimMem] samples will be logged; [MissionPerf] is unaffected.` |
| `OffForMission_Formats` | `AnimMemLine.OffForMission()` | `[AnimMem] off for this mission: 'Enable Animation Clip Memory Probe' is off (Battle Load Diagnostics page).` |
| `MissionStart_Formats` | `AnimMemLine.MissionStart(10485760, 12582912, false)` | `[AnimMem] mission start: sample every 1 s, line every 5 s, startKB=10240 budgetKB=12288 pctOfBudget=83 loadingNow=0` |
| `Periodic_Formats` | `AnimMemLine.Periodic(5.0, 12582912, 12582912, false, 2, 9437184, 12582912, 0, 6)` | `[AnimMem] t=+5s loadedKB=12288 budgetKB=12288 pctOfBudget=100 loadingNow=0 drops=2 minKB=9216 maxKB=12288 loadingSamples=0/6` |
| `Periodic_OverBudget_PctAbove100` | `AnimMemLine.Periodic(10.0, 15728640, 12582912, true, 0, 15728640, 15728640, 1, 5)` | `[AnimMem] t=+10s loadedKB=15360 budgetKB=12288 pctOfBudget=125 loadingNow=1 drops=0 minKB=15360 maxKB=15360 loadingSamples=1/5` |
| `Summary_Formats` | `AnimMemLine.Summary(7.0, 6, 10485760, 12582912, 12582912, 12582912, 4, 2, 0, false)` | `[AnimMem] summary: t=+7s samples=6 startKB=10240 endKB=12288 peakKB=12288 peakPct=100 samplesAtOrAbove90Pct=4 drops=2 loadingSamples=0 stopped=0` |
| `Stopped_Formats` | `AnimMemLine.Stopped(new InvalidOperationException("boom"))` | `[AnimMem] stopped for this mission after InvalidOperationException: boom` |

Arithmetic: KB is `bytes / 1024` (integer division on `long`); a percentage is
`(int)(bytes * 100L / budgetBytes)` (floor; above 100 is legitimate, see Current state); `t` uses `{0:0}`
like `MissionPerfLine`; `scanMs` uses `{0:0.0}`; hex is `{0:X}` with a literal `0x` prefix. Parameter
order for `Periodic`: `(double tSeconds, int loadedBytes, int budgetBytes, bool loadingNow, int drops,
int minBytes, int maxBytes, int loadingSamples, int samples)`; for `Summary`: `(double tSeconds, int
samples, int startBytes, int endBytes, int peakBytes, int budgetBytes, int samplesAtOrAbove90Pct, int
drops, int loadingSamples, bool stopped)`.

Build the test project: it fails, every error a CS0103 naming `AnimMemLine` (the `AnimMemory`
namespace exists since Step 4). Then create
`Main/Features/MissionPerf/AnimMemory/AnimMemLine.cs` (`internal static class`, `string.Format(CultureInfo.InvariantCulture, ...)`).

**Verify**: `--filter "FullyQualifiedName~AnimMemLineTests"` reports 8 passed.

### Step 6: RED, the probe

Create `AnimMemoryProbeTests.cs`. Fakes: `Substitute.For<INativeModuleMemoryAdapter>()`,
`Substitute.For<IAnimationLoadingAdapter>()`, `Substitute.For<IModLogger>()`. The standard arrangement:
`GetModuleBase("TaleWorlds.Native.dll")` returns `SyntheticNativeImage.Base`;
`Copy(Base, 4096)` returns `Headers(Standard)`; `Copy(Base + 0x1000, 0x200)` returns
`TextWithSiteAt(0x40, 0x3010, 0x2020)`; `ReadInt32(Base + 0x2020)` returns `0x4B400000`
(12582912.0f); `ReadInt32(Base + 0x3010)` returns `5000000`.

Tests (each reason string is pinned literally through `AnimMemLine.Disabled(...)`):

- `EnsureArmed_SyntheticImage_ReturnsTrueAndLogsHeader`: one `LogInfo` starting with
  `[AnimMem] armed: TaleWorlds.Native.dll base=0x180000000 text=0x1000+0x200 loadSite=0x1040 budgetSite=0x1065 counter=0x3010 budget=0x2020 budgetBytes=12582912 scanMs=`
  and matching `scanMs=\d+\.\d$`.
- `EnsureArmed_CalledTwice_ScansOnceAndLogsOnce` (`Received(1).Copy(Base + 0x1000, 0x200)`, one header).
- `EnsureArmed_ModuleNotLoaded_DisablesWithReason`: base 0; reason
  `TaleWorlds.Native.dll is not loaded in this process`.
- `EnsureArmed_HeadersNotPe_DisablesWithReason`: `the module's PE headers did not parse`.
- `EnsureArmed_NoTextSection_DisablesWithReason`, `EnsureArmed_NoRdataSection_DisablesWithReason`,
  `EnsureArmed_NoDataSection_DisablesWithReason`: headers built from `Standard` minus that one
  section; reasons `no .text section in the module headers`, `no .rdata section in the module
  headers`, `no .data section in the module headers`.
- `EnsureArmed_NoSite_DisablesWithReason`: text all zeros;
  `the eviction-pass signature matched nothing in .text, so this engine build differs from the one it was written for`.
- `EnsureArmed_TwoSites_DisablesWithReason`: site at `0x40` and `0x100`;
  `the eviction-pass signature matched 2 or more times in .text, so the site is ambiguous`.
- `EnsureArmed_CounterTargetInRdata_DisablesWithReason`: counter `0x2010`;
  `the loaded-bytes target 0x2010 is not an aligned 4-byte address inside .data`.
- `EnsureArmed_CounterTargetMisaligned_DisablesWithReason`: counter `0x3011`;
  `the loaded-bytes target 0x3011 is not an aligned 4-byte address inside .data`.
- `EnsureArmed_BudgetTargetOutsideRdata_DisablesWithReason`: budget `0x3020`;
  `the budget target 0x3020 is not an aligned 4-byte address inside .rdata`.
- `EnsureArmed_BudgetFloatWrong_DisablesWithReason`: `ReadInt32(Base + 0x2020)` returns `0x4B000000`;
  `the budget float reads 8388608, expected 12582912 (12 MiB)` (format the float with `R`).
- `EnsureArmed_BudgetFloatNaN_Disables`: returns `0x7FC00000`; reason contains `reads NaN` (the NaN gate
  test the architecture rule requires).
- `EnsureArmed_CounterNegative_DisablesWithReason`: counter read `-4`;
  `the loaded-bytes counter reads -4, a negative byte count`.
- `EnsureArmed_CopyThrows_DisablesWithReason`: `Copy` throws `new InvalidOperationException("boom")`;
  `reading module memory threw InvalidOperationException: boom`.
- `EnsureArmed_AfterDisable_ReturnsFalseWithoutLoggingAgain`.
- `DisableForProcess_AfterArming_LogsReasonOnceAndEnsureArmedReturnsFalse`.
- `ReadLoadedBytes_ReadsTheCounterAddress` (`ReadInt32(Base + 0x3010)`),
  `IsAnyClipLoading_DelegatesToTheAdapter`, `BudgetBytes_AfterArming_Is12582912`.

**Verify**: building `TAOM.Tests` fails, every error a CS0246 (or CS0103) naming a missing
`INativeModuleMemoryAdapter`, `IAnimationLoadingAdapter`, `IAnimClipMemoryProbe` or `AnimMemoryProbe`.
The test file's usings: `System`, `System.Text.RegularExpressions`,
`Microsoft.VisualStudio.TestTools.UnitTesting`, `NSubstitute`, `TAOM.Adapters` (it exists),
`TAOM.Core.Logging`, `TAOM.Features.MissionPerf.AnimMemory`.

### Step 7: GREEN, the adapters and the probe

All four adapter files use namespace `TAOM.Adapters`, as the top-level files in `Main/Adapters/` do.

`Main/Adapters/INativeModuleMemoryAdapter.cs` (public, ADR-007; summary: read-only access to a native
module mapped in this process; the caller guarantees every address lies inside a mapped section):

```csharp
public interface INativeModuleMemoryAdapter
{
    /// <summary>Base address of a module already loaded in this process, or 0 when it is not.</summary>
    long GetModuleBase(string moduleFileName);
    /// <summary>A copy of <paramref name="count"/> bytes starting at <paramref name="address"/>.</summary>
    byte[] Copy(long address, int count);
    /// <summary>One aligned 32-bit read; atomic on x64.</summary>
    int ReadInt32(long address);
}
```

`Main/Adapters/NativeModuleMemoryAdapter.cs` (`public sealed`):
`[DllImport("kernel32", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)] private static extern IntPtr GetModuleHandleW(string lpModuleName);`
(`GetModuleHandle` takes no reference, so nothing is freed); `GetModuleBase` returns
`GetModuleHandleW(name).ToInt64()`; `Copy` allocates `new byte[count]` and calls
`Marshal.Copy(new IntPtr(address), buffer, 0, count)`; `ReadInt32` is
`Marshal.ReadInt32(new IntPtr(address))`. No write method of any kind.

`Main/Adapters/IAnimationLoadingAdapter.cs` (public): `bool IsAnyAnimationLoadingFromDisk();`.
`Main/Adapters/AnimationLoadingAdapter.cs` (`public sealed`): returns
`MBAnimation.IsAnyAnimationLoadingFromDisk()` (`using TaleWorlds.MountAndBlade;`).

`Main/Features/MissionPerf/AnimMemory/IAnimClipMemoryProbe.cs` (public, because the session tests fake it):

```csharp
public interface IAnimClipMemoryProbe
{
    /// <summary>First call finds the engine's counter and budget and logs the result; later calls return it.</summary>
    bool EnsureArmed();
    int BudgetBytes { get; }
    /// <summary>The engine's on-demand clip byte total. Only after EnsureArmed returned true.</summary>
    int ReadLoadedBytes();
    bool IsAnyClipLoading();
    /// <summary>Turns the probe off for the rest of the process and logs the reason once.</summary>
    void DisableForProcess(string reason);
}
```

`Main/Features/MissionPerf/AnimMemory/AnimMemoryProbe.cs` (`public sealed`, constructor
`(INativeModuleMemoryAdapter memory, IAnimationLoadingAdapter loading, IModLogger logger)`), state
NotTried, Armed or Disabled. `EnsureArmed` on the first call, inside one `try` timed by a `Stopwatch`:

1. `base = memory.GetModuleBase(ClipBudgetSignature.ModuleName)`; 0 disables.
2. `headers = memory.Copy(base, 4096)`; `PeSectionTable.Parse` null disables; find `.text`, `.rdata`,
   `.data` in that order, a missing one disables.
3. `text = memory.Copy(base + text.VirtualAddress, text.VirtualSize)`; `ClipBudgetSignature.Resolve(text,
   text.VirtualAddress)`; drop the array reference straight after (about 10 MB, once per process).
   `MatchCount` 0 or 2 disables.
4. `CounterRva % 4 == 0 && data.Contains(CounterRva, 4)`, else disable; the same for `BudgetRva` and
   `.rdata`. Only after both pass may any `ReadInt32` run (Current state, "Reading safely").
5. `budget = BitConverter.ToSingle(BitConverter.GetBytes(memory.ReadInt32(base + BudgetRva)), 0)`;
   disable when `!(budget == ClipBudgetSignature.ExpectedBudgetBytes)` (NaN fails).
6. A first `ReadInt32(base + CounterRva)` below 0 disables.
7. Armed: store the absolute counter address and `BudgetBytes = 12582912`, and log
   `AnimMemLine.Armed(...)` with the elapsed milliseconds.

A caught exception disables with `reading module memory threw {Type}: {Message}`. Every disable goes
through one private method that sets the state and logs `AnimMemLine.Disabled(reason)` via `LogInfo`,
once. `DisableForProcess` uses the same method and is a no-op when already disabled.

**Verify**: `--filter "FullyQualifiedName~AnimMemoryProbeTests"` passes every test; the build of
`Main/TAOM.csproj` exits 0. If `GetModuleHandleW`, `Marshal.Copy` or `Marshal.ReadInt32` does not
compile on net472, STOP.

### Step 8: RED then GREEN, the per-mission session

`AnimMemorySessionTests.cs`, faking `IAnimClipMemoryProbe` (`BudgetBytes` 12582912) and `IModLogger`.
The standard sequence: `ReadLoadedBytes()` returns `10485760, 12582912, 11534336, 11534336, 9437184,
12582912`; `IsAnyClipLoading()` returns false; call `Start(0.0)`, then `Tick(1.0)` through `Tick(5.0)`.
Sequences use NSubstitute's `Returns(first, then...)`; a sequence that throws uses the lambda form,
for example `probe.ReadLoadedBytes().Returns(x => 10485760, x => throw new
InvalidOperationException("boom"))` (NSubstitute 5.1.0 repeats the last entry for later calls).

- `Start_LogsMissionStartLine`: exactly
  `[AnimMem] mission start: sample every 1 s, line every 5 s, startKB=10240 budgetKB=12288 pctOfBudget=83 loadingNow=0`.
- `Tick_BeforeOneSecond_DoesNotSample` (`Tick(0.5)` leaves `ReadLoadedBytes` at one call).
- `Tick_AtFiveSeconds_LogsPeriodicLine`: exactly
  `[AnimMem] t=+5s loadedKB=12288 budgetKB=12288 pctOfBudget=100 loadingNow=0 drops=2 minKB=9216 maxKB=12288 loadingSamples=0/6`
  (the start sample belongs to the first window; drops are 12288 to 11264 and 11264 to 9216).
- `Tick_DropAcrossWindowBoundary_CountsInTheNextWindow`: reads are the standard six, then
  `8388608` five times; `Start(0.0)`, `Tick(1.0)` through `Tick(10.0)`. The second periodic line is
  exactly
  `[AnimMem] t=+10s loadedKB=8192 budgetKB=12288 pctOfBudget=66 loadingNow=0 drops=1 minKB=8192 maxKB=8192 loadingSamples=0/5`
  (the drop from the t=5 sample, in the first window, to the t=6 sample counts in the second).
- `Tick_LoadingSamples_AreCounted`: `IsAnyClipLoading()` returns `false, true, false, true, false,
  false`; the t=5 line is exactly
  `[AnimMem] t=+5s loadedKB=12288 budgetKB=12288 pctOfBudget=100 loadingNow=0 drops=2 minKB=9216 maxKB=12288 loadingSamples=2/6`.
- `End_LogsSummary`: after the sequence, `End(7.0)` logs exactly
  `[AnimMem] summary: t=+7s samples=6 startKB=10240 endKB=12288 peakKB=12288 peakPct=100 samplesAtOrAbove90Pct=4 drops=2 loadingSamples=0 stopped=0`.
- `Sample_At11324621Bytes_CountsAsAtOrAbove90Pct` and `Sample_At11324620Bytes_DoesNot`: one read of
  that value, `Start(0.0)`, `End(1.0)`; the summary contains ` samplesAtOrAbove90Pct=1 ` and
  ` samplesAtOrAbove90Pct=0 ` respectively (the rule is `bytes * 10L >= budget * 9L`; 90% of 12582912
  is 11324620.8; `peakPct` is 90 and 89).
- `Tick_NegativeRead_DisablesProbeForProcessAndStops`: reads `10485760, -1`; `Start(0.0)`,
  `Tick(1.0)`, `Tick(2.0)`, `Tick(3.0)`, `End(3.0)`. `DisableForProcess("the loaded-bytes counter
  reads -1, a negative byte count")` is received once, `ReadLoadedBytes` exactly twice, no `LogError`,
  and the summary is exactly
  `[AnimMem] summary: t=+3s samples=1 startKB=10240 endKB=10240 peakKB=10240 peakPct=83 samplesAtOrAbove90Pct=0 drops=0 loadingSamples=0 stopped=1`.
- `Tick_ReaderThrows_LogsOneErrorAndStops`: the read in `Start` returns `10485760` and the first
  read in `Tick` throws (`Returns(x => 10485760, x => throw new InvalidOperationException("boom"))`);
  same calls as the test above. Exactly one `LogError`, equal to
  `[AnimMem] stopped for this mission after InvalidOperationException: boom`; `ReadLoadedBytes`
  exactly twice; the summary is the same `stopped=1` string as in the test above.
- `End_WithNoSamples_LogsNothing`: every read throws (`Returns(x => throw new
  InvalidOperationException("boom"))`); `Start(0.0)`, `Tick(1.0)`, `End(2.0)`. `LogInfo` is never
  received (no start line, no summary), `LogError` exactly once (from `Start`), `ReadLoadedBytes`
  exactly once.

Every exact string above was produced by a scratch model of the rules below run with NSubstitute
5.1.0; re-check them against your implementation, not the other way round.

Build the test project: it fails, every error a CS0246 naming `AnimMemorySession`. Then create
`Main/Features/MissionPerf/AnimMemory/AnimMemorySession.cs` (`internal sealed`, constructor
`(IAnimClipMemoryProbe probe, IModLogger logger)`), with `Start(double now)`, `Tick(double now)`,
`End(double now)`, every body inside `try`/`catch (Exception)` that logs `AnimMemLine.Stopped(ex)` via
`LogError` once and sets `stopped`. The rules:

- **Taking a sample**: read `ReadLoadedBytes()`. A negative value calls
  `DisableForProcess("the loaded-bytes counter reads {value}, a negative byte count")`, sets
  `stopped`, logs nothing itself and is not a sample. Otherwise call `IsAnyClipLoading()` and record:
  a drop when the value is lower than the previous sample (whichever window that was in, never for the
  first sample), the window's min, max, sample and loading counts, and the mission's sample count, peak,
  last value, `samplesAtOrAbove90Pct`, drops and loading count.
- **`Start(now)`** takes the first sample; if it was recorded, it logs `AnimMemLine.MissionStart`
  with it and sets `nextSample = now + 1.0` and `nextLine = now + 5.0`. The start sample belongs to
  the first window.
- **`Tick(now)`** returns at once when `stopped`. A sample is due when `now >= nextSample`; then
  `nextSample = now + 1.0` and a sample is taken. After a recorded sample, a line is due when
  `now >= nextLine`: log `AnimMemLine.Periodic` with the window's figures, set `nextLine = now + 5.0`
  and reset the window counters.
- **`End(now)`** logs nothing when the mission has no recorded sample; otherwise it logs
  `AnimMemLine.Summary` with `stopped` as recorded. `t` is always `now` (seconds since `Start`).

**Verify**: `--filter "FullyQualifiedName~AnimMemorySessionTests"` passes every test.

### Step 9: RED then GREEN, the toggle, the co-op classification and the wiring

RED, in this order:

1. `TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs:211`: change `reflected: 9` to
   `reflected: 10`. (If plan 028 has landed, the number there is already higher: add one to whatever
   it is.)
2. New `AnimMemoryProbeWiringTests.cs` (model it on `BattleCorpsesWiringTests.cs`):
   `FeatureModules_ListTheAnimMemoryProbeModuleOnce`, `Module_DeclaresTheMissionBehavior`
   (`typeof(AnimMemoryProbeMissionBehavior)`), `Module_RegistersTheProbeAsASingleton` (DryIoc
   `Container`, `RegisterInstance(Substitute.For<IModLogger>())`, resolve `IAnimClipMemoryProbe`,
   `Assert.AreSame` twice: the once-per-process latch only works as a singleton),
   `Setting_DefaultsOn` (`Assert.IsTrue(new BattleLoadDiagnosticsSettings().EnableAnimMemoryProbe)`),
   `Setting_IsInstrumentation` (`Assert.IsFalse(CoopSettingsRelevance.IsSimulationRelevant(typeof(BattleLoadDiagnosticsSettings).GetProperty(nameof(BattleLoadDiagnosticsSettings.EnableAnimMemoryProbe))))`;
   the method is `public static bool IsSimulationRelevant(PropertyInfo property)`,
   `CoopSettingsRelevance.cs:142`), and `Behavior_MakesNoNativeWrite`
   (`RepoPaths.ReadSource` of the behaviour, the probe and `Main/Adapters/NativeModuleMemoryAdapter.cs`
   with `stripComments: true` contain none of `Marshal.Write`, `VirtualProtect`, `WriteProcessMemory`).
   Usings: `System.Linq`, `DryIoc`, `Microsoft.VisualStudio.TestTools.UnitTesting`, `NSubstitute`,
   `TAOM.Composition`, `TAOM.Core.Logging`, `TAOM.Features.BattleLoadDiagnostics`,
   `TAOM.Features.CoopInterop`, `TAOM.Features.MissionPerf.AnimMemory`,
   `TAOM.Features.MissionPerf.AnimMemory.Hooks`, `TAOM.Tests.Infrastructure`.

Build the test project.

**Verify (RED)**: the build fails with at least one error, and every error is one of: CS0234 saying
`Hooks` does not exist in the namespace `TAOM.Features.MissionPerf.AnimMemory` (from the `using`);
CS0246 naming `AnimMemoryProbeModule` or `AnimMemoryProbeMissionBehavior`; CS1061 or CS0117 naming
`EnableAnimMemoryProbe`. Expect to see only the CS0234 line: while a `using` fails, the compiler skips
method-body errors (Current state). The pinned count change fails only at run time, in GREEN's first
run if the classification is missing.

GREEN:

- `BattleLoadDiagnosticsSettings.cs`, after `EnableMissionPerfHeartbeat` (line 61):
  ```csharp
  [SettingPropertyGroup("Mission Performance")]
  [SettingPropertyBool("Enable Animation Clip Memory Probe", Order = 10, RequireRestart = false,
      HintText = "Writes an [AnimMem] line to the TAOM debug log every 5 seconds while a mission runs: how much on-demand animation clip data the engine holds against its 12 MiB budget, whether a clip is loading from disk right now, and how often the total fell (clips evicted), plus a summary when the mission ends. Read-only: it finds the two engine values once per game session by a signature check, then reads the clip byte total once a second; if the check fails on this game version it turns itself off and says why in the log. Takes effect at the next mission start. Default ON.")]
  public bool EnableAnimMemoryProbe { get; set; } = true;
  ```
  (`Order = 10` leaves 1 to 9 for plan 028's profiler settings in the same group. A new property name,
  so the persisted-default trap does not apply.)
- `CoopSettingsRelevance.cs`, the `Instrumentation` set: after `"CultureDoctrineDebug", "EnableMissionPerfHeartbeat",`
  add a comment line `// The [AnimMem] clip memory probe; it reads, never changes, the engine.`
  and `"EnableAnimMemoryProbe",`.
- `Main/Features/MissionPerf/AnimMemory/Hooks/AnimMemoryProbeMissionBehavior.cs`
  (`public sealed class ... : MissionLogic`, constructor `(IAnimClipMemoryProbe probe, IModLogger logger)`,
  under 80 lines; usings `System`, `System.Diagnostics`, `TaleWorlds.MountAndBlade`,
  `TAOM.Core.Logging`, `TAOM.Features.BattleLoadDiagnostics`, `TAOM.Features.MissionPerf.AnimMemory`):
  - `AfterStart()`: `_session = null`; in a `try`: if
    `!(BattleLoadDiagnosticsSettings.Instance?.EnableAnimMemoryProbe ?? true)` log
    `AnimMemLine.OffForMission()` and return; if `!_probe.EnsureArmed()` return (the probe already
    logged its reason, once per process); else stamp `_start = Stopwatch.GetTimestamp()`, create the
    session and `Start(0.0)`. The `catch` nulls the session and logs
    `AnimMemLine.Stopped(ex)` through `LogError` inside its own `try { } catch { }`, as the heartbeat does.
  - `OnMissionTick(float dt)`: `_session?.Tick(Seconds())`.
  - `protected override void OnEndMission()`: `_session?.End(Seconds()); _session = null;`.
  - `Seconds()` is `(Stopwatch.GetTimestamp() - _start) / (double)Stopwatch.Frequency`.
  - Summary comment: why it exists (the clip lever decision), that the scan runs in `AfterStart` under
    the loading screen, and that it reads only.
- `Main/Features/MissionPerf/AnimMemory/AnimMemoryProbeModule.cs`: copy the `BattleCorpsesModule`
  shape and its usings (with `TAOM.Features.MissionPerf.AnimMemory.Hooks` for its own `Hooks`);
  `Id => "AnimMemoryProbe"`; `RegisterServices` registers
  `INativeModuleMemoryAdapter`/`NativeModuleMemoryAdapter`, `IAnimationLoadingAdapter`/`AnimationLoadingAdapter`
  and `IAnimClipMemoryProbe`/`AnimMemoryProbe`, each `Reuse.Singleton`; one `MissionBehaviorDecl.Of((_, r) =>
  new AnimMemoryProbeMissionBehavior(r.Resolve<IAnimClipMemoryProbe>(), r.Resolve<IModLogger>()))`.
- `Main/Composition/FeatureModules.cs`: append
  `new Features.MissionPerf.AnimMemory.AnimMemoryProbeModule(),` after the TournamentRewards line.
- Docs counts (the test `EveryDocQuotingTheSettingsCounts_AgreesWithReflection` reads them):
  - `docs/features/coop-interop.md:310`: `**337**` becomes `**338**`.
  - `docs/features/coop-interop.md:312`: `9 in \`BattleLoadDiagnosticsSettings\`` becomes `10 in \`BattleLoadDiagnosticsSettings\``.
  - `docs/features/coop-interop.md:316`: `The 120 excluded (` becomes `The 121 excluded (`, and the
    text `added 2026-10-01, #704)` becomes
    `added 2026-10-01, #704, and the Animation Clip Memory probe toggle added 2026-10-02)`.
  - `docs/features/bannerlord-together-compat.md:291`: `TAOM's 337 MCM settings` becomes
    `TAOM's 338 MCM settings`.
  - Leave every `217` as it is. If plan 028 landed first, the numbers are higher: use the totals the
    failing assertion message states.

**Verify (GREEN)**: `--filter "FullyQualifiedName~AnimMemoryProbeWiringTests|FullyQualifiedName~SettingsFingerprintTests"`
passes, and so does
`--filter "FullyQualifiedName~FeatureModulesTests|FullyQualifiedName~SettingRequireRestartPostureTests"`.

### Step 10: the docs

- `docs/features/mission-perf-heartbeat.md`: add a section `## Animation clip memory probe` before
  `## Key Files`, covering: what it measures and why (two sentences from "Why this matters"); how it
  finds the values (the signature, the three section checks, the 12582912 check, the RVAs on v1.5.3 as
  an example, and that a failed check disables it with one line); cost (one scan of about 10 MB once per
  process inside the first mission's loading screen, `scanMs` in the header, plus one read of the
  budget float; then one 32-bit read of the counter and one `IsAnyAnimationLoadingFromDisk` call per
  second); the toggle (Battle Load Diagnostics page,
  `Mission Performance` group, `EnableAnimMemoryProbe`, default on, read at each mission start,
  excluded from the co-op fingerprint); and a **log lines** table listing all eight lines from Step 5
  with their fields and the example strings. State that `pctOfBudget` above 100 is expected (the loader
  lets the total pass 12 MiB before the eviction pass trims it) and that `drops` counts one-second
  samples lower than the one before, a proxy for evictions. Add the twelve new production files
  (Scope) to `## Key Files` and the new test classes to `## Tests`.
- `docs/reference/feature-map.md:72`, the MissionPerf row: append, before ` See [mission-perf-heartbeat.md]`,
  `Also the \`[AnimMem]\` probe (\`AnimMemory/\`): on-demand animation clip bytes against the engine's 12 MiB budget, read-only, signature-guarded.`
- `docs/reference/engine/mission-frame-threads-and-native-costs.md` section 6: after the last bullet
  (line 165), add a bullet tagged **[TAOM-verified, Ghidra and a disp32 scan, 2026-10-02]**: the counter at
  `0xDABE40` is written only by `lock xadd` at `0x591319` (in `0x5911A0`, adding a clip's size field
  `+0x80` after a load) and at `0x21E0EF` (eviction) among rip-relative references, and read at
  `0x21E00F` and `0x830A3`; after its add, `0x5911A0` compares the total with `0xF00000` (15 MiB) and
  above it sets a once-flag and hands an object to `0x44400` (that this schedules eviction is an
  inference). Then one sentence: the `[AnimMem]` probe logs that counter against the budget once a
  second, locating both by signature, never by offset (link
  `[mission-perf-heartbeat.md](../../features/mission-perf-heartbeat.md)`). Re-verify each address you
  write with `python -B tools/native_sig_author.py disasm <rva> --n 8` before writing it.

**Verify**: `python tools/lint_docs.py --fail-on-drift` exits 0, and
`git diff HEAD -- docs | LC_ALL=C.UTF-8 grep -nP "^\+.*[\x{2013}\x{2014}]"` prints nothing (the
`LC_ALL` prefix is required: without it this machine's grep rejects `\x{2014}`). The new `.cs` files
are untracked, so a plain diff cannot see them; Step 11 checks them after staging.

### Step 11: full verification and commit

Run the RefAsm unit step, then the build, the full suite, `lint_docs`, and the done-criteria greps
below. Then stage the in-scope paths explicitly and run
`git diff --cached -- docs Main TAOM.Tests | LC_ALL=C.UTF-8 grep -nP "^\+.*[\x{2013}\x{2014}]"`: it
must print nothing (the staged diff against HEAD holds every new file and only added lines, so the em
dashes already in `BattleLoadDiagnosticsSettings.cs` do not count). Then commit with the subject and
`Not-tested:` trailer from "Git workflow". Body draft (re-check every claim against your code before
using it):

```
The engine keeps only 12 MiB of on-demand animation clip data and
evicts clips past that; a worker that then needs an evicted clip
blocks until it reloads, which shows up as a battle frame spike.
Whether TAOM battles hit that budget decides between making TAOM's
hot clips resident and raising the budget, so measure it first.

A new probe, on by default on the Battle Load Diagnostics page,
finds the engine's loaded-clip byte counter and its 12 MiB budget by
a signature check once per game session, then logs an [AnimMem] line
every 5 s in every mission (loaded KB, percent of budget, whether a
clip is loading, drops as an eviction proxy, window min and max) and
a summary at mission end. It only reads: if the signature, the
section checks or the 12582912 budget value do not match, it turns
itself off and logs why. It is excluded from the co-op settings
fingerprint like the frame-time heartbeat.
```

**Verify**: every Done criterion holds; `git log -1 --format=%s` prints the subject.

## Test plan

- New test classes, all under `TAOM.Tests/Features/MissionPerf/AnimMemory/`: `ClipBudgetSignatureTests`
  (parse, find: unique, none, two, wildcards, end, short; rip targets of the two real encodings;
  resolve on the real 50 bytes), `PeSectionTableTests` (parse and every reject, including a non-x64
Machine and a non-PE32+ magic; find; bounds),
  `ClipBudgetSignatureInstalledBinaryTests` (LiveInstall: the real DLL on disk), `AnimMemLineTests`
  (all eight formats literally), `AnimMemoryProbeTests` (armed header, once-only, every disable reason,
  NaN, negative, exception, disable latch, reads), `AnimMemorySessionTests` (start line, cadence, 5 s
  line, drops in and across windows, loading count, 90% boundary both sides, summary, negative read,
  exception, empty), `AnimMemoryProbeWiringTests` (module once, behaviour declared, singleton, default
  on, instrumentation, no native write). Plus the moved pinned count in `SettingsFingerprintTests`.
- Patterns to model: `TAOM.Tests/Features/MissionPerf/FrameStatsTests.cs` (pure cadence tests),
  `TAOM.Tests/Features/BattleCorpses/BattleCorpsesWiringTests.cs` (wiring),
  `TAOM.Tests/Adapters/MissionAdapterFactoryTests.cs:67` (`logger.Received(1).LogInfo(Arg.Is<string>(...))`).
- Not testable offline (the `Not-tested:` trailer, and only these): `NativeModuleMemoryAdapter`'s three
  members against the running game, `AnimationLoadingAdapter`, and
  `AnimMemoryProbeMissionBehavior`'s engine callbacks. Every decision behind them is in the tested
  classes.

## Done criteria

Machine-checkable. ALL must hold. Run the `git grep` checks after staging: `git grep` searches only
files in the index, so before `git add` it cannot see the new files.

- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0
- [ ] `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` shows `Failed: 1` (only
      `EveryLanguage_DeclaresARowForEveryEnglishKey`), `Skipped: 2`, and Passed equal to 12345 plus the
      number of new test methods
- [ ] `--filter "FullyQualifiedName~ClipBudgetSignatureInstalledBinaryTests"` shows `Passed: 2`
      (not skipped)
- [ ] The RefAsm unit step (Commands) fails only on the names Step 1 recorded, and every new test it
      runs passes (or the step is reported as not run, with its restore error). A new test that fails
      there only because it needs the installed engine is tagged `[TestCategory("RequiresGame")]`
      and named in your report
- [ ] `git grep -n -E "Marshal\.Write|VirtualProtect|WriteProcessMemory" -- Main/Features/MissionPerf Main/Adapters/NativeModuleMemoryAdapter.cs` prints nothing
- [ ] `git grep -n -E "0x21E00F|0xDABE40|0xB2E2DC|0x21E034" -- Main` prints nothing (no fixed offset in
      production code, comments included)
- [ ] `git grep -n "AnimMemory" -- Main/SubModule.cs Main/IoC.cs Main/TAOM.csproj` prints nothing
- [ ] `git grep -n "EnableAnimMemoryProbe" -- Main` lists `BattleLoadDiagnosticsSettings.cs`,
      `CoopSettingsRelevance.cs` and the behaviour
- [ ] `python tools/lint_docs.py --fail-on-drift` exits 0
- [ ] `git status --porcelain` lists only in-scope files, plus this plan file if it is untracked in
      your worktree (never stage or commit it)
- [ ] Every comment, doc line, test oracle and commit-body sentence this plan supplied was re-checked
      against the code or the binary it describes

## STOP conditions

Stop and report (do not improvise) if:

- `.claude/pinned-game-version.txt` is not `v1.5.3`, or `TaleWorlds.Native.dll` is not 14,209,376 bytes.
- Step 2's disassembly or scan differs from Current state (the pattern matches 0 or 2 or more times,
  or at another RVA), or the installed-binary tests compute targets other than `0xDABE40` and
  `0xB2E2DC` on that binary.
- The pattern cannot be made to match exactly once without changing it (do not shorten, lengthen or
  re-wildcard it on your own).
- `GetModuleHandleW`, `Marshal.Copy`, `Marshal.ReadInt32` or `BitConverter.ToSingle` is unavailable on
  net472 (a compile error you cannot fix by a `using`).
- The work seems to need `Main/SubModule.cs`, `Main/IoC.cs`, `Main/TAOM.csproj`, a protected file, or
  any write to native memory.
- The code at a "Current state" location does not match its excerpt (apart from plan 028's count change
  described in Step 9).
- A step's verification fails twice after a reasonable fix.

## Orchestrator steps (not the executor's)

- Issue: file it before dispatch and give its number to the executor for the commit body if wanted.
- No `/localize`: MCM labels and log lines are not localized text.
- After the executor's report, spot-check one `[AnimMem]` header and one 5 s line in a real
  `taom_debug_*.log` when the maintainer runs a battle (FOR-MIKE item 2 of the perf run).

## After merge: the maintainer's actions

Pull and deploy as usual. Then, per the perf run's FOR-MIKE item 2: one troll-heavy battle and one
large vanilla-troop battle with the probe on (it is on by default), and read the `[AnimMem]` lines and
summaries next to `[MissionPerf]`. A total sitting at or above 100% with frequent drops supports a
budget raise or resident clips; a total far below 12 MiB rules both out. No restart of Claude sessions
is needed (no hooks or settings change).

## Maintenance notes

- **Engine bump**: the probe disables itself with one reason line if the signature no longer matches,
  and `ClipBudgetSignatureInstalledBinaryTests` fails, which is the prompt for `/engine-bump` to
  re-derive the pattern with `tools/native_sig_author.py` (and update the v1.5.3 RVA test's pinned file
  length and RVAs).
- **Plan 028** edits the same files (`BattleLoadDiagnosticsSettings.cs`, `CoopSettingsRelevance.cs`,
  the pinned count in `SettingsFingerprintTests.cs`, `coop-interop.md`, `bannerlord-together-compat.md`,
  `mission-perf-heartbeat.md`, the feature-map row). Whichever merges second resolves the counts by
  adding both plans' settings; the engine reference page also says plan 028 records
  `IsAnyAnimationLoadingFromDisk`, which this plan does not change.
- **What the review should probe**: that no read can happen before both section checks pass
  (`AnimMemoryProbe.EnsureArmed` order); that the NaN budget path disables; that the 10 MB text copy
  is released after the scan; that the session never logs per frame; and that the 5 s line keeps
  every sample's information through its min, max, drops and loading count.
- **Deliberate additions to the brief's line format**: `minKB`, `maxKB` and `loadingSamples` on the
  5 s line, and `samples`, `startKB`, `endKB`, `peakPct`, `loadingSamples`, `stopped` on the summary,
  so no one-second sample is dropped silently (the maintainer's logging instruction D6). The summary
  counts `samplesAtOrAbove90Pct` rather than seconds because samples are one second apart only while
  frames are shorter than a second.
- **Deferred**: any write to the budget (a guarded four-byte patch) and any Loading Type change on
  TAOM clips are the maintainer's decisions after the measurement, not part of this plan. Also
  deferred: the Configuration table in `docs/features/battle-load-diagnostics.md` lacks both Mission
  Performance toggles (`EnableMissionPerfHeartbeat` already, `EnableAnimMemoryProbe` after this plan);
  add both rows once plans 028 and 036 have merged.
