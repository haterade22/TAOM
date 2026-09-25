# RCA: plan 010 review, C# on hosted Windows runners (2026-09-24)

## Top-line

`/deep-review` of plan 010 (`2ca0805b..b8c00045`, branch `improve/010-ci-on-hosted-windows`) ran
six lenses: Standards, Engine compatibility, Efficiency, Completeness, Data flow and Design. The
XML and Tooling lenses were not launched. A Codex adversarial review (gpt-6-astra, ultra) ran beside
them. Together they confirmed 9 defects: 0 HIGH, 1 MED and 8 LOW. Two findings were false positives
or already handled. The mechanism held: install mode evaluates to byte-identical references in all
three projects, the RefAsm build compiles with 0 errors and the same 2,271 warnings as the
executor's replay, and the hosted steps replay green here after the fixes (unit 8,184 executed,
gate 338 of 338).

The one finding that reaches past the change: **the new guard test proved the game version, while
its name, the workflow header and the CHANGELOG all claimed it proved the Steam build.** BUTR
publishes several builds per game version (Agent 2 counted 35 of 51 game versions on nuget.org
with more than one; not re-counted here), and the pin `v1.5.3` cannot tell them apart. A same-label hotfix would have left CI compiling and
binding-checking against the old build with nothing red. The fix compares the version's fourth
part with `TaleWorlds.Library.ApplicationVersion.DefaultChangeSet`, a `const` the compiler inlines,
so it fails locally when the install moves.

All nine are fixed on the branch. Three improvement proposals that change behaviour are left for
Mike (listed in the deep-review report).

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | MED | `BannerlordRefAsmVersion_PinnedGameVersion_IsTheSameGameBuild` asserted only `StartsWith("1.5.3.")`; `csharp.yml:6` and the CHANGELOG said "the same Steam build". Found by Agent 2 F1, Agent 5 (follow-up) and Codex P3 #1. | Logic error (test oracle weaker than its name) | The plan prescribed the prefix assertion, and the pin file holds only the version label. Nobody asked whether BUTR ever ships two builds of one label. | Fixed: the test compares the fourth part with `ApplicationVersion.DefaultChangeSet` (122374 in the installed DLL, verified with `taom-src`). RED shown by setting the targets to `122375`: the old assertion passed, the new one failed. Lesson in testing-qa. |
| F2 | LOW | The reference guard read only `Include` and `Exclude` for `$(GameFolder)`, so `<Reference Include="X"><HintPath>$(GameFolder)\...</HintPath>`, the usual Bannerlord spelling, passed and would break only on CI. An import with a `Condition` also counted. Found by Agent 1 L1, Agent 4, Agent 6 and Codex P3 #1. | Test gap | The guard was written from the current csproj spelling (`%(Identity)` HintPaths), not from the spellings it must reject. | Fixed: the whole element is searched for four install properties, and only an unconditional import counts. A fixture test failed first (RED), then passed; a RefAsm-only import condition made the repo test fail by mutation. Same lesson as F1. |
| F3 | LOW | The `csharp.yml` header said it "compiles every C# project", runs "every test that can run without Bannerlord", "for every push and pull request". It builds three projects, drops class-tagged runnable methods and 24 game-gated skips, and triggers on two branches. Found by Agent 1 L2, Codex P3 #2 and notes from Agents 2 to 5. | Other: doc claim wider than the code | The header was written from the goal, not read back against the `on:` block and the filters. This repeats the #647 lesson "A CI step covers only the branches its workflow's `on:` block names". | Fixed. Repeat offender: the existing build-tooling-workflow lesson covers it, so no new lesson. |
| F4 | LOW | `tests.md` gave one CI failure signature (an NRE from a TaleWorlds frame). The executor's own first run (`6a.log`) also failed with `FileNotFoundException` for module assemblies, a `TypeInitializationException` wrapping one, and an NRE from a TaleWorlds attribute constructor. The rule also implied `RequiresGame` affects the gate, which it does not. Found by Agent 2 F2, Agent 4 and Agent 5 traces 5 and 6. | Other: rule text narrower than the measured run | The rule was written from the expected failure; the log that showed the others was not re-read. | Fixed: every signature is listed, and the rule says `RequiresGame` only leaves the unit step. The CHANGELOG line "103 classes that execute engine code" is corrected. Lesson in testing-qa. |
| F5 | LOW | The `.ai/verification.md` no-game recipe could not work as written: a restore without `-p:TaomGameRefs=RefAsm` downloads no BUTR package, the unfiltered `managed-tests` row runs `RequiresGame` tests on stubs, and a machine with the game mixes real module DLLs in. The RefAsm error then said "Restore first", which repeats the failing step. Found by Agent 5 traces 8 and 9, and Agent 6. | Other: recipe never executed | The executor ran the CI commands, never the documented reviewer recipe; the plan's `NOGAME` prefix lived only in the plan. | Fixed: the doc names the property on restore, build and test, points at the workflow's filters and says to unset the game variables; the error says to restore with the property. Lesson in build-tooling-workflow. |
| F6 | LOW | The missing-install error named only `bin\Win64_Shipping_Client`, while `Directory.Build.props:41` also accepts `bin\Gaming.Desktop.x64_Shipping_Client` for `BANNERLORD_GAME_DIR`. Found by Agent 2 F3. | Other: message narrower than the code | The message was written from the override rule (`:37`), not the game-dir rule. | Fixed. One-off. |
| F7 | LOW | The gate step is titled "a skip fails", but `MapInconclusiveToFailed` covers Inconclusive only; an `[Ignore]`d binding check is NotExecuted and passed. Found by Agent 5 trace 11. | Missing guard | The runsettings were taken as the whole skip story. | Fixed: the step fails when `total` differs from `executed` (338 of 338 in the replay). The `[Ignore]` case itself was not run (UNVERIFIED). One-off. |
| F8 | LOW | The CHANGELOG said CI compiles C# "again" (no run ever compiled it), that the workflow runs on `bannerlord-1.4.5` (it will not until the file is ported there) and linked no issue although #421 (open) describes this gap. Found by Agent 1 L4, Agent 4 and Agent 5 trace 13. | Other: doc claim | The entry was written from intent; a push to `bannerlord-1.4.5` runs the workflows in that commit, and this file is not on that branch yet. | Fixed: "again" removed, the 1.4.5 wording corrected, #421 cited as the C# half (the Python half stays open). |
| F9 | LOW | `feature-map.md` still sent CI readers to `build.yml` only and did not name `GameReferences.targets`. Found by Agent 4. | Other: completeness | The plan deferred the rows because another session was editing the file; that session had finished by review time. | Fixed: both rows updated. One-off. |

## Root-cause pattern

F1, F2, F3, F4 and F8 share one cause: **text and tests written from what the change intends,
not from what the evidence shows.** The executor had the proof in hand in each case (the nuget.org
build list, the `6a.log` failure types, the `on:` block, the #421 issue) and wrote the claim
before reading it back. This is the `evidence-over-claims.md` §C facet 1 trap ("writing the summary
before its evidence exists"), applied to a test name and a rule rather than a CHANGELOG.

## Why each agent missed these

The lenses were the review; this section records which lens caught what and why the others did
not. The executor's own gates (Standards checks, TDD, the byte-identical reference snapshots) were
aimed at parity and so could not see an overclaim.

- **Agent 1 (Standards)** caught F2, F3 and F8's issue link. Its checklist has no engine-identity
  check, so F1 fell to Agent 2; its prose checks cover dashes and budgets, not whether a failure
  list matches a log (F4).
- **Agent 2 (Engine compatibility)** caught F1, F4 and F6 by comparing nuspec tags, the installed
  DLL and nuget.org's build list. It does not read workflow triggers or reviewer recipes (F5, F7).
- **Agent 3 (Efficiency)** found no defect, correctly: none of the nine is a cost problem.
- **Agent 4 (Completeness)** caught F2, F4, F8 and F9. It checks tests, docs and issues, not the
  engine build identity (F1) or the gate's skip semantics (F7).
- **Agent 5 (Data flow)** caught F4, F5, F7 and F8 by tracing each value to its consumer. It saw F1
  as a follow-up ("pin granularity") rather than a finding, because it treated the plan's prefix
  contract as the intended one.
- **Agent 6 (Design)** caught F2 and F5 as proposals. Its lens judges simplicity, not claims.
- **Codex** caught F1, F2 (partly) and F3 as P3 observations and disputed or left unverified all
  ten Known Suspects, with reasons. It missed F4 to F9: it reviewed through git objects only and
  did not read the executor's logs or the reviewer recipe.

## Feedback memories to codify

- **testing-qa:** a pin or guard test proves the identity its name claims, and is tested against
  the spellings it must reject (F1, F2).
- **testing-qa:** a rule's list of failure signatures comes from the run log (F4).
- **build-tooling-workflow:** a documented recipe for an opt-in build mode is run as written,
  restore included, before it is committed (F5).
