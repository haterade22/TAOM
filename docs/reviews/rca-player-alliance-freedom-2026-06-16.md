# RCA — Player Alliance Freedom (2026-06-16)

Deep-review (5 core agents) of the PlayerAllianceFreedom feature: player-founded kingdoms can now form alliances (initiate via dialog + receive AI offers + the vanilla Kingdom→Diplomacy button is unblocked). One confirmed HIGH finding, fixed in-session. The other agent flags were a duplicate ID (the HIGH), plus disputed/overstated items recorded below for completeness.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | HIGH | Duplicate `<string id="taom_alliance_formed">` — a pre-existing entry (line 371, vanilla-harvested, `{KINGDOM1_LINK}/{KINGDOM2_LINK}`, key `{=0cdXddA9}`) collided with the new dialog-notification entry (line 820, `{PLAYER_KINGDOM}/{TARGET_KINGDOM}`). Duplicate ids shadow each other; the dialog message risked rendering the wrong text with unset variables. | Localization / Verify-Before-Reference | When adding the new key I did not grep the strings file for the chosen id first. The `taom_alliance_*` cluster *looked* free because the nearby keys (`taom_alliance_lore`/`_blocked`/`_war_blocked`) were unique — but `taom_alliance_formed` already lived 449 lines earlier in the file. | Fixed: renamed the new key to `taom_player_alliance_formed` (XML + code). Generalizable rule below (grep the string id before adding). |
| 2 | LOW (disputed) | ADR-002: `if (...) { reason = new TextObject(...); return false; }` in `TaomKingdomDecisionPermissionModel` + the `if (modifier != 0f)` label branch in `TaomAllianceModel`. | Standards | Agent 1 flagged inline `if` in GameModel bodies. | DISPUTED — not fixed. This is the established TaleWorlds permission-gate idiom (map a service bool → out `reason` + early return), identical to the file's pre-existing sibling methods (`IsWarDecisionAllowed`/`IsPeaceDecisionAllowed`) and not introduced by this change. The *decision* lives in `IDiplomacyService`; the model only formats reason/label text at the boundary, which ADR-002 permits. Agent 1 itself hedged ("debatable"). No restructure. |
| 3 | LOW (overstated) | `AllianceAdapter.FindKingdom` does `Kingdom.All.FirstOrDefault` (O(n)); `CanPlayerProposeAlliance` triggers up to 4 scans. | Efficiency | Agent 3 assumed `CanPlayerProposeAlliance` runs in AI daily-tick loops over many kingdom pairs. | DISPUTED severity — not fixed. The AI daily-tick path is the two GameModels, which use pure dictionary lookups (`GetAllianceScoreModifier`/`IsAllianceDecisionAllowed`), NOT `CanPlayerProposeAlliance`. The latter runs only from the dialog condition (per-conversation) + once on accept — ~80 comparisons per dialog refresh over ~20 kingdoms, negligible. `FindKingdom` is also pre-existing adapter code untouched by this change. Out of scope. |
| 4 | INFO (by design) | Dialog-initiated alliance costs 0 influence; the vanilla Kingdom-screen button costs ~200 influence for the same outcome. | Data flow / UX | Agent 5 flagged the asymmetry. | Accepted by design — the user explicitly chose "full freedom." Documented (not silenced) in `docs/features/diplomacy.md` + CHANGELOG so the asymmetry is intentional and discoverable, per the "no silent deferral" rule. |

## Root-cause pattern

Finding #1 is a **namespace-collision-by-prefix-assumption**: a new id was added to a 800+-line shared strings file on the assumption that a sensible-looking name in a thematic cluster was free. It was not — the same id existed far away in the file from an earlier vanilla-string harvest. This is the localization-file instance of the project's recurring "classify/verify by grep, not by assumption" lesson (cf. `feedback_classify_by_grep_not_by_assumption.md`). The CLAUDE.md "Verify Before Reference" rule already covers sprites (`read TAOMSpriteData.xml before Sprite="X"`) and prefab children; it did not explicitly extend to localization string ids.

## Why each deep-review agent missed / caught it

- **Agent 1 (Standards):** Out of scope — checks ADR compliance, not XML id uniqueness.
- **Agent 2 (Compatibility):** Out of scope — verifies TaleWorlds API signatures only.
- **Agent 3 (Efficiency):** Out of scope.
- **Agent 4 (Completeness):** Confirmed all 4 keys exist in the source XML and are referenced in code — but checked *presence*, not *uniqueness*. It correctly reported the keys present; it did not grep for duplicate ids.
- **Agent 5 (Data Flow):** **CAUGHT IT.** Its "string key → consumption" trace enumerated every `{=key}` against the XML and surfaced the two entries sharing `taom_alliance_formed`. This is the data-flow agent doing exactly what it is for — finding a cross-location gap (here, two XML lines 449 apart) that per-line review misses.

## Feedback memory to codify

`feedback_verify_string_id_unique_before_add.md` — Before adding a `<string id="X">` to a shared module-strings XML, grep the whole file for `id="X"` (and the `{=X}` key). Thematic-cluster proximity is not evidence the id is free; vanilla-harvested strings and earlier features scatter ids across the file. Duplicate ids shadow silently — no build error, no test failure, only a wrong-text-at-runtime bug that only the data-flow review or in-game testing catches. This is the localization extension of CLAUDE.md "Verify Before Reference" and a sibling of `feedback_classify_by_grep_not_by_assumption.md`.

---

## Codex adversarial review (2026-06-16)

Codex (gpt-5.5, xhigh) ran after the deep-review fix landed. It DISPUTED the core mechanic correctly (verified +1000 clears every `CanMakeAlliance` gate in both directions; confirmed the string-key fix complete; confirmed the 2-arg regression path byte-identical) and found **3 new findings**, all verified against source + v1.4.6 decompile and all confirmed real. Fixed in-session.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| C1 | HIGH | `InvolvesPlayerKingdom` (both GameModels) used `Clan.PlayerClan?.Kingdom` without checking the player *rules* it. A **vassal/mercenary** player's liege kingdom (AI-ruled) would get the +1000 score bonus AND the lore-Hostile permission bypass for its AI-driven alliance decisions — changing AI-vs-AI diplomacy, which the feature explicitly must NOT do. | Missing precondition / helper duplication drift | The dialog's `GetPlayerLedKingdom` had the correct `RulingClan == PlayerClan` check, but the two model helpers were written fresh and **diverged** from it. Deep-review Agent 5 verified `involvesPlayer` was symmetric + null-safe but accepted the "player's kingdom" definition at face value — it never asked "does this fire when the player is a vassal?" | Fixed: extracted a single `PlayerKingdomHelper.GetPlayerRuledKingdom()`/`InvolvesPlayerRuledKingdom()` (requires `RulingClan == PlayerClan`); both models + the dialog now call it. Lesson: when the same boundary predicate appears in N entry points, centralize it — divergent copies of an authorization check are a bug class. |
| C2 | MED | `GetConversationRulerKingdom` accepted any member of the ruling *clan*, not the kingdom **leader** — a non-leader royal could bind the whole realm to an alliance. | Missing precondition (membership vs rulership) | Deep-review confirmed the target gate excluded non-ruling *clans* and was null-safe, but did not distinguish ruling-clan-*member* from ruling-clan-*leader*. | Fixed: require `kingdom.Leader == hero`. Lesson: a "sovereign actor" check must compare against the leader hero, not clan membership. |
| C3 | LOW | The dialog showed the "alliance forged" message unconditionally after a `void FormPlayerAlliance`, which can no-op (eliminated/missing kingdom between condition and consequence) → false success message. | Missing post-condition verification | Happy-path modeling; the no-op-then-claim-success window wasn't traced. | Fixed: `FormPlayerAlliance` now returns `bool` (confirms `AreAllied` after the engine call); the dialog gates its message + a warning logs the no-op. Added 2 service tests (engine-forms vs engine-no-ops). |

### Root-cause pattern

C1 and C2 are the **same shape**: an identity/authorization predicate that is too loose — "is a member of the player's/ruling kingdom" instead of "*rules* it." C1's specific trigger was **helper duplication**: three boundary spots needed "does the player rule kingdom X," the dialog got it right, the two models got it wrong, and nothing forced them to agree. The fix (one shared `PlayerKingdomHelper`) removes the drift surface for both the bug and any future recurrence. This is the same lesson as the deep-review's string-id finding from a different angle: **a predicate/id that must be consistent across call sites should have exactly one definition.**

### Why deep-review's 5 agents didn't catch C1/C2

- Standards/Compatibility/Efficiency agents are out of scope for authorization semantics.
- Completeness verified tests exist for the service methods (which are correct) — the bug is in the *model boundary* helpers (entry points, not unit-tested per ADR-008).
- Data-flow (the closest) verified `involvesPlayer` was symmetric + null-safe and that AI-vs-AI was unchanged *for the AI-vs-AI argument path* — but it reasoned about "the player's kingdom" as the founded-kingdom happy case and didn't enumerate the vassal/mercenary player state. The miss is a **state-enumeration gap** on the player's own faction role (ruler vs vassal vs mercenary), analogous to the entity-state-matrix rule but applied to the player's political status rather than a Hero lifecycle.

### Note on test coverage for C1/C2

The fixed predicates live in `PlayerKingdomHelper` / the dialog behavior, both of which read `Clan.PlayerClan` / `Hero.OneToOneConversationHero` (campaign statics, not mockable in the unit harness). Per ADR-008 these boundary checks are verified in-game, not unit-tested — so the preventive value is the single-source-of-truth helper, not a new unit test. In-game check added to the verification list: confirm a *vassal* player does NOT see/trigger the freedom bypass, and the dialog line appears only when talking to the actual kingdom leader.

---

## Follow-up (2026-06-17): player alliances vanish from the encyclopedia (durability)

**In-game report (post-ship):** a player who founded their own kingdom formed an alliance via the Kingdom→Diplomacy button, but the encyclopedia showed no ally shortly after; also "no alliance missions and no diplomacy options."

**Root cause (decompile-verified, v1.4.6 — the linchpin read directly):** vanilla `AllianceCampaignBehavior.OnWarDeclared` (AllianceCampaignBehavior.cs:678-681) calls `EndAlliance` the instant war is declared between two allied kingdoms. TAOM's `AllianceCampaignBehavior_EndAlliance_Patch` blocks that end **only for `Permanent`-tier** pairs. A player-formed alliance defaults to `Neutral` tier → unprotected → any war on the pair silently dissolves it. The only other `EndAlliance` caller (line 654-656) is the expiry path, gated on `EndTime.IsPast`; TAOM's `MaxDurationOfAlliance => 100 years` keeps it dormant. So the **war declaration is the only realistic auto-break.**

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| F1 | HIGH | A player-formed alliance is silently auto-broken: vanilla `OnWarDeclared → EndAlliance` ends it on any war declared on the pair, and TAOM's end-protection is scoped to `Permanent` tier only — player alliances (`Neutral`) are unprotected. | Protection scoped to one tier misses a newly-introduced state | The player-alliance-freedom feature added a NEW way to create alliance state (player-formed, `Neutral` tier) but reused the *durability* model designed for the lore `Permanent` alliances — which doesn't cover the new instances. The original work focused on *forming* alliances (score + permission bypass) and never asked "what auto-UNDOES this state, and does our protection extend to it?" | Fixed: `DiplomacyService.IsWarAllowed` blocks war between the kingdom the player *rules* (`IAllianceAdapter.GetPlayerRuledKingdomId()`) and a *current ally* — stopping the involuntary war at its source rather than blocking `EndAlliance` (which would trap the player's own break-alliance action). 4 unit tests; in-game confirmation via temporary `[Diplomacy][diag]` logging. |
| F2 | INFO | "No diplomacy options" (dialog never appears). | Reachability (by design) | Part B's dialog requires conversing one-on-one with a rival kingdom's *leader* (gate tightened per Codex MED-C2). In normal play you rarely meet a rival ruler. | Not a bug — **user decision: keep leader-only**; the always-reachable Kingdom-screen button is the primary path. Documented in the feature doc. |
| F3 | INFO | "No alliance missions." | Downstream | Vanilla call-to-war / ally content has nothing to trigger on if the alliance doesn't persist. | Expected to resolve once F1 holds; confirm in-game. |

### Root-cause pattern (the systemic lesson)

**A new mechanism that creates engine state inherits the durability of the LEAST-protected existing category, not the one you mentally file it under.** Player alliances are conceptually "the player's deliberate choice = should stick," but mechanically they're `Neutral`-tier alliances, and TAOM only hardened `Permanent`-tier alliances against the engine's auto-break. When you add a feature that *creates* engine state (an alliance, a stance, a settlement override), audit the full lifecycle of that state — specifically **what vanilla systems can UNDO it, and whether any TAOM protection covers the new instances** — not just the creation path. This is the lifecycle sibling of the "override-calls-base inherits the base's preconditions" lesson (`feedback_taleworlds_computed_getter_nre_route_through_chokepoint`): there it was a *crash* inheriting an unguarded base; here it's *state durability* inheriting an unscoped protection.

### Why the original deep-review + Codex missed F1

Both reviews scoped to the *forming* path (score, permission, the decision flow, the string key). Neither traced **the post-formation lifecycle** — "the alliance forms; now what in the engine can end it, and are we protected?" The deep-review data-flow agent traced `involvesPlayer` end-to-end through `CanMakeAlliance` (formation) but stopped at "alliance forms = success"; it never asked "does it *persist*?" — the exact "is the field populated vs are non-empty values actually produced" gap the data-flow prompt warns about, applied one step later (formed vs stays-formed). The fix lives on a path (`IsWarAllowed` / `OnWarDeclared`) that wasn't in the feature's original change scope, so a change-scoped review couldn't reach it.

### Diagnostics (temporary)

`AllianceCampaignBehavior_StartAlliance_Patch` (Postfix) logs player-involved alliance formation on any path; the `EndAlliance` patch logs player-involved end attempts; `IsWarAllowed` logs protective war blocks. These confirm form → war-blocked → no-end in the player's log and disambiguate form-then-break (F1, fixed) from never-persist (would re-scope to the formation path). **Strip after in-game sign-off** (`feedback_comprehensive_diag_logging_then_remove`). NOTE: this follow-up has NOT yet been through `/deep-review` + `/review-codex` — that gate is deferred until in-game logging confirms F1 is the actual cause, per the Iron Law (no fix declared done without root-cause confirmation).

### Feedback memory to codify

`feedback_new_engine_state_audit_what_undoes_it.md` — when a feature *creates* engine state (alliance/stance/override), audit what vanilla systems auto-UNDO it and whether existing TAOM protection (often scoped to a specific lore tier / category) extends to the new instances. New state inherits the least-protected category's durability, not the one you associate it with.

---

## Codex review of the durability fix (2026-06-17) — fix REVERTED to diagnostics-only

The durability fix above (block the involuntary war via `DiplomacyService.IsWarAllowed`) went through `/deep-review` (5 agents, READY) then `/review-codex` (gpt-5.5 xhigh). Deep-review's efficiency flags were dismissed on code re-read (the `AreAllied` scan is short-circuited; the diag string is gated). **Codex found 2 HIGH the deep-review missed, and the verdict was to REVERT.**

| # | Sev | Finding | Verified? | Outcome |
|---|-----|---------|-----------|---------|
| C1 | HIGH | **Soft-lock.** Blocking war between the player and a current ally removes the player's only exit from an alliance. v1.4.6 has **no "break alliance" UI** — `KingdomDiplomacyVM` exposes only propose-Alliance / declare-War / declare-Peace / TradeAgreement; the player ends an alliance by *declaring war on the ally* (`OnDeclareWar` → `DeclareWarDecision` → `DeclareWarAction.ApplyByKingdomDecision` → `OnWarDeclared` → `EndAlliance`). The fix blocked that at both `TaomKingdomDecisionPermissionModel.IsWarDecisionAllowedBetweenKingdoms` and the `DeclareWarAction` prefix → player trapped for ~100 years. | **CONFIRMED** by me: decompiled `KingdomDiplomacyVM` (no break-alliance action) + grepped all 3 `EndAlliance` callers (all internal: expiry/war/daily-cleanup). | **Reverted the war-block.** |
| C2 | HIGH | **Call-to-war atomicity.** `StartCallToWarAgreement` commits the agreement + gold transfer + `OnCallToWarAgreementStarted` + bonuses *before* `DeclareWarAction.ApplyByCallToWarAgreement`. Blocking the war at `ApplyInternal` leaves a paid agreement with no war. | CONFIRMED via Codex's pasted v1.4.6 decompile (side effects precede the war call). | Mooted by the revert. |
| C3 | LOW | Player-ruled predicate duplicated in `AllianceAdapter.GetPlayerRuledKingdomId` + `PlayerKingdomHelper` — the same drift shape as review 54's vassal-bypass bug. | CONFIRMED. | Mooted by the revert (`GetPlayerRuledKingdomId` removed — no consumer remained). |

**Decision (user-confirmed): revert the war-block, ship diagnostics-only.** Reverted `IsWarAllowed` to its prior behavior, removed `IAllianceAdapter.GetPlayerRuledKingdomId` (no remaining consumer) + its 4 tests. Kept only the `[Diplomacy][diag]` logging (StartAlliance Postfix + EndAlliance line). One play session's log will confirm whether the alliance is form-then-broken or never-persists; the targeted fix is written *then*, against the confirmed cause.

### Root-cause pattern (why the war-block was wrong)

Two compounding mistakes: (1) **I fixed an unconfirmed root cause** — the diagnostics that would confirm form-then-broken vs never-persist had not yet run in-game, yet I shipped a behavioral fix anyway, violating the Iron Law I'd written into the plan ("diagnose first"). (2) **Blocking a state transition trapped the entity.** "Protect the alliance" was implemented as "block the war," but the war was the player's *only* exit — so the protection became a cage. **When you block a transition to protect state, enumerate every exit the entity had and confirm at least one deliberate exit survives.** This is the inverse of `feedback_new_engine_state_audit_what_undoes_it` (which asks "what undoes this state?"); here the answer ("the war") was *also the only sanctioned exit*, so removing it created the soft-lock.

### Why each reviewer landed where it did

- **Deep-review (5 agents): MISSED C1 + C2, and actively asserted the opposite.** The data-flow agent claimed "the player can break the alliance via the vanilla Break Alliance UI → EndAlliance directly" — there is no such UI. I relayed that unverified to the user (evidence-over-claims §A.4 violation: a confident subagent claim relayed without spot-verifying the load-bearing fact). The agents reasoned about the war-block in isolation (does it block the right wars? is it efficient?) and never asked "after this blocks the war, can the player still leave the alliance at all?" — a missing **exit-survival** trace.
- **Codex: CAUGHT both**, by decompiling `KingdomDiplomacyVM` to enumerate the actual player actions (no break-alliance) and `StartCallToWarAgreement` to find the side-effect-before-war ordering. Same strength as prior reviews: independently decompiling to settle a load-bearing assumption instead of asserting it.

### Feedback memory to codify (this round)

Extend `feedback_new_engine_state_audit_what_undoes_it.md` with the **exit-survival** corollary: before blocking a transition to *preserve* state, enumerate the entity's exits and confirm a deliberate one survives (don't turn protection into a soft-lock); and **don't ship a behavioral fix for an unconfirmed root cause** — diagnose first when the trigger isn't reproduced.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
