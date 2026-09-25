# Codex adversarial review: plan 001 (cross-campaign-singleton-resets), branch improve/001-specialresources-reset

Feature: Reset CareerSystem + SpecialResources singletons on the new-campaign boundary. Two feature services hold per-hero state in a `Reuse.Singleton` dictionary that lives for the **entire Bannerlord process**, not for one campaign. When a player finishes campaign A and starts campaign B without restarting the game, B inherits A's data: - **CareerSystem** (`CareerDataService._heroData`): B's player inherits A's career choices, tier unlocks, and quest flags. Worse — at character creation, `CareerCreationHandler` calls `TryAddChoice(root, maxChoices)`; with leaked choices already counted, `GetChoiceCount() >= maxChoicesAllowed` **silently rejects B's root choice**. AI/lord career entries leak wholesale. On B's first save the leaked data is serialized into B's save file — permanent contamination. - **SpecialResources** (`SpecialResourceStorageService._data`): B starts with A's resource balances for every `(hero, resource)` pair except the single key `InitializeHero` overwrites. A stale entry also makes the `_storage.Contains(...)` seeding gate suppress B's legitimate `StartingAmount` seeding, so the player inherits A's balance for any resource A ever touched. Both feature areas already have the *pattern* for this fix — `SpecialResourceService.ResetSessionState()` (Phase 9b #133 P2 R1) clears the service's session state on `OnNewGameCreated`, but it **never clears the storage dict**, and `CareerPersistenceBehavior` has no new-game reset at all. This plan closes both gaps. After it lands, starting a second campaign in the same process produces a clean slate for both features. (Reference memory: `feedback_singleton_controller_per_mission_behavior_lifetime_asymmetry.md` — singleton-scoped state leaking across lifetime boundaries.)

HOW TO READ THE CODE (important): you are running in E:\repos\TAOM, but its working tree holds ANOTHER session's unrelated uncommitted edits. Review ONLY the branch under test through git refs:
- The change: git diff a39a9c86..improve/001-specialresources-reset
- Any file as the branch has it: git show improve/001-specialresources-reset:<path>
- The base for comparison: git show a39a9c86:<path>
Do NOT modify any file anywhere. Do NOT run builds, tests or the game. Read-only review.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: rohan is NOT a valid ID (Rohan uses vlandia). dol_guldur is NOT valid (use dolguldur).

READ FIRST:
- The plan the change implements: git show improve/001-specialresources-reset:plans/001-cross-campaign-singleton-resets.md (intent, scope, STOP conditions)
- AGENTS.md (the project's rules; ADR-002 thin entry points, ADR-007 adapters, TDD, banned constructs)
- Any feature doc under docs/features/ the diff touches (git show on the branch)
- Engine behaviour: the decompile dump at E:\Decompiled_Bannerlord\ (Bannerlord v1.5.3; it can lag, so treat it as a guide, and say UNVERIFIED where it matters)

KNOWN SUSPECTS (CONFIRM or DISPUTE each with file:line evidence):
1. From the plan's STOP conditions, did the change hit or mishandle this risk? The code at the locations in "Current state" doesn't match the excerpts (drift since `141b749`) — especially if `CareerPersistenceBehavior.RegisterEvents()` is no longer empty, or `SpecialResourcesBehavior.OnNewGameCreated` already clears storage.
2. From the plan's STOP conditions, did the change hit or mishandle this risk? A step's verification fails twice after a reasonable fix attempt.
3. From the plan's STOP conditions, did the change hit or mishandle this risk? The fix appears to require touching an out-of-scope file (`IoC.cs` / `SubModule.cs` / either feature's IoC registration / `CareerDataService.cs` / `SpecialResourceStorageService.cs` interface).
4. From the plan's STOP conditions, did the change hit or mishandle this risk? `CampaignEvents.OnNewGameCreatedEvent` / `AddNonSerializedListener` does not have the signature documented in "Engine facts" (i.e. the sibling `SpecialResourcesBehavior.cs:51` registration no longer compiles the same way) — report the mismatch, do not decompile-and-improvise.
5. From the plan's STOP conditions, did the change hit or mishandle this risk? The Step 5 absent-key SyncData test cannot be written cleanly with NSubstitute — report it; do NOT distort production code for testability (ship the load-branch null-local fix as structurally-covered).
6. From the plan's STOP conditions, did the change hit or mishandle this risk? You discover the assumption "`RestoreData(null)` coalesces to an empty dict" is false in either service (it is true in both as of `141b749` — re-read if drift is suspected).
7. Fail-safe defaults: any new or changed '?? true' / '?? false', config default or MCM default. Is the failure direction the safe one? Does a changed MCM default use a renamed setting (json2 persists old values)?
8. Stale state across lifecycle boundaries: singletons, statics or caches that survive a save load, a new campaign or a mission end.
9. Tests that pass without proving the behaviour: assertions on source text, mocks that test themselves, Assert.Inconclusive paths that report green.
10. Dead or no-op code introduced by the change; a gate that can never fire.

CHANGED FILES (4 files changed, 199 insertions(+), 5 deletions(-)):
C#:
- Main/Features/SpecialResources/SpecialResourcesBehavior.cs
Tests:
- TAOM.Tests/Features/SpecialResources/SpecialResourcesBehaviorSessionResetTests.cs
Harness and docs:
- CHANGELOG.md
- docs/features/special-resources.md

REQUIRED SECTIONS:
1. VANILLA CODE: for every Harmony patch, GameModel override or engine API the diff touches or relies on, paste the relevant v1.5.3 decompile excerpt as a code block and state what the change assumes about it.
2. DEEP ANALYSIS: walk concrete scenarios through the changed code (first boot, save load, new campaign in the same process, mission start and end, co-op or dedicated server where relevant, the failure path of every try/catch the diff adds or changes).
3. CONFIG CROSS-REFERENCE: every ID, path, setting name or config key the diff adds or reads, checked against its source of truth.
4. FINDINGS OR OBSERVATIONS: numbered, each with severity (P1 crash/data loss/security, P2 wrong behaviour, P3 quality), file:line on the branch, the reasoning, and a concrete fix. Say explicitly when the plan itself is wrong.

QUALITY GATES: cite file:line for every claim; do not flag vanilla-matching code as a bug; do not assume empire=Rohan; do not skip hard sections; mark engine behaviour you could not read as UNVERIFIED.

PRIOR REVIEW LESSONS: SUCCESSES: config ID cross-reference caught rohan/dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches. FAILURES: assuming empire=Rohan (it is Dunland); flagging vanilla-matching code as bugs; skipping hard sections.

OUTPUT: return the full review as your FINAL MESSAGE (the dispatcher redirects stdout into a file; do NOT write any file yourself). The very last line of your final message must be exactly: END OF CODEX REVIEW
