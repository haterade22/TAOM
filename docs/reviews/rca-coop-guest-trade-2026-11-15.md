# RCA: co-op guest trade review (#1329)

**Scope.** `/deep-review` tier 1 of the uncommitted guest trade change on `feat/coop-guest-trade`
(base `a5c6584c0`): a co-op guest buys from the Elite Emissary and upgrades armour through the host.
First wave: Data flow, Engine compatibility, XML. Second wave (a HIGH was found): Standards,
Efficiency, Completeness, Design. Codex tier 1 was skipped on the maintainer's word (usage limit) and
is owed later. Every confirmed finding is below, grouped by root cause, with its fix and its test.

## Class 1: an external id read by its name, not by its source (1 HIGH, 1 LOW)

**Instances.**
- HIGH: `CoopTransport.HeroOfConnectedPeer` passed Coop's `Player.HeroId` to TAOM, which looked the
  hero up with `CampaignObjectManager.Find<Hero>`. `Player.HeroId` is Coop's object-manager key,
  `"Hero_" + StringId` (`HeroInterface.RegisterPrimary`, `CreateCharacterState.TryCreatePlayer` via
  `ObjectManager.TryGetId`). `Find` compares `StringId` only, so the host refused every real guest
  as "not a free leader". Both first-wave lenses found it on their own.
- LOW: `GuestTradeHeroAdapter` treated `MobileParty.SiegeEvent` as "in a siege". It is
  `BesiegerCamp?.SiegeEvent`, the besieging side only, so a defender inside a besieged town could
  trade.

**Why missed.** Both values were read from their names. "HeroId" read as the hero's id, and
"SiegeEvent" read as any siege. The transport test injected the hero lookup as a delegate that
returned `"hero_1"`, so no test met Coop's real id format.

**Fix.** The transport now calls `IPlayerManager.TryGetSenderHero` and returns `hero.StringId`, after
the current-controller check (`CoopTransport.HeroOfCurrentController`). The adapter also checks
`settlement.IsUnderSiege`.

**Test.** `CoopTransportTests.HeroOfCurrentController_IsTheHerosStringId_NotCoopsObjectKey` and
`..._IsNull_ForAConnectionNoLongerCurrent_OrWithNoHero`. The siege check is in an engine-bound
adapter with no unit seam; `coop-compat.md` "How to verify in game" step 4 now checks a refusal.

**Prevent.** A trap line in the orientation trap index ("Coop ids"), and the lesson in
`lessons/adapters-taleworlds-api.md`. A seam that carries an external id gets one test with that
system's real id shape, not a plain string.

## Class 2: a value the host decides, taken from the guest or from a default (1 MED, 1 LOW-MED)

**Instances.**
- MED: `GuestTradeHost.WithBalance` filled a null `ResourceName` with the faction resource's name.
  A null name means the purchase was paid in gold, so a gold-only guest read "for 500 Horses".
- LOW-MED: the guest built its armoury list at its own computed level. A visiting armourer's bonus
  is host state only, so the guest could see offers disabled that the host would allow.

**Why missed.** The `??` fallback treated null as "missing" when it carried a meaning. The armoury
path assumed the guest's copy of town state equals the host's.

**Fix.** The fallback is gone. The reply carries the host's armoury level (wire fields 12 and 13 on
`CoopCompatTradeReply`), and the guest builds its list at that level.

**Test.** `GuestTradeHostTests.EmissaryBuy_PaidInGold_KeepsTheGoldName_NotTheResources`,
`ArmouryBalance_RepliesWithTheHerosOwnKingdomResource` (level), the relay mapping tests and the
wire contract pins.

**Prevent.** On a guest, every number the host decides (a level, a price, a balance) comes from the
host's reply. A null in a result record gets a test of its own meaning before any default fills it.

## Class 3: a request path that ends in silence (1 LOW-MED, 1 LOW)

**Instances.**
- LOW-MED: a request from a connection with no hero was dropped, and a guest whose host never
  answered saw nothing on the first click and nothing after 15 seconds.
- LOW: the "not in this settlement yet. Try again in a moment" text described a delay that does not
  happen: Coop's server sets `CurrentSettlement` in the same game-thread action that starts the
  encounter (`ServerSettlementExitEnterHandler`).

**Why missed.** The "drop a request from an unknown connection" check came from Sternab's relay,
which had no reply path for it. The text was written before the server's entry order was read.

**Fix.** The transport raises such a request with an empty hero, and the host answers it
`NotFreeLeader`. The client returns `GuestTradeSend.NoAnswer` once after the timeout, with its own
text. The refusal text now names the settlement, battle and siege cases.

**Test.** `CoopTransportTests.TradeRequest_FromAConnectionThatStandsForNoHero_IsRaisedWithNoHero...`,
`GuestTradeHostTests.AConnectionThatControlsNoHero_IsAnswered_NotFreeLeader`,
`GuestTradeClientTests.AnUnansweredRequest_PastTheTimeout_IsReportedOnce...`.

**Prevent.** Every request a player sends ends in a reply or a visible notice; a drop is allowed only
for a sender that cannot be answered.

## Class 4: tests that pin one side of a mapping, and a seam with no wiring test (2 MED, 3 LOW)

**Instances.** The relay's host-to-wire and wire-to-request mapping had no test that fails when a
field is dropped or swapped (MED). No test resolved `GuestTradeModule`'s graph, which CoopCompat
resolves inside the try that guards its transport (MED). The client's no-link, throwing-link and
empty-id paths, and the host's no-resource path, had no test (LOW). `GuestTradeModule` used
`RegisterDelegate` on the belief that DryIoc needs it for a class with an internal test constructor;
the precedents `FordsRunReport` and `CoopSessionProvider` show it does not (LOW).

**Why missed.** The tests were written per class. The relay test used a host that refused everything,
so no field beyond the refusal crossed the wire.

**Fix and test.** `GuestTradeRelayServiceTests.AnUpgradeRequest_MapsEveryFieldIn_AndEveryReplyFieldOut`
and `ABuyRequest_MapsSubjectAndQuantityIn_AndTheResultOut`; `GuestTradeWiringTests` (plain
`Register<GuestTradeClient>` resolves); the client and host edge tests.

**Prevent.** A relay or adapter between two formats gets one test per direction that asserts every
field, through a collaborator that returns a full result.

## Design changes from the second wave (no defect)

- The host's 64-entry reply cache is deleted: every send has a new GUID and the `NoAnswer` path
  sends nothing, so no TAOM client repeats an id. A repeated id now runs twice; no caller produces
  one.
- One `GuestTradeTexts.AskHost` replaces the copy in each presenter.
- `ArmouryPlayerAdapter.ChangeRelationWith` makes one engine call for both heroes; both engine
  entries reach `ChangeRelationAction.ApplyInternal`.
- `IGuestTradeShops.ArmouryOffers` takes the level, so an upgrade reads it once.
- Comments that narrated the change (ADR-009) are removed; the adapter interface docs now name the
  hero a `ForHero` instance acts for.

## Not applied

- Unit tests of the presenters' guest branches: the presenters are thin Gauntlet code with no
  seam. The routing flags (`IsGuest`, `IsGuestWithoutLink`) and `AskHost`'s parts are tested on the
  client; step 4 of the in-game check covers the screens.
- A null guard on `ForHero`, and a "still in the town" check on a late reply: no current path needs
  either, and the host re-checks the settlement before any charge.
- `CoopCompatProtocol.ProtocolVersion` stays 1. A host on an older `TAOM.CoopCompat` has no trade
  handler, so the guest sees `NoAnswer`; the join handshake already reports the build mismatch.

## Owed

- The Codex tier 1 pass (skipped on the maintainer's word, 2026-11-15).
- The two-client in-game check, `coop-compat.md` "How to verify in game" step 4.
- The language rows for the new keys (the lead's language pass).
