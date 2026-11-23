# Codex final check: co-op session token, second fix round (2026-11-22)

You are an adversarial senior reviewer for TAOM (Bannerlord v1.5.7 mod, .NET Framework 4.7.2, co-op bridge to
BannerlordCoop at E:/repos/BannerlordCoop). Review only; edit nothing. Report P1, P2 or P3 with file:line and the
failure scenario, or say the fix holds.

Your second pass is in `docs/reviews/raw/codex-prereview-coop-session-token-fixes-2026-11-22.md`. Check only these
four fixes in the current `git diff` (base HEAD 9fa3bd53c):

1. Your P1 "baseline overwrites a newer career after a client restart": `CareerSyncHost.Baseline()` now sends every
   entry at sequence 0. Read `CareerSyncClient.Receive` and `Send`: is there any state in which a client has sent no
   report in this process yet and still receives a baseline entry for its own hero, and would applying it then be wrong?
2. Your P1 "catch-up misses server changes": `CareerSyncHost.OnLocalChange` now records the hero in `_lastSequence`.
   Does `Baseline()` now cover a death clear of an offline hero during a join? Any side effect on `Handle` or on the
   sequence that `OnLocalChange` broadcasts?
3. Your P3 "bare reply gives a false Match": the tokenless mismatch reply now has ProtocolVersion 0. Read
   `SettingsHandshakeService.OnReply`: is the result inconclusive with the build notice and nothing else?
4. Your P3 "log once is not thread safe": `WarnDroppedOnce` now decides under a lock.

Also read the two changed doc bullets in docs/features/coop-compat.md (the protocol bullet and "Catch-up on hello").
