# RCA: BannerlordCoop campaign-free session, review of f1a3bc340..ad54d0f58 (2026-11-14)

Scope: BannerlordCoop branch `feat/campaign-free-session` (Coop Epic #4691), commits 348c88341 (steps 1 to 3)
and ad54d0f58 (steps 4 to 6). Review: `/deep-review` tier 1. Codex (gpt-6-astra, ultra) plus eight lenses in
two waves: data flow (server and join), data flow (mission path), engine compatibility, a no-campaign sweep of
mission patches, then standards, efficiency, completeness and design.

## Findings and root causes

| # | Severity | Finding | Root cause |
|---|---|---|---|
| 1 | CRITICAL | `SessionJoinState` never moves the peer out of `ConnectionMessageQueue`'s Dropping phase, so the server drops every ordinary send to a session client: no credential, no host assignment, no close. | The new join path skips the two states (`TransferSaveState`, `LoadingState`) that open the queue. The E2E `MockNetworkBase` has no queue, so every test passed. Two doc comments stated the wrong premise ("the queue drops a SendAll"), and the review brief repeated it. |
| 2 | HIGH | A session client registers no item or modifier ids, so weapon drop, pickup and ammo data fail to pack. | Same cause as 1: the object registration lives in `LoadingState` and `CharacterCreationState`, which a session skips. Spawn equipment still works because its surrogate uses `StringId`, which hid the gap. |
| 3 | HIGH | The session host request reads membership on the poll thread while the entry is written on the game thread. | The new handler did not follow its campaign sibling, which runs the whole request on the game thread. |
| 4 | MEDIUM | A client cannot leave on server loss: `GoToMainMenu` returns when `Campaign.Current` is null. | Client exit was copied from campaign states that assume a campaign. |
| 5 | MEDIUM | `IsRepair` reads any destruction-state drop as a repair; vanilla `OnHit` also lowers the state after a heal. | An engine claim written from the method's role, not read from the v1.5.7 decompile. |
| 6 | MEDIUM | The reset message has no sender check and no receive-path test. | The new message was added without the authority check its sibling state message has. |
| 7 | MEDIUM | A peer gate reset leaves the navmesh open. | `CastleGate.OnMissionReset` does not close the faces; vanilla closes them in `AfterMissionStart`, which a mid-mission reset does not run. |
| 8 | MEDIUM | `SessionHostChanged` fires again for the same host and epoch; the session join replay blocks the relay thread; `HideLoadingScreen` runs on the poll thread; the session disconnect skips peer cleanup; `CoopEmptyTeamDeploymentPatch` runs in sessions. | Campaign code paths reached by a session without a check of their campaign premises. |
| 9 | MEDIUM | `BuildOwnedAgentRecords` moved a locked lookup ahead of its filters (campaign path cost). | A refactor for the resolver seam reordered the loop. |
| 10 | MEDIUM | About 150 lines of host start copied in `CoopartiveMultiplayerExperience`, outside the accepted 60-line copy. | Copy chosen over extraction without asking. |
| 11 | LOW | Contract gaps for a mod author (behaviour timing, `MissionCombatantsLogic`, end condition, engines, host-only AI, epoch on mod messages), docs, style and test-quality items. | No doc for the mod author was planned as a step. |

## Re-review of the fix commit 2f31f0499 (Codex high, data flow lens)

| # | Severity | Finding | Root cause |
|---|---|---|---|
| R1 | HIGH | The item-id fix registered in `SessionState` at the main menu, where no Game exists, so the registration threw and the open request was never sent. | The fix copied the campaign's place for registration (a client state) without checking that the Game exists there; the unit test used a mock registry manager. |
| R2 | MEDIUM | The new reset sender check dropped a correct reset that arrived before the new host's assignment. | The check did not follow the state message, which buffers an epoch that is ahead. |
| R3 | MEDIUM | The full-health repair rule missed a reset followed by a hit before the next poll. | A design-lens rule was applied without asking what state the host reads one poll later. The decompile shows only `Reset` reaches state 0 from a broken state. |
| R4 | LOW | Clan finance sent null hero ids for session players after the disconnect fix. | The disconnect fix made seven campaign subscribers run on a session server; one was not null-safe. |

Fixed in aba6490ee, test first; re-reviewed again.

## Re-review 2 of aba6490ee (Codex high, data flow lens)

| # | Severity | Finding | Verdict and root cause |
|---|---|---|---|
| S1 | MEDIUM (both reviewers) | A buffered reset can land after the states that follow it and overwrite them. The promoted host sends its authority first, and states then apply against that per-machine record while the reset waits for `session.HostEpoch`. | Confirmed. The reset path and the state path decided "ahead" on different keys. The R2 test sent no authority message, so it did not model a real promotion. Fixed: the reset uses the state path's key (`IsResetFromHost`), in the handler and in the drain. |
| S2 | LOW (lens) | A reset from a non-host peer at the new epoch could replace the host's buffered reset. | Confirmed; the S1 fix drops it once the authority is recorded. |
| S3 | MEDIUM (Codex) | A late reset from an older host could replace a newer buffered reset. | Not reachable: the BR-102 accepted-epoch watermark (`HostEpochPolicy.IsStale`) drops it first. A regression test now proves that. |
| S4 | MEDIUM (Codex) | `IsRepair` can report a reset after a hit that sets state 0 and then throws in the engine. | No code change. The host's own machine is broken by an engine exception in that case, and the host's real state follows every reset. The comment now states this. |

## Lessons

- A test transport that skips a production gate proves nothing about that gate. When a new path skips states, list
  every side effect those states have (queue phase, registries, events) and check each one on the new path.
- A doc comment that states a premise is a claim; check it against the code before it goes into a review brief.
- A handler next to a sibling follows the sibling's thread rule unless it says why not.
- Engine behaviour in a comment needs the decompile line, not the method name.
- A fix that moves work to a new place must check what exists at that place (here: a Game and its object
  manager); a mock in the test hides the absence.
- A new check on a message must follow the sibling message's handling of an epoch that is ahead, on the same key.
  A test for message order must send the messages the real sender sends, in its order.
- Before a guard goes in, prove its test fails without it; twice in this round the existing code already covered
  the case, and the guard was removed.
- Git Bash `sed -i` drops CR from a CRLF file on this machine, even with an empty script; edit CRLF files with
  the Edit tool or a byte-level script.

## Fixes

All on BannerlordCoop `feat/campaign-free-session`, test first: 2f31f0499 (review), aba6490ee (re-review 1),
10114c4ea (re-review 2). Draft PR: BannerlordCoop #4694 into `bannerlord-1.5.x`. A live two-client session run is
still owed.
