# RCA — Faction-Map CC Page Rewrite Phase 2 (deep-review fixes)

**Date:** 2026-06-01
**Feature:** `Main/Features/FactionMap/*` + `Main/_Module/ModuleData/factionmap/factions.json` + `taom_module_strings.xml`, Phase 2 of issue [#260](https://github.com/haterade22/TAOM/issues/260).
**Review pipeline:** Phase 1 (commit `53ce308`) → Phase 2 (commit `cbbcc41`) → `/deep-review` (6 agents) → adjudicate findings → fix → final `/verify`.
**Codex review:** queued — runs after this commit.

## Top-line summary

Deep-review (6 agents — 5 core + 1 tooling-correctness for the new harvester script) returned **0 CRITICAL, 1 HIGH (Tooling H1), 2 MED (Tooling M1+M2), 1 GAP (Data Flow), 0 LOW** plus 4 of 6 agents PASS-clean.

After verification:
- **Tooling HIGH H1 was a FALSE POSITIVE.** Re-ran the harvester empirically; git diff showed zero changes and the file remained 2049 CRLF / 0 bare LF. Python's universal-newline mode handles this correctly on Windows.
- **Tooling MED M1+M2 rejected** per `simplicity-criterion.md` (one-off interactive tool; `--dry-run` + stderr warnings = scope creep with zero observable benefit).
- **Data Flow GAP confirmed.** `FactionSelectionService.FormatDifficultyText` returned 7 hard-coded English strings ("Difficulty: Hard" etc.) with no `{=KEY}` token — a pre-existing Phase 1 holdover, but within the user-approved "full localization sweep" scope, so fixed in this commit.

## Findings table

| # | Sev | Finding | Category | Verdict | Resolution |
|---|---|---|---|---|---|
| 1 | HIGH | Harvester would corrupt line endings on every run (CRLF / LF mismatch) | Encoding / I/O | **FALSE POSITIVE** | Verified by re-running the tool: zero git diff, file remains 2049 CRLF / 0 LF. Python `Path.read_text()` and `Path.write_text()` with default `newline=None` do universal-newlines translation on BOTH read and write under Windows text mode — the in-memory representation is `\n` and the on-disk file is `\r\n`, transparently. The agent's reasoning ("bare `\n` produced by `\\n.join(...)` will mix with the file") is correct in principle for binary I/O but incorrect for text mode. No fix needed. |
| 2 | MED | No `--dry-run` flag on harvester | Tooling discipline | **REJECTED** (simplicity-criterion) | One-off interactive harvester; running the tool once and re-running on regeneration is the entire workflow. Adding a flag with zero observable benefit is YAGNI. |
| 3 | MED | Duplicate-key warnings printed to stdout instead of stderr | Tooling discipline | **REJECTED** (simplicity-criterion) | Tool is invoked interactively, not from CI. Stdout warning is visible when run by a human; switching to stderr or `logging` adds complexity for zero observable gain. The only warning case is duplicate keys, which the json/string-id naming convention already prevents. |
| 4 | GAP | `FactionSelectionService.FormatDifficultyText` returns 7 hard-coded English strings with no `{=KEY}` token | Pre-existing Phase 1 holdover + Phase 2 scope | **CONFIRMED, fixed** | Wrapped each branch in `{=taom_faction_difficulty_N}default` format and added 7 corresponding `<string>` entries to `taom_module_strings.xml` in a hand-authored block (not auto-harvested — these keys come from C# code, not `factions.json`). Updated `FactionSelectionServiceTests.FormatDifficultyText_ValidDifficulty_ReturnsKeyedString` to assert the new prefixed form. |

## Root-cause pattern — Audit finding accuracy is ~95%, not 100%

The Tooling HIGH was confident-sounding, technically literate, and wrong. This is the recurring pattern documented in `feedback_audit_findings_not_always_correct.md`: review agents (and Codex) are usually accurate but not infallible, and confidence does NOT correlate with correctness. The discipline that caught this was `evidence-over-claims.md` — "verify before acting." A one-line empirical test (re-run the tool, diff the file) disproved the agent's hypothesis in under a second.

The mechanical guard for the next reviewer: **when an agent flags an I/O-format issue (encoding, line endings, BOM), the verification step is to actually run the tool and compare bytes — not to read the source code and trust your reading of it.** Two intelligent agents (me and the tooling-correctness reviewer) read the same source code and disagreed on whether universal-newlines translation propagates through both `read_text` and `write_text`. The empirical answer (1-line `python` script + `git diff --stat`) settled it.

## Why each agent missed (or accepted) these

| Agent | Result | Why this report |
|---|---|---|
| Standards (Haiku) | PASS clean | Phase 2 is data + tests + tooling; ADRs largely don't apply. Correct PASS. |
| API Compatibility (Sonnet) | PASS, with corroborating TextObject behavior | Verified `TaleWorlds.Localization.TextObject` behavior against installed v1.4.5 DLL. Correctly noted no additional API surface in Phase 2. |
| Efficiency (Haiku) | PASS clean | Content + once-per-click code; performance is a non-issue here. Correct PASS. |
| Completeness (Haiku) | PASS clean | All 6 completeness checks satisfied. Correct PASS. |
| Data Flow (Sonnet) | 6/7 traces CONNECTED, 1 GAP | Correctly identified the `FormatDifficultyText` gap as a Phase 1 holdover within Phase 2 scope. **Highest-value agent of the 6.** |
| Tooling Correctness (Sonnet) | 1 HIGH (false positive) + 2 MED + several OK items | Read the script source carefully and reasoned about Python's text I/O semantics. The reasoning was internally consistent but the empirical behavior on Windows text mode differs from the agent's mental model. |

## Preventive actions

### 1. Reinforce evidence-over-claims for I/O / encoding findings

Already codified in `.claude/rules/evidence-over-claims.md` ("verify before implementing") and `feedback_audit_findings_not_always_correct.md`. This RCA adds a worked example for the I/O-format category — when an agent flags an encoding/BOM/line-ending issue, run the tool and diff the bytes. Don't fix on inspection alone.

### 2. Extend the "no scope creep" guard to tooling

For one-off harvester / lint / data-migration tools, `simplicity-criterion.md` still applies. Add a `--dry-run` flag when the tool writes to LIVE shared data (game install, external module). For a tool that writes to a repo file you're about to commit, the git diff is the dry-run. No fix needed in this category.

### 3. Localization sweep coverage

The `FormatDifficultyText` gap is a generalizable failure mode: when running a localization sweep on data files (`factions.json`), don't forget the C# strings that flow through the same VM. The Phase 1 helper (`FactionDisplayHelper.Localize`) wraps `DifficultyText` — so the gap was structurally invisible from a JSON-only view but visible from a service-method view.

**Add to `feedback_faction_map_update_with_cultural_feats.md`** (the standing instruction codified earlier this session): when localizing, audit BOTH the data file (`factions.json`) AND any C# methods that return strings flowing into the same VM (in this case `FactionSelectionService.FormatDifficultyText`).

(Codified inline in this RCA; if the same pattern shows up again, promote to its own memory entry.)

## Verification

```
dotnet build TAOM.Tests --p:DisableModuleCopy=true ...  -> 0 Errors
dotnet test  TAOM.Tests (full)                          -> 2798 / 0 / 2  (no regression)
FactionMap filter                                       -> 89 / 0 / 0
python tools/harvest_factionmap_strings.py              -> Wrote 599 keys; git diff = empty
python tools/validate_moduledata.py                     -> PASS
PowerShell XML smoke                                    -> module_strings: OK (1870 entries, +7 difficulty keys)
```

## Files changed in this fix commit

- `Main/Features/FactionMap/FactionSelectionService.cs` — wrap 7 difficulty branches in `{=KEY}default`.
- `Main/_Module/ModuleData/taom_module_strings.xml` — add 7 hand-authored `<string id="taom_faction_difficulty_N">` entries above the auto-harvested block.
- `TAOM.Tests/Features/FactionMap/FactionSelectionServiceTests.cs` — update `FormatDifficultyText_ValidDifficulty_*` test to assert new keyed form.
- `docs/reviews/rca-faction-map-phase2-2026-06-01.md` — this file.

## Linked prior context

- [`feedback_audit_findings_not_always_correct.md`](C:/Users/mikew/.claude/projects/c--Users-mikew-source-repos-TAOM/memory/feedback_audit_findings_not_always_correct.md) — applied to the Tooling HIGH false positive.
- [`feedback_faction_map_update_with_cultural_feats.md`](C:/Users/mikew/.claude/projects/c--Users-mikew-source-repos-TAOM/memory/feedback_faction_map_update_with_cultural_feats.md) — the standing instruction this whole session traces to.
- `.claude/rules/evidence-over-claims.md` — verify-before-acting discipline.
- `.claude/rules/simplicity-criterion.md` — Yes/No matrix used to reject the two MED findings as scope creep.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/rca-faction-map-phase2-codex-2026-06-01.md](./rca-faction-map-phase2-codex-2026-06-01.md)

<!-- backlinks-end -->
