# RCA: GUI prefab attribute-name gate, dead prefabs, packager guard (#1024, 2026-10-26)

## Top-line

#1024 added a gate that checks every attribute on a TAOM GUI prefab element against the properties the engine can set,
removed 15 attribute uses the engine ignored (misspellings, names a widget type does not have, and a `VisualDefinition`
attribute the loader never reads), deleted four prefabs nothing loads, and added a packager check that refuses to package
a TAOM module holding a file the repo does not track. The tier 2 review ran Codex at `xhigh`, the data flow, XML and
tooling lenses. They confirmed 1 HIGH, 1 MEDIUM and 7 LOW findings, all fixed in the same session; the HIGH fix was
re-reviewed by Codex at `high` and the tooling lens.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | The packager check refused every editor-published channel package, over `AssetPackages/pack0.tpac`, and told the reader to prune it; in a published folder that file is TAOM's packed UI, so following the message would ship TAOM without its sprites and banners (tooling lens, lens 5, both proven on the real v2.0.35 testing package). | Gate modelled one input shape | The check was designed and tested against the dev install only; the release flow runs the same gate a second time on a different folder shape. The never-delete rule lived only in the maintainer's local memory, which subagents never load. | `pack<N>.tpac` is skipped only in a module with no loose `Assets/`; the rule is now in `release-process.md` and the release skill; tests for both shapes. Lesson in `build-tooling-workflow.md`. |
| 2 | MEDIUM | For `A.B` where `A` is declared as an interface or abstract type (`LayoutImp`), the gate accepted any `B`, so `LayoutImp.HorizontalLayoutMethd` passed although the engine ignores it (Codex). | Static type used where the engine uses the runtime type | The engine resolves `B` on `A`'s runtime value; the plan chose "not judged" for unknown runtime types instead of a failure. | Known runtime types (`GridWidget` to `GridLayout`, `ListPanel` to `StackLayout`, from their constructors); any other unknown intermediate fails. |
| 3 | LOW | The gate skipped `VisualDefinition` attributes, where `EaseIn="true"` (four uses) did nothing: the loader reads only five names there (lens 5). | Element treated as structural | `VisualDefinition` was put in the skip list with `Children` and `Constants`. | Checked against the five names the loader reads; `VisualState` against its properties; the four uses deleted. |
| 4 | LOW | The allowlist comment said vanilla-carried names are kept, but the change deleted some vanilla-carried names and kept others (lens 7). | Rule text not matching the rule applied | The comment was written before the builder deleted the extra names. | Comment states the applied rule. |
| 5 | LOW | "Delete, never respell" was written as universal; it is right only for a shipped prefab (lens 7). | Rule scope | Written for the case at hand. | "In a shipped prefab, delete it; in markup you are writing, fix the name and look in game." |
| 6 | LOW | A path rule stated an allowlist count nothing computes, and named a scope the gate does not scan (lens 7). | Uncomputed count | CLAUDE.md bans such counts; not checked. | Count dropped, scope corrected. |
| 7 | LOW | The test's description of get-only properties was wrong for property types the string setter does not convert (lens 7). | Engine claim from the plan | Copied from the plan without reading the setter's type dispatch. | Corrected. |
| 8 | LOW | The release docs and the packager's help still said the gate proves the DLLs only (tooling lens, lens 5). | Docs not swept | Only the prune paragraph was updated. | Docs, help, docstring and refusal heading updated. |
| 9 | LOW | The build brief said vanilla dropped `AlwaysRefreshAll`; vanilla still carries it, on a `ListPanel` where TAOM uses a `NavigatableListPanel` (lens 7). | Claim from a plan, not the file | Taken from the architect's summary. | Corrected in the test comment; the removal stands (a no-op either way). |

**Fix review.** The HIGH fix was re-read by the tooling lens (re-running its probes on the real channel folders and the
dev install, plus a mutation harness) and Codex at `high`. Neither found a CRITICAL or HIGH; they found 1 MEDIUM and 3
LOW, all fixed: the exemption infers "editor-published" from a missing `Assets/` folder, so a published folder with a
stray `Assets/` would get advice to delete its pack (the refusal for a pack now explains both folder kinds and
recommends no deletion; an explicit CLI flag was rejected by the simplicity criterion, since every real folder fits the
rule); two guarantees of the exemption had no test (only pack files exempt, the message naming the checked root); the
visual-attribute check had no negative fixture; and one removal also dropped two spaces of its line.

Follow-up closed 2026-11-04 (abb718c2f, #1027) (no issue: a look change, so it needs Mike's word): vanilla's custom battle panels use
`EaseType="EaseOut" EaseFunction="Quint"` where TAOM's copy had the dead `EaseIn="true"`; adopting vanilla's values would
make the panels ease visibly.

## Root-cause pattern

Findings 1 and 2 share one cause: the check was designed against the shape of the input in front of the designer (the
dev install; the declared type) rather than every shape the system feeds it (an editor-published folder; the runtime
type). Findings 4, 5 and 9 are text written from the plan before the build settled.

## Why each reviewer caught or missed what it did

- **Tooling lens:** ran the check read-only on the real channel folders, which is how it found 1.
- **Data flow (5):** traced the release flow to its second gate run (1) and the `VisualDefinition` loader (3).
- **XML (7):** checked every comment and rule line against the engine and vanilla (4 to 7, 9).
- **Codex (`xhigh`):** built a concrete misspelled-suffix counterexample from the decompile (2); it did not run the
  packager on a channel folder.

## Feedback memories to codify

None. The lessons carry the rules.
