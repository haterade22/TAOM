# Codex re-review: co-op session token, after the review fixes (2026-11-22)

You are an adversarial senior reviewer for TAOM, a Bannerlord v1.5.7 mod (.NET Framework 4.7.2) with a co-op
bridge to BannerlordCoop (source at E:/repos/BannerlordCoop, branch bannerlord-1.5.x). Read AGENTS.md first.
Review only; edit nothing; write no file in the repo. Report findings as P1, P2 or P3 with file:line, the failure
scenario and the evidence you read. Say UNVERIFIED for any Coop claim you could not check.

## Scope

`git diff` in this checkout (uncommitted, base HEAD 9fa3bd53c), plus the untracked
`CoopCompat/Coop/Messages/ISessionStamped.cs`. Your first pass on this change is in
`docs/reviews/raw/codex-prereview-coop-session-token-2026-11-22.md`. Since then these fixes were made:

1. Your P1 "joining clients miss career changes": `CareerSyncHost.Baseline()` and `CareerSyncRelayService.OnHello`
   send a player who says hello the stored career of every hero a player reported in this server process.
2. Your P1 "new client, pre-token server": `CoopCompatProtocol.ProtocolVersion` is now 2, so the relays drop the
   other build's messages before they act.
3. Your P2 "connections accumulate": closed connections are forgotten at each hello as well as each broadcast.
4. A lens finding: the token is now one per Coop session (one session container, held weakly), not one per hello,
   because Coop raises `ClientCampaignReady` after every mission. A message for an earlier session's token is dropped.
5. A lens finding: a tokenless hello reply that reports a build mismatch is passed on as a bare mismatch (no verdict,
   groups or load order).
6. A lens finding: the first dropped server message of each session is logged through `CoopTransport.Create(warn)`.
7. Docs: coop-compat.md "Session token", the protocol bullet, the career catch-up bullet; coop-interop.md trust bullet.

## Questions to answer

1. Does each fix close its finding? Name any scenario still open.
2. Did a fix add a defect? In particular: the baseline's own-hero entry against the client's join report and
   `_lastSent` (`CareerSyncClient.Receive`); a baseline sent on every hello after every mission; the session check
   when `ContainerProvider` has no container; thread safety of `_session` and `_dropLoggedIn`; the protocol bump
   against the handshake (`SettingsHandshakeService`, `BuildIdentity`), the Long Defeat session link, and Coop's own
   module-version check.
3. Are the docs true line by line?
