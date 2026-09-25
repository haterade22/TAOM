# RCA: plan 009 maintainer decisions, second review (2026-09-24)

## Summary

The second review of plan 009 (`4c728dac..b6cb6ff5`: the class-by-class category index and the
localized failure notice) had seven lenses and Codex. Every lens and Codex found the same HIGH
defect: the 60 translated rows for the notice sat after `</strings>`, where
`LocalizedTextManager.LoadLanguage` never reads them, so maintainer decision 2 did nothing for any
non-English player while every test and the CHANGELOG said it was done. The translator's
`sync_missing_ids` put them there: it reads these files as CRLF, but their lines end `\r\r\n` with
a bare-LF tail, so the last rows and `</strings>` split as one line and the insert lands after it.
Ten more findings were confirmed (4 LOW data, test and doc gaps, 2 NIT), one was a false positive,
and one LOW is a design question for Mike. Report:
`docs/reviews/deep-review-009-guarded-patch-category-apply-decisions-2026-09-24.md`.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | HIGH | 60 translated rows written as children of `<base>`, after `</strings>` | Other: data written where the engine never reads | The seeding was trusted; `LanguageFileCoverageTests`, `LanguageTextIntegrityTests` and the translator's own parser all walk descendants, so a row at any depth counted as present. No check reads rows the way the engine does | `LanguageDataXmlTests.AllTranslationFiles_StringRowOutsideRootStrings_IsNeverPresent` (RED on the `b6cb6ff5` blobs); lesson in `lessons/localization-ui.md`; the tool fix is a follow-up |
| 2 | LOW | Seeded rows ended `\r\n` in `\r\r\n` files | Convention inconsistency | Same tool, same line-ending misread | Resolved by `7eae4704`; normalising the files is a follow-up |
| 3 | LOW | DE, FR and JP phase fragments ungrammatical inside the sentence | Other: translation of a fragment out of its sentence | Each key was translated alone; nobody read the rendered sentence per language | Rows and cache fixed; lesson in `lessons/localization-ui.md` |
| 4 | LOW | A category that lost a class at index time returns success | Logic error (result semantics) | Parity with Harmony was the goal, and Harmony has no skipped class; the new state was not traced to the three callers that read the boolean | Documented on `Apply` and `TryApply`; the behaviour change waits for Mike |
| 5 | LOW | Null-category guard untested | Missing test | The probe assembly held only categorised classes; `tests.md` "Skip-Guard Exhaustion" was not applied to the new `continue` | Probe class and test added; mutation-checked |
| 6 | NIT | Two test names off the naming convention | Convention inconsistency | Written quickly beside the RED test | Renamed |
| 7 | LOW | CHANGELOG mixed two test snapshots | Other: evidence drift | The count was updated after a later commit; the suite line was not | Re-quoted from this pass's run |
| 8 | NIT | Stale line references in the lifecycle doc and the first report | Convention inconsistency | The lines moved when the index wiring was added | Fixed |
| 9 | LOW | Multi-class parity claim in `Apply` untested | Missing test | The first review rejected such a test because compiled metadata order is not controllable; the emitted assembly removed that objection and nobody revisited it | Test added |
| 10 | LOW | Architecture tree missing the two new root files | Convention inconsistency | New files at `Main/` root; the tree is not in any checklist | Added |
| 11 | LOW | #653 body stale | Convention inconsistency | Issue bodies are updated at `/ship` | Owed at `/ship` |

Finding 12 (vanilla `Ok` untranslated in Chinese) was a false positive: the Native Chinese language
files are UTF-16, which a byte grep does not match.

## Root-cause pattern

Findings 1 and 2 share one cause, and finding 3 a second, both in how the localization pipeline was
verified: by presence (the id exists somewhere in the file, the text is not English) instead of by
what the engine renders (the row is where `LoadLanguage` reads it, and the sentence reads correctly
once `{PHASE}` is filled).

## Why each agent missed these

The first review ran before the decisions were implemented, so none of its lenses saw these files.
In this review every lens found finding 1. What each did not find:

- **Agent 1 (Standards):** found 1, 4, 5, 6, 7. Did not read the translations for grammar (not in
  its checks).
- **Agent 2 (Compatibility):** found 1, 4, 8. Grammar is outside its lens.
- **Agent 3 (Efficiency):** found 1 outside its lens; nothing in its lens.
- **Agent 4 (Completeness):** found 1, 3, 7, 9, 10, 11; its N-2 was the false positive, because it
  grepped bytes in UTF-16 files.
- **Agent 5 (Data flow):** found 1, 3, 4, 7, 8. Did not flag the untested null guard (rule 2c covers
  reachability, not test coverage).
- **Agent 6 (Design):** found 1, 3, 4. Test naming and gaps are not its lens.
- **Agent 7 (XML):** found 1, 2, 3. The C# guard and doc drift are outside its lens.
- **Codex:** found 1 and 4. It missed the tool-side root cause, the grammar and the test gaps.
- **The builder:** ran `LanguageFileCoverageTests` as proof of delivery and trusted a green result
  from a test that cannot see placement.

## Feedback memories to codify

None beyond the two lessons appended to `docs/reviews/lessons/localization-ui.md`. The gate makes
finding 1 mechanical, so it needs no memory entry.
