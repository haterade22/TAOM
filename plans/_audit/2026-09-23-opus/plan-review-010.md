# Cold review: plan 010 (CI on hosted Windows)

Reviewed file: `plans/010-ci-on-hosted-windows.md` (1,424 lines), against `.claude/skills/improve/references/plan-template.md` "Quality bar", with every "Current state" excerpt opened at `b2e387db` (`git show b2e387db:<path>`). HEAD is `4b5662b2`; Part A of the drift check prints nothing today (run).

**Verdict:** one blocking defect. It is mechanical and cheap to fix. Everything else is either verified correct or non-blocking polish. After the fix, a weak executor can run this plan.

## Blocking

### B1. The "byte-identical references" gate compares a volatile timestamp (lines 466-471, 653-657, 727-728, 1091-1092, 1113)

The plan's equivalence proof diffs the raw JSON of `dotnet msbuild <proj> -getItem:Reference`, and says to STOP when the diff is not `IDENTICAL`. That JSON includes the well-known metadata `ModifiedTime`, `CreatedTime` and **`AccessedTime`** for every file reference. On this desktop, NTFS last-access updates are on (`fsutil behavior query disablelastaccess` prints `2 (System Managed, Last Access Time Updates ENABLED)`).

What I ran: I evaluated `Main/TAOM.csproj -getItem:Reference` twice, with no project change between the runs. In between, the only thing that happened was a `cmp` that read two game DLLs. The diff was not empty:

```
<         "AccessedTime": "2026-09-23 21:14:12.4020919",
>         "AccessedTime": "2026-09-23 21:39:37.8081706",
```

Every build and test run between the Step 0.6 snapshot and the Step 3, Step 4.2 and Done-criteria diffs reads the game's TaleWorlds DLLs. That covers Step 0.7, Step 2 and Step 9. So each of those diffs will show `AccessedTime` changes even when install mode is exactly equivalent. A weak executor then hits the STOP at line 1113 ("Step 3 or 4's `diff` is not `IDENTICAL` ... Do not 'fix' the baseline") and cannot finish. The Done criterion at 1091 cannot pass either.

**Fix:** drop the volatile keys before diffing. One way is a tiny helper in `E:\repos\wt-010-logs\` (written with the Write tool) that keeps only `Identity`, `HintPath`, `Private`, `Aliases` and `DefiningProjectFullPath` per item (or deletes every `*Time` key), and diffs the normalized output. Apply it in 0.6, 3, 4.2 and the Done criteria, and say explicitly that `*Time` fields are excluded because the file system updates them on read.

## Non-blocking

1. **Step 6 classification misses non-TaleWorlds dependency DLLs (lines 211-216, 791-804).** In install mode, the local test bin gets five game-bin DLLs besides TaleWorlds: `System.Management.dll`, `System.Numerics.Vectors.dll`, `Steamworks.NET.dll`, `GalaxyCSharp.dll` and `StbSharp.dll`. I confirmed each one byte-identical to the game's copy with `cmp`. They arrive as RAR dependencies of the `Private=True` TaleWorlds references. The audit's measurement swapped only the 50 TaleWorlds DLLs, so those five were still in its bin. A RefAsm build will not copy them. If any unit test loads one of them, the failure is a `FileNotFoundException` for an assembly that rule (a) does not list, so the plan says STOP. That is safe, and the chance looks low: no test names `GpuDisplayCollector`, and `PolygonWidget` uses `SNV::Vector2` only as a local at `:728`. Still, add `System.Management`, `System.Numerics.Vectors`, `Steamworks.NET` and `GalaxyCSharp` to rule (a) or to an explicit STOP line, so the executor is not left guessing.
2. **Step 0.7's pass condition is a judgment (lines 487-491).** "Only Armory-reading tests" leaves the executor to decide what reads the Armory. Make it checkable: every failing test's class must be a `LiveInstall` row in the manifest, or be one of the two named tests.
3. **Step 1 has no verification command (line 498).** The template wants a command on every step. Suggest `grep -n '^- \*\*Issue\*\*' <plan>`, which prints a line containing either `https://github.com` or `create before`.
4. **Step 11's Verify compares against a baseline it never recorded (line 1064).** It checks that `git -C E:/repos/TAOM diff --cached --name-only` is "unchanged from before your commit", but no earlier step records that output. Add the recording to Step 11.4.
5. **Step 6 (b) classification is partly judgment (lines 798-800).** "The message shows the test needs vanilla method IL..." cannot be decided mechanically. It is bounded by the 10-addition cap and the STOP, so acceptable, but a list of example failure messages from the audit's 29 would help.
6. **Scope line numbers after plan 008 (line 384).** `TAOM.Tests/TAOM.Tests.csproj:35` is correct at `b2e387db`, but plan 008 inserts an `ItemGroup`, which can shift it. Step 3.4 correctly says "the TaleWorlds reference line" without a number; say the same in Scope.
7. **Exemplar mismatch (lines 314-318).** The rule text says repo-file tests use `RepoPaths`. The named exemplar, `BundledDependencyManifestTests`, does not: it builds `RepoRoot` from `AppDomain.CurrentDomain.BaseDirectory` at `:27-28`. The plan's own test code uses `RepoPaths` correctly, so this only misleads if the executor models on the exemplar. Also, `:30-31` are the path properties; the `XDocument.Load` and `Descendants("PackageReference")` calls are at `:50-51`.
8. **The commit gates are listed incompletely (lines 433-439).** `block-broad-git-add.sh` and `block-dangerous-git.sh` also run on Bash. I checked both against this plan's commands (`git add $(cut ... | sort -u)`, `git commit -F`, `git -C ... worktree add`), and none of them triggers. There is no action, but naming them would stop a surprise ask from looking like a deny.

## Excerpt check at `b2e387db` (question 3)

Every excerpt and line citation matched. I checked these:

- **`build.yml`**: 278 lines; triggers at `:3-8`; `check-build-config` `:15-25` (warning at `:24`); `validate-xml` `:27`; `hook-harness` `:159`; `python-tests` `:192-236`; `build` `:238-278`; `if:` at `:259`; comment at `:248-252`.
- **`Main/TAOM.csproj`**: 197 lines; `:17-24`, `:25`, `:35`, `:40`, `:44`, `:48`, `:52`, `:56`, `:89`, `:95-98`, `:137-195`, `:146-158`, `</Project>` at `:197`.
- **`Dependencies/TAOM.Dependencies.csproj`**: 126 lines; `:12` `LangVersion preview`; `:16-17`.
- **`TAOM.Tests/TAOM.Tests.csproj`**: 42 lines; `:35`, with `Private True` at `:37`.
- **`Directory.Build.props`**: 43 lines; `:37-41`.
- **`GameAssemblies.cs`**: `:33-34`, `:36-79`, `:111-122`, `:124-131`.
- **Other source and tests**: `PolygonWidget.cs:1`; `EnlistmentTickBindingTests.cs:17-18`; `PlayerClanLeadershipService.cs:57,96-100`; `NativeSkinFixesInstallerTests.cs:33,42`.
- **Docs and config**: `.ai/policy.md:78-79`; `.ai/verification.md:6-7` and `:21-23` (the text is verbatim); `.gitignore:75,82`; `tests.md` at 3,914 bytes with a `## Test Organization` heading; `issue-triage-2026-08-08.md:297`; `feature-map.md:117`; `troop-skill-balance.md:224`.
- **Build package**: `Basic.targets` `:12-16`, `:35-37`, `:47`, `:53`, `:64`.
- **SDK**: `FrameworkReferenceResolution.targets:514-531`; `Microsoft.NET.Sdk.props:124-125`.
- **NuGet props**: `nuget.g.props:7,117`.
- **Counts**: 290 `BindingVerification` attributes. The manifest has 139 rows over 120 files: 100 `RequiresGame` in 92 files, 29 `RequiresGameIL` and 10 `LiveInstall`.
- **Tagger dry run**: on `git archive b2e387db TAOM.Tests`, Appendix B printed `added=139 present=0 errors=0`, then `added=0 present=139 errors=0`. The `E:\Steam` loop leaves only `NativeSkinFixesInstallerTests.cs` untagged.
- **`git grep` for `GameFolder|HintPath|GameBinariesFolder`**: matches only `GameAssemblies.cs` and `TAOM.Tests.csproj`.
- **Environment**: `BANNERLORD_GAME_DIR` is set, `dotnet` is 10.0.401, `pwsh` is 7.6.6, PyYAML and `cygpath` are present. The Step 4 `global-packages` path derivation yields a clean path with no stray CR.
- **Output folder**: the test output is `TAOM.Tests/bin/Debug/net472`, matching the CI `refasm-game` path.

Mismatches: none in the excerpts. The only imprecise citation is item 7 above.

## Question 4 checks

| Check | Result |
|---|---|
| TDD order | Pass: Step 2 (RED, both tests fail) precedes Step 3 (GREEN). No production C# changes. |
| Issue-first | Pass: Status line 50 plus Step 1 (orchestrator creates it; #421 is noted). |
| Binding ADRs named | Pass: ADR-002/003/004/005/007/008 and `tests.md`, each with a one-line summary (lines 308-329). |
| Single-owner files | Pass: the `TAOM.csproj` and `TAOM.Dependencies.csproj` edits are enumerated character for character; `IoC.cs`, `SubModule.cs` and `Directory.Build.props` are out of scope with a STOP (lines 398-399, 1119-1120). `config-protection.sh` protects `Directory.Build.props`, which the plan does not touch. |
| STOP conditions specific | Pass: they name NU1101/NU1102, CS errors, STUB, Skipped > 0, the 10-addition cap and gate denials. B1 makes one of them fire falsely. |
| Done criteria machine-checkable | Mostly: the diff criterion (1091) is broken by B1, and the rest are commands with outputs. |
| Planned-at SHA and drift paths | Pass: `b2e387db`. Part A covers the csproj/props/workflow/doc/rule files plus read dependencies; Part B covers the 120 manifest files. `CHANGELOG.md` and the two new files are sensibly excluded. |
| Non-deploying flags | Pass: every `dotnet build`/`test`/`msbuild` carries `-p:DisableModuleCopy=true -p:ModuleId=`, and `./build.ps1` is forbidden. |

## Question 5

A Python scan found no U+2014 or U+2013 anywhere in the plan. There are no secrets; the only key-like strings are the public `PublicKeyToken=b03f5f7f11d50a3a` assembly identities.
