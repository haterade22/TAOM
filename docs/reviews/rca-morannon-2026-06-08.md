# RCA — Morannon armor + troop tree deep-review (2026-06-08)

## Summary

Deep-review of two commits (`ae2313e` Morannon armor + `9747c01` Morannon troop tree) surfaced 4 confirmed findings: 1 cosmetic generator drift (MED), 2 robustness defects in the generator's file I/O (HIGH-by-rule, LOW-in-practice on this project), and 1 cross-module data-dependency flag that, on verification, turned out to be intentional project-wide policy (NOT a session bug). No game-runtime bugs introduced. The Morannon feature itself shipped clean — 92 armor items + 10 troops + party/recruitment wiring all validated end-to-end.

All session-actionable findings were fixed in-session (generator patched + Armory cleaned). The pre-existing project-wide flag was documented and memorialized rather than "fixed" — declaring `LOTRLOME_Armory` as a `<DependedModule>` would break the Bannerlord editor.

## Findings

| # | Sev | Finding | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | MED | `tools/generate_mordor_armor.py` `apply()` injected the section-comment header on every run with new items; no idempotence guard. After this session's Morannon apply, every Armory XML carried 2 "KEYforce Mordor" headers (one historical orc-pool from a prior commit + one new Morannon). | Generator drift / tooling correctness | The dedup logic was scoped to `<Item id="X">` substring matching — a per-item idempotence check. The section comment was a separate insertion site with no parallel guard. The author didn't extend "idempotent" thinking to all sites that mutate the file. | **Done:** added `if section_marker in original_content: section_comment = ""` guard in `apply()`. Section header is now inserted exactly once per file across any number of apply runs. |
| 2 | HIGH-by-rule (LOW-in-practice) | `apply()` opened files without explicit `newline=` parameter. On Windows native Python the default `text` mode translates CRLF↔LF, so a round-trip preserves CRLF safely. If ever executed on Linux/Mac (CI, container, etc.), the write side would emit bare LF and corrupt the Armory file's line endings. TAOM is Windows-only today, so the practical impact is zero. | Platform-dependent I/O | The author treated "Python text mode" as automatically handling line-endings, which is true on the development OS but not portably. The rule from `feedback_xml_tool_bom_io_convention.md` exists for this exact issue but was scoped in the author's memory to BOM, not CRLF. | **Done:** added `newline=""` to both read and write `open()` calls. CRLF is now preserved verbatim on any platform. Also reinforces the generic XML-tool I/O convention for future generators. |
| 3 | MED | `apply()` overwrote files in-place with no `.bak` sidecar. Since the Armory is not git-tracked, an unintended corruption is unrecoverable except by re-running the generator (which works only if the corruption left valid XML). | Defensive I/O | The author's prior generators (`generate_dale_armor.py`, `generate_isengard_armor.py`) followed the same in-place pattern with no backup, so the missing backup wasn't visible as a deviation — it was the established (poor) convention. | **Done:** the generator now writes `<filepath>.bak` containing the original content before the in-place overwrite. Same convention should be propagated to the sibling per-culture generators (`generate_dale_armor.py`, `generate_isengard_armor.py`, `generate_gondor_armor.py`, etc.) in a follow-up sweep. |
| 4 | "CRITICAL" → re-classified PROJECT-WIDE INTENTIONAL | Data-flow agent flagged `Main/_Module/SubModule.xml` for not declaring `LOTRLOME_Armory` as a `<DependedModule>`, even though every TAOM troop XML references `sk_*` items defined in that Armory module (`troops_gondor.xml` 1,124 refs, `troops_mordor.xml` 506, `troops_dale.xml` 324). | Pre-existing TAOM design choice — NOT a bug | The cross-module data-dependency rule (the one Bandit Management codex review #2026-05-27 added) fires on any TAOM-controlled module-to-module reference. The rule lacks an exception for **editor-mode compatibility**: TAOM intentionally omits the LOTRLOME_Armory declaration because declaring it breaks the Bannerlord editor (`Win64_Shipping_wEditor`). Runtime load-order relies on alphabetical / launcher convention; editor mode requires the omission. | **Done:** memorialized as `memory/feedback_no_depended_module_for_lotrlome_armory.md` so future deep-reviews drop the false-positive. Rule itself stays in place for OTHER cross-module dependencies (e.g., `TAOM_Map` → `TAOM`); the exception is scoped to asset-only Armory modules. **Do NOT add the `<DependedModule>` declaration.** |

## Root-cause pattern: "rule fires correctly but lacks a project-specific exception"

Findings 2 and 4 share a shape: a generic best-practice rule (newline=, declare module dependencies) is technically correct in the abstract, but the project has a context-specific reason to deviate. The right preventive action is NOT to "follow the rule everywhere" but to encode the exception in a way that future deep-reviews surface the rule's coverage rather than the false positive.

Both findings now have memory entries that the data-flow / tooling agents will load on future runs.

## Why each deep-review agent missed (or surfaced) the findings

| Agent | Verdict | Why this distribution |
|---|---|---|
| 1 Standards | ALL PASS | No standards rules were violated. The session work was pure data + a static-dict update; nothing in the rule set could have fired. **No miss; correct verdict.** |
| 2 Compatibility | ALL PASS | No new TaleWorlds API surfaces; only existing item/troop/culture XML refs, all of which resolved. **No miss; correct verdict.** |
| 3 Efficiency | NO ISSUES | Pool size grew from 10→15 in a once-per-daily-tick path. Truly no perf concern. **No miss; correct verdict.** |
| 4 Completeness | COMPLETE | Tests updated, CHANGELOG updated, troops.md checklist 7/7. Tests-blocked-by-locked-DLL flagged as env-issue. **No miss; correct verdict.** |
| 5 Data Flow | 1 GAP (false positive) | Surfaced the missing `LOTRLOME_Armory` declaration as CRITICAL. Was technically right per the rule, but the rule lacked the editor-mode exception. The agent's broader trace (item refs, upgrade graph, weight arithmetic, name brackets, hair_cover_type, covers_hands, Pik-shares-Inf) was all correct. **Correct rule application, missing exception.** |
| 6 Tooling Correctness | 2 MED + 1 HIGH-by-rule + 1 LOW | This agent caught all three real session-actionable findings (comment dedup, newline=, .bak). **The other 5 agents would not have caught these — they're C#-centric and don't review Python tooling.** Without the tooling agent, this session would have shipped 2 medium defects in a script that mutates live external data. The skill's "always launch a tooling correctness agent when `tools/*.py` writes outside the repo" rule fired correctly. |

The tooling-correctness agent is the load-bearing addition for any session that extends a script that writes to the external Armory or `TAOM_Map`. **Keep the rule — it caught everything the C#-centric agents missed.**

## Feedback memories to codify

Only one is genuinely new and project-shaping; the others are scoped extensions or already exist:

1. **NEW: `feedback_no_depended_module_for_lotrlome_armory.md`** — written this session. Documents the editor-mode constraint and the cross-module rule's exception. Indexed in MEMORY.md.
2. **Reinforce, don't add:** `feedback_xml_tool_bom_io_convention.md` already exists. The `newline=""` finding is in scope but wasn't applied. The lesson is "the BOM rule's intent is broader than BOM — it's about preserving file bytes verbatim, which also means CRLF." Consider editing that memory's text to make `newline=""` an explicit named requirement alongside BOM preservation. Deferred — one-line tweak; not blocking.
3. **No new generic rule needed for comment dedup:** the issue is project-local to the per-culture generators. Worth sweeping the sibling generators (Dale, Isengard, Gondor, Erebor, etc.) for the same pattern in a follow-up commit, but no global rule is warranted yet — the pattern only manifests in apply-style generators that inject a section header.

## Cross-references

- **Commits in scope:** `ae2313e` (Morannon armor), `9747c01` (Morannon troops), and a follow-up commit landing the generator patches + Armory cleanup + this RCA.
- **Sibling RCAs with similar shape:** `docs/reviews/rca-scene-tooling-2026-05-28.md` (tooling-correctness agent caught BOM I/O issue the 5 core C# agents all missed). Same agent-coverage pattern.
- **Feedback memories referenced:** `feedback_no_depended_module_for_lotrlome_armory.md` (NEW), `feedback_xml_tool_bom_io_convention.md` (pre-existing, reinforced), `feedback_native_port_hot_path_audit.md` (referenced by the C++ checks — not in scope this session).

## Verdict

- All session-actionable findings fixed in-session.
- One project-wide false-positive memorialized so it stops firing.
- No game-runtime bugs introduced by the Morannon work.
- Tooling-correctness agent earned its keep — without it, 3 of the 4 findings would not have been caught.
