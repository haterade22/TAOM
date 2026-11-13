# RCA: Co-op client set up the initial alliances while it built its join campaign (#1136, deep review 2026-11-12)

## Top-line

A first-time co-op joiner builds a local campaign for character creation before it loads the host's save. TAOM's
`DiplomacyBehavior` ran its new-game step there and called `StartAlliance` 38 times. On a client, Coop's
`StartAlliance` prefix asks the server instead of forming the alliance, and each request stopped at Coop's kingdom id
lookup: 38 `Failed to get id for object of type TaleWorlds.CampaignSystem.Kingdom` errors in `Coop_client.log` and 38
TAOM warnings that blamed an unregistered behavior. The host's save already carries the alliances, so the client now
skips the step. The tier 1 deep review (lens 5 data flow and lens 2 engine compatibility, at max effort; Codex out of
usage, Mike 2026-11-12) confirmed 1 MEDIUM and 2 LOW findings. All are fixed in this change except the doc rows in
finding 3, which are a follow-up.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | The first fix gated on `ShouldDeferToHost`. With a co-op mod TAOM cannot probe (BannerlordTogether, a forced flag, a failed Coop bind), that is true in solo too, so a new solo campaign got no initial alliances, and the log line "the host's save carries them" was false there | Wrong predicate for a creation step | The sibling `OnSessionLaunched` gate in the same file reads `ShouldDeferToHost`, and the fix copied it. That gate yields a repair on a loaded save; this step creates state that only the host's save would replace | Gate on `IsCoopClient`; new test `Diplomacy_OnNewGameCreatedFollowUp_UnprobeableCoopModWithNoSession_EstablishesInitialAlliances` (red on the first fix, green now); the doc row names the predicate per handler and says why |
| 2 | LOW | The test and code comments said Coop sends one request per alliance to the server. In every run read, no request left the client: the kingdom id lookup failed first | Comment states an effect that does not happen | Written from the Coop prefix alone, before the handler's id lookup was read | Comments now say the prefix asks the server and that today each request stops at the id lookup |
| 3 | LOW | The `NamedCompanion`, `BannerInjection` and `CultureMarketplace` rows in `coop-interop.md` still say their new-game work "runs only while a campaign is built", without naming the joiner's local campaign | Docs drift | Out of this change's scope; lens 5 found it on a sweep of sibling rows | Follow-up: lens 5 found no code gate needed (Coop blocks or ignores their client actions); one clause per row |

## Root-cause pattern

TAOM has two co-op predicates that agree on BannerlordCoop and disagree on a co-op mod TAOM cannot probe.
`ShouldDeferToHost` yields a decision to the host, so it is safe to be wrong toward "yield" on a peer that only repairs
or vetoes. A step that creates state is the opposite case: yielding there loses the state when no host exists. The
first fix chose the predicate by copying its neighbour in the same file, not by reading the truth table for its own
case.

## Why each agent missed these

- **The first fix:** copied the gate from `OnSessionLaunched` in the same class.
- **Lens 5 (data flow):** found findings 1, 2 and 3 by tracing the predicate through `CoopSessionPolicy` for every
  row of its truth table and by reading Coop's `AllianceHandler`.
- **Lens 2 (engine compatibility):** confirmed that index 0 fires once per new campaign, that a joiner builds that
  campaign, and that the lambda registration matches the method group; it found finding 2 independently and flagged
  the unprobeable-mod row for the design call.

## Changes

- `Main/Features/Diplomacy/DiplomacyBehavior.cs`: `OnNewGameCreatedFollowUp(int)` returns before
  `EstablishInitialAlliances` when `_coop.IsCoopClient`; the listener is a lambda so tests can call the step.
- `TAOM.Tests/Features/CoopInterop/CoopAuthorityGateTests.cs`: four tests (Coop client, Coop host, solo with steps
  0 and 1, unprobeable co-op mod with no session).
- `docs/features/coop-interop.md`: the `DiplomacyBehavior` row.
- `docs/reviews/lessons/state-lifecycle-save.md`: one lesson.

## Verification

- TDD: the client test was red before the first fix (compile error, then a mutant `if (false)` turned it red); the
  unprobeable-mod test was red on the `ShouldDeferToHost` gate and green on `IsCoopClient`; a mutant back to
  `ShouldDeferToHost` turns it red again.
- Co-op tests: 225 passed, 0 failed (`--filter FullyQualifiedName~CoopInterop|...CoopGateDocCoverageTests|...CoopVetoClassificationTests`).
- In game, CoopMcpServer, one Host and one client on save `taomcoop157`, Coop DEBUG build with PRs #4668 and #4669:
  - Run 4, TAOM without the fix: the client logged 38 Kingdom id errors, and the `[DEBUG-k1ng]` stack named
    `DiplomacyBehavior.OnNewGameCreatedPartialFollowUp` → `EstablishInitialAlliances` → `AllianceAdapter.StartAlliance`.
  - Run 5, TAOM with the first fix (`ShouldDeferToHost`): the client logged `[Diplomacy][coop] initial alliances
    skipped: the host's save carries them` and 0 Kingdom id errors; the host logged 38 already-allied. On a
    BannerlordCoop client `IsCoopClient` and `ShouldDeferToHost` are both true, so the final predicate takes the same
    branch there; the final build's own run is recorded in the commit.
