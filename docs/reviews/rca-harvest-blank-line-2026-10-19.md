# RCA: harvest_literal_loc_keys.py added a blank line before </strings> (#919, 2026-10-19)

## Top-line

Every `--apply` run that wrote rows left one more blank line before `</strings>` in the target strings
file. `insert_rows` walked back over the trailing blank lines, then inserted its block (which ends in
its own blank line) in front of them with `lines[at:at] = block`. The fix replaces the blank run:
`lines[at:close] = block`. The bug had been trimmed by hand in at least five reviews before anyone
fixed the tool, and it was filed three times: #781 (2026-10-09), #919 and #922 (both 2026-10-19, six
minutes apart, from two sessions). #702 also lists it beside a separate raw-newline defect.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | LOW | One extra blank line per harvest that wrote rows. | Off-by-one slice | The comment said "drop the trailing blank line" and the code inserted instead of replacing. No test covered the tool. | Fixed; `tools/tests/test_harvest_literal_loc_keys.py` added. |
| 2 | LOW | The first regression test used only the one-blank-line shape, so a wrong fix that overwrites the last row (`lines[close - 1:close]`) also passed. | Test covers one input shape | The test was written from the bug report's example. | The test now runs 0, 1 and 2 blank lines, LF and CRLF, both root shapes, plus whitespace-only lines, and compares the whole output text. It fails on the old code (13 sub-cases) and on that mutant (9). Lesson in `lessons/build-tooling-workflow.md`. |
| 3 | LOW | The same bug had three open issues. | Process | `/issue` files with `gh issue create` and has no step that searches for an existing issue. | Lesson below; a search step in the `/issue` skill is a follow-up on Mike's word (harness edit). |

## Follow-ups (pre-existing, not in this change)

- `insert_rows` writes without parsing the result and picks the newline style by presence, not by
  majority (`tools/README.md` "XML I/O convention"). No current target triggers either; #702 already
  plans to rework this write path and can take both.
- A raw `\n` in a C# default still lands in the attribute; #702 tracks it.
- Close #781 and #922 as duplicates of #919, and trim #702 to its raw-newline half, on Mike's word.
