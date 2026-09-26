# RCA: Shader Pre-compilation parked again (2026-09-25)

## Top-line

The change comments out the main-menu "Pre-compile Shaders" option, the `Patch21_ShaderPrecompilation`
wiring in `SubModule.cs` and both MCM attribute stacks in `TaomSettings.cs`, following the 2026-08-20
park (`5ae02f08`), on `bannerlord-1.5.x` and `bannerlord-1.4.5`. All six applicable deep-review lenses
ran as agents (wave 1: Standards, Engine compatibility, Data flow, Completeness; wave 2 after the Opus
5.5 limit reset: Efficiency, Design), followed by one convergence pass on the fixes. None found a
runtime or compatibility defect: 15 data flows traced with no gap, every API in the commented block
verified on the installed v1.5.3, and Efficiency found a net gain (a postfix that ran every frame is
gone). Every confirmed finding was LOW, about comments or docs that still described the feature as
live or stated engine and MCM behaviour wrongly. All are fixed in the working tree.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | LOW | `docs/modding/file-catalogue.md:134` and `docs/INDEX.md:162` still read as live | Reversal sweep too narrow | The doc list came from the older park `5ae02f08`; the re-enable `1e654021` had rewritten more docs than that park touched | Lesson "Reversing a toggle again" in `lessons/build-tooling-workflow.md` |
| 2 | LOW | `docs/features/battle-load-diagnostics.md:287` presented the walk as available | Reversal sweep too narrow | Written by the second #560 commit `1700156b`, so even a sweep of `1e654021` alone would have missed it; the line names neither the setting nor the menu option | Same lesson: every commit since the template, plus a slug and issue-number grep |
| 3 | LOW | `SubModule.cs` Patch21 comment, the registry and the feature doc's efficiency line said the postfix costs one check "per loading-screen frame"; vanilla calls `LoadingWindowViewModel.Update` every frame from `GauntletDefaultLoadingWindowManager.OnLateTick`, loading screen or not | Engine claim copied, not re-verified | The sentence came from `5ae02f08` and the re-enable's registry text; neither was checked against the engine. The first fix missed the feature doc's copy (convergence pass) | All three corrected against the v1.5.3 decompile; same lesson |
| 4 | LOW | `TaomSettings.cs` park comment did not say what MCM does to attribute-less properties; the first fix then overstated it as unconditional | Consequence of the park unstated, then overstated | MCM saves only settings with changes (`ModOptionsVM.ExecuteDoneInternal`), so stored values drop only after a save that changes some TAOM setting | Comment and feature doc banner now state the condition |
| 5 | LOW | Feature doc body read as live, posture-test claim vacuous while parked, #287 and #560 listed OPEN though both are closed; two history banners restated the Changelog | Doc drift | The banner was rewritten; the body, the issue list and the older banners were not re-read | Framing sentence, test line, issue rows fixed; the two redundant banners deleted (Design P2) |
| 6 | LOW | `main-menu-customizer.md` (lines 5, 42, the diagram) and `feature-map.md:38` described the guard as running or placed the MCM attributes in `SubModule.cs` | Doc precision | Rows were annotated where the option was named, not where the behaviour was described | Fixed |
| 7 | LOW | Re-enable notes said Patch21 is "near the top of OnSubModuleLoad" (`SubModule.cs` and the feature doc's park-history banner) | Copied location claim | Copied from `5ae02f08`, where it was already wrong | "in OnSubModuleLoad"; the banner is deleted |
| 8 | LOW | `SubModule.cs` field comment "stays null while parked" would go stale on un-park | Comment true in one state only | The re-enable list did not include it | Reworded to hold in both states |

## Root-cause pattern

Findings 1 to 3 and 7 share one cause: the re-park was built by replaying an older template, and the
template's text was trusted as current. The docs the intervening re-enable commits had touched, and
the engine facts the old comment asserted, were never re-derived. This repeats the 2026-07-01 lesson
("A structural refactor's leftover-reference sweep must cover living docs") and the 2026-09-24
CHANGELOG retirement (C5 in `rca-changelog-at-release-2026-09-24.md`), so the rule now also sits on
the always-loaded "Parked features" row of the orientation trap index (Design P1).

## Why each agent missed these

The lenses found all eight; the convergence pass found the residuals of 3 and 4 and the partial
sweep in 6. Before the review, the orchestrator's own grep searched only for `EnableShaderPrecompil`,
`TaomPrecompileShaders` and `Patch21_ShaderPrecompilation`. That matched the file-catalogue row, but
the orchestrator took its doc list from the older park, so the hit was not acted on. The INDEX line
says only `(Patch21)` and the battle-load line names only the doc and "the walk", so neither matched.

## Sweep of the audit snapshots

`git show --stat 1e654021` also lists `docs/audits/feature-manifest.md` and
`docs/audits/test-coverage.md`. Both are dated point-in-time audits (2026-05-13) that do not track
parks, and test counts do not change here, so they were left as they are.

## Open outside the change

- #480 (OPEN) still lists "Cold shader-precompile walk, headless" as a checklist item.
- `bannerlord-1.4.5` cannot compile on this desktop (it targets Bannerlord 1.4.8; the install is
  v1.5.3), so the 1.4.5 port is verified only as a line-for-line copy of the 1.5.x change.

## Feedback memories to codify

None. The lesson is "Reversing a toggle again: sweep every commit since the template, and re-verify
its claims" in [build-tooling-workflow.md](lessons/build-tooling-workflow.md). Feature doc:
[shader-precompilation.md](../features/shader-precompilation.md).
