# RCA: co-op session token (#1471)

**Scope.** The TAOM.CoopCompat session token on `fix/coop-hideout-finds` (base `9fa3bd53c`), the fix
for a Codex P1 on the co-op extras series. `/deep-review` tier 1: Codex at high in three passes
(the token, its fixes, a final check of the second fixes) and Lens 5 Data flow. Every confirmed P1 and P2 is below, grouped by root cause, with
its fix and its test. The P3 items are fixed and listed in the last class.

## Class 1: a client trusted any message that reached its broker (the original P1)

**Instance.** `CoopTransport.OnCareerUpdate`, and the momentum, trade reply and hello reply
handlers, accepted any message of their type that Coop published into the client's broker. Two Coop
paths publish a message with no sender identity: the mission P2P client
(`LiteNetP2PClient.HandleReceivedPayload` publishes any `IMessage` from a mission peer) and the
server relay (`RelayPacketHandler.HandlePacket` sends a mission peer's raw bytes to a client over
the server link). A player in the same mission could send another player a forged
`CoopCompatCareerUpdate` and change that player's career on the victim's client.

**Why missed.** The design read "server to client" from the message direction in our own code. It
did not read how Coop delivers bytes to a client. The relay makes a forged packet arrive over the
server connection, so the first fix idea (check that the sender is the server's `NetPeer`) could not
have closed it.

**Fix.** A session token (the maintainer's choice). The client picks a random token per session
container and sends it in its hello. The server keeps one token per connection, by reference, and
stamps it on every server-to-client message for that connection (`ISessionStamped`). The client
drops a message with another token or none. The protocol is now 2, and each relay drops a message
from another protocol before it acts.

**Test.** `CoopTransportTests`: other token or none dropped, nothing accepted before the hello, a
new session's token replaces the old one, each broadcast and reply carries the token of its peer.

**Prevent.** Before a co-op handler trusts a message, trace every Coop path that can publish that
type into the broker, the relay and the mission P2P client included. A direction in our own code is
not a sender identity.

## Class 2: the token cut a path that Coop's own join queue covered (2 P1, 1 P2)

**Instances.**
- P1: a player who loads while another player's career changes missed that change. The server sent
  stamped broadcasts only to connections that had said hello, and the hello comes after Coop's
  catch-up (`CampaignState`, `ClientCampaignReady`). Coop's join queue
  (`TransferSaveState`, `ConnectionMessageQueue`) would have held the packet, but the token
  broadcast never gave it one.
- P1 (second pass): the catch-up baseline carried the previous client process's report sequence. A
  restarted client counts from 1, so a late baseline entry for its own hero could overwrite a newer
  career it had just sent.
- P1 (second pass): the baseline listed only heroes that a player had reported in this process. A
  server change to an offline hero (a death clear) during a join was in neither the broadcast nor
  the baseline.
- P2: closed connections stayed in the token map until the next broadcast.

**Why missed.** The first design treated "the client has said hello" as "the client can receive".
It did not read what Coop queues for a loading client. The baseline then reused the live sequence
number across processes, where the two counters are not comparable.

**Fix.** On a hello, the server sends that player a baseline: the stored career of every hero a
player reported or the server changed in this process (`CareerSyncHost.Baseline`), at sequence 0.
A client counts its reports from 1 in each process, so the baseline never beats its own join
report. `OnLocalChange` records the hero, so server changes are in the baseline. A hello and a
broadcast both forget closed connections.

**Test.** `CareerSyncHostTests.Baseline_IsTheStoredCareerOfEveryHeroSyncedInThisProcess_AtSequenceZero`
(includes a server change, excludes a hero only in the save),
`Baseline_LeavesOutAReportedHeroWithNoStoredCareer_AndIsEmptyWithTheSwitchOff`,
`CareerSyncRelayServiceTests.Hello_OnTheServer_SendsThatPlayerTheStoredCareerOfEveryReportedHero`,
`CoopTransportTests` (a hello forgets closed connections).

**Prevent.** A change that gates delivery on a new condition lists what the old path delivered in
the gap, and who now delivers it. A sequence number is compared only inside the process that counts
it.

## Class 3: a mixed build that mutates and then drops the reply (1 P1)

**Instance.** A token client on a pre-token server sent trade and career requests that the server
carried out, and then dropped every tokenless reply. A purchase could charge the player with no
result on screen.

**Why missed.** The review of the token looked at the forged-message case only. It did not list each
pairing of old and new builds.

**Fix.** Protocol 2. Each relay drops a request or reply from another protocol before it calls a
host service. The handshake still answers across protocols, so the player sees the build notice.
Coop's `ModuleValidator` refuses a different module version, so a mixed pair needs two builds with
the same version number.

**Test.** The relay protocol tests and the `WireContractNamesTests` layout pins.

**Prevent.** A wire change gets a table of each old and new pairing with what each side does, in
the review prompt and in the feature doc.

## Class 4: minor (P3, fixed)

- The tokenless mismatch reply kept protocol 2 and read as a settings match. It is now a bare
  reply at protocol 0 with `BuildMismatch` only.
- "Logged once per session" for a dropped message was not atomic across the campaign and mission
  pollers. The decision is now under a lock.
- `coop-interop.md` and `coop-compat.md` overstated the guarantee for a protocol 1 client and the
  handshake exception. Both now say what each pairing does.

## Not fixed

- `CoopCompatLdRunEnded` stays without a token. A forged one shows a false notice and a log line; it
  ends nothing. Documented in `coop-compat.md`.
- A protocol 1 client stays open to the forged update until it updates. Documented.

## Final check

Codex at high on the second fix diff (2026-11-22): the four fixes hold (sequence 0 after a
restart, the death clear of an offline hero in the baseline, the bare reply read as inconclusive,
the drop log under a lock). One P3 remained: `coop-compat.md` said the client "always" drops its
own baseline entry, which is true only after its first report. The doc now says so.
