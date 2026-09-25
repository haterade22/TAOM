# Codex adversarial review: plan 002 (nan-infinity-config-guards), branch improve/002-nan-guards

Feature: Reject NaN/Infinity in the TroopWeight and Career mutation config loaders. This is the **4th shipping of the house NaN/Infinity config-guard bug class** (Career cooldown review #31, EditorCacheRebuild review #38, scene-scripts CS_Road 2026-05-13 are the prior three — all documented in `FiniteFloatValidator.cs` and `.claude/rules/csharp-architecture.md`). Two config loaders parse `float` values with `float.TryParse(..., NumberStyles.Float, ...)` and then guard with a bare comparison. `float.TryParse` **parses the literal strings `"NaN"`, `"Infinity"`, and `"-Infinity"` as `true`** (with `NumberStyles.Float`), and every IEEE-754 comparison against `NaN` returns `false` — so `NaN <= 0` is `false` and `Infinity <= 0` is `false`, and both sneak past the "must be positive" guard. A `NaN` troop weight corrupts the troop-weight party-budget math; a `NaN`/`Infinity` mutation param flows unguarded into Career ability templates — the exact "NaN cooldown → ability permanently ready / never ready" failure that review #31 was supposed to have killed. The fix routes both loaders through the established house guard `TAOM.Core.Validation.FiniteFloatValidator` so a fourth instance can't ship.

HOW TO READ THE CODE (important): you are running in E:\repos\TAOM, but its working tree holds ANOTHER session's unrelated uncommitted edits. Review ONLY the branch under test through git refs:
- The change: git diff a39a9c86..improve/002-nan-guards
- Any file as the branch has it: git show improve/002-nan-guards:<path>
- The base for comparison: git show a39a9c86:<path>
Do NOT modify any file anywhere. Do NOT run builds, tests or the game. Read-only review.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: rohan is NOT a valid ID (Rohan uses vlandia). dol_guldur is NOT valid (use dolguldur).

READ FIRST:
- The plan the change implements: git show improve/002-nan-guards:plans/002-nan-infinity-config-guards.md (intent, scope, STOP conditions)
- AGENTS.md (the project's rules; ADR-002 thin entry points, ADR-007 adapters, TDD, banned constructs)
- Any feature doc under docs/features/ the diff touches (git show on the branch)
- Engine behaviour: the decompile dump at E:\Decompiled_Bannerlord\ (Bannerlord v1.5.3; it can lag, so treat it as a guide, and say UNVERIFIED where it matters)

KNOWN SUSPECTS (CONFIRM or DISPUTE each with file:line evidence):
1. From the plan's STOP conditions, did the change hit or mishandle this risk? The drift check shows either `TroopWeightXmlLoader.cs` or `MutationParams.cs` changed since `141b749` and the "Current state" excerpts no longer match the live code.
2. From the plan's STOP conditions, did the change hit or mishandle this risk? The Step 1 RED tests unexpectedly PASS before any production edit (the guard may already exist — report rather than deleting tests).
3. From the plan's STOP conditions, did the change hit or mishandle this risk? A verification fails twice after a reasonable fix attempt.
4. From the plan's STOP conditions, did the change hit or mishandle this risk? The fix appears to require touching an out-of-scope file (especially `IoC.cs` / `SubModule.cs` / either `.csproj`) — e.g., a build error claims `FiniteFloatValidator` is not referenced by the Main project (it is: same `TAOM.Core.Validation` namespace, same assembly — if this happens, something is wrong; report).
5. From the plan's STOP conditions, did the change hit or mishandle this risk? `FiniteFloatValidator.IsFinite` is not found / has a different signature than the one quoted in "Current state."
6. Fail-safe defaults: any new or changed '?? true' / '?? false', config default or MCM default. Is the failure direction the safe one? Does a changed MCM default use a renamed setting (json2 persists old values)?
7. Stale state across lifecycle boundaries: singletons, statics or caches that survive a save load, a new campaign or a mission end.
8. Tests that pass without proving the behaviour: assertions on source text, mocks that test themselves, Assert.Inconclusive paths that report green.
9. Dead or no-op code introduced by the change; a gate that can never fire.

CHANGED FILES (3 files changed, 56 insertions(+)):
C#:
- Main/Features/CareerSystem/Mutations/MutationParams.cs
Tests:
- TAOM.Tests/Features/CareerSystem/MutationParamsTests.cs
Harness and docs:
- CHANGELOG.md

REQUIRED SECTIONS:
1. VANILLA CODE: for every Harmony patch, GameModel override or engine API the diff touches or relies on, paste the relevant v1.5.3 decompile excerpt as a code block and state what the change assumes about it.
2. DEEP ANALYSIS: walk concrete scenarios through the changed code (first boot, save load, new campaign in the same process, mission start and end, co-op or dedicated server where relevant, the failure path of every try/catch the diff adds or changes).
3. CONFIG CROSS-REFERENCE: every ID, path, setting name or config key the diff adds or reads, checked against its source of truth.
4. FINDINGS OR OBSERVATIONS: numbered, each with severity (P1 crash/data loss/security, P2 wrong behaviour, P3 quality), file:line on the branch, the reasoning, and a concrete fix. Say explicitly when the plan itself is wrong.

QUALITY GATES: cite file:line for every claim; do not flag vanilla-matching code as a bug; do not assume empire=Rohan; do not skip hard sections; mark engine behaviour you could not read as UNVERIFIED.

PRIOR REVIEW LESSONS: SUCCESSES: config ID cross-reference caught rohan/dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches. FAILURES: assuming empire=Rohan (it is Dunland); flagging vanilla-matching code as bugs; skipping hard sections.

OUTPUT: return the full review as your FINAL MESSAGE (the dispatcher redirects stdout into a file; do NOT write any file yourself). The very last line of your final message must be exactly: END OF CODEX REVIEW
