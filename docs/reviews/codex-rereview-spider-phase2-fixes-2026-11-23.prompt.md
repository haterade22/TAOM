# Codex re-review: the fixes to the spider phase 2 review (2026-11-23)

You are an independent, adversarial reviewer. Read AGENTS.md and `.ai/roles/reviewer.md` first. Review only; change
no file. Report P1 (a wrong result, data loss, or a gate that passes a wrong state), P2, P3, each with file, line,
the failing input and what you read. Missing engine evidence stays UNVERIFIED.

## Scope

The uncommitted work in this checkout (base `435e5644a`). Your first pass is
`docs/reviews/raw/codex-prereview-spider-phase2-2026-11-23.md`; its ten points and the four Claude lenses' points
are listed with their fixes in section 4 of `docs/reviews/rca-spider-key-times-slide-capsule-2026-11-23.md` (R1 to
R11). Judge the FIXES. For each of your earlier P1 points say: fixed, partly fixed (with the input that still
fails), or not fixed. Then look for a defect a fix added.

The fixes, by file:

- `tools/elephant_write_masters.py`: `new_payload(keep_times)`, `rekey(raw, loop, sources)`, the `--sources`
  argument, the three refusals in `--rekey --apply` (a folder inside a module, the master's own folder, no
  sources), the temp file and `os.replace`.
- `tools/check_gait_stride.py`: `key_span`, `range_findings` (no master is a finding), the master list in `main`.
- `tools/verify_mount_assets.py`: the finite, side and height checks in `capsule_findings`, `body_findings`,
  CHECK 4 through it.
- `tools/bind_spider_clips.py`: `MOVEMENTS` (the stand pace's `none/none` row last), `hidden_rows`, the overlay
  rider rows and the hidden rows in `planned_problems`.
- `tools/audit_mount_parity.py`: `STRICT_VERBS`. `tools/data_gates.py`: the `spider-body` row.
- `tools/gen_elephant_anim_clips.ps1`: `Get-Planned`, `Get-FieldDiffs`, the in-place predicate.
- `Main/Features/Spider/SpiderSlideMeter.cs` (`JudgedSeconds`; a slow reading is left out), `SpiderTrialRules.cs`
  (`JudgedAtLeastSeconds`), `Client/Features/Spider/Trial/CommandTrialHost.Spider.cs`, `CommandTrialScript.Spider.cs`.
- Their tests under `tools/tests/` and `TAOM.Tests/Features/Spider/`.

## Questions

1. `hidden_rows`: does its filing match the loader you can read in
   `docs/features/spider.md` (the wildcard bullet)? Name a table it passes that the loader would still shadow, or
   one it fails wrongly (two feet, a `front_left` row, a row with direction and turn both set).
2. `rekey`: is there still an input of the old writer's form it corrupts, or a migrated master it changes again?
   Does `rewrite` with `keep_times` break the case it was changed for on 2026-11-23 (a loop clip written over an
   old master with `--resize`)?
3. `SpiderSlideMeter`: with slow readings left out, can unrelated fast spans of one mount, minutes apart, be
   joined into one window? Is `judged >= 5` reachable in a run with one rider, and is a floor of 5 s meaningful
   against 147 s measured?
4. `Get-FieldDiffs`: a float compared as a string: can two equal records differ in text, or two different ones
   agree? Does `Get-Planned` leave a field unset that the old writer set?
5. `capsule_findings`: any capsule that does not hold the body and still passes.
6. Any test added by these fixes that still passes with its production code removed.

Python tests need numpy and lz4; if they are missing, say so and reason from the source. Never run `./build.ps1`
and never pass `--apply`.
