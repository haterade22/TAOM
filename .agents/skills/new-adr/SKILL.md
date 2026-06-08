---
name: new-adr
description: Scaffold a new Architecture Decision Record, auto-numbered from existing ADRs, with context pre-filled from recent git history and CHANGELOG.
argument-hint: "[decision-name e.g. use-singletons-for-services]"
---

# New ADR

Scaffold a numbered ADR at `docs/adrs/` with context pre-filled from the current session's work.

**Decision name:** `$ARGUMENTS` (required — becomes the slug and title)

## Step 1: Determine Next ADR Number

List `docs/adrs/` and find the highest-numbered ADR file:

```bash
ls docs/adrs/ | grep -E '^[0-9]+-' | sort -n | tail -5
```

Extract the highest number (e.g., `009-self-documenting-code.md` → 9). Next ADR = 9 + 1 = **010**.

Zero-pad to 3 digits: `010`, `011`, etc.

## Step 2: Gather Context

Run these to pre-fill the Context section:

```bash
# Recent decisions/changes for context
git log --oneline -10

# Current session's work
git diff --name-only HEAD 2>/dev/null | head -20

# Latest CHANGELOG entry (first 30 lines)
head -30 CHANGELOG.md
```

## Step 3: Read Template Format

Read `docs/adrs/000-template.md` to verify the exact heading/formatting style. Do NOT guess — TAOM ADRs use inline bold fields (`**Status**:`, `**Date**:`) not separate headings.

## Step 4: Generate the ADR File

Write to `docs/adrs/[NNN]-[decision-name].md` using this exact format (matching existing ADRs):

```markdown
# ADR-[NNN]: [Human-Readable Title from $ARGUMENTS]

**Status**: Proposed

**Date**: [today's date YYYY-MM-DD]

**Priority**: Standard

## Context

[2-4 sentences describing the problem or situation that motivated this decision.
Pre-fill with context from: git log themes, CHANGELOG entry, changed files.]

## Decision

[TBD — describe the chosen approach in 1-3 sentences once decided]

## Consequences

### Positive
- [TBD]

### Negative
- [TBD]

### Neutral
- [TBD]

## Alternatives Considered

### Alternative 1: [Name]
- **Pros**:
- **Cons**:
- **Why rejected**:

## Examples

### Good (Follows This ADR)

```csharp
// Example of code that follows this ADR
```

### Bad (Violates This ADR)

```csharp
// Example of code that violates this ADR
```

## Migration Strategy

[How to apply this ADR to existing code, if applicable]

## References

- [Links to relevant discussions, external docs, or commit SHAs]

## Related ADRs

- [ADR-NNN]: [Title] — [brief relationship]
```

## Step 5: Report

Print: `Created docs/adrs/[NNN]-[decision-name].md`

Remind the user to:
1. Fill in the Decision and Consequences sections
2. Add code examples (Good/Bad)
3. Update `docs/adrs/README.md` with the new entry
4. Set `**Priority**: Mandatory` if this is a NEVER-VIOLATE rule
