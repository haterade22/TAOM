# RCA — AlignmentDesertion deep review (2026-06-27)

## Top-line

Deep review of the new **AlignmentDesertion** feature: 5 core agents (standards, compatibility,
efficiency, completeness, data-flow), each finding adversarially verified by an independent Sonnet
skeptic. **8 findings raised; 5 refuted as NOT_A_BUG on re-read; 3 confirmed, all LOW** after severity
correction (two were initially raised CRITICAL/HIGH and downgraded by the verifier). Zero HIGH/MEDIUM.
Standards, API compatibility (every TaleWorlds signature verified against installed v1.4.6 DLLs), the
`gondor`/`mordor` culture resolution, all 6 MCM toggles, the decision matrix, and the behavior↔service
POCO mapping all verified correct end-to-end.

One confirmed finding was a genuine code/test gap and was **fixed in-session**; the other two are
process items (localization propagation, GitHub issue number) that were already surfaced to the user.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|-----|---------|----------|------------|-------------------|
| 1 | LOW | `AlignmentDesertionConfigProvider` had no test class — the "Config Providers MUST Validate → one test per validation rule" mandate was unmet (Rate above-1 / below-0 / NaN / Infinity / valid). | Testing & QA | During TDD I drove the *service* tests (RED→GREEN) and treated the config provider as boilerplate copied from a sibling, so I skipped its dedicated test class. The rule is documented but applies to the *provider*, not the service I was focused on. | **FIXED** — added `AlignmentDesertionConfigProviderTests` (12 tests: per-validation-rule rate cases + missing/malformed/empty/cache). Validation code itself was already correct. |
| 2 | LOW | New player-facing string `{=taom_align_desertion}` exists only in English; absent from the 12 translated locale files. (Raised CRITICAL; verifier downgraded — engine falls back to English, no crash.) | Localization | Localization propagation is a deliberate post-implementation pipeline step (`/localize` → `translate_with_claude.py`), explicitly deferred and flagged to the user, not an oversight. | Run `/localize` before ship (standing follow-up, already surfaced). |
| 3 | LOW | Feature doc lists the GitHub issue as "pending — open before the closing commit"; no issue number yet. (Raised HIGH; verifier downgraded — this is the intended pre-close state per CLAUDE.md.) | Process | The obligation fires at the *closing commit*, which hasn't happened (user runs git only on request). The doc's placeholder is the prescribed reminder text. | Open the issue before the closing commit (user's call — public artifact). |

## Refuted findings (NOT_A_BUG on adversarial re-read)

| Dim | Finding | Why refuted |
|-----|---------|-------------|
| efficiency | "`Rate` read per-troop inside the loop" | Factually wrong — `var rate = _settings.Rate` is cached to a local *before* the foreach; `_settings` is never touched in the loop. |
| efficiency | "`Math.Min` cap is redundant" | The finding itself says "not a correctness bug"; the cap is intentional defensive programming (rate pre-validated to [0,1]). |
| compatibility | "Shared id→side dict could collide kingdom vs culture ids" | Hypothetical future hazard; the 2 real mismatches (gondor/mordor) have explicit matching entries; design is documented in the interface XML doc. |
| compatibility | "`Clan.PlayerClan` null-deref during early ticks" | Tick events are raised by the campaign dispatcher — structurally cannot fire before `Campaign.Current` is set; vanilla uses the identical unguarded pattern; reference-equality wouldn't throw even on null. |
| dataflow | "null-`Town` path untested" | Entry-point I/O; `?.` + null guard handles villages correctly; architecture exempts entry points from coverage. |

## Why each agent's first pass missed finding #1 (the one real gap)

- **Standards / Compatibility / Efficiency** — out of scope; they review the provider's *code*, which was correct. A missing *test* isn't a code defect.
- **Completeness** — checked "tests exist for the service" (they do, 20) and "feature doc / CHANGELOG / IoC" (all present) but did not enumerate "every validating config provider has a `*ConfigProviderTests`." It reported the feature COMPLETE on the service-test axis.
- **Data Flow** — actually caught it (traced the `Rate` validation path and noticed no provider-test file). This is consistent with the skill's note that Data Flow is the highest-value agent: it found the only confirmed code/test gap that the dedicated Completeness agent missed.

## Lesson (Testing & QA)

A validating config provider copied from a sibling inherits the *code* but not the *tests*. When TDD
focuses on the service, the provider's mandated per-validation-rule tests get skipped because the
provider "looks done." The fix is mechanical: every `*ConfigProvider` with a `Validate(...)` method gets
a `*ConfigProviderTests` in the same PR, one test per rule (plus missing/malformed/empty/cache), mirrored
from `RecruitmentAlignmentConfigProviderTests`. Appended to `LESSONS-LEARNED.md` (Testing & QA).

## Verification

`dotnet test TAOM.Tests --filter AlignmentDesertion` → green (service 18 + GetCultureSide 5 + ConfigProvider 12).
No HIGH/MEDIUM findings; no deferrals requiring a GitHub-issue / commit-trailer / CHANGELOG record.

## Codex adversarial pass (2026-06-27)

Independent Codex (gpt-5.5, xhigh) review after the 5-agent deep-review + commit `ac20b2d7`. Verdict:
**0 CRITICAL, 0 HIGH, 1 MEDIUM, 2 LOW** — all 3 confirmed against source + installed v1.4.6 DLLs and
fixed in follow-up. Codex independently re-derived the full decision matrix, verified the TroopRoster
mutation + DailyTick lifecycle against the engine, and cross-referenced every alignment id (no typos).

| # | Sev | Finding | Category | Why missed | Fix |
|---|-----|---------|----------|------------|-----|
| C1 | MEDIUM | `OnDailyTickParty` gated only on `LeaderHero.Clan.Kingdom`, so it processed **any** hero-led party with a kingdom — incl. player/AI **caravans** (companion-led → clan + kingdom present). Contradicts the "lords' field parties" MCM hint; risked silently shedding caravan guards. | Missing vanilla gate (party-type) | `DailyTickPartyEvent` fires for **every** `MobileParty` (lord/caravan/garrison/villager/militia/bandit). My behavior + its comment *assumed* only lord parties have a hero-led clan — false for caravans. The deep-review Data Flow agent traced "bandit/caravan excluded" but accepted the same false premise instead of checking the party-type flags. | Gate `if (!party.IsLordParty && !party.IsMainParty) return;`. Also definitively excludes garrison parties from the party path (they desert via `OnDailyTickSettlement`), closing a latent double-processing path. |
| C2 | LOW | Service comment claimed mercenary owners are exempt ("no purge"), but a mercenary clan keeps `Clan.Kingdom = employer`, so it resolves to the employer's side and **does** purge. | Stale/incorrect comment | Wrote the comment from an assumption about mercenary clan state without decompiling `StartMercenaryServiceAction`. | Corrected the comment; behavior (mercenary lord parties shed opposed troops, fighting for their employer's side) is intended and kept. |
| C3 | LOW | Rate `0` still shed 1 per opposed type (min-1 floor fired at 0%); a 0% slider reads as "off" to a user but wasn't. | Logic / UX semantics | Designed the min-1 floor for small stacks at normal rates; didn't consider rate exactly 0 as a distinct "no desertion" intent (treated the master toggle as the only off switch). | Added `if (rate <= 0f) return result;` before the loop; pinned by `CalculateDesertion_RateZero_ReturnsEmpty`. |

### Lesson (Campaign Mechanics) — `DailyTickPartyEvent` fires for ALL party types; filter explicitly

`CampaignEvents.DailyTickPartyEvent` (and `MobileParty.All` iteration generally) yields **every** mobile
party — lord, caravan, garrison, villager, militia, bandit. A handler that means "lord field parties +
the player" must gate on `party.IsLordParty || party.IsMainParty`, NOT on a proxy like "has a hero
leader with a kingdom" (caravans satisfy that). Likewise a garrison handled via `DailyTickSettlementEvent`
must be excluded from the party path or it double-processes. Appended to `LESSONS-LEARNED.md` (Campaign
Mechanics). Why the deep-review missed it: the Data Flow agent reasoned about *which clans* are excluded
(kingdomless/bandit) but inherited the same "only lords are hero-led" premise the code's own comment
asserted — it never enumerated the party-type flags. Codex caught it by decompiling `CaravanPartyComponent`.

### Follow-up verification

Build green; `--filter AlignmentDesertion|AlignmentServiceTests` → **82 passed, 0 failed** (+1 rate-0 test).
Fixes land in a follow-up commit (the original `ac20b2d7` is already pushed; not amended).
