---
name: investigate
description: Six-phase root-cause debugging for crashes, build failures, or "why is this broken" reports. Iron Law - no fixes without root cause. Auto-engages /freeze.
allowed-tools:
  - Bash
  - Read
  - Grep
  - Glob
  - Edit
  - Write
  - WebSearch
  - AskUserQuestion
hooks:
  PreToolUse:
    - matcher: "Edit"
      hooks:
        - type: command
          command: "bash ${CLAUDE_PROJECT_DIR}/.Codex/skills/freeze/check-freeze.sh"
    - matcher: "Write"
      hooks:
        - type: command
          command: "bash ${CLAUDE_PROJECT_DIR}/.Codex/skills/freeze/check-freeze.sh"
---

# /investigate — Systematic Root-Cause Debugging

Adapted from [garrytan/gstack/investigate](https://github.com/garrytan/gstack/tree/main/investigate). Reuses our existing `/freeze` hook so debug scope is auto-locked. Aligns with our `feedback_root_cause_mandatory.md` and `feedback_simpler_fix_first.md` memories.

## Iron Law

**NO FIXES WITHOUT ROOT-CAUSE INVESTIGATION FIRST.**

Fixing symptoms creates whack-a-mole debugging. Every fix that doesn't address root cause makes the next bug harder to find. Find the root cause, then fix it. This is a BLOCKING GATE, not optional.

---

## Phase 1: Root-Cause Investigation

Gather context before forming any hypothesis.

1. **Collect symptoms.** Read error messages, stack traces, save-load logs, Bannerlord crash dumps. If the user hasn't given enough context, ask ONE question at a time via `AskUserQuestion`.

2. **Read the code.** Trace from symptom back. Use `Grep`/`find_symbol` (Serena MCP) to find references; `Read` to understand logic.

3. **Check recent changes.**
   ```bash
   git log --oneline -20 -- <affected-files>
   ```
   Was this working before? What's in the diff?

4. **Reproduce.** Can you trigger the bug deterministically? Bannerlord-specific:
   - Save-load loop (load → action → save → load — does state corrupt?)
   - Fresh campaign vs migrated save?
   - Specific culture/race/feature interaction?
   - Mission start vs campaign map only?

5. **Check the simpler-fix-first list.** Per `feedback_simpler_fix_first.md`: before investigating engine internals, check whether the bug is in:
   - `skins.xml` / `monsters.xml` / `action_sets.xml` (race + animation issues)
   - `taom_*.xml` data files (XML schema or value problems)
   - XSLT transforms (vanilla attribute drift)
   - `IoC.cs` registration order (service-locator hot path)

6. **Verify TaleWorlds API signatures.** Per AGENTS.md, `E:\Decompiled_Bannerlord\` is **v1.4** but the installed game is v1.3.15. Before assuming a method exists or has a particular signature, run `ilspycmd` against the installed DLLs at `%BANNERLORD_GAME_DIR%\bin\Win64_Shipping_Client\`. If decompiled and installed disagree, trust installed. Use the `/research` skill if the surface is large.

Output: **"Root-cause hypothesis: ..."** — a specific, testable claim about *what* is wrong and *why*.

---

## Phase 2: Scope Lock (auto-freeze)

Once you have a hypothesis, lock edit scope to the affected directory. Unlike a casual edit-restrict, this skill's own PreToolUse hooks (declared in this SKILL.md's frontmatter) ARE active while `/investigate` is running, so writing the state file here will fire them — that's why this skill can auto-engage freeze without separately invoking `/freeze`.

```bash
SCOPE="<e.g. Main/Features/CareerSystem/>"

STATE_DIR="${CLAUDE_PROJECT_DIR}/.Codex/tmp/freeze"
mkdir -p "$STATE_DIR"
echo "$SCOPE" > "$STATE_DIR/freeze-dir.txt"
echo "Debug scope locked to: $SCOPE"
```

Tell the user: *"Edits restricted to `<scope>` for this debug session. Outside-scope edits will be hard-blocked. Run `/unfreeze` to release."*

If the bug genuinely spans the whole repo (e.g., a shared adapter contract change), skip the lock and note why.

**Important difference from feature-builder:** Other agents/skills that don't have their own `hooks:` frontmatter cannot auto-engage freeze by writing the state file alone — the file is inert outside an active hook-bearing skill. They must explicitly invoke `/freeze` instead. `/investigate` is special because it declares its own copy of the freeze hook in this SKILL.md.

---

## Phase 3: Pattern Match

Check whether the bug matches a known TAOM/Bannerlord failure pattern before writing any fix:

| Pattern | Signature | Where to look |
|---------|-----------|---------------|
| **Harmony patch type-load** | `TypeLoadException` at startup, mod fails before main menu | Patch target signature mismatch (1.3.15 vs 1.4 decompile drift) — verify with `ilspycmd` |
| **Harmony patch silent skip** | Patch loads, but behavior doesn't change in-game | Wrong target method, prefix returning false unexpectedly, manual patch matching wrong overload |
| **GameModel cast** | `InvalidCastException` in DefaultXxxModel call | New override returning wrong type, or base call missing |
| **Service-locator at startup** | `IoC.Resolve` returns null, NullReferenceException in patch | Service registered after CampaignBehavior runs — registration order bug. See `feedback_no_service_locator_in_services.md` |
| **VM string crash** | `{=key}Text` shows literal in UI, or NRE on property access | Missing `TextObject().ToString()` per `feedback_localization_textobject.md` |
| **Sprite missing / atlas corruption** | Items render in underwear, sprite renders blank/black | Wrong sprite ID in XML, or oversized PNG corrupting atlas — see `feedback_sprite_dimensions.md` |
| **Save-load state loss** | Buff or feature works once, lost after save+load | Missing `SyncData`, or storing non-savable type. See `feedback_lifecycle_state_matrix.md` for buff lifecycle paths |
| **Race / culture XML wiring** | Custom culture characters appear vanilla | XSLT didn't pass through new attribute, or XML missing required field |
| **Collection-API self-inclusion** | OffByOne in counts, suspicious behavior near 'self' | TaleWorlds collection iter includes the caller. See `feedback_collection_api_inclusion.md` |
| **Engine-scale property** | `AgentDrivenProperties` change has no effect or wrong magnitude | Missed downstream consumer / clamp / multiplier. See `feedback_engine_scale_research.md` |

Also check:
- `git log --all --oneline -- <file>` for prior fixes in the same area — **recurring bugs in the same files are an architectural smell**, not coincidence
- `docs/reviews/REVIEW-LOG.md` for prior Codex/deep-review findings on this code
- `docs/features/<feature>.md` for known limitations / design notes

If WebSearch is needed for a Bannerlord modding pattern, **sanitize the error first** — strip path prefixes (`E:\Steam\steamapps\...`, `c:\Users\mikew\...`), customer identifiers, mod-internal IDs, and stack hostnames. Search the generic error category, not the raw message.

---

## Phase 4: Hypothesis Testing

Before writing ANY fix, verify your hypothesis.

1. **Confirm.** Add a temporary log line, assertion, or debug output at the suspected root cause. Reproduce. Does evidence match?

2. **If wrong:** return to Phase 1. Do NOT guess your way to a fix. The cost of a second investigation is minutes; the cost of a wrong fix is hours.

3. **3-strike rule.** If three hypotheses fail in sequence, **STOP** and use `AskUserQuestion`:
   ```
   3 hypotheses tested, none match. This may be architectural rather than a simple bug.
   A) Continue — I have a new hypothesis: [describe]
   B) Escalate — this needs human eyes
   C) Instrument and wait — add logging, defer fix to next repro
   ```

**Red flags — slow down if you see any of these:**
- "Quick fix for now" — there is no "for now." Fix it right or escalate.
- Proposing a fix before tracing data flow.
- Each fix reveals a new problem elsewhere — wrong layer, not wrong code.

---

## Phase 5: Implementation

Once root cause is confirmed:

1. **Fix the root cause, not the symptom.** Smallest change that eliminates the actual problem.

2. **Minimal diff.** Fewest files, fewest lines. Resist refactoring adjacent code while debugging — log it for a follow-up.

3. **Write a regression test.** Per `tests.md` (TDD-mandatory):
   - **Fails** without the fix (proves the test is meaningful)
   - **Passes** with the fix (proves the fix works)

4. **Build + tests.**
   ```bash
   ./build.ps1 -RunTests
   ```
   No build errors. No test regressions. Paste the output.

5. **Blast-radius gate.** If the fix touches >5 files, `AskUserQuestion`:
   ```
   This fix touches N files. Large blast radius for a bug fix.
   A) Proceed — root cause genuinely spans these files
   B) Split — fix the critical path now, defer the rest
   C) Rethink — maybe a more targeted approach exists
   ```

---

## Phase 6: Verification & Report

Reproduce the original bug scenario and confirm it's fixed. **Not optional.** For Bannerlord, this often means launching the game manually — say so explicitly if you can't test in-game.

Run the test suite. Paste the output.

Output a structured debug report:

```
DEBUG REPORT
=========================================
Symptom:           [what the user observed]
Root cause:        [what was actually wrong, in TAOM terms]
Phase that found it: [Phase 3 pattern match | Phase 4 hypothesis | etc.]
Fix:               [what changed, file:line references]
Evidence:          [test output, in-game repro result if applicable]
Regression test:   [TAOM.Tests/.../FooTests.cs:line]
Files touched:     [N]
Related:           [TODO.md / feedback_*.md memories applied / docs/reviews items]
Status:            DONE | DONE_WITH_CONCERNS | BLOCKED
=========================================
```

If a previously-noted memory pattern (e.g., `feedback_simpler_fix_first.md`, `feedback_engine_scale_research.md`) directly fingered the root cause, name it: **"Prior memory applied: <memory_name>"** — makes the compounding visible across sessions.

---

## When to release the freeze

After verification passes, ask the user whether to release the scope lock or keep it for the next change. If keeping, leave the freeze state in place; if releasing, run `/unfreeze`.

## Notes

- This skill auto-attaches the `/freeze` hook to its own session. It works whether or not the user explicitly ran `/freeze` first.
- For multi-feature bugs, run `/investigate` once per feature in sequence, releasing freeze between runs.
- Pair with `/codex-verify` after fix lands, before merge — gets an adversarial second opinion on the root-cause analysis.
