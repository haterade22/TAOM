# Codex pre-review: a sally-out must not open a meeting with an empty party (2026-11-08)

You are the reviewer (`.ai/roles/reviewer.md`). This is a review-only assignment: do not modify,
create or delete any file, do not run a build that deploys (`./build.ps1`), and do not commit. Read
`AGENTS.md`, `.ai/policy.md`, `.ai/review-reference.md` and `.claude/rules/harmony-patches.md` first.
Every claim needs a source location you read in this run; mark anything you cannot prove UNVERIFIED.
Other sessions are editing this checkout, so print `git rev-parse HEAD` at the top of your report.

## The change

Uncommitted on `bannerlord-1.5.x`, issue #1099. Only these are in scope. The first three new files are
untracked, so read them directly; use `git diff -- <file>` for the others.

| File | In scope |
|---|---|
| `Main/Features/MapEventGuard/Hooks/Patch142_SallyOutEmptyPartyGuard.cs` | new, the whole file |
| `TAOM.Tests/Features/MapEventGuard/Patch142SallyOutEmptyPartyGuardTests.cs` | new, the whole file |
| `tools/ingame_checks/1099.toml` | new, the whole file |
| `Main/SubModule.cs` | the Patch142 hunk only |
| `TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs` | the `Patch142_SallyOutEmptyPartyGuard` row only |
| `docs/reference/harmony-patch-registry.md` | the `## Patch142_SallyOutEmptyPartyGuard` section only |
| `docs/features/map-event-guard.md` | the Patch142 row only |
| `docs/reference/taleworlds-api-snapshot/patch-targets.md` | generated, the Patch142 row and the count |

Other modified files in the tree are other work and out of scope.

The crash, from a player's v2.0.36 game on Bannerlord v1.5.6: a `NullReferenceException` with this stack.

```
GauntletMapConversationView.CreateConversationTableau
GauntletMapConversationView.InitializeConversation
MapScreen.HandleMapConversationInit
ConversationManager.OpenMapConversation
PlayerEncounter.DoMeetingInternal
PlayerEncounter.DoMeeting
EncounterGameMenuBehavior.game_menu_encounter_meeting_on_init
GameMenu.ActivateGameMenu
PlayerEncounter.Init
EncounterManager.RestartPlayerEncounter
EncounterManager.StartPartyEncounter
SallyOutsCampaignBehavior.CheckSallyOut
SallyOutsCampaignBehavior.CheckForSettlementSallyOut
CampaignEvents.HourlyTickSettlement
```

After it, `MapConversationTableau.OnTick` threw an `ArgumentOutOfRangeException` on every frame.

The maintainer decided two things: the guard is a Prefix on `CheckSallyOut` and not on `DoMeeting`, and
the player's own sally button (`EncounterGameMenuBehavior.sally_out_consequence`) stays unguarded. Test
whether the first decision prevents the crash. Everything else in the patch comments and the registry
section is a claim to test.

Engine signatures and bodies: the installed v1.5.6 DLLs are the authority (`pwsh tools/taom-src.ps1
path <Type>`, or the `ilspy` MCP server). Do not paste decompiled engine bodies into your report
beyond the short quote that proves a claim.

## Known suspects (give each a verdict with evidence)

1. **The partner party.** On every path from `CheckSallyOut` to the lookup in `DoMeetingInternal`, which
   party is the conversation partner? Does it always equal one of the two parties the prefix tests?
   Read `PlayerEncounter.SetupFields`, `Init`, every other assignment of `_encounteredParty`, and the
   settlement branch at the top of `DoMeetingInternal`.
2. **The null value.** Which dereference in `CreateConversationTableau` can throw on this path?
3. **Other null sources.** Can the conversation open with a null partner on a path that does not start
   in `CheckSallyOut`, and is any such path as reachable as this one?
4. **What the skip loses.** What does vanilla do on the paths where the prefix returns false? Follow
   `salliedOut` through `CheckForSettlementSallyOut`, and the naval re-check.
5. **Over-firing.** The prefix runs before vanilla's strength comparison. On which states does it report a
   skipped sally when vanilla would not have sallied? How often can that happen, and what does that do
   to the warning line and to the meaning of the check in `tools/ingame_checks/1099.toml`?
6. **AI-only sieges.** What changes when the garrison has 0 men but other defender parties would have
   sallied? Is the rule too wide?
7. **Harmony binding.** Does a `ref bool salliedOut` prefix parameter bind to the target's `out bool`
   under Harmony 2.4.2? Can the JIT inline the private target?
8. **Co-op and server.** Is the `ReviewedSafe` classification correct under `docs/features/coop-interop.md`?
   Does the target run on a dedicated server or a co-op client?
9. **Tests.** Do the tests fail when the behaviour breaks and pass when only the internals change?

## Output

Group findings by severity (CRITICAL, HIGH, MEDIUM, LOW). For each one give the file and line, the
evidence, and the failure it causes. If there are no findings, say so and list what you checked.
