# RCA: AI party size, player-clan scaling and MCM cache invalidation (2026-09-01)

Deep review of the uncommitted changeset on `bannerlord-1.4.5` adding `IsScalablePlayerLordParty` +
the `Apply Party Size To Player Clan` dropdown, `AiPartySizeSettingsWatcher`, and
`RequireRestart = false` on the seven attributes in the AI Party Size MCM group.

Five agents ran in parallel: Standards (haiku), API Compatibility (sonnet), Efficiency (haiku),
Completeness (haiku), Cross-System Data Flow (sonnet). No Codex pass.

**Verdict: 3 findings fixed, 5 rejected on evidence, 0 deferred.** Suite 7801 passed / 0 failed afterwards.

## Findings

| # | Sev | Finding | Category | Why missed | Action |
|---|---|---|---|---|---|
| 1 | MED | `EnsureSubscribed(TaomSettings.Instance)` at `SubModule.cs:796` fails silently when `Instance` is null: no retry, no log. The only symptom would be "my slider did nothing until I reloaded", which is indistinguishable from the bug the watcher exists to fix | Silent failure | Written that way deliberately (fail-safe over fail-loud) without asking how anyone would *diagnose* the safe failure | FIXED: `IModLogger` injected, warning on the null path, 4 tests |
| 2 | MED | Three `TaomSettings.Instance` reads per `ApplyAiLordScaling`, on a path that runs per party per limit recompute | Warm-path waste | I added the third read without checking that `GlobalSettings<T>.Instance` is a computed property, not a cached field | FIXED: one read, cached in a local; dead `CurrentPlayerClanScaling()` removed |
| 3 | MED | `EnsureSubscribed_RepeatedAndNullCalls_AreSafe` asserted `Assert.IsTrue(true)` | Test theatre | I assumed the handler count was unobservable without reflection. `BaseSettings.PropertyChanged` is `virtual`, so a stub can override the accessors | FIXED: 4 real tests covering stack, detach, warn, and null-after-attach |

### Rejected, with the evidence

| # | Claim | Verdict |
|---|---|---|
| R1 | HIGH: `CurrentPlayerClanId()` must use `IClanBannerAdapter.GetPlayerClanId()` (ADR-007) | **Rejected.** The adapter exists but is `Clan.PlayerClan?.StringId` with **no** `Campaign.Current` guard, and `Clan.PlayerClan` dereferences `Campaign.Current` unguarded (`Clan.cs:275`). Adopting it trades a guarded call for an unguarded one plus a cross-feature dependency (`IClanBannerAdapter` is registered in `BannerInjectionIoC`). The same file has used `Campaign.Current != null && ... == Clan.PlayerClan` since #461, through a deep review and a Codex pass |
| R2 | HIGH x3: `AiPartySizeSettingsWatcher` touches `MobileParty.All` / `Campaign.Current` from a service (ADR-007) | **Rejected as not-this-changeset.** Nine other IoC-registered services under `Main/Features/` do the same, including `RefugeService`, `SupplyCaravanService`, `SupplySourceService`, `CampVisualService`, `RefugeVisualService`, `SupplyRouteVisualService`. A codebase-wide accepted deviation is not a defect this diff introduced. Worth a real decision someday; not by ambush during a feature review |
| R3 | MED: cache the player clan id in a `private static string` | **Rejected as harmful.** The suggested code is a static mutable field on a `Reuse.Singleton` service with no reset path, which is exactly what `.claude/rules/csharp-architecture.md` "Singleton Services Holding Per-Campaign State" forbids: campaign A's id leaks into campaign B in one process. The snippet also called `Campaign.Current?.PlayerClan`, which is not the API (`Clan.PlayerClan` is static), so it was never checked against the engine |

## What the review confirmed rather than found

Worth recording, because these were the three risks flagged before launching and all three came back clean:

1. **`TroopRoster.VersionNo` bumps have no gameplay side effect.** It is `[CachedData]`, not
   `[SaveableProperty]`. Every consumer was enumerated (`PartyBase.PartySizeLimit`,
   `NumberOfMenWithHorse`, `GetNumberOfHealthyMenOfTier`, `ValidateTroopListCache`,
   `MobileParty.PartySizeRatio`, and the party speed/weight caches) and every one is a memoized
   cache-key pattern that only forces a lazy recompute. Vanilla uses the same idiom in
   `PerkHelper.ClearPerksForSkill` and `PatrolPartiesCampaignBehavior.OnBuildingLevelChanged`.
2. **No vanilla path can false-positive the takeover detection.** An exhaustive grep of the v1.4.8
   dump found exactly two files referencing `PlayerDefaultFaction`, with one write site:
   `Campaign.cs:1323`, at new-campaign init. `ChangeKingdomAction`, `ClanFoundingAction`, heir
   succession and `DestroyClanAction` never touch it. TAOM's only other writer is Player Switcher's
   takeover path; its adoption path does not.
3. **The master toggle folds the new branch.** `IsEnabled()` returns before the player/AI ternary, so
   `EnableAiPartyScaling = false` restores vanilla for player clans too.

Also confirmed: no double-scaling (`IsLordParty` and `IsGarrison` are mutually exclusive, so garrisons
never reach `ApplyAiLordScaling`); the relief GATE remains closed to player clans at every dropdown
setting; no `ResetForNewSession()` owed, since the watcher holds a process-lifetime reference and reads
campaign state fresh at fire time.

## Second pass: tailored review

The five agents above ran the skill's standard prompts and produced standard output. Asked for a
review specific to this change, a second pass posed three hypotheses aimed at what is actually unusual
here. Two were disproved; the third found the real defect, which the first pass had not only missed
but had actively asserted was impossible.

| # | Sev | Finding | Outcome |
|---|---|---|---|
| 4 | **HIGH (docs)** | The stated justification for withholding food and wage relief from player clans was **false**, and shipped into `ai-party-size.md`, `CHANGELOG.md`, a doc-comment and a test comment | FIXED in all four; **#532** filed for the design tradeoff |
| H1 | none | Hypothesis: `Clan.PlayerClan` differs per co-op peer, so peers compute different `PartySizeLimit` for the same party | **Disproved.** It is `Campaign.PlayerDefaultFaction`, a field on the shared `Campaign`, and a joining client is seeded with the host's save. Under the counterfactual the change would have *reduced* divergence anyway (6.2x gap to 0 for a taken-over clan) |
| H2 | note | Hypothesis: the TroopWeight shed now harms player-clan companion parties | **Disproved, it improves them.** Worked case (base 186, roster 1984, avg weight 1.07): before, limit deflates to 47 and the shed removes 1937 of 1984 in one tick (97.6%); after, limit is 941 and it removes 1043 (52.6%). The exemption boundary is unchanged, so the sibling asymmetry noted on #530 still stands |

### Finding 4 in detail

Withholding the relief is a legitimate balance call and is not being reversed. What was wrong was the
reason given for it. The claim was that both pressures the relief offsets are unreachable for a player
clan. Verified against v1.4.8:

- **Wage.** The AI mechanism really is blocked: `ClanVariablesCampaignBehavior:176409` guards
  `clan != Clan.PlayerClan` before `MakeClanFinancialEvaluation`. But `AddPartyExpense:56399` *also*
  skips its cash-poor floor for the player clan, so the full bill is drawn from clan gold and
  `HasUnpaidWages:56536` still drives the morale penalty once it empties.
- **Food.** `BuyFoodInternal:202604` early-returns on `IsMainParty`, which protects the player's own
  party and nothing else. `TryBuyingFood:202573` has **no clan gate**, and a player-clan companion
  party is not `IsMainParty`, so it auto-buys food and starves exactly as an AI party does.

The companion-party case is the whole point, because those are the parties this change newly scales.
At ~10 denars/head/day a scaled 941-man companion party costs ~9,400/day; #530's three non-main
parties together exceed 80,000/day.

**Why missed, and this is the uncomfortable part:** the false claim did not come from an agent. I
wrote it, from a half-verified engine read, and then repeated it four times. I checked
`BuyFoodInternal`'s first line, saw `IsMainParty`, and generalised "the player" from "the player's
main party" without opening the caller. Every subsequent restatement cited the same line number, so
the four copies looked like corroboration when they were one unchecked read. The first-pass data-flow
agent then recorded it as "airtight at the TAOM call-site level regardless" and explicitly noted it
had **not** independently re-verified it against the engine, which is the flag I should have caught.

**Prevent:** an early-return guard is evidence about the entity it names, not about the category that
entity belongs to. `IsMainParty` is not "the player". Before generalising a guard into a claim about a
whole class, open the caller and enumerate which members of the class actually reach it. And a claim
repeated across four artifacts is still one claim: it needs one verification, not four restatements.

## Root-cause pattern: the fail-safe that cannot be diagnosed

Findings 1 and 3 are the same mistake pointing two ways. In both cases the code was *correct* and the
observability was missing, and in both cases I reasoned "this is safe" and stopped, rather than asking
"if this safe path fires wrongly, how would anyone ever know?"

- Finding 1: a null-check that returns quietly. Safe, and undiagnosable, because its symptom is
  identical to the bug the class was written to fix.
- Finding 3: a test whose stated subject was idempotence and whose assertion was `true`. It would have
  passed with the `ReferenceEquals` guard deleted.

**Prevent:** when a guard silently disables a feature rather than throwing, it needs either a log line
or a test that fails when the guard is removed. "No exception" is not a passing condition for a test
about behaviour. Neither costs anything, and the absence of both is what turns a safe failure into an
unfindable one.

## Why each agent missed what it missed

- **Standards (haiku)** produced 3 HIGHs, all rejected. It correctly located a real adapter but never
  read its body, so it recommended a fix that drops a null guard; and it never checked whether the
  pattern it flagged was already ubiquitous. **A violation the codebase commits in nine other services
  is a policy question, not a review finding.**
- **API Compatibility (sonnet)** missed nothing and was the strongest pass: it enumerated the whole
  `VersionNo` consumer set and reported three items as honestly UNVERIFIED rather than guessing.
- **Efficiency (haiku)** found the right thing (finding 2) but graded it UNVERIFIED because it declined
  to decompile `GlobalSettings<T>.Instance` despite being told to. The API agent had the answer.
  **Cross-reading agents' outputs against each other resolved it; neither agent alone could.**
- **Completeness (haiku)** found finding 3 and was right to. Its suggested repair
  (`Substitute.For<BaseSettings>()` plus a handler count) would not have compiled as written, but the
  finding was sound.
- **Data Flow (sonnet)** found finding 1, the only cross-file gap, and closed the false-positive
  question by exhaustive grep rather than sampling. It correctly declined to call finding 1 HIGH.

## Lesson to codify

Appended to `docs/reviews/lessons/state-lifecycle-save.md`:

### A guard that silently disables a feature needs a log line or a test that fails without it

**Why missed:** the guard was written as fail-safe and reviewed as correct. Nobody asked how the safe
failure would be *observed*. Its symptom (an MCM change not taking effect until reload) is exactly the
symptom of the bug the guarded code fixes, so a real occurrence would be misdiagnosed as the original
defect and the guard would never be suspected.

**Prevent:** for every early-return that turns a feature off rather than throwing, require one of: a
warning log naming the feature, or a unit test that fails when the guard is deleted. Applies to
optional-dependency attach points especially (MCM, a co-op host, an absent config file).

**Source:** deep review 2026-09-01, `docs/reviews/rca-ai-party-size-player-clan-2026-09-01.md`
findings 1 and 3.
