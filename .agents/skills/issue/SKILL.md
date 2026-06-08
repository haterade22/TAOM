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

Run this command after filling in the template:

```bash
gh issue create \
  --title "[one-line description of the bug/crash]" \
  --label "bug" \
  --body "$(cat <<'EOF'
## Problem

[Exact error message or symptom. Stack trace if available. Steps to reproduce.]

## Analysis

[Root cause. What was examined. Why it happened. What TaleWorlds internals were involved.]

## Solution

[What was changed. Why this approach was chosen over alternatives.]

## Files Changed

| File | Change |
|------|--------|
| `path/to/file.cs` | One-line description |

## Testing

[How the fix was verified. Unit tests added/updated. Manual testing steps.]
EOF
)"
```

## For Feature Issues

```bash
gh issue create \
  --title "[one-line description of the feature]" \
  --label "feature" \
  --body "$(cat <<'EOF'
## Motivation

[Why this feature exists. What problem it solves. Specific examples.]

## Design

[Architecture decisions. Extension points used (GameModel, Harmony, CampaignBehavior). Alternatives considered.]

## Implementation

[Key files. Patterns used. Configuration format. IoC registration.]

## Testing

[Test coverage summary. How to verify it works in-game.]
EOF
)"
```

## Steps

1. Determine issue type from `$ARGUMENTS`
2. Fill in all sections — do NOT leave placeholder text
3. Run the `gh issue create` command
4. Output the created issue URL
5. Reference the issue number in your next commit message

## After Completing Work

Close the issue when done:

```bash
gh issue close <number> --comment "Resolved in [commit hash]. [One-sentence summary of what was done.]"
```
