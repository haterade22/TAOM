# RCA — PartyBase.get_Owner NRE: deterministic new-campaign CTD (v2.0.8.0, crash 0b462fd8)

**Date:** 2026-07-02
**Branch:** `hotfix/party-owner-getter-nre` (off `bannerlord-1.4.5`)
**Trigger:** player crash bundle `taom_crash_20260702_025643_0b462fd8` — new game → campaign map → CTD on the first settlement daily-tick wave (Summer 1 1084, day 0).

## Top-line summary

Commit `9034e5dc` (2026-06-26, career pip-bonus wiring) added a `GetDailyHealingHpForHeroes` override that resolved the career-passive hero via `party?.Owner`. `PartyBase.get_Owner` is a **throwing computed getter**: for a non-mobile (settlement) party it returns `Settlement.Owner => OwnerClan.Leader` with no guard, and `Settlement.OwnerClan` returns null for any settlement that is neither Village, Town, nor Hideout. TAOM_Map has exactly one such settlement — `retirement_retreat` (the lone `CustomSettlementComponent` among 988; its `RetirementSettlementComponent.MapFaction => null` and `OwnerClan` → null). The engine's `PartyHealCampaignBehavior.OnDailyTickSettlement` feeds **every** settlement's `PartyBase` into the healing model daily, so every campaign on v2.0.8.0 crashed within its first in-game day.

The same `party.Owner ?? party.LeaderHero` pattern existed at **7 sites** (6 career-passive resolutions + `CultureFeatAdapter.ResolvePartyCulture`'s owner limb). Only the healing site was live — the data-flow review verified the other 5 model sites' engine callers never pass settlement parties, and the `ResolvePartyCulture` limb was reachable only via the `HasFeat` patch with a settlement party.

**Fix:** `CareerPassiveHero.ResolveId(party) => (party?.MobileParty?.Owner ?? party?.LeaderHero)?.StringId` (safe accessor `MobileParty.Owner => _partyComponent?.PartyOwner`; owner-first order preserved) at all 6 passive sites; `ResolvePartyCulture` owner limb → `party.MobileParty?.Owner?.Culture`. **Prevention:** `PartyOwnerGetterBanTests` — raw-IL scan of every method body in TAOM.dll asserting zero `PartyBase.get_Owner` call sites (RED at 7 pre-fix, GREEN post-fix).

## Findings table

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| 1 | CRITICAL | `TaomPartyHealingModel.GetDailyHealingHpForHeroes` calls `party?.Owner` → NRE inside the getter on the retreat's settlement party → CTD every campaign, daily | Adapters & TaleWorlds API | `?.` on the result *looks* null-safe; reviewers pattern-matched "has null-conditional → safe". The adapters.md rule ("guard the inner object, not the result", #281) existed but names `party.Culture`/`MapFaction` as examples — `Owner` was assumed to be a field. The 6-dim deep-review + Codex CLEAN pass on `9034e5dc` both accepted `party?.Owner` | Assembly-wide IL ban test (`PartyOwnerGetterBanTests`); LESSONS-LEARNED entry; adapters.md example list is not exhaustive — the classifier is "read the getter body", never "the siblings in this chain are safe" |
| 2 | HIGH (latent) | Same pattern at 5 more model sites + `ResolvePartyCulture` (the chokepoint *built by the #281 fix to be null-safe* contained `party.Owner?.Culture`) | Adapters & TaleWorlds API | The #281 fix swapped the *known* throwing limbs (`party.Culture`) and treated `Owner` as one of the safe ones — the fix for one throwing-getter bug *introduced* another instance of the same class, because nothing mechanically enforced the rule | Same IL ban test covers all sites; text grep is insufficient (see #3) |
| 3 | MED (process) | Text grep for `party.Owner` missed `TaomRaidModel.CalculateHitDamage` (`attackerSide?.LeaderParty?.Owner`) — found only by the IL scan | Testing & QA | Grep keys on the receiver's variable name; the 7th site's receiver was an expression | Ban at the IL level (call-site to `MethodInfo`), not the text level — the test *is* the grep that can't be fooled by naming |
| 4 | LOW (docs) | `docs/features/cultural-feats.md` asserted "`Owner?.Culture` … need[s] no inner guard" — the exact false claim that shipped the crash, stated as documentation | Docs | The doc was written from the #281 fix's (wrong) safety classification and never re-derived from the getter body | Fixed in this changeset; doc now states the getter body + cites this crash |

## Root-cause pattern

**A null-conditional on the RESULT of a computed engine getter reads as "handled" to every reviewer, but guards nothing when the getter throws internally.** This is the third shipping instance of the class (issue #281 `party.Culture`; the #281 fix's own `party.Owner`; this crash). The recurring blind spot: safety is classified by *syntax at the call site* (has `?.`) or by *analogy to siblings in the chain* (Hero.Culture is a field, so Owner is too), never by *reading the member's definition*. adapters.md already states the rule — what was missing was mechanical enforcement, which the IL ban test now provides for this getter specifically.

**Secondary pattern:** a fix for one instance of a bug class can introduce another instance of the same class in the same edit (the #281 chokepoint), because the fix author operates from the same wrong classifier that produced the original bug. Mechanical enforcement (a test that fails on the *class*, not the instance) is the only reliable break.

## Why each deep-review agent missed it (on `9034e5dc`, the regression commit)

That commit's 6-dim review + Codex pass returned 0 HIGH. Per-dimension:
- **Standards:** `party?.Owner` violates no ADR — it's a boundary class touching sealed types legitimately.
- **API compatibility:** verifies members *exist with matching signatures*; `PartyBase.Owner` exists. Signature verification ≠ throw-safety analysis of the getter body.
- **Efficiency:** property reads are cheap; nothing to flag.
- **Completeness:** tests existed for the pure math; the GameModel boundary is exempt from unit tests by ADR-008, so no test exercised a settlement party.
- **Data flow:** traced config→consumer flows; "which party kinds does the engine pass to this override, and does every accessor survive each kind" was not in its rule set. It is now the decisive question for any GameModel override that receives `PartyBase` (this hotfix's data-flow agent answered exactly that question per site).

## Deliberate behavior decisions (documented, not bugs)

- **Settlement parties no longer resolve a career-passive hero** (old: fief owner where the chain didn't throw). Career passives are player-hero-exclusive (verified: every `SetCareer`/`OnCareerSelected` site is `Hero.MainHero`-gated) and `settlement.Party.MemberRoster` never holds combat members (militia/garrison live in their own MobileParty rosters) — no player-visible loss.
- **`ResolvePartyCulture` for owned settlements' settlement parties** now falls to the `Settlement.Culture` field (vanilla `HasFeat`'s own final fallback) instead of the fief-owner-leader's culture. Reimplementing the owner walk safely would hand-roll the same throwing chain class this fix removes — rejected per `simplicity-criterion.md` (tiny win, real added fragility). Flagged by the data-flow review as favorable-direction.
- **`_customOwner` drop:** the only gameplay `SetCustomOwner` call site (`KillCharacterAction` — a dead player-clan companion's party gets `_customOwner = Hero.MainHero` during its disband window) is the one path where a custom owner could carry a career; passives skip there now, transiently, and the engine's own load-fixup (`PartyBase.cs:765-768`) already clears stale caravan custom owners. StoryMode's tutorial call site never runs in a TAOM sandbox campaign.

## Feedback memories to codify

One LESSONS-LEARNED entry (Adapters & TaleWorlds API): computed-getter safety is classified by reading the member definition, and mechanically banning a confirmed-throwing getter at the IL level beats re-teaching the classifier per review. No new memory file — the existing adapters.md rule is correct; its enforcement gap is what this closes.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
