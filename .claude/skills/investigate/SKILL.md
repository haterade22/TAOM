---
name: investigate
description: Use on a crash, exception or "why is this broken" report, or a build error past its retry budget. No fix before a named root cause; engages /freeze.
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
          command: "bash ${CLAUDE_PROJECT_DIR}/.claude/skills/freeze/check-freeze.sh"
          timeout: 5
    - matcher: "Write"
      hooks:
        - type: command
          command: "bash ${CLAUDE_PROJECT_DIR}/.claude/skills/freeze/check-freeze.sh"
          timeout: 5
---

# /investigate — Systematic Root-Cause Debugging

Adapted from [garrytan/gstack/investigate](https://github.com/garrytan/gstack/tree/main/investigate). Reuses our existing `/freeze` hook so debug scope is auto-locked.

## Iron Law

**NO FIXES WITHOUT ROOT-CAUSE INVESTIGATION FIRST.**

Fixing symptoms creates whack-a-mole debugging. Every fix that doesn't address root cause makes the next bug harder to find. Find the root cause, then fix it. This is a BLOCKING GATE, not optional.

---

## Phase 1: Root-Cause Investigation

Gather context before forming any hypothesis.

A crash reported only by logs: read every line of both logs, collapsing repeats with `sort | uniq -c`, before naming a cause. Never tail and grep, because a native crash leaves no error line (Mike, 2026-09-22). A player's engine build is context, not a finding to file, and never "tell players to update" (Mike, 2026-09-12).

1. **Collect symptoms.** Read error messages, stack traces, save-load logs, Bannerlord crash dumps. If the user hasn't given enough context, ask ONE question at a time via `AskUserQuestion`.

   **A native crash or a hang takes a detour first.** An access violation in `TaleWorlds.Native.dll`, a
   CTD with no managed culprit, or a freeze: run `/native-crash-triage` to name the site and read it as C
   (its hang section covers a freeze), then come back here with that site as the symptom.

2. **Read the code.** Trace from symptom back. Use `Grep` to find references; `Read` to understand logic.

2a. **Map the failing type's neighbourhood from the code graph (mandatory when the trace or symptom names a TAOM type, #677).** `python tools/graphify_taom.py refresh --if-stale` (background when stale), then `python tools/graphify_taom.py explain "<Type>"` for what it inherits, injects and calls, and `python tools/graphify_taom.py affected "<Type>" --depth 2` for everything that reaches it. That is the candidate list for Phase 3's pattern match. It is a lead list: open each file before it shapes a hypothesis. Engine types are not in the graph (use `pwsh tools/taom-src.ps1 path <Type>`), and neither is XML.

2b. **Map a stack trace to its owning patch (crash-to-owner in one step).** If the trace names a TaleWorlds type or a TAOM patch, grep it in [`docs/reference/harmony-patch-registry.md`](../../../docs/reference/harmony-patch-registry.md) FIRST — the registry maps a failing type to its owning `PatchNN` category, exact target method, and status (active / DISABLED / PARKED) before you trace further. (This is where the former CLAUDE.md "Harmony Patch Categories" table now lives.)

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

5. **Check the simpler-fix-first list.** Before investigating engine internals, check whether the bug is in:
   - `skins.xml` / `monsters.xml` / `action_sets.xml` (race + animation issues)
   - `taom_*.xml` data files (XML schema or value problems)
   - XSLT transforms (vanilla attribute drift)
   - `IoC.cs` registration order (service-locator hot path)

6. **Verify TaleWorlds API signatures.** The decompile dump at `E:\Decompiled_Bannerlord\` can lag the installed game after an engine bump. Before assuming a method exists or has a particular signature, run `pwsh tools/taom-src.ps1 path <Type>` (or `ilspycmd`) against the installed DLLs at `%BANNERLORD_GAME_DIR%\bin\Win64_Shipping_Client\`. If dump and installed disagree, trust installed. For a large surface, follow `docs/ai-includes/taleworlds-research-guide.md`.

Output: **"Root-cause hypothesis: ..."** — a specific, testable claim about *what* is wrong and *why*.

---

## Phase 2: Scope Lock (auto-freeze)

Once you have a hypothesis, lock edit scope to the affected directory. Unlike a casual edit-restrict, this skill's own PreToolUse hooks (declared in this SKILL.md's frontmatter) ARE active while `/investigate` is running, so writing the state file here will fire them — that's why this skill can auto-engage freeze without separately invoking `/freeze`.

```bash
SCOPE="<e.g. Main/Features/CareerSystem/>"

STATE_DIR="${CLAUDE_PROJECT_DIR}/.claude/tmp/freeze"
mkdir -p "$STATE_DIR"
echo "$SCOPE" > "$STATE_DIR/freeze-dir.txt"
echo "Debug scope locked to: $SCOPE"
```

Lifecycle: harness-facts.md "Hook lifecycle".

Tell the user: *"Edits restricted to `<scope>` for this debug session. Outside-scope edits will be hard-blocked. Run `/unfreeze` to release."*

If the bug genuinely spans the whole repo (e.g., a shared adapter contract change), skip the lock and note why.

---

## Phase 3: Pattern Match

Check whether the bug matches a known TAOM/Bannerlord failure pattern before writing any fix:

| Pattern | Signature | Where to look |
|---------|-----------|---------------|
| **Harmony patch type-load** | `TypeLoadException` at startup, mod fails before main menu | Patch target signature mismatch (installed-vs-dump drift after an engine bump) — verify with `pwsh tools/taom-src.ps1 path <Type>` / `ilspycmd` |
| **Harmony patch silent skip** | Patch loads, but behavior doesn't change in-game | Wrong target method, prefix returning false unexpectedly, manual patch matching wrong overload |
| **GameModel cast** | `InvalidCastException` in DefaultXxxModel call | New override returning wrong type, or base call missing |
| **Service-locator at startup** | `IoC.Resolve` returns null, NullReferenceException in patch | Service registered after CampaignBehavior runs (registration order bug). |
| **VM string crash** | `{=key}Text` shows literal in UI, or NRE on property access | Missing `TextObject().ToString()` |
| **Sprite missing / atlas corruption** | Items render in underwear, sprite renders blank/black | Wrong sprite ID in XML, or oversized PNG corrupting atlas |
| **Save-load state loss** | Buff or feature works once, lost after save+load | Missing `SyncData`, or storing non-savable type. |
| **Race / culture XML wiring** | Custom culture characters appear vanilla | XSLT didn't pass through new attribute, or XML missing required field |
| **Collection-API self-inclusion** | OffByOne in counts, suspicious behavior near 'self' | TaleWorlds collection iter includes the caller. |
| **Engine-scale property** | `AgentDrivenProperties` change has no effect or wrong magnitude | Missed downstream consumer / clamp / multiplier. |
| **Native CTD** | Access violation in `TaleWorlds.Native.dll`, no managed frame to blame | `/native-crash-triage`: name the site, decompile it; the output names the engine method when the site implements one |
| **Behaviour below an `[EngineMethod]`** | The managed trace ends in an engine interface call (`MBAPI.IMB*`, `EngineApplicationInterface.I*`, `LibraryApplicationInterface.I*`), or data the engine parses natively misbehaves | Decompile the native side: `python tools/native_decompile.py --engine-method <name>` / `--string <attribute>`. Never infer it from the method name |

Also check:
- `git log --all --oneline -- <file>` for prior fixes in the same area — **recurring bugs in the same files are an architectural smell**, not coincidence
- `docs/reviews/REVIEW-LOG.md` for prior Codex/deep-review findings on this code
- `docs/features/<feature>.md` for known limitations / design notes

**Several symptoms: look for a shared dimension.** When one bug surfaces as many seemingly unrelated symptoms, or repeated runs keep finding "different bugs", list the symptoms first, then look for what they share:
- Same TaleWorlds API surface (for example, all touch `MobileParty.Position`)
- Same lifecycle phase (campaign load, mission start, save, shutdown)
- Same culture, race, kingdom, settlement or clan id family
- Same Harmony patch order (priority conflicts)
- Same data file (XML config, JSON tuning)
- Same C# layer (adapter, service, GameModel)
- Same native crash site (one `TaleWorlds.Native.dll` offset, or one engine method, across reports): name it once with `python tools/native_crash_triage.py --rva 0x<offset>` and read it as C with `python tools/native_decompile.py --rva 0x<offset>`, then run `/native-crash-triage`

If every symptom shares one dimension, that dimension is the suspect: trace one symptom to it, then check that the others come from the same place. Correlated symptoms can still be unrelated; if the hypothesis fails on more than one symptom, back up.

If WebSearch is needed for a Bannerlord modding pattern, **sanitize the error first** — strip path prefixes (`E:\Steam\steamapps\...`, `c:\Users\mikew\...`), customer identifiers, mod-internal IDs, and stack hostnames. Search the generic error category, not the raw message.

---

## Phase 4: Hypothesis Testing

Before writing ANY fix, verify your hypothesis.

1. **Confirm.** Add a temporary log line, assertion, or debug output at the suspected root cause. Reproduce. Does evidence match?

2. **If wrong:** return to Phase 1. Do NOT guess your way to a fix. The cost of a second investigation is minutes; the cost of a wrong fix is hours.

3. **3-strike rule.** If three hypotheses fail in sequence, first write down the premise they share, and run one observation that would come out differently if that premise were false. Then **STOP** and use `AskUserQuestion`:
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

1. **Fix the root cause, not the symptom.** Smallest change that eliminates the actual problem. Ask whether the triggering state is legitimate: if it should never exist, a null-check or fallback where it broke hides the bug, so fix where the state is created.

2. **Minimal diff.** Fewest files, fewest lines. Resist refactoring adjacent code while debugging — log it for a follow-up.

3. **Write a regression test.** Per `tests.md` (TDD-mandatory):
   - **Fails** without the fix (proves the test is meaningful)
   - **Passes** with the fix (proves the fix works)

4. **Build + tests.**
   ```bash
   dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=
   ```
   No build errors. No test regressions. Paste the output. Use `./build.ps1` only when a game check follows, and never while Bannerlord runs.

5. **Blast-radius gate.** If the fix touches >5 files, `AskUserQuestion`:
   ```
   This fix touches N files. Large blast radius for a bug fix.
   A) Proceed — root cause genuinely spans these files
   B) Split — fix the critical path now, defer the rest
   C) Rethink — maybe a more targeted approach exists
   ```

---

## Phase 6: Verification & Report

Reproduce the original bug scenario and confirm it's fixed. **Not optional.** For Bannerlord, this often means a game run. A check a Fords match exercises goes to /game-run under its gate; for any other check, name what Mike must play and leave it owed.

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
Related:           [docs/reviews items]
Status:            DONE | DONE_WITH_CONCERNS | BLOCKED
=========================================
```

---

## When to release the freeze

After verification passes, ask the user whether to release the scope lock or keep it for the next change. If keeping, leave the freeze state in place; if releasing, run `/unfreeze`.

## Notes

- This skill auto-attaches the `/freeze` hook to its own session. It works whether or not the user explicitly ran `/freeze` first.
- For multi-feature bugs, run `/investigate` once per feature in sequence, releasing freeze between runs.
- Pair with `/review-codex` after the fix lands, before merge, for an adversarial second opinion on the root-cause analysis (paid: only on the user's word).
