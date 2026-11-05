# RCA: schema check names every engine loader (#980, deep-review, 2026-10-23)

## Top-line

Issue #980 finished the work `48d66b59a` started on `tools/validate_xml_schemas.py`: a NATIVE bucket for files a
`project.mbproj` row names, exit 1 when a run on named files finds a real fault, and one shared walk of SubModule.xml
registrations. `/deep-review` ran seven lenses in two waves (Standards, Engine, Data flow, Tooling; then Efficiency,
Completeness, Design) and a convergence pass. No CRITICAL or HIGH finding; every confirmed finding was fixed before the
commit. Whole-module runs, which the commit hook makes, are byte-identical to before.

The main finding repeats the defect #980 exists to remove, one loader further on: the first NATIVE line said "loaded
through project.mbproj row X" for any row, while the engine loads a row only when a caller requests its exact id
(managed `GetMergedXmlForNative`) or when native code reads its `type`. The tool again described a load path from the
shape of the data rather than from the engine's consumer.

## Findings and root causes

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | NATIVE said "loaded" for every `<file>` row; the engine loads a row only when its id is requested (managed) or its `type` is read (native), so a dead id such as `soln_spider_monster` printed "loaded". | Load path inferred from data shape (REPEAT of the #980 class) | The brief asked for "a file a row names" and described one loader; the builder verified the row parsing and path joins, not who requests a row. | The line says "named by" a row with its id and type, and that it loads only if requested; the id and type check stays in `audit_mbproj_registration.py`, whose own gaps are #983. Lesson in `lessons/build-tooling-workflow.md`. |
| 2 | MED | The NOT REGISTERED line and docstring still said `project.mbproj` rows were not checked, contradicting the NATIVE line in the same output; a test pinned the stale text. | Old text left beside new behaviour | The new bucket was added above the old reason without re-reading it. | Reworded; the test asserts the new text. |
| 3 | MED | A `project.mbproj` the engine refuses at startup passed a named run quietly, unlike the tool's own SubModule.xml rule ("each is reported, never skipped"). | Parity with a sibling rule | Silent skips were written as crash safety, not as the engine's verdict. | Its problems are MISSING when the file is named (exit 1), computed outside the scan gate so naming it alone still checks it. |
| 4 | LOW | Row names joined with pathlib; the engine joins strings, so a leading slash or an absolute name resolved differently. | Engine join | The language-file code had the fix; the new code did not reuse it. | String join, tests for both shapes. |
| 5 | LOW | An unsupported encoding escaped as a traceback (exit 1 instead of the documented 2), at three parse sites. | Exit-code contract | Only ParseError and OSError were caught. | ValueError and LookupError caught; tests. |
| 6 | LOW | Tests needing lxml carried no `NEEDS_LXML` marker, so the suite failed on a machine without lxml, against its own contract. | Test contract | New classes copied an undecorated neighbour. | Markers; a no-lxml run gives 0 failures. |
| 7 | LOW | The verdict was computed twice and each not-registered path checked three times. | Duplication | `real_faults` re-derived what `_print` printed. | Faults counted where printed; `_print` returns the verdict; `real_faults` deleted. |
| 8 | LOW | Docs: README listed NATIVE as a real-fault case; the test-count split; a ragged `--help` docstring; "now" in a lens instruction; `validation-and-testing.md` still said no validator models the XSD layer (wrong since 2026-09-18); the NATIVE line pointed at a tool that cannot check a native file's content, and the gate table had no row for voice and sound files. | Docs | Written from the brief, not from the code and the gate table. | All corrected; a gate-table row for `audit_voice_clip_lengths.py`. |

Applied design proposals (behaviour-preserving): one stylesheet-lookup helper for the three copies of the engine's
`.xsl`, else `.xslt`, rule; the registration walk returns two plain lists instead of tagged tuples; the duplicate
exit-code test class folded into the CLI class. Not applied: one shared `_cli` test helper on `_Tree` (about 15 test
lines; its own change, listed on #980's close).

## Root-cause pattern: a deferred follow-up with no owner, three times

The defect in #980 was found on 2026-10-04 (MS7-4), on 2026-10-06 (finding 11) and in the #848 review, and each time
written as "`FOLLOW-UP`, not fixed here" with no issue. The commit that added a lessons bullet against exactly that
(`48d66b59a`, 2026-10-21) shipped an RCA that again left three items without an issue; #980 was filed two days later.
A prose rule that fails three times needs a machine check: #984 adds one to `lint_docs.py` (a new `FOLLOW-UP` line in
an RCA must cite an issue or a reason). This review filed its own follow-up the same day (#983).

## Why each lens caught or missed what it did

- **Standards (1)** found the stale NOT REGISTERED text (2), the double verdict (7) and the lxml markers (6).
- **Engine (2)** found the "loaded" overclaim (1) with both loaders traced in managed and native code, and the quiet
  startup failures (3).
- **Data flow (5)** found the overclaim from the consumer side (1), the stale text (2) and the leading-slash join (4),
  and proved whole-module output byte-identical.
- **Tooling** reproduced 1, 3, 4, 5 and 6 on synthetic modules and checked every live row.
- **Efficiency (3)** found the triple check (7) and the gate caution behind 3.
- **Completeness (4)** found the issue body, the gate pointer and table row, the stale guide and the repeat pattern.
- **Design (6)** gave the shapes applied above.

## Convergence pass

One deep-reviewer re-ran every check against the fixed tree: whole-module output byte-identical to HEAD on the repo and
`--live`; `registered_files` and `applied_stylesheets` identical on all 20 real modules and 16 synthetic edge cases; a
broken `project.mbproj` passed alone fails in all seven shapes; no shipped language file turns the new exit 1 red. It
found one MEDIUM and five LOW, all fixed:

| # | Sev | Finding | Fix |
|---|---|---|---|
| C1 | MED | A Python parse failure was reported as an engine failure. The engine reads `project.mbproj` and `language_data.xml` as decoded text and ignores the declared encoding, so a file declaring an unknown encoding loads in the game (replayed on the installed .NET Framework) while the tool said the game would not start. | One helper parses like the engine (decode as UTF-8, ignore the declared encoding) before calling anything an engine failure. Remaining limit stated: character references the engine accepts with `CheckCharacters=false` still fail here. |
| C2 | LOW | A row name under four characters was reported as a startup throw; the engine checks length only when the id is requested. | Check dropped. |
| C3 | LOW | The README's exit-1 sentence named NATIVE, which never fails a run. | Corrected. |
| C4 | LOW | The new gate row sent all of `module_sounds.xml` to the voice clip audit, which checks only the sounds a voice binds. | Row says exactly that. |
| C5 | LOW | The testing guide said the gate checks every registered file; a registration with no XSD prints NO SCHEMA. | Corrected. |
| C6 | LOW | One RCA pointer dated a fix to #980 that landed in `48d66b59a`. | Corrected. |

**Why C1 got through.** The tool's own parser stood in for the engine's: "Python cannot parse it" was written as
"the engine throws", the same move as finding 1 (the data's shape standing in for the engine's consumer). The
preventive action is the lesson in `lessons/build-tooling-workflow.md`: a validator states an engine verdict only from
the engine's own read path.
