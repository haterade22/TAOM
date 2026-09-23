# Rules Catalog — `.claude/rules/`

> What each rule is for. A rule's scope is its own `paths:` frontmatter, the only source of truth;
> this table no longer repeats the globs, because 11 of 16 copies had drifted by 2026-09-23. To see a
> scope: `grep -A14 '^paths:' .claude/rules/<rule>.md`. Where knowledge goes, and why these tiers:
> [ADR-011](../adrs/011-knowledge-delivery-tiers.md).

## Load convention

A rule with `paths:` loads when Claude reads a matching file in the repo; a rule without it loads in
every session and subagent. The details and their sources (never for a file outside the repo, the
`/compact` behaviour, a frontmatter that fails to parse) are in `.claude/rules/harness-facts.md`
"Rule loader (memory) semantics" and "Context loading".

## Always-load rules (no `paths:`)

_(6 rules; `python tools/lint_docs.py --context-budget-json` gives their sizes. `harness-facts.md` became path-scoped on 2026-09-23.)_

| Rule | Content |
|------|---------|
| `environment-failures.md` | Report environment failures (missing tools, paths, MCP down) and stop; don't fix infra. Check which machine you are on first. |
| `evidence-over-claims.md` | Verify a review finding before implementing it; no performative agreement; no "done" without fresh output (a subagent's self-report doesn't count); never state an unread fact. |
| `output-style.md` | Part 1 (chat): open with scrutiny, not agreement; tag every response `[Certain]`/`[Likely]`/`[Guessing]`; one step per message in a live session. Part 2 (produced prose): no em or en dash, no AI-writing tells; boldface and tables stay. |
| `simplicity-criterion.md` | Keep-or-reject matrix: a tiny gain with added complexity is rejected; a deletion that holds parity always wins. |
| `think-before-coding.md` | State load-bearing assumptions before the first edit and ask when one is uncertain; don't ask on trivial work; make the goal testable; reuse-before-write ladder. |
| `working-discipline.md` | Autonomous-loop stewardship (continue established work, never stop to ask permission) and edit-scope discipline, including during a review gate. |

## Path-scoped rules (load when a matching file is read)

| Rule | Content |
|------|---------|
| `adapters.md` | Adapter pattern, research-first |
| `csharp-architecture.md` | Layer stack, IoC lifetimes, non-negotiable rules, stale-file re-read, mission-scope agent handles and threads |
| `csharp-patterns.md` | Hook, Strategy and GameModel patterns, quick reference |
| `external-skill-ports.md` | Authoring a skill from scratch, and the per-field checklist for porting one from an external suite |
| `gamemodels.md` | GameModel override pattern, base-class rules, registration |
| `gui-ui.md` | Sprite verification, UIExtenderEx safety, ViewModel bindings |
| `harmony-patches.md` | Patch types, thin entry points, thread-local state, the shared deferred `MovementOrder` category |
| `harness-facts.md` | Verified Claude Code load semantics, hook lifecycle and visibility, frontmatter schema, with sources |
| `hook-authoring.md` | Hook conventions: sibling-mirroring, the two-stage git-commit matcher, amend handling, fail open but never silent, timeouts, prove a gate live, log rotation |
| `moduledata-validation.md` | Run `python tools/validate_moduledata.py` before committing ModuleData; schemas are the source of truth; the XML I/O convention for data-writing scripts |
| `native-cpp-ports.md` | The 6-point C++ port audit (hot-path logging, SEH specificity, offsets, atomics, SRWLock, C++ review) |
| `provenance.md` | Name the third-party source and state its license; the derivation vocabulary; the register as the record |
| `tests.md` | TDD, naming, AAA pattern, coverage |
| `troops.md` | Troop checklist, races, party templates, save compatibility |
| `vanilla-data-comparison.md` | Compare against the installed vanilla before modifying mirrored data; stale references crash |
| `xml-data.md` | NPC naming, region codes, culture ids, equipment roster schema, formatting |
| `xslt.md` | XSLT passthrough, SandBoxCore reference |

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
