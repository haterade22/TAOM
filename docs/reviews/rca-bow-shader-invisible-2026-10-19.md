# RCA: bows on TAOM shaders make their holder invisible (#916, 2026-10-19)

## Top-line

A character who carried one of 26 Armory bow meshes did not render at all, in character creation
and in game, and the bow thumbnails stayed on the loading spinner. The Armory art drop of 2026-10-15
(lotraom-assets `db981663`) moved bow and crossbow materials (12 on the shipped shader report, 13
listed in #916) to three new shader assets,
`taom_deformer_{bow,longbow,crossbow}`. A shader asset only describes a shader: the engine compiles
its six generated `.rs` pass files, and only from `<Bannerlord>\Shaders\Sources`. Those 18 files
existed only on the shader author's machine. The 2026-10-15 review of that commit had reported this
as its HIGH finding F1, and the materials went live anyway, with no gate to stop them.

The fix ships the 18 files in `Main/_Module/Shaders/Sources` and copies them into the game folder at
startup (`ShaderSourceInstall`), and `tools/package_release.py` refuses a package whose shader report
names a `taom_` shader the package does not carry. The hand copy was verified in game on 2026-10-19
(`rgl_log_35668.txt`: 28 `taom_deformer` compiles, no errors, bows render).

The first version of the fix, pushed as `d99398f12` while `/deep-review` was running, carried two
HIGH defects of its own, and its merge `85cbd20c8` dropped the localization rows, which turned
`NoNewUnregisteredLocalizationKeys` red on trunk. All findings below were confirmed against the
code, the installed v1.5.5 DLLs (ilspycmd), the native engine (Ghidra) or a probe run.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 0 | HIGH | Art drop shipped shader assets whose sources exist on one machine only (#916 itself) | Unversioned dependency outside every module | The 2026-10-15 review found it (F1) but nothing could block it: no gate read shader reports, and the copy path did not exist | `ShaderSourceInstall` plus the release gate; lesson in `data-content-cultures.md` |
| 1 | HIGH | The copy-failure warning was an `InformationManager.DisplayMessage` chat line from the main-menu hook; the initial screen calls `ClearAllMessages()` after the splash, so no player sees it | **Repeat** of the plan-009 lesson (`lessons/localization-ui.md`, "Nothing receives a chat message before the initial screen") | The implementer brief (written by the orchestrator) said "show one InformationManager message"; the implementer copied a `DisplayMessage` idiom from an in-game adapter. The lesson's gate reads `SubModule.cs` only, so code in a feature folder is invisible to it | Warning is now an inquiry from the module's `MainMenu` phase, pinned by a source-text test in `ShaderSourceInstallWiringTests`; lesson amended with the second instance and the brief wording |
| 2 | HIGH | The gate checked `<shader>.rs` only. The engine compiles the pass files (`_gbuffer`, `_shadowmap`, `_pointlight`, `_constant_output`), and session 35668 never compiled the base file, so a package missing a pass file passed | Gate checks a proxy, not what the consumer reads | The gate brief said "`<shader>.rs`"; nobody compared the checked name with the `compile_shader` lines in the log | Gate requires all six files for a `taom_` shader; test per missing suffix; lesson in `build-tooling-workflow.md` |
| 3 | HIGH | An empty, truncated or reformatted shader report passed in silence: every row failed the parse, so no shader was checked | Silent clean | The parse skipped what it could not read, with no count and no cross-check | Rows must add up to the report's `Total variants:` trailer; a passing run prints its row and shader counts; unreadable report exits 2 |
| 4 | HIGH | Merge `85cbd20c8` took the incoming side where both parents inserted before `</strings>`, dropping all 13 rows of `taom_shader_source_install_failed`; trunk went red | Merge conflict resolution on a generated-append file | The merge was pushed without a test run after it | Rows re-harvested and re-seeded in the fix commit; lesson in `build-tooling-workflow.md`: run the localization filter (or `/verify`) after any merge that touches ModuleData strings, before the push |
| 5 | MEDIUM | The gate took the game folder as the parent of `--source`. The `/release` channel folders have no `Shaders/Sources`, so every native shader read as missing and every channel dry run would exit 2 | Untested input shape | Tests built only the dev-install layout | `--game-root` flag, refusal naming it when the folder is absent, runbooks updated, channel-layout tests |
| 6 | MEDIUM | The gate judged files on disk under `<src>/TAOM`, not what the package ships: `--modules LOTRLOME_Armory` alone passed | Gate checks a proxy | Sibling gates walk the plan's `_copy_list`; this one did not | Gate reads the TAOM plan's copy list |
| 7 | MEDIUM | The warning named `BasePath.Name + "Shaders/Sources"`, which is the relative `../../Shaders/Sources` | Engine value assumed absolute | `BasePath.Name` was not decompiled before use in player text | `Path.GetFullPath` for both folders, logged and shown |
| 8 | MEDIUM | Nothing re-checks the 18 copies after an engine update; a TaleWorlds change to the native deformers would bring #916 back silently | Derived data pinned to one engine version | No gate tied the copies to their native twins | LiveInstall `TaomShaderNativeTwinTests` (each copy equals its twin plus the known 4-line delta) |
| 9 | LOW | The report parse dropped rows whose 40-character Material column leaves one space (168 rows) and would drop MetaMesh names with a space under a naive split | Fixed-width format parsed by gap width | The fixture was not built in the live format | Right-anchored row regex; fixtures built with the live 60/40/40 widths |
| 10 | LOW | Module names and the `taom_` prefix compared case-sensitively | Inconsistent with the packager's DLL check | Not tested | Casefolded; tests |
| 11 | LOW | The service's listing-failure branch had no test; a missing or empty module folder read as success | Coverage gap | No RED run was recorded for the first version | Tests added; a missing source folder is now a failure |
| 12 | LOW | The copy also ran on a dedicated server | Missing gate | The sibling notice's server gate was not mirrored | The feature module skips the copy on a dedicated server |
| 13 | LOW | `harvest_literal_loc_keys.py` adds one more blank line before `</strings>` on every run (pre-existing since `76c82cb63`) | Tool defect | Earlier harvests were trimmed by hand | Follow-up closed 2026-11-04 (337228885): fix the insert, add a test for the tool |

## Root-cause pattern: a gate that checks a stand-in

Findings 2, 3, 5 and 6 share one shape: the gate checked something near the real condition. It
checked the base `.rs` instead of the files the engine compiles, files on disk instead of what the
package ships, the rows it could parse instead of the rows the report holds, and a guessed game
folder instead of a named one. Each stand-in matched the real condition on the machine where the
gate was written, so its tests passed. The rule: **a gate reads the same input its consumer reads
(the engine's compile list, the package's copy list), and proves it read all of it (a count, a
trailer).**

## Why each agent missed these in the first version

- **Implementer subagents:** each followed its brief, and the briefs carried findings 1, 2 and 5.
  The orchestrator wrote those briefs from the plan, not from the engine log or the runbook.
- **The orchestrator's own check:** confirmed the gate exits 2 on the live install and passes the
  tests, both on the one layout the gate was written for.
- **`/deep-review`:** caught every finding above except 0 (pre-existing) and 4 (it happened during
  the review). Data flow and standards found finding 1 independently; tooling found 3.

## Process note

The work was committed and pushed (`d99398f12`, subject without the version label, body without
#916) while the review was running, so the review's base moved and its findings land as a second
commit. The fix commit's body carries the release-note paragraph that `d99398f12` lacks.

## Feedback memories to codify

None new. Finding 1 is a repeat: its lesson entry is amended with the brief wording that caused the
second instance. The new source-text test pins this feature's two files only; another module's
startup notice sent with `DisplayMessage` is still unchecked. A general gate is a follow-up decision.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/lessons/build-tooling-workflow.md](lessons/build-tooling-workflow.md)
- [docs/reviews/lessons/data-content-cultures.md](lessons/data-content-cultures.md)
- [docs/reviews/lessons/localization-ui.md](lessons/localization-ui.md)

<!-- backlinks-end -->
