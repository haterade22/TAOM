---
name: scope-check
description: Assess whether a proposed change fits the current work context or represents scope creep
argument-hint: [description of proposed change]
---

# Scope Check Assessment

Evaluate whether the following proposed change aligns with the current work context.

## Proposed Change: `$ARGUMENTS`

## Assessment Steps

1. **Read recent CHANGELOG entries** — Check `CHANGELOG.md` for the last 2-3 dated sections to understand recent work themes and feature areas.

2. **Review recent commit history** — Run `git log --oneline -15` to see the pattern of recent work. Note which feature areas, file paths, and types of changes dominate.

3. **Check in-progress work** — Run `git diff --name-only` and `git diff --staged --name-only` to see what is actively being modified right now.

4. **Identify the current work theme** — Based on the above, summarize:
   - What feature area(s) are currently active?
   - What kind of work is being done (new feature, bugfix, refactor, data entry)?
   - Are there open threads of work that are incomplete?

5. **Assess the proposed change** — Evaluate `$ARGUMENTS` against the current context:

   **Natural Extension** (GREEN): The change...
   - Directly relates to files currently being modified
   - Fixes a bug discovered during current work
   - Completes an incomplete feature from recent commits
   - Is a small improvement in the same feature area

   **Adjacent Work** (YELLOW): The change...
   - Touches a related but different feature area
   - Could be deferred without impacting current work
   - Requires context-switching but shares some code paths

   **Scope Creep** (RED): The change...
   - Is in a completely unrelated feature area
   - Requires significant new infrastructure
   - Would interrupt incomplete in-progress work
   - Introduces new dependencies or architectural patterns

6. **Provide recommendation** — State the assessment (GREEN/YELLOW/RED) with reasoning. If YELLOW or RED, suggest:
   - Whether to defer (and what to finish first)
   - Whether to create a GitHub issue to track it instead
   - A minimal version that could be done now vs. later

## Important

This is a READ-ONLY assessment. Do not make any code changes. Only analyze and advise.
