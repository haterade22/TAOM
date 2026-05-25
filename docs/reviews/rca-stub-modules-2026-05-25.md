# RCA — Stub Modules Deep Review (2026-05-25)

## Top-line summary

Deep review of the DR3 stub-module work (commits `031283c` and `8a9d18f`) surfaced 5 confirmed findings (1 MED + 3 LOW + 1 operational HIGH). No CRITICAL violations, no data-flow gaps, no Bannerlord-API incompatibilities. The work is functionally correct; what slipped through were:

1. An over-permissive MSBuild glob (`**\*.*` instead of `**\SubModule.xml`) that would deploy stray editor swap/backup files if any existed.
2. Two doc-drift issues: `dr3-maintenance.md`'s Category-1 version table wasn't updated when the underlying csproj pins bumped during the same session, and the "bump stub version when PackageReference bumps" rule was documented in two places but not in `.serena/memories/task_completion_checklist.md` (the file a maintainer is most likely to consult during PR closeout).
3. The csproj's `<PackageReference>` lines themselves had no inline reminder of the stub-bump dependency.
4. No retroactive GitHub issue tracking the third-party-mod-compatibility scenario.

All fixed in commit `<TBD>` alongside this RCA.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | MED | `Stubs/**\*.*` MSBuild glob would deploy any file extension (e.g., `.bak`, `.tmp`, `.swp`) dropped in `Stubs/` to the game install | Over-permissive glob | When writing the `DeployTAOMDependenciesStubs` target I matched the `..\Stubs\**\*.*` convention used loosely in other Bannerlord example projects. Did not consciously evaluate that stubs are filename-stable (always `SubModule.xml`) and don't need a wildcard extension. The deep-review's Agent 3 (build-time) caught this in 30 seconds with the question "what if someone drops a `.bak`?". | Tightened to `..\Stubs\**\SubModule.xml`. Added inline comment in the csproj target documenting why. No new rule needed — this is a pattern of "favor filename-explicit globs over `**.*`" that applies to all `<Copy>` items in future MSBuild targets. |
| 2 | LOW | `docs/migration/dr3-maintenance.md` Category 1 table showed stale pins (`Lib.Harmony 2.2.2`, `Bannerlord.MCM 5.11.3`) while csproj + stubs were both at `2.4.2` / `5.11.4` | Documentation drift during in-session version bumps | The MCM `5.11.3 → 5.11.4` bump happened in commit `16e8ee2` and the Lib.Harmony `2.2.2 → 2.4.2` revert happened across `0d50fa4 → c451770`. Each individual commit updated csproj + SubModule.xml comment + CHANGELOG, but the **summary table at the top of dr3-maintenance.md was not refreshed.** Tables tend to be authored once and forgotten — they're "background reference," not "active edit surface." | Updated the table in this commit. **Systemic preventive action:** when an agent updates a version pin in ANY config file (csproj, json, xml), it should grep the docs/ tree for the old version string and update every occurrence. Future deep-review Agent 1 (Standards) should add a check: "for every Version string changed in this changeset, confirm no doc references the old value." See "Feedback memories to codify" below. |
| 3 | LOW | "Bump stub `<Version>` when matching `<PackageReference>` bumps" rule was documented in `dr3-maintenance.md` (Stub modules section) and `CLAUDE.md` (Key Paths) but absent from `.serena/memories/task_completion_checklist.md` | Documentation-coverage gap on a maintainer-facing checklist | The rule was authored alongside the Stub modules section. I picked the two natural homes (the dedicated maintenance doc + the top-level codebase pointer) but didn't consider that PR-closeout checklists in `.serena/memories/` are the authoritative "did I miss anything?" surface for the next maintainer. The deep-review's Agent 5 (data flow) explicitly traced rule discoverability across docs and caught the gap. | Added a Step 7 to `task_completion_checklist.md`. The systemic lesson: when a new cross-file invariant is documented, ALSO add it to the checklist. Update the deep-review skill's Agent 4 (Completeness) prompt to include: "for any new cross-file invariant introduced (e.g., 'when X changes, also change Y'), confirm it's added to `.serena/memories/task_completion_checklist.md`." |
| 4 | LOW | No inline csproj comment near `<PackageReference Include="Lib.Harmony">` etc. linking to the stub-bump rule | Same root cause as #3 — discoverability at point-of-edit | Added an inline `<!-- WHEN BUMPING ... also bump the matching stub <Version> ... -->` comment block immediately above the three BUTR `<PackageReference>` lines. A maintainer bumping the version in isolation (without reading dr3-maintenance.md first) now sees the prompt in-place. | No additional rule needed beyond the systemic one in finding #3 — "documentation belongs at point-of-edit AND in the maintainer checklist." |
| 5 | LOW | No retroactive GitHub issue documenting the third-party-mod-compatibility scenario | Process: reactive in-session work was committed + CHANGELOG-entered but never issue-tracked | The stub-modules work was discovered mid-DR3 verification — it wasn't planned upfront. CHANGELOG entries are comprehensive but GitHub issues are the searchable knowledge base for "we hit this before, here's how we fixed it." | Created issue (link below). Systemic preventive action: when significant reactive work surfaces and ships, default to creating a retroactive issue if no existing one applies. Update CLAUDE.md "Mandatory" docs section to clarify: retroactive issues are OK and encouraged when the work is non-trivial. |
| H1 | HIGH (operational) | Build fails with `UnauthorizedAccessException` when Bannerlord is running | Inherent file-locking constraint, not a code defect | The MSBuild `PostBuildCopyToModules` step writes directly to the game install. There's no retry / wait / queue mechanism, and adding one would be more fragile than the current loud-fail behavior. | Documented in `dr3-maintenance.md` under the update procedure: "Build prerequisite: Bannerlord must be CLOSED during `./build.ps1`." No code change; this is an environmental constraint inherent to Bannerlord modding. |

## Root-cause pattern: documentation drift during in-session version bumps

Findings #2 and #3 share a theme: when a version pin or cross-file invariant changes, the **summary table** and **maintainer checklist** lag behind. The actively-edited files (the csproj, the SubModule.xml, the CHANGELOG entry) get the update; the reference doc table and the closeout checklist don't.

This is a recurring class of bug across this project. Previous instances:
- TAOM.Dependencies stub modules (this RCA)
- Likely also: any time a CHANGELOG entry gets the new version but a feature doc still references the old

**Generalizable preventive rule:** when an agent edits a version string in a "primary" file (csproj, source code, etc.), grep all `docs/**/*.md` + `.serena/memories/*.md` + `CLAUDE.md` for the OLD version string. Update every occurrence in the same commit. Add this as a check in the deep-review skill's Agent 1 (Standards) prompt.

## Why each deep-review agent missed (or caught) what

**Agent 1 (Standards):** CAUGHT — checked all 4 stub SubModule.xml files schema-correctness, the MSBuild target structure, and the CLAUDE.md entry. PASSED clean. Did NOT catch the inline-csproj-comment gap (finding #4) because that's not in any standards rule yet — comments are advisory, not enforced. Did NOT catch the doc-drift table (finding #2) because Agent 1's prompt didn't include "grep all docs for old version strings." **Scope extension needed.**

**Agent 2 (Compatibility):** CAUGHT — exhaustively verified launcher / ModuleInfo / dep-resolution behavior across the v1.4.5 decompile. 6 verified, 0 incompatible, 1 unverified (game-side behavior with mismatched ticks — not a stub defect). This was the highest-confidence pass.

**Agent 3 (Efficiency / Build-time):** CAUGHT — found the glob over-matching (finding #1) and the operational HIGH (Bannerlord-running file lock). The build-time adaptation of this agent's role worked well; the "what if someone drops a stray file?" question came directly from the prompt's "item glob over-matching risk" check.

**Agent 4 (Completeness):** CAUGHT — flagged the missing GitHub issue (finding #5) and identified the csproj inline-comment discoverability gap (finding #4). Confirmed CHANGELOG + dr3-maintenance.md + CLAUDE.md were all updated. Did NOT catch the Category 1 table stale-version issue (finding #2) because Agent 4's prompt focused on "is the entry present?" not "is the content accurate?"

**Agent 5 (Data Flow):** CAUGHT THE MOST — traced all 9 data flows cleanly + caught both doc-drift inconsistencies (findings #2 and #3). The trace-7 ("Documentation coverage of the stub version bump rule") is exactly the right grep — it walked the rule from its declaration site through every file a maintainer would consult. This is a vindication of the "data flow agent is the highest-value pass" principle in the deep-review skill.

## Feedback memories to codify

One new memory worth promoting:

**`feedback_version_pin_doc_drift.md`** — When an agent updates a version string in a "primary" file (csproj `<PackageReference Version="...">`, source code constants, XML `<Version value="...">`), it MUST grep all `docs/**/*.md`, `.serena/memories/*.md`, `CLAUDE.md`, and `README.md` for the OLD version string and update every occurrence in the same commit. Particular hot spots: summary tables in maintenance docs, "current version" notes in feature docs, version-pinned examples in API references. **Why:** doc-summary tables and maintainer checklists tend to be authored once and forgotten; they don't get re-read on every edit. The CHANGELOG and inline csproj comments DO get re-read because they're at point-of-edit. Past incidents: `dr3-maintenance.md` Category 1 table (this RCA), likely others not yet RCA'd.

Implementation: add to deep-review Agent 1 (Standards) prompt: "For every Version string changed in this changeset, grep all `docs/**/*.md` + `.serena/memories/*.md` + `CLAUDE.md` for the OLD value and confirm zero stale references remain."

## Commit linkage

- Original stub work: `031283c` (feat: four stub modules)
- Auto-enable follow-up: `8a9d18f` (fix: DefaultModule=true)
- This RCA + fixes: see issue [#221](https://github.com/haterade22/TAOM/issues/221) — closes #221
