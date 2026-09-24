# Plan 019: Stop discarding the nullable warnings and graduate the first folder (Siege) to errors

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report; do not improvise. Do NOT edit `plans/README.md`: the orchestrator
> maintains that index.
>
> **Where you work (read this twice)**: all work happens in the worktree `E:/repos/wt-plan-019`,
> never in the main tree `E:/repos/TAOM`. The main tree holds another session's uncommitted edits
> (including `Main/IoC.cs`, `Main/SubModule.cs`, `CHANGELOG.md` and several feature folders). Your
> shell working directory resets to `E:/repos/TAOM` between calls, so:
> - **Every Bash call begins with `cd /e/repos/wt-plan-019 && `** (the one exception is the
>   `git worktree add` in Step 0, which uses `git -C`). A relative path in a call without that
>   prefix runs against the main tree. If a call is refused or waits on a permission prompt, STOP
>   and report it; do not move the work into the main tree.
> - **Every Read, Edit and Write path is absolute**: `E:/repos/wt-plan-019/<repo path>`. Never open
>   anything under `E:/repos/TAOM/` for editing.
> - **Scratch files** (build logs, the counting script) go in `E:/repos/wt-plan-019-scratch/`
>   (Bash: `/e/repos/wt-plan-019-scratch/`), created in Step 0, never inside the worktree.
> - **Every `git commit` line in this plan is written with the prefix too.** Copy each one whole;
>   never run a bare `git commit` or `git add` (without the prefix it stages or commits in the main
>   tree, possibly another session's hunks).
> - **Tool timeout**: pass `timeout: 600000` on every call that runs `dotnet build` or `dotnet test`.
>   At planning the full suite took 40 s wall (build plus run) and a fresh Main build 8 s, so this is
>   ample. If a call still times out, re-run the same command with `run_in_background: true` and
>   read its log after it exits; never judge a step from a partial log. Never run two `dotnet`
>   commands at once in this worktree.
> - Never spell `python3`; use `python`. Write any multi-line script with the Write tool, never a
>   Bash heredoc (heredocs mangle backslashes).
>
> **Drift check (run first, after Step 0 creates the worktree)**:
> `cd /e/repos/wt-plan-019 && git diff --stat b2e387db..HEAD -- Main/TAOM.csproj Dependencies/TAOM.Dependencies.csproj .editorconfig Main/Features/Siege TAOM.Tests/Features/Siege docs/features/siege.md docs/ai-includes/code-quality.md`
> Expected: no output (at planning time `HEAD` was `4b5662b2`, which touched none of these, and the
> main tree had no uncommitted edit to any of them). `CHANGELOG.md` is in scope but deliberately left
> out: every session appends to it. Any output: compare the "Current state" excerpts against the
> live files; on a mismatch, STOP and report.

## Status

- **Priority**: P3
- **Effort**: S (two one-line csproj edits, one `.editorconfig` block, one nested `.editorconfig`, one guard clause, seven property annotations, one signature, two tests, three doc edits)
- **Risk**: LOW. Step 1 is output-neutral by construction and is proven by a warning count. The Siege edits are annotations plus one guard clause whose runtime outcome equals today's (see Current state). No save-format change, no IoC change, no `SubModule.cs` change.
- **Depends on**: none. Plan 010 edits other lines of the same two csproj files (Reference Include/Exclude strings); see Maintenance notes.
- **Category**: tech-debt
- **Planned at**: commit `b2e387db`, 2026-09-23 (`HEAD` was `4b5662b2`; no in-scope file differs)
- **Issue**: create before implementation lands (orchestrator). If the orchestrator gave you an
  issue number `N`, end the Commit B and Commit C bodies with a line `Refs #N`. If it gave none,
  write no issue reference (never invent a number).
- **Review**: the executor cannot invoke skills or spawn agents, so the C# commit lands on the plan
  branch without `/deep-review`. The orchestrator runs `/deep-review` on the branch before any merge
  (reviewer focus: Maintenance notes).

## Why this matters

`Directory.Build.props` turns nullable reference types on for every project, and then
`Main/TAOM.csproj` and `Dependencies/TAOM.Dependencies.csproj` throw the seven main nullable warning
ids away with `<NoWarn>`. A scratch build at `b2e387db` with the suppression lifted printed **2,028**
distinct nullable diagnostics (CS8618 756, CS8603 408, CS8604 331, CS8625 285, CS8600 108, CS8601
78, CS8602 62), and every new file in every folder lands with no null-flow signal at all, so the
backlog only grows. Because the compiler's `/nowarn` beats any `.editorconfig` severity for the same
id, no folder can opt back in today. This plan moves the suppression from `<NoWarn>` into the root
`.editorconfig` (build output unchanged), then makes the first folder, `Main/Features/Siege`,
null-clean and sets the seven ids to **error** there, so any new null-flow mistake in that folder
fails the build. Each later folder is a small, separate change using the procedure in Maintenance
notes.

## Current state

All excerpts are from commit `b2e387db`, read with `git show b2e387db:<path>`.

### The suppression

- `Directory.Build.props:5-6` (not edited by this plan; it is hook-protected):
  ```xml
  		<LangVersion>10.0</LangVersion>
  		<Nullable>enable</Nullable>
  ```
  `LangVersion` 10 means the C# 11 `required` modifier is NOT available in `Main`; do not use it.
  (`Dependencies/TAOM.Dependencies.csproj:12` overrides this with `<LangVersion>preview</LangVersion>`;
  this plan edits no Dependencies code.)
- `Main/TAOM.csproj:9` (single-owner file, in scope for exactly this line):
  ```xml
  		<NoWarn>$(NoWarn);8600;8601;8602;8603;8604;8618;8625</NoWarn>
  ```
- `Dependencies/TAOM.Dependencies.csproj:9` (single-owner file, in scope for exactly this line):
  ```xml
  		<NoWarn>$(NoWarn);8600;8601;8602;8603;8604;8618;8625;8374;8174</NoWarn>
  ```
  `8374` and `8174` are not nullable ids: they stay.
- `Main/TAOM.csproj:113` already references the `Nullable` 1.3.1 polyfill, so `[NotNullWhen]`,
  `[MaybeNull]` and friends compile on net472.
- `TAOM.Tests/TAOM.Tests.csproj` has **no** `<NoWarn>`: the test project shows its nullable warnings
  today (2,256 distinct CS86xx in the planning baseline's test build). This plan must not silence
  them, which is why the root `.editorconfig` block below is scoped to `Main/` and `Dependencies/`
  rather than to every `*.cs`.
- No `TreatWarningsAsErrors`, ruleset or globalconfig exists anywhere; the only `.editorconfig` is the
  root one. Its full content (22 lines):
  ```ini
  root = true

  [*]
  charset = utf-8
  end_of_line = crlf
  insert_final_newline = true
  trim_trailing_whitespace = true

  [*.cs]
  indent_style = space
  indent_size = 4

  [*.{xml,xslt,xsd,json,csproj}]
  indent_style = space
  indent_size = 2

  [*.{ps1,psm1}]
  indent_style = space
  indent_size = 4

  [*.md]
  trim_trailing_whitespace = false
  ```

### Why the ids must leave `<NoWarn>` (verified during the audit, not assumed)

In the SDK 10.0.401 compiler (`Microsoft.CodeAnalysis.CSharp.CSharpDiagnosticFilter.GetDiagnosticReport`,
decompiled during the audit's verification pass) an `.editorconfig` severity is consulted only when
the id is NOT already in the compilation's specific diagnostic options, and `/nowarn` (what `<NoWarn>`
becomes) puts the id there as `Suppress`. So a folder `.editorconfig` cannot re-enable an id that
`<NoWarn>` suppresses. Moving the suppression into the root `.editorconfig` as `none` keeps today's
output and lets a nearer `.editorconfig` override it per folder (a nearer file wins over a farther
one for the same key).

### Baseline numbers you will compare against (planning build of `b2e387db`)

- `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` on a fresh worktree: exit 0,
  `2 Warning(s)`, `0 Error(s)`. The two are analyzer warnings, not nullable ones:
  `BHA0001` at `Main/Features/LordPartyTemplates/Hooks/Patch88_InitializeLordPartyPropertiesScope.cs:22`
  and `BHA0006` at `Main/Features/CultureDoctrine/Hooks/TeamTacticProbe.cs:40`. Zero CS86xx.
- Tests: 10,239 total, 10,235 passed, **2 failed**, 2 skipped (40 s wall for build plus run). The 2
  failures are `TheElkItem_DeclaresTheScaleTheReachIsTunedFor` (in the Elk tests) and
  `AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist` (in `AnimaliaMountWiringTests`): both
  assert on the live, unversioned Armory install, which another session is editing. They are NOT
  caused by this plan; do not chase, fix or skip them. They may also pass, if that session has
  finished. The 2 skipped are deliberate `[Ignore]`s in `WargAttackServiceTests.cs`.
- Siege's share of the 2,028 (the whole folder, 8 locations in 3 files):
  ```
  Main\Features\Siege\Hooks\BesiegerCamp_GetSiegeCampPartyPosition_Patch.cs(50,24) CS8602
  Main\Features\Siege\Models\ActiveSiegeDefenseEvent.cs(7,19) CS8618
  Main\Features\Siege\Models\ActiveSiegeDefenseEvent.cs(8,19) CS8618
  Main\Features\Siege\Models\KingdomSiegeMessages.cs(5,19) CS8618
  Main\Features\Siege\Models\KingdomSiegeMessages.cs(6,19) CS8618
  Main\Features\Siege\Models\KingdomSiegeMessages.cs(7,19) CS8618
  Main\Features\Siege\Models\KingdomSiegeMessages.cs(8,19) CS8618
  Main\Features\Siege\Models\KingdomSiegeMessages.cs(9,19) CS8618
  ```

### Why Siege is the first folder (and not Arena, the audit's first pick)

Candidates were the small folders (at most 21 warnings) with crash or RCA history. Measured from the
planning `-p:NoWarn=` build log and `git grep` over `docs/reviews/rca-*.md` (252 RCAs at `b2e387db`;
"naming an NRE" = the file contains `NullReferenceException` or the word `NRE`):

| Folder | Warnings (files) | CS8602 | RCAs naming the folder's code | Of those, naming an NRE | What the warnings are |
|---|---|---|---|---|---|
| `Features/Siege` | 8 (3) | 1 | 5 (1 names `Patch8_SiegeCampGuard`, 4 name `SiegeDefense`/`TaomSiegeEventModel`/`Features/Siege/`) | 1 | one dereference that contradicts the method's own null handling, in a crash guard; a JSON DTO whose properties really can be null |
| `Features/Arena` | 21 (5) | 0 | 9 | 2 | annotation debt only: `ResetForUnload` nulling static caches (CS8625), `return null` from a `[HarmonyFinalizer]` typed `Exception` (CS8603), a nullable argument to a callee that null-checks it |
| `Features/TroopWeight` | 10 (4) | 4 | 11 | 1 | all flow-analysis false positives or annotation debt (read at planning: every CS8602 follows a `?.` check on a related local; `GetTroopWeight(CharacterObject)` already handles null) |
| `Features/PlayerSwitcher` | 6 (2) | 3 | 1 | 0 | flow false positives on `_view?.` inside lambdas |

Siege wins on value: it is the only small folder where a warning marks a real inconsistency in code
that exists to prevent a crash, and graduating it also turns a JSON-fed DTO (where `null` is a real
input) into checked code. Its RCA history is thinner than Arena's or TroopWeight's, but graduating
those folders would add `?` marks and catch nothing today; the long-term value of the ratchet is the
null-flow signal on new code in every graduated folder.

### The Siege code

`Main/Features/Siege/Hooks/BesiegerCamp_GetSiegeCampPartyPosition_Patch.cs` (66 lines; the Harmony
prefix of `Patch8_SiegeCampGuard`, applied by `Main/SubModule.cs:1545`
`_harmony.PatchCategory("Patch8_SiegeCampGuard");`). Lines 14-65:

```csharp
    [HarmonyPrefix]
    public static bool Prefix(
        BesiegerCamp __instance,
        MobileParty mobileParty,
        ref MatrixFrame[] siegeCamp1GlobalFrames,
        ref MatrixFrame[] siegeCamp2GlobalFrames,
        ref CampaignVec2 __result)
    {
        try
        {
            if (siegeCamp1GlobalFrames != null && siegeCamp1GlobalFrames.Length > 0)
                return true;

            var settlement = __instance.SiegeEvent?.BesiegedSettlement;
            var settlementId = settlement?.StringId ?? "unknown";
            var settlementName = settlement?.Name?.ToString() ?? "unknown";
            int camp2Count = siegeCamp2GlobalFrames?.Length ?? 0;

            Debug.Print(
                $"TAOM: WARNING — Settlement '{settlementName}' (id={settlementId}) has no siege_camp_1 scene entities (camp2={camp2Count} frames). Fix in map editor.",
                0, Debug.DebugColor.Red, 17592186044416uL);

            if (siegeCamp2GlobalFrames != null && siegeCamp2GlobalFrames.Length > 0)
            {
                Debug.Print(
                    $"TAOM: Using siege_camp_2 frames as fallback for '{settlementId}'",
                    0, Debug.DebugColor.Yellow, 17592186044416uL);
                siegeCamp1GlobalFrames = siegeCamp2GlobalFrames;
                siegeCamp2GlobalFrames = Array.Empty<MatrixFrame>();
                return true;
            }

            Debug.Print(
                $"TAOM: No siege camp frames at all for '{settlementId}', generating positions around gate",
                0, Debug.DebugColor.Red, 17592186044416uL);
            // Distribute parties around the gate instead of stacking on one point
            var gate = settlement.GatePosition;
            int partyIndex = mobileParty?.Party?.Index ?? 0;
            ...
            return false;
        }
        catch (Exception ex)
        {
            Debug.Print($"TAOM: BesiegerCamp patch exception: {ex.Message}", 0, Debug.DebugColor.Red, 17592186044416uL);
            return true;
        }
    }
```

Lines 27-29 treat `settlement` as nullable; line 50 dereferences it (the CS8602). With no settlement
and no frames, today's flow is: NRE at line 50, caught, "patch exception" logged, `return true`, so
vanilla runs. Engine facts (v1.5.3, `pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.Siege.BesiegerCamp`):

```csharp
	public SiegeEvent SiegeEvent { get; private set; }                                   // :26
	public BesiegerCamp(SiegeEvent siegeEvent, IFaction besiegerFaction)                  // :167
	internal CampaignVec2 GetSiegeCampPartyPosition(MobileParty mobileParty, MatrixFrame[] siegeCamp1GlobalFrames, MatrixFrame[] siegeCamp2GlobalFrames)  // :305
	...
			int num2 = MBRandom.RandomInt(siegeCamp1GlobalFrames.Length);
			...
			campaignVec = new CampaignVec2(siegeCamp1GlobalFrames[num2].origin.AsVec2 + ...
```

Vanilla indexes `siegeCamp1GlobalFrames` with no length check, so with empty frames vanilla throws;
that is the `IndexOutOfRangeException` the feature exists to prevent (`docs/features/siege.md:7-9`).
`SiegeEvent`'s constructor assigns `BesiegedSettlement = settlement;`
(`TaleWorlds.CampaignSystem.Siege.SiegeEvent.cs:798` in the same cache), so in a normal campaign the
settlement is not null and this path is rare. The guard this plan adds keeps today's outcome
(`return true`, vanilla runs) and only replaces the thrown-and-caught NRE with an explicit, logged
branch. **Choosing a new fallback position for the no-settlement case is a gameplay decision this
plan does not take** (deferred; see Maintenance notes).

`Main/Features/Siege/Models/ActiveSiegeDefenseEvent.cs` (whole file):

```csharp
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.Siege.Models;

public class ActiveSiegeDefenseEvent
{
    public string SettlementId { get; set; }
    public string DefenderFactionId { get; set; }
    public CampaignTime Deadline { get; set; }
    public bool PlayerAccepted { get; set; }
    public bool RewardClaimed { get; set; }
}
```

Both construction sites set both strings (`SiegeDefenseService.cs:118-125` from
`ISiegeEventAdapter.SettlementId`/`DefenderFactionId`, both `string`; `:264-271` from the save
snapshot key and `parts[0] ?? ""`). It is not itself saved (the service saves a
`Dictionary<string, string>` snapshot at `:230-240`), so a property initializer changes no save data.

`Main/Features/Siege/Models/KingdomSiegeMessages.cs` (whole file):

```csharp
namespace TAOM.Features.Siege.Models;

public class KingdomSiegeMessages
{
    public string Title { get; set; }
    public string Body { get; set; }
    public string AcceptButton { get; set; }
    public string AcceptMessage { get; set; }
    public string RewardMessage { get; set; }
}
```

It is deserialized by Newtonsoft from `ModuleData/siege/siege_defense_config.json`
(`SiegeDefenseConfigProvider.cs:34`
`JsonConvert.DeserializeObject<SiegeDefenseConfig>(json) ?? new SiegeDefenseConfig();`), so an entry
missing a key leaves that property `null`: the honest annotation is `string?`. Its consumers, all in
`Main/Features/Siege/SiegeDefenseService.cs`:

```csharp
    private static string Resolve(string template, string settlement, string attacker,      // :84
        int days, int influence, int relation)
    {
        if (string.IsNullOrEmpty(template)) return "";                                        // :87
        return template
            .Replace("{settlement}", settlement)
            ...
    }
    ...
            var title = Resolve(msgs.Title, settlementName, attackerName, days, 0, 0);        // :137
            var body = Resolve(msgs.Body, settlementName, attackerName, days, 0, 0);          // :138
            var acceptMsg = Resolve(msgs.AcceptMessage, settlementName, attackerName, days, 0, 0);  // :139
            ...
                affirmativeText: msgs.AcceptButton,                                            // :146
    ...
        var rewardMsg = Resolve(msgs.RewardMessage, evt.SettlementId, "", 0,                  // :335
```

Two compiler facts the fixes rely on:

1. **On net472, `string.IsNullOrEmpty` does not narrow.** The net472 reference assemblies carry no
   `[NotNullWhen(false)]`, so after `if (string.IsNullOrEmpty(x)) return;` the compiler still treats
   `x` as maybe-null. Evidence from the planning build: `TroopWeightXmlLoader.cs:92` reports CS8604 on
   `id` right after `if (string.IsNullOrEmpty(id)) continue;` at `:70`. Use
   `x is null || x.Length == 0` wherever narrowing is needed.
2. **TaleWorlds assemblies are nullable-oblivious.** `grep -c "NullableContextAttribute\|NullableAttribute"`
   over `TaleWorlds.Core.dll`, `TaleWorlds.CampaignSystem.dll` and `TaleWorlds.Library.dll` in the
   game's `bin/Win64_Shipping_Client` prints `0` for each, so passing a `string?` into an engine
   parameter (such as `InquiryData`'s `affirmativeText` at `:146`) raises no warning.

`KingdomSiegeMessages` and `ActiveSiegeDefenseEvent` are used only inside `Main/Features/Siege/` and
`TAOM.Tests/Features/Siege/SiegeDefenseServiceTests.cs` (`git grep` at planning).

### Binding conventions

- **ADR-002 (thin entry points)**: patches, models and behaviors stay under 150 lines and delegate.
  The patch is 66 lines; the guard adds about 7. Do not extract a service for one guard clause
  (`.claude/rules/simplicity-criterion.md`: a tiny win plus an added abstraction is a Reject).
- **ADR-007 (adapters)**: services never take sealed TaleWorlds types. Nothing here changes a service
  signature to an engine type; the patch is an entry point and may touch `BesiegerCamp`.
- **ADR-008 (testability)**: services are unit-testable without the engine; a real guard gets a test.
  The patch test uses bare engine objects, the pattern of
  `TAOM.Tests/Features/FiefGranting/FiefGrantingBehaviorCaptureGateTests.cs` (uninitialized objects
  via `FormatterServices.GetUninitializedObject`) and
  `TAOM.Tests/Features/BannerColorPersistence/Clan_UpdateBannerColorsAccordingToKingdom_PatchTests.cs`
  (calling a static `Prefix` directly).
- **`docs/ai-includes/code-quality.md:392-428`** ("Nullable Reference Types"): a Bad/Good code
  block only. Bad: dereferencing a `T?` (CS8602) and `hero!.Name` ("Null-forgiving operator hides
  bug"). Good: return `T?` so the caller handles null, `?? throw`, or an `is null` check that narrows.
  **This plan adds two rules of its own** (they are not in that file): use `= null!` only on a field
  the constructor cannot assign and that is set before first use; never put `!` on a value read
  from an engine type (TaleWorlds assemblies are nullable-oblivious, see fact 2 above, so `!` there
  asserts something nobody checked).
- **TDD**: RED, GREEN, REFACTOR. The failing test (Step 3) precedes the guard (Step 4).
- **Human prose**: no em or en dash in docs, CHANGELOG or commit bodies (commas, colons,
  parentheses instead). Code is exempt; do not "fix" the existing `—` inside the patch's log string.

## Commands you will need

| Purpose | Command | Expected on success |
|---|---|---|
| Build (counting) | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId= --no-incremental` | exit 0, `0 Error(s)` |
| Build tests (counting) | `dotnet build TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --no-incremental` | exit 0 |
| Tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | only the 2 known Animalia/Elk failures, 2 skipped |
| Filtered tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<ClassName>"` | all pass |
| Data | `python tools/validate_moduledata.py` | 0 errors (not needed: no ModuleData change) |
| Docs | `python tools/lint_docs.py` (this plan runs it as `python tools/lint_docs.py --quick --summary`, the dead-link check with a one-line-per-count summary) | `dead_links:` equal to the Step 0 baseline |
| Count nullable diagnostics | `python /e/repos/wt-plan-019-scratch/count_cs86.py <log> '<path fragment>'` | see each step |

**Reading a test summary**: VSTest pads the numbers, so a real line looks like
`Failed!  - Failed:     2, Passed: 10235, Skipped:     2, Total: 10239, Duration: 30 s - TAOM.Tests.dll (net472)`.
Compare the numbers, never the spacing. Where this plan writes `Failed: 1, Passed: 1`, the output
reads `Failed:     1, Passed:     1`; both mean the same thing.

The build commands are the AGENTS.md non-deploying form (`-p:DisableModuleCopy=true -p:ModuleId=`)
plus `--no-incremental`, which forces the compiler to run. Without it an up-to-date build skips
compilation and prints no warnings at all, so a count of 0 would prove nothing. NEVER `./build.ps1`
(it deploys into the game install).

## Scope

**In scope** (the only files you may modify or create):

- `Main/TAOM.csproj`: line 9 only (single-owner; this exact edit is authorized by this plan).
- `Dependencies/TAOM.Dependencies.csproj`: line 9 only (single-owner; this exact edit is authorized).
- `.editorconfig` (root): append one block.
- `Main/Features/Siege/.editorconfig` (new).
- `Main/Features/Siege/Hooks/BesiegerCamp_GetSiegeCampPartyPosition_Patch.cs`
- `Main/Features/Siege/Models/ActiveSiegeDefenseEvent.cs`
- `Main/Features/Siege/Models/KingdomSiegeMessages.cs`
- `Main/Features/Siege/SiegeDefenseService.cs`: `Resolve` only.
- `TAOM.Tests/Features/Siege/SiegeCampGuardPatchTests.cs` (new).
- `TAOM.Tests/Features/Siege/SiegeDefenseServiceTests.cs`: line 373 only (Step 5 item 4).
- Only if Step 1's fallback is used: `Main/.editorconfig` and `Dependencies/.editorconfig` (new),
  in place of the root `.editorconfig` edit.
- `docs/features/siege.md`, `docs/ai-includes/code-quality.md`, `CHANGELOG.md`.

**Out of scope** (do NOT touch):

- `Directory.Build.props` (hook-protected by `.claude/hooks/config-protection.sh`; not needed).
- `Main/IoC.cs`, `Main/SubModule.cs`, `TAOM.Tests/TAOM.Tests.csproj`: no change is needed. If you
  believe one is, STOP and report.
- Nullable warnings in every other folder, including `Dependencies/Foundation` (3) and
  `Features/Arena` (21). One folder per change.
- `docs/reviews/rca-banner-bearers-2026-07-16.md:24`, which calls the suppression deliberate. RCAs
  are historical records; the `code-quality.md` paragraph in Step 8 supersedes it.
- Any new fallback position for the no-settlement siege case (gameplay decision, deferred).
- Any `tools/` script, any ModuleData, any other test file.

## Git workflow

- **Worktree**: `git -C E:/repos/TAOM worktree add E:/repos/wt-plan-019 -b plan/019-nullable-ratchet bannerlord-1.5.x`
  (Step 0). If the branch or directory already exists, STOP and report; do not delete, reuse or reset
  either.
- **Stage explicit paths only**, by name (`cd /e/repos/wt-plan-019 && git add <path> <path>`), never
  `git add -A`, `git add .` or `git commit -a`. Commit through the Bash tool with
  `cd /e/repos/wt-plan-019 && git commit -m "<subject>" -m "<body>"`.
- **Subject**: `<type>(<scope>): v<version> - <description>`, at most 72 characters, where
  `<version>` is the `<Version value="...">` in `Main/_Module/SubModule.xml` (`v2.0.30` at planning;
  re-read it with `cd /e/repos/wt-plan-019 && grep -o '<Version value="[^"]*"' Main/_Module/SubModule.xml | head -1`).
  **The subject hook reads the main tree, not the worktree**: `.claude/hooks/check-commit-subject-version.sh:59`
  does `cd "${CLAUDE_PROJECT_DIR:-$(pwd)}"` and checks the subject against
  `E:/repos/TAOM/Main/_Module/SubModule.xml`. Before each commit, run
  `cd /e/repos/wt-plan-019 && grep -o '<Version value="[^"]*"' Main/_Module/SubModule.xml /e/repos/TAOM/Main/_Module/SubModule.xml`
  → two lines with the same version. If they differ (someone bumped the release in the main tree),
  STOP and report both versions; do not pick one.
  Check a subject's length with `printf '%s' "<subject>" | wc -c` (must print 72 or less). Body
  wrapped at 72. **No AI attribution trailer** (no `Co-Authored-By`). Optional trailers:
  `Not-tested:`, `Research:`. The three subjects are given in the steps (they assume `v2.0.30`;
  substitute the version you read).
- **Never push**, never open a PR, never merge, never touch another branch.
- If any hook denies or asks on a commit, an Edit or a Write, STOP and report the message verbatim;
  never bypass a hook, and never write a file through Bash or PowerShell because the Edit or Write
  tool was refused.

## Steps

### Step 0: Set up and record baselines

1. Create the worktree (Git workflow above) and the scratch folder:
   `cd /e/repos/wt-plan-019 && git rev-parse --show-toplevel && mkdir -p /e/repos/wt-plan-019-scratch && git rev-parse HEAD > /e/repos/wt-plan-019-scratch/base.txt && cat /e/repos/wt-plan-019-scratch/base.txt`
   → prints `E:/repos/wt-plan-019` then 40 hex. That SHA is **BASE**. The Done criteria diff against
   BASE, never against `bannerlord-1.5.x` (another session may commit to that branch meanwhile).
2. Run the drift check from the header. Expected: no output.
3. With the Write tool, create `E:/repos/wt-plan-019-scratch/count_cs86.py` with exactly this content
   (it was validated at planning against the audit's scratch log: it reports `total=2028` and 8
   Siege locations on the `-p:NoWarn=` build):

   ```python
   """Count distinct CS86xx diagnostics in an MSBuild log.

   Usage: python count_cs86.py <build.log> [path-fragment]
   Prints: total=<n> errors=<n> warnings=<n>, then one line per code, then
   in_fragment=<n> for diagnostics whose path contains the fragment
   (case-insensitive, either slash style), and those locations.
   """
   import re
   import sys
   from collections import Counter

   rx = re.compile(
       r"(?P<path>[A-Za-z]:[\\/][^:()\r\n]+?\.cs)\((?P<line>\d+),(?P<col>\d+)\): "
       r"(?P<sev>warning|error) (?P<code>CS86\d\d): (?P<msg>[^\[\r\n]*)"
   )

   log = sys.argv[1]
   frag = sys.argv[2].replace("/", "\\").lower() if len(sys.argv) > 2 else None
   seen = set()
   for line in open(log, encoding="utf-8", errors="replace"):
       for m in rx.finditer(line):
           seen.add((m["path"], int(m["line"]), int(m["col"]), m["sev"], m["code"], m["msg"].strip()))

   rows = sorted(seen)
   sev = Counter(r[3] for r in rows)
   print(f"total={len(rows)} errors={sev['error']} warnings={sev['warning']}")
   for code, n in sorted(Counter(r[4] for r in rows).items()):
       print(f"  {code} {n}")
   if frag:
       hits = [r for r in rows if frag in r[0].replace("/", "\\").lower()]
       print(f"in_fragment={len(hits)}")
       for r in hits:
           print(f"  {r[0]}({r[1]},{r[2]}) {r[3]} {r[4]}")
   ```
4. Baseline Main build:
   `cd /e/repos/wt-plan-019 && dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId= --no-incremental > /e/repos/wt-plan-019-scratch/b0-main.log 2>&1; echo "rc=$?"; grep -E "Warning\(s\)|Error\(s\)" /e/repos/wt-plan-019-scratch/b0-main.log; python /e/repos/wt-plan-019-scratch/count_cs86.py /e/repos/wt-plan-019-scratch/b0-main.log | head -1`
   → `rc=0`, `2 Warning(s)`, `0 Error(s)`, `total=0 errors=0 warnings=0`.
5. Baseline test-project build (proves later that the test project is not silenced):
   `cd /e/repos/wt-plan-019 && dotnet build TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --no-incremental > /e/repos/wt-plan-019-scratch/b0-tests.log 2>&1; echo "rc=$?"; python /e/repos/wt-plan-019-scratch/count_cs86.py /e/repos/wt-plan-019-scratch/b0-tests.log 'TAOM.Tests\' | grep -E "^total|^in_fragment"`
   → `rc=0` and an `in_fragment=` number (2,256 at `b2e387db`; it may differ if BASE is newer).
   Write that number to `/e/repos/wt-plan-019-scratch/tests-cs86-baseline.txt`. If it is 0, STOP
   (the counter or the log is wrong).
6. Baseline tests:
   `cd /e/repos/wt-plan-019 && dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= > /e/repos/wt-plan-019-scratch/t0.log 2>&1; echo "rc=$?"; grep -E "Passed!|Failed!" /e/repos/wt-plan-019-scratch/t0.log; grep -E "^\s*Failed " /e/repos/wt-plan-019-scratch/t0.log`
   → at `b2e387db` the summary numbers were `Failed: 2, Passed: 10235, Skipped: 2, Total: 10239`.
   **The counts are informational** (they change if BASE is newer); **only the failing test names
   gate**. The allowed failing names are `TheElkItem_DeclaresTheScaleTheReachIsTunedFor` and
   `AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist` (either, both or neither). Any other
   failing name: STOP and report it. Record the `Total:` number and the failing names in
   `/e/repos/wt-plan-019-scratch/t0-summary.txt` (Write tool) for Step 7.
7. Tree still clean after the builds (proves no build rewrites a tracked file, such as the vendored
   DLLs `.gitignore` re-includes under `Dependencies/_Module/bin/Win64_Shipping_Client/`):
   `cd /e/repos/wt-plan-019 && git status --porcelain`
   → no output. Any output: STOP and report it (the later "nothing unstaged" checks would fail for a
   reason this plan does not cause).
8. Docs baseline:
   `cd /e/repos/wt-plan-019 && python tools/lint_docs.py --quick --summary > /e/repos/wt-plan-019-scratch/lint0.txt 2>&1; echo "rc=$?"; grep "dead_links:" /e/repos/wt-plan-019-scratch/lint0.txt`
   → `rc=0` and one `dead_links:` line (`0` at planning). Step 8 compares against this number.

### Step 1: Move the suppression from `<NoWarn>` into the root `.editorconfig`

1. `Main/TAOM.csproj:9`: replace
   `		<NoWarn>$(NoWarn);8600;8601;8602;8603;8604;8618;8625</NoWarn>` with
   `		<NoWarn>$(NoWarn)</NoWarn>`
   (keep the line and its two leading tabs, so other projects' NoWarn composition is untouched).
2. `Dependencies/TAOM.Dependencies.csproj:9`: replace
   `		<NoWarn>$(NoWarn);8600;8601;8602;8603;8604;8618;8625;8374;8174</NoWarn>` with
   `		<NoWarn>$(NoWarn);8374;8174</NoWarn>`.
3. Root `.editorconfig`: append this block after the existing `[*.md]` section (one blank line
   before it):

   ```ini

   # Nullable ratchet (plan 019). The seven nullable warnings are off for production code by
   # default. A folder that is null-clean carries its own .editorconfig setting them to error;
   # the nearer file wins. TAOM.Tests is deliberately not listed: its warnings stay visible.
   [{Main,Dependencies}/**.cs]
   dotnet_diagnostic.CS8600.severity = none
   dotnet_diagnostic.CS8601.severity = none
   dotnet_diagnostic.CS8602.severity = none
   dotnet_diagnostic.CS8603.severity = none
   dotnet_diagnostic.CS8604.severity = none
   dotnet_diagnostic.CS8618.severity = none
   dotnet_diagnostic.CS8625.severity = none
   ```

**Verify**:
- `cd /e/repos/wt-plan-019 && dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId= --no-incremental > /e/repos/wt-plan-019-scratch/b1-main.log 2>&1; echo "rc=$?"; grep -E "Warning\(s\)|Error\(s\)" /e/repos/wt-plan-019-scratch/b1-main.log; python /e/repos/wt-plan-019-scratch/count_cs86.py /e/repos/wt-plan-019-scratch/b1-main.log | head -1`
  → `rc=0`, `2 Warning(s)`, `0 Error(s)`, `total=0 errors=0 warnings=0` (identical to Step 0.4).
  **If `total` is greater than 0**, look at the paths: run
  `python /e/repos/wt-plan-019-scratch/count_cs86.py /e/repos/wt-plan-019-scratch/b1-main.log 'wt-plan-019\Main\'`
  and the same with `'wt-plan-019\Dependencies\'`. If the two `in_fragment` numbers add up to
  `total` (every diagnostic is under `Main\` or `Dependencies\`; about 2,028 means the section glob
  matched nothing), the glob did not match: apply the fallback below. If they do not add up (some
  path is outside those two folders), STOP and report the paths. The fallback, and nothing
  else: remove the block you appended to the root file, and instead create
  `E:/repos/wt-plan-019/Main/.editorconfig` and `E:/repos/wt-plan-019/Dependencies/.editorconfig`,
  each containing a `[*.cs]` header followed by the same seven `dotnet_diagnostic` lines (no
  `root = true`). Re-run the verify. If `total` is still not 0, STOP.
- `cd /e/repos/wt-plan-019 && dotnet build TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --no-incremental > /e/repos/wt-plan-019-scratch/b1-tests.log 2>&1; echo "rc=$?"; python /e/repos/wt-plan-019-scratch/count_cs86.py /e/repos/wt-plan-019-scratch/b1-tests.log 'TAOM.Tests\' | grep -E "^in_fragment"; cat /e/repos/wt-plan-019-scratch/tests-cs86-baseline.txt`
  → `rc=0` and the two numbers are equal. If they differ, STOP (the change silenced or added
  test-project warnings).

**Commit A** (after both verifies pass):
`cd /e/repos/wt-plan-019 && git add Main/TAOM.csproj Dependencies/TAOM.Dependencies.csproj .editorconfig && git status --porcelain`
(add `Main/.editorconfig Dependencies/.editorconfig` instead of `.editorconfig` if you used the
fallback) → only those paths staged (`M ` or `A `), nothing else listed. Run the version check from
Git workflow. Then:
`cd /e/repos/wt-plan-019 && git commit -m "chore(build): v2.0.30 - move nullable suppression into .editorconfig" -m "<body>"`
with a body saying: the seven nullable ids left the two csproj NoWarn lists because /nowarn beats
any .editorconfig severity, so no folder could opt back in; the root .editorconfig now sets them to
none for Main and Dependencies only; build output unchanged (2 analyzer warnings, 0 CS86xx);
TAOM.Tests warnings untouched.

### Step 2: Prove a folder can opt back in (Siege at `warning`)

Create `E:/repos/wt-plan-019/Main/Features/Siege/.editorconfig` with exactly:

```ini
# Nullable ratchet (plan 019): this folder is null-clean. Keep it that way.
[*.cs]
dotnet_diagnostic.CS8600.severity = warning
dotnet_diagnostic.CS8601.severity = warning
dotnet_diagnostic.CS8602.severity = warning
dotnet_diagnostic.CS8603.severity = warning
dotnet_diagnostic.CS8604.severity = warning
dotnet_diagnostic.CS8618.severity = warning
dotnet_diagnostic.CS8625.severity = warning
```

(`warning` is temporary, for this step and the next three. Step 6 flips it to `error`.)

**Verify**:
`cd /e/repos/wt-plan-019 && dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId= --no-incremental > /e/repos/wt-plan-019-scratch/b2.log 2>&1; echo "rc=$?"; grep -E "Warning\(s\)|Error\(s\)" /e/repos/wt-plan-019-scratch/b2.log; python /e/repos/wt-plan-019-scratch/count_cs86.py /e/repos/wt-plan-019-scratch/b2.log 'Main\Features\Siege\'`
→ `rc=0`, `10 Warning(s)`, `0 Error(s)`, `total=8`, `in_fragment=8`, and the 8 locations are exactly
the list in Current state (1 CS8602 at the patch `(50,24)`, 2 CS8618 in `ActiveSiegeDefenseEvent.cs`,
5 CS8618 in `KingdomSiegeMessages.cs`). If `total` differs from `in_fragment`, or the list differs,
STOP. Do not commit yet.

### Step 3 (RED): failing test for the no-settlement guard

Create `E:/repos/wt-plan-019/TAOM.Tests/Features/Siege/SiegeCampGuardPatchTests.cs`:

```csharp
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.Siege.Hooks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Library;

namespace TAOM.Tests.Features.Siege;

/// <summary>
/// Patch8_SiegeCampGuard when the camp has no besieged settlement. The bare BesiegerCamp is
/// uninitialized, so SiegeEvent is null and the prefix sees no settlement. Debug.DebugManager is a
/// substitute so the prefix's log lines are observable (Debug.Print is a no-op without one).
/// </summary>
[TestClass]
public class SiegeCampGuardPatchTests
{
    private IDebugManager? _previousDebugManager;
    private IDebugManager _debug = null!;

    [TestInitialize]
    public void Setup()
    {
        _previousDebugManager = Debug.DebugManager;
        _debug = Substitute.For<IDebugManager>();
        Debug.DebugManager = _debug;
    }

    [TestCleanup]
    public void Cleanup() => Debug.DebugManager = _previousDebugManager;

    private static BesiegerCamp BareCamp() =>
        (BesiegerCamp)FormatterServices.GetUninitializedObject(typeof(BesiegerCamp));

    [TestMethod]
    public void Prefix_NoSettlementAndNoFrames_DefersToVanillaWithoutThrowing()
    {
        var camp1 = new MatrixFrame[0];
        var camp2 = new MatrixFrame[0];
        CampaignVec2 result = default;

        bool runVanilla = BesiegerCamp_GetSiegeCampPartyPosition_Patch.Prefix(
            BareCamp(), null!, ref camp1, ref camp2, ref result);

        Assert.IsTrue(runVanilla);
        _debug.Received(1).Print(Arg.Is<string>(m => m.Contains("No besieged settlement")),
            Arg.Any<int>(), Arg.Any<Debug.DebugColor>(), Arg.Any<ulong>());
        _debug.DidNotReceive().Print(Arg.Is<string>(m => m.Contains("patch exception")),
            Arg.Any<int>(), Arg.Any<Debug.DebugColor>(), Arg.Any<ulong>());
    }

    [TestMethod]
    public void Prefix_NoSettlementButCamp2Frames_SwapsCamp2IntoCamp1()
    {
        var camp1 = new MatrixFrame[0];
        var camp2 = new[] { MatrixFrame.Identity };
        CampaignVec2 result = default;

        bool runVanilla = BesiegerCamp_GetSiegeCampPartyPosition_Patch.Prefix(
            BareCamp(), null!, ref camp1, ref camp2, ref result);

        Assert.IsTrue(runVanilla);
        Assert.AreEqual(1, camp1.Length);
        Assert.AreEqual(0, camp2.Length);
    }
}
```

Facts this test relies on (v1.5.3 decompile, `TaleWorlds.Library.Debug.cs`):
`public static IDebugManager DebugManager { get; set; }` (`:81`), and
`Debug.Print` forwards to `DebugManager.Print(message, logLevel, color, debugFilter)` only when
`DebugManager != null` and `debugFilter & 0xFFFFFFFF00000000` is non-zero (`:163-174`); the patch
passes `17592186044416uL` (bit 44), so it is forwarded. `IDebugManager.Print` is
`void Print(string message, int logLevel = 0, Debug.DebugColor color = Debug.DebugColor.White, ulong debugFilter = 17592186044416uL);`.
`MatrixFrame.Identity` exists (`TaleWorlds.Library.MatrixFrame.cs:12`). The test project does not
enable MSTest parallelization (no `Parallelize` attribute or runsettings), so the static
`DebugManager` swap is safe.

**Verify (RED)**:
`cd /e/repos/wt-plan-019 && dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~SiegeCampGuardPatchTests" 2>&1 | tail -n 25`
→ a `Failed!` summary with `Failed: 1, Passed: 1` (numbers only; see "Reading a test summary"):
`Prefix_NoSettlementAndNoFrames_DefersToVanillaWithoutThrowing` fails on
the `Received(1)` "No besieged settlement" check (today the prefix throws an NRE at line 50 and logs
"patch exception" instead); `Prefix_NoSettlementButCamp2Frames_SwapsCamp2IntoCamp1` passes (it pins
that the guard must go after the camp-2 fallback). If the first test passes, or it fails for any
other reason (for example a `TypeInitializationException` building `BesiegerCamp`, or a compile
error), STOP and report the output.

### Step 4 (GREEN): the guard

In `Main/Features/Siege/Hooks/BesiegerCamp_GetSiegeCampPartyPosition_Patch.cs`, insert this block
between the closing `}` of the camp-2 fallback (`return true;` then `}` at lines 43-44) and the
`Debug.Print(` that says "No siege camp frames at all" (line 46):

```csharp
            // Without a settlement there is no gate to ring. Defer to vanilla explicitly (the
            // same outcome as before this guard, when the dereference below threw and the catch
            // returned true) instead of throwing on purpose.
            if (settlement == null)
            {
                Debug.Print(
                    "TAOM: No besieged settlement and no siege camp frames; deferring to vanilla",
                    0, Debug.DebugColor.Red, 17592186044416uL);
                return true;
            }

```

Change nothing else in the file (not the `MobileParty mobileParty` parameter, not the catch).

**Verify (GREEN)**:
- `cd /e/repos/wt-plan-019 && dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~SiegeCampGuardPatchTests" 2>&1 | tail -n 5`
  → `Passed!` with `Failed: 0, Passed: 2` (numbers only).
- `cd /e/repos/wt-plan-019 && dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId= --no-incremental > /e/repos/wt-plan-019-scratch/b4.log 2>&1; echo "rc=$?"; python /e/repos/wt-plan-019-scratch/count_cs86.py /e/repos/wt-plan-019-scratch/b4.log 'Main\Features\Siege\' | grep -E "^total|^in_fragment|CS8602"`
  → `rc=0`, `total=7`, `in_fragment=7`, and no `CS8602` line.
- `cd /e/repos/wt-plan-019 && wc -l Main/Features/Siege/Hooks/BesiegerCamp_GetSiegeCampPartyPosition_Patch.cs`
  → under 150 (about 77).

### Step 5: Annotate the two models and `Resolve`

1. `Main/Features/Siege/Models/ActiveSiegeDefenseEvent.cs`: give the two strings an initializer
   (every construction site sets them; the initializer only states the invariant):
   ```csharp
       public string SettlementId { get; set; } = "";
       public string DefenderFactionId { get; set; } = "";
   ```
2. `Main/Features/Siege/Models/KingdomSiegeMessages.cs`: replace the class body with
   ```csharp
   /// <summary>
   /// One kingdom's entry in siege_defense_config.json. Newtonsoft leaves a property null when its
   /// key is missing from the entry, so every message is nullable; consumers fall back to "".
   /// </summary>
   public class KingdomSiegeMessages
   {
       public string? Title { get; set; }
       public string? Body { get; set; }
       public string? AcceptButton { get; set; }
       public string? AcceptMessage { get; set; }
       public string? RewardMessage { get; set; }
   }
   ```
3. `Main/Features/Siege/SiegeDefenseService.cs` `Resolve` (`:84-94`): change the first parameter to
   `string? template` and replace `if (string.IsNullOrEmpty(template)) return "";` with
   ```csharp
           // net472's string.IsNullOrEmpty has no [NotNullWhen(false)], so spell the check out
           // for the compiler; the behaviour is identical.
           if (template is null || template.Length == 0) return "";
   ```
   Leave the rest of `Resolve` and every caller unchanged (`affirmativeText: msgs.AcceptButton` at
   `:146` passes a `string?` into a nullable-oblivious engine parameter: no warning, and the same
   value as today).
4. `TAOM.Tests/Features/Siege/SiegeDefenseServiceTests.cs:373` currently reads
   `        Assert.IsTrue(msgs.Title.Contains("{attacker}"));`. With `Title` now `string?` that line
   would add one CS8602 to the test project (which shows its warnings). Replace it with
   `        Assert.IsTrue(msgs.Title?.Contains("{attacker}") == true);`
   (same assertion: it still fails when `Title` is null or lacks the token). Change no other line
   of that file.

**Verify**:
- `cd /e/repos/wt-plan-019 && dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId= --no-incremental > /e/repos/wt-plan-019-scratch/b5.log 2>&1; echo "rc=$?"; grep -E "Warning\(s\)|Error\(s\)" /e/repos/wt-plan-019-scratch/b5.log; python /e/repos/wt-plan-019-scratch/count_cs86.py /e/repos/wt-plan-019-scratch/b5.log 'Main\Features\Siege\'`
  → `rc=0`, `2 Warning(s)`, `0 Error(s)`, `total=0`, `in_fragment=0`.
  If a new CS86xx appears in the Siege folder, fix it only by an annotation (`?` on a declaration
  that really can hold null) or an explicit `is null` check; if the only fix you can see is `!` on
  an engine getter or a behaviour change, STOP and report the location.
- `cd /e/repos/wt-plan-019 && dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~TAOM.Tests.Features.Siege" 2>&1 | tail -n 5`
  → `Passed!` with `Failed: 0` (numbers only; the existing `SiegeDefenseServiceTests`,
  `SiegeEngineAvailabilityServiceTests` and the 2 new tests).

### Step 6: Graduate Siege to `error` and prove the error tier bites

1. In `Main/Features/Siege/.editorconfig`, change all seven `= warning` to `= error`.
2. Build: `cd /e/repos/wt-plan-019 && dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId= --no-incremental > /e/repos/wt-plan-019-scratch/b6.log 2>&1; echo "rc=$?"; grep -E "Warning\(s\)|Error\(s\)" /e/repos/wt-plan-019-scratch/b6.log; grep -c "severity = error" Main/Features/Siege/.editorconfig`
   → `rc=0`, `2 Warning(s)`, `0 Error(s)`, `7`.
3. Probe (temporary; it must not be committed): with the Write tool create
   `E:/repos/wt-plan-019/Main/Features/Siege/NullableProbe.cs`:
   ```csharp
   namespace TAOM.Features.Siege;

   internal static class NullableProbe
   {
       internal static int Length(string? s) => s.Length;
   }
   ```
   Build: `cd /e/repos/wt-plan-019 && dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId= --no-incremental > /e/repos/wt-plan-019-scratch/b6-probe.log 2>&1; echo "rc=$?"; grep -E "NullableProbe.cs\([0-9]+,[0-9]+\): error CS8602" /e/repos/wt-plan-019-scratch/b6-probe.log | head -1`
   → `rc=1` and one `error CS8602` line naming `NullableProbe.cs`.
4. Delete the probe: `cd /e/repos/wt-plan-019 && rm Main/Features/Siege/NullableProbe.cs && git status --porcelain -- Main/Features/Siege`
   → no line mentions `NullableProbe.cs`. Re-run the build from item 2 → `rc=0`, `0 Error(s)`.

### Step 7: Full suite, test-project warnings, then commit B

1. `cd /e/repos/wt-plan-019 && dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= > /e/repos/wt-plan-019-scratch/t7.log 2>&1; echo "rc=$?"; grep -E "Passed!|Failed!" /e/repos/wt-plan-019-scratch/t7.log; grep -E "^\s*Failed " /e/repos/wt-plan-019-scratch/t7.log`
   → `Total:` is the number in `t0-summary.txt` plus 2; every failing name is one of the two
   allowed Animalia/Elk names from Step 0.6 (one that passed in Step 0 may fail now, or the reverse:
   another session edits that Armory); 2 skipped. Any other failing name: STOP.
2. `cd /e/repos/wt-plan-019 && dotnet build TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --no-incremental > /e/repos/wt-plan-019-scratch/b7-tests.log 2>&1; echo "rc=$?"; python /e/repos/wt-plan-019-scratch/count_cs86.py /e/repos/wt-plan-019-scratch/b7-tests.log 'TAOM.Tests\' | grep -E "^in_fragment"; cat /e/repos/wt-plan-019-scratch/tests-cs86-baseline.txt`
   → `rc=0`; the two numbers are equal (the new test file adds no nullable warnings). If the first
   is larger, remove the new warnings in `SiegeCampGuardPatchTests.cs` only; if smaller, STOP.
3. **Commit B**:
   `cd /e/repos/wt-plan-019 && git add Main/Features/Siege/.editorconfig Main/Features/Siege/Hooks/BesiegerCamp_GetSiegeCampPartyPosition_Patch.cs Main/Features/Siege/Models/ActiveSiegeDefenseEvent.cs Main/Features/Siege/Models/KingdomSiegeMessages.cs Main/Features/Siege/SiegeDefenseService.cs TAOM.Tests/Features/Siege/SiegeCampGuardPatchTests.cs TAOM.Tests/Features/Siege/SiegeDefenseServiceTests.cs && git status --porcelain`
   → exactly those seven paths staged, nothing unstaged or untracked. Run the version check from
   Git workflow. Then
   `cd /e/repos/wt-plan-019 && git commit -m "fix(siege): v2.0.30 - null-clean the Siege folder, nullable as errors" -m "<body>"`
   with a body covering: the folder's 8 nullable warnings are fixed (7 annotations, 1 guard); the
   guard replaces a thrown-and-caught NRE when a besieger camp has no settlement and no frames, with
   the same outcome (defer to vanilla); KingdomSiegeMessages is nullable because a partial JSON entry
   leaves a key null; the nested .editorconfig sets the seven ids to error for this folder; and a
   trailer `Not-tested: live siege with a settlement-less BesiegerCamp (not reachable in a normal campaign; SiegeEvent's constructor sets the settlement)`,
   then `Refs #N` only if the orchestrator gave you `N`.

### Step 8: Documentation and CHANGELOG, then commit C

1. `docs/features/siege.md` (line numbers as at `b2e387db`; each target is also named by its text.
   In the one-line quotes below, a backslash before a backtick only escapes it for this plan: write
   the backtick without the backslash):
   - "Solution Approach" list (lines 18-21): insert a new item between item 2 (line 19) and item 3
     (line 20, "If neither set of frames exists, sets `__result` to `settlement.GatePosition`"),
     and renumber the old items 3 and 4 to 4 and 5. The new item 3:
     `3. If neither set of frames exists and there is no besieged settlement to ring (the camp's \`SiegeEvent\` or its settlement is null), it logs that and returns \`true\`, deferring to vanilla. That is the same outcome the catch-all produced before, now without throwing.`
     Then change the start of the renumbered item 4 from "If neither set of frames exists, sets" to
     "Otherwise, with neither set of frames, it sets" (the rest of that line unchanged).
   - Component diagram (lines 24-36): replace line 33, the only diagram line containing
     `__result = settlement.GatePosition`, with these two lines (keep the leading spaces exactly):
     ```
       |     `-- No:  no settlement? --> log + return true (original runs)
       |              else __result = settlement.GatePosition  --> return false (skip original)
     ```
   - Key Files table (lines 41-44): add two rows after the patch row:
     `| \`Main/Features/Siege/.editorconfig\` | Sets the seven nullable warnings to error for this folder (nullable ratchet) |`
     `| \`TAOM.Tests/Features/Siege/SiegeCampGuardPatchTests.cs\` | Unit tests calling the Prefix directly: no-settlement guard and camp-2 fallback |`
   - Replace the three-sentence paragraph under `## Tests` (line 52, starting "No unit tests exist for
     the Siege feature") with:
     `\`TAOM.Tests/Features/Siege/SiegeCampGuardPatchTests.cs\` calls the prefix directly on an uninitialized \`BesiegerCamp\` with a substituted \`Debug.DebugManager\`: the no-settlement, no-frames path defers to vanilla without throwing, and the camp-2 fallback still swaps frames. \`SiegeDefenseServiceTests\` and \`SiegeEngineAvailabilityServiceTests\` cover the siege-defense service.`
   - Under `## Changelog`, add a line after the existing one:
     `- 2026-09-23: the folder is null-clean and its \`.editorconfig\` sets the nullable warnings to errors (plan 019); explicit no-settlement branch in the prefix.`
     (Use the date you do the work if it is not 2026-09-23.)
2. `docs/ai-includes/code-quality.md`: after the closing code fence of the "Nullable Reference
   Types" section (line 428, just before `### LINQ Best Practices`), insert:

   ```markdown

   **How nullable is enforced (the ratchet).** `Directory.Build.props` enables nullable for every
   project. For `Main/` and `Dependencies/` the root `.editorconfig` sets the seven main nullable
   ids (CS8600, CS8601, CS8602, CS8603, CS8604, CS8618, CS8625) to `none`; a folder that is
   null-clean carries its own `.editorconfig` setting them to `error`, and the nearer file wins.
   Graduated folders: `Main/Features/Siege`. Never add these ids back to a csproj `<NoWarn>`: the
   compiler's `/nowarn` beats every `.editorconfig`, which would silently turn off every graduated
   folder. On net472 `string.IsNullOrEmpty` does not narrow (no `[NotNullWhen]` in the reference
   assemblies), so write `x is null || x.Length == 0` where the compiler must see the check. This
   supersedes the "deliberately suppressed project-wide" note in
   `docs/reviews/rca-banner-bearers-2026-07-16.md` for graduated folders.
   ```
3. `CHANGELOG.md`: if the first `## ` heading is today's date, add the entry directly under it;
   otherwise add a new `## <today, YYYY-MM-DD>` heading above the first one. Entry:

   ```markdown
   ### fix(siege): v2.0.30 - nullable warnings graduate folder by folder, Siege first

   The seven main nullable warnings (CS8600 to CS8604, CS8618, CS8625) were thrown away by
   `<NoWarn>` in both production csproj files, and `/nowarn` beats any `.editorconfig`, so no
   folder could turn them back on (2,028 were hidden at `b2e387db`). They now live in the root
   `.editorconfig` as `none` for `Main/` and `Dependencies/` (build output unchanged; test-project
   warnings untouched), and `Main/Features/Siege` is the first folder at `error`.

   - **Siege is null-clean**: the siege-camp guard (Patch8) now has an explicit branch for a camp
     with no settlement instead of throwing and catching its own NRE (same outcome, defer to
     vanilla); `KingdomSiegeMessages` is nullable because a partial JSON entry leaves a key null.
   - **New tests**: `SiegeCampGuardPatchTests` (2).
   - **Procedure for the next folder**: `docs/ai-includes/code-quality.md`, "How nullable is
     enforced".
   ```
   Substitute the version you read in the Git workflow step if it is not `v2.0.30`.
4. Verify:
   - `cd /e/repos/wt-plan-019 && python tools/lint_docs.py --quick --summary > /e/repos/wt-plan-019-scratch/lint8.txt 2>&1; echo "rc=$?"; grep "dead_links:" /e/repos/wt-plan-019-scratch/lint0.txt /e/repos/wt-plan-019-scratch/lint8.txt`
     → `rc=0` and the two `dead_links:` numbers are equal (Step 0.8 baseline versus now). If the
     second is larger, your edit added a dead link: run
     `cd /e/repos/wt-plan-019 && python tools/lint_docs.py --quick` to see it, fix it in your own
     lines, and re-run.
   - `cd /e/repos/wt-plan-019 && git diff -U0 -- docs/features/siege.md docs/ai-includes/code-quality.md CHANGELOG.md | python -X utf8 -c "import sys; print(sum(1 for l in sys.stdin if l.startswith('+') and not l.startswith('+++') and (chr(0x2013) in l or chr(0x2014) in l)))"`
     → `0` (no em or en dash on an added line).
5. **Commit C**:
   `cd /e/repos/wt-plan-019 && git add docs/features/siege.md docs/ai-includes/code-quality.md CHANGELOG.md && git status --porcelain`
   → exactly those three staged, nothing else listed. Run the version check from Git workflow. Then
   `cd /e/repos/wt-plan-019 && git commit -m "docs(nullable): v2.0.30 - per-folder nullable ratchet procedure" -m "<body>"`
   (body: where the ratchet is documented and that Siege is the first graduated folder; `Refs #N`
   only if the orchestrator gave you `N`).

### Step 9: Final verification

- `cd /e/repos/wt-plan-019 && git log --oneline $(cat /e/repos/wt-plan-019-scratch/base.txt)..HEAD`
  → exactly 3 commits (A, B, C), subjects as above.
- `cd /e/repos/wt-plan-019 && git diff --stat $(cat /e/repos/wt-plan-019-scratch/base.txt)..HEAD`
  → only the in-scope paths from Scope (13 files, or 14 with the Step 1 fallback).
- `cd /e/repos/wt-plan-019 && git status --porcelain` → empty.
- `cd /e/repos/wt-plan-019 && git log --format=%B $(cat /e/repos/wt-plan-019-scratch/base.txt)..HEAD | grep -ci "co-authored-by"` → `0`.

## Test plan

- New file `TAOM.Tests/Features/Siege/SiegeCampGuardPatchTests.cs`, class `SiegeCampGuardPatchTests`:
  - `Prefix_NoSettlementAndNoFrames_DefersToVanillaWithoutThrowing`: the regression. RED before Step 4
    (the prefix throws its own NRE and logs "patch exception"), GREEN after (logs "No besieged
    settlement", returns `true`).
  - `Prefix_NoSettlementButCamp2Frames_SwapsCamp2IntoCamp1`: characterization; passes before and
    after, and fails if the guard is placed above the camp-2 fallback.
  - Cells covered for the prefix with a null settlement: (no camp-1 frames, no camp-2 frames) and
    (no camp-1 frames, camp-2 frames). The camp-1-present branch returns before `settlement` is read
    and is untouched. The gate-ring branch needs a real `Settlement` (`GatePosition`), which is not
    constructible without a campaign: structurally untestable here.
- Pattern: `TAOM.Tests/Features/FiefGranting/FiefGrantingBehaviorCaptureGateTests.cs` (uninitialized
  engine objects), `TAOM.Tests/Features/BannerColorPersistence/Clan_UpdateBannerColorsAccordingToKingdom_PatchTests.cs`
  (direct `Prefix` calls).
- Annotation changes (models, `Resolve`) change no behaviour; the existing
  `TAOM.Tests/Features/Siege/SiegeDefenseServiceTests.cs` (including the three `GetMessages_*` tests)
  covers them. Its line 373 is rewritten null-safely (Step 5 item 4) with the same assertion.
- The compiler is the test for the ratchet itself: Step 2 (8 warnings appear), Step 5 (0), Step 6
  (the probe produces `error CS8602`).
- `Not-tested:` a live siege with a settlement-less `BesiegerCamp` (not reachable in a normal
  campaign).
- Verification: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` → Step 0's total
  plus 2, only the known Animalia/Elk failures, including the 2 new tests passing.

## Done criteria

ALL must hold (run each command as `cd /e/repos/wt-plan-019 && <command>`):

- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId= --no-incremental` exits 0
      with `2 Warning(s)` and `0 Error(s)`, and `count_cs86.py` on its log prints `total=0`.
- [ ] `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` shows total = Step 0 total + 2,
      failing names only from the two allowed Animalia/Elk names, and `SiegeCampGuardPatchTests`
      2 of 2 passing.
- [ ] `python tools/lint_docs.py --quick --summary` prints the same `dead_links:` number as Step 0.8.
- [ ] `grep -c "8600" Main/TAOM.csproj Dependencies/TAOM.Dependencies.csproj` prints `:0` for both.
- [ ] `grep -c "severity = none" .editorconfig` prints `7` (or, with the Step 1 fallback,
      `Main/.editorconfig` and `Dependencies/.editorconfig` each print `7`).
- [ ] `grep -c "severity = error" Main/Features/Siege/.editorconfig` prints `7`.
- [ ] The TAOM.Tests `in_fragment` count after the change equals the Step 0 baseline file.
- [ ] `test ! -e Main/Features/Siege/NullableProbe.cs && echo gone` prints `gone`.
- [ ] `git diff --stat <BASE>..HEAD` lists only in-scope paths; `git status --porcelain` is empty.
- [ ] Three commits, subjects at most 72 characters, no `Co-Authored-By`.

## STOP conditions

Stop and report (do not improvise) if:

- The drift check prints anything and the live files differ from the Current state excerpts.
- Step 0's baseline build shows any CS86xx, or anything other than `2 Warning(s)`, or any test
  fails whose name is not one of the two Animalia/Elk names, or `git status --porcelain` is not
  empty after the baseline builds (Step 0.7).
- The worktree's and the main tree's `Main/_Module/SubModule.xml` versions differ at a commit (the
  subject hook checks the main tree's; see Git workflow).
- After Step 1 (and its one sanctioned fallback) the Main build still shows CS86xx, or the
  TAOM.Tests nullable count changed: the suppression move is not output-neutral.
- Step 2 shows a Siege warning list that differs from the 8 in Current state, or any CS86xx outside
  `Main\Features\Siege\` (the nested file leaked or the root block does not apply).
- Step 3's first test passes before the guard exists, or fails for a reason other than the
  "No besieged settlement" `Received(1)` check (for example the bare `BesiegerCamp` cannot be
  created, or `Debug.DebugManager` cannot be set).
- A Siege warning can only be silenced with `!` on an engine value, a `#pragma warning disable`, a
  new `<NoWarn>`, or a behaviour change. None of those is allowed here.
- The fix appears to need `Main/IoC.cs`, `Main/SubModule.cs`, `Directory.Build.props`,
  `TAOM.Tests/TAOM.Tests.csproj`, or any file outside Scope.
- The Step 6 probe does NOT fail the build (the `error` tier is not in effect).
- A step's verification fails twice after a reasonable fix attempt, or any hook blocks an action.

## Maintenance notes

- **The ratchet procedure (one folder per change).** To graduate folder `X`:
  1. Create `Main/.../X/.editorconfig` with the seven ids at `warning` (copy Siege's file).
  2. Build with the counting command and `count_cs86.py <log> 'Main\...\X\'`; `total` must equal
     `in_fragment` (nothing leaks outside `X`).
  3. Fix every location: `?` on declarations that really hold null, `is null` checks where the engine
     or a config file can hand back null (with a test for any guard that changes control flow),
     `= ""` or a collection initializer where every constructor path sets the value. Never `!` on an
     engine getter, never `#pragma warning disable`, never `required` (unavailable in `Main` at
     `LangVersion` 10.0; `Dependencies` compiles with `preview` and could use it, but keep one style
     so a graduated folder reads the same on both sides). `= null!` only on a
     field assigned outside the constructor before first use (an IoC-injected static seam, for
     example), with a one-line comment saying who assigns it.
  4. Flip the file to `error`, rebuild (`0 Error(s)`), run the suite, commit
     `fix(<scope>): v<ver> - null-clean <X>, nullable as errors`, and add `X` to the "Graduated
     folders" list in `docs/ai-includes/code-quality.md`.
  Never leave a folder at `warning`: the shipped build has 2 warnings in total, and a standing
  warning tier becomes noise within weeks.
- **Suggested next folders** (planning measurements at `b2e387db`, warnings in files):
  `Dependencies/Foundation` 3 in 2 (`PatchShield.cs:313`, `SubModuleConstructionGuard.cs:248,253`;
  the crash-containment layer, but its warnings are annotation debt: the callee null-checks);
  `Features/TroopWeight` 10 in 4 (11 RCAs; all flow or annotation debt); `Features/CrashReport` 8 in
  4 (`CrashBundleWriter.cs:120-124` are `c.Logs.X` after a `c.Logs?.X` `IsNullOrEmpty` check, the
  net472 narrowing gap); `Features/PlayerSwitcher` 6 in 2; `Features/Arena` 21 in 5. Large folders
  last, split by file where possible: `Adapters` 216 (the engine-null boundary), `Features/Enlistment`
  145, `Features/CulturalFeats` 140 (131 are CS8618 on registered-feat fields).
- **A possible helper, deferred**: a `TAOM.Core` `static bool IsNullOrEmpty([NotNullWhen(false)] string? s)`
  (the `Nullable` polyfill provides the attribute) would remove the net472 narrowing gap in one
  place. Not added here: one call site does not justify it (simplicity criterion). Revisit when a
  graduation hits it in three or more places.
- **A gate, deferred**: once three or more folders graduate, a `tools/tests` check that every
  `Main/**/.editorconfig` below the root sets all seven ids to `error` stops a folder quietly
  downgrading. Not worth it for one folder.
- **Deferred gameplay decision**: with no besieged settlement and no camp frames, the prefix still
  defers to vanilla, which then indexes an empty array (the crash the feature exists to prevent).
  In a normal campaign `SiegeEvent`'s constructor sets the settlement, so this is theoretical. If it
  ever appears in a log ("No besieged settlement and no siege camp frames"), choose a fallback (for
  example keeping the party at its current position) as a separate, smoke-tested change. Note also
  that `docs/reviews/rca-field-commission-reset-equipments-2026-08-20.md:16` calls vanilla a safe
  default for Patch8's catch-all; on this path it is not.
- **Hotfix escape**: a graduated folder at `error` can block an unrelated urgent build. The escape is
  a one-line severity edit in that folder's `.editorconfig`, reviewable in the diff; never re-add
  the ids to `<NoWarn>` (that disables every graduated folder at once).
- **Interaction with plan 010** (CI on hosted Windows): it edits the Reference Include/Exclude lines
  of the same two csproj files (`Main/TAOM.csproj:25,35`, `Dependencies/TAOM.Dependencies.csproj:16-17`),
  not line 9, so a merge should be clean; once CI compiles C#, a nullable error in a graduated folder
  fails CI, which is the point.
- **Reviewer focus** (`/deep-review` before merge): (1) the root block really covers only `Main/` and
  `Dependencies/` (TAOM.Tests count unchanged); (2) the guard sits after the camp-2 fallback and
  returns `true`; (3) `KingdomSiegeMessages` consumers still behave for a partial JSON entry
  (`Resolve` returns `""`, `AcceptButton` null reaches `InquiryData` exactly as before); (4) the
  static `Debug.DebugManager` swap in the test is restored in `[TestCleanup]`.
