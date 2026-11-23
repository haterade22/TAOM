# Codex review: co-op session token (2026-11-22)

You are an adversarial senior reviewer for TAOM, a Bannerlord v1.5.7 mod (.NET Framework 4.7.2) with a co-op
bridge to BannerlordCoop (source at E:/repos/BannerlordCoop, branch bannerlord-1.5.x). Read AGENTS.md first.
Review only; edit nothing; write no file in the repo. Report findings as P1 (wrong behaviour, crash, data loss, a
player able to change another player's or the server's state), P2 (likely defect), P3 (minor), each with
file:line, the failure scenario and the evidence you read. Say UNVERIFIED for any Coop claim you could not check
in the Coop source.

## Scope

`git diff` in this checkout (uncommitted, base HEAD 9fa3bd53c): the TAOM.CoopCompat transport and messages, their
tests, and two doc sections (docs/features/coop-compat.md "Session token", docs/features/coop-interop.md one bullet
under the career sync trust model).

The defect it fixes is your earlier P1: `CoopTransport.OnCareerUpdate` (and the other client handlers) accepted any
`CoopCompatCareerUpdate` that reached the client's message broker. Coop's mission P2P client
(`LiteNetP2PClient.HandleReceivedPayload`) publishes any IMessage from a mission peer, and Coop's server relays a
mission peer's raw bytes to another client over the server link (`RelayPacketHandler.HandlePacket`,
`target.Send(relayPacket.Payload, ...)`), so a sender check by NetPeer could not tell a relayed forgery from the
server. The maintainer chose a session token: the client picks a random token per hello, the server stamps it per
connection on every server-to-client message, the client drops other tokens.

## Questions to answer

1. Can a mission peer still get a server-to-client TAOM message accepted by another client? Consider: learning the
   victim's token (is the hello or any stamped message ever relayed, broadcast or echoed where a peer can read it;
   does Coop forward a client's hello anywhere), replaying, the tokenless hello reply exception, the window before
   the first hello, a reconnect.
2. Server side: is `_peerTokens` correct under Coop's threads (hello on the poller thread, sends on the game thread)?
   Can it grow without bound, keep a dead connection, or map a reconnect to the old token? Is a broadcast that now
   loops over hello'd connections a behaviour change for anyone who used to get `SendAll` (for example a client whose
   hello failed or arrived after a broadcast)? Does stamping one message object per connection break if Coop
   serializes later than `INetwork.Send` (read `CoopNetworkBase.Send`, `MessagePacket.Create`, any batcher)?
3. Mixed builds: an older client (v2.0.40 or the co-op extras build without the token) with a newer server, and a
   newer client with an older server. What does each side see for the handshake notice, momentum, trade replies and
   career sync? Is any outcome worse than "the join notice warns about the build"?
4. Can a hostile client affect another player through its own hello (oversize token, a token equal to another
   player's, many hellos)?
5. Any other server-to-client TAOM message (for example `CoopCompatLdRunEnded` over the session channel) that has
   the same hole and is not covered? Report it, do not fix it.
6. Are the docs true line by line?
