# Codex re-review: the fix for a sally-out meeting with an empty party (2026-11-08)

You are the reviewer (`.ai/roles/reviewer.md`). This is a review-only assignment: do not modify, create or
delete any file, do not run a build that deploys (`./build.ps1`), and do not commit. Read `AGENTS.md`,
`.ai/policy.md`, `.ai/review-reference.md` and `.claude/rules/harmony-patches.md` first. Every claim needs a
source location you read in this run; mark anything you cannot prove UNVERIFIED. Other sessions are editing this
checkout, so print `git rev-parse HEAD` at the top of your report.

## The change

Uncommitted on `bannerlord-1.5.x`, issue #1099. Only these are in scope. Untracked files: read them directly.
Others: `git diff -- <file>`.

| File | In scope |
|---|---|
| `Main/Features/MapEventGuard/Hooks/Patch142_SallyOutEmptyPartyGuard.cs` | new, the whole file |
| `Main/Features/Enlistment/Hooks/EnlistmentMenuBehavior.cs` | the `OnConversationEnded` change only |
| `Main/SubModule.cs` | the Patch142 hunk only |
| `TAOM.Tests/Features/MapEventGuard/Patch142SallyOutEmptyPartyGuardTests.cs` | new, the whole file |
| `TAOM.Tests/Features/Enlistment/EnlistmentConversationEndTests.cs` | new, the whole file |
| `TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs` | the `Patch142_SallyOutEmptyPartyGuard` row only |
| `tools/ingame_checks/1099.toml` | new, the whole file |
| `docs/reference/harmony-patch-registry.md` | the `## Patch142_SallyOutEmptyPartyGuard` section only |
| `docs/features/map-event-guard.md`, `docs/features/enlistment.md` | the #1099 changes only |
| `docs/reviews/rca-sally-empty-party-2026-11-08.md` | new, the whole file |

Other modified files in the tree are other work and out of scope.

This re-reviews a fix. Your earlier pre-review of the first version is at
`docs/reviews/raw/codex-prereview-sally-empty-party-2026-11-08.md`, and its three MEDIUM findings are answered
in the RCA. Test each answer; do not accept it.

The maintainer decided two things: the guard is a Prefix on `CheckSallyOut` and not on `DoMeeting`, and the
player's own sally button (`EncounterGameMenuBehavior.sally_out_consequence`) stays unguarded. Everything else in
the comments, the registry section and the RCA is a claim to test.

Engine signatures and bodies: the installed v1.5.6 DLLs are the authority (`pwsh tools/taom-src.ps1 path <Type>`,
or the `ilspy` MCP server). Do not paste decompiled engine bodies into your report beyond the short quote that
proves a claim.

## Known suspects (give each a verdict with evidence)

1. **The menu fix.** With `OnConversationEnded` returning while the player is in a map event, does the player's
   battle close? Name the code that advances it. Can the player end on a menu with no way forward?
2. **The state gate.** The hook runs its new test only when the record reads `EnlistedAttached`. Could the same
   stall happen in another enlistment state, or through another writer of the wait menu?
3. **The early return.** `Evaluate` runs vanilla unless the besieger leader's map event is the main party's map
   event. Is every route to `encounter_meeting` from `CheckSallyOut` inside that condition?
4. **The garrison.** The rule no longer reads the garrison. Is there a route where the garrison is the conversation
   partner, with 0 men, and the menu is `encounter_meeting`?
5. **The roster walk.** Does `AnyoneRemains` over `MemberRoster` predict what
   `GetConversationCharacterPartyLeader` returns after `Finish`? Consider capture, `DiedInBattle`, a fugitive,
   a wounded-only party, and a leader hero with no roster entry.
6. **What a skip costs.** On which states does the prefix still return false, and what then closes the battle?
7. **Tests.** Do the tests fail when the behaviour breaks and pass when only the internals change? Are the
   identity-only engine objects a fair stand-in for the reads they replace?

## Output

Group findings by severity (CRITICAL, HIGH, MEDIUM, LOW). For each one give the file and line, the evidence, and
the failure it causes. If there are no findings, say so and list what you checked.
