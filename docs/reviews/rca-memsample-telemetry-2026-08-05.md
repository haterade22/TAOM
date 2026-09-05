# RCA — [MemSample] telemetry + minidump tooling deep review (2026-08-05)

**Scope:** #386 `[MemSample]` telemetry (C# + Python halves) and #387 `native_crash_triage.py --dump`,
built by three parallel workflow lanes, reviewed by 6 agents (5 core + Python-tooling).
**Outcome:** 2 HIGH, 2 MED, 3 LOW, 1 named test gap — all fixed same-session; both suites green after
(C# filter 138/138, Python 361). The cross-language threshold drift (HIGH #1) was found
independently by two agents with different counterexamples — the redundancy paid for itself.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| 1 | HIGH | C# `WarnThresholdMb` uses integer-floor division (`limit * 10 / 100`); the Python mirror `_headroom_low` used exact cross-multiplication (`headroom * 100 < limit * percent`). They disagree in the ~1 MB band below the true 10% line whenever the commit limit isn't a multiple of 10 — i.e. on essentially every real machine. Empirically: `limit=20481, headroom=2048` → C# healthy, Python "MEMORY PRESSURE". | cross-language contract | The twin-pin strategy pinned the **line-format** contract with literal tests but not the **decision** contract. The constants (2048/10/512) were mirrored and commented "numerically identical", yet the *arithmetic* (truncation semantics) had no boundary test — every existing test used limits divisible by 100 or cases decided by the 2048 floor. | Boundary pins added in BOTH languages at the truncation edge (`limit=31646`: headroom 3164 healthy / 3163 low; `limit=20481`: headroom 2048 healthy). Lesson appended (testing-qa): a mirrored decision function is pinned by a **non-round boundary value**, not by mirroring its constants. |
| 2 | HIGH | `run_dump` caught only `ValueError` around the minidump parse; every truncated-file `struct.unpack` raises `struct.error` (which is NOT a `ValueError` subclass) — a corrupt/truncated dump produced a raw traceback instead of a message. `commit_summary` also trusted the stream header's `szentry` blindly. | error taxonomy in binary parsers | The author assumed `struct.error` inherits `ValueError`; the negative tests covered bad-signature and missing-file but never truncation (the failure class the tool exists to meet — torn crash bundles). | Broadened both catch sites to `(ValueError, struct.error, OSError)`; `szentry < 48` / absurd-count guard + short-read break in `commit_summary`; 3 malformed-dump tests + 5 graceful-degradation tests added. Lesson appended (build-tooling-workflow): enumerate the parser library's REAL exception types and test truncation at each stream boundary. |
| 3 | MED | `MemStats()` built its string with `+=` (one intermediate allocation per call, 5 main-thread phase points per battle). | allocation | Lane agent style miss; trivial. | Restructured to conditional single interpolation. No systemic lesson — cost was already bounded and measured. |
| 4 | MED | Five graceful-degradation paths in `--dump` (missing MemoryList, RSP outside ranges, no exception stream, thread not found, address not in any module) existed in code but had zero tests — a refactor could silently turn any of them into a crash. | test coverage | Lane C tested the happy path + 2 degrade paths and stopped. | One test per named path (5 added). Folded into #2's lesson. |
| 5 | LOW | MCM `RequireRestart` defaults `true`; the new interval slider's hint text promises "no restart needed" — the UI badge would contradict it. | MCM UX | `BaseSettingPropertyAttribute`'s ctor default is non-obvious and the settings file's own precedent omits the flag everywhere. | `RequireRestart = false` set on both new properties (both are live-read per poll). |
| 6 | LOW | Module docstring claimed old logs "behave byte-identically"; the `--json` payload now always carries a `memory` key (null for old logs). | doc precision | Written from the report-text perspective; JSON not re-checked against the claim. | Docstring now states the report text is byte-identical and names the one JSON delta. |
| 7 | LOW | A torn/partial `[MemSample]` line (realistic at crash time) is silently skipped — intended, but untested. | test coverage | Intended behavior relied on incidental regex non-match. | Regression test added pinning skip-without-crash-or-count. |
| 8 | GAP | The 5 MemStats-bearing phase lines' trailing tokens had no Python fixture — the exact regex class (`_EQUIP_BEGIN_RE`) broke from a near-identical trailing-token addition on 2026-08-02. | test coverage | Tolerance was verified by reasoning over the regexes, not by a fixture; the prior incident was even cited in-source. | Two fixture tests added (verdict-kind equality with/without suffix; phase list + scene extraction under suffix). |

## Root-cause pattern

Findings 1, 2, and 8 share one theme: **the contract that was pinned is narrower than the contract
being relied on.** The line FORMAT was pinned by twin literals while the threshold ARITHMETIC ran
unpinned; exception handling was written against an assumed taxonomy instead of the library's real
one; regex tolerance was argued from inspection where a prior incident already proved fixtures are
the only durable pin. The generalization: when a review says "X is correct by construction/inspection",
ask what TEST pins X — especially at boundaries and in the other language of a cross-language pair.

## Why each agent missed / caught these

- **Standards (A1), Completeness (A4), API (A2), Efficiency (A3):** #1 requires comparing arithmetic
  across languages — outside all four scopes by design. A2 did surface the `RequireRestart` mismatch
  (#5) via decompiling the vendored MCM attribute — decompile-the-dependency again beat reading names.
- **Data Flow (A5):** caught #1 (empirical counterexample + 468-combination scan) and named gap #8 —
  the "trace the value across the boundary" rule working exactly as intended.
- **Tooling (A6):** caught #1 independently (different counterexample at the pinned limit 31646),
  #2 (reproduced both truncation crashes), #6, #7. The 2026-05-28 decision to add a dedicated
  tooling agent for Python changesets is re-validated — the 5 core agents would have shipped #2.
- The implementing lanes missed #1 because each lane satisfied its OWN tests; the twin literal pins
  they were told to share covered format only. The pinned-contract brief should have included a
  pinned boundary VALUE table, not just pinned strings — that is the transferable fix for
  parallel-lane briefs.

## Feedback memories to codify

None new — both lessons fit existing category files (appended below); the parallel-lane brief
improvement is recorded here and in the lessons entries.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
