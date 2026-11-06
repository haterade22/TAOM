# Architecture Decision Records

Quick reference index for TAOM ADRs. See individual files for complete details with examples and migration strategies.

## ADR Index

| ADR | Title | Summary | Status | Priority |
|-----|-------|---------|--------|----------|
| [001](./001-xml-config.md) | XML Configuration Format | Engine data is XML; a feature's own tuning file may be validated JSON (amended 2026-11) | Accepted | Standard |
| [002](./002-thin-entry-points.md) | Thin Entry Points Pattern | Entry points <150 lines, delegate to services | Accepted | **Mandatory** |
| [003](./003-no-regions.md) | No Code Regions | No `#region` directives, use class decomposition; a service may be split into partial files by concern (2026-10-21 amendment) | Accepted | Standard |
| [004](./004-no-obsolete.md) | No Obsolete Markers | Migrate all usage sites immediately, no deprecation | Accepted | Standard |
| [005](./005-no-preprocessor-directives.md) | No #if DEBUG in App Code | Use DI for environment-specific code (except IoC.cs) | Accepted | Standard |
| 006 | _(Revoked — never filed)_ | Number reserved; no ADR was written for this slot | Revoked | — |
| [007](./007-adapter-pattern.md) | Adapter Pattern for Sealed Classes | Services use adapters, NOT sealed TaleWorlds types | Accepted | **Mandatory** |
| [008](./008-testability-requirements.md) | Testability Requirements | Business logic must be 100% unit testable | Accepted | **Mandatory** |
| [009](./009-self-documenting-code.md) | Self-Documenting Code Standards | Comments on a keep-list only, one-sentence summaries, code reads like English | Accepted | Standard |
| [010](./010-knowledge-base-architecture.md) | Knowledge-Base Architecture | `docs/INDEX.md` + linter + backlinks + `raw/research/` layers; keep markdown links over Obsidian wikilinks | Accepted | Standard |

## Quick Rules Reference

### Critical Enforceable Rules

**All Projects:**
- No `#region` directives (ADR-003)
- No `[Obsolete]` attributes (ADR-004)
- No `#if` outside IoC.cs (ADR-005), gated by `BannedConstructsGateTests`

**Mod Services:**
- Services MUST use adapter interfaces (ICareerHeroAdapter, etc.) (ADR-007)
- Services MUST NOT accept sealed types (Hero, Settlement, etc.) (ADR-007)
- Entry points <150 lines, delegate to services (ADR-002)
- Use XML for engine data; a feature's own tuning numbers may be validated JSON (ADR-001)

### Not gated

Review applies these (lens 9a); no test or hook checks them:
- A comment stays only on the keep-list: a licence or attribution; behaviour forced by the engine or a vendor, naming that constraint; a public contract doc; a marker a gate reads (`swallow-ok`). Any other comment becomes a name, a test or nothing (ADR-009).
- A summary is one sentence by default. It may run longer only to record an engine fact, a line citation or why a gate exists (ADR-009).

## When to Read Full ADRs

- **New Features**: Read [ADR-002](./002-thin-entry-points.md), [ADR-007](./007-adapter-pattern.md)
- **Refactoring**: Read [ADR-003](./003-no-regions.md), [ADR-004](./004-no-obsolete.md)
- **XML Configuration**: Read [ADR-001](./001-xml-config.md)
- **Service Design**: Read [ADR-007](./007-adapter-pattern.md) (adapter pattern), [ADR-005](./005-no-preprocessor-directives.md) (environment handling)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/agent-operating-manual.md](../ai-includes/agent-operating-manual.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)

<!-- backlinks-end -->
