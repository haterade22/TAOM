# Architecture Decision Records

Quick reference index for TAOM ADRs. See individual files for complete details with examples and migration strategies.

## ADR Index

| ADR | Title | Summary | Status | Priority |
|-----|-------|---------|--------|----------|
| [001](./001-xml-config.md) | XML Configuration Format | All config files use XML (no JSON) | Accepted | Standard |
| [002](./002-thin-entry-points.md) | Thin Entry Points Pattern | Entry points <150 lines, delegate to services | Accepted | **Mandatory** |
| [003](./003-no-regions.md) | No Code Regions | No `#region` directives, use class decomposition | Accepted | Standard |
| [004](./004-no-obsolete.md) | No Obsolete Markers | Migrate all usage sites immediately, no deprecation | Accepted | Standard |
| [005](./005-no-preprocessor-directives.md) | No #if DEBUG in App Code | Use DI for environment-specific code (except IoC.cs) | Accepted | Standard |
| [007](./007-adapter-pattern.md) | Adapter Pattern for Sealed Classes | Services use adapters, NOT sealed TaleWorlds types | Accepted | **Mandatory** |
| [008](./008-testability-requirements.md) | Testability Requirements | Business logic must be 100% unit testable | Accepted | **Mandatory** |
| [009](./009-self-documenting-code.md) | Self-Documenting Code Standards | No inline comments, ELI5 summaries, code reads like English | Accepted | Standard |

## Quick Rules Reference

### Critical Enforceable Rules

**All Projects:**
- No `#region` directives (ADR-003)
- No `[Obsolete]` attributes (ADR-004)
- No `#if DEBUG` in application code (ADR-005) - Only in IoC.cs
- No comments inside methods (ADR-009)
- XML summaries must be ELI5-simple (ADR-009)

**Mod Services:**
- Services MUST use adapter interfaces (IHeroAdapter, etc.) (ADR-007)
- Services MUST NOT accept sealed types (Hero, Settlement, etc.) (ADR-007)
- Entry points <150 lines, delegate to services (ADR-002)
- Use XML for game config, not JSON (ADR-001)

## When to Read Full ADRs

- **New Features**: Read [ADR-002](./002-thin-entry-points.md), [ADR-007](./007-adapter-pattern.md)
- **Refactoring**: Read [ADR-003](./003-no-regions.md), [ADR-004](./004-no-obsolete.md)
- **XML Configuration**: Read [ADR-001](./001-xml-config.md)
- **Service Design**: Read [ADR-007](./007-adapter-pattern.md) (adapter pattern), [ADR-005](./005-no-preprocessor-directives.md) (environment handling)
