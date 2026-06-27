# Codex Adversarial Review — AlignmentDesertion (2026-06-27)

Model: gpt-5.5, reasoning effort xhigh. Prompt: `codex-adversarial-alignmentdesertion-2026-06-27.prompt.md`.
(The full `codex exec` trace — ~34k lines of decompile tool-calls — was trimmed to keep the repo lean;
the verbatim final review is below.)

**Verdict: 0 CRITICAL / 0 HIGH / 1 MEDIUM / 2 LOW — ISSUES FOUND.** All 3 confirmed against source +
installed v1.4.6 DLLs and fixed in a follow-up commit. See `rca-alignment-desertion-2026-06-27.md`.

---

**1. Known Suspects**
1. CONFIRMED, with rate-0 caveat. `AlignmentDesertionService.cs:49` skips `Count <= 0` before count math, and `:56-57` applies `max(1, int(count * rate))` then caps at stack count. JSON rate is finite `[0,1]` via `AlignmentDesertionConfigProvider.cs:73-78`; the MCM slider is bounded `0f..1f` at `TaomSettings.cs:581-584`. Rate `0` still removes `1` from a non-empty opposed stack. That matches the feature docs/minimum-one hint, but one test comment says otherwise. See finding #3.
2. DISPUTED as written. Owner resolution is confirmed: parties use `party.LeaderHero?.Clan?.Kingdom` at `AlignmentDesertionBehavior.cs:50-56`; garrisons use `settlement.OwnerClan?.Kingdom` at `:68-75`; kingdomless owners skip. Mercenaries resolve to employer kingdom because installed engine `StartMercenaryServiceAction.ApplyStart` sets `clan.Kingdom = kingdom` before `clan.StartMercenaryService()`. The blanket caravan exclusion is false for player caravans with companion leaders. See finding #1.
3. CONFIRMED. `GetKingdomSide` and `GetCultureSide` both call the same private `GetSide` over one dictionary at `AlignmentService.cs:32-41`. No conflicting id whose kingdom and culture sides should differ. `gondor`/`mordor` are explicit culture keys in `alignment.json:3-4`.
4. CONFIRMED. AlignmentRecruitment remains kingdom-keyed: `TaomVolunteerModel.cs:47-50` passes `buyerHero.Clan.Kingdom.StringId` + `sellerHero.CurrentSettlement.MapFaction.StringId`; `RecruitmentAlignmentService.cs:32-33` calls `GetKingdomSide`, not `GetCultureSide`.
5. CONFIRMED. The behavior snapshots first (`:84-90`) and mutates later (`:101-116`). Heroes skipped in snapshot + service; vanished stacks skip via `FindIndexOfTroop < 0`; current count re-read and clamped at `:107-113`.
6. DISPUTED only for the mobile-party hint. The gates are real and player-owned is `clan == Clan.PlayerClan`. But the hint says "lords' field parties" (`TaomSettings.cs:597-598`) while player caravans with a companion leader can pass the party path.
7. CONFIRMED. No SyncData state. Events are non-serialized listeners; the engine's daily tickers persist their ticker index/debt.

**2. Engine Behavior**
TroopRoster removal used correctly: `AddToCounts(CharacterObject,int)` accepts negative counts → `AddToCountsAtIndex` subtracts and removes depleted rows at `Number==0`; `FindIndexOfTroop` returns -1 if absent; `GetElementNumber(index)` returns 0 for invalid indices. DailyTick lifecycle relied on correctly: `CampaignPeriodicEventManager` saves ticker `TickDebt`/`Index`, invokes daily party/settlement tickers from `PeriodicDailyTick`, and `Campaign.Tick` calls periodic events only after campaign ticking is active — so `Clan.PlayerClan` (deref `Campaign.Current`) is not reached before campaign init through these handlers.

**3. Decision Matrix** (code agrees with independent derivation)
Evil owner + Free troop → Desert. Free owner + Evil troop → Desert. Same-side → Keep. Neutral owner (umbar) → Keep. Neutral-culture troop → Keep. Hero → Keep. Kingdomless owner → Keep. rate=0, opposed>0 → Desert 1 (see #3). rate=1 → full stack. count=1, opposed → Desert 1.

**4. Config Cross-Reference**
No typo ids in `alignment.json` (no `rohan`, no `dol_guldur`); `alignment_desertion_config.json` has only booleans/rate. All vanilla + custom kingdom/culture ids verified to exist in spkingdoms/spcultures (XSLT + taom_*). Minor/bandit cultures (`umbar_corsairs`, `gondor_soldiers`) intentionally absent → Neutral, pinned by tests.

**5. Anything The Deep-Review Missed**
The real missed production issue is the party-type gate: player caravans with companion leaders can be processed even though docs/hints say non-lord parties early-out. Plus two LOW drift issues: a stale mercenary comment and a misleading rate-zero test comment. (Not re-reporting the known localization/pending-issue LOWs.)

**6. Findings**
| # | Severity | File:Line | Bug | Fix |
|---|---|---|---|---|
| 1 | MEDIUM | `AlignmentDesertionBehavior.cs:50` | Party path gates only on `LeaderHero.Clan.Kingdom`, so player caravans with companion leaders pass (`MobileParty.LeaderHero => PartyComponent?.Leader`; `CaravanPartyComponent.Leader => _leader`; player caravan creation passes the selected companion). Contradicts "lords' field parties". | Gate by party type, e.g. `party.IsLordParty || party == MobileParty.MainParty`. |
| 2 | LOW | `AlignmentDesertionService.cs:43` | Comment says mercenary owners "no purge", but mercenary clans keep `Clan.Kingdom` = employer and are processed. | Fix comment (or add an explicit mercenary exemption if intended). |
| 3 | LOW | `AlignmentDesertionConfigProviderTests.cs:118` | Test comment says rate `0` is a valid "no desertion" rate, but the service min-1 logic removes one opposed type when enabled. | Change the comment + pin rate-0 behavior, or make rate 0 disable. |

CRITICAL: 0 | HIGH: 0 | MEDIUM: 1 | LOW: 2
VERDICT: ISSUES FOUND
