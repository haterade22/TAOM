# ADR-005: No Preprocessor Directives in Application Code

**Status**: Accepted
**Date**: 2025-11-02
**Context**: Build configuration management and testability

## Decision

Preprocessor directives (`#if`, `#ifdef`, `#ifndef`, `#else`, `#endif`) are prohibited in all application code. Environment-specific behavior MUST be handled through dependency injection with different service implementations. Preprocessor directives are ONLY allowed in `IoC.cs` for build-specific service registration.

## Rationale

Code inside a conditional block is untestable in the other build configuration, so a release bug can sit in a debug-only path unseen.

## Enforcement

Enforcement: `BannedConstructsGateTests`.

### Amendment (2026-11-03): reference sections removed

The Examples, Common Use Cases, Exceptions, Migration Strategy, Consequences and Testing Strategy sections left. They taught a strategy-pattern interface pair for a case TAOM has none of, and listed enforcement (an analyzer, architecture tests, a CLAUDE.md link) that did not exist. The Decision, including the `IoC.cs` exception for build-specific registration, is unchanged, and the gate named above holds it.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/adrs/README.md](./README.md)
- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
