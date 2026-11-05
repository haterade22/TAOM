# Plan 113: Tell a slow shader compile from a wedge: honest triage, a main-thread freeze sampler, notices that claim only what they can see

> **Executor instructions**: Follow this plan part by part, step by step. Run every verification
> command and confirm the expected result before moving on. If anything in "STOP conditions"
> occurs, stop and report; do not improvise. Parts A to E are each independently landable and each
> has its own done criteria; finish a part (all its commits green) before starting the next. When
> done, report status, commit hashes and the test totals you measured in the hand-off; do not edit
> `plans/README.md` (the integrator records them after the merge).
>
> **Drift check (run first, in the worktree)**:
> `git diff --stat 1b00d1acd..HEAD -- tools/triage_battle_load.py tools/tests/test_triage_battle_load.py Main/SubModule.cs Main/TAOM.csproj Main/Features/BattleLoadDiagnostics Main/Features/ShaderCompileNotice Main/Features/CoopInterop/CoopSettingsRelevance.cs Main/_Module/ModuleData/taom_module_strings.xml Main/_Module/ModuleData/Languages TAOM.Tests/Features/BattleLoadDiagnostics TAOM.Tests/Features/ShaderCompileNotice TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs docs/features/battle-load-diagnostics.md docs/features/shader-precompilation.md docs/features/coop-interop.md docs/features/bannerlord-together-compat.md docs/reference/provenance-register.md docs/reference/harmony-patch-registry.md .github/ISSUE_TEMPLATE/battle-load-hang.md`
> Changes by earlier WPs of this programme are expected (WP104: `Main/SubModule.cs`, two strings
> in the triage tool, `taom_module_strings.xml`, the 12 `Languages/*/std_taom_module_strings_*.xml`
> files and `battle-load-diagnostics.md`; WP105: `Main/SubModule.cs`; WP106: BattleLoadDiagnostics files,
> `Main/SubModule.cs`, `CoopSettingsRelevance.cs`, `SettingsFingerprintTests.cs`,
> `battle-load-diagnostics.md`, `coop-interop.md`, `bannerlord-together-compat.md`; WP107:
> `Main/SubModule.cs`, `battle-load-diagnostics.md`, `BattleLoadStallWatchdog.cs`, the Patch37 section of
> `harmony-patch-registry.md`; WP108: `Main/SubModule.cs` (the Patch90 block) and its own section of `harmony-patch-registry.md`; WP111:
> `CoopSettingsRelevance.cs`, `SettingsFingerprintTests.cs`, `taom_module_strings.xml`; WP112:
> `OnBeforeInitialModuleScreenSetAsRoot` in `Main/SubModule.cs` now calls `CustomizeMenu` through
> `MainMenuStep`, new `Main/Composition/MainMenuStep.cs`). Compare the live
> files against the excerpts below and STOP only on a real mismatch (see STOP conditions).

## Status

- **Priority**: P1 (Part B alone is P2, but Part C reuses its counter, so it lands first)
- **Effort**: L (five parts; each fits one executor session or less)
- **Risk**: MED (a background thread that may suspend the main thread, a per-frame stamp, OS windows over the game; every piece fails open and has its own off switch or stand-down)
- **Depends on**: WP106 merged (it brings `ThreadStackCapture`, `MissionTickStallProbe` and the `[MissionStall]` watchdog from `f75288ebf`); transitively WP101 to WP105 (WP104's plan 009 changes the Patch43 failure string the triage tool quotes)
- **Category**: diagnostics / perf (load hang, freeze)
- **Wave / lane**: wave 1, lane C
- **Executor**: laptop
- **Decisions applied**: D1 (no `CHANGELOG.md` staging; the commit body is the entry), D2 (both features stay hand-wired: neither has a 1.5.x `*Module.cs`), D4 (ShaderPrecompilation stays ACTIVE; evidence recorded in Part A), D7 (no `Client/` or co-op hunks), D8 (subjects carry `v2.0.28`), D10, D11 (ShaderCacheKeeper not ported), D12 (bow shader sources not ported), D19 (git only through the Bash tool)
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x `origin/bannerlord-1.5.x` `e45f6df2c`, 2026-11-04. (The 1.5.x tip moved to `b7a3ac17b` while planning; `git log e45f6df2c..b7a3ac17b` touches none of this plan's paths.)
- **Issue**: port of #752 (shader compile notice) and #1025 (triage render silence). New work (freeze sampler, render-wait notice, stale-notice evidence, opt-in dump, shader-cache state line): the orchestrator creates the issue before Part C lands; cite it as `Refs #<n>` in the Part C, D and E bodies.

## Why this matters

On 2026-11-01 a Harad player's first battle sat on the loading screen: two `WaitingForRender` samples
(E3 lines 218-219: `shaders=2720`, then `shaders=2697` 2.26 s later), then 92 s of silence until the
log ended; the previous session's load of the same scene never reached playable either (E3 line 17, stale in-flight marker). The
same player's next session (E4) shows a 68 s silence across a load that then completed (lines
2125-2131: 1569 to 0) and a second silent stall that never finished (line 5138: 806 pending, at least
206 s). The verification pass (V02) found the logs cannot decide compile versus wedge: the shader count
is read and published only by the main thread, so a blocked main thread freezes the number the 300 s
watchdog reads, and the 1.4.x triage tool calls E3 "a COLD SHADER CACHE ... DRAINING", the reading the
1.5.x line withdrew (`89374fc4f`) after a public 1.4.8 bundle of exactly this shape (#612). A player can therefore kill a load that was still working (V02: not
established for E3) or report a hang, with nothing in the log that names the frame. After this plan: the triage tool stops calling a silent render
wait "draining"; the game logs `[Freeze]` lines from 10 s of main-thread silence with the main thread's
stack and the engine's own `compile_shader` count; a window over the loading screen says "compiling"
only while compile lines are arriving; and the next-session notice says what the last load was doing.

## Current state

### Files and roles (1.4.x at `1b00d1acd`, equal to the working tree `c5b84fb41` for all of them)

| Path | Role |
|---|---|
| `tools/triage_battle_load.py` (1,413 lines) | Offline log triage. Identical to 1.5.x `89374fc4f^` except two Patch43 strings (`:530`, `:1096`) |
| `tools/tests/test_triage_battle_load.py` (153 tests) | Identical to 1.5.x `89374fc4f^` |
| `Main/Features/BattleLoadDiagnostics/Hooks/MissionState_OnTick_RenderWait_Patch.cs` | Main-thread postfix that reads the native shader count and publishes it, 1 Hz line |
| `Main/Features/BattleLoadDiagnostics/BattleLoadRenderWaitProbe.cs` | Static handoff of that count to background threads |
| `Main/Features/BattleLoadDiagnostics/BattleLoadStallWatchdog.cs` | 5 s background poll; `STILL LOADING` at `StallWatchdogSeconds` (300) |
| `Main/Features/BattleLoadDiagnostics/ExitStallSampler.cs` | The `Thread.Suspend` stack-capture precedent (#331); WP106 moves its capture into `ThreadStackCapture` |
| `Main/Features/BattleLoadDiagnostics/BattleLoadStallMarker.cs`, `StallReportNotifier.cs`, `Domain/StallMarkerInfo.cs` | In-flight marker and the next-session inquiry |
| `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs`, `...SettingsProvider.cs`, `IBattleLoadDiagnosticsSettingsProvider.cs`, `BattleLoadDiagnosticsIoC.cs` | MCM page, provider, feature IoC (not single-owner) |
| `Main/Features/CoopInterop/CoopSettingsRelevance.cs` | Every MCM property must be classified; instrumentation list at `:52-69` |
| `TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs:211` | `AssertSplit(typeof(BattleLoadDiagnosticsSettings), reflected: 7, covered: 0);` (WP106 makes it 8) |
| `docs/features/coop-interop.md:310-313`, `docs/features/bannerlord-together-compat.md:291` | Quote the MCM total (`**243**`, `243 MCM settings`) and the BattleLoadDiagnostics split (7); `EveryDocQuotingTheSettingsCounts_AgreesWithReflection` checks the total |
| `Main/SubModule.cs` (single owner; edits listed per part) | `IoC.Configure();` `:114`; `OnBeforeInitialModuleScreenSetAsRoot` `:525-527`; stale marker `:556-562`; sampler starts `:580-591`; Patch21 `:421-426`; `TaomPrecompileShaders` option `:638-664`; Patch43 `:1672-1677`; `OnApplicationTick` `:1913-1930` |
| `Main/TAOM.csproj` (single owner) | No WinForms or Drawing reference today; `System.IO.Compression.FileSystem` reference at `:34` |
| `Main/_Module/ModuleData/taom_module_strings.xml` | English rows; file ends with the SpecialResources block, then `</strings>` |

### Excerpts

`MissionState_OnTick_RenderWait_Patch.cs:53-72` (the count exists only while main-thread frames run):
```csharp
public static void Postfix(MissionState __instance)
    if (!__instance.FirstMissionTickAfterLoading) return;
    if (!BattleLoadLoadingWindow.IsOpen) return;
    ...
    try { shaders = Utilities.GetNumberOfShaderCompilationsInProgress(); }
    ...
    BattleLoadRenderWaitProbe.Publish(shaders, DateTime.UtcNow);
    _service?.NoteWaitingForRender(shaders);
```
`BattleLoadRenderWaitProbe.cs:16-18`: "`Utilities.GetNumberOfShaderCompilationsInProgress()` is a native
call and is read ONLY from the main thread ... instead of calling into the engine off-thread." Never
call that method from any new background code.

`BattleLoadStallWatchdog.cs:22` 5 s poll; `:33` `ShaderNoProgressSeconds = 60d`; `:64`
`public static volatile bool SuppressStallDetection;` (true while a shader-precompile walk runs);
`:115-127` `Decide` returns `FireWedge` for a count unchanged 60 s. `BattleLoadDiagnosticsSettings.cs:36`
`StallWatchdogSeconds { get; set; } = 300;` So the watchdog fires at 300 to 305 s and cannot tell a
compile that blocks the main thread from a wedge (V02).

`tools/triage_battle_load.py:947` (1.4.x), the rule that calls E3 draining:
`"draining": (len(shader_counts) > 1 and shader_counts[-1] < shader_counts[0]) or None,`

`Main/SubModule.cs:1913-1915`:
```csharp
protected override void OnApplicationTick(float dt)
{
    _timeAccelerationService?.OnTick();
```
`Main/SubModule.cs:556-561` (main menu): `var stale = stallMarker?.TryConsumeStaleMarker(); if (stale != null) Features.BattleLoadDiagnostics.StallReportNotifier.Notify(stale);`
`StallReportNotifier.cs:43-47`: `BuildMessage` starts "A battle or scene load in your previous session did not finish (the game was closed while still loading)." and the body is hard-coded English (title and buttons are localized).
`BattleLoadStallMarker.cs:74-97` `TryConsumeStaleMarker` reads, parses (`Parse`, `:114-131`), logs a WARNING, deletes; the marker holds `scene=`, `utc=`, `log=` (absolute path of the prior `taom_debug` log).

### Engine facts verified on the installed v1.4.8 (`pwsh -NoProfile -File tools/taom-src.ps1 path <Type>`, cache `C:\Users\mikew\.taom-src\v1.4.8`)

- `TaleWorlds.MountAndBlade.MBSubModuleBase`: `protected internal virtual void OnApplicationTick(float dt)` (`:40`).
- `TaleWorlds.MountAndBlade.Module`: `internal void OnApplicationTick(float dt)` (`:463`); it ticks the game state manager and `GameManagerBase.Current.OnTick(dt)`, then `foreach (MBSubModuleBase item in CollectSubModules()) item.OnApplicationTick(dt);` (`:521-524`).
- `TaleWorlds.MountAndBlade.CoreManaged`: `void IManagedComponent.OnApplicationTick(float dt) { Module.CurrentModule.OnApplicationTick(dt); }` (`:120-123`).
- `TaleWorlds.DotNet.Managed`: `internal static void ApplicationTick(float dt)` (`:290`) AND `internal static void ApplicationTickLight(float dt)` (`:304`); both loop `_components[i].OnApplicationTick(dt)`. Consequence: stamp the heartbeat in TAOM's `SubModule.OnApplicationTick` override, which every native frame reaches through either entry (except a frame whose game-state tick throws before the submodule loop: see the blind spot in Step C3). A prefix on `Managed.ApplicationTick` alone would miss the light ticks. No Harmony patch is needed.
- `Module.SetInitialModuleScreenAsRootScreen()` (`:750`) calls each submodule's `OnBeforeInitialModuleScreenSetAsRoot()` (`:758`); it runs inside `Module.OnApplicationTick`, so on the main thread.
- `Module.Initialize()` (`:246`): `LoadLocalizationXmls();` (`:262`) runs before `LoadSubModules(modules, loadNewModules: false);` (`:267`), so `TextObject` resolves in `OnSubModuleLoad`.
- `TaleWorlds.ModuleManager.ModuleHelper`: `public static List<ModuleInfo> GetActiveModules()` (`:196`).
- `TaleWorlds.Localization.TextObject`: `public TextObject(string value, Dictionary<string, object> attributes = null)` (`:71`); `public TextObject SetTextVariable(string tag, string variable)` (`:334`).
- `TaleWorlds.Engine.Utilities`: `public static int GetNumberOfShaderCompilationsInProgress()` (`:597-599`), main thread only (above).
- `TaleWorlds.MountAndBlade.MissionState`: `public bool FirstMissionTickAfterLoading { get; private set; }` (`:37`), `protected override void OnTick(float realDt)` (`:78`), the `Handler.RenderIsReady()` gate (`:110`), cleared at `:208`.
- Native (from the D3 verification report, which disassembled the installed `TaleWorlds.Native.dll`; report read this session, not re-disassembled): the engine logs `compile_shader: %s, %s, %s, %d, %d, %d, %d.` per compiled shader; at startup it compares the sorted module-id list stored in `%ProgramData%\Mount and Blade II Bannerlord\Shaders\CoreShaders\D3D11\shader_mapping.bin` and, on a change of the module SET or game build, deletes `*.sacx|*.bin|*.changeset` there and rewrites the mapping header. Reordering alone does not trigger it (the list is sorted before the compare).
- `MiniDumpWriteDump` (Part E): the shipped `Dependencies/_Module/bin/Win64_Shipping_Client/Bannerlord.ButterLib.dll` (ilspycmd, read this session) declares `[DllImport("dbghelp.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] static extern bool MiniDumpWriteDump(IntPtr hProcess, uint ProcessId, SafeHandle hFile, MINIDUMP_TYPE DumpType, ...3 optional pointers)` and `MiniDumpWithUnloadedModules = 0x20`, `MiniDumpWithThreadInfo = 0x1000`, and writes a dump of its own process this way.

### What WP106 must have provided (from `f75288ebf`; STOP before Part A if absent)

- `Main/Features/BattleLoadDiagnostics/ThreadStackCapture.cs`: `internal static class ThreadStackCapture` with `public static StackTrace Capture(Thread thread)` (suspend, walk, resume; throws on failure) and `public static string FormatFrames(StackTrace stack)` (one `    at Type.Method` line per frame).
- `MissionTickStallProbe.cs`: `public sealed class MissionTickStallProbe` with `public static readonly MissionTickStallProbe MissionFrame`, `public bool TryRead(out long sinceUtcTicks, out Thread? thread)`, `internal void EnterAt(long utcTicks)`, `public void Exit()`.
- `MissionTickStallWatchdog` writing `[MissionStall]` at 10/20/40 s, and `IBattleLoadDiagnosticsSettingsProvider.MissionTickStallSamplerEnabled` plus the MCM property `EnableMissionTickStallSampler` in group `"Exit Stall Sampler"`.
- f75288ebf's own doc text: "A thread stopped inside native code is suspended only at its next managed safe point, so a stack can come back as `<capture failed: ...>`". Expect that for a render-wait block.

### Conventions that bind this work

- **TDD**: failing test first, then the code (RED, GREEN, REFACTOR). Model C# tests on `TAOM.Tests/Features/BattleLoadDiagnostics/MissionTickStallWatchdogTests.cs` (MSTest, NSubstitute, explicit UTC ticks, a `DescribeStack` seam that suspends nothing).
- **ADR-007**: services never touch sealed TaleWorlds types. The watchdog, policies, parsers and formatters here use no engine type at all; `TextObject` and `ModuleHelper` appear only in the named boundary code that runs on the main thread (`ShaderCompileNoticeEntry` in B1, `RenderWaitNoticeTexts.Resolve` in D1, `StallReportNotifier.BuildMessage` in D2).
- **ADR-002**: thin entry points under 150 lines; every `SubModule.cs` edit here is one guarded call.
- **ADR-003** no `#region`; **ADR-004** no `[Obsolete]`; **ADR-005** no `#if DEBUG`.
- **MCM json2 rule**: never flip a persisted default; the two new settings are new names. Every MCM property needs `RequireRestart = false` (`SettingRequireRestartPostureTests`), a `CoopSettingsRelevance` classification and the fingerprint count.
- **Log contracts are twin-pinned**: a C# formatter test asserts an exact literal and `tools/tests/test_triage_battle_load.py` parses the same literal (the `[MemSample]` precedent, #386).
- **Never** call `Utilities.GetNumberOfShaderCompilationsInProgress()` off the main thread; **never** add a `Thread.Suspend` call outside `ThreadStackCapture`; **never** lower process priority during a battle (D3).
- **Localization**: every new `{=taom_...}` literal needs an English row in a ModuleData strings XML (`UnregisteredLocalizationKeyBaselineTests`) AND a row with the same id in each of the 12 `Main/_Module/ModuleData/Languages/<LANG>/std_taom_module_strings_<code>.xml` files: `TAOM.Tests/Infrastructure/Localization/LanguageFileCoverageTests.cs` (`EveryLanguage_DeclaresARowForEveryEnglishKey`, no baseline) fails the full suite otherwise. Those language rows carry the English text verbatim (no `{=key}` prefix), which is the translator's own "untranslated" signal (`AccentStrippedTranslationTests` summary; the 1.4.x precedent is `docs/features/shader-precompilation.md`, "Translation state"). Add them only with the "Language rows helper" in the Commands section; never translate, never touch any other language file; WP135 runs the translator.
- `.claude/rules/harmony-patches.md` is not engaged: this plan adds no Harmony patch.

## Commands you will need

Run from the worktree root `C:\Users\mikew\source\repos\taom-port\113`.

| Purpose | Command | Expected |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | 0 failed |
| Filtered | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter FullyQualifiedName~<Class>` | 0 failed |
| Triage tests | `python tools/tests/test_triage_battle_load.py` | `OK`, test count as stated per step |
| Tool suite | `python -m unittest discover -s tools/tests -t .` | no new failure vs your A0 baseline |
| Docs | `python tools/lint_docs.py` | no new finding in touched files |
| Dash check | `git diff HEAD -U0 -- <files> \| python -X utf8 -c "import sys; b=[l for l in sys.stdin.read().splitlines() if l.startswith('+') and ('\u2014' in l or '\u2013' in l)]; print(len(b)); print('\n'.join(b))"` | `0` (run on staged doc/prose changes before each commit; use `--cached` instead of `HEAD` after staging) |

The `\|` in the Dash check cell is the table's escaped pipe: type a plain `|`. Keep `-X utf8`: without
it Python 3.14 on this laptop decodes the pipe as cp1252 and prints `0` even when the diff holds an em
or en dash (checked 2026-11-04 with a test line holding both).

Both `-p:` flags are mandatory on every build and test (`Main/TAOM.csproj:133-136` does not gate the
copy target on `DisableModuleCopy`). Never `./build.ps1`. Never two builds at once in one worktree.

Executor environment: the maintainer's laptop; Bannerlord v1.4.8 (build 119303) is installed at
`C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord` (env `BANNERLORD_GAME_DIR`).
Never write into the game install and never start the game: the in-game checks are the maintainer's.

**Language rows helper** (B1, D1 and D2 use it; run from the worktree root in one Bash call after
replacing the `ROWS` list with the step's rows). It appends each row, with the English text verbatim,
just before `  </strings>` in each of the 12 module-strings language files, byte for byte (the files
have no BOM, end their rows with CR LF, and close with `  </strings>` CR CR LF), and refuses to write
a file that already has a key or no longer parses:
```bash
python -X utf8 - <<'EOF'
import glob, pathlib, xml.etree.ElementTree as ET
ROWS = [("<key>", "<English text>")]
files = sorted(glob.glob("Main/_Module/ModuleData/Languages/*/std_taom_module_strings_*.xml"))
assert len(files) == 12, files
for f in files:
    p = pathlib.Path(f)
    data = p.read_bytes()
    assert data.count(b"\n  </strings>") == 1, f
    for key, _ in ROWS:
        assert ('id="%s"' % key).encode() not in data, (f, key)
    rows = b"".join(('    <string id="%s" text="%s" />' % (k, t)).encode("utf-8") + b"\r\n" for k, t in ROWS)
    new = data.replace(b"\n  </strings>", b"\n" + rows + b"  </strings>", 1)
    ET.fromstring(new)
    p.write_bytes(new)
print("ok", len(files))
EOF
```
**Verify** (each use): it prints `ok 12`; `git diff --numstat -- Main/_Module/ModuleData/Languages`
shows `<number of rows> 0` for each of the 12 files and nothing else.

## Scope

**In scope** (the only files you may modify or create):
- `tools/triage_battle_load.py`, `tools/tests/test_triage_battle_load.py`
- `Main/Features/ShaderCompileNotice/**` (new folder), `TAOM.Tests/Features/ShaderCompileNotice/**` (new)
- `Main/Features/BattleLoadDiagnostics/**` (new files listed per part; edits to `BattleLoadDiagnosticsSettings.cs`, `BattleLoadDiagnosticsSettingsProvider.cs`, `IBattleLoadDiagnosticsSettingsProvider.cs`, `BattleLoadDiagnosticsIoC.cs`, `BattleLoadStallMarker.cs`, `StallReportNotifier.cs`, `Domain/StallMarkerInfo.cs`; Part A3: the header comment of `Hooks/MissionState_TickLoading_BattleLoad_Patch.cs`, no code), `TAOM.Tests/Features/BattleLoadDiagnostics/**`
- `Main/Features/CoopInterop/CoopSettingsRelevance.cs`, `TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs`
- **Single-owner, explicitly in scope**: `Main/SubModule.cs` (Part B: `OnSubModuleLoad` two calls, `OnBeforeInitialModuleScreenSetAsRoot` one call; Part C: `OnApplicationTick` one line, `OnBeforeInitialModuleScreenSetAsRoot` one call) and `Main/TAOM.csproj` (Part B: two references).
- `Main/_Module/ModuleData/taom_module_strings.xml` (English rows only) and the 12 `Main/_Module/ModuleData/Languages/<LANG>/std_taom_module_strings_<code>.xml` files (the same ten rows with the English text verbatim, appended by the "Language rows helper", nothing else)
- Docs: `docs/features/battle-load-diagnostics.md`, `docs/features/shader-precompilation.md`, `docs/features/shader-compile-notice.md` (new), `docs/features/coop-interop.md`, `docs/features/bannerlord-together-compat.md`, `docs/reference/provenance-register.md`, `docs/reference/harmony-patch-registry.md` (Part A3: the `TickLoading` paragraph of the Patch43 section only), `.github/ISSUE_TEMPLATE/battle-load-hang.md`

**Out of scope** (do NOT touch):
- `Main/IoC.cs` (nothing here needs it; if a registration seems to, STOP).
- `CHANGELOG.md` (D1), `.claude/**`, `CLAUDE.md`, `AGENTS.md`, `docs/reviews/lessons/*`, `docs/reviews/LESSONS-LEARNED.md`, `docs/reviews/REVIEW-LOG.md`, `docs/INDEX.md`, `docs/reference/feature-map.md`, `tools/README.md` (WPs 142 to 144 sync these), any `docs/reviews/rca-*.md`.
- Every other file under `Main/_Module/ModuleData/Languages/`, any translation of the new rows, and `tools/translation_cache/*` (WP135).
- `tools/package_release.py` and its tests (`d99398f12`'s shader gate is WP115's).
- `Native/**` (D11), `Main/_Module/Shaders/**` and any ShaderSourceInstall code (D12), `Client/**` (absent on 1.4.x), TrollBruteForce and BannerBearers code (`25995dc91`; the banner-bearer model is WP130's).
- `Main/Features/ShaderPrecompilation/**` and the Patch21 and `TaomPrecompileShaders` wiring (D4 keeps them as they are).
- `Main/Features/CrashReport/**` (the `LogTailCollector` rgl selection issue is deferred, see Maintenance notes).
- `BattleLoadStallWatchdog` thresholds and the `StallWatchdogSeconds` default (json2 rule).

## Git workflow

1. Worktree (Bash tool, from the main checkout):
   `git -C /c/Users/mikew/source/repos/TAOM worktree add /c/Users/mikew/source/repos/taom-port/113 -b port148/113-shader-stall-and-freeze-watchdog bannerlord-1.4.5`. Work only there.
2. Run git only through the Bash tool (D19). Never `git add -A`, `-u` or `.`; stage explicit paths.
3. Cherry-pick only with `git cherry-pick -n <sha>`; never let cherry-pick, `--continue`, `-C`, `-c` or `--amend --no-edit` create the commit (`GIT_EDITOR=true` on this laptop would commit the 1.5.x message unedited).
4. Bookkeeping hunks from a cherry-pick: a conflicted bookkeeping path gets `git checkout --ours <path>` then `git add <path>`; a cleanly changed one gets `git checkout HEAD <path>`.
5. Commit: `git add <paths>`, write the message to `/c/Users/mikew/source/repos/taom-port/113-msg.txt` (outside the worktree), then in the NEXT, separate Bash call `git commit -F /c/Users/mikew/source/repos/taom-port/113-msg.txt`, then delete that file. Never in the same call as `git add`, never in a parallel call, never through PowerShell. Never `--no-verify`; if a hook blocks, read its message and fix the cause or STOP.
6. Message template (subject at most 72 characters; keep the 1.5.x type and scope for ports):
   ```
   <type>(<scope>): v2.0.28 - <description>

   <Body for 1.4.x readers, wrapped at 72: what changed on the 1.4.8 line
   and why. It becomes the release note (D1). No em or en dashes. No
   Co-Authored-By or "Generated with" line.>

   Research: <what was verified on v1.4.8>
   Not-tested: <what needs an in-game check>
   Ported-from: <40-hex sha>      (one line per included 1.5.x commit; omit for new work)
   ```
7. Build and run the filtered tests after each commit; the full suite at the end of each part. Never push, open a PR, tag or merge into `bannerlord-1.4.5`; the integrator merges.

---

## Part A: the triage tool reads a silent render wait honestly (Python and docs)

### Step A0: baseline and dependency checks

```
cd /c/Users/mikew/source/repos/taom-port/113
dotnet restore TAOM.sln
dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=
dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=
python -m unittest discover -s tools/tests -t .
git grep -n "internal static class ThreadStackCapture" -- Main/
git grep -n "public static readonly MissionTickStallProbe MissionFrame" -- Main/
git grep -n "MissionTickStallSamplerEnabled" -- Main/Features/BattleLoadDiagnostics/IBattleLoadDiagnosticsSettingsProvider.cs
git diff 89374fc4f^ HEAD -- tools/triage_battle_load.py tools/tests/test_triage_battle_load.py
git grep -c "GetNumberOfShaderCompilationsInProgress" -- Main/
```
**Verify**: build exit 0; test run 0 failed (record total, passed, skipped: this is your C# baseline);
record the tool-suite failure count; each of the three `git grep -n` lines prints exactly one match (else
STOP: WP106 is not merged); the `git diff` prints either nothing (WP104 brought `bdf7d515c`'s
`[PatchApply]` hint) or exactly two hunks changing `'[PatchApply] Patch43_BattleLoadDiagnostics FAILED'`
back to `'Patch43 diagnostics failed to apply'` (WP104 did not). Anything else: STOP. Record the last
command's output: on `1b00d1acd` it is five `file:1` lines (two real calls, both on the main thread:
`Hooks/MissionState_OnTick_RenderWait_Patch.cs` and `ShaderPrecompilation/ShaderPrecompileRunner.cs`;
three comments: `BattleLoadRenderWaitProbe.cs`, `IBattleLoadDiagnosticsService.cs`,
`ShaderPrecompileDecider.cs`). This plan must leave that output unchanged.

### Step A1: transplant the triage series end state (89374fc4f, d99398f12, b9eabd712, 76a395729, 1581d4d0a)

Method: end-state transplant of two files. The 1.4.x files equal the 1.5.x parent of `89374fc4f`
(checked this session), and `1581d4d0a`'s copies equal the 1.5.x tip, so checking out `1581d4d0a`'s
two files replays exactly the triage hunks of the five commits. Simulation (`git merge-tree` onto
`origin/bannerlord-1.4.5`): `89374fc4f` conflicts only in `docs/reviews/lessons/state-lifecycle-save.md`;
`d99398f12`'s triage hunks merge cleanly (it conflicts only in files this step does not take);
`b9eabd712`, `76a395729` and `1581d4d0a` conflict in the two triage files (their predecessors are
missing) and in unrelated files this step does not take. No other 1.5.x commit (merges included)
changed the two triage files between `89374fc4f^` and `1581d4d0a` (`git log` checked 2026-11-04).

1. `git checkout 1581d4d0a tools/triage_battle_load.py tools/tests/test_triage_battle_load.py`
2. Patch43 strings: run `git grep -n "Patch43 diagnostics failed to apply" -- Main/SubModule.cs`.
   If it prints a match (plan 009 did not convert Patch43), edit the two strings in
   `tools/triage_battle_load.py` that read `'[PatchApply] Patch43_BattleLoadDiagnostics FAILED'`
   (in `_classify_render_wait` and `_format_timings`) to `'Patch43 diagnostics failed to apply'`.
   If it prints nothing, leave them (they match `PatchCategoryApplier`'s `[PatchApply] {category} FAILED`).
3. In `_POST_LOAD_FREEZE_STEPS` replace the bullet
   `"On v2.0.35 and later the log also carries [MissionStall] and [SkeletonBuffer] lines: "`
   `"read them for the last thing the game thread did.",` with
   `"Builds with the battle-freeze sampler (#634) write [MissionStall] lines and builds with the "`
   `"skeleton-buffer watch write [SkeletonBuffer] lines: read them for the last thing the game "`
   `"thread did.",` (1.4.x has no v2.0.35; the test only asserts both tags).
4. `git add tools/triage_battle_load.py tools/tests/test_triage_battle_load.py`

**Verify**: `python tools/tests/test_triage_battle_load.py` prints `Ran 187 tests` and `OK` (153 + 34
ported; if the count differs, report it, but 0 failures is mandatory). Tool suite: no new failure.

Commit A1 subject: `fix(tools): v2.0.28 - triage tells a silent render wait from a drain`
Body draft: "The battle-load triage tool no longer calls a stuck loading screen a draining shader cache
when the 1 Hz render-wait lines stopped long before the log or the watchdog line. The count in those
lines comes from the main thread, so when it blocks the count freezes with it. When the stream went
silent for 30 s or more, the verdict now says the log alone cannot separate a slow compile from a
wedge and points at the compile_shader lines in the full rgl_log and a hang dump. It reports only the
last load's own watchdog line, words a stuck shaders=0 wait consistently, and flags a flat heapMB tail
after BattlePlayable (the #634 freeze shape)." Trailers: `Research: 1.4.x tool equals 1.5.x 89374fc4f^ apart from the Patch43 hint; v1.4.8 MissionState.cs:110 gate`;
`Not-tested: none (offline tool)`; `Ported-from:` lines for `89374fc4f26683f3978072d30aed0832e9a3815e`,
`d99398f1210eeabc67a5158c56479fb9a5f6749f`, `b9eabd71250e4861fe3921fa1dfea5973e7c2c36`,
`76a3957297d2733afcce95c45e234ab8b2d931a1`, `1581d4d0adfa0d6be4f04423de18f8ad99f01f8a`; `Refs #1025`.

### Step A2: same-session compile-rate heuristic (new, TDD)

V02's rule 1: when the same log holds an earlier render wait whose 1 Hz stream went silent and came
back with a lower count (E4's first tournament: 1569 at 20:21:20, 0 at 20:22:29, then BattlePlayable),
print the stalled load's silence against that rate, labelled as a heuristic. Never word it as a verdict.

RED: add `class SessionRenderRateTests(unittest.TestCase)` to `tools/tests/test_triage_battle_load.py`
with a local `_rw(seq, ms, waited, shaders, ts)` helper (same body as `RenderWaitSilenceTests._rw`):
1. `test_session_rate_line_compares_a_stalled_load_with_an_earlier_blocked_compile`: log = `_init()`,
   `_init_done()`, `_finish_begin()`, `_after_start_begin()`, `_after_start_done()`, `_finish_done()`,
   `_rw(1715, 88359, 1, 1569, "2026-11-01 20:21:20")`, `_mem(ts="2026-11-01 20:21:49")`,
   `_rw(1716, 156648, 68290, 0, "2026-11-01 20:22:29")`, `_playable_at("2026-11-01 20:22:30")`, then a
   second load (`_init(seq=1600, ms=107600)`, `_init_done(seq=1601, ms=108000)`, `_finish_begin(seq=1602, ms=110000)`,
   `_after_start_begin(seq=1603, ms=110500)`, `_after_start_done(seq=1604, ms=111000)`, `_finish_done(seq=1639, ms=111326)`),
   `_rw(1640, 111327, 1, 806, "2026-11-01 20:48:25")`, `_mem(ts="2026-11-01 20:51:51")`.
   Assert the verdict is `RENDER_WAIT`, and the report contains `heuristic`, `1569`, `69s`, `about 35s`
   and `206s`.
2. `test_no_session_rate_without_an_earlier_silent_drain`: only the second load; report has no `heuristic`.
3. `test_short_gap_between_samples_is_not_a_session_rate`: first pair 20 s apart (below
   `RENDER_WAIT_SILENCE_THRESHOLD_S`); no `heuristic` line.
4. `test_json_carries_session_render_rate`: CLI `--json` on log 1 has `session_render_rate` with
   `shaders=1569`, `seconds=69`; on log 2 the key exists and is `null`.

GREEN: in `tools/triage_battle_load.py` add `session_render_rate(tl) -> dict | None`: scan `tl.events`
for consecutive `WaitingForRender` pairs inside one mission segment (no mission-start phase between,
see `_MISSION_START_PHASES`) before the last mission's start, whose stamps differ by at least
`RENDER_WAIT_SILENCE_THRESHOLD_S` (use `_seconds_between`) and whose later `shaders=` is lower; take the
most recent pair; return `{"shaders": drop, "seconds": gap, "per_second": round(drop / gap, 1)}`.
Render one line in the render-wait block of `_format_timings` only when the current load's
`silent_s` is not None:
`Same-session rate (heuristic): an earlier load in this log cleared 1569 shaders across a 69s render-wait silence (22.7/s). At that rate the 806 pending here need about 35s; this load has been silent 206s.`
(the estimate is `round(pending * seconds / shaders)` from the UNROUNDED rate, `pending` being the
current load's last `shaders=`: 806 x 69 / 1569 gives 35; dividing by the rounded `per_second` gives
806 / 22.7, which rounds to 36 and fails test 1. `_format_timings` receives only the timings dict, so
compute the rate in `classify_phase_timings` and carry it there, for example as
`render_wait["session_rate"]`). Add `"session_render_rate"` to the `--json` payload next to
`post_load_freeze`. Verdict and exit code unchanged.

**Verify**: `python tools/tests/test_triage_battle_load.py` prints `Ran 191 tests` and `OK`.

Commit A2 subject: `feat(tools): v2.0.28 - triage compares a stall with the session's rate`
Body: one paragraph explaining the heuristic and that it never changes the verdict. Trailers:
`Research: player logs of 2026-11-01 (E4: 1569 to 0 across 69 s; 806 then 206 s silent)`, `Not-tested: none`.

### Step A3: docs that say what the evidence supports (D4 record, V02 guidance, #612)

1. `docs/features/battle-load-diagnostics.md`:
   - Replace the whole paragraph starting `Verdicts:` (1.4.x `:680`) with the one from
     `git show 1581d4d0a:docs/features/battle-load-diagnostics.md` (the line starting `Verdicts:`), then
     append two sentences: the post-load freeze check (flat `heapMB` across 3 or more `[MemSample]` lines
     after the last `BattlePlayable` adds a "Post-load freeze check" section; verdict stays COMPLETED)
     and the A2 heuristic line (labelled, decoration only).
   - In "The render-ready gate" section replace the bullet at `:280-282` ("... a count that stops
     changing is the wedge.") with: the line is written at most once a second, on render-wait frames,
     and its count is published only by the main thread; while frames run a falling count is a working
     load; once the stream goes silent the count is frozen with the blocked thread and proves nothing
     either way (public 1.4.8 bundle of 2026-10-18: 3128 then 3066, then about 290 s of silence; E4 of
     2026-11-01: 68 s of silence across a load that then completed). Only the engine's `compile_shader`
     lines in the full `rgl_log_<pid>.txt` tell a slow compile from a wedge. Add one sentence on #612:
     one public 1.4.8 report (2026-10-19) says every large battle loaded again after the player swapped
     an elf hero's Warg mount for a horse; that is a player-reported cure, not a proven mechanism, and the
     2026-11-01 Harad party had no creature mount.
   - After the `### Read a hang log` subsection add `### When a battle sits on the loading screen`
     with these seven bullets (source: V02 section 4, verified by the orchestrator on 2026-11-04):
     (1) last BattleLoad line `WaitingForRender` and no `BattlePlayable`: held at the render gate,
     compiling or wedged; `BattlePlayable` present and the game froze as the loading screen dropped:
     suspect a missing collision body (`rgl_log_errors_<pid>.txt`: `get_object failed for body:`);
     (2) to tell compile from wedge while stuck, open the newest
     `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_<pid>.txt`: new `compile_shader:` lines
     with fresh timestamps mean it is compiling; a warm cache logs none; (3) wait at least 5 minutes on a
     first battle after a game update or a change to the set of active mods (v1.4.8 deletes its runtime
     shader cache then; reordering alone does not); observed stalls ran 68 s (E4) to about 305 s
     (b18f3441); (4) for a report, lower MCM "Stall Threshold (seconds)" to about 120 so the watchdog
     writes a bundle (its `STILL LOADING` wording does not mean wedged), and, while frozen, create a dump
     file of Bannerlord.exe in Task Manager (Details tab, right-click; the file is about as large as the
     game's memory use, so keep it and offer it); (5) a hero on a TAOM creature mount: swapping it for a
     horse is a cheap test backed by one report (#612), not a fix to promise; (6) battle size, corpse and
     ragdoll settings cannot help before `BattlePlayable` (no agent exists yet; the preload covers every
     troop of every involved party); (7) optional warm-up: the main-menu "Pre-compile Shaders" walk
     prepays character shaders (20 to 70 minutes cold).
   - Changelog line (newest first): `- 2026-11-04 (1.4.x port, WP113): triage end state from 1.5.x (#1025): a silent render-wait stream is a frozen count, not a drain; session-rate heuristic; render-wait guidance for players.`
2. `docs/features/shader-precompilation.md`:
   - Under the STATUS banner (`:3`) add a quote block: `**1.4.x decision D4 (2026-11-04): stays ACTIVE.**`
     The 1.5.x line parked the walk on 2026-09-25 (`25995dc91`) as "more problems than it is worth for
     now"; its RCA records no runtime or compatibility defect, and that park never reached 1.4.x. On
     1.4.x the walk is the only player-side shader warm-up (the 1.5.x ShaderCacheKeeper is not ported,
     D11). The 2026-11-01 Harad logs contain no walk line; their render waits sat on vanilla scenes that
     ship their own sacks (`battle_terrain_031`, `arena_aserai_a`, `arena_empire_a`, per the loading-hang
     lens), so the pending compiles were character and equipment shaders, which the default character
     pass prepays; scene passes stay off by default (`EnableShaderPrecompileScenePasses`). Kept open: on
     the Order of Battle fallback only (never taken by the one-entry player party),
     `ShaderPrecompilePlayerAgentGuard.OnMissionTick` calls `FinishDeployment` inside the mission's
     `OnMissionTick` loop.
   - `:19`: replace "players re-run it after adding, removing or reordering mods." with "players re-run it after adding or removing mods or after a game update. Reordering alone does not clear the cache: v1.4.8 sorts the module ids before it compares them."
   - `:136`: replace "enabling NavalDLC, toggling any mod, or a launcher reordering the module list discards the work." with "enabling NavalDLC or toggling any mod discards the work (a reorder does not: the engine sorts the module ids before comparing them)."
   - Changelog line: `- 2026-11-04 (1.4.x, WP113): kept ACTIVE under decision D4; the reorder claim corrected (the engine sorts the module list before comparing it).`
3. `.github/ISSUE_TEMPLATE/battle-load-hang.md` (use `MSYS_NO_PATHCONV=1` for any `git show` of it):
   in "Files to attach", after item 3 add an item for `rgl_log_<number>.txt`, the FULL engine log from
   the same folder ("it shows whether the engine was still compiling shaders: `compile_shader:` lines"),
   and an item: "If the game is stuck right now: before closing it, create a dump file of Bannerlord.exe
   in Task Manager (Details tab, right-click). It is large (about the game's memory use), so do not
   attach it; say you have it and we will ask for it." Renumber the save-file item.
4. The `polls=1` reading is not the #352 wait (hand-over from WP108). On v1.4.8
   `PreloadHelper.WaitForMeshesToBeLoaded` is called from each preload view's `OnSceneRenderingStarted`
   (`MissionPreloadView`, `ArenaPreloadView` and the like), after loading has finished, once `MissionScreen`
   has dropped the loading window. `TickLoading` only calls `LoadMission` or `FinishMissionLoading`
   (taom-src `MissionState.cs:221-233`). So `polls=1` with a large `waitMs` on `FinishMissionLoadingBegin`
   is a block inside one frame, written before that wait ever runs, and #352 cannot be its name. Plan 108's
   "Current state" and Maintenance notes hold the source. Do not name Patch90 in this wording (this WP does
   not depend on WP108). Re-read each location first (A1 and A2 moved the tool's lines; earlier WPs may
   have moved the rest), and STOP if a quoted text is gone:
   - `Main/Features/BattleLoadDiagnostics/Hooks/MissionState_TickLoading_BattleLoad_Patch.cs` (`:13-16` at
     `1b00d1acd`), comment only: replace the four lines of the `polls=1, waitMs large` entry with
     ```
     //   polls=1, waitMs large   -> the block was INSIDE a single frame: the main thread stalled, it
     //                              was not waiting on workers. This is NOT the #352
     //                              PreloadHelper.WaitForMeshesToBeLoaded wait: on v1.4.8 that wait runs
     //                              from OnSceneRenderingStarted, after loading has finished.
     ```
   - `docs/features/battle-load-diagnostics.md` (`:94` at `1b00d1acd`), the table row whose first cell is
     `polls=1`, `waitMs` large: its second cell becomes
     "the block was **inside one frame**: a stalled main thread, not async waiting. It is not the #352 `WaitForMeshesToBeLoaded` wait, which on v1.4.8 runs from `OnSceneRenderingStarted` after loading has finished".
   - `docs/reference/harmony-patch-registry.md` (`:316` at `1b00d1acd`), the paragraph starting
     "The third, `MissionState.TickLoading`": replace
     "**blocking native spin inside one frame** (the #352 `WaitForMeshesToBeLoaded` shape)" with
     "**block inside one frame** (not the #352 `WaitForMeshesToBeLoaded` wait, which on v1.4.8 runs from `OnSceneRenderingStarted` after loading has finished)".
     The whole paragraph is one physical line and holds two em dashes, so this edit puts them in the diff:
     make the one after `never logs**` a colon and the one before `opposite diagnoses` a colon.
   - `docs/features/battle-load-diagnostics.md` (`:125` at `1b00d1acd`): replace "the thread blocked INSIDE frame 1, the #352 shape" with "the thread blocked INSIDE frame 1, not the #352 wait".
   - `tools/triage_battle_load.py`, `_format_timings` (about `:1299-1310` at `1581d4d0a`; A2 moves it): in
     the comment, replace the two lines "Printing the first reading for the second case sends the reader to #352 for a"
     and "stall that is nowhere near WaitForMeshesToBeLoaded." with one line, `# Printing the first reading for the second case names the wrong place.`;
     replace the `polls=1 with a large wait` note, keeping its `out.append` and indent, with the text
     `"  polls=1 with a large wait: the main thread BLOCKED inside one frame, not async streaming. "`
     `"This is not the #352 wait: v1.4.8's WaitForMeshesToBeLoaded runs after loading, once "`
     `"rendering starts."`. Keep `BLOCKED inside one frame` whole: `test_completed_wait_polls_one_still_reports_the_352_native_spin`
     asserts it. Leave that test, its name and the heartbeat branch's `This is NOT the #352 shape` as they are.

**Verify**: dash check prints `0` for the six files; `python tools/lint_docs.py` shows no new finding
in the four markdown files; `python tools/tests/test_triage_battle_load.py` prints `Ran 191 tests` and `OK`;
`dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` exits 0;
`git grep -n "WaitForMeshesToBeLoaded shape" -- tools/triage_battle_load.py Main/Features/BattleLoadDiagnostics/Hooks/MissionState_TickLoading_BattleLoad_Patch.cs` and
`git grep -n "polls=1.*WaitForMeshesToBeLoaded.\? shape" -- docs/features/battle-load-diagnostics.md docs/reference/harmony-patch-registry.md` each print nothing.
Stage exactly `docs/features/battle-load-diagnostics.md`, `docs/features/shader-precompilation.md`,
`.github/ISSUE_TEMPLATE/battle-load-hang.md`, `docs/reference/harmony-patch-registry.md`,
`tools/triage_battle_load.py` and `Main/Features/BattleLoadDiagnostics/Hooks/MissionState_TickLoading_BattleLoad_Patch.cs`;
`git diff --cached --stat` lists only these six files.

Commit A3 subject: `docs(battle-load): v2.0.28 - read a render-wait stall honestly`
Body: what changed in the three docs, D4 kept ACTIVE with its evidence, the reorder correction, and that
a `polls=1` load stall is no longer attributed to the #352 wait.
Trailers: `Research: D3 disassembly (sorted module list), V02 guidance, 25995dc91 RCA; v1.4.8 MissionState.cs:221-233, WaitForMeshesToBeLoaded callers per plan 108`.

### Part A done criteria

- [ ] `python tools/tests/test_triage_battle_load.py` reports 191 tests, `OK`.
- [ ] `git grep -n "v2.0.35" -- tools/triage_battle_load.py` prints nothing.
- [ ] `git grep -n "_render_silence\|RENDER_WAIT_SILENCE_THRESHOLD_S\|classify_post_load_freeze\|session_render_rate" -- tools/triage_battle_load.py` prints matches for all four.
- [ ] Three commits on the branch: `git log --oneline bannerlord-1.4.5..HEAD` prints 3 lines, and `git diff --name-only bannerlord-1.4.5...HEAD` (three dots: from the merge base) lists only Part A's seven files (the six of A3 plus `tools/tests/test_triage_battle_load.py`).

---

## Part B: shader compile notice port (82645b53f) and a shader-cache state line

### Step B1: port 82645b53f (startup notice), adapted to 1.4.x

`git cherry-pick -n 82645b53f`. Simulated conflicts: `Main/SubModule.cs`, `Main/TAOM.csproj`,
`Main/_Module/ModuleData/taom_module_strings.xml`, `docs/features/shader-cache-keeper.md`,
`docs/reference/feature-map.md`, `docs/reference/provenance-register.md`. Clean: the five
`Main/Features/ShaderCompileNotice/*.cs` files, `TAOM.Tests/Features/ShaderCompileNotice/ShaderCompileProgressTests.cs`,
`docs/features/shader-compile-notice.md`. Resolve:

| Path | Resolution |
|---|---|
| `docs/features/shader-cache-keeper.md` | Absent on 1.4.x (D11): `git rm docs/features/shader-cache-keeper.md` |
| `docs/reference/feature-map.md` | Bookkeeping: `git checkout --ours` it, `git add` it |
| `Main/TAOM.csproj` | Keep ours. Insert after `<Reference Include="System.IO.Compression.FileSystem" />`: the 1.5.x comment ("ShaderCompileNotice draws a plain Windows window over the game while the engine's main thread is blocked compiling shaders ... The game bin ships neither assembly; the .NET Framework 4.7.2 copies load.") and `<Reference Include="System.Windows.Forms" />`, `<Reference Include="System.Drawing" />`. Keep 1.4.x's `System.Numerics.Vectors.dll` reference unchanged (1.5.x's `$(TaomSystemNumericsVectorsDll)` line is not ours) |
| `Main/SubModule.cs` | Keep ours. 1.4.x has no `EarlyLogBridge`: insert the Start block (the 1.5.x `try { Features.ShaderCompileNotice.ShaderCompileNoticeEntry.Start(IoC.Resolve<IModLogger>(), IoC.Resolve<TAOM.Features.CoopInterop.IDedicatedServerProvider>().IsDedicatedServer); } catch { }` with its comment) immediately after `IoC.Configure();` and before the `// Issue #371` build-stamp block. Insert `Features.ShaderCompileNotice.ShaderCompileNoticeEntry.Stop();` immediately after `base.OnBeforeInitialModuleScreenSetAsRoot();`, with `// Only sets a volatile flag, so it cannot throw.` directly above it |
| `taom_module_strings.xml` | Keep ours; add the 1.5.x three rows (`taom_shader_notice_title_total`, `taom_shader_notice_title_count`, `taom_shader_notice_detail`, with the `<!-- harvested by tools/harvest_literal_loc_keys.py -->` and `<!-- ShaderCompileNotice -->` comments) after the SpecialResources block, before `</strings>` |
| `docs/reference/provenance-register.md` | Keep ours; copy the 1.5.x Sources-table row for "Yotthani VanillaTuning (shader-compile-notice)" (behavioural-port, covers `Main/Features/ShaderCompileNotice/**`, uncleared; text from `git show 82645b53f -- docs/reference/provenance-register.md`) to just after the "Yotthani modules (FieldCamp, Refuge, SupplyLines)" row, and its `### Yotthani VanillaTuning (shader-compile-notice)` detail section to just after the `### Yotthani commissioned modules (FieldCamp, Refuge, SupplyLines)` section. Drop every ShaderCacheKeeper line of the hunk context |

Then adapt (in the cherry-picked files): `ShaderCompileNoticeEntry.cs` comment "Module.cs v1.5.4 lines
277 and 282" becomes "Module.cs v1.4.8 :262 (LoadLocalizationXmls) and :267 (LoadSubModules)";
`ShaderCompileNotice.cs` summary "about 1,340 shaders" gets "(measured on v1.5.4; not measured on
v1.4.8)". In `docs/features/shader-compile-notice.md`: replace the ShaderCacheKeeper paragraph with
"ShaderCacheKeeper (the 1.5.x proxy that keeps the cache across shader-less mod-list changes) is not
ported to the 1.4.x line (decision D11), so on 1.4.8 every change of the active module set pays this
rebuild."; Timing bullet: started "right after `IoC.Configure()`" and "the timing below was measured on
v1.5.4"; Texts bullet: cite v1.4.8 `Module.cs:262` and `:267`; Dependencies: add "the installed v1.4.8
native logs the same `compile_shader:` line (D3 verification)"; GitHub Issue: "Port of #752 (1.5.x)";
Changelog: add `- 2026-11-04: ported to the 1.4.x line (WP113), wired after IoC.Configure (no EarlyLogBridge on 1.4.x).`

Language rows: run the "Language rows helper" (Commands section) with
`ROWS = [("taom_shader_notice_title_total", "Compiling shaders: {COUNT} of about {TOTAL}"), ("taom_shader_notice_title_count", "Compiling shaders: {COUNT}"), ("taom_shader_notice_detail", "The game is rebuilding its shaders after a change to your mod list or to the game. It is not frozen.")]`
(the three English texts without their `{=key}` prefix); expect `ok 12` and `3 0` per language file.

`git add` exactly: the five `Main/Features/ShaderCompileNotice/*.cs`, the test file, `Main/SubModule.cs`,
`Main/TAOM.csproj`, `Main/_Module/ModuleData/taom_module_strings.xml`, the 12
`Main/_Module/ModuleData/Languages/*/std_taom_module_strings_*.xml`, `docs/features/shader-compile-notice.md`,
`docs/reference/provenance-register.md`, plus the resolved `feature-map.md` and the removal.
Check `git status`: no other staged path; `docs/reference/feature-map.md` shows no diff against HEAD.

**Verify**: build exit 0; filtered `FullyQualifiedName~ShaderCompileProgressTests` 16 passed; filtered
`"FullyQualifiedName~LanguageFileCoverageTests|FullyQualifiedName~LanguageDataXmlTests|FullyQualifiedName~UnregisteredLocalizationKeyBaselineTests"` 0 failed;
`git grep -n "EarlyLogBridge\|ShaderCacheKeeper\|TAOM.Client" -- Main/Features/ShaderCompileNotice Main/SubModule.cs` prints nothing.

Commit B1 subject: `feat(shaders): v2.0.28 - show shader compile progress during rebuilds`
Body: rewrite 82645b53f's body for 1.4.x (no ShaderCacheKeeper; English texts, translation by WP135).
Trailers: `Research: v1.4.8 Module.cs:262/:267 order; native compile_shader format (D3)`;
`Not-tested: the window, thread and priority in game (#752 check owed)`;
`Ported-from: 82645b53fbae055e139fc7a8b96eda5ab3e020e0`.

### Step B2: log the runtime shader cache state at startup (new, TDD)

Why: D3 found that v1.4.8 rewrites `shader_mapping.bin` at startup when it drops its runtime cache, so
one line at `OnSubModuleLoad` answers "was this session cold from the start?" in every future log.

RED: `TAOM.Tests/Features/ShaderCompileNotice/ShaderCacheStateTests.cs` (MSTest), pinned literals:
1. `Describe_FolderMissing_SaysNotFound` -> `[ShaderCache] CoreShaders/D3D11 folder not found`
2. `Describe_NoMapping_SaysAbsent` -> `[ShaderCache] CoreShaders/D3D11 rewrittenThisStart=unknown mapping=absent sacx=0`
3. `Describe_MappingWrittenAfterStart_SaysRewritten` (bytes 13, written 2026-11-04 10:00:03Z, start 10:00:00Z, 0 sacx) -> `[ShaderCache] CoreShaders/D3D11 rewrittenThisStart=true mappingBytes=13 sacx=0 mappingWrittenUtc=2026-11-04T10:00:03Z processStartUtc=2026-11-04T10:00:00Z`
4. `Describe_MappingWrittenBeforeStart_SaysKept` (38461 bytes, 2026-08-14T13:44:00Z, 363 sacx, start 2026-11-04T10:00:00Z) -> `[ShaderCache] CoreShaders/D3D11 rewrittenThisStart=false mappingBytes=38461 sacx=363 mappingWrittenUtc=2026-08-14T13:44:00Z processStartUtc=2026-11-04T10:00:00Z`

GREEN: `Main/Features/ShaderCompileNotice/ShaderCacheState.cs`:
`public static class ShaderCacheState` with
`public static string Describe(bool folderExists, long? mappingBytes, DateTime? mappingWrittenUtc, int sacxCount, DateTime processStartUtc)`
(UTC stamps formatted `yyyy-MM-ddTHH:mm:ssZ`, invariant culture) and the boundary
`public static void Log(IModLogger logger)`: folder = `Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Mount and Blade II Bannerlord", "Shaders", "CoreShaders", "D3D11")`;
stat `shader_mapping.bin`; count `*.sacx`; start = `Process.GetCurrentProcess().StartTime.ToUniversalTime()`;
`logger.LogInfo(Describe(...))`; any exception -> `logger.LogWarning("[ShaderCache] state not read: " + type + ": " + message)`.
`Main/SubModule.cs`: right after the B1 notice-start block, `try { Features.ShaderCompileNotice.ShaderCacheState.Log(IoC.Resolve<IModLogger>()); } catch { }`
with a two-line comment (D3: the engine rewrites the mapping when it drops its cache at startup).

**Verify**: build exit 0; filtered `FullyQualifiedName~ShaderCompileNotice` 20 passed; full suite 0 failed.

Commit B2 subject: `feat(shaders): v2.0.28 - log whether the shader cache was dropped`
Trailers: `Research: D3 disassembly of v1.4.8 RVA 0x1f3ba0 (delete, then header rewrite)`;
`Not-tested: rewrittenThisStart=true after a mod-set change (in game)`.

### Part B done criteria

- [ ] Full suite: 0 failed; total = A0 baseline + 20.
- [ ] `git grep -n "System.Windows.Forms" -- Main/TAOM.csproj` prints one match.
- [ ] `git grep -n "ShaderCompileNoticeEntry.Start\|ShaderCompileNoticeEntry.Stop\|ShaderCacheState.Log" -- Main/SubModule.cs` prints three matches.

---

## Part C: a main-thread heartbeat and the `[Freeze]` sampler

Design (decided here; do not redesign):
- **Heartbeat**: `SubModule.OnApplicationTick` stamps a UTC tick count every managed frame (light or full tick).
- **Watchdog**: a 1 s `System.Threading.Timer`. A poll that sees a NEW beat ends any episode (writes one
  "recovered" line if the episode logged anything), remembers the engine log's length and the gen0 GC
  count, and returns. Otherwise silence = now minus the last beat; at 10, 20, 40 and 80 s of silence it
  evaluates one sample (each threshold once per episode).
- **Stand-down**: precompile walk active (`BattleLoadStallWatchdog.SuppressStallDetection`) or exit sampler
  armed (`service.ExitWindowOpenedUtcTicks != 0`): log nothing. Mission frame in flight
  (`MissionTickStallProbe.MissionFrame`): log the line WITHOUT a stack (`[MissionStall]` owns that thread).
- **Heap gate** (no suspension while managed code is busy, e.g. a save or a load): heap moving means at
  least 3 gen0 collections AND at least 0.3 per second over the window since the previous threshold.
  Calibration from E4: 2 gen0 collections across the 69 s blocked wait (lines 2125 and 2131, 0.03/s);
  7,098 across 1,625 s of normal play (lines 2124 and 5137, 4.4/s). When moving: one stack-less line at the
  40 s threshold only, nothing at 10 and 20.
- **Evidence in every line**: context, the last published shader count, the engine log's `compile_shader`
  count since the last frame, memory tokens, the last BattleLoad phase. The engine log is tailed from
  the timer thread (a file read); the native counter is never called.

### Step C1: RED tests

Create these test classes (MSTest, NSubstitute; namespace `TAOM.Tests.Features.BattleLoadDiagnostics`):
1. `MainThreadHeartbeatTests`: `TryRead_BeforeAnyBeat_ReturnsFalse`; `BeatAt_RecordsTheTicks`; `BindCurrentThread_RecordsThisThread`.
2. `FreezeSamplePolicyTests`: `CrossesThreshold_FiresOncePerThresholdInOrder` (10, 20, 40, 80; nothing at 9.9 or after 4 samples);
   `Decide_WalkOrExitSampler_IsNone`; `Decide_MissionFrame_LogsWithoutStack`; `Decide_HeapMovingBeforeForty_IsNone`;
   `Decide_HeapMovingAtFortyOnce_LogsWithoutStackThenNone`; `HeapMoving_CalibrationCells` ((2, 69) false, (3, 10) true, (8, 40) false, (12, 40) true, (44, 10) true, (5, 0) false).
3. `FreezeLogTests` (pinned literals, copied verbatim into the Python twin in C4):
   - `FormatSample_AllTokens_MatchesPinnedLiteral`: `FormatSample(20, 2, "battle-load", 2697, true, 31, 2, "gc=3167/785/81 heapMB=466", "phase=WaitingForRender seq=40 waitedMs=2259 shaders=2697")` ==
     `[Freeze] main thread silent 20s sample#2 context=battle-load shaders=2697 rglCompiles=31 lastCompileAgoS=2 gc=3167/785/81 heapMB=466 last phase=WaitingForRender seq=40 waitedMs=2259 shaders=2697`
   - `FormatSample_Unmeasured_OmitsTokens`: shaders `-1` omits `shaders=`; `rglAvailable=false` writes `rgl=unavailable` and no `rglCompiles`/`lastCompileAgoS`; `lastCompileAgoSeconds=null` omits `lastCompileAgoS`.
   - `FormatRecovered_MatchesPinnedLiterals`: `FormatRecovered(68, true, 1569)` == `[Freeze] main thread recovered after about 68s rglCompiles=1569`; `FormatRecovered(45, false, 0)` == `[Freeze] main thread recovered after about 45s rgl=unavailable`.
   - `FormatStarted_NamesTheLogFileOrItsAbsence`: path `...\rgl_log_1744.txt` -> `[Freeze] main-thread freeze sampler started; engine log rgl_log_1744.txt`; null -> `[Freeze] main-thread freeze sampler started; engine log not found, compile counts unavailable`.
4. `RglLogLocatorTests`: `PidExactName_IsChosen`; `ErrorsLog_IsNeverChosen`; `NoPidMatch_FallsBackToNewestWrittenSinceProcessStart`; `NothingSuitable_ReturnsNull`.
5. `RglCompileTailTests` (temp files): `LinesAppendedAfterMarkFresh_AreCounted`; `LinesBeforeMarkFresh_AreNotCounted`; `MarkerSplitAcrossTwoReads_CountedOnce`; `MissingFile_IsUnavailable`; `SecondsSinceLastCompile_NullUntilACompileThenMeasured`; `OneReadIsCappedAtFourMegabytes`.
6. `MainThreadFreezeWatchdogTests` (drive `Poll(long nowUtcTicks)`; fakes: heartbeat instance, mission-frame probe instance, `IRglCompileTail` substitute, `Func<int>` gen0, `Func<bool>` walk, `DescribeStack` seam):
   `NoBeatYet_LogsNothing`; `FreshBeats_LogNothing`; `SilentNineSeconds_LogsNothing`;
   `SilentMainThread_SamplesWithStacksAtTenTwentyFortyEighty_ThenStops` (4 warnings, `sample#1`..`sample#4`, the fake frame text);
   `NewBeatAfterALoggedEpisode_LogsRecoveredOnce`; `NewBeatAfterASilentButUnloggedEpisode_LogsNoRecovered`;
   `ExitSamplerArmed_LogsNothing`; `PrecompileWalk_LogsNothing`; `MissionFrameInFlight_LogsWithoutStack`;
   `HeapMoving_LogsOneStacklessLineAtForty`; `SettingOff_LogsNothing`; `CaptureThrows_LineSaysCaptureFailed`;
   `EmptyStack_LineSaysNoManagedFrames`; `RglCompilesSinceLastFrame_AppearInTheLine`.
7. `BattleLoadDiagnosticsSettingsProviderTests`: add `MainThreadFreezeSamplerEnabled_NoMcmInstance_DefaultsTrue`.

**Verify**: build FAILS to compile on the missing types (RED). Do not commit.

### Step C2: GREEN implementation

New files in `Main/Features/BattleLoadDiagnostics/` (namespace `TAOM.Features.BattleLoadDiagnostics`):

`MainThreadHeartbeat.cs`:
```csharp
public sealed class MainThreadHeartbeat
{
    public static readonly MainThreadHeartbeat Instance = new MainThreadHeartbeat();
    private long _lastBeatUtcTicks;   // 0 = never
    private Thread? _thread;
    public void Beat() => BeatAt(DateTime.UtcNow.Ticks);
    internal void BeatAt(long utcTicks) => Volatile.Write(ref _lastBeatUtcTicks, utcTicks);
    public void BindCurrentThread() => Volatile.Write(ref _thread, Thread.CurrentThread);
    public bool TryRead(out long lastBeatUtcTicks, out Thread? thread)
    {
        lastBeatUtcTicks = Volatile.Read(ref _lastBeatUtcTicks);
        thread = Volatile.Read(ref _thread);
        return lastBeatUtcTicks != 0L;
    }
}
```

`FreezeSamplePolicy.cs`:
```csharp
internal enum FreezeStandDown { None, PrecompileWalk, ExitSampler, MissionFrame }
internal enum FreezeSampleAction { None, LogWithoutStack, LogWithStack }
internal static class FreezeSamplePolicy
{
    internal static readonly double[] ThresholdsSeconds = { 10d, 20d, 40d, 80d };
    public const int HeapMovingMinCollections = 3;
    public const double HeapMovingMinPerSecond = 0.3;
    public static bool CrossesThreshold(double silentSeconds, int samplesTaken)
        => samplesTaken < ThresholdsSeconds.Length && silentSeconds >= ThresholdsSeconds[samplesTaken];
    public static bool HeapMoving(int gen0Delta, double windowSeconds)
        => windowSeconds > 0 && gen0Delta >= HeapMovingMinCollections && gen0Delta >= HeapMovingMinPerSecond * windowSeconds;
    public static FreezeSampleAction Decide(int thresholdIndex, FreezeStandDown standDown, bool heapMoving, bool heapLineLogged)
    {
        if (standDown == FreezeStandDown.PrecompileWalk || standDown == FreezeStandDown.ExitSampler) return FreezeSampleAction.None;
        if (standDown == FreezeStandDown.MissionFrame) return FreezeSampleAction.LogWithoutStack;
        if (heapMoving) return thresholdIndex >= 2 && !heapLineLogged ? FreezeSampleAction.LogWithoutStack : FreezeSampleAction.None;
        return FreezeSampleAction.LogWithStack;
    }
}
```

`FreezeLog.cs`: `internal static class FreezeLog` with `Tag = "[Freeze]"`, `FormatSample(int silentSeconds, int sampleNumber, string context, int shadersInFlight, bool rglAvailable, int rglCompiles, int? lastCompileAgoSeconds, string memTokens, string statusLine)`,
`FormatRecovered(int silentSeconds, bool rglAvailable, int rglCompiles)`, `FormatStarted(string? rglPath)` (file name only),
and second-line constants (each starts with four spaces):
`NotSampledMissionFrame = "    <stack not sampled: a mission frame is in flight and [MissionStall] samples this thread>"`,
`NotSampledHeapMoving(int gen0Delta, int windowSeconds)` -> `"    <stack not sampled: the managed heap is moving ({d} gen0 collections in {s}s), so the game is busy in managed code such as a save or a load>"`,
`NoManagedFrames = "    <no managed frames: the main thread is in native code>"`,
`CaptureFailed(Exception ex)` -> `"    <capture failed: {Type}: {Message}>"`, `ThreadUnknown = "    <thread unknown>"`.
Contexts: `battle-load` (`BattleLoadLoadingWindow.IsOpen`), `mission-frame` (probe in flight), else `other`.

`RglLogLocator.cs`: `internal readonly struct RglCandidate` with a plain constructor `(string fullPath, DateTime lastWriteUtc, int rootIndex)` and get-only properties `FullPath`, `LastWriteUtc`, `RootIndex` (`Directory.Build.props` pins `LangVersion` 10.0, so no primary constructor) and
`internal static class RglLogLocator` with pure `Choose(int pid, DateTime processStartUtc, IEnumerable<RglCandidate> candidates)`
(1: the candidate named exactly `rgl_log_{pid}.txt`, lowest `rootIndex`; 2: else the newest `rgl_log_*.txt` whose
name does not start with `rgl_log_errors_` and whose `lastWriteUtc >= processStartUtc`; 3: else null) and the
boundary `ResolveForCurrentProcess()`: roots `CommonApplicationData` then `MyDocuments`, each
`+ "Mount and Blade II Bannerlord\logs"` (the order `LogTailCollector` already uses), enumerate `rgl_log_*.txt`,
`Process.GetCurrentProcess()` Id and `StartTime.ToUniversalTime()`; catch everything and return null. That the
file name carries the process id is UNVERIFIED on v1.4.8 (hence the fallback).

`IRglCompileTail.cs` / `RglCompileTail.cs` (the interface is `public`, not `internal`: the watchdog tests
substitute it with NSubstitute 5.1.0, whose Castle DynamicProxy cannot implement an internal interface
because `Main/TAOM.csproj:117-125` grants `InternalsVisibleTo` only to `TAOM.Tests` and `TAOM_Online`,
not `DynamicProxyGenAssembly2`; `RglCompileTail` itself may stay internal):
```csharp
public interface IRglCompileTail
{
    bool Available { get; }
    string? Path { get; }
    void MarkFresh();                       // remember the current end of the file; reset the count
    void ReadNew(long nowUtcTicks);         // read appended bytes (at most 4 MB per call), count compile_shader
    int CompilesSinceFresh { get; }
    double? SecondsSinceLastCompile(long nowUtcTicks);
}
```
`RglCompileTail(Func<string?> resolvePath)`: resolve lazily and retry at most every 30 s while null; open with
`FileShare.ReadWrite | FileShare.Delete`; if the length shrank, restart at 0; count with a fresh
`ShaderCompileNotice.ShaderCompileProgress` per `MarkFresh` (its `Feed` handles a marker split across reads);
stamp the poll time whenever the count rises; any IO error sets `Available = false` until the next success.

`MainThreadFreezeWatchdog.cs` (public sealed, `IDisposable`):
- Public ctor `(IModLogger logger, IBattleLoadDiagnosticsSettingsProvider settings, IBattleLoadDiagnosticsService service)`
  chains to an internal ctor that also takes `MainThreadHeartbeat heartbeat, MissionTickStallProbe missionFrame, IRglCompileTail tail, Func<int> gen0Collections, Func<bool> walkActive`
  (defaults: `MainThreadHeartbeat.Instance`, `MissionTickStallProbe.MissionFrame`, `new RglCompileTail(RglLogLocator.ResolveForCurrentProcess)`, `() => GC.CollectionCount(0)`, `() => BattleLoadStallWatchdog.SuppressStallDetection`).
- `internal Func<Thread, string> DescribeStack { get; set; }` default: capture with `ThreadStackCapture.Capture`; `FrameCount == 0` -> `FreezeLog.NoManagedFrames`, else `ThreadStackCapture.FormatFrames`.
- `Start()`: idempotent; `_heartbeat.BindCurrentThread()` (Start is called on the main thread), log `FreezeLog.FormatStarted(_tail.Path)` once (make the `Path` getter attempt the lazy resolve, so the start line names the file when it already exists), start the 1 s timer whose callback wraps `Poll(DateTime.UtcNow.Ticks)` in the `_pollActive` Interlocked guard and a catch that logs `[Freeze] poll failed: ...` (sibling pattern).
- `internal void Poll(long now)`:
```csharp
if (!_settings.MainThreadFreezeSamplerEnabled) { _episode = null; return; }
if (!_heartbeat.TryRead(out long beat, out Thread? main)) return;
if (beat != _lastSeenBeat)
{
    _lastSeenBeat = beat;
    if (_episode != null && _episode.LinesLogged > 0)
        _logger.LogInfo(FreezeLog.FormatRecovered(Secs(beat - _episode.Since), _tail.Available, _tail.CompilesSinceFresh));
    _episode = null;
    _tail.MarkFresh(); _gcRef = _gen0(); _gcRefTicks = now;
    return;
}
double silent = SecsD(now - beat);
_episode ??= new Episode(beat);
_tail.ReadNew(now);
if (!FreezeSamplePolicy.CrossesThreshold(silent, _episode.SamplesTaken)) return;
int index = _episode.SamplesTaken++;
var standDown = _walkActive() ? FreezeStandDown.PrecompileWalk
    : _service.ExitWindowOpenedUtcTicks != 0L ? FreezeStandDown.ExitSampler
    : _missionFrame.TryRead(out _, out _) ? FreezeStandDown.MissionFrame
    : FreezeStandDown.None;
int gcDelta = _gen0() - _gcRef; double window = SecsD(now - _gcRefTicks);
bool heapMoving = FreezeSamplePolicy.HeapMoving(gcDelta, window);
var action = FreezeSamplePolicy.Decide(index, standDown, heapMoving, _episode.HeapLineLogged);
_gcRef = _gen0(); _gcRefTicks = now;
if (action == FreezeSampleAction.None) return;
string second = standDown == FreezeStandDown.MissionFrame ? FreezeLog.NotSampledMissionFrame
    : action == FreezeSampleAction.LogWithoutStack ? FreezeLog.NotSampledHeapMoving(gcDelta, (int)window)
    : Describe(main);   // ThreadUnknown when null; CaptureFailed(ex) when DescribeStack throws
if (action == FreezeSampleAction.LogWithoutStack && standDown == FreezeStandDown.None) _episode.HeapLineLogged = true;
_episode.LinesLogged++;
_logger.LogWarning(FreezeLog.FormatSample((int)silent, index + 1, Context(), ShadersIfWindowOpen(),
    _tail.Available, _tail.CompilesSinceFresh, LastCompileAgo(now), ProcessMemoryTokens.Format(),
    _service.CurrentStatusLine) + "\n" + second);
```
  (`ShadersIfWindowOpen()` returns `BattleLoadRenderWaitProbe.ShadersInFlight` when `BattleLoadLoadingWindow.IsOpen`, else -1.)
- `Dispose()`: dispose the timer.

Settings and wiring:
- `BattleLoadDiagnosticsSettings.cs`: in group `"Exit Stall Sampler"`, next `Order` after `EnableMissionTickStallSampler`:
  `[SettingPropertyBool("Enable Main Thread Freeze Sampler", Order = <n>, RequireRestart = false, HintText = "If the game finishes no frame for 10 seconds anywhere (map, menus, loading screens), writes a [Freeze] line to the TAOM debug log at +10/+20/+40/+80 s with the number of shaders the engine compiled meanwhile, and photographs the main thread's call stack when that is safe. It never runs while the game is responsive, stands aside during a mission exit and the shader pre-compilation walk, and skips the stack while the game is busy saving or loading. Default ON.")] public bool EnableMainThreadFreezeSampler { get; set; } = true;`
- `IBattleLoadDiagnosticsSettingsProvider`: `bool MainThreadFreezeSamplerEnabled { get; }` (doc comment); provider: `BattleLoadDiagnosticsSettings.Instance?.EnableMainThreadFreezeSampler ?? true`.
- `CoopSettingsRelevance.cs`: add `"EnableMainThreadFreezeSampler"` to the Instrumentation set next to `"EnableMissionTickStallSampler"`.
- `SettingsFingerprintTests.cs`: `BattleLoadDiagnosticsSettings` `reflected:` = the live value + 1.
- `docs/features/coop-interop.md` (`What is NOT done` paragraph) and `docs/features/bannerlord-together-compat.md` (the "Settings parity" row): the quoted total + 1, the BattleLoadDiagnostics split + 1; if the paragraph states the excluded count, set it to total minus simulation-relevant.
- `BattleLoadDiagnosticsIoC.cs`: `container.Register<MainThreadFreezeWatchdog>(Reuse.Singleton);` (no Resolve inside the registration body).
- `Main/SubModule.cs`: first statement of `OnApplicationTick(float dt)`: `Features.BattleLoadDiagnostics.MainThreadHeartbeat.Instance.Beat();` (comment: once per managed frame; Module.OnApplicationTick reaches it from both native tick entries). In `OnBeforeInitialModuleScreenSetAsRoot`, right after the `MemoryStationSampler` start line:
  `try { IoC.Resolve<Features.BattleLoadDiagnostics.MainThreadFreezeWatchdog>().Start(); } catch { /* never block the main menu over a diagnostic */ }` with a comment (main thread; idempotent because this hook fires on every return to the main menu).

**Verify**: build exit 0; filtered `FullyQualifiedName~BattleLoadDiagnostics` 0 failed (the new classes all
run); filtered `FullyQualifiedName~SettingsFingerprintTests` and `FullyQualifiedName~SettingRequireRestartPostureTests` 0 failed;
`git grep -c "GetNumberOfShaderCompilationsInProgress" -- Main/` prints exactly the five `file:1` lines
you recorded at A0 (no new file, no higher count: the new code neither calls nor names the native counter);
`git grep -n "\.Suspend()" -- Main/` prints no file created by this plan.

### Step C3: feature doc, then commit C

`docs/features/battle-load-diagnostics.md`: after the WP106 section "The battle-freeze sampler", add
`### The main-thread freeze sampler (MainThreadFreezeWatchdog, [Freeze])`: heartbeat source and the two
native tick entries; thresholds; the stand-down table; the heap gate with the E4 calibration; the
evidence tokens with the pinned sample and recovered lines; what an empty stack and `<capture failed>`
mean (native code); that `rglCompiles` is read from the engine log by a file tail and may lag if the
engine buffers its log (UNVERIFIED); the `[ShaderCache]` line from Part B, linked to
`shader-compile-notice.md` (until something links it, `lint_docs.py` reports that new doc as an orphan
feature doc); the one known blind spot: the beat runs after `GlobalGameStateManager.OnTick` /
`GameManagerBase.Current.OnTick` in `Module.OnApplicationTick` (v1.4.8 `Module.cs:512`, `:519`, `:523`)
and Patch37's finalizer on `Module.OnApplicationTick` swallows a throw from those ticks, so a frame
whose game tick throws skips the beat and a throw repeated every frame reads as silence while frames
still run (the `[CrashReport]` capture lines in the same log tell the two apart). Add the setting row to
"Configuration", the new files to "Key Files", the new test classes to "Tests", a Changelog line.
Dash check and lint on the doc.

Stage the new `Main/Features/BattleLoadDiagnostics/*.cs`, the edited settings/provider/interface/IoC files,
`CoopSettingsRelevance.cs`, `Main/SubModule.cs`, the new and edited test files, `SettingsFingerprintTests.cs`,
the three docs.

Commit C subject: `feat(diagnostics): v2.0.28 - main-thread heartbeat and freeze sampler`
Body: what a player's log now shows when the game stops finishing frames, the stand-downs, the off switch.
Trailers: `Research: v1.4.8 Managed.ApplicationTick/ApplicationTickLight -> CoreManaged -> Module.OnApplicationTick -> submodules; f75288ebf ThreadStackCapture`;
`Not-tested: Thread.Suspend on a live main thread; engine log flush latency; ticks while minimized`; `Refs #<new issue>`.

### Step C4: triage reads the `[Freeze]` and `[ShaderCache]` evidence (Python, TDD)

RED, in `tools/tests/test_triage_battle_load.py`: pinned twins (copy the C# literals verbatim)
`PINNED_FREEZE_SAMPLE`, `PINNED_FREEZE_RECOVERED`, `PINNED_SHADER_CACHE_REWRITTEN`, and `class FreezeEvidenceTests` with
`test_freeze_sample_literal_parses`; `test_freeze_recovered_literal_parses`;
`test_compiles_during_the_silence_read_as_a_slow_compile` (the 1.5.x `_ended_log` shape plus a `[Freeze]`
line stamped after the last sample with `rglCompiles=812`: the summary contains `812` and `still compiling`
and does not contain `points to a wedge`; never assert on the bare word `wedge`, because the existing
silent-branch summary already ends "...from a main-thread wedge."); `test_no_compiles_for_sixty_seconds_reads_as_a_wedge` (`silent 80s`,
`rglCompiles=0`: summary names a wedge and the hang dump; it says the engine log showed no compile in 80 s);
`test_rgl_unavailable_keeps_the_cannot_separate_wording`; `test_freeze_lines_from_an_earlier_load_are_ignored`;
`test_shader_cache_rewritten_this_start_adds_a_note`; `test_json_carries_freeze_and_shader_cache_blocks`
(`freeze` = `{samples, max_silent_s, rgl_compiles, recovered_after_s}` or null; `shader_cache_rewritten` = true/false/null).

GREEN, in `tools/triage_battle_load.py`: regexes for the sample line (`\[Freeze\] main thread silent (\d+)s sample#(\d+) context=(\S+)`,
then tokens taken only from the text before ` last `), the recovered line, and `rewrittenThisStart=(true|false|unknown)`;
store them on `Timeline` (new dataclass fields with empty defaults; they never become phase events);
`_freeze_evidence(tl)` keeps `[Freeze]` lines stamped at or after the last mission start and after the
last `WaitingForRender` sample; pass it to `_classify_render_wait`: in the silent branch, `rglCompiles > 0`
appends "TAOM's freeze sampler counted N compile_shader lines in the engine log during the silence: the
engine was still compiling, a slow cold compile rather than a wedge."; `rglCompiles == 0` with the largest
`silent >= 60` and not `rgl=unavailable` appends "TAOM's freeze sampler saw no compile_shader line reach
the engine log in Ns of silence: the main thread is not compiling, which points to a wedge (next step:
a hang dump, /native-crash-triage Phase 2b)."; otherwise unchanged. When `rewrittenThisStart=true`, add a
note to RENDER_WAIT verdicts: "The engine dropped or first built its runtime shader cache at this start
([ShaderCache] line), so this session's first battles compile cold." Extend the `_POST_LOAD_FREEZE_STEPS`
bullet from A1 with "and [Freeze] lines". Add the two JSON keys.

**Verify**: `python tools/tests/test_triage_battle_load.py` prints `Ran 199 tests` and `OK`; tool suite no new failure.

Commit C4 subject: `feat(tools): v2.0.28 - triage reads [Freeze] compile evidence`
Trailers: `Research: C# FreezeLog and ShaderCacheState literals (twin pins)`, `Refs #<new issue>`.

### Part C done criteria

- [ ] Full suite 0 failed; total = Part B total + the number of C# test methods you added in C1 (report it; at least 38).
- [ ] `python tools/tests/test_triage_battle_load.py`: 199 tests, `OK`.
- [ ] `git grep -n "MainThreadHeartbeat.Instance.Beat()" -- Main/SubModule.cs` one match; `git grep -n "MainThreadFreezeWatchdog>().Start()" -- Main/SubModule.cs` one match.
- [ ] `git grep -n "EnableMainThreadFreezeSampler" -- Main/Features/CoopInterop/CoopSettingsRelevance.cs` one match.

---

## Part D: notices that claim only what they can see

### Step D1: a render-wait notice driven by the engine log (new, TDD)

Rule (binding, K3 and #612): the window says "compiling" only while `compile_shader` lines are arriving
in the engine log; it never uses the main-thread shader count as progress.

RED:
- `TAOM.Tests/Features/BattleLoadDiagnostics/RenderWaitNoticeDeciderTests.cs`, one test per cell:
  not in render wait -> Hidden; walk active -> Hidden; silent 4 s with a compile 1 s ago -> Hidden;
  engine log unavailable at 120 s -> Hidden; silent 6 s, last compile 1 s ago, 40 compiles -> Compiling(40);
  silent 30 s and no compile yet -> Hidden; silent 61 s and no compile at all -> NoProgress(61);
  silent 90 s, last compile 70 s ago -> NoProgress(70); silent 90 s, last compile 30 s ago -> Hidden.
- `MainThreadFreezeWatchdogTests`: add `RenderWaitSilence_PublishesTheDecisionEveryPoll` (an
  `IRenderWaitNotice` substitute receives Compiling while the fake tail reports fresh compiles) and
  `NewBeat_PublishesHidden`.

GREEN:
- `Main/Features/BattleLoadDiagnostics/RenderWaitNotice.cs` (consumer side, no WinForms):
```csharp
public enum RenderWaitNoticeKind { Hidden, Compiling, NoProgress }
public readonly struct RenderWaitNoticeDecision
{
    public static readonly RenderWaitNoticeDecision Hidden = new RenderWaitNoticeDecision(RenderWaitNoticeKind.Hidden, 0, 0);
    public RenderWaitNoticeDecision(RenderWaitNoticeKind kind, int compiles, int seconds) { Kind = kind; Compiles = compiles; Seconds = seconds; }
    public RenderWaitNoticeKind Kind { get; } public int Compiles { get; } public int Seconds { get; }
}
public interface IRenderWaitNotice { void Show(RenderWaitNoticeDecision decision); }
public static class RenderWaitNoticeDecider
{
    public const double ShowAfterSilentSeconds = 5d, CompilingWithinSeconds = 20d, NoProgressAfterSeconds = 60d;
    public static RenderWaitNoticeDecision Decide(bool inRenderWait, bool walkActive, double silentSeconds,
        bool rglAvailable, int compiles, double? secondsSinceLastCompile)
    {
        if (!inRenderWait || walkActive || !rglAvailable || silentSeconds < ShowAfterSilentSeconds) return RenderWaitNoticeDecision.Hidden;
        if (secondsSinceLastCompile is double s && s < CompilingWithinSeconds)
            return new RenderWaitNoticeDecision(RenderWaitNoticeKind.Compiling, compiles, (int)silentSeconds);
        double quiet = secondsSinceLastCompile ?? silentSeconds;
        return silentSeconds >= NoProgressAfterSeconds && quiet >= NoProgressAfterSeconds
            ? new RenderWaitNoticeDecision(RenderWaitNoticeKind.NoProgress, compiles, (int)quiet)
            : RenderWaitNoticeDecision.Hidden;
    }
}
```
- Watchdog: add `IRenderWaitNotice notice` to both ctors (public ctor gets it from DI; tests pass a substitute).
  On a new beat call `notice.Show(Hidden)`; on every silent poll, after `_tail.ReadNew(now)`, call
  `notice.Show(RenderWaitNoticeDecider.Decide(inRenderWait, _walkActive(), silent, _tail.Available, _tail.CompilesSinceFresh, _tail.SecondsSinceLastCompile(now)))`
  with `inRenderWait = BattleLoadLoadingWindow.IsOpen && BattleLoadRenderWaitProbe.ShadersInFlight >= 0` (inject as `Func<bool>` for tests).
  Wrap `Show` in try/catch; a notice failure never stops sampling.
- `Main/Features/ShaderCompileNotice/RenderWaitNoticeHost.cs` (`public sealed class RenderWaitNoticeHost : IRenderWaitNotice`):
  ctor `(IModLogger logger, IDedicatedServerProvider server)` runs on the main thread (the watchdog is
  first resolved at the main menu); it resolves texts through a boundary `RenderWaitNoticeTexts.Resolve()`
  and calls `ShaderCompileProgress.ShouldStart(server.IsDedicatedServer, ShaderCompileNoticeEntry.ActiveModuleIds())`
  (extract that `internal static List<string> ActiveModuleIds()` from `ShaderCompileNoticeEntry.Start`, behaviour unchanged);
  when it must not start, the host is inert. `Show` stores the latest decision in a volatile field; a non-Hidden
  decision with no window running starts one background STA thread (`Name = "TAOM render-wait notice"`)
  running a WinForms `ApplicationContext` with a 300 ms timer that creates one `ShaderCompileNoticeWindow`
  sized from the widest title and detail, updates title and detail from the latest decision, closes once
  Hidden has held for 2 s, and exits the thread. It never touches the engine and never changes process priority.
- `ShaderCompileNoticeWindow.cs`: add `public string DetailText { set => _detail.Text = value; }` (the ctor's detail argument is used for sizing, so pass the widest detail).
- Texts (`RenderWaitNoticeTexts`, placeholders `%COUNT%` and `%SECONDS%` filled on the notice thread, as in `ShaderCompileNoticeTexts`), registered in `taom_module_strings.xml`:
  `taom_rw_notice_compiling_title` "Preparing the battle: {COUNT} shaders compiled";
  `taom_rw_notice_compiling_detail` "The game is compiling this battle's shaders. It is not frozen. The first battle after a game update or a change to your mods can take several minutes.";
  `taom_rw_notice_stalled_title` "Still loading: no shader compiles for {SECONDS} s";
  `taom_rw_notice_stalled_detail` "The game's own log shows no progress. If nothing changes for 5 minutes the game may be stuck: create a dump file of Bannerlord.exe in Task Manager and send your TAOM logs."
  Then run the "Language rows helper" with these four `(key, English text)` pairs as `ROWS` (`ok 12`, `4 0` per language file) and stage the 12 files with the commit.
- `BattleLoadDiagnosticsIoC.cs`: `container.Register<IRenderWaitNotice, Features.ShaderCompileNotice.RenderWaitNoticeHost>(Reuse.Singleton);`
- Doc: `### The render-wait notice` in `battle-load-diagnostics.md` (trigger rule, the honesty rule, needs the master toggle because it reads the load window, owed checks) and one cross-link line in `shader-compile-notice.md`.

**Verify**: build exit 0; filtered `FullyQualifiedName~RenderWaitNotice` and `FullyQualifiedName~MainThreadFreezeWatchdogTests` 0 failed; filtered `"FullyQualifiedName~LanguageFileCoverageTests|FullyQualifiedName~LanguageDataXmlTests|FullyQualifiedName~UnregisteredLocalizationKeyBaselineTests"` 0 failed; full suite 0 failed; dash check 0.

Commit D1 subject: `feat(diagnostics): v2.0.28 - honest notice during a battle render wait`
Trailers: `Research: engine log tail only; native counter stays main-thread`; `Not-tested: window over borderless and exclusive fullscreen; engine log flush latency`; `Refs #<new issue>`.

### Step D2: the next-session notice says what the last load was doing (LOADING-HANG-04, TDD)

RED:
- `StallEvidenceTests`: `Parse_RenderWaitThenFreezeWithCompiles_ClassifiesCompiling`;
  `Parse_RenderWaitThenFreezeWithNoCompilesForEightySeconds_ClassifiesStuck`;
  `Parse_RenderWaitWithoutFreezeLines_ClassifiesRenderWaitUnknown`;
  `Parse_LastPhaseAgentEquipBegin_ClassifiesNone`; `Parse_LinesBeforeTheLastMissionInitialize_AreIgnored`;
  `Parse_NoBattleLoadLine_ReturnsNull`. Build the `[Freeze]` lines with `FreezeLog.FormatSample` so the contract stays one.
- `BattleLoadStallMarkerTests`: `TryConsumeStaleMarker_ReadsEvidenceFromThePriorLog` (temp prior log plus a marker whose `log=` points at it).

GREEN:
- `Main/Features/BattleLoadDiagnostics/Domain/StallEvidence.cs`: `public enum StallEvidenceKind { None, Compiling, Stuck, RenderWaitUnknown }`;
  `public sealed class StallEvidence` with `LastPhase`, `LastShaders` (int?), `FreezeSilentSeconds` (int?), `FreezeRglCompiles` (int?),
  `FreezeRglUnavailable` (bool), `public static StallEvidence? Parse(string tailText)` (scope: lines after the last
  `[BattleLoad] seq=` line whose phase is `MissionInitialize`; anchor on the `[BattleLoad] seq=` prefix, because a
  `[Freeze]` line's status tail also carries `phase=`; the last `[BattleLoad] seq=` line gives the phase and `shaders=`; the `[Freeze]` sample
  line with the largest silence gives the freeze tokens) and `Classify()`: phase not `WaitingForRender` -> None;
  `FreezeRglCompiles > 0` -> Compiling; `FreezeRglCompiles == 0 && FreezeSilentSeconds >= 60 && !FreezeRglUnavailable` -> Stuck; else RenderWaitUnknown.
- `StallEvidenceReader.TryRead(string? logPath)`: last 256 KB of the prior log (`FileShare.ReadWrite`), `Parse`; catch everything, return null.
- `StallMarkerInfo`: add `public StallEvidence? Evidence { get; internal set; }`. `BattleLoadStallMarker.TryConsumeStaleMarker`:
  after `Parse`, set `info.Evidence = StallEvidenceReader.TryRead(info.LogFilePath)` and append ` evidence=<Kind>` to its WARNING line.
- `StallReportNotifier.BuildMessage`: after the first sentence add one localized sentence by kind
  (`TextObject` + `SetTextVariable`, main thread at the main menu):
  `taom_bld_stall_compiling` "The battle was still being prepared: the game's own log shows {COUNT} shader compiles after the loading screen stopped updating. The first battle after a game update or a change to your mods can take several minutes. Next time, give it at least 5 minutes before closing the game.";
  `taom_bld_stall_stuck` "The battle had stopped loading: the game's own log shows no shader compiles in the last {SECONDS} seconds before the game was closed. Please attach the log below to a bug report.";
  `taom_bld_stall_render_unknown` "The battle was waiting for its graphics to be prepared. After a game update or a change to your mods this can take several minutes, but the log cannot tell whether it was still working. If it happens again, wait 5 minutes before closing the game."
  Register the three rows next to the existing `taom_bld_stall_*` rows. None -> the message is unchanged.
  Then run the "Language rows helper" with these three `(key, English text)` pairs as `ROWS` (`ok 12`, `3 0` per language file) and stage the 12 files with the commit.
- Doc: update "Reaching the dev: the stall marker + next-session notice" with the evidence rule.

**Verify**: build exit 0; filtered `FullyQualifiedName~StallEvidence` and `FullyQualifiedName~BattleLoadStallMarkerTests` 0 failed; filtered `"FullyQualifiedName~LanguageFileCoverageTests|FullyQualifiedName~LanguageDataXmlTests|FullyQualifiedName~UnregisteredLocalizationKeyBaselineTests"` 0 failed; full suite 0 failed.

Commit D2 subject: `fix(diagnostics): v2.0.28 - stall notice says what the last load did`
Trailers: `Not-tested: the inquiry text in game`; `Refs #<new issue>`.

### Part D done criteria

- [ ] Full suite 0 failed; total = Part C total + the D1 and D2 test methods (report; at least 18).
- [ ] `git grep -n "GetNumberOfShaderCompilationsInProgress\|ShadersInFlight" -- Main/Features/ShaderCompileNotice` prints nothing (the notice never reads the native count).
- [ ] `git grep -n "PriorityClass" -- Main/Features/ShaderCompileNotice/RenderWaitNoticeHost.cs` prints nothing.

---

## Part E: an opt-in memory dump of a frozen game (new, TDD)

Why: a Task Manager dump is a full dump (about the game's memory use, 9 to 11 GB in E3 and E4), and a
frozen player rarely takes one. A thread-stacks-only minidump written by TAOM itself at 80 s of silence
names the native frame (the freeze lens lists the v1.4.8 skeleton-buffer spin addresses such a dump
would show, FREEZE-THREADING-06) and is a few MB.

RED:
- `FreezeDumpPolicyTests`: `Disabled_NeverWrites`; `AlreadyWrittenThisProcess_NeverWrites`; `BeforeEightySeconds_NeverWrites`;
  `WalkOrExitSampler_NeverWrites`; `HeapMoving_NeverWrites`; `StillCompiling_NeverWrites` (engine log available, last compile 10 s ago);
  `FlatHeapAtEightyNoCompiles_Writes`; `MissionFrameInFlight_Writes`.
- `FreezeDumpWriterTests`: `FileName_HasTimestampAndPid` -> `taom_freeze_2026-11-04_20-03-30_pid1744.dmp`.
- Provider: `FreezeDumpEnabled_NoMcmInstance_DefaultsFalse`.

GREEN:
- `FreezeDumpPolicy.ShouldWrite(bool enabled, bool alreadyWritten, int thresholdIndex, FreezeStandDown standDown, bool heapMoving, bool rglAvailable, double? secondsSinceLastCompile)`:
  false unless enabled, not written, `thresholdIndex == 3`, stand-down not walk or exit sampler, heap not moving,
  and not (`rglAvailable` and `secondsSinceLastCompile < 30`).
- `FreezeDumpWriter`: P/Invoke as shipped in ButterLib (`dbghelp.dll`, `SetLastError = true`, `MarshalAs(UnmanagedType.Bool)` return)
  with the three optional pointer arguments declared `IntPtr` and passed `IntPtr.Zero`; dump type `0x1000 | 0x20`
  (thread info, unloaded modules); `TryWrite(string directory, out string pathOrError)` writes
  `FileName(DateTime.Now, pid)` into the folder of `Path.GetFullPath(logger.LogFilePath)` (fallback `Logs`),
  using `FileStream.SafeFileHandle` and `Process.GetCurrentProcess()` handle and id. Any failure returns false with the error text.
- Watchdog: at threshold index 3 evaluate the policy (independent of the sample action); on true write once per
  process and log `[Freeze] memory dump written: <path> (<n> MB)` or `[Freeze] memory dump failed: <error>`.
- Setting: `WriteFreezeDump`, display name `"Write Memory Dump On Freeze"` (the name the in-game check uses) (group `"Exit Stall Sampler"`, next Order, `RequireRestart = false`, default `false`,
  HintText "When the game has finished no frame for 80 seconds and the engine log shows no shader compiling, writes one small memory dump (thread stacks only, a few MB) to the TAOM Logs folder for a bug report. One per game session. Off by default: turn it on when asked to help with a freeze report.");
  provider `FreezeDumpEnabled`; `CoopSettingsRelevance` Instrumentation entry; fingerprint and the two co-op docs counts + 1 again.
- Doc: `### Optional freeze dump` (when it fires, size, where, how a maintainer reads it: WinDbg `~*k`, `python tools/native_crash_triage.py --rva 0x<offset>`), Configuration row.

**Verify**: build exit 0; filtered `FullyQualifiedName~FreezeDump` and `FullyQualifiedName~SettingsFingerprintTests` 0 failed; full suite 0 failed.

Commit E subject: `feat(diagnostics): v2.0.28 - opt-in memory dump on a frozen game`
Trailers: `Research: MiniDumpWriteDump shape and MINIDUMP_TYPE values from the shipped Bannerlord.ButterLib.dll`;
`Not-tested: writing a dump from inside the frozen game`; `Refs #<new issue>`.

### Part E done criteria

- [ ] Full suite 0 failed; total = Part D total + the Part E test methods (report; at least 10).
- [ ] `git grep -n "WriteFreezeDump" -- Main/Features/CoopInterop/CoopSettingsRelevance.cs` one match; the setting defaults to `false`.

---

## Disposition table (every commit in the pack)

| SHA | 1.5.x subject | Disposition |
|---|---|---|
| `89374fc4f` | fix(tools): v2.0.34 - battle-load triage spots a frozen shader count | TRANSPLANTED (A1, end state). Doc hunk in A3. Lesson hunk (`state-lifecycle-save.md`) to WP142 |
| `d99398f12` | Add shader source files for bow deformers and implement shader source installation service | PARTIAL. Triage hunks TRANSPLANTED (A1). `tools/package_release.py` `missing_armory_shaders` gate and its tests: WP115. ShaderSourceInstall (18 `.rs`, service, adapter, `.gitattributes` rule, 13 strings, doc): SKIPPED (D12) |
| `b9eabd712` | Refactor UIExtenderEx registration to OnGameStart for improved stability | PARTIAL. Triage hunks TRANSPLANTED (A1). `Client/` registration move, `ClientSubModuleTests`, `ClientIsolationGateTests`, `CoopSuppressedUiAttribute`, the `SubModule.cs` comment and the co-op docs: SKIPPED (TAOM.Client absent on 1.4.x, D7; the hazard is latent on 1.4.x, no mixin reads a `CampaignUIHelper` static). `harmony-il.md`, `build-tooling-workflow.md`, `state-lifecycle-save.md` lessons: WP142. Review prompt and RCA: not ported (review records) |
| `76a395729` | feat: Implement LastSeenTextGuard to prevent NullReferenceException in HeroHelper.GetLastSeenText | PARTIAL. Triage hunks TRANSPLANTED (A1); doc hunk in A3. Patch123, `LastSeenState`, their tests, the registry, API-snapshot and v1.5.5 doc rows: SKIPPED (adapts a v1.5.x `HeroHelper` rewrite; v1.4.8 reads only `LastKnownClosestSettlement`). Lessons: WP142. RCA and prompt: not ported |
| `1581d4d0a` | fix(tools): v2.0.35 - battle-load triage reports only this load's watchdog | TRANSPLANTED (A1, #1025). Doc hunk in A3. Lesson: WP142. RCA `rca-triage-render-silence-2026-10-26.md`: not ported (review record) |
| `0b74cc2dd` | fix(state-lifecycle): clarify outcome of battle load issue related to mount swapping | Lessons-only hunk to WP142; its content restated in `battle-load-diagnostics.md` (A3) as a player report, not a mechanism |
| `82645b53f` | feat(shaders): v2.0.34 - show shader compile progress during rebuilds | PORTED (B1, cherry-pick -n, adapted; #752). `feature-map.md` hunk dropped (WP142); `shader-cache-keeper.md` hunk dropped (doc absent, D11); provenance row kept; its three strings land in `taom_module_strings.xml` plus English placeholder rows in the 12 language files (the pack's port note; translation by WP135) |
| `25995dc91` | feat: Add custom battle banner bearers model to restrict trolls from carrying banners | NOT PORTED here. ShaderPrecompilation re-park: rejected by D4 (park reason does not hold on 1.4.x; evidence written in A3). `TaomCustomBattleBannerBearersModel`: WP130. TrollBruteForce, Patch92, `TrollActionTrace`, `TrollFormationSpacingTracker`, `IFormationAdapter.UnitDiameter`, MixedFormations pitch, hill-troll tools and docs: OUT (feature and content). `animation-skeleton.md` and `build-tooling-workflow.md` lessons: WP142 (engine notes optional in WP144). Re-park RCA: not ported |
| `372773d1a` | feat(shaders): v2.0.34 - keep the shader cache across mod list changes | SKIPPED (D11). Gate in Maintenance notes |
| `84f0459bc` | fix(shaders): v2.0.35 - TAOM installs its bow shader sources at startup | SKIPPED (D12). WP115's `missing_armory_shaders` gate catches the day a 1.4.8 Armory needs them |

Deep-dive findings owned here:

| Finding | Where it is addressed |
|---|---|
| LOADING-HANG-01 (first-battle stall at the render gate; nothing shown or logged) | A1, A3 (triage and guidance), B (startup notice, cache-state line), C (`[Freeze]` with engine compile counts), D1 (render-wait notice). The keeper fix direction is deferred (D11) |
| LOADING-HANG-04 (stale marker cannot tell a kill during a compile from a hang) | D2 |
| FREEZE-THREADING-02 (E3 is a main-thread stall before any mission tick; watchdog too late, no stack) | A1, C |
| FREEZE-THREADING-06 (no diagnostic names the frame of a frozen game) | C (heartbeat, stacks), E (dump) |
| RUNTIME-PERF-01 (negative result: not TAOM per-frame code) | Nothing to fix; C's `context=mission-frame` lines with `rglCompiles` answer its open mid-battle compile question |
| #612 Warg-mount wedge lesson | A3 (feature doc); lessons file in WP142 |
| WP108 hand-over: the `polls=1` means #352 wording (`MissionState_TickLoading_BattleLoad_Patch.cs`, `battle-load-diagnostics.md`, the registry, `tools/triage_battle_load.py`) | A3 item 4; the sites plan 108 did not name stay as they are (Maintenance notes) |

## Test plan

| Part | New tests | Pattern to copy |
|---|---|---|
| A | 34 ported Python tests (A1) + 4 `SessionRenderRateTests` (A2): 153 to 191 | `RenderWaitSilenceTests` in the same file |
| B | 16 `ShaderCompileProgressTests` (ported) + 4 `ShaderCacheStateTests` | the ported file |
| C | `MainThreadHeartbeatTests` 3, `FreezeSamplePolicyTests` 6, `FreezeLogTests` 4, `RglLogLocatorTests` 4, `RglCompileTailTests` 6, `MainThreadFreezeWatchdogTests` 14, provider 1; Python `FreezeEvidenceTests` 8 (191 to 199) | `MissionTickStallWatchdogTests`, `MemoryPressureSamplerTests` literal pins |
| D | `RenderWaitNoticeDeciderTests` 9, watchdog 2, `StallEvidenceTests` 6, marker 1 | `BattleLoadStallMarkerTests` temp-file style |
| E | `FreezeDumpPolicyTests` 8, `FreezeDumpWriterTests` 1, provider 1 | `BattleLoadDiagnosticsSettingsProviderTests` |

Structurally untestable (name them in `Not-tested:`): `Thread.Suspend` on the live main thread; the
WinForms windows and their threads; the engine log's flush latency; the dbghelp call; native ticks while
the game is minimized; everything about the render gate itself.

## Done criteria (whole WP)

- [ ] `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` exits 0.
- [ ] `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` 0 failed; total = the previous merged WP's recorded count + every C# test method this plan added (report the number).
- [ ] `python tools/tests/test_triage_battle_load.py`: 199 tests, `OK`; tool suite no new failure.
- [ ] `git grep -c "GetNumberOfShaderCompilationsInProgress" -- Main/` prints exactly the five `file:1` lines recorded at A0 (the only calls stay `Main/Features/BattleLoadDiagnostics/Hooks/MissionState_OnTick_RenderWait_Patch.cs` and `Main/Features/ShaderPrecompilation/ShaderPrecompileRunner.cs`, both main-thread).
- [ ] `git diff --name-only bannerlord-1.4.5...HEAD` (three dots: from the merge base, so WPs merged into `bannerlord-1.4.5` after this branch was cut do not show) lists only in-scope paths; no `CHANGELOG.md`, `.claude/`, lessons, `Main/IoC.cs`, and no language file other than the 12 `std_taom_module_strings_*.xml`.
- [ ] `git diff --numstat bannerlord-1.4.5...HEAD -- Main/_Module/ModuleData/Languages` prints exactly 12 lines, each `10 0` (3 rows from B1, 4 from D1, 3 from D2, nothing removed).
- [ ] Every commit subject matches `^[a-z]+\([a-z0-9-]+\): v2\.0\.28 - ` and is at most 72 characters (`git log --format=%s bannerlord-1.4.5..HEAD`).
- [ ] The Dash check command (with `-X utf8`) prints `0` over `git diff bannerlord-1.4.5...HEAD -U0 -- '*.md'`.
- [ ] The hand-off reports status, commit hashes and the measured test totals; `plans/README.md` is untouched (the integrator records them).

## STOP conditions

Stop and report (do not improvise) if:
- Any A0 `git grep` for the WP106 symbols prints no match, or `git diff 89374fc4f^ HEAD` on the two triage files shows anything other than nothing or the two Patch43 string hunks.
- `Main/Features/ShaderCompileNotice/` already exists, or `git grep -n "MainThreadHeartbeat\|\[Freeze\]" -- Main/ tools/` matches before Part C (someone landed overlapping work).
- `Main/SubModule.cs` has no `protected override void OnApplicationTick(float dt)`, or the Patch21 lines or the `TaomPrecompileShaders` option are commented out (that would contradict D4: report, do not re-enable).
- `Main/Composition/FeatureModules.cs` exists and lists a module for BattleLoadDiagnostics or ShaderCompileNotice (D2 then requires module wiring; this plan's hand-wiring would be wrong).
- `git cherry-pick -n 82645b53f` conflicts in a path not in the B1 table.
- The "Language rows helper" stops on an assertion (a key already present, a file without exactly one `  </strings>`, a file that no longer parses, not exactly 12 files), or `LanguageFileCoverageTests`, `LanguageDataXmlTests` or `UnregisteredLocalizationKeyBaselineTests` still fail after the rows are in, or the ModuleData validation hook blocks a strings-only commit (never bypass it).
- `SettingsFingerprintTests` or `EveryDocQuotingTheSettingsCounts_AgreesWithReflection` cannot be satisfied by the +1 changes described (the live numbers disagree with each other).
- Any implementation step seems to need: a call to `GetNumberOfShaderCompilationsInProgress` off the main thread, a `Thread.Suspend` outside `ThreadStackCapture`, a new Harmony patch, an edit to `Main/IoC.cs`, a change of a persisted MCM default, or reading `BattleLoadRenderWaitProbe` as notice progress.
- A TaleWorlds signature differs from "Engine facts" (do not decompile and improvise; report the mismatch).
- A step's verification fails twice after a reasonable fix attempt.

## In-game checks owed (maintainer; logs are `Logs\taom_debug_*.log` next to the game exe and `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_<pid>.txt`)

1. **Triage (no game, after Part A)**: run `python tools/triage_battle_load.py` on the two 2026-11-01 Harad logs. Expect E3: "went silent 92s before the log ended ... cannot separate a slow cold compile from a main-thread wedge"; E4: the same wording for the 806 load with 206 s, plus the "Same-session rate (heuristic)" line saying about 35 s.
2. **Startup notice (#752, Part B)**: toggle a shader-less mod (BetterTime) and start. Expect the window over the loading screen within seconds, counting up, no focus taken, closed at the main menu; Task Manager shows Below normal while it is up; both `[ShaderNotice]` lines; `[ShaderCache] ... rewrittenThisStart=true` with a low `sacx=`. Start again unchanged: `rewrittenThisStart=false`. Also check exclusive fullscreen.
3. **Freeze sampler (Part C)**: (a) 10 minutes of normal map play: no `[Freeze]` line; (b) alt-tab away for 30 s on the map: no `[Freeze]` line (if lines appear, the native loop stops ticking while unfocused: report it); (c) save a large campaign: at most one stack-less `[Freeze]` line at 40 s and one `recovered` line; (d) the first battle after the BetterTime toggle: `[Freeze]` lines during the render wait with `rglCompiles` rising, then `recovered after about N s`; note whether the stack reads `<capture failed` or `<no managed frames`; (e) in Visual Studio attach, freeze only the main thread for 90 s, continue: four `[Freeze]` lines with stacks, then `recovered`.
4. **Render-wait notice (Part D1)**: during (3d), the window says "Preparing the battle: N shaders compiled" while compile lines arrive and closes at BattlePlayable. Check borderless and exclusive fullscreen (the game must not lose focus or minimize).
5. **Stale notice (Part D2)**: kill the game during a cold render wait; at the next main menu the inquiry carries the "still being prepared" sentence (compile lines seen) or "had stopped loading" (none for 60 s or more).
6. **Dump (Part E)**: enable "Write Memory Dump On Freeze", repeat (3e): one `taom_freeze_*.dmp` of a few MB in `Logs`, `[Freeze] memory dump written`; WinDbg `~*k` shows the main thread.

## Maintainer steps

- Create the GitHub issue for the new work (freeze sampler, render-wait notice, stale-notice evidence, opt-in dump, shader-cache line) before Part C lands; executors cite it as `Refs #<n>`.
- Translator run for the ten new English keys (`taom_shader_notice_*` 3, `taom_rw_notice_*` 4, `taom_bld_stall_*` 3; each lands with an English placeholder row in all 12 language files): WP135 or `/localize` (paid; maintainer's call).
- The in-game checks above. This WP changes only `TAOM.dll` (no `Dependencies/`), so it adds no #371 pairing requirement.
- Review gate: the executor runs `/deep-review` on Parts C and D before hand-off (C# across many files plus a single-owner file) and `/verify` before each part's final commit, if its session can invoke skills; otherwise it says so in the hand-off and the integrator runs them. Never `/review-codex` or `/codex-verify` (paid; maintainer only). `/verify-bindings` is not needed (no new binding).

## Maintenance notes

- **ShaderCacheKeeper gate (D11)**: revisit `372773d1a` only when (1) a `shader_mapping.bin` written by v1.4.8 build 119303 has been read and matches `keeper.c`'s parser, (2) the engine's compared module list is shown equal to the command-line list the keeper writes (D3 claim 8; a NavalDLC-without-War-Sails player is the known counter-path), and (3) the maintainer accepts shipping an unsigned `xinput9_1_0.dll` into the game's bin folder. Part B's `[ShaderCache]` line measures how often players start on a dropped cache, which is the keeper's value.
- **Bow shader sources (D12)**: if a 1.4.8 Armory ever references `taom_deformer_*` materials, port `d99398f12` plus `84f0459bc` wired through `SubModule.cs`; WP115's packager gate flags it.
- **Deferred, not fixed here**: `LogTailCollector.FindLatestRglLog` (`Main/Features/CrashReport/Collectors/LogTailCollector.cs:51-75`) globs `rgl_log_*.txt`, which also matches `rgl_log_errors_*.txt`, so a bundle may carry the errors log instead of the full one; the bundle has one rgl slot and the missing-body triage prefers the errors log, so fixing it needs a decision about carrying both. `ShaderPrecompilePlayerAgentGuard`'s `FinishDeployment` inside `OnMissionTick` (Order of Battle fallback only; WP144 records it as a fix item). The precompile hint and inquiry strings still say "reordering" (player text; WP135). The same `polls=1` means #352 claim at the sites plan 108 did not hand over (`BattleLoadDiagnosticsService.cs:23-24`, `Domain/BattleLoadPhase.cs:81-82`, `battle-load-diagnostics.md:749`, the `MissionInitializeDone` hint in `tools/triage_battle_load.py`, `docs/investigations/native-commit-audit-2026-08.md:728`, `docs/reviews/lessons/testing-qa.md:929`, cited at `1b00d1acd`): not corrected here, the maintainer assigns them. Plan 108's four wording corrections are done in A3 item 4.
- **Reviewer focus**: the watchdog's new-beat test (`beat != _lastSeenBeat`) must be the only episode end; the heap gate must reset its reference after every threshold; no allocation or logging may sit between suspend and resume (it lives only in `ThreadStackCapture`); the notice host must exit its thread and must not touch the engine; the per-frame `Beat()` must stay two writes.
- **Interactions**: WP109's skeleton-buffer watch adds `[SkeletonBuffer]` lines the post-load check already names; WP117's `[MissionPerf]` heartbeat stamps inside the mission tick for frame statistics, a different measurement that plan 117 keeps beside this one on purpose ("Two per-frame stamps on purpose"), so keep `MainThreadHeartbeat` the only liveness stamp and do not merge the two; WP114's save probes can read `[Freeze] ... recovered after about N s` as a save-duration signal; any later MCM page change must update the fingerprint and the two co-op docs.
- **UNVERIFIED and owed**: that `rgl_log_<n>.txt` is named by the process id (the locator falls back to the newest log written since process start); the engine log's flush latency; whether native keeps ticking while the window is minimized; how `Thread.Suspend` plus `StackTrace` behaves on a main thread blocked in native code (f75288ebf expects `<capture failed>`); window visibility over exclusive fullscreen; that only a startup cache drop writes `shader_mapping.bin` before `OnSubModuleLoad`.
- **Not changed on purpose**: the 300 s `StallWatchdogSeconds` default (json2 rule; the `[Freeze]` lines are the early marker V02 asked for); the master toggle semantics.
