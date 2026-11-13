# RCA: co-op player identity audit fixes (#1141), 2026-11-13

Tier 1 deep review of the worktree `E:/repos/taom-mainhero-audit` (branch `fix/coop-mainhero-audit`, base 3da5883b5): Lens 5 Data flow and Lens 2 Engine compatibility, one wave, no HIGH, so no second wave. Codex did not run (out of usage). The change routes three sites that meant "the player" as `Hero.MainHero` or `Clan.PlayerClan` through `IPlayerControlQuery`. The review found two MEDIUM and nine LOW items. All are fixed or documented in the same change, except the follow-ups named below.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| R1 | MED | `TaomPregnancyModel` reimplements `GetDailyChanceOfPregnancyForHero` without `base`, so Coop's prefix on the vanilla method (which asks `IsPlayerHero`) never ran. A co-op guest's marriage got the AI clan brake and the AI race bonus. The audit had marked this site "vanilla parity". | co-op identity | The audit compared TAOM with vanilla only. Under Coop the reference is vanilla plus Coop's patches on it. | Fixed: `IsPlayerMarriage` asks the query; `Unknown` is not a player there. The general case (16 models) is #1151. Lesson in `campaign-mechanics.md`. |
| R2 | MED | The War Chronicle rule "a player's realm gets no rally effects" read `kingdom.RulingClan == Clan.PlayerClan`, so a realm a guest rules got rally effects, including the escape multiplier the fixed escape pass reads. | co-op identity | #1141 grepped `Hero.MainHero` only; the rally compares with `Clan.PlayerClan` through a local variable. | Fixed: `KingdomWarSnapshotAdapter` asks `PlayerClanCheck`, shared with the escape adapter. |
| R3 | LOW | Skipping a guest in the Realms offices pass and `GrantToChildren` would leave the guest's offices empty and the clan's children without titles, because no Realms player action reaches the host from a guest. | design | The audit treated every "skip the player" as safe; it is safe only where the player can act instead. | Narrowed: only the AI claims (which spend the clan's influence and gold) skip a guest. Recorded in the `RealmsCampaign.IsAPlayersClan` doc and the `coop-interop.md` Realms row. |
| R4 | LOW | The Realms query parameter was optional and the helpers fell back to the local comparison, so a dropped argument compiled, passed every test and silently restored the old rule. The fallback also made the server's own clan a player's for Realms but not for the escape pass. | wiring | Written so the existing tests compiled unchanged. | The parameter is required and the fallback is gone; the one other test passes a substitute. |
| R5 | LOW | The escape adapter's comment said an extra escape for a player is a write outside Coop's captivity flow. `ApplyByEscape` goes through `ApplyInternal`, which Coop handles on the server. | doc claim | Reasoned from the name, not the path. | Comment rewritten: the extra roll is help for AI lords, so a player gets none. |
| R6 | LOW | The Realms comment said the query's answer "needs no campaign". Only the test fake does: the production query reads `Clan.PlayerClan`. | doc claim | Stated from the test, not the implementation. | Removed with R4. |
| R7 | LOW | The escape contract (`IPrisonerEscapeAdapter.Escape`) and `PrisonerEscapeSnapshot.IsPlayerClan` still described "the main hero" and "the player's clan". | stale text | The change edited the implementation only. | Updated. |
| R8 | LOW | The `Unknown` table in `coop-interop.md` had no rows for the new callers. | stale text | Same as R7. | Rows added for the escape and rally, the Realms claims and pregnancy. |
| R9 | LOW | "Weekly" was wrong for `GrantToChildren` (daily and at title creation). | doc precision | Copied from the claims pass. | Moot after R3; the test doc now names the claims only. |
| R10 | LOW | The unit tests prove the helpers, not the call sites: putting the old comparison back at a call site fails no test. | test strength | The call sites read sealed engine types in loops that need a campaign. | FOLLOW-UP no issue: each call site is one token, read by both lenses; a source gate here would be a large allowlist for little gain. Named in the commit body. |
| R11 | LOW | The two volunteer boundaries (`TaomVolunteerModel`, `CastleNotableMaintainer`) still compare with `Clan.PlayerClan`, so a guest's fiefs get the `VolunteerRate` multiplier. The caravan flag is wrong for guests only when `applyToPlayerCaravans` is off (default on). | co-op identity | Same grep gap as R2. | FOLLOW-UP #1141: listed there as the remaining `Clan.PlayerClan` sites; `war-chronicle.md` names the volunteer limit. |

Sites confirmed as parity and left unchanged: `RefugeService.ReadRefugePrisonerAt` (vanilla's peace release, which Coop leaves to the server, skips only `Hero.MainHero`), `CastleRecruitObserver` (a diagnostic).

## Root-cause pattern

R1 and R2 share one cause: the audit asked "does TAOM match vanilla?" and "does the code name `Hero.MainHero`?". Under Coop the reference behaviour is vanilla plus Coop's own patches, and "the player" also hides behind `Clan.PlayerClan` and local variables.

## Why each agent missed these

- **The #1141 audit agent** classified the pregnancy and Refuge sites by vanilla parity only, and grepped one identifier.
- **Lens 2 Engine compatibility** found R1, R5, R6 and the R4 asymmetry by reading Coop's installed patches.
- **Lens 5 Data flow** found R2, R3, R4, R7 to R11 by following the War Chronicle rule to every consumer.

## Feedback memories to codify

None: the lesson goes to `docs/reviews/lessons/campaign-mechanics.md`.
