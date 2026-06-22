# RCA — SkillSpector adoption deep-review (2026-06-22)

`/deep-review` on the SkillSpector adoption changeset (Python auditor + clean-room YARA + tests + doc-routing) ran 22 agents across 5 dimensions with per-finding adversarial verification: **17 findings confirmed, 0 refuted.** One HIGH, four MED, twelve LOW. All actionable findings were fixed in-session and regression-tested (test suite 166 → 185). This RCA records why each shipped and the systemic preventive action.

The changeset is Python + YARA + markdown — no C#. Core agents 1 (ADR/adapter standards) and 2 (Bannerlord API) were N/A by construction; the review was scoped to auditor logic, YARA rules, tests, completeness, and doc-routing.

## Findings

| # | Sev | Bug | Category | Why missed | Action |
|---|-----|-----|----------|------------|--------|
| 1 | MED | `agency-wildcard-tools` regex misses JSON-quoted key (`"tools": ["*"]`); `scan_permissions` misses a bare `"*"` allow-grant → a real wildcard grant slips both scanners | regex coverage | Only the YAML frontmatter form (`allowed-tools: ["*"]`) was tested; the JSON config form was never exercised. The host config has TWO syntaxes (YAML skill frontmatter + JSON settings) and only one was covered. | FIXED: optional quotes in the regex + new `perm-star-wildcard` CRITICAL rule; both forms regression-tested |
| 2 | MED | `promptleak-reveal` misses the `the` article ("reveal **the** system prompt") | regex coverage | Optional filler groups covered `your`/`full`/`system` but not the most common English article. Ported pattern wasn't fuzzed against natural phrasings. | FIXED: added `(?:the\s+)?` |
| 3 | MED | `toolmisuse-rmtree-root` matches ANY absolute path; message said "root-ish path" | message/intent drift | The regex matched the first `/` in any string literal; the message overpromised specificity. | FIXED: message relabeled "absolute path" (absolute-path rmtree in an untrusted skill is legitimately suspicious; self-audit downgrade mutes noise) |
| 4 | MED | 26/36 SkillSpector regex rules + 3/8 AST rules had no firing test (9 untested rules were HIGH) | test coverage | Tests were written as "~2 representative rules per category", chosen for breadth, not to cover every HIGH-severity rule. A regex typo in an untested HIGH rule would surface only on a missed real attack. | FIXED: firing test for every HIGH rule + the 3 AST leaf rules + chain co-emission |
| 5 | HIGH | `test_audit_allow_suppresses_a_line` asserted `assertTrue(res.suppressed or True)` — vacuously true; comment claimed a suppression path that `scan_skillspector` didn't implement | vacuous test + impl gap | The test was written to pass, not to fail on regression; the `or True` is unconditionally true. Underlying: `scan_skillspector` (like `scan_hook`/`scan_injection`) silently `continue`d on suppression without recording it, so the suppressed-summary undercounted. | FIXED: `scan_skillspector` now records suppressions (matching `scan_secrets`/`scan_ast`); test asserts the real entry |
| 6 | LOW | `test_external_fires_full_severity` asserted `!= INFO` (would pass if all HIGH silently became MED) | weak assertion | Same "assert the easy thing" pattern as #5 — the assertion was weaker than the claim it guards. | FIXED: anchored to `any(severity == HIGH)` |
| 7 | LOW | `test_empty_file_list_does_not_crash` vacuous under both yara-present and yara-absent | weak assertion | Asserted only "no CRITICAL"; trivially true in both branches. | FIXED: split on yara importability; asserts exact rule + severity |
| 8 | LOW | Three YARA fail-open paths untested (malformed `.yar`, per-file `yara.Error`, MEDIUM→MED normalization) | test coverage | Fail-open contract was implemented but only the happy path + empty-list were tested. | FIXED: malformed-`.yar` compile-error test + severity-normalization test (per-file `yara.Error` left untested — hard to trigger deterministically; the empty-list + compile-error tests cover the contract) |
| 9 | LOW | `example-skip` broad `never ` hint suppresses malicious phrases mid-sentence on a self-audit ("will **never** fail, inject false memories") | undocumented trade-off | Known/intended (self-audit only; `--external` is unaffected) but no test exposed the boundary. | FIXED: boundary test documents it as an accepted trade-off |
| 10 | LOW | `rogue-modify-skill` FP on skill-authoring docs ("update SKILL.md") under `--external` | regex over-match | The rule intends "skill rewrites itself at runtime" but matches authoring documentation. | ACCEPTED + documented: narrowing trades a FP for a FN, and `--external` is explicitly the higher-FP foreign-vet mode; self-audit downgrades to INFO |
| 11 | LOW | YARA `$beacon` `BeaconType` false-positives on BLE/iBeacon code (CRITICAL) | rule over-breadth | `BeaconType` is a standard BLE field name; the catch-all string wasn't context-anchored. | FIXED: narrowed to `C2Server\s*[=:]` (Cobalt Strike config field); the other 6 C2 strings still cover real frameworks |
| 12 | LOW | YARA `$nmap_sn` covered only `-sS/-sU/-sV/-sn` (missed `-sC`, `-sT`, `-sA`, …) | rule undercoverage | The classic `-sC -sV` detection scan was missed. | FIXED: broadened to `-s[A-Za-z]` |
| 13 | LOW | YARA `$ff_logins` required `logins.json` before `firefox` (forward order only) | rule undercoverage | Reversed phrasing ("firefox … logins.json") missed. | FIXED: added a reversed-order string |
| 14 | LOW | YARA `$py_sock` misses a two-line Python socket shell (`[^\n]` anchors to one line) | rule undercoverage | YARA regex `.`/`[^\n]` does not span newlines; multi-line construction missed. | ACCEPTED + documented: one of six disjuncts; skill artifacts rarely split socket construction across lines; the strong revshell strings (devtcp, nc -e) are single-line |
| 15 | LOW | YARA `$rc_persist` FP on benign `echo 'export PATH=…' >> ~/.profile` | rule over-breadth | The canonical persistence TTP shape also matches benign install-guide setup. | ACCEPTED + documented: adding a command-content anchor would trade the FP for a FN on a function-append payload — worse for a security tool; TAOM self-corpus has zero hits |
| 16 | LOW | No GitHub issue for the adoption (CLAUDE.md requires one per system change) | process | The work proceeded straight from request → implementation without opening a tracking issue. | DEFERRED to user: `/issue` is outward-facing + explicit-intent-only (CLAUDE.md "Never auto-invoke"). Offered; not auto-created. |

(The verifier merged #1's two halves — JSON-key + bare-`*` — into one finding; the original 17-entry confirmed list collapses to the 16 distinct bugs above.)

## Root-cause patterns

Two systemic threads run through the findings:

**A — Pattern rules authored/ported without a per-rule positive+negative test matrix, and without enumerating host-config format variations.** Findings #1, #2, #3, #4, #10–#15 are all "the rule fires on the case I tested, not on the cases I didn't." The host surface has two config syntaxes (YAML skill frontmatter, JSON `settings.json`/`.mcp.json`); #1 shipped because only YAML was tested. Natural-language phrasings (article insertion, word order) are a second uncovered axis. For a *detection* ruleset, an untested rule is a rule that only works by luck.

**B — Tests written to pass, not to fail on regression.** Findings #5 (`or True`), #6 (`!= INFO`), #7 (vacuous both-branch), #8/#9 (paths simply untested) are the same failure: the assertion is weaker than the claim it nominally guards. This is the exact thing `evidence-over-claims.md` warns about applied to test code — a green test that proves nothing is worse than no test, because it reads as coverage.

The one genuinely-HIGH finding (#5) sits at the intersection: a vacuous assertion masking a real implementation gap (suppressions not recorded).

Only finding #1 had real security impact (a wildcard grant slipping TAOM's own self-audit); the rest are rule-quality / test-quality on an advisory tool. But #1 is exactly the class this tool exists to catch, so it's the load-bearing fix.

## Why each agent missed these (at authoring time)

- **My own tests (authoring):** wrote firing tests for ~2 rules/category as "representative" and one knowingly-weak `or True` placeholder. Both are pattern B. The fix is the per-rule matrix now in place.
- **The prior adversarial doc-verification workflow (this session):** scoped to DOCS only (routing accuracy, links, consistency). It could not catch regex/test bugs in the code — correct scoping, wrong layer for these findings. It did catch and fix the one doc bug (ast-exec/yara wrongly coupled to `--external`).
- **The deep-review workflow:** caught all 17 — that's the point of running it. The RCA question is "why ship," answered by patterns A and B above, not "why missed by review."

## Preventive action codified

- **Memory (new):** `feedback_detection_ruleset_per_rule_test_matrix.md` — when authoring/porting a pattern-detection ruleset (regex, YARA, AST), every rule needs ≥1 positive firing test, and the host's config format variations (for Claude config: YAML frontmatter AND JSON settings) are a mandatory coverage axis. Generalizes pattern A.
- **Memory (new):** `feedback_no_vacuous_test_assertions.md` — `assertTrue(x or True)`, `assert != <one-of-many>` where the claim is `== <specific>`, and assertions that hold in every branch are banned; a test must be able to fail. Generalizes pattern B; ties to `evidence-over-claims.md`.
- **Auditor:** the wildcard-coverage gap (#1) is closed + regression-tested (`WildcardCoverageTests`); future Claude-config audits catch a bare `*` or JSON-quoted wildcard grant.
- Both the deep-review changeset author (me) and any future SkillSpector-pattern extension should run the auditor against a synthetic foreign skill via `--external` AND assert per-rule firing, not just "it runs."

## Status

All HIGH + MED + actionable LOW findings fixed and regression-tested (185 tests pass; self-audit exit 0). Two LOW YARA findings (#14, #15) and one LOW regex finding (#10) consciously ACCEPTED with documented rationale (narrowing trades a false-positive for a false-negative, the wrong trade for a security tool). One LOW process finding (#16, GitHub issue) deferred to the user per the outward-facing-action rule.
