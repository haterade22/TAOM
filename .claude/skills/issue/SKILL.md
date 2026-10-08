---
name: issue
description: Create a GitHub issue for a feature, bug fix, or crash with all required TAOM sections
argument-hint: "[bug|feature|crash] [brief description]"
---

# Create GitHub Issue

Create a GitHub issue following TAOM's mandatory issue format.

## Type: `$ARGUMENTS`

Determine from `$ARGUMENTS` whether this is a `bug`/`crash` fix or a `feature`.

---

## For Bug/Crash Issues

Write the filled template to a file with the Write tool, run `python tools/check_public_text.py <file>`, then run `gh issue create --title "<title>" --label "<bug|feature>" --body-file <file>`. Sections of the bug/crash template:

- `## Problem`: exact error message or symptom, stack trace if available, steps to reproduce.
- `## Analysis`: root cause, what was examined, why it happened, which TaleWorlds internals were involved.
- `## Solution`: what was changed, and why this approach over the alternatives.
- `## Files Changed`: a table of `File | Change`, one line per file.
- `## Testing`: how the fix was verified, unit tests added or updated, manual testing steps.

## For Feature Issues

Same procedure, with these sections:

- `## Motivation`: why this feature exists, what problem it solves, specific examples.
- `## Design`: architecture decisions, extension points used (GameModel, Harmony, CampaignBehavior), alternatives considered.
- `## Implementation`: key files, patterns used, configuration format, IoC registration.
- `## Testing`: test coverage summary, how to verify it works in-game.

## Steps

1. Determine issue type from `$ARGUMENTS`
2. Fill in all sections — do NOT leave placeholder text
3. Write the body to a file, run `check_public_text.py`, then `gh issue create --body-file`
4. Output the created issue URL
5. Reference the issue number in your next commit message

## After Completing Work

Close the issue when done:

```bash
gh issue close <number> --comment "Resolved in [commit hash]. [One-sentence summary of what was done.]"
```
